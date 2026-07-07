using Funcy.Console.Handlers;
using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Ui.Input;
using Funcy.Console.Ui.State;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Funcy.Infrastructure.Azure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Funcy.Tests.Handlers;

public class FunctionActionHandlerErrorTests
{
    // Inert session monitor: these tests exercise the error-log path, not session detection.
    private sealed class NoopSessionMonitor : IAzureSessionMonitor
    {
        public event Action? Changed { add { } remove { } }
        public AzureSessionState State => AzureSessionState.Healthy;
        public Func<Task>? ReAuthenticatedCallback { get; set; }
        public Task RunProbeLoopAsync(CancellationToken token) => Task.CompletedTask;
        public Task ProbeOnceAsync(CancellationToken token) => Task.CompletedTask;
        public void ReportPossibleAuthFailure(Exception? ex) { }
        public void ReportPossibleAuthFailure(string? outputOrMessage) { }
        public void BeginReLogin() { }
    }

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private const string Subscription = "sub-1";

    private static FunctionAppDetails MakeApp(string name) => new()
    {
        Name = name,
        State = FunctionState.Running,
        ResourceGroup = "rg",
        Subscription = Subscription,
        Id = $"/subscriptions/{Subscription}/sites/{name}"
    };

    private static (FunctionActionHandler handler, FunctionStateCoordinator coordinator) BuildHandler(
        IFunctionAppManagementService management, IUiErrorLog errorLog)
    {
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription(Subscription);
        var statusManager = new FunctionStatusManager(coordinator, new AnimationHandler());
        var handler = new FunctionActionHandler(
            management,
            new ThrowingFunctionService(),
            statusManager,
            coordinator,
            NullLogger<FunctionActionHandler>.Instance,
            errorLog,
            new NoopSessionMonitor());
        return (handler, coordinator);
    }

    [Fact]
    public async Task ExecuteAction_WhenActionFails_ReportsToErrorLog()
    {
        var errorLog = new UiErrorLog();
        var (handler, _) = BuildHandler(new FakeManagement(new InvalidOperationException("boom")), errorLog);

        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        errorLog.Changed += () => reported.TrySetResult();

        await handler.Dispatch(new InputActionResult(FunctionAction.Start, MakeApp("app-a")));

        await reported.Task.WaitAsync(Timeout);

        var entry = Assert.Single(errorLog.GetSnapshot());
        Assert.Equal("app-a", entry.Scope);
        Assert.Contains("Start failed", entry.Message);
        Assert.Contains("boom", entry.Message);
    }

    [Fact]
    public async Task ExecuteAction_WhenCancelled_DoesNotReportToErrorLog()
    {
        var errorLog = new UiErrorLog();
        var (handler, coordinator) = BuildHandler(
            new FakeManagement(new OperationCanceledException()), errorLog);

        // The failure path always drives the status to Error via CompleteOperation; observing that
        // update confirms the catch block ran, so a missing report is a real assertion, not a race.
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.OnFunctionAppUpdated += app =>
        {
            if (app.Status.Status == StatusType.Error)
            {
                completed.TrySetResult();
            }
        };

        await handler.Dispatch(new InputActionResult(FunctionAction.Start, MakeApp("app-a")));

        await completed.Task.WaitAsync(Timeout);

        // Cancellations are not real failures -> nothing surfaced. (The OCE path never calls Report.)
        Assert.Equal(0, errorLog.Count);
    }

    private sealed class FakeManagement(Exception toThrow) : IFunctionAppManagementService
    {
        public Task StartFunction(FunctionAppDetails functionAppDetails) => throw toThrow;
        public Task StopFunction(FunctionAppDetails functionAppDetails) => throw toThrow;
        public Task SwapFunction(FunctionAppDetails functionAppDetails, FunctionAppSlotDetails functionAppSlot)
            => throw toThrow;
        public Task SetFunctionDisabled(FunctionAppDetails functionAppDetails, string functionName, bool disabled)
            => throw toThrow;
    }

    private sealed class ThrowingFunctionService : IAzureFunctionService
    {
        public Task<List<FunctionAppDetails>> GetFunctionsFromDatabase(string subscriptionId)
            => throw new NotSupportedException();

        public IAsyncEnumerable<FunctionAppFetchResult> GetFunctionAppDetailsAsync(
            string subscriptionId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<FunctionAppDetails> GetFunctionAppDetails(FunctionAppDetails functionAppDetails)
            => throw new NotSupportedException();

        public IAsyncEnumerable<FunctionAppFetchResult> GetFunctionAppFunctionsAndSlotsAsync(
            List<FunctionAppDetails> functionAppDetails, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task SetPinnedAsync(string azureId, bool isPinned) => throw new NotSupportedException();
    }
}
