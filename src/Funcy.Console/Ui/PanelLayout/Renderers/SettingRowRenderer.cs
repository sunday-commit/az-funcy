using Funcy.Console.Settings;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class SettingLayoutRenderer : ILayoutRenderer<SettingItemDetails>
{
    public RowMarkup CreateRowMarkup(SettingItemDetails item)
    {
        var value = item.Value.EscapeMarkup();
        var rowMarkup = new RowMarkup
        {
            Key = item.Key
        };
        rowMarkup.Add("Setting", new RowCell(UiStyles.CreateSelectedCell(item.Name), new Markup(item.Name)));
        rowMarkup.Add("Value", new RowCell(UiStyles.CreateSelectedCell(value), new Markup(value)));
        rowMarkup.Add("Description", new RowCell(UiStyles.CreateSelectedCell(item.Description), new Markup(item.Description)));

        return rowMarkup;
    }

    public ColumnLayout<SettingItemDetails> CreateColumnLayout()
    {
        return new ColumnLayout<SettingItemDetails>(
            new Column<SettingItemDetails>("Setting", i => i.Name, 34),
            new Column<SettingItemDetails>("Value", i => i.Value, 30),
            new Column<SettingItemDetails>("Description", i => i.Description));
    }
}
