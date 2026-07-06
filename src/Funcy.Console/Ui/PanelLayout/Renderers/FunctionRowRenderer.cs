using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class FunctionLayoutRenderer: ILayoutRenderer<FunctionDetails>
{
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

        return rowMarkup;
    }

    public ColumnLayout<FunctionDetails> CreateColumnLayout()
    {
        return new ColumnLayout<FunctionDetails>(
            new Column<FunctionDetails>("Name", f => f.Name),
            new Column<FunctionDetails>("Trigger", f => f.Trigger),
            new Column<FunctionDetails>("State", f => f.IsDisabled ? "Disabled" : "Enabled", 12));
    }
}
