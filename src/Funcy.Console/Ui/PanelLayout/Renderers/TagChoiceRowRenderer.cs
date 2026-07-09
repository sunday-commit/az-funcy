using Funcy.Console.Settings;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

public class TagChoiceLayoutRenderer : ILayoutRenderer<TagChoice>
{
    public RowMarkup CreateRowMarkup(TagChoice item)
    {
        var glyph = item.Selected ? UiStyles.ToggleOn : UiStyles.ToggleOff;
        var name = item.Name.EscapeMarkup();
        var plain = $"{glyph} {name}";

        // Checked tags render in the normal colour; unchecked ones are dimmed so the selection
        // reads at a glance. The selected (cursor) row uses the plain form under the highlight.
        var styled = item.Selected ? plain : $"[{UiStyles.Hint}]{plain}[/]";

        var rowMarkup = new RowMarkup { Key = item.Key };
        rowMarkup.Add("Tag", new RowCell(UiStyles.CreateSelectedCell(plain), new Markup(styled)));
        return rowMarkup;
    }

    public ColumnLayout<TagChoice> CreateColumnLayout()
        => new(new Column<TagChoice>("Tag", i => i.Name, Flex: true));
}
