namespace Funcy.Console.Ui;

public interface IUiStatusState
{
    event Action? Changed;

    UiStatusSnapshot GetSnapshot();

    void BeginInventoryValidation();
    void EndInventoryValidation();
    void BeginDetailsRefresh();
    void EndDetailsRefresh();
    void SetTotalDetails(int count);

    void IncrementDetailsInFlight();
    void ResetDetailsInFlight();

    void SetLastError(string? message);
}

public readonly struct UiStatusSnapshot
{
    public bool IsInventoryValidating { get; init; }
    public int DetailsInFlight { get; init; }
    public long LastInventoryRefreshUtcTicks { get; init; }
    public long LastErrorUtcTicks { get; init; }
    public string? LastError { get; init; }
    public bool IsDetailsRefreshing { get; init; }
    public int TotalDetails { get; init; }
}

public sealed class UiStatusState : IUiStatusState
{
    public event Action? Changed;

    private int _isInventoryValidating;
    private int _isDetailsRefreshing;
    private int _totalDetails;
    private int _detailsInFlight;

    private long _lastDetailsRefreshUtcTicks;
    private long _lastErrorUtcTicks;
    private string? _lastError;

    private int _changeQueued;

    public UiStatusSnapshot GetSnapshot()
    {
        return new UiStatusSnapshot
        {
            IsDetailsRefreshing = Volatile.Read(ref _isDetailsRefreshing) == 1,
            IsInventoryValidating = Volatile.Read(ref _isInventoryValidating) == 1,
            DetailsInFlight = Volatile.Read(ref _detailsInFlight),
            LastInventoryRefreshUtcTicks = Volatile.Read(ref _lastDetailsRefreshUtcTicks),
            LastErrorUtcTicks = Volatile.Read(ref _lastErrorUtcTicks),
            LastError = Volatile.Read(ref _lastError),
            TotalDetails = Volatile.Read(ref _totalDetails)
        };
    }

    public void BeginInventoryValidation()
    {
        if (Interlocked.Exchange(ref _isInventoryValidating, 1) == 1)
            return;

        QueueChanged();
    }

    public void EndInventoryValidation()
    {
        if (Interlocked.Exchange(ref _isInventoryValidating, 0) == 0)
            return;

        // Inventory validation completing is a data refresh, so stamp the "last updated"
        // timestamp surfaced as LastInventoryRefreshUtcTicks (details refresh stamps it too).
        Volatile.Write(ref _lastDetailsRefreshUtcTicks, DateTime.UtcNow.Ticks);
        QueueChanged();
    }

    public void BeginDetailsRefresh()
    {
        if (Interlocked.Exchange(ref _isDetailsRefreshing, 1) == 1)
            return;
        
        QueueChanged();
    }

    public void EndDetailsRefresh()
    {
        if (Interlocked.Exchange(ref _isDetailsRefreshing, 0) == 0)
            return;
        
        Volatile.Write(ref _lastDetailsRefreshUtcTicks, DateTime.UtcNow.Ticks);
        QueueChanged();
    }

    public void IncrementDetailsInFlight()
    {
        Interlocked.Increment(ref _detailsInFlight);
        QueueChanged();
    }
    
    public void SetTotalDetails(int count)
    {
        Interlocked.Exchange(ref _totalDetails, count);
        QueueChanged();
    }

    public void ResetDetailsInFlight()
    {
        Interlocked.Exchange(ref _detailsInFlight, 0);

        QueueChanged();
    }

    public void SetLastError(string? message)
    {
        Volatile.Write(ref _lastError, message);
        Volatile.Write(ref _lastErrorUtcTicks, message is null ? 0 : DateTime.UtcNow.Ticks);
        QueueChanged();
    }

    private void QueueChanged()
    {
        if (Interlocked.Exchange(ref _changeQueued, 1) == 1)
            return;

        ThreadPool.QueueUserWorkItem(static state =>
        {
            var self = (UiStatusState)state!;
            Interlocked.Exchange(ref self._changeQueued, 0);
            self.Changed?.Invoke();
        }, this);
    }
}