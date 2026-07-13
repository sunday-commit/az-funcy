using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Console.Ui.State;
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
    private readonly IUiErrorLog? _uiErrorLog;
    private readonly string _functionAppKey;

    private readonly Lock _countGate = new();
    private CancellationTokenSource? _countCts;

    // Last-known resolved %SettingName% binding names, keyed by function Key. A Details-kind
    // republish replaces the row set with freshly DB-mapped instances that carry the RAW names,
    // which would revert the "Listens to" column to %X% until the async re-fetch lands (and leave
    // it raw for good if that fetch is superseded, cancelled or fails). Overlaying this memo on
    // every SetAll keeps the display resolved across all orderings. Display-only; SQLite keeps raw.
    private readonly Dictionary<string, ResolvedNames> _resolvedNames = new();
    private readonly Lock _resolvedNamesGate = new();

    private readonly record struct ResolvedNames(string? QueueName, string? TopicName, string? SubscriptionName);

    public FunctionListController(IListPanelView<FunctionDetails> view,
        string appKey,
        IEnumerable<FunctionDetails> initial,
        FunctionStateCoordinator coordinator,
        IServiceBusInsightService insightService,
        ILogger<FunctionListController> logger,
        IUiStatusState uiStatusState,
        Action? invalidate = null,
        IUiErrorLog? uiErrorLog = null)
        : base(view)
    {
        _coordinator = coordinator;
        _insightService = insightService;
        _logger = logger;
        _invalidate = invalidate;
        _uiStatusState = uiStatusState;
        _uiErrorLog = uiErrorLog;
        _functionAppKey = appKey;

        View.SetAll(OverlayResolvedNames(initial.ToList()));
        PushStatusToView(_uiStatusState.GetSnapshot());
        _invalidate?.Invoke();

        _coordinator.OnFunctionListUpdated += OnListUpdated;
        _uiStatusState.Changed += OnUiStatusChanged;

        TriggerCountFetch();
    }

    public override void Refresh() => TriggerCountFetch();

    private void OnListUpdated(string functionAppKey, List<FunctionDetails> updated)
    {
        if (string.Equals(_functionAppKey, functionAppKey))
        {
            View.SetAll(OverlayResolvedNames(updated));
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

        CancellationToken token;
        lock (_countGate)
        {
            _countCts?.Cancel();
            _countCts?.Dispose();
            _countCts = new CancellationTokenSource();
            token = _countCts.Token;
        }

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
                var permissionError = results.Select(r => r.ErrorMessage).FirstOrDefault(m => m is not null);
                if (permissionError is not null)
                {
                    _uiErrorLog?.Report(app.Name, permissionError);
                }
                foreach (var result in results)
                {
                    if (!byKey.TryGetValue(result.FunctionKey, out var function))
                    {
                        continue;
                    }

                    function.ActiveMessages = result.ActiveMessages;
                    function.DeadLetteredMessages = result.DeadLetteredMessages;
                    // Cache first-time resolutions and configuration-driven namespace changes on the
                    // model; the app-list sync persists them to SQLite.
                    if (!string.IsNullOrEmpty(result.NamespaceId)
                        && !string.Equals(function.ServiceBusNamespaceId, result.NamespaceId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        function.ServiceBusNamespaceId = result.NamespaceId;
                    }
                    // Publish the resolved %SettingName% binding names so "Listens to" shows the real
                    // target. This is a per-runtime display value; the raw names remain in SQLite.
                    function.QueueName = result.QueueName;
                    function.TopicName = result.TopicName;
                    function.SubscriptionName = result.SubscriptionName;
                    // Remember them so a later Details-kind republish (which carries raw names) can be
                    // overlaid back to the resolved display without waiting on another fetch.
                    lock (_resolvedNamesGate)
                    {
                        _resolvedNames[function.Key] =
                            new ResolvedNames(result.QueueName, result.TopicName, result.SubscriptionName);
                    }
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

    // Applies any memoized resolved binding names onto the incoming instances so the "Listens to"
    // column stays resolved even when the update carries raw %SettingName% names (e.g. a Details
    // republish of freshly DB-mapped functions). Mutates the display instances only; the raw names
    // stay authoritative in SQLite. A subsequent re-fetch refreshes both the memo and the row.
    private List<FunctionDetails> OverlayResolvedNames(List<FunctionDetails> functions)
    {
        lock (_resolvedNamesGate)
        {
            if (_resolvedNames.Count == 0)
            {
                return functions;
            }

            foreach (var function in functions)
            {
                if (_resolvedNames.TryGetValue(function.Key, out var resolved))
                {
                    function.QueueName = resolved.QueueName;
                    function.TopicName = resolved.TopicName;
                    function.SubscriptionName = resolved.SubscriptionName;
                }
            }
        }

        return functions;
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
        lock (_countGate)
        {
            _countCts?.Cancel();
            _countCts?.Dispose();
            _countCts = null;
        }
        base.Dispose();
    }
}
