using Funcy.Console.Handlers;
using Funcy.Console.Handlers.Models;
using Funcy.Console.Ui;
using Funcy.Console.Ui.Input;
using Funcy.Console.Ui.Navigation;
using Funcy.Console.Ui.PanelLayout;
using Funcy.Console.Ui.PanelLayout.Renderers;
using Funcy.Console.Ui.Pagination.Matchers;
using Funcy.Console.Ui.Panels;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Core.Model;
using Spectre.Console;
using Xunit;

namespace Funcy.Tests.Panels;

public class ListPanelViewBehaviorTests
{
    private static ConsoleKeyInfo Key(ConsoleKey key) => new('\0', key, false, false, false);

    // ---- Filtering ----

    [Fact]
    public void SetSearchText_FiltersVisibleRows()
    {
        var view = MakeView(Items(5));
        view.RenderIfNeeded();

        view.SetSearchText("app-03");
        Assert.Equal("app-03", view.GetSelectedItemKey());
    }

    [Fact]
    public void SetSearchText_NoMatch_GivesEmptySelection()
    {
        var view = MakeView(Items(5));
        view.RenderIfNeeded();

        view.SetSearchText("nonexistent");
        Assert.Equal("", view.GetSelectedItemKey());
    }

    [Fact]
    public void SetSearchText_ClearingRestoresAllRows()
    {
        var view = MakeView(Items(5));
        view.RenderIfNeeded();

        view.SetSearchText("app-03");
        view.SetSearchText("");
        Assert.Equal("app-00", view.GetSelectedItemKey()); // back to first of the full list
    }

    [Fact]
    public void SetSearchText_IsTrimmed()
    {
        var view = MakeView(Items(5));
        view.RenderIfNeeded();
        view.SetSearchText("  app-02  ");
        Assert.Equal("app-02", view.GetSelectedItemKey());
    }

    // ---- Selection across mutations ----

    [Fact]
    public void GetSelectedItemKey_EmptyView_ReturnsEmptyString()
    {
        var view = MakeView([]);
        view.RenderIfNeeded();
        Assert.Equal("", view.GetSelectedItemKey());
    }

    [Fact]
    public void Remove_SelectedItem_SelectionIndexStaysAndPointsAtShiftedItem()
    {
        // Characterization: selection is index-based; removing the selected row keeps the index,
        // so the next row slides under the cursor.
        var view = MakeView(Items(5));
        view.RenderIfNeeded();
        view.HandleInput(Key(ConsoleKey.DownArrow));
        view.HandleInput(Key(ConsoleKey.DownArrow)); // selected index 2 -> app-02

        view.Remove("app-02");
        view.RenderIfNeeded();

        Assert.Equal("app-03", view.GetSelectedItemKey()); // app-03 slid into index 2
    }

    [Fact]
    public void Remove_LastItem_ClampsSelectionToNewLast()
    {
        var view = MakeView(Items(3));
        view.RenderIfNeeded();
        view.HandleInput(Key(ConsoleKey.DownArrow));
        view.HandleInput(Key(ConsoleKey.DownArrow)); // index 2 -> app-02 (last)

        view.Remove("app-02");
        view.RenderIfNeeded();

        Assert.Equal("app-01", view.GetSelectedItemKey()); // clamped to new last
    }

    [Fact]
    public void Upsert_NewItem_KeepsSelectionByIndex()
    {
        var view = MakeView(Items(3));
        view.RenderIfNeeded();
        view.HandleInput(Key(ConsoleKey.DownArrow)); // index 1 -> app-01

        view.Upsert(new TestItem("app-99", 99));
        view.RenderIfNeeded();

        Assert.Equal("app-01", view.GetSelectedItemKey()); // app-99 sorts last, index 1 unchanged
    }

    // ---- Navigation requests ----

    [Fact]
    public void TryGetNavigationRequest_NoDelegate_ReturnsFalse()
    {
        var view = MakeView(Items(3));
        view.RenderIfNeeded();
        Assert.False(view.TryGetNavigationRequest(out var req));
        Assert.Null(req);
    }

    [Fact]
    public void TryGetNavigationRequest_WithDelegateAndSelection_ReturnsRequest()
    {
        var view = MakeView(Items(3),
            onEnterNavigation: item => new NavigationRequest(PanelTarget.Functions, item.Key));
        view.RenderIfNeeded();

        Assert.True(view.TryGetNavigationRequest(out var req));
        Assert.Equal(new NavigationRequest(PanelTarget.Functions, "app-00"), req);
    }

