using Funcy.Console.Ui.State;
using Funcy.Core.Model;

namespace Funcy.Console.Ui.Shortcuts;

public class UiErrorShortcutProvider : IShortcutProvider<UiErrorEntry>
{
    public Dictionary<TableIndex, ShortcutMap> Describe(UiErrorEntry? item)
        => new()
        {
            { new TableIndex(0, 2), new ShortcutMap(ListPanelShortcuts.Filter, true) },
            { new TableIndex(0, 3), new ShortcutMap(ListPanelShortcuts.ClearIssues, true) },
        };

    // No Azure actions apply to error entries; Clear is routed by controller type in MainContainer.
    public bool IsActionValid(UiErrorEntry? item, FunctionAction action) => false;
}
