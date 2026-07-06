using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Microsoft.Extensions.Logging;

namespace Funcy.Console.Ui.Controllers;

// Marks a controller that can re-run its own on-demand fetch (wired to the Refresh shortcut).
public interface ICountRefreshable
{
    void Refresh();
}

public sealed class FunctionListController : ListPanelControllerBase<FunctionDetails>, ICountRefreshable
{
    private readonly FunctionStateCoordinator _coordinator;
    private readonly IServiceBusInsightService _insightService;
    private readonly ILogger<FunctionListController> _logger;
    private readonly Action? _invalidate;
    private readonly IUiStatusState _uiStatusState;
    private readonly string _functionAppKey;

    private CancellationTokenSource? _countCts;

    public FunctionListController(IListPanelView<FunctionDetails> view,
        string appKey,
        IEnumerable<FunctionDetails> initial,
        FunctionStateCoordinator coordinator,
        IServiceBusInsightService insightService,
        ILogger<FunctionListController> logger,
        IUiStatusState uiStatusState,
        Action? invalidate = null)
        : base(view)
    {
        _coordinator = coordinator;
        _insightService = insightService;
        _logger = logger;
        _invalidate = invalidate;
        _uiStatusState = uiStatusState;
        _functionAppKey = appKey;

        View.SetAll(initial.ToList());
        PushStatusToView(_uiStatusState.GetSnapshot());
        _invalidate?.Invoke();

        _coordinator.OnFunctionListUpdated += OnListUpdated;
        _uiStatusState.Changed += OnUiStatusChanged;

        TriggerCountFetch();
    }

    public void Refresh() => TriggerCountFetch();

    private void OnListUpdated(string functionAppKey, List<FunctionDetails> updated)
    {
        if (string.Equals(_functionAppKey, functionAppKey))
        {
            View.SetAll(updated);
            _invalidate?.Invoke();
            // The row set was replaced; re-fetch counts for the new instances.
            TriggerCountFetch();
        }
    }

    // Fetches Service Bus message counts for the app's SB-triggered functions off the render
    // thread and pushes them into the view row-by-row. Safe to call repeatedly: a new call
    // cancels the previous fetch.
    private void TriggerCountFetch()
    {
        var app = _coordinator.TryGet(_functionAppKey);
        if (app is null || string.IsNullOrEmpty(app.Id))
        {
            return;
        }

        var serviceBusFunctions = app.Functions.Where(f => f.IsServiceBusTrigger).ToList();
        if (serviceBusFunctions.Count == 0)
        {
            return;
        }

        _countCts?.Cancel();
        _countCts?.Dispose();
        var cts = new CancellationTokenSource();
        _countCts = cts;
        var token = cts.Token;

        foreach (var function in serviceBusFunctions)
        {
            function.CountStatus = ServiceBusCountStatus.Loading;
            View.Upsert(function);
        }
        _invalidate?.Invoke();

        _ = Task.Run(async () =>
        {
            try
            {
                var results = await _insightService.GetCountsAsync(app.Id, serviceBusFunctions, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var byKey = serviceBusFunctions.ToDictionary(f => f.Key);
                foreach (var result in results)
                {
                    if (!byKey.TryGetValue(result.FunctionKey, out var function))
                    {
                        continue;
                    }

                    function.ActiveMessages = result.ActiveMessages;
                    function.DeadLetteredMessages = result.DeadLetteredMessages;
                    function.CountStatus = result.Success
                        ? ServiceBusCountStatus.Loaded
                        : ServiceBusCountStatus.Failed;
                    View.Upsert(function);
                }

                _invalidate?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer fetch or panel disposal.
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to fetch Service Bus counts for {FunctionApp}", _functionAppKey);
            }
        }, token);
    }

    private void OnUiStatusChanged()
    {
        PushStatusToView(_uiStatusState.GetSnapshot());
        _invalidate?.Invoke();
    }

    public override void Dispose()
    {
        _coordinator.OnFunctionListUpdated -= OnListUpdated;
        _uiStatusState.Changed -= OnUiStatusChanged;
        _countCts?.Cancel();
        _countCts?.Dispose();
        base.Dispose();
    }
}
