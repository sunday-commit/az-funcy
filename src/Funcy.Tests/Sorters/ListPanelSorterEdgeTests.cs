using Funcy.Console.Ui.PanelLayout;
using Funcy.Console.Ui.Pagination.Sorters;
using Xunit;

namespace Funcy.Tests.Sorters;

public class ListPanelSorterEdgeTests
{
    private sealed record Row(string Name, int? Value);

    private static ListPanelSorter<Row> MakeSorter() =>
        new(new ColumnLayout<Row>(
            new Column<Row>("Name", r => r.Name),
            new Column<Row>("Value", r => r.Value),
            new Column<Row>("NoSelector", null))); // column index 3 has a null selector

    [Fact]
    public void Toggle_ColumnZero_IsNoOp()
    {
        // The map is keyed 1..N; index 0 is never present.
        var sorter = MakeSorter();
        sorter.Toggle(0);
        Assert.Null(sorter.CurrentColumn);
    }

    [Fact]
    public void Sort_NullSelectorColumn_KeepsSourceOrder()
    {
        // Column 3 has selector (_ => null); all keys equal null so OrderBy is a stable no-op.
        var sorter = MakeSorter();
        sorter.Toggle(3);
        var source = new List<Row> { new("c", 1), new("a", 2), new("b", 3) };
        var result = sorter.Sort(source);
        Assert.Equal(source, result);
    }

    [Fact]
    public void Sort_IsStable_ForEqualKeys()
    {
        // Sorting by Value where several rows share a value must preserve their relative order.
        var sorter = MakeSorter();
        sorter.Toggle(2); // Value ascending
        var source = new List<Row>
        {
            new("first", 5),
            new("second", 5),
            new("third", 1),
            new("fourth", 5)
        };
        var result = sorter.Sort(source);
        Assert.Equal(["third", "first", "second", "fourth"], result.Select(r => r.Name));
    }

    [Fact]
    public void Sort_NumericColumn_Ascending()
    {
        var sorter = MakeSorter();
        sorter.Toggle(2);
        var result = sorter.Sort([new("a", 30), new("b", 10), new("c", 20)]);
        Assert.Equal([10, 20, 30], result.Select(r => r.Value));
    }

    [Fact]
    public void Sort_NullValues_SortFirstAscending()
    {
        // Characterization: LINQ OrderBy places null keys before non-null in ascending order.
        var sorter = MakeSorter();
        sorter.Toggle(2);
        var result = sorter.Sort([new("a", 10), new("b", null), new("c", 5)]);
        Assert.Equal([null, 5, 10], result.Select(r => r.Value));
    }

    [Fact]
    public void Sort_ReturnsNewList_DoesNotMutateSource()
    {
        var sorter = MakeSorter();
        sorter.Toggle(1);
        var source = new List<Row> { new("c", 1), new("a", 2) };
        var result = sorter.Sort(source);
        Assert.Equal(["c", "a"], source.Select(r => r.Name)); // source untouched
        Assert.Equal(["a", "c"], result.Select(r => r.Name));
    }
}
