using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.Shortcuts;

public class FunctionAppShortcutProvider : IShortcutProvider<FunctionAppDetails>
{
    private readonly IUiStatusState _uiStatusState;

    public FunctionAppShortcutProvider(IUiStatusState uiStatusState)
    {
        _uiStatusState = uiStatusState;
    }

    public Dictionary<TableIndex, ShortcutMap> Describe(FunctionAppDetails? app)
    {
        var snapshot = _uiStatusState.GetSnapshot();
        var canRefreshAll = !snapshot.IsInventoryValidating && !snapshot.IsDetailsRefreshing;

        var shortcutList = new Dictionary<TableIndex, ShortcutMap>
        {
            {new TableIndex(0, 2), new ShortcutMap(ListPanelShortcuts.Filter, true)},
            {new TableIndex(0, 3), new ShortcutMap(ListPanelShortcuts.Swap, CanSwap(app))},
            {new TableIndex(0, 4), new ShortcutMap(ListPanelShortcuts.Refresh, CanRefresh(app))},
            {new TableIndex(0, 5), new ShortcutMap(ListPanelShortcuts.RefreshAll, canRefreshAll)},
            {new TableIndex(1, 2), new ShortcutMap(ListPanelShortcuts.Start, CanStart(app))},
            {new TableIndex(1, 3), new ShortcutMap(ListPanelShortcuts.Stop, CanStop(app))},
            {new TableIndex(1, 4), new ShortcutMap(ListPanelShortcuts.ChangeSubscription, true)}
        };
        return shortcutList;
    }

    public bool IsActionValid(FunctionAppDetails? getSelectedItem, FunctionAction action)
    {
        return action switch
        {
            FunctionAction.Start => getSelectedItem is not null && CanStart(getSelectedItem),
            FunctionAction.Stop => getSelectedItem is not null && CanStop(getSelectedItem),
            FunctionAction.Swap => getSelectedItem is not null && CanSwap(getSelectedItem),
            FunctionAction.Refresh => getSelectedItem is not null && CanRefresh(getSelectedItem),
            FunctionAction.RefreshAll => !_uiStatusState.GetSnapshot().IsInventoryValidating
                                        && !_uiStatusState.GetSnapshot().IsDetailsRefreshing,
            FunctionAction.ChangeSubscription => true,
            _ => false
        };
    }

    private static bool CanStart(FunctionAppDetails? app) =>
        app is not null && app.State == FunctionState.Stopped && app.Status.Status != StatusType.InProgress;

    private static bool CanStop(FunctionAppDetails? app) =>
        app is not null && app.State == FunctionState.Running && app.Status.Status != StatusType.InProgress;

    private static bool CanSwap(FunctionAppDetails? app) =>
        app is not null && app.Status.Status != StatusType.InProgress && app.Slots.Count >= 0;

    private static bool CanRefresh(FunctionAppDetails? app) =>
        app is not null && app.Status.Status != StatusType.InProgress;
}
