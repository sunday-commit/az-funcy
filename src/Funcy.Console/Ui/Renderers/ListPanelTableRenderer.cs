using Funcy.Console.Handlers.Models;
using Spectre.Console;
using Funcy.Console.Ui.PanelLayout;
using Spectre.Console.Rendering;

namespace Funcy.Console.Ui.Renderers;

public class ListPanelTableRenderer<T>
{
    private readonly IReadOnlyList<Column<T>> _columns;
    private readonly ColumnLayout<T> _columnLayout;
    public Table Table { get; set; }

    public ListPanelTableRenderer(ColumnLayout<T> columnLayout, int width = AdaptiveLayout.MinTableWidth)
    {
        Table = new Table();
        Table.Border(TableBorder.None);
        _columnLayout = columnLayout;
        _columns = columnLayout.Columns;
        var index = 1;
        foreach (var column in _columns)
        {
            var tableColumn = new TableColumn(Header(UiStyles.CreateHeaderText(column.Header, column.Selector is not null ? index : null, false)))
            {
                // NoWrap plus per-cell Overflow.Ellipsis (applied in Render) guarantees every row
                // occupies exactly one terminal line: content wider than the resolved column width
                // is cropped, never wrapped. This is what keeps ListPanelPaginator's "1 row == 1
                // line" assumption true, so scrolling shows/hides exactly what is on screen.
                NoWrap = true
            };
            index++;

            if (column.Alignment is { } alignment)
            {
                tableColumn.Alignment = alignment;
            }

            Table.AddColumn(tableColumn);
        }

        ApplyWidth(width);
    }

    // A header markup that crops rather than wraps, matching the data cells (see Clip).
    private static Markup Header(string markup) => new(markup) { Overflow = Overflow.Ellipsis };

    // Forces a cell to render on a single line, cropping with an ellipsis at the column width.
    // Applied to every cell so no renderer can accidentally emit a wrapping (multi-line) row.
    private static Markup Clip(Markup markup)
    {
        markup.Overflow = Overflow.Ellipsis;
        return markup;
    }

    // Re-flows the table to a new target width: each column takes its resolved (flexed) width and
    // the table itself is pinned to the target so the panel is filled edge to edge. Must be called
    // on the render thread (it mutates the Spectre table).
    public void ApplyWidth(int tableWidth)
    {
        Table.Width(tableWidth);
        var resolved = _columnLayout.Resolve(tableWidth);
        for (var i = 0; i < _columns.Count; i++)
        {
            if (resolved[i] > 0)
            {
                Table.Columns[i].Width(resolved[i]);
            }
        }
    }

    public void ToggleSortingColumn(int? columnIndex, bool descending)
    {
        var index = 0;
        foreach (var column in _columns)
        {
            Table.Columns[index].Header = Header(UiStyles.CreateHeaderText(column.Header, column.Selector is not null ? index+1 : null, descending,
                columnIndex is not null && columnIndex.Value == index + 1));
            index++;
        }
    }

    public void Render(IEnumerable<RowMarkup> rows, int selectedIndex, List<AnimationContext>? animatingKeys)
    {
        Table.Rows.Clear();

        var i = 0;
        foreach (var row in rows)
        {
            List<IRenderable> markupsToRender = [];
            foreach (var column in _columns)
            {
                var isSelected = i == selectedIndex;

                if (column.AnimationColumn && animatingKeys is not null)
                {
                    var animationContext = animatingKeys.FirstOrDefault(a => a.FunctionAppKey == row.Key);
                    if (animationContext is null) continue;
                    markupsToRender.Add(Clip(new Markup(animationContext.AnimationFrame)));
                }
                else
                {
                    markupsToRender.Add(Clip(row.GetCell(column.Header, isSelected)));
                }
            }

            Table.AddRow(markupsToRender);
            i++;
        }
    }

    public void RenderEmpty(string? message = null)
    {
        Table.Rows.Clear();

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var cells = new List<IRenderable> { Clip(new Markup(message)) };
        for (var i = 1; i < _columns.Count; i++)
        {
            cells.Add(Clip(new Markup(" ")));
        }

        Table.AddRow(cells);
    }
}
