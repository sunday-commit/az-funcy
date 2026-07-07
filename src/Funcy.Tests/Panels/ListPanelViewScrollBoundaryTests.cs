using Funcy.Console.Handlers;
using Funcy.Console.Handlers.Models;
using Funcy.Console.Ui;
using Funcy.Console.Ui.PanelLayout;
using Funcy.Console.Ui.PanelLayout.Renderers;
using Funcy.Console.Ui.Pagination.Matchers;
using Funcy.Console.Ui.Panels;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Core.Model;
using Spectre.Console;
using Xunit;

namespace Funcy.Tests.Panels;

// Adversarial scrolling: drives the view past the end of the list, against filtered lists, and
// through window resizes that shrink the visible window mid-scroll. The invariant under test is
// that the selection index can never point outside _visibleRows (GetSelectedItem indexes it), so
// none of these sequences may throw.
public class ListPanelViewScrollBoundaryTests
{
    private static ConsoleKeyInfo Key(ConsoleKey key) => new('\0', key, false, false, false);

    private static List<TestItem> Items(int count) =>
        Enumerable.Range(0, count).Select(i => new TestItem($"item-{i:D3}", "v")).ToList();

    [Fact]
    public void GetSelectedItemKey_AfterWindowShrinksMidScroll_DoesNotThrow()
    {
        var height = 30; // MaxVisibleRows == 22
        var view = MakeView(() => height);
        view.SetAll(Items(50));
        view.RenderIfNeeded();

        for (var i = 0; i < 20; i++)
        {
            view.HandleInput(Key(ConsoleKey.DownArrow)); // selection lands deep inside the tall window
        }

        height = 12; // MaxVisibleRows drops to 4; selection index 20 is now past the visible window
        view.HandleResize();

        // On the unfixed paginator the selection index (20) is never pulled back inside the shrunken
        // window, so _visibleRows[20] on a 4-row window throws IndexOutOfRange.
        var ex = Record.Exception(() => view.GetSelectedItemKey());
        Assert.Null(ex);
    }

    [Fact]
    public void GetShortcuts_AfterWindowShrinksMidScroll_DoesNotThrow()
    {
        var height = 40; // Max 32
        var view = MakeView(() => height);
        view.SetAll(Items(60));
        view.RenderIfNeeded();

        for (var i = 0; i < 30; i++)
        {
            view.HandleInput(Key(ConsoleKey.DownArrow));
        }

        height = 10; // Max 2
        view.HandleResize();

        var ex = Record.Exception(() => view.GetShortcuts());
        Assert.Null(ex);
    }

    [Fact]
    public void ArrowDown_PastLastItem_DoesNotThrow_AndSelectionStaysInVisibleRows()
    {
        var view = MakeView(() => 12); // Max 4
        view.SetAll(Items(3));
        view.RenderIfNeeded();

        var ex = Record.Exception(() =>
        {
            for (var i = 0; i < 25; i++)
            {
                view.HandleInput(Key(ConsoleKey.DownArrow));
            }
        });

        Assert.Null(ex);
        Assert.Contains(view.GetSelectedItemKey(), view.GetVisibleKeys());
    }

    [Fact]
    public void PageDown_PastEnd_DoesNotThrow()
    {
        var view = MakeView(() => 14); // Max 6
        view.SetAll(Items(40));
        view.RenderIfNeeded();

        var ex = Record.Exception(() =>
        {
            for (var i = 0; i < 30; i++)
            {
                view.HandleInput(Key(ConsoleKey.PageDown));
            }
        });

        Assert.Null(ex);
        Assert.Contains(view.GetSelectedItemKey(), view.GetVisibleKeys());
    }

    [Fact]
    public void Filtering_ToFewerThanVisibleStart_DoesNotThrow_AndKeepsRowsVisible()
    {
        var view = MakeView(() => 14); // Max 6
        view.SetAll(Items(40));
        view.RenderIfNeeded();

        // Scroll deep so VisibleStartIndex is well past any small filtered result.
        for (var i = 0; i < 30; i++)
        {
            view.HandleInput(Key(ConsoleKey.DownArrow));
        }

        // Filter down to a set that is smaller than the current scroll offset.
        view.SetSearchText("item-00"); // matches item-000..item-009 (10 rows)

        Assert.Null(Record.Exception(() => view.GetSelectedItemKey()));
        // The matches must actually be windowed, not skipped past into a blank frame.
        Assert.NotEmpty(view.GetVisibleKeys());
        Assert.Contains(view.GetSelectedItemKey(), view.GetVisibleKeys());
    }

