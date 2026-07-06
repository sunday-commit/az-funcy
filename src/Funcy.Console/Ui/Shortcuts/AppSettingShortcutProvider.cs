using Funcy.Core.Model;

namespace Funcy.Console.Ui.Shortcuts;

public class AppSettingShortcutProvider : IShortcutProvider<AppSettingDetails>
{
    public Dictionary<TableIndex, ShortcutMap> Describe(AppSettingDetails? item)
    {
        return new Dictionary<TableIndex, ShortcutMap>
        {
            { new TableIndex(0, 2), new ShortcutMap(ListPanelShortcuts.Filter, true) },
            { new TableIndex(0, 3), new ShortcutMap(ListPanelShortcuts.Mask, item is not null) }
        };
    }

    public bool IsActionValid(AppSettingDetails? getSelectedItem, FunctionAction action)
        => action == FunctionAction.ToggleMask && getSelectedItem is not null;
}
