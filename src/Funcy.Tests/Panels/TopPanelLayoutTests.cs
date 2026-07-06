using Funcy.Console.Ui.Panels;
using Xunit;

namespace Funcy.Tests.Panels;

// The subscription name is the squeezed victim of the shortcut grid, so it flexes into the extra
// terminal width: nameWidth = adaptiveTableWidth - fixedColumns(102), floored at the historic 35.
public class TopPanelLayoutTests
{
    [Theory]
    [InlineData(80, 35)]    // narrow: table floors at 115 -> 115-102 = 13, below the min -> 35
    [InlineData(121, 35)]   // table 115 -> 13 -> floored to 35
    [InlineData(140, 35)]   // table 134 -> 134-102 = 32, still below the min -> 35
    [InlineData(150, 42)]   // table 144 -> 144-102 = 42
    [InlineData(300, 78)]   // wide: table 180 -> 180-102 = 78
    public void ResolveNameWidth_FlexesAboveTheFixedGrid(int consoleWidth, int expected)
    {
        Assert.Equal(expected, TopPanel.ResolveNameWidth(consoleWidth));
    }

    [Fact]
    public void ResolveNameWidth_NeverBelowHistoricMinimum()
    {
        for (var consoleWidth = 1; consoleWidth <= 500; consoleWidth++)
        {
            Assert.True(TopPanel.ResolveNameWidth(consoleWidth) >= 35);
        }
    }
}
