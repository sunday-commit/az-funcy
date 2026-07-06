using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class FunctionLayoutRenderer: ILayoutRenderer<FunctionDetails>
{
    private const int ListensToWidth = 34;

    public RowMarkup CreateRowMarkup(FunctionDetails item)
    {
        var rowMarkup = new RowMarkup
        {
            Key = item.Key
        };
        rowMarkup.Add("Name", new RowCell(UiStyles.CreateSelectedCell(item.Name), new Markup(item.Name)));
        rowMarkup.Add("Trigger", new RowCell(UiStyles.CreateSelectedCell(item.Trigger), new Markup(item.Trigger)));

        var listensTo = Truncate(item.ListensTo, ListensToWidth);
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
            new Column<FunctionDetails>("Name", f => f.Name, 34),
            new Column<FunctionDetails>("Trigger", f => f.Trigger, 20),
            new Column<FunctionDetails>("Listens to", f => f.ListensTo, ListensToWidth),
            new Column<FunctionDetails>("Msgs", f => f.ActiveMessages, 8, Alignment: Justify.Right),
            new Column<FunctionDetails>("DLQ", f => f.DeadLetteredMessages, 8, Alignment: Justify.Right));
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