    [Fact]
    public void TryGetNavigationRequest_DelegateReturnsNull_ReturnsFalse()
    {
        var view = MakeView(Items(3), onEnterNavigation: _ => null!);
        view.RenderIfNeeded();
        Assert.False(view.TryGetNavigationRequest(out var req));
        Assert.Null(req);
    }

    [Fact]
    public void TryGetNavigationRequest_EmptySelectionWithDelegate_ReturnsFalse()
    {
        // With a navigation delegate but no selection, the call is a no-op (returns false)
        // rather than throwing.
        var view = MakeView([], onEnterNavigation: item => new NavigationRequest(PanelTarget.Functions, item.Key));
        view.RenderIfNeeded();
        Assert.False(view.TryGetNavigationRequest(out var req));
        Assert.Null(req);
    }

    [Fact]
    public void TryGetActionNavigationRequest_EmptySelectionWithDelegate_ReturnsFalse()
    {
        var view = MakeView([], onActionNavigation: item => new NavigationRequest(PanelTarget.Slots, item.Key));
        view.RenderIfNeeded();
        Assert.False(view.TryGetActionNavigationRequest(out var req));
        Assert.Null(req);
    }

    [Fact]
    public void TryGetActionNavigationRequest_NoDelegate_ReturnsFalse()
    {
        var view = MakeView(Items(3));
        view.RenderIfNeeded();
        Assert.False(view.TryGetActionNavigationRequest(out _));
    }

    // ---- Rendering / dirty flag ----

    [Fact]
    public void RenderIfNeeded_OnlyRendersWhenDirty()
    {
        var animations = new CountingAnimationProvider();
        var view = MakeView(Items(3), animations: animations);

        view.RenderIfNeeded();                    // SetAll marked dirty -> renders
        Assert.Equal(1, animations.GetAnimationsCalls);

        view.RenderIfNeeded();                    // nothing changed -> no render
        Assert.Equal(1, animations.GetAnimationsCalls);
    }

    [Fact]
    public void SetUiStatus_MarksDirty_TriggeringNextRender()
    {
        var animations = new CountingAnimationProvider();
        var view = MakeView(Items(3), animations: animations);
        view.RenderIfNeeded();
        animations.GetAnimationsCalls = 0;

        view.SetUiStatus(new UiStatusSnapshot { IsInventoryValidating = true });
        view.RenderIfNeeded();
        Assert.Equal(1, animations.GetAnimationsCalls);
        Assert.True(view.GetUiStatusSnapshot().IsInventoryValidating);
    }

    // ---- Sorting ----

    [Fact]
    public void SortViewBy_Ascending_OrdersByColumnSelector()
    {
        // Column 2 = Rank. Ranks: app-00->2, app-01->0, app-02->1  (see Items()).
        var view = MakeView(Items(3));
        view.RenderIfNeeded();
        view.SortViewBy(2); // ascending by Rank
        Assert.Equal(["app-01", "app-02", "app-00"], StepThrough(view, 3));
    }

    [Fact]
    public void SortViewBy_Descending_ReversesOrder()
    {
        var view = MakeView(Items(3));
        view.RenderIfNeeded();
        view.SortViewBy(2);
        view.SortViewBy(2); // descending by Rank
        Assert.Equal(["app-00", "app-02", "app-01"], StepThrough(view, 3));
    }

    [Fact]
    public void SortViewBy_ThirdToggle_ResetsToNaturalOrder()
    {
        var view = MakeView(Items(3));
        view.RenderIfNeeded();
        view.SortViewBy(2);
        view.SortViewBy(2);
        view.SortViewBy(2); // reset -> natural order (by Key)
        Assert.Equal(["app-00", "app-01", "app-02"], StepThrough(view, 3));
    }

    // ---- Pagination windowing ----

    [Fact]
    public void HandleInput_DownArrow_ScrollsThroughWindowedList()
    {
        // windowHeight 10 -> MaxVisibleRows 2, so 4 items require scrolling.
        var view = MakeView(Items(4), windowHeight: 10);
        view.RenderIfNeeded();

        Assert.Equal("app-00", view.GetSelectedItemKey());
        view.HandleInput(Key(ConsoleKey.DownArrow));
        Assert.Equal("app-01", view.GetSelectedItemKey());
        view.HandleInput(Key(ConsoleKey.DownArrow));
        Assert.Equal("app-02", view.GetSelectedItemKey()); // window scrolled
        view.HandleInput(Key(ConsoleKey.DownArrow));
        Assert.Equal("app-03", view.GetSelectedItemKey());
        view.HandleInput(Key(ConsoleKey.DownArrow));
        Assert.Equal("app-03", view.GetSelectedItemKey()); // clamped at bottom
    }

