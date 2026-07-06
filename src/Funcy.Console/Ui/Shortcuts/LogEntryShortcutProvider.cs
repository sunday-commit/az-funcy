using Funcy.Core.Model;

namespace Funcy.Console.Ui.Shortcuts;

public class LogEntryShortcutProvider : IShortcutProvider<LogEntryDetails>
{
    public Dictionary<TableIndex, ShortcutMap> Describe(LogEntryDetails? item)
    {
        return new Dictionary<TableIndex, ShortcutMap>
        {
            { new TableIndex(0, 2), new ShortcutMap(ListPanelShortcuts.Filter, true) },
            { new TableIndex(0, 4), new ShortcutMap(ListPanelShortcuts.Refresh, true) },
            { new TableIndex(1, 5), new ShortcutMap(ListPanelShortcuts.TypeFilter, true) },
        };
    }

    // Refresh/type-filter are handled directly by the controller, not the action pipeline.
    public bool IsActionValid(LogEntryDetails? getSelectedItem, FunctionAction action) => false;
}
