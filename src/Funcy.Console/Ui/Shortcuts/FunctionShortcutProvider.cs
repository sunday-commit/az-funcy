using Funcy.Core.Model;

namespace Funcy.Console.Ui.Shortcuts;

public class FunctionShortcutProvider : IShortcutProvider<FunctionDetails>
{
    public Dictionary<TableIndex, ShortcutMap> Describe(FunctionDetails? slotDetails)
    {
        var shortcutList = new Dictionary<TableIndex, ShortcutMap>
        {
            {new TableIndex(0, 2), new ShortcutMap(ListPanelShortcuts.Filter, true)},
            {new TableIndex(0, 3), new ShortcutMap(ListPanelShortcuts.Refresh, true)},
        };
        return shortcutList;
    }

    public bool IsActionValid(FunctionDetails? getSelectedItem, FunctionAction action)
    {
        return action == FunctionAction.Refresh;
    }
}
