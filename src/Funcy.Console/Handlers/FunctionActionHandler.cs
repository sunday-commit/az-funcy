using System.Collections.Concurrent;
using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Handlers.Models;
using Funcy.Console.Ui;
using Funcy.Console.Ui.Input;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Funcy.Infrastructure.Azure;
using Microsoft.Extensions.Logging;

namespace Funcy.Console.Handlers;

public class FunctionActionHandler(
    IFunctionAppManagementService functionAppManagement,
    IAzureFunctionService functionService,
    FunctionStatusManager functionStatusManager,
    FunctionStateCoordinator coordinator,
    ILogger<FunctionActionHandler> logger,
    IAzureSessionMonitor sessionMonitor) : IActionDispatcher
{
    private readonly ConcurrentDictionary<string, DispatchedFunction> _currentTasks = [];

    // Guards concurrent disable/enable toggles per function (keyed by FunctionDetails.Key).
    private readonly ConcurrentDictionary<string, byte> _togglingFunctions = [];

    private async Task AddNewTask(string name, DispatchedFunction dispatchedFunction)
    {
        _currentTasks.TryAdd(name, dispatchedFunction);
        if (dispatchedFunction.FunctionAppDetails.Status.Status != StatusType.InProgress)
        {
            await functionStatusManager.BeginOperation(dispatchedFunction.FunctionAppDetails, dispatchedFunction.Action);
        }
    }
    
    public async Task Dispatch(InputActionResult inputResult)
    {
        // A function-level toggle is intentionally kept off the app-level status/animation
        // machinery: it changes one function, not the whole app. It uses an optimistic inline
        // cue on the row instead (the "toggling" state) and its own in-flight guard.
        if (inputResult.Action == FunctionAction.ToggleDisabled)
        {
            await ToggleFunctionDisabledAsync(inputResult);
            return;
        }

        if (!_currentTasks.ContainsKey(inputResult.FunctionAppDetails.Name))
        {
            await AddNewTask(inputResult.FunctionAppDetails.Name,
                new DispatchedFunction(inputResult.Action, inputResult.FunctionAppDetails));

            _ = ExecuteActionAsync(inputResult);
        }
    }

    private async Task ExecuteActionAsync(InputActionResult inputResult)
    {
        var details = inputResult.FunctionAppDetails;

        try
        {
            switch (inputResult.Action)
            {
                case FunctionAction.Start:
                    await functionAppManagement.StartFunction(details);
                    details = await functionService.GetFunctionAppDetails(details);
                    details.State = FunctionState.Running;
                    break;
                case FunctionAction.Stop:
                    await functionAppManagement.StopFunction(details);
                    details.State = FunctionState.Stopped;
                    details.Functions = [];
                    break;
                case FunctionAction.Swap:
                    await functionAppManagement.SwapFunction(details, inputResult.SlotDetails);
                    details = await functionService.GetFunctionAppDetails(details);
                    details.State = FunctionState.Running;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            await functionStatusManager.CompleteOperation(details, inputResult.Action, true);
        }
        catch (Exception ex)
        {
            sessionMonitor.ReportPossibleAuthFailure(ex);
            await functionStatusManager.CompleteOperation(details, inputResult.Action, false);
        }
        finally
        {
            _currentTasks.TryRemove(inputResult.FunctionAppDetails.Name, out _);
        }
    }
    
    private async Task ToggleFunctionDisabledAsync(InputActionResult inputResult)
    {
        var app = inputResult.FunctionAppDetails;
        var function = inputResult.FunctionDetails;
        if (function is null)
        {
            return;
        }

        if (!_togglingFunctions.TryAdd(function.Key, 0))
        {
            return;
        }

        var target = !function.IsDisabled;

        // Optimistic UI: flip the row immediately and mark it in-flight, then republish the list
        // through the coordinator so the controller re-renders on the UI thread.
        function.IsDisabled = target;
        function.IsToggling = true;
        await coordinator.PublishUpdateAsync(app);

        try
        {
            await functionAppManagement.SetFunctionDisabled(app, function.Name, target);
        }
        catch (Exception e)
        {
            sessionMonitor.ReportPossibleAuthFailure(e);
            logger.LogError(e, "Failed to toggle function {FunctionName} on {FunctionAppName}", function.Name, app.Name);
            function.IsDisabled = !target; // revert on failure
        }
        finally
        {
            function.IsToggling = false;
            await coordinator.PublishUpdateAsync(app);
            _togglingFunctions.TryRemove(function.Key, out _);
        }
    }

    private async Task<FunctionAppDetails> LoadStartedFunctionAppDetails(FunctionAppDetails details)
    {
        const int maxAttempts = 5;
        details.State = FunctionState.Running;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var updatedDetails = await functionService.GetFunctionAppDetails(details);
            if (updatedDetails.Functions.Count > 0 || attempt == maxAttempts)
            {
                return updatedDetails;
            }

            await Task.Delay(TimeSpan.FromSeconds(attempt));
        }

        return details;
    }
}
