using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class FunctionAppLayoutRenderer(IReadOnlyList<string> tagColumns, Func<string, int> getColumnWidth) : ILayoutRenderer<FunctionAppDetails>
{
    public RowMarkup CreateRowMarkup(FunctionAppDetails item) => CreateRowMarkup(item, bypassed: false);

    public RowMarkup CreateBypassRowMarkup(FunctionAppDetails item) => CreateRowMarkup(item, bypassed: true);

    private RowMarkup CreateRowMarkup(FunctionAppDetails item, bool bypassed)
    {
        var rowMarkup = new RowMarkup
        {
            Key = item.Key
        };
        // Pinned apps get a glyph prefix; the Name sort key stays item.Name so ordering is unaffected.
        var pinPrefix = item.IsPinned ? $"{UiStyles.PinGlyph} " : string.Empty;

        Markup selectedName;
        Markup unselectedName;
        if (bypassed)
        {
            selectedName = UiStyles.CreateSelectedCell($"{pinPrefix}{UiStyles.BypassGlyph} {item.Name}");
            unselectedName = item.IsPinned
                ? new Markup($"[{UiStyles.Sort}]{UiStyles.PinGlyph}[/] " + $"[{UiStyles.Bypass}]{UiStyles.BypassGlyph} {item.Name}[/]")
                : UiStyles.CreateBypassNameCell(item.Name);
        }
        else
        {
            selectedName = UiStyles.CreateSelectedCell($"{pinPrefix}{item.Name}");
            unselectedName = item.IsPinned
                ? new Markup($"[{UiStyles.Sort}]{UiStyles.PinGlyph}[/] {item.Name}")
                : new Markup(item.Name);
        }
        rowMarkup.Add("Name", new RowCell(selectedName, unselectedName));

        foreach (var tag in tagColumns)
        {
            var value = item.Tags.TryGetValue(tag, out var v) ? v : string.Empty;
            rowMarkup.Add(tag, new RowCell(UiStyles.CreateSelectedCell(value), new Markup(value)));
        }

        rowMarkup.Add("State", new RowCell(UiStyles.CreateSelectedCell(item.State.ToDisplayLabel()), UiStyles.CreateStateCell(item.State)));
        rowMarkup.Add("Status", new RowCell(UiStyles.CreateSelectedCell(item.Status.ToDisplayLabel()), UiStyles.CreateStatusCell(item.Status)));
        rowMarkup.Add("", new RowCell(UiStyles.CreateSelectedCell(item.AnimatingFrame), new Markup(item.AnimatingFrame)));
        
        return rowMarkup;
    }

    public ColumnLayout<FunctionAppDetails> CreateColumnLayout()
    {
        var columns = new List<Column<FunctionAppDetails>>
        {
            new("Name", f => f.Name, 40)
        };

        foreach (var tag in tagColumns)
        {
            var tagCopy = tag;
            columns.Add(new Column<FunctionAppDetails>(tagCopy, f => f.Tags.TryGetValue(tagCopy, out var v) ? v : null, getColumnWidth(tagCopy)));
        }

        columns.Add(new Column<FunctionAppDetails>("State", f => f.State, 10));
        columns.Add(new Column<FunctionAppDetails>("Status", f => f.Status.ToDisplayLabel(), 20));
        columns.Add(new Column<FunctionAppDetails>("", null, 10, true));

        return new ColumnLayout<FunctionAppDetails>([.. columns]);
    }
}
