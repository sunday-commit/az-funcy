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

// windowWidth is injected exactly like windowHeight; a resize that changes the resolved table
// width must re-flow columns, re-notify the layout renderer, and rebuild the markup cache.
public class ListPanelViewAdaptiveWidthTests
{
    [Fact]
    public void Resize_ToDifferentWidth_RebuildsMarkupCache()
    {
        var width = 300; // clamps to MaxTableWidth (180)
        var layout = new WidthTrackingLayoutRenderer();
        var view = MakeView(layout, () => width);

        view.SetAll([new TestItem("a"), new TestItem("b")]);
        Assert.Equal(2, layout.CreateRowMarkupCalls);

        layout.CreateRowMarkupCalls = 0;
        width = 130; // resolves to a smaller table width
        view.HandleResize();

        // Both cached rows are rebuilt at the new width.
        Assert.Equal(2, layout.CreateRowMarkupCalls);
    }

    [Fact]
    public void Resize_WithinTheSameClampedWidth_DoesNotRebuildCache()
    {
        var width = 300; // clamps to 180
        var layout = new WidthTrackingLayoutRenderer();
        var view = MakeView(layout, () => width);

        view.SetAll([new TestItem("a"), new TestItem("b")]);
        layout.CreateRowMarkupCalls = 0;

        width = 400; // still clamps to 180 -> no change, no rebuild
        view.HandleResize();

        Assert.Equal(0, layout.CreateRowMarkupCalls);
    }

    [Fact]
    public void Resize_PropagatesResolvedFlexWidthToLayoutRenderer()
    {
        var width = 140; // table width 134
        var layout = new WidthTrackingLayoutRenderer();
        var view = MakeView(layout, () => width);
        view.SetAll([new TestItem("a")]);

        // Layout: Name(10, flex) + Fixed(5). Extra = 134 - 15 = 119 -> Name = 129.
        Assert.Equal(129, layout.LastResolvedWidths!["Name"]);
        Assert.Equal(5, layout.LastResolvedWidths!["Fixed"]);
    }

    private static ListPanelView<TestItem> MakeView(WidthTrackingLayoutRenderer layout, Func<int> windowWidth)
        => new(
            new AllMatcher(),
            layout,
            new NoShortcuts(),
            new NoAnimations(),
            onEnterNavigation: null,
            header: "Test",
            onAction: null,
            onActionNavigation: null,
            emptyStateMessage: null,
            windowHeight: () => 30,
            windowWidth: windowWidth);

    private sealed record TestItem(string Key) : IHasKey, IComparable<TestItem>
    {
        public int CompareTo(TestItem? other) => string.CompareOrdinal(Key, other?.Key);
    }

    private sealed class WidthTrackingLayoutRenderer : ILayoutRenderer<TestItem>
    {
        public int CreateRowMarkupCalls;
        public IReadOnlyDictionary<string, int>? LastResolvedWidths;

        public RowMarkup CreateRowMarkup(TestItem item)
        {
            CreateRowMarkupCalls++;
            return new RowMarkup { Key = item.Key }
                .Add("Name", new RowCell(new Markup(item.Key), new Markup(item.Key)));
        }

        public ColumnLayout<TestItem> CreateColumnLayout()
            => new(
                new Column<TestItem>("Name", i => i.Key, 10, Flex: true),
                new Column<TestItem>("Fixed", i => i.Key, 5));

        public void SetResolvedWidths(IReadOnlyDictionary<string, int> resolvedWidths)
            => LastResolvedWidths = resolvedWidths;
    }

    private sealed class NoAnimations : IAnimationProvider
    {
        public List<AnimationContext> GetAnimations() => [];
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
