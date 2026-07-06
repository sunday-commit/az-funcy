using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.Controllers;

// Streams a single function's Application Insights telemetry into a list panel, polling as close
// to realtime as ingestion allows. All view mutation goes through SetAll/SetHeader/invalidate;
// the Spectre table itself is only ever touched on the render thread.
public sealed class FunctionLogsController : ListPanelControllerBase<LogEntryDetails>
{
    private const int InitialMaxRows = 200;
    private const int IncrementalMaxRows = 200;
    private const int BufferCapacity = 1000;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    // Small overlap so entries arriving out of order around the boundary are not missed; dedupe
    // by Key removes the duplicates that the overlap re-fetches.
    private static readonly TimeSpan PollOverlap = TimeSpan.FromSeconds(30);

    private readonly ILogQueryExecutor _executor;
    private readonly IAppInsightsResolver _resolver;
    private readonly Action? _invalidate;
    private readonly string _functionAppArmId;
    private readonly string _functionAppName;
    private readonly string _functionName;

    private readonly LogBuffer _buffer = new(BufferCapacity);
    private readonly Lock _sync = new();
    private readonly SemaphoreSlim _pollNow = new(0, 1);
    private readonly CancellationTokenSource _cts = new();

    private LogTypeFilter _filter = LogTypeFilter.All;
    private DateTimeOffset? _lastPolled;

    public FunctionLogsController(
        IListPanelView<LogEntryDetails> view,
        ILogQueryExecutor executor,
        IAppInsightsResolver resolver,
        string functionAppArmId,
        string functionAppName,
        string functionName,
        Action? invalidate = null)
        : base(view)
    {
        _executor = executor;
        _resolver = resolver;
        _invalidate = invalidate;
        _functionAppArmId = functionAppArmId;
        _functionAppName = functionAppName;
        _functionName = functionName;

        View.SetEmptyStateMessage($"[gray]Loading Application Insights logs for {Markup.Escape(functionName)}…[/]");
        UpdateHeader();
        _invalidate?.Invoke();

        _ = PollLoopAsync(_cts.Token);
    }

    public override void Refresh()
    {
        // Wake the poll loop for an immediate fetch (bounded release avoids overflow).
        if (_pollNow.CurrentCount == 0)
        {
            _pollNow.Release();
        }
    }

    public override void ToggleTypeFilter()
    {
        lock (_sync)
        {
            _filter = _filter.Next();
            ApplyViewLocked();
        }

        _invalidate?.Invoke();
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var resourceId = await _resolver.ResolveResourceIdAsync(_functionAppArmId, cancellationToken);
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                View.SetEmptyStateMessage("[gray]No Application Insights configured for this app.[/]");
                _invalidate?.Invoke();
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                await PollOnceAsync(resourceId, cancellationToken);
                await _pollNow.WaitAsync(PollInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Panel popped / disposed.
        }
    }

    private async Task PollOnceAsync(string resourceId, CancellationToken cancellationToken)
    {
        DateTimeOffset? since;
        lock (_sync)
        {
            since = _buffer.MaxTimestamp is { } max ? max - PollOverlap : null;
        }

        try
        {
            var maxRows = since is null ? InitialMaxRows : IncrementalMaxRows;
            var entries = await _executor.QueryAsync(
                new LogQueryRequest(resourceId, _functionAppName, _functionName, since, maxRows),
                cancellationToken);

            lock (_sync)
            {
                _buffer.Merge(entries);
                _lastPolled = DateTimeOffset.Now;
                if (_buffer.Count == 0)
                {
                    View.SetEmptyStateMessage("[gray]No log entries yet. Application Insights ingestion can lag 1-3 min.[/]");
                }

                ApplyViewLocked();
            }

            _invalidate?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch
        {
            // Transient query failure: keep the existing buffer and try again next poll.
        }
    }

    // Push the currently-filtered buffer to the view. Caller holds _sync.
    private void ApplyViewLocked()
    {
        View.SetAll(_buffer.Snapshot(_filter));
        UpdateHeader();
    }

    private void UpdateHeader()
    {
        var polled = _lastPolled is { } p ? p.ToString("HH:mm:ss") : "—";
        var count = _buffer.Count;
        View.SetHeader($"Logs: {Markup.Escape(_functionName)} [yellow]{_filter.ToDisplayLabel()}[/] (polled {polled}, {count} entries)");
    }

    public override void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _pollNow.Dispose();
        base.Dispose();
    }
}
