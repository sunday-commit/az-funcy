using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class FunctionAppSlotLayoutRenderer: ILayoutRenderer<FunctionAppSlotDetails>
{
    public RowMarkup CreateRowMarkup(FunctionAppSlotDetails item)
    {
        var rowMarkup = new RowMarkup
        {
            Key = item.Key
        };
        rowMarkup.Add("Name", new RowCell(UiStyles.CreateSelectedCell(item.Name), new Markup(item.Name)));
        rowMarkup.Add("State", new RowCell(UiStyles.CreateSelectedCell(item.State.ToDisplayLabel()), UiStyles.CreateStateCell(item.State)));
        
        return rowMarkup;
    }

    public ColumnLayout<FunctionAppSlotDetails> CreateColumnLayout()
    {
        return new ColumnLayout<FunctionAppSlotDetails>(new Column<FunctionAppSlotDetails>("Name", s => s.Name, Flex: true),
            new Column<FunctionAppSlotDetails>("State", s => s.State, 10));
    }
}