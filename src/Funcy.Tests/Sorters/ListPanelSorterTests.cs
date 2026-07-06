using Funcy.Console.Ui.PanelLayout;
using Funcy.Console.Ui.Pagination.Sorters;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Sorters;

public class ListPanelSorterTests
{
    private sealed record PinItem(string Name, bool IsPinned) : IPinnable;

    private static ListPanelSorter<PinItem> MakePinSorter()
        => new(new ColumnLayout<PinItem>(new Column<PinItem>("Name", p => p.Name)));

    private static ListPanelSorter<string> MakeSorter(int columnCount = 3)
    {
        var columns = Enumerable.Range(1, columnCount)
            .Select(i => new Column<string>($"Col{i}", s => s))
            .ToArray();
        return new ListPanelSorter<string>(new ColumnLayout<string>(columns));
    }

    [Fact]
    public void InitialState_HasNoColumnSelected()
    {
        var sorter = MakeSorter();
        Assert.Null(sorter.CurrentColumn);
        Assert.False(sorter.Desc);
    }

    [Fact]
    public void Toggle_NewColumn_SetsColumnAscending()
    {
        var sorter = MakeSorter();
        sorter.Toggle(1);
        Assert.Equal(1, sorter.CurrentColumn);
        Assert.False(sorter.Desc);
    }

    [Fact]
    public void Toggle_SameColumn_FlipsToDescending()
    {
        var sorter = MakeSorter();
        sorter.Toggle(1);
        sorter.Toggle(1);
        Assert.Equal(1, sorter.CurrentColumn);
        Assert.True(sorter.Desc);
    }

    [Fact]
    public void Toggle_SameColumn_ThirdTime_ClearsSort()
    {
        var sorter = MakeSorter();
        sorter.Toggle(1);
        sorter.Toggle(1);
        sorter.Toggle(1);
        Assert.Null(sorter.CurrentColumn);
        Assert.False(sorter.Desc);
    }

    [Fact]
    public void Toggle_DifferentColumn_ResetsToPreviousColumn()
    {
        var sorter = MakeSorter();
        sorter.Toggle(1);
        sorter.Toggle(1); // now Desc = true
        sorter.Toggle(2);
        Assert.Equal(2, sorter.CurrentColumn);
        Assert.False(sorter.Desc);
    }

    [Fact]
    public void Toggle_InvalidIndex_IsNoOp()
    {
        var sorter = MakeSorter();
        sorter.Toggle(99);
        Assert.Null(sorter.CurrentColumn);
        Assert.False(sorter.Desc);
    }

    [Fact]
    public void Sort_WithNoColumn_ReturnsSourceInOriginalOrder()
    {
        var sorter = MakeSorter();
        var source = new List<string> { "b", "a", "c" };
        var result = sorter.Sort(source);
        Assert.Equal(source, result);
    }

    [Fact]
    public void Sort_Ascending_OrdersBySelector()
    {
        var sorter = MakeSorter();
        sorter.Toggle(1);
        var result = sorter.Sort(["c", "a", "b"]);
        Assert.Equal(["a", "b", "c"], result);
    }

    [Fact]
    public void Sort_Descending_OrdersByDescSelector()
    {
        var sorter = MakeSorter();
        sorter.Toggle(1);
        sorter.Toggle(1); // Desc = true
        var result = sorter.Sort(["c", "a", "b"]);
        Assert.Equal(["c", "b", "a"], result);
    }

    [Fact]
    public void Sort_NoColumn_FloatsPinnedItemsToTop_PreservingNaturalOrder()
    {
        var sorter = MakePinSorter();
        // Caller passes items already in natural order; pinned group must move up but keep order.
        var source = new List<PinItem>
        {
            new("alpha", false),
            new("bravo", true),
            new("charlie", false),
            new("delta", true)
        };

        var result = sorter.Sort(source).Select(p => p.Name).ToList();

        Assert.Equal(["bravo", "delta", "alpha", "charlie"], result);
    }

    [Fact]
    public void Sort_ColumnAscending_KeepsPinnedFirstThenSortsByColumn()
    {
        var sorter = MakePinSorter();
        sorter.Toggle(1); // ascending by Name
        var source = new List<PinItem>
        {
            new("charlie", false),
            new("delta", true),
            new("alpha", false),
            new("bravo", true)
        };

        var result = sorter.Sort(source).Select(p => p.Name).ToList();

        // Pinned group first (sorted asc), then the rest (sorted asc).
        Assert.Equal(["bravo", "delta", "alpha", "charlie"], result);
    }

    [Fact]
    public void Sort_ColumnDescending_KeepsPinnedFirstThenSortsByColumnDesc()
    {
        var sorter = MakePinSorter();
        sorter.Toggle(1);
        sorter.Toggle(1); // descending by Name
        var source = new List<PinItem>
        {
            new("charlie", false),
            new("delta", true),
            new("alpha", false),
            new("bravo", true)
        };

        var result = sorter.Sort(source).Select(p => p.Name).ToList();

        // Pinned group first (sorted desc), then the rest (sorted desc).
        Assert.Equal(["delta", "bravo", "charlie", "alpha"], result);
    }
}
