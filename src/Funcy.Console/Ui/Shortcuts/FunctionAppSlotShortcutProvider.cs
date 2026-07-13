using Funcy.Core.Model;

namespace Funcy.Console.Ui.Shortcuts;

public class FunctionAppSlotShortcutProvider : IShortcutProvider<FunctionAppSlotDetails>
{
    public required FunctionAppDetails FunctionApp { get; init; }
    
    public Dictionary<TableIndex, ShortcutMap> Describe(FunctionAppSlotDetails? slotDetails)
    {
        var shortcutList = new Dictionary<TableIndex, ShortcutMap>
        {
            {new TableIndex(0, 2), new ShortcutMap(ListPanelShortcuts.Filter, true)},
            // {new TableIndex(1, 2), new ShortcutMap(ListPanelShortcuts.Start, CanStart(app))}, //TODO: enable start and stop for slots
            // {new TableIndex(1, 3), new ShortcutMap(ListPanelShortcuts.Stop, CanStop(app))},
            {new TableIndex(0, 3), new ShortcutMap(ListPanelShortcuts.Swap, CanSwap(slotDetails))}
        };
        return shortcutList;
    }

    public bool IsActionValid(FunctionAppSlotDetails? getSelectedItem, FunctionAction action)
    {
        return action switch
        {
            FunctionAction.Start => CanStart(getSelectedItem),
            FunctionAction.Stop => CanStop(getSelectedItem),
            FunctionAction.Swap => CanSwap(getSelectedItem),
            _ => false
        };
    }

    private static bool CanStart(FunctionAppSlotDetails? slotDetails) =>
        slotDetails is null or { State: FunctionState.Stopped, Status.Status: StatusType.Idle };

    private static bool CanStop(FunctionAppSlotDetails? slotDetails) =>
        slotDetails is null or { State: FunctionState.Running, Status.Status: StatusType.Idle };

    private bool CanSwap(FunctionAppSlotDetails? slotDetails) =>
        slotDetails is null || FunctionApp.Status.Status == StatusType.Idle;
}