using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class FunctionLayoutRenderer: ILayoutRenderer<FunctionDetails>
{
    private const int ListensToWidth = 28;

    // Grows with the table (see SetResolvedWidths); starts at the configured minimum so the
    // first markup build before any resize still truncates sanely.
    private int _listensToWidth = ListensToWidth;

    public void SetResolvedWidths(IReadOnlyDictionary<string, int> resolvedWidths)
    {
        if (resolvedWidths.TryGetValue("Listens to", out var width) && width > 0)
        {
            _listensToWidth = width;
        }
    }

    public RowMarkup CreateRowMarkup(FunctionDetails item)
    {
        var stateLabel = item.IsDisabled ? "Disabled" : "Enabled";
        var rowMarkup = new RowMarkup
        {
            Key = item.Key
        };
        rowMarkup.Add("Name", new RowCell(UiStyles.CreateSelectedCell(item.Name), new Markup(item.Name)));
        rowMarkup.Add("Trigger", new RowCell(UiStyles.CreateSelectedCell(item.Trigger), new Markup(item.Trigger)));

        rowMarkup.Add("State", new RowCell(UiStyles.CreateSelectedCell(stateLabel),
            UiStyles.CreateFunctionStateCell(item.IsDisabled, item.IsToggling)));

        var listensTo = Truncate(item.ListensTo, _listensToWidth);
        rowMarkup.Add("Listens to", new RowCell(UiStyles.CreateSelectedCell(listensTo), new Markup(Markup.Escape(listensTo))));

        var msgs = CountText(item.ActiveMessages, item.CountStatus);
        rowMarkup.Add("Msgs", new RowCell(UiStyles.CreateSelectedCell(msgs), new Markup(msgs)));

        var dlq = CountText(item.DeadLetteredMessages, item.CountStatus);
        var dlqIsDanger = item.CountStatus == ServiceBusCountStatus.Loaded && item.DeadLetteredMessages > 0;
        var dlqUnselected = dlqIsDanger ? new Markup(UiStyles.CreateDangerText(dlq)) : new Markup(dlq);
        rowMarkup.Add("DLQ", new RowCell(UiStyles.CreateSelectedCell(dlq), dlqUnselected));

        return rowMarkup;
    }

    public ColumnLayout<FunctionDetails> CreateColumnLayout()
    {
        return new ColumnLayout<FunctionDetails>(
            new Column<FunctionDetails>("Name", f => f.Name, 28, Flex: true),
            new Column<FunctionDetails>("Trigger", f => f.Trigger, 15),
            new Column<FunctionDetails>("State", f => f.IsDisabled ? "Disabled" : "Enabled", 10),
            new Column<FunctionDetails>("Listens to", f => f.ListensTo, ListensToWidth, Flex: true),
            new Column<FunctionDetails>("Msgs", f => f.ActiveMessages, 7, Alignment: Justify.Right),
            new Column<FunctionDetails>("DLQ", f => f.DeadLetteredMessages, 7, Alignment: Justify.Right));
    }

    private static string CountText(long? value, ServiceBusCountStatus status) => status switch
    {
        ServiceBusCountStatus.Loading => "…",
        ServiceBusCountStatus.Failed => "?",
        ServiceBusCountStatus.Loaded => value?.ToString() ?? "?",
        _ => string.Empty
    };

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max)
        {
            return value;
        }

        return value[..(max - 1)] + "…";
    }
}
