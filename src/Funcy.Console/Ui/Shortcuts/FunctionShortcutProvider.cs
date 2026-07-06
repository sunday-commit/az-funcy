using Funcy.Core.Model;

namespace Funcy.Console.Ui.Shortcuts;

public class FunctionShortcutProvider : IShortcutProvider<FunctionDetails>
{
    public Dictionary<TableIndex, ShortcutMap> Describe(FunctionDetails? function)
    {
        var shortcutList = new Dictionary<TableIndex, ShortcutMap>
        {
            {new TableIndex(0, 2), new ShortcutMap(ListPanelShortcuts.Filter, true)},
            {new TableIndex(0, 3), new ShortcutMap(ListPanelShortcuts.DisableEnable, CanToggle(function))},
        };
        return shortcutList;
    }

    public bool IsActionValid(FunctionDetails? getSelectedItem, FunctionAction action)
    {
        return action switch
        {
            FunctionAction.ToggleDisabled => CanToggle(getSelectedItem),
            _ => false
        };
    }

    // Blocked while a toggle for this function is already in flight.
    private static bool CanToggle(FunctionDetails? function) =>
        function is not null && !function.IsToggling;
}
