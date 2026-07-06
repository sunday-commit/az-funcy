using Funcy.Console.Ui;
using Funcy.Core.Model;
using Spectre.Console;
using Xunit;

namespace Funcy.Tests.Ui;

public class UiHelperTests
{
    [Theory]
    [InlineData(FunctionState.Running)]
    public void GetStateColor_Running_IsGreen(FunctionState state)
        => Assert.Equal(Color.Green, UiHelper.GetStateColor(state));

    [Fact]
    public void GetStateColor_Stopped_IsRed()
        => Assert.Equal(Color.Red, UiHelper.GetStateColor(FunctionState.Stopped));

    [Fact]
    public void GetStateColor_Unknown_IsWhite()
        => Assert.Equal(Color.White, UiHelper.GetStateColor(FunctionState.Unknown));

    [Fact]
    public void GetStatusColor_Swapped_IsCornflowerBlue()
        => Assert.Equal(Color.CornflowerBlue, UiHelper.GetStatusColor(new FunctionStatus { Status = StatusType.Swapped }));

    [Fact]
    public void GetStatusColor_Success_IsGreen()
        => Assert.Equal(Color.Green, UiHelper.GetStatusColor(new FunctionStatus { Status = StatusType.Success }));

    [Fact]
    public void GetStatusColor_Error_IsRed()
        => Assert.Equal(Color.Red, UiHelper.GetStatusColor(new FunctionStatus { Status = StatusType.Error }));

    [Theory]
    [InlineData(StatusType.Idle)]
    [InlineData(StatusType.InProgress)]
    public void GetStatusColor_IdleOrInProgress_IsWhite(StatusType status)
        => Assert.Equal(Color.White, UiHelper.GetStatusColor(new FunctionStatus { Status = status }));
}
