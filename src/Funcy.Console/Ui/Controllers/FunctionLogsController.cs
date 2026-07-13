using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Spectre.Console;
using Funcy.Infrastructure.Azure;

namespace Funcy.Console.Ui.Controllers;

// Loads a single function's Application Insights telemetry into a list panel. Fetches once on
// open and then only on demand (R) — there is no background polling. All view mutation goes
// through SetAll/SetHeader/invalidate; the Spectre table itself is only ever touched on the
// render thread.
public sealed class FunctionLogsController : ListPanelControllerBase<LogEntryDetails>, IClipboardCopyController
{
    private const int InitialMaxRows = 200;
    private const int IncrementalMaxRows = 200;
    private const int BufferCapacity = 1000;
    // Small overlap so entries arriving out of order around the boundary are not missed; dedupe
    // by Key removes the duplicates that the overlap re-fetches.
    private static readonly TimeSpan PollOverlap = TimeSpan.FromSeconds(30);
    private static readonly string[] SpinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    private static readonly TimeSpan SpinnerInterval = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan CopyConfirmDuration = TimeSpan.FromSeconds(1.5);

    private readonly ILogQueryExecutor _executor;
    private readonly IAppInsightsResolver _resolver;
    private readonly IClipboardService _clipboard;
    private readonly Action? _invalidate;
    private readonly string _functionAppArmId;
    private readonly string _functionAppName;
    private readonly string _functionName;

    private readonly LogBuffer _buffer = new(BufferCapacity);
    private readonly Lock _sync = new();
    private readonly SemaphoreSlim _pollNow = new(0, 1);
    private readonly CancellationTokenSource _cts = new();

    // The Time column is the first column in LogEntryLayoutRenderer; the sorter keys columns 1-based.
    private const int TimeColumnIndex = 1;

    private LogTypeFilter _filter = LogTypeFilter.All;
    private LogLookback _lookback = LogLookback.OneHour;
    private DateTimeOffset? _lastPolled;
    private bool _copyConfirm;
    // Newest-first matches the buffer's natural order; the toggle flips to oldest-first.
    private bool _newestFirst = true;