    [Fact]
    public void HandleInput_PageDown_JumpsWindowForward()
    {
        var view = MakeView(Items(4), windowHeight: 10); // Max 2
        view.RenderIfNeeded();
        view.HandleInput(Key(ConsoleKey.PageDown));
        Assert.Equal("app-02", view.GetSelectedItemKey());
    }

    // ---- Empty-state message ----

    [Fact]
    public void EmptyStateMessage_InvokedWhenNoItemsAndNoSearch()
    {
        var calls = 0;
        var view = MakeView([], emptyStateMessage: _ => { calls++; return "nothing here"; });
        view.RenderCurrentView();
        Assert.Equal(1, calls);
    }

    [Fact]
    public void EmptyStateMessage_NotInvoked_WhenSearchActive()
    {
        var calls = 0;
        var view = MakeView(Items(3), emptyStateMessage: _ => { calls++; return "nothing here"; });
        view.RenderIfNeeded();
        view.SetSearchText("no-match"); // triggers a render with empty visible rows
        Assert.Equal(0, calls); // search present -> empty-state delegate is skipped
    }

    [Fact]
    public void EmptyStateMessage_NotInvoked_WhenItemsPresent()
    {
        var calls = 0;
        var view = MakeView(Items(3), emptyStateMessage: _ => { calls++; return "nothing here"; });
        view.RenderCurrentView();
        Assert.Equal(0, calls);
    }

    // ---- helpers ----

    private static List<TestItem> Items(int n)
    {
        // Rank is deliberately a rotation of the index so column-sort differs from natural (Key) order.
        return Enumerable.Range(0, n)
            .Select(i => new TestItem($"app-{i:D2}", (i + 2) % Math.Max(n, 1)))
            .ToList();
    }

    private static List<string> StepThrough(ListPanelView<TestItem> view, int count)
    {
        var keys = new List<string> { view.GetSelectedItemKey() };
        for (var i = 1; i < count; i++)
        {
            view.HandleInput(Key(ConsoleKey.DownArrow));
            keys.Add(view.GetSelectedItemKey());
        }
        return keys;
    }

    private static ListPanelView<TestItem> MakeView(
        IReadOnlyList<TestItem> items,
        CountingAnimationProvider? animations = null,
        Func<TestItem, NavigationRequest>? onEnterNavigation = null,
        Func<TestItem, NavigationRequest>? onActionNavigation = null,
        Func<UiStatusSnapshot, string?>? emptyStateMessage = null,
        int windowHeight = 30)
    {
        var view = new ListPanelView<TestItem>(
            new KeyMatcher(),
            new TestLayoutRenderer(),
            new NoShortcuts(),
            animations ?? new CountingAnimationProvider(),
            onEnterNavigation: onEnterNavigation,
            header: "Test",
            onAction: null,
            onActionNavigation: onActionNavigation,
            emptyStateMessage: emptyStateMessage,
            windowHeight: () => windowHeight);
        view.SetAll(items);
        return view;
    }

    private sealed record TestItem(string Key, int Rank) : IHasKey, IComparable<TestItem>
    {
        public int CompareTo(TestItem? other) => string.CompareOrdinal(Key, other?.Key);
    }

    private sealed class TestLayoutRenderer : ILayoutRenderer<TestItem>
    {
        public RowMarkup CreateRowMarkup(TestItem item)
            => new RowMarkup { Key = item.Key }.Add("Key", new RowCell(new Markup(item.Key), new Markup(item.Key)));

        public ColumnLayout<TestItem> CreateColumnLayout()
            => new(new Column<TestItem>("Key", i => i.Key), new Column<TestItem>("Rank", i => i.Rank));
    }

    private sealed class CountingAnimationProvider : IAnimationProvider
    {
        public int GetAnimationsCalls;
        public List<AnimationContext> GetAnimations() { GetAnimationsCalls++; return []; }
        public AnimationContext? GetAnimation(string key) => null;
    }

    private sealed class KeyMatcher : ISearchMatcher<TestItem>
    {
        public bool TryMatch(TestItem item, string input) => item.Key.Contains(input, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NoShortcuts : IShortcutProvider<TestItem>
    {
        public Dictionary<TableIndex, ShortcutMap> Describe(TestItem? item) => new();
        public bool IsActionValid(TestItem? item, FunctionAction action) => false;
    }
}
