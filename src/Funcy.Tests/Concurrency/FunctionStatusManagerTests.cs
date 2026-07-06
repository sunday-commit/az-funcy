using Funcy.Console.Handlers;
using Funcy.Console.Handlers.Concurrency;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Concurrency;

// The reset-to-idle step uses a real Task.Delay keyed on FunctionStatus.GetTimeToLive; these tests
// drive the deterministic synchronous transitions and the TTL==0 (Error) path that resets nothing.
public class FunctionStatusManagerTests
{
    private static FunctionStatusManager MakeManager(out AnimationHandler animations)
    {
        animations = new AnimationHandler();
        return new FunctionStatusManager(new FunctionStateCoordinator(), animations);
    }

    private static FunctionAppDetails App() =>
        new() { Name = "appA", State = FunctionState.Running, ResourceGroup = "rg", Subscription = "sub", Id = "id" };

    [Fact]
    public async Task BeginOperation_SetsInProgressWithAction_AndRegistersAnimation()
    {
        var manager = MakeManager(out var animations);
        var app = App();

        await manager.BeginOperation(app, FunctionAction.Start);

        Assert.Equal(StatusType.InProgress, app.Status.Status);
        Assert.Equal(FunctionAction.Start, app.Status.Action);
        Assert.NotNull(animations.GetAnimation(app.Key)); // animation frame registered
    }

    [Fact]
    public async Task CompleteOperation_Success_SetsSuccessAndClearsAction_AndStopsAnimation()
    {
        var manager = MakeManager(out var animations);
        var app = App();
        await manager.BeginOperation(app, FunctionAction.Refresh);

        await manager.CompleteOperation(app, FunctionAction.Refresh, success: true);

        Assert.Equal(StatusType.Success, app.Status.Status);
        Assert.Null(app.Status.Action);
        Assert.Null(animations.GetAnimation(app.Key)); // animation removed
    }

    [Fact]
    public async Task CompleteOperation_SwapSuccess_SetsSwapped()
    {
        var manager = MakeManager(out _);
        var app = App();

        await manager.CompleteOperation(app, FunctionAction.Swap, success: true);

        Assert.Equal(StatusType.Swapped, app.Status.Status);
    }

    [Fact]
    public async Task CompleteOperation_Failure_SetsError_AndDoesNotAutoReset()
    {
        // Error has TimeToLive 0, so ResetToIdleAfterDelay returns immediately without resetting.
        var manager = MakeManager(out _);
        var app = App();

        await manager.CompleteOperation(app, FunctionAction.Start, success: false);

        Assert.Equal(StatusType.Error, app.Status.Status);
        Assert.Null(app.Status.Action);
    }

    [Fact]
    public async Task CompleteOperation_FailedSwap_StillSetsError_NotSwapped()
    {
        var manager = MakeManager(out _);
        var app = App();

        await manager.CompleteOperation(app, FunctionAction.Swap, success: false);

        Assert.Equal(StatusType.Error, app.Status.Status);
    }
}
