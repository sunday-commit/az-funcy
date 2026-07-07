using Funcy.Console.Ui.State;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class UiErrorLayoutRenderer : ILayoutRenderer<UiErrorEntry>
{
    // Table width is 115; Time (10) + Scope (30) leaves room for a wide, single-line message.
    private const int MaxMessageLength = 68;

    public RowMarkup CreateRowMarkup(UiErrorEntry item)
    {
        var time = item.TimestampUtc.ToString("HH:mm:ss");
        var scope = Markup.Escape(item.Scope);
        var message = Markup.Escape(Truncate(item.Message, MaxMessageLength));

        var rowMarkup = new RowMarkup { Key = item.Key };
        rowMarkup.Add("Time", new RowCell(UiStyles.CreateSelectedCell(time), new Markup(time)));
        rowMarkup.Add("Scope", new RowCell(UiStyles.CreateSelectedCell(scope), new Markup(scope)));
        rowMarkup.Add("Message",
            new RowCell(UiStyles.CreateSelectedCell(message), new Markup($"[{UiStyles.Danger}]{message}[/]")));

        return rowMarkup;
    }

    public ColumnLayout<UiErrorEntry> CreateColumnLayout()
        => new(
            new Column<UiErrorEntry>("Time", e => e.TimestampUtc, 10),
            new Column<UiErrorEntry>("Scope", e => e.Scope, 30),
            new Column<UiErrorEntry>("Message", e => e.Message));

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : string.Concat(value.AsSpan(0, max - 1), "…");
}
