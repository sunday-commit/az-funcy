using Funcy.Console.Ui.PanelLayout;
using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class LogEntryLayoutRenderer : ILayoutRenderer<LogEntryDetails>
{
    // Fixed columns are sized for their content plus the sort marker the header appends ("(n) ↓")
    // and the cell padding (1 each side), so they read with breathing room rather than flush
    // together: "HH:mm:ss" (8), "Exception" (9), "Critical" (8). Message is the only flex column,
    // so it absorbs all the spare width the terminal offers.
    private const int TimeWidth = 12;
    private const int TypeWidth = 15;
    private const int SevWidth = 13;
    private const int MessageWidth = 40;

    // Grows with the table (see SetResolvedWidths); starts at the configured minimum so the first
    // markup build before any resize still truncates sanely.
    private int _messageWidth = MessageWidth;

    public void SetResolvedWidths(IReadOnlyDictionary<string, int> resolvedWidths)
    {
        if (resolvedWidths.TryGetValue("Message", out var width) && width > 0)
        {
            _messageWidth = width;
        }
    }

    public RowMarkup CreateRowMarkup(LogEntryDetails item)
    {
        var color = ColorFor(item);
        var rowMarkup = new RowMarkup { Key = item.Key };
        rowMarkup.Add("Time", Cell(item.Timestamp.ToLocalTime().ToString("HH:mm:ss"), color));
        rowMarkup.Add("Type", Cell(item.ItemType.ToString(), color));
        rowMarkup.Add("Sev", Cell(item.Severity ?? "", color));
        rowMarkup.Add("Message", Cell(Truncate(item.Message, _messageWidth - 1), color));
        return rowMarkup;
    }

    public ColumnLayout<LogEntryDetails> CreateColumnLayout()
    {
        return new ColumnLayout<LogEntryDetails>(
            new Column<LogEntryDetails>("Time", e => e.Timestamp, TimeWidth),
            new Column<LogEntryDetails>("Type", e => e.ItemType, TypeWidth),
            new Column<LogEntryDetails>("Sev", e => e.Severity, SevWidth),
            new Column<LogEntryDetails>("Message", e => e.Message, MessageWidth, Flex: true));
    }

    private static RowCell Cell(string text, string? color)
    {
        var escaped = Markup.Escape(text);
        var selected = UiStyles.CreateSelectedCell(escaped);
        var unselected = color is null ? new Markup(escaped) : new Markup($"[{color}]{escaped}[/]");
        return new RowCell(selected, unselected);
    }

    // Exceptions and errors stand out in danger red; warnings in yellow.
    private static string? ColorFor(LogEntryDetails item)
    {
        if (item.ItemType == LogItemType.Exception)
        {
            return UiStyles.Danger;
        }

        return item.Severity switch
        {
            "Critical" or "Error" => UiStyles.Danger,
            "Warning" => "yellow",
            _ => null,
        };
    }

    private static string Truncate(string text, int max)
    {
        var single = text.ReplaceLineEndings(" ");
        return single.Length <= max ? single : single[..(max - 1)] + "…";
    }
}
