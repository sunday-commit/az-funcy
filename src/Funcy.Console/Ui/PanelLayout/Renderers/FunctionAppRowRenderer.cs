using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class FunctionAppLayoutRenderer(
    IReadOnlyList<string> tagColumns,
    Func<string, int> getColumnWidth,
    bool showServiceBusCounts = false) : ILayoutRenderer<FunctionAppDetails>
{
    // General rule for a sortable column's minimum width: header text + the sort marker the header
    // renderer appends ("(n) ↓") + the column's cell padding (1 each side). Falling short of that
    // wraps the marker onto a second line (what happened to Msgs/DLQ at 7, then still tight at 9).
    private const int SortMarkerAndPadding = 7; // "(n) ↓" (5) + padding (2)

    // Msgs/DLQ hold small right-aligned numbers, so the header ("Msgs" = 4) is the binding width:
    // 4 + SortMarkerAndPadding = 11.
    private const int CountWidth = 11;

    // The State/Status columns are enum-labelled. Sized a little wider than the widest label
    // ("Running"/"Stopped" = 7; "Refreshing..." = 13) so they get some breathing room instead of
    // sitting flush against the next column. The spare comes from the flexing right-hand margin.
    private const int StateWidth = 13;
    private const int StatusWidth = 16;

    // Name flexes together with the trailing (animation) column, so the spare width is split
    // between Name and a right-hand margin instead of Name (the sole flex column before) swallowing
    // all of it. The tag columns stay content-sized. A small floor keeps Name readable on a narrow
    // terminal; on a wide one it grows enough for the long app names.
    private const int NameWidth = 20;


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

    // Sort key for the Msgs/DLQ columns: the aggregated count, but null while the cell renders
    // empty (the app has no Service Bus functions or its counts haven't all resolved). Keeping the
    // key null in that case lets the sorter sink the blanks to the bottom instead of ordering them
    // as zeros ahead of the real values.
    private static object? CountSortKey(FunctionAppDetails app, Func<ServiceBusAppCounts, long> pick)
    {
        var counts = ServiceBusCountAggregator.Aggregate(app);
        return counts is { HasServiceBusFunctions: true, AllLoaded: true } ? pick(counts) : null;
    }

    public ColumnLayout<FunctionAppDetails> CreateColumnLayout()
    {
        var columns = new List<Column<FunctionAppDetails>>
        {
            new("Name", f => f.Name, NameWidth, Flex: true)
        };

        foreach (var tag in tagColumns)
        {
            var tagCopy = tag;
            columns.Add(new Column<FunctionAppDetails>(tagCopy, f => f.Tags.TryGetValue(tagCopy, out var v) ? v : null, getColumnWidth(tagCopy)));
        }

        columns.Add(new Column<FunctionAppDetails>("State", f => f.State, StateWidth));
        columns.Add(new Column<FunctionAppDetails>("Status", f => f.Status.ToDisplayLabel(), StatusWidth));

        if (showServiceBusCounts)
        {
            columns.Add(new Column<FunctionAppDetails>("Msgs", f => CountSortKey(f, c => c.ActiveMessages), CountWidth, Alignment: Justify.Right));
            columns.Add(new Column<FunctionAppDetails>("DLQ", f => CountSortKey(f, c => c.DeadLetteredMessages), CountWidth, Alignment: Justify.Right));
        }

        // Trailing animation column flexes so leftover width parks here as a right-hand margin
        // rather than inflating Name or a tag column.
        columns.Add(new Column<FunctionAppDetails>("", null, 10, AnimationColumn: true, Flex: true));

        return new ColumnLayout<FunctionAppDetails>([.. columns]);
    }
}
