using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class SubscriptionLayoutRenderer: ILayoutRenderer<SubscriptionDetails>
{
    public RowMarkup CreateRowMarkup(SubscriptionDetails item)
    {
        var rowMarkup = new RowMarkup
        {
            Key = item.Key
        };
        var current = item.Current ? " [[current]]" : "";
        rowMarkup.Add("Name",
            new RowCell(UiStyles.CreateSelectedCell(item.Name, current),
                UiStyles.CreateUnselectedCellWithStatus(item.Name, current)));
        
        return rowMarkup;
    }

    public ColumnLayout<SubscriptionDetails> CreateColumnLayout()
    {
        return new ColumnLayout<SubscriptionDetails>(new Column<SubscriptionDetails>("Name", (f) => f.Name, Flex: true));
    }
}