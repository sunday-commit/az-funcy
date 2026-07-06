using Funcy.Console.Ui.PanelLayout.Renderers;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Renderers;

public class FunctionLayoutRendererTests
{
    private readonly FunctionLayoutRenderer _sut = new();

    private static FunctionDetails MakeFunction(bool isDisabled) =>
        new() { Name = "ProcessPayment", FunctionAppName = "appA", Trigger = "HttpTrigger", IsDisabled = isDisabled };

    [Fact]
    public void CreateColumnLayout_ExposesStateColumn()
    {
        var layout = _sut.CreateColumnLayout();

        Assert.Contains(layout.Columns, c => c.Header == "State");
    }

    [Fact]
    public void StateColumn_Selector_ReflectsDisabledState()
    {
        var stateColumn = _sut.CreateColumnLayout().Columns.Single(c => c.Header == "State");

        Assert.Equal("Disabled", stateColumn.Selector!(MakeFunction(isDisabled: true)));
        Assert.Equal("Enabled", stateColumn.Selector!(MakeFunction(isDisabled: false)));
    }

    [Fact]
    public void CreateRowMarkup_IncludesStateCell()
    {
        var row = _sut.CreateRowMarkup(MakeFunction(isDisabled: true));

        Assert.True(row.Cells.ContainsKey("State"));
    }
}
