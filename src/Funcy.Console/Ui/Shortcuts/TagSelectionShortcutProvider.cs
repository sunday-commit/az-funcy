using Funcy.Console.Settings;
using Funcy.Core.Model;

namespace Funcy.Console.Ui.Shortcuts;

public class TagSelectionShortcutProvider : IShortcutProvider<TagChoice>
{
    public Dictionary<TableIndex, ShortcutMap> Describe(TagChoice? item)
    {
        return new Dictionary<TableIndex, ShortcutMap>
        {
            { new TableIndex(0, 2), new ShortcutMap(ListPanelShortcuts.Filter, true) },
            { new TableIndex(0, 3), new ShortcutMap(ListPanelShortcuts.Select, item is not null) }
        };
    }

    public bool IsActionValid(TagChoice? getSelectedItem, FunctionAction action) => false;
}
