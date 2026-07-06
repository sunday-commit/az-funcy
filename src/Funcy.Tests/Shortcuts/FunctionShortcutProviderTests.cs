using Funcy.Console.Ui.Shortcuts;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Shortcuts;

public class FunctionShortcutProviderTests
{
    private readonly FunctionShortcutProvider _sut = new();

    private static FunctionDetails MakeFunction(bool isDisabled = false, bool isToggling = false) =>
        new()
        {
            Name = "ProcessPayment",
            FunctionAppName = "appA",
            Trigger = "HttpTrigger",
            IsDisabled = isDisabled,
            IsToggling = isToggling
        };

    [Fact]
    public void Toggle_Enabled_WhenFunctionSelected()
        => Assert.True(_sut.IsActionValid(MakeFunction(), FunctionAction.ToggleDisabled));

    [Fact]
    public void Toggle_Enabled_WhenFunctionAlreadyDisabled()
        => Assert.True(_sut.IsActionValid(MakeFunction(isDisabled: true), FunctionAction.ToggleDisabled));

    [Fact]
    public void Toggle_Disabled_WhenNoSelection()
        => Assert.False(_sut.IsActionValid(null, FunctionAction.ToggleDisabled));

    [Fact]
    public void Toggle_Disabled_WhenToggleInFlight()
        => Assert.False(_sut.IsActionValid(MakeFunction(isToggling: true), FunctionAction.ToggleDisabled));

    [Fact]
    public void OtherActions_AreNotValid()
        => Assert.False(_sut.IsActionValid(MakeFunction(), FunctionAction.Start));

    [Fact]
    public void Describe_ExposesDisableEnableShortcut_WhenSelectable()
    {
        var map = _sut.Describe(MakeFunction());

        var entry = Assert.Single(map, kvp => kvp.Value.Shortcut == ListPanelShortcuts.DisableEnable);
        Assert.True(entry.Value.IsEnabled);
    }

    [Fact]
    public void Describe_DisablesShortcut_WhenToggleInFlight()
    {
        var map = _sut.Describe(MakeFunction(isToggling: true));

        var entry = Assert.Single(map, kvp => kvp.Value.Shortcut == ListPanelShortcuts.DisableEnable);
        Assert.False(entry.Value.IsEnabled);
    }
}
