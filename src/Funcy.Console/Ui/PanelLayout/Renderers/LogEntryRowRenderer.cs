using Funcy.Console.Ui.PanelLayout;
using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class LogEntryLayoutRenderer : ILayoutRenderer<LogEntryDetails>
{
    // Table total width is 115: 10 + 11 + 9 + 85.
    private const int MessageWidth = 85;

    public RowMarkup CreateRowMarkup(LogEntryDetails item)
    {
        var color = ColorFor(item);
        var rowMarkup = new RowMarkup { Key = item.Key };
        rowMarkup.Add("Time", Cell(item.Timestamp.ToLocalTime().ToString("HH:mm:ss"), color));
        rowMarkup.Add("Type", Cell(item.ItemType.ToString(), color));
        rowMarkup.Add("Sev", Cell(item.Severity ?? "", color));
        rowMarkup.Add("Message", Cell(Truncate(item.Message, MessageWidth - 1), color));
        return rowMarkup;
    }

    public ColumnLayout<LogEntryDetails> CreateColumnLayout()
    {
        return new ColumnLayout<LogEntryDetails>(
            new Column<LogEntryDetails>("Time", e => e.Timestamp, 10),
            new Column<LogEntryDetails>("Type", e => e.ItemType, 11),
            new Column<LogEntryDetails>("Sev", e => e.Severity, 9),
            new Column<LogEntryDetails>("Message", e => e.Message, MessageWidth));
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
