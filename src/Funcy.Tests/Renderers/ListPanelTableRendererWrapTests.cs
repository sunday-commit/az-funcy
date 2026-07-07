using Funcy.Console.Ui.PanelLayout;
using Funcy.Console.Ui.Renderers;
using Funcy.Core.Model;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace Funcy.Tests.Renderers;

// Bug B: a cell wider than its column used to wrap, making a row occupy 2+ terminal lines while
// the paginator assumes 1 row == 1 line. Every column is now NoWrap and every cell crops with an
// ellipsis, so a row can never span more than one line regardless of content length.
public class ListPanelTableRendererWrapTests
{
    private sealed record Row(string Key, string Name, string Tag) : IHasKey;

    private static ColumnLayout<Row> Layout() => new(
        new Column<Row>("Name", r => r.Name, 20, Flex: true),
        new Column<Row>("Tag", r => r.Tag, 10));

    // Renders the table to plain text (no ansi/colors) at a generous profile width and returns
    // the non-empty visual lines, so tests can count how many terminal rows each table row takes.
    private static string[] RenderLines(Table table)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 500;
        console.Write(table);
        return writer.ToString()
            .Split('\n')
            .Select(l => l.TrimEnd())
            .Where(l => l.Length > 0)
            .ToArray();
    }

    private static RowMarkup MakeRow(Row r) =>
        new RowMarkup { Key = r.Key }
            .Add("Name", new RowCell(new Markup(r.Name), new Markup(r.Name)))
            .Add("Tag", new RowCell(new Markup(r.Tag), new Markup(r.Tag)));

    [Fact]
    public void AllColumns_AreNoWrap()
    {
        var renderer = new ListPanelTableRenderer<Row>(Layout(), 60);

        Assert.All(renderer.Table.Columns, c => Assert.True(c.NoWrap));
    }

    [Fact]
    public void Render_OverLongContent_ProducesExactlyOneLinePerRow()
    {
        var renderer = new ListPanelTableRenderer<Row>(Layout(), 60);

        // Header-only baseline so the assertion does not depend on how Spectre lays out the header.
        renderer.Render([], selectedIndex: -1, animatingKeys: null);
        var headerLines = RenderLines(renderer.Table).Length;

        var rows = new[]
        {
            MakeRow(new Row("a", new string('N', 200), new string('T', 80))),
            MakeRow(new Row("b", new string('X', 150), new string('Y', 90))),
            MakeRow(new Row("c", "short", "s")),
        };
        renderer.Render(rows, selectedIndex: 0, animatingKeys: null);

        var lines = RenderLines(renderer.Table);

        // One line of header chrome + exactly one line per data row: no row wrapped.
        Assert.Equal(headerLines + rows.Length, lines.Length);
    }

    [Fact]
    public void Render_OverLongContent_DoesNotSpillPastTableWidth()
    {
        const int tableWidth = 60;
        var renderer = new ListPanelTableRenderer<Row>(Layout(), tableWidth);

        renderer.Render([MakeRow(new Row("a", new string('N', 300), new string('T', 300)))],
            selectedIndex: 0, animatingKeys: null);

        var lines = RenderLines(renderer.Table);

        Assert.All(lines, l => Assert.True(l.Length <= tableWidth,
            $"line '{l}' ({l.Length}) exceeded table width {tableWidth}"));
    }

    [Fact]
    public void Render_WhenFixedWidthsExceedTableWidth_StaysSingleLine_AndDoesNotThrow()
    {
        // Over-budget: three fixed columns whose widths sum well past the target table width.
        var layout = new ColumnLayout<Row>(
            new Column<Row>("Name", r => r.Name, 40),
            new Column<Row>("Tag", r => r.Tag, 40),
            new Column<Row>("More", r => r.Tag, 40));
        var renderer = new ListPanelTableRenderer<Row>(layout, 60);

        var row = new RowMarkup { Key = "a" }
            .Add("Name", new RowCell(new Markup(new string('N', 100)), new Markup(new string('N', 100))))
            .Add("Tag", new RowCell(new Markup(new string('T', 100)), new Markup(new string('T', 100))))
            .Add("More", new RowCell(new Markup(new string('M', 100)), new Markup(new string('M', 100))));

        string[] lines = [];
        var ex = Record.Exception(() =>
        {
            renderer.Render([], -1, null);
            var headerLines = RenderLines(renderer.Table).Length;
            renderer.Render([row], 0, null);
            lines = RenderLines(renderer.Table);
            Assert.Equal(headerLines + 1, lines.Length);
        });

        Assert.Null(ex);
    }
}
