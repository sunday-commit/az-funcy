using Funcy.Console.Handlers.Concurrency;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Concurrency;

// Additional characterization beyond FunctionStateCoordinatorTests, covering merge kinds,
// per-subscription caching, event sequencing and TryGet semantics.
public class FunctionStateCoordinatorMergeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static FunctionAppDetails App(string name, string subscription) => new()
    {
        Name = name,
        State = FunctionState.Running,
        ResourceGroup = "rg",
        Subscription = subscription,
        Id = $"/subscriptions/{subscription}/sites/{name}"
    };

    [Fact]
    public async Task StateOnlyUpdate_ReplacesFunctionsAndSlots_DoesNotPreserve()
    {
        // Characterization: only Inventory updates preserve cached functions/slots. A StateOnly
        // update carries the incoming details verbatim, so cached functions are dropped if the
        // incoming payload has none.
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription("sub-1");

        var seeded = App("appA", "sub-1");
        seeded.Functions = [new FunctionDetails { FunctionAppName = "appA", Name = "f1", Trigger = "HttpTrigger" }];
        coordinator.InitCache([seeded]);

        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.OnFunctionAppUpdated += _ => updated.TrySetResult();

        await coordinator.PublishUpdateAsync(App("appA", "sub-1"), FunctionAppUpdateKind.StateOnly);
        await updated.Task.WaitAsync(Timeout);

        Assert.Empty(coordinator.TryGet("appA")!.Functions);
    }

    [Fact]
    public async Task OnFunctionListUpdated_FiresForDetails_ButNotForInventory()
    {
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription("sub-1");
        coordinator.InitCache([]);

        var listUpdates = 0;
        var detailsProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.OnFunctionListUpdated += (key, _) =>
        {
            Interlocked.Increment(ref listUpdates);
            if (key == "appB") detailsProcessed.TrySetResult();
        };

        // Updates are drained sequentially from one channel, so by the time the Details list-event
        // for appB fires, the Inventory update for appA has fully completed.
        await coordinator.PublishUpdateAsync(App("appA", "sub-1"), FunctionAppUpdateKind.Inventory);
        await coordinator.PublishUpdateAsync(App("appB", "sub-1"), FunctionAppUpdateKind.Details);
        await detailsProcessed.Task.WaitAsync(Timeout);

        Assert.Equal(1, listUpdates); // only the Details update raised the list event
    }

    [Fact]
    public async Task Update_ForNonCurrentSubscription_IsDropped()
    {
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription("sub-1");
        coordinator.InitCache([]);

        var updates = 0;
        var keeperProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.OnFunctionAppUpdated += app =>
        {
            Interlocked.Increment(ref updates);
            if (app.Name == "keeper") keeperProcessed.TrySetResult();
        };

        await coordinator.PublishUpdateAsync(App("dropped", "sub-2"), FunctionAppUpdateKind.Inventory);
        await coordinator.PublishUpdateAsync(App("keeper", "sub-1"), FunctionAppUpdateKind.Inventory);
        await keeperProcessed.Task.WaitAsync(Timeout);

        Assert.Equal(1, updates);
        Assert.Null(coordinator.TryGet("dropped"));
        Assert.NotNull(coordinator.TryGet("keeper"));
    }

    [Fact]
    public void TryGet_UnknownKey_ReturnsNull()
    {
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription("sub-1");
        coordinator.InitCache([]);
        Assert.Null(coordinator.TryGet("does-not-exist"));
    }

    [Fact]
    public void InitCache_RaisesOnCacheInit_WithSuppliedList()
    {
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription("sub-1");

        List<FunctionAppDetails>? received = null;
        coordinator.OnCacheInit += apps => received = apps;

        var seed = new List<FunctionAppDetails> { App("appA", "sub-1"), App("appB", "sub-1") };
        coordinator.InitCache(seed);

        Assert.NotNull(received);
        Assert.Equal(2, received!.Count);
        Assert.Equal(2, coordinator.GetCachedFunctionAppDetails().Count);
    }

    [Fact]
    public void InitCache_ClearsPreviousEntriesForSubscription()
    {
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription("sub-1");
        coordinator.InitCache([App("old", "sub-1")]);
        coordinator.InitCache([App("new", "sub-1")]);

        Assert.Null(coordinator.TryGet("old"));
        Assert.NotNull(coordinator.TryGet("new"));
    }

    [Fact]
    public void IsSubscriptionKnownEmpty_TrueOnlyAfterEmptyInit()
    {
        var coordinator = new FunctionStateCoordinator();
        Assert.False(coordinator.IsSubscriptionKnownEmpty("sub-x")); // never seen

        coordinator.MarkSubscriptionAsEmpty("sub-x");
        Assert.True(coordinator.IsSubscriptionKnownEmpty("sub-x"));
    }

    [Fact]
    public void IsSubscriptionKnownEmpty_FalseWhenSubscriptionHasApps()
    {
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription("sub-1");
        coordinator.InitCache([App("appA", "sub-1")]);
        Assert.False(coordinator.IsSubscriptionKnownEmpty("sub-1"));
    }

    [Fact]
    public void GetCachedFunctionAppDetails_ReturnsCurrentSubscriptionOnly()
    {
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription("sub-1");
        coordinator.InitCache([App("a", "sub-1"), App("b", "sub-1")]);
        coordinator.SetSubscription("sub-2");
        coordinator.InitCache([App("c", "sub-2")]);

        Assert.Equal(["c"], coordinator.GetCachedFunctionAppDetails().Select(a => a.Name));
    }
}
