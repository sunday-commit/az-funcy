using Funcy.Console.Ui.PanelLayout;
using Xunit;

namespace Funcy.Tests.PanelLayout;

// Resolve() distributes the spare width (target - sum of configured widths) across the flex
// columns; fixed columns keep their configured width and never shrink.
public class ColumnLayoutResolveTests
{
    private sealed record Row;

    private static Column<Row> Fixed(string header, int width) => new(header, _ => null, width);
    private static Column<Row> Flexed(string header, int width) => new(header, _ => null, width, Flex: true);

    [Fact]
    public void SingleFlexColumn_AbsorbsAllExtra()
    {
        var layout = new ColumnLayout<Row>(Flexed("Name", 40), Fixed("State", 10), Fixed("Status", 20));

        var resolved = layout.Resolve(115);

        // Configured sum 70, extra 45 -> all to the one flex column.
        Assert.Equal(new[] { 85, 10, 20 }, resolved);
    }

    [Fact]
    public void TwoFlexColumns_SplitExtraEvenly()
    {
        var layout = new ColumnLayout<Row>(Flexed("Name", 28), Fixed("Trigger", 15), Flexed("Listens to", 28), Fixed("Msgs", 7));

        var resolved = layout.Resolve(100);

        // Configured sum 78, extra 22 -> 11 each to the two flex columns.
        Assert.Equal(new[] { 39, 15, 39, 7 }, resolved);
    }

    [Fact]
    public void TwoFlexColumns_OddExtra_GivesRemainderToEarlierColumn()
    {
        var layout = new ColumnLayout<Row>(Flexed("Name", 28), Flexed("Listens to", 28));

        var resolved = layout.Resolve(85);

        // Configured sum 56, extra 29 -> 15 to the first, 14 to the second.
        Assert.Equal(new[] { 43, 42 }, resolved);
    }

    [Fact]
    public void FixedColumns_NeverShrinkBelowConfiguredWidth_WhenTargetIsTooSmall()
    {
        var layout = new ColumnLayout<Row>(Flexed("Name", 40), Fixed("State", 10), Fixed("Status", 20));

        // Target below the configured sum: nothing to distribute, everyone keeps their width.
        var resolved = layout.Resolve(50);

        Assert.Equal(new[] { 40, 10, 20 }, resolved);
    }

    [Fact]
    public void NoFlexColumns_LeavesEveryWidthUnchanged()
    {
        var layout = new ColumnLayout<Row>(Fixed("A", 10), Fixed("B", 20));

        Assert.Equal(new[] { 10, 20 }, layout.Resolve(200));
    }

    [Fact]
    public void FlexColumn_WithZeroConfiguredWidth_TakesTheWholeExtra()
    {
        // Mirrors the subscriptions/settings layouts: the widest text column is configured at 0
        // and flexes to fill the table.
        var layout = new ColumnLayout<Row>(Fixed("Setting", 34), Fixed("Value", 30), Flexed("Description", 0));

        Assert.Equal(new[] { 34, 30, 51 }, layout.Resolve(115));
    }
}
