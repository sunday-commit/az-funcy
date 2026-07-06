using Funcy.Console.Handlers.Models;
using Spectre.Console;
using Funcy.Console.Ui.PanelLayout;
using Spectre.Console.Rendering;

namespace Funcy.Console.Ui.Renderers;

public class ListPanelTableRenderer<T>
{
    private readonly IReadOnlyList<Column<T>> _columns;
    public Table Table { get; set; }
    
    public ListPanelTableRenderer(ColumnLayout<T> columnLayout, int width = 115)
    {
        Table = new Table();
        Table.Border(TableBorder.None);
        Table.Width(width);
        _columns = columnLayout.Columns;
        var index = 1;
        foreach (var column in _columns)
        {
            var tableColumn = new TableColumn(UiStyles.CreateHeaderText(column.Header, column.Selector is not null ? index : null, false));
            index++;
            if (column.Width > 0)
            {
                tableColumn.Width(column.Width);
            }

            if (column.Alignment is { } alignment)
            {
                tableColumn.Alignment = alignment;
            }

            Table.AddColumn(tableColumn);
        }
    }

    public void ToggleSortingColumn(int? columnIndex, bool descending)
    {
        var index = 0;
        foreach (var column in _columns)
        {
            Table.Columns[index].Header(UiStyles.CreateHeaderText(column.Header, column.Selector is not null ? index+1 : null, descending,
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
                    markupsToRender.Add(new Markup(animationContext.AnimationFrame));
                }
                else
                {
                    markupsToRender.Add(row.GetCell(column.Header, isSelected));
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

        var cells = new List<IRenderable> { new Markup(message) };
        for (var i = 1; i < _columns.Count; i++)
        {
            cells.Add(new Markup(" "));
        }

        Table.AddRow(cells);
    }
}
