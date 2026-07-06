using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Models;

public class FunctionStatusTests
{
    [Theory]
    [InlineData(StatusType.InProgress, 50)]
    [InlineData(StatusType.Success, 3)]
    [InlineData(StatusType.Error, 0)]
    [InlineData(StatusType.Swapped, 60)]
    public void GetTimeToLive_ReturnsExpectedSeconds(StatusType status, int expected)
    {
        var s = new FunctionStatus { Status = status };
        Assert.Equal(expected, s.GetTimeToLive());
    }

    [Fact]
    public void GetTimeToLive_Idle_Throws()
    {
        var s = new FunctionStatus { Status = StatusType.Idle };
        Assert.Throws<ArgumentOutOfRangeException>(() => s.GetTimeToLive());
    }

    [Theory]
    [InlineData(StatusType.Idle, "")]
    [InlineData(StatusType.Success, "Success")]
    [InlineData(StatusType.Error, "Error")]
    [InlineData(StatusType.Swapped, "Swapped")]
    public void ToDisplayLabel_NonInProgress(StatusType status, string expected)
    {
        var s = new FunctionStatus { Status = status };
        Assert.Equal(expected, s.ToDisplayLabel());
    }

    [Theory]
    [InlineData(FunctionAction.Start, "Starting...")]
    [InlineData(FunctionAction.Stop, "Stopping...")]
    [InlineData(FunctionAction.Swap, "Swapping...")]
    [InlineData(FunctionAction.Refresh, "Refreshing...")]
    public void ToDisplayLabel_InProgress_UsesAction(FunctionAction action, string expected)
    {
        var s = new FunctionStatus { Status = StatusType.InProgress, Action = action };
        Assert.Equal(expected, s.ToDisplayLabel());
    }

    [Fact]
    public void ToDisplayLabel_InProgress_NullAction_ReturnsEmpty()
    {
        var s = new FunctionStatus { Status = StatusType.InProgress, Action = null };
        Assert.Equal("", s.ToDisplayLabel());
    }

    [Fact]
    public void ToDisplayLabel_InProgress_UnmappedAction_ReturnsEmpty()
    {
        var s = new FunctionStatus { Status = StatusType.InProgress, Action = FunctionAction.RefreshAll };
        Assert.Equal("", s.ToDisplayLabel());
    }

    [Theory]
    [InlineData(FunctionState.Running, "Running")]
    [InlineData(FunctionState.Stopped, "Stopped")]
    public void FunctionState_ToDisplayLabel(FunctionState state, string expected)
        => Assert.Equal(expected, state.ToDisplayLabel());

    [Fact]
    public void FunctionState_Unknown_ToDisplayLabel_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => FunctionState.Unknown.ToDisplayLabel());

    [Theory]
    [InlineData(FunctionAction.Start, FunctionState.Running)]
    [InlineData(FunctionAction.Stop, FunctionState.Stopped)]
    public void FunctionAction_GetFunctionState(FunctionAction action, FunctionState expected)
        => Assert.Equal(expected, action.GetFunctionState());

    [Theory]
    [InlineData(FunctionAction.Swap)]
    [InlineData(FunctionAction.Refresh)]
    [InlineData(FunctionAction.ChangeSubscription)]
    public void FunctionAction_GetFunctionState_Unsupported_Throws(FunctionAction action)
        => Assert.Throws<ArgumentOutOfRangeException>(() => action.GetFunctionState());
}
