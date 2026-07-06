using Funcy.Console.Settings;
using Funcy.Core.Model;

namespace Funcy.Console.Ui.Shortcuts;

public class SettingShortcutProvider : IShortcutProvider<SettingItemDetails>
{
    public Dictionary<TableIndex, ShortcutMap> Describe(SettingItemDetails? item)
    {
        return new Dictionary<TableIndex, ShortcutMap>
        {
            { new TableIndex(0, 2), new ShortcutMap(ListPanelShortcuts.Filter, true) },
            { new TableIndex(0, 3), new ShortcutMap(ListPanelShortcuts.Edit, item is not null) }
        };
    }

    public bool IsActionValid(SettingItemDetails? getSelectedItem, FunctionAction action)
    {
        return false;
    }
}
