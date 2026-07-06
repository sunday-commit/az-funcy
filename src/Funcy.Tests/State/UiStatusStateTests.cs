using Funcy.Console.Ui;
using Xunit;

namespace Funcy.Tests.State;

public class UiStatusStateTests
{
    [Fact]
    public void FreshState_SnapshotIsAllDefault()
    {
        var s = new UiStatusState().GetSnapshot();
        Assert.False(s.IsInventoryValidating);
        Assert.False(s.IsDetailsRefreshing);
        Assert.Equal(0, s.DetailsInFlight);
        Assert.Equal(0, s.TotalDetails);
        Assert.Equal(0, s.LastInventoryRefreshUtcTicks);
        Assert.Equal(0, s.LastErrorUtcTicks);
        Assert.Null(s.LastError);
    }

    [Fact]
    public void BeginAndEndInventoryValidation_TogglesFlag()
    {
        var s = new UiStatusState();
        s.BeginInventoryValidation();
        Assert.True(s.GetSnapshot().IsInventoryValidating);
        s.EndInventoryValidation();
        Assert.False(s.GetSnapshot().IsInventoryValidating);
    }

    [Fact]
    public void BeginAndEndDetailsRefresh_TogglesFlag()
    {
        var s = new UiStatusState();
        s.BeginDetailsRefresh();
        Assert.True(s.GetSnapshot().IsDetailsRefreshing);
        s.EndDetailsRefresh();
        Assert.False(s.GetSnapshot().IsDetailsRefreshing);
    }

    [Fact]
    public void EndDetailsRefresh_StampsLastRefreshTimestamp()
    {
        // The "last updated" timestamp (exposed as LastInventoryRefreshUtcTicks) is written when
        // a details refresh completes.
        var s = new UiStatusState();
        s.BeginDetailsRefresh();
        Assert.Equal(0, s.GetSnapshot().LastInventoryRefreshUtcTicks);
        s.EndDetailsRefresh();
        Assert.True(s.GetSnapshot().LastInventoryRefreshUtcTicks > 0);
    }

    [Fact]
    public void EndInventoryValidation_StampsTimestamp()
    {
        // Inventory validation completing is also a data refresh, so it stamps the timestamp.
        var s = new UiStatusState();
        s.BeginInventoryValidation();
        Assert.Equal(0, s.GetSnapshot().LastInventoryRefreshUtcTicks);
        s.EndInventoryValidation();
        Assert.True(s.GetSnapshot().LastInventoryRefreshUtcTicks > 0);
    }

    [Fact]
    public void IncrementDetailsInFlight_Accumulates()
    {
        var s = new UiStatusState();
        s.IncrementDetailsInFlight();
        s.IncrementDetailsInFlight();
        s.IncrementDetailsInFlight();
        Assert.Equal(3, s.GetSnapshot().DetailsInFlight);
    }

    [Fact]
    public void ResetDetailsInFlight_ZeroesCounter()
    {
        var s = new UiStatusState();
        s.IncrementDetailsInFlight();
        s.ResetDetailsInFlight();
        Assert.Equal(0, s.GetSnapshot().DetailsInFlight);
    }

    [Fact]
    public void SetTotalDetails_StoresCount()
    {
        var s = new UiStatusState();
        s.SetTotalDetails(7);
        Assert.Equal(7, s.GetSnapshot().TotalDetails);
    }

    [Fact]
    public void SetLastError_StoresMessageAndTimestamp()
    {
        var s = new UiStatusState();
        s.SetLastError("boom");
        var snap = s.GetSnapshot();
        Assert.Equal("boom", snap.LastError);
        Assert.True(snap.LastErrorUtcTicks > 0);
    }

    [Fact]
    public void SetLastError_Null_ClearsMessageAndTimestamp()
    {
        var s = new UiStatusState();
        s.SetLastError("boom");
        s.SetLastError(null);
        var snap = s.GetSnapshot();
        Assert.Null(snap.LastError);
        Assert.Equal(0, snap.LastErrorUtcTicks);
    }

    [Fact]
    public void Changed_FiresAfterAStateMutation()
    {
        var s = new UiStatusState();
        using var fired = new ManualResetEventSlim(false);
        s.Changed += () => fired.Set();

        s.BeginInventoryValidation();

        // QueueChanged marshals the callback onto the thread pool; give it a bounded wait.
        Assert.True(fired.Wait(TimeSpan.FromSeconds(2)));
    }
}
