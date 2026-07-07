namespace Funcy.Console.Ui.State;

public interface IUiErrorLog
{
    // Raised (coalesced) whenever the set of entries changes, so the UI can react.
    event Action? Changed;

    int Count { get; }

    void Report(string scope, string message);
    void Clear();

    // Snapshot ordered newest-first.
    IReadOnlyList<UiErrorEntry> GetSnapshot();
}

// Bounded, thread-safe ring buffer of surfaced errors. Entries arrive from background tasks;
// the Changed event is coalesced (same pattern as UiStatusState) so a burst of failures does
// not spam the render loop.
public sealed class UiErrorLog : IUiErrorLog
{
    private const int Capacity = 200;

    private readonly Lock _gate = new();
    private readonly Queue<UiErrorEntry> _entries = new();
    private long _sequence;
    private int _changeQueued;

    public event Action? Changed;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public void Report(string scope, string message)
    {
        lock (_gate)
        {
            _entries.Enqueue(new UiErrorEntry(++_sequence, DateTime.UtcNow, scope, message));
            while (_entries.Count > Capacity)
            {
                _entries.Dequeue();
            }
        }

        QueueChanged();
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (_entries.Count == 0)
            {
                return;
            }

            _entries.Clear();
        }

        QueueChanged();
    }

    public IReadOnlyList<UiErrorEntry> GetSnapshot()
    {
        lock (_gate)
        {
            // Enqueue order is oldest-first; reverse to newest-first for display.
            var items = _entries.ToArray();
            Array.Reverse(items);
            return items;
        }
    }

    private void QueueChanged()
    {
        if (Interlocked.Exchange(ref _changeQueued, 1) == 1)
        {
            return;
        }

        ThreadPool.QueueUserWorkItem(static state =>
        {
            var self = (UiErrorLog)state!;
            Interlocked.Exchange(ref self._changeQueued, 0);
            self.Changed?.Invoke();
        }, this);
    }
}
