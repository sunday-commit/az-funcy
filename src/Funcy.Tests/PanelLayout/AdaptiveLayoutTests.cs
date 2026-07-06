using Funcy.Console.Ui.PanelLayout;
using Xunit;

namespace Funcy.Tests.PanelLayout;

// Table width = consoleWidth - PanelChrome(4) - SafetyGap(2), clamped to [115, 180].
public class AdaptiveLayoutTests
{
    [Theory]
    [InlineData(80, 115)]   // narrow: below the floor, stays at MinTableWidth (Spectre truncates)
    [InlineData(121, 115)]  // 121-6 = 115, exactly the floor
    [InlineData(140, 134)]  // normal: 140-6 = 134
    [InlineData(160, 154)]  // normal-wide
    [InlineData(186, 180)]  // 186-6 = 180, exactly the ceiling
    [InlineData(300, 180)]  // wide: clamped to MaxTableWidth
    public void ResolveTableWidth_ClampsToBounds(int consoleWidth, int expected)
    {
        Assert.Equal(expected, AdaptiveLayout.ResolveTableWidth(consoleWidth));
    }

    [Fact]
    public void ResolveTableWidth_NeverLeavesTheBounds()
    {
        for (var consoleWidth = 1; consoleWidth <= 500; consoleWidth++)
        {
            var width = AdaptiveLayout.ResolveTableWidth(consoleWidth);
            Assert.InRange(width, AdaptiveLayout.MinTableWidth, AdaptiveLayout.MaxTableWidth);
        }
    }

    [Fact]
    public void PanelWidth_AddsChromeToTableWidth()
    {
        Assert.Equal(134 + AdaptiveLayout.PanelChrome, AdaptiveLayout.PanelWidth(134));
    }
}
