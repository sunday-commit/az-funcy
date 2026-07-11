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

public class ListPanelViewTests
{
    [Fact]
    public void SetAll_BuildsMarkupForEveryItem_ButUpsertRebuildsOnlyTheChangedRow()
    {
        var layout = new CountingLayoutRenderer();
        var view = MakeView(layout);

        var items = Enumerable.Range(0, 50)
            .Select(i => new TestItem($"app-{i:D2}", "v1"))
            .ToList();

        view.SetAll(items);
        Assert.Equal(50, layout.CreateRowMarkupCalls);

        layout.CreateRowMarkupCalls = 0;
        view.Upsert(new TestItem("app-10", "v2"));

        // The whole point of the targeted update: only the changed row's markup is rebuilt,
        // not all N (which was the old O(N²) behaviour).
        Assert.Equal(1, layout.CreateRowMarkupCalls);
    }

    [Fact]
    public void SetAll_ThenRender_MakesItemsVisible()
    {
        var view = MakeView(new CountingLayoutRenderer());

        view.SetAll([new TestItem("a", "1"), new TestItem("b", "2")]);
        view.RenderIfNeeded();

        // An empty selection key would mean nothing got windowed — i.e. the MaxVisibleRows == 0
        // regression where the first render did Take(0) and showed nothing.
        Assert.Equal("a", view.GetSelectedItemKey());
    }

    [Fact]
    public void Upsert_DefersRendering_UntilRenderIfNeeded()
    {
        var animations = new CountingAnimationProvider();
        var view = MakeView(new CountingLayoutRenderer(), animations);

        view.SetAll([new TestItem("a", "1")]);
        view.RenderIfNeeded();

        // GetAnimations() is called once per non-empty render, so it doubles as a render probe.
        animations.GetAnimationsCalls = 0;

        view.Upsert(new TestItem("a", "2"));
        Assert.Equal(0, animations.GetAnimationsCalls); // background op must not render

        view.RenderIfNeeded();
        Assert.Equal(1, animations.GetAnimationsCalls); // rendering happens on the render thread
    }

    private static ListPanelView<TestItem> MakeView(
        CountingLayoutRenderer layout,
        CountingAnimationProvider? animations = null)
    {
        return new ListPanelView<TestItem>(
            new AllMatcher(),
            layout,
            new NoShortcuts(),
            animations ?? new CountingAnimationProvider(),
            onEnterNavigation: null,
            header: "Test",
            onAction: null,
            onActionNavigation: null,
            emptyStateMessage: null,
            windowHeight: () => 30,
            windowWidth: () => 120);
    }

    private sealed record TestItem(string Key, string Value) : IHasKey, IComparable<TestItem>
    {
        public int CompareTo(TestItem? other) => string.CompareOrdinal(Key, other?.Key);
    }

    private sealed class CountingLayoutRenderer : ILayoutRenderer<TestItem>
    {
        public int CreateRowMarkupCalls;

        public RowMarkup CreateRowMarkup(TestItem item)
        {
            CreateRowMarkupCalls++;
            return new RowMarkup { Key = item.Key }
                .Add("Name", new RowCell(new Markup(item.Key), new Markup(item.Key)));
        }

        public ColumnLayout<TestItem> CreateColumnLayout()
            => new(new Column<TestItem>("Name", i => i.Key));
    }

    private sealed class CountingAnimationProvider : IAnimationProvider
    {
        public int GetAnimationsCalls;

        public List<AnimationContext> GetAnimations()
        {
            GetAnimationsCalls++;
            return [];
        }

        public AnimationContext? GetAnimation(string key) => null;
    }

    private sealed class AllMatcher : ISearchMatcher<TestItem>
    {
        public bool TryMatch(TestItem app, string input)
            => app.Key.Contains(input, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NoShortcuts : IShortcutProvider<TestItem>
    {
        public Dictionary<TableIndex, ShortcutMap> Describe(TestItem? item) => new();
        public bool IsActionValid(TestItem? item, FunctionAction action) => false;
    }
}
