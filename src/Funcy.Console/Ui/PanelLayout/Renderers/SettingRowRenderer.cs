using Funcy.Console.Settings;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class SettingLayoutRenderer : ILayoutRenderer<SettingItemDetails>
{
    public RowMarkup CreateRowMarkup(SettingItemDetails item)
    {
        var rowMarkup = new RowMarkup
        {
            Key = item.Key
        };
        rowMarkup.Add("Setting", new RowCell(UiStyles.CreateSelectedCell(item.Name), new Markup(item.Name)));

        var (plain, styled) = FormatValue(item);
        rowMarkup.Add("Value", new RowCell(UiStyles.CreateSelectedCell(plain), new Markup(styled)));

        rowMarkup.Add("Description", new RowCell(UiStyles.CreateSelectedCell(item.Description), new Markup(item.Description)));

        return rowMarkup;
    }

    // Returns the value cell as (plain, styled): the plain form is markup-safe for the selected
    // (highlighted) cell; the styled form carries colour for the unselected cell.
    private static (string Plain, string Styled) FormatValue(SettingItemDetails item)
    {
        switch (item.Kind)
        {
            case SettingKind.Toggle:
            {
                var plain = item.IsOn ? $"{UiStyles.ToggleOn} On" : $"{UiStyles.ToggleOff} Off";
                var styled = item.IsOn ? plain : $"[{UiStyles.Hint}]{plain}[/]";
                return (plain, styled);
            }
            case SettingKind.TagSelection when string.IsNullOrWhiteSpace(item.Value):
                return ("none", $"[{UiStyles.Hint}]none[/]");
            default:
            {
                var escaped = item.Value.EscapeMarkup();
                return (escaped, escaped);
            }
        }
    }

    public ColumnLayout<SettingItemDetails> CreateColumnLayout()
    {
        return new ColumnLayout<SettingItemDetails>(
            new Column<SettingItemDetails>("Setting", i => i.Name, 34),
            new Column<SettingItemDetails>("Value", i => i.Value, 30),
            new Column<SettingItemDetails>("Description", i => i.Description, Flex: true));
    }
}