    public FunctionLogsController(
        IListPanelView<LogEntryDetails> view,
        ILogQueryExecutor executor,
        IAppInsightsResolver resolver,
        IClipboardService clipboard,
        string functionAppArmId,
        string functionAppName,
        string functionName,
        Action? invalidate = null)
        : base(view)
    {
        _executor = executor;
        _resolver = resolver;
        _clipboard = clipboard;
        _invalidate = invalidate;
        _functionAppArmId = functionAppArmId;
        _functionAppName = functionAppName;
        _functionName = functionName;

        // Static placeholder until the poll loop resolves the resource and the spinner takes over.
        View.SetEmptyStateMessage($"[gray]Loading logs for {Markup.Escape(functionName)}…[/]");
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

    public override void CycleLookback(bool longer)
    {
        lock (_sync)
        {
            var next = longer ? _lookback.Longer() : _lookback.Shorter();
            if (next == _lookback)
            {
                // Already at the widest/narrowest window; nothing to refetch.
                return;
            }

            _lookback = next;
            // The retained entries no longer match the requested range; drop them so the next
            // fetch reloads the whole window (since = null).
            _buffer.Clear();
            ApplyViewLocked();
        }

        _invalidate?.Invoke();
        // Re-fetch immediately with the new window.
        Refresh();
    }

    public override void ToggleSortOrder()
    {
        bool newestFirst;
        lock (_sync)
        {
            _newestFirst = !_newestFirst;
            newestFirst = _newestFirst;
            UpdateHeader();
        }

        // Drive the view's sorter directly so it is a clean two-way toggle (asc/desc), not the
        // three-state column cycle. Descending on Time == newest first.
        (View as IOrderTogglePanel)?.SetSortOrder(TimeColumnIndex, descending: newestFirst);
        _invalidate?.Invoke();
    }

    public void CopySelectedValue()
    {
        var key = View.GetSelectedItemKey();
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        LogEntryDetails? entry;
        lock (_sync)
        {
            entry = _buffer.Find(key);
        }

        if (entry is not null)
        {
            _ = CopyAsync(entry.Message);
        }
    }

    private async Task CopyAsync(string message)
    {
        var copied = await _clipboard.TryCopyAsync(message, _cts.Token);
        if (!copied)
        {
            return;
        }

        lock (_sync)
        {
            _copyConfirm = true;
            UpdateHeader();
        }

        _invalidate?.Invoke();

        try
        {
            await Task.Delay(CopyConfirmDuration, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_sync)
        {
            _copyConfirm = false;
            UpdateHeader();
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

            // Initial load, then block until the user asks for a refresh (R) or changes the
            // lookback window — no periodic polling.
            await PollOnceAsync(resourceId, cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                await _pollNow.WaitAsync(cancellationToken);
                await PollOnceAsync(resourceId, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Panel popped / disposed.
        }
        catch (ObjectDisposedException)
        {
            // Dispose() raced the loop between cancellation and the next semaphore wait.
        }
        catch (Exception e) when (AzurePermissionError.IsAccessDenied(e))
        {
            View.SetEmptyStateMessage(
                $"[{UiStyles.Danger}]Could not resolve Application Insights due to missing access. Required: Website Contributor on the Function App and Reader access to its Azure resources.[/]");
            _invalidate?.Invoke();
        }
    }

    private async Task PollOnceAsync(string resourceId, CancellationToken cancellationToken)
    {
        DateTimeOffset? since;
        LogLookback lookback;
        bool wasEmpty;
        lock (_sync)
        {
            since = _buffer.MaxTimestamp is { } max ? max - PollOverlap : null;
            lookback = _lookback;
            wasEmpty = _buffer.Count == 0;
        }

        // Animate a spinner in the empty state while the first load for this window is in flight;
        // there is nothing to show yet, so the panel would otherwise look frozen.
        CancellationTokenSource? spinnerCts = null;
        if (wasEmpty)
        {
            spinnerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = SpinLoadingAsync($"Loading last {lookback.ToDisplayLabel()}", spinnerCts.Token);
        }

        try
        {
            var maxRows = since is null ? InitialMaxRows : IncrementalMaxRows;
            var entries = await _executor.QueryAsync(
                new LogQueryRequest(resourceId, _functionAppName, _functionName, since, maxRows, lookback.ToTimeSpan()),
                cancellationToken);

            // Stop the spinner before writing the final empty-state text so it cannot overwrite it.
            spinnerCts?.Cancel();

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
        catch (Exception e) when (AzurePermissionError.IsAccessDenied(e))
        {
            spinnerCts?.Cancel();
            View.SetEmptyStateMessage(
                $"[{UiStyles.Danger}]Log query access denied. Required: Monitoring Reader on Application Insights and, when workspace access is required, Log Analytics Reader on the workspace.[/]");
            _invalidate?.Invoke();
        }
        catch
        {
            spinnerCts?.Cancel();
            View.SetEmptyStateMessage("[gray]Could not load logs. Press R to try again.[/]");
            _invalidate?.Invoke();
        }
        finally
        {
            spinnerCts?.Dispose();
        }
    }

    private async Task SpinLoadingAsync(string label, CancellationToken cancellationToken)
    {
        var frame = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                View.SetEmptyStateMessage($"[gray]{label} {SpinnerFrames[frame]}[/]");
                _invalidate?.Invoke();
                frame = (frame + 1) % SpinnerFrames.Length;
                await Task.Delay(SpinnerInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Load finished (or panel closed); the caller sets the terminal empty-state text.
        }
    }

    // Push the currently-filtered buffer to the view. Caller holds _sync.
    private void ApplyViewLocked()
    {
        View.SetAll(_buffer.Snapshot(_filter));
        UpdateHeader();
    }

    // Caller holds _sync (or runs single-threaded in the ctor).
    private void UpdateHeader()
    {
        var refreshed = _lastPolled is { } p ? p.ToString("HH:mm:ss") : "—";
        var count = _buffer.Count;
        var order = _newestFirst ? "newest" : "oldest";
        var confirm = _copyConfirm ? $" · [green]{UiStyles.OkGlyph} copied[/]" : "";
        View.SetHeader(
            $"Logs: {Markup.Escape(_functionName)} " +
            $"[yellow]{_filter.ToDisplayLabel()}[/] · last [yellow]{_lookback.ToDisplayLabel()}[/] · [yellow]{order} first[/] " +
            $"(refreshed {refreshed}, {count} entries){confirm}");
    }

    public override void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _pollNow.Dispose();
        base.Dispose();
    }
}
