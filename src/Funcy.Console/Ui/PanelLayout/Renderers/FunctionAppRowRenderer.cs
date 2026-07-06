using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class FunctionAppLayoutRenderer(
    IReadOnlyList<string> tagColumns,
    Func<string, int> getColumnWidth,
    bool showServiceBusCounts = false) : ILayoutRenderer<FunctionAppDetails>
{
    private const int NameWidth = 40;
    private const int CountWidth = 7;

    // With the two count columns enabled we trim the Name column by their combined width so the
    // fixed table budget is not pushed out further than the tag columns already do. A dedicated
    // adaptive-width PR is in flight; this is the graceful stop-gap, not proper distribution.
    private int NameColumnWidth => showServiceBusCounts ? NameWidth - (2 * CountWidth) : NameWidth;


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

        if (showServiceBusCounts)
        {
            AddServiceBusCells(rowMarkup, item);
        }

        rowMarkup.Add("", new RowCell(UiStyles.CreateSelectedCell(item.AnimatingFrame), new Markup(item.AnimatingFrame)));

        return rowMarkup;
    }

    // Aggregated Msgs/DLQ across the app's Service Bus functions. Rendered empty until every SB
    // function's counts have resolved (or when the app has none); DLQ > 0 uses the danger style,
    // matching the functions view.
    private static void AddServiceBusCells(RowMarkup rowMarkup, FunctionAppDetails item)
    {
        var counts = ServiceBusCountAggregator.Aggregate(item);
        var show = counts.HasServiceBusFunctions && counts.AllLoaded;

        var msgs = show ? counts.ActiveMessages.ToString() : string.Empty;
        rowMarkup.Add("Msgs", new RowCell(UiStyles.CreateSelectedCell(msgs), new Markup(msgs)));

        var dlq = show ? counts.DeadLetteredMessages.ToString() : string.Empty;
        var dlqIsDanger = show && counts.DeadLetteredMessages > 0;
        var dlqUnselected = dlqIsDanger ? new Markup(UiStyles.CreateDangerText(dlq)) : new Markup(dlq);
        rowMarkup.Add("DLQ", new RowCell(UiStyles.CreateSelectedCell(dlq), dlqUnselected));
    }

    public ColumnLayout<FunctionAppDetails> CreateColumnLayout()
    {
        var columns = new List<Column<FunctionAppDetails>>
        {
            new("Name", f => f.Name, 40, Flex: true)
        };

        foreach (var tag in tagColumns)
        {
            var tagCopy = tag;
            columns.Add(new Column<FunctionAppDetails>(tagCopy, f => f.Tags.TryGetValue(tagCopy, out var v) ? v : null, getColumnWidth(tagCopy)));
        }

        columns.Add(new Column<FunctionAppDetails>("State", f => f.State, 10));
        columns.Add(new Column<FunctionAppDetails>("Status", f => f.Status.ToDisplayLabel(), 20));

        if (showServiceBusCounts)
        {
            columns.Add(new Column<FunctionAppDetails>("Msgs", f => ServiceBusCountAggregator.Aggregate(f).ActiveMessages, CountWidth, Alignment: Justify.Right));
            columns.Add(new Column<FunctionAppDetails>("DLQ", f => ServiceBusCountAggregator.Aggregate(f).DeadLetteredMessages, CountWidth, Alignment: Justify.Right));
        }

        columns.Add(new Column<FunctionAppDetails>("", null, 10, true));

        return new ColumnLayout<FunctionAppDetails>([.. columns]);
    }
}
