using Funcy.Console.Handlers.Concurrency;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Concurrency;

public class FunctionStateCoordinatorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static FunctionAppDetails MakeApp(string name, string subscription) =>
        new()
        {
            Name = name,
            State = FunctionState.Running,
            ResourceGroup = "rg",
            Subscription = subscription,
            Id = $"/subscriptions/{subscription}/sites/{name}"
        };

    [Fact]
    public async Task RemoveFunctions_EvictsMissingAppFromInMemoryCache()
    {
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription("sub-1");
        coordinator.InitCache([MakeApp("appA", "sub-1"), MakeApp("appB", "sub-1")]);

        var removed = new TaskCompletionSource<FunctionAppDetails>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.OnFunctionAppRemoved += app => removed.TrySetResult(app);

        // appB is no longer reported by Azure -> it must be evicted.
        await coordinator.RemoveFunctions(["appA"]);

        var removedApp = await removed.Task.WaitAsync(Timeout);

        Assert.Equal("appB", removedApp.Name);
        Assert.Null(coordinator.TryGet("appB"));   // evicted from the in-memory cache
        Assert.NotNull(coordinator.TryGet("appA")); // still present
    }

    [Fact]
    public async Task RemoveFunctions_DoesNotEvictAppsFromOtherSubscriptions()
    {
        var coordinator = new FunctionStateCoordinator();

        coordinator.SetSubscription("sub-1");
        coordinator.InitCache([MakeApp("shared", "sub-1"), MakeApp("only-1", "sub-1")]);

        coordinator.SetSubscription("sub-2");
        coordinator.InitCache([MakeApp("shared", "sub-2")]);

        var removed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.OnFunctionAppRemoved += _ => removed.TrySetResult();

        // Current subscription is sub-2; nothing should remain there.
        await coordinator.RemoveFunctions([]);
        await removed.Task.WaitAsync(Timeout);

        // The sub-2 entry is gone...
        Assert.Null(coordinator.TryGet("shared"));

        // ...but sub-1 (including the same-named app) is untouched.
        coordinator.SetSubscription("sub-1");
        Assert.NotNull(coordinator.TryGet("shared"));
        Assert.NotNull(coordinator.TryGet("only-1"));
    }

    [Fact]
    public async Task PublishUpdate_AddsAppToCache_AndRaisesEvent()
    {
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription("sub-1");
        coordinator.InitCache([]);

        var updated = new TaskCompletionSource<FunctionAppDetails>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.OnFunctionAppUpdated += app => updated.TrySetResult(app);

        await coordinator.PublishUpdateAsync(MakeApp("appA", "sub-1"), FunctionAppUpdateKind.Inventory);
        var updatedApp = await updated.Task.WaitAsync(Timeout);

        Assert.Equal("appA", updatedApp.Name);
        Assert.NotNull(coordinator.TryGet("appA"));
    }

    [Fact]
    public async Task InventoryUpdate_PreservesExistingFunctionsAndSlots()
    {
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription("sub-1");

        var seeded = MakeApp("appA", "sub-1");
        seeded.Functions = [new FunctionDetails { FunctionAppName = "appA", Name = "f1", Trigger = "HttpTrigger" }];
        seeded.Slots =
        [
            new FunctionAppSlotDetails
                { Id = "slot-id", FullName = "appA/staging", Name = "staging", State = FunctionState.Running }
        ];
        coordinator.InitCache([seeded]);

        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.OnFunctionAppUpdated += _ => updated.TrySetResult();

        // Inventory updates from Graph carry no functions/slots; merge must keep the cached ones.
        await coordinator.PublishUpdateAsync(MakeApp("appA", "sub-1"), FunctionAppUpdateKind.Inventory);
        await updated.Task.WaitAsync(Timeout);

        var cached = coordinator.TryGet("appA");
        Assert.NotNull(cached);
        Assert.Single(cached!.Functions);
        Assert.Single(cached.Slots);
    }

    [Fact]
    public async Task DetailsUpdate_ReplacesFunctionsAndSlots()
    {
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription("sub-1");

        var seeded = MakeApp("appA", "sub-1");
        seeded.Functions = [new FunctionDetails { FunctionAppName = "appA", Name = "stale", Trigger = "HttpTrigger" }];
        coordinator.InitCache([seeded]);

        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.OnFunctionAppUpdated += _ => updated.TrySetResult();

        var fresh = MakeApp("appA", "sub-1");
        fresh.Functions =
        [
            new FunctionDetails { FunctionAppName = "appA", Name = "f1", Trigger = "HttpTrigger" },
            new FunctionDetails { FunctionAppName = "appA", Name = "f2", Trigger = "TimerTrigger" }
        ];

        await coordinator.PublishUpdateAsync(fresh, FunctionAppUpdateKind.Details);
        await updated.Task.WaitAsync(Timeout);

        var cached = coordinator.TryGet("appA");
        Assert.NotNull(cached);
        Assert.Equal(2, cached!.Functions.Count);
        Assert.DoesNotContain(cached.Functions, f => f.Name == "stale");
    }
}
