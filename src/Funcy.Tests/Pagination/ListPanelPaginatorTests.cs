using Funcy.Console.Ui.Pagination;
using Xunit;

namespace Funcy.Tests.Pagination;

// windowHeight is injected; MaxVisibleRows == windowHeight - 8.
public class ListPanelPaginatorTests
{
    private static ListPanelPaginator Make(int windowHeight) => new(() => windowHeight);

    [Fact]
    public void Constructor_PrimesMaxVisibleRows()
    {
        var p = Make(18);
        Assert.Equal(10, p.MaxVisibleRows);
        Assert.Equal(0, p.SelectedIndex);
        Assert.Equal(0, p.VisibleStartIndex);
    }

    [Theory]
    [InlineData(30, 22)]
    [InlineData(18, 10)]
    [InlineData(8, 0)]
    [InlineData(5, -3)] // Characterization: no floor, MaxVisibleRows can go negative for tiny windows.
    public void UpdateMaxVisibleRows_IsWindowHeightMinusEight(int windowHeight, int expected)
    {
        var p = Make(windowHeight);
        p.UpdateMaxVisibleRows();
        Assert.Equal(expected, p.MaxVisibleRows);
    }

    [Fact]
    public void MoveDown_WithinWindow_MovesSelection_NoScroll()
    {
        var p = Make(18); // Max 10
        p.UpdateTotalRows(5);

        Assert.False(p.MoveDown());
        Assert.Equal(1, p.SelectedIndex);
        Assert.Equal(0, p.VisibleStartIndex);
    }

    [Fact]
    public void MoveDown_AtEndOfShortList_ClampsToLastIndex()
    {
        var p = Make(18); // Max 10
        p.UpdateTotalRows(3);

        for (var i = 0; i < 6; i++) p.MoveDown();

        Assert.Equal(2, p.SelectedIndex); // amount-1
        Assert.Equal(0, p.VisibleStartIndex);
    }

    [Fact]
    public void MoveDown_ScrollsWindow_WhenSelectionReachesBottom()
    {
        var p = Make(10); // Max 2
        p.UpdateTotalRows(5);

        Assert.False(p.MoveDown()); // sel 0 -> 1
        Assert.Equal(1, p.SelectedIndex);
        Assert.Equal(0, p.VisibleStartIndex);

        Assert.True(p.MoveDown()); // scroll: start 1, sel 1
        Assert.Equal(1, p.SelectedIndex);
        Assert.Equal(1, p.VisibleStartIndex);

        Assert.True(p.MoveDown()); // scroll: start 2
        Assert.Equal(2, p.VisibleStartIndex);

        Assert.True(p.MoveDown()); // scroll: start 3 (bottom; start+sel = 4 = last)
        Assert.Equal(3, p.VisibleStartIndex);
        Assert.Equal(1, p.SelectedIndex);

        Assert.False(p.MoveDown()); // cannot scroll further
        Assert.Equal(3, p.VisibleStartIndex);
        Assert.Equal(1, p.SelectedIndex);
    }

    [Fact]
    public void MoveUp_ScrollsBackAndClampsAtTop()
    {
        var p = Make(10); // Max 2
        p.UpdateTotalRows(5);
        for (var i = 0; i < 4; i++) p.MoveDown(); // start 3, sel 1

        Assert.False(p.MoveUp()); // sel 1 -> 0, no scroll
        Assert.Equal(0, p.SelectedIndex);
        Assert.Equal(3, p.VisibleStartIndex);

        Assert.True(p.MoveUp()); // scroll up: start 2, sel 0
        Assert.Equal(0, p.SelectedIndex);
        Assert.Equal(2, p.VisibleStartIndex);
    }

    [Fact]
    public void MoveUp_AtTop_StaysAtZero()
    {
        var p = Make(18);
        p.UpdateTotalRows(5);
        Assert.False(p.MoveUp());
        Assert.Equal(0, p.SelectedIndex);
        Assert.Equal(0, p.VisibleStartIndex);
    }

    [Fact]
    public void PageDown_AdvancesWindowByPage_ThenClampsAtBottom()
    {
        var p = Make(10); // Max 2
        p.UpdateTotalRows(5);

        Assert.True(p.PageDown()); // start min(3, 2) = 2
        Assert.Equal(2, p.VisibleStartIndex);

        Assert.True(p.PageDown()); // start min(3, 4) = 3
        Assert.Equal(3, p.VisibleStartIndex);

        Assert.False(p.PageDown()); // start+Max=5>=5 -> sel = Max-1, no scroll
        Assert.Equal(3, p.VisibleStartIndex);
        Assert.Equal(1, p.SelectedIndex);
    }

    [Fact]
    public void PageUp_RewindsWindowByPage_ThenClampsAtTop()
    {
        var p = Make(10); // Max 2
        p.UpdateTotalRows(5);
        p.PageDown();
        p.PageDown(); // start 3

        Assert.True(p.PageUp()); // start max(0, 1) = 1
        Assert.Equal(1, p.VisibleStartIndex);

        Assert.True(p.PageUp()); // start max(0, -1) = 0
        Assert.Equal(0, p.VisibleStartIndex);

        Assert.False(p.PageUp()); // at top -> sel = 0
        Assert.Equal(0, p.SelectedIndex);
        Assert.Equal(0, p.VisibleStartIndex);
    }

    [Fact]
    public void UpdateTotalRows_ShrinkingList_ResetsSelectionToTop()
    {
        var p = Make(10); // Max 2
        p.UpdateTotalRows(5);
        for (var i = 0; i < 4; i++) p.MoveDown(); // start 3, sel 1 (last item)

        p.UpdateTotalRows(2); // list shrank drastically

        Assert.Equal(0, p.SelectedIndex);
        Assert.Equal(0, p.VisibleStartIndex);
    }
}