    [Fact]
    public void Remove_LastItemWhileSelected_DoesNotThrow()
    {
        var view = MakeView(() => 30);
        var items = Items(5);
        view.SetAll(items);
        view.RenderIfNeeded();

        for (var i = 0; i < 4; i++)
        {
            view.HandleInput(Key(ConsoleKey.DownArrow)); // select last item
        }

        view.Remove(items[^1].Key);
        view.RenderIfNeeded();

        Assert.Null(Record.Exception(() => view.GetSelectedItemKey()));
        Assert.Contains(view.GetSelectedItemKey(), view.GetVisibleKeys());
    }

    [Fact]
    public void SetAll_ShorterList_WhileSelectionBeyondNewCount_DoesNotThrow()
    {
        var view = MakeView(() => 30);
        view.SetAll(Items(40));
        view.RenderIfNeeded();

        for (var i = 0; i < 20; i++)
        {
            view.HandleInput(Key(ConsoleKey.DownArrow));
        }

        view.SetAll(Items(2)); // selection index far beyond the new count
        view.RenderIfNeeded();

        Assert.Null(Record.Exception(() => view.GetSelectedItemKey()));
        Assert.Contains(view.GetSelectedItemKey(), view.GetVisibleKeys());
    }

    [Fact]
    public void ZeroRowWindow_ThenScroll_DoesNotThrow()
    {
        var height = 6; // Max floored to 0
        var view = MakeView(() => height);
        view.SetAll(Items(10));
        view.RenderIfNeeded();

        var ex = Record.Exception(() =>
        {
            for (var i = 0; i < 10; i++)
            {
                view.HandleInput(Key(ConsoleKey.DownArrow));
                view.HandleInput(Key(ConsoleKey.PageDown));
            }

            // Grow back and make sure the still-valid selection survives the resize.
            height = 20;
            view.HandleResize();
            view.GetSelectedItemKey();
        });

        Assert.Null(ex);
    }

    private static ListPanelView<TestItem> MakeView(Func<int> windowHeight)
    {
        return new ListPanelView<TestItem>(
            new SubstringMatcher(),
            new SimpleLayoutRenderer(),
            new NoShortcuts(),
            new NoAnimations(),
            onEnterNavigation: null,
            header: "Test",
            onAction: null,
            onActionNavigation: null,
            emptyStateMessage: null,
            windowHeight: windowHeight,
            windowWidth: () => 140);
    }

    private sealed record TestItem(string Key, string Value) : IHasKey, IComparable<TestItem>
    {
        public int CompareTo(TestItem? other) => string.CompareOrdinal(Key, other?.Key);
    }

    private sealed class SimpleLayoutRenderer : ILayoutRenderer<TestItem>
    {
        public RowMarkup CreateRowMarkup(TestItem item) =>
            new RowMarkup { Key = item.Key }.Add("Name", new RowCell(new Markup(item.Key), new Markup(item.Key)));

        public ColumnLayout<TestItem> CreateColumnLayout() => new(new Column<TestItem>("Name", i => i.Key, Flex: true));
    }

    private sealed class SubstringMatcher : ISearchMatcher<TestItem>
    {
        public bool TryMatch(TestItem app, string input) =>
            app.Key.Contains(input, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NoAnimations : IAnimationProvider
    {
        public List<AnimationContext> GetAnimations() => [];
        public AnimationContext? GetAnimation(string key) => null;
    }

    private sealed class NoShortcuts : IShortcutProvider<TestItem>
    {
        public Dictionary<TableIndex, ShortcutMap> Describe(TestItem? item) => new();
        public bool IsActionValid(TestItem? item, FunctionAction action) => false;
    }
}
