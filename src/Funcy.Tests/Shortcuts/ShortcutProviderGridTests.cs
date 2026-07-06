using Funcy.Console.Ui;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Core.Model;
using Xunit;
using AppContext = Funcy.Console.AppContext;

namespace Funcy.Tests.Shortcuts;

// Characterization: locks the exact shortcut grids (TableIndex -> shortcut + enabled) and
// IsActionValid results that all the feature branches build on.
public class ShortcutProviderGridTests
{
    private static FunctionAppDetails App(FunctionState state, int slots = 0, StatusType status = StatusType.Idle)
    {
        var app = new FunctionAppDetails { Name = "appA", State = state, ResourceGroup = "rg", Subscription = "sub", Id = "id" };
        app.Status.Status = status;
        app.Slots = Enumerable.Range(0, slots)
            .Select(i => new FunctionAppSlotDetails { Id = $"i{i}", FullName = $"appA/s{i}", Name = $"s{i}", State = FunctionState.Running })
            .ToList();
        return app;
    }

    // ---- FunctionAppShortcutProvider ----

    [Fact]
    public void FunctionApp_RunningIdleWithSlot_FullGrid()
    {
        var sut = new FunctionAppShortcutProvider(new UiStatusState());
        var grid = sut.Describe(App(FunctionState.Running, slots: 1));

        Assert.Equal(new ShortcutMap(ListPanelShortcuts.Filter, true), grid[new TableIndex(0, 2)]);
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.Swap, true), grid[new TableIndex(0, 3)]);
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.Refresh, true), grid[new TableIndex(0, 4)]);
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.RefreshAll, true), grid[new TableIndex(0, 5)]);
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.Start, false), grid[new TableIndex(1, 2)]);
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.Stop, true), grid[new TableIndex(1, 3)]);
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.ChangeSubscription, true), grid[new TableIndex(1, 4)]);
        Assert.Equal(7, grid.Count);
    }

    [Fact]
    public void FunctionApp_StoppedIdleNoSlots_StartEnabled_SwapAndStopDisabled()
    {
        var sut = new FunctionAppShortcutProvider(new UiStatusState());
        var grid = sut.Describe(App(FunctionState.Stopped, slots: 0));

        Assert.Equal(new ShortcutMap(ListPanelShortcuts.Swap, false), grid[new TableIndex(0, 3)]);
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.Start, true), grid[new TableIndex(1, 2)]);
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.Stop, false), grid[new TableIndex(1, 3)]);
    }

    [Fact]
    public void FunctionApp_InProgress_DisablesSwapRefreshStartStop()
    {
        var sut = new FunctionAppShortcutProvider(new UiStatusState());
        var grid = sut.Describe(App(FunctionState.Running, slots: 2, status: StatusType.InProgress));

        Assert.False(grid[new TableIndex(0, 3)].IsEnabled); // Swap
        Assert.False(grid[new TableIndex(0, 4)].IsEnabled); // Refresh
        Assert.False(grid[new TableIndex(1, 2)].IsEnabled); // Start
        Assert.False(grid[new TableIndex(1, 3)].IsEnabled); // Stop
    }

    [Fact]
    public void FunctionApp_NullSelection_OnlyGlobalShortcutsEnabled()
    {
        var sut = new FunctionAppShortcutProvider(new UiStatusState());
        var grid = sut.Describe(null);

        Assert.True(grid[new TableIndex(0, 2)].IsEnabled);  // Filter
        Assert.True(grid[new TableIndex(0, 5)].IsEnabled);  // RefreshAll
        Assert.True(grid[new TableIndex(1, 4)].IsEnabled);  // ChangeSubscription
        Assert.False(grid[new TableIndex(0, 3)].IsEnabled); // Swap
        Assert.False(grid[new TableIndex(0, 4)].IsEnabled); // Refresh
        Assert.False(grid[new TableIndex(1, 2)].IsEnabled); // Start
        Assert.False(grid[new TableIndex(1, 3)].IsEnabled); // Stop
    }

    [Fact]
    public void FunctionApp_RefreshAll_DisabledWhileInventoryValidating()
    {
        var state = new UiStatusState();
        state.BeginInventoryValidation();
        var sut = new FunctionAppShortcutProvider(state);
        var grid = sut.Describe(App(FunctionState.Running));
        Assert.False(grid[new TableIndex(0, 5)].IsEnabled);
        Assert.False(sut.IsActionValid(App(FunctionState.Running), FunctionAction.RefreshAll));
    }

    [Fact]
    public void FunctionApp_RefreshAll_DisabledWhileDetailsRefreshing()
    {
        var state = new UiStatusState();
        state.BeginDetailsRefresh();
        var sut = new FunctionAppShortcutProvider(state);
        Assert.False(sut.IsActionValid(App(FunctionState.Running), FunctionAction.RefreshAll));
    }

    [Theory]
    [InlineData(FunctionAction.ChangeSubscription, true)]
    [InlineData(FunctionAction.HideSubscription, false)]
    [InlineData(FunctionAction.ToggleSubscriptionVisibility, false)]
    public void FunctionApp_IsActionValid_MiscActions(FunctionAction action, bool expected)
    {
        var sut = new FunctionAppShortcutProvider(new UiStatusState());
        Assert.Equal(expected, sut.IsActionValid(App(FunctionState.Running, slots: 1), action));
    }

    [Fact]
    public void FunctionApp_IsActionValid_NullItem_StartStopSwapRefreshFalse()
    {
        var sut = new FunctionAppShortcutProvider(new UiStatusState());
        Assert.False(sut.IsActionValid(null, FunctionAction.Start));
        Assert.False(sut.IsActionValid(null, FunctionAction.Stop));
        Assert.False(sut.IsActionValid(null, FunctionAction.Swap));
        Assert.False(sut.IsActionValid(null, FunctionAction.Refresh));
    }

    // ---- FunctionAppSlotShortcutProvider ----

    private static FunctionAppSlotDetails Slot(FunctionState state = FunctionState.Running, StatusType status = StatusType.Idle)
    {
        var slot = new FunctionAppSlotDetails { Id = "i", FullName = "appA/s", Name = "s", State = state };
        slot.Status.Status = status;
        return slot;
    }

    [Fact]
    public void Slot_Grid_HasFilterAndSwap()
    {
        var parent = App(FunctionState.Running, slots: 1);
        var sut = new FunctionAppSlotShortcutProvider { FunctionApp = parent };
        var grid = sut.Describe(Slot());

        Assert.Equal(2, grid.Count);
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.Filter, true), grid[new TableIndex(0, 2)]);
        Assert.True(grid[new TableIndex(0, 3)].IsEnabled); // Swap enabled: parent app is Idle
    }

    [Fact]
    public void Slot_Swap_DisabledWhenParentAppBusy()
    {
        var parent = App(FunctionState.Running, slots: 1, status: StatusType.InProgress);
        var sut = new FunctionAppSlotShortcutProvider { FunctionApp = parent };
        var grid = sut.Describe(Slot());
        Assert.False(grid[new TableIndex(0, 3)].IsEnabled);
    }

    [Fact]
    public void Slot_IsActionValid_StartWhenStoppedAndIdle()
    {
        var sut = new FunctionAppSlotShortcutProvider { FunctionApp = App(FunctionState.Running) };
        Assert.True(sut.IsActionValid(Slot(FunctionState.Stopped, StatusType.Idle), FunctionAction.Start));
        Assert.False(sut.IsActionValid(Slot(FunctionState.Running, StatusType.Idle), FunctionAction.Start));
    }

    [Fact]
    public void Slot_IsActionValid_StopWhenRunningAndIdle()
    {
        var sut = new FunctionAppSlotShortcutProvider { FunctionApp = App(FunctionState.Running) };
        Assert.True(sut.IsActionValid(Slot(FunctionState.Running, StatusType.Idle), FunctionAction.Stop));
        Assert.False(sut.IsActionValid(Slot(FunctionState.Stopped, StatusType.Idle), FunctionAction.Stop));
    }

    [Fact]
    public void Slot_IsActionValid_RefreshFalse()
    {
        var sut = new FunctionAppSlotShortcutProvider { FunctionApp = App(FunctionState.Running) };
        Assert.False(sut.IsActionValid(Slot(), FunctionAction.Refresh));
    }

    // ---- FunctionShortcutProvider ----

    [Fact]
    public void Function_Grid_IsFilterOnly()
    {
        var sut = new FunctionShortcutProvider();
        var grid = sut.Describe(new FunctionDetails { Name = "fn", FunctionAppName = "appA", Trigger = "t" });
        Assert.Single(grid);
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.Filter, true), grid[new TableIndex(0, 2)]);
    }

    [Theory]
    [InlineData(FunctionAction.Start)]
    [InlineData(FunctionAction.Swap)]
    [InlineData(FunctionAction.Refresh)]
    public void Function_IsActionValid_AlwaysFalse(FunctionAction action)
    {
        var sut = new FunctionShortcutProvider();
        Assert.False(sut.IsActionValid(new FunctionDetails { Name = "fn", FunctionAppName = "appA", Trigger = "t" }, action));
    }

    // ---- SubscriptionShortcutProvider ----

    private static SubscriptionDetails Sub() => new() { Name = "Prod", Id = "id" };

    [Fact]
    public void Subscription_Grid_DefaultHideEmpty_ShowsShowAllShortcut()
    {
        // AppContext.HideEmptySubscriptions defaults to true => "Show all" shortcut is offered.
        var sut = new SubscriptionShortcutProvider(new AppContext(null!, null!, null!));
        var grid = sut.Describe(Sub());

        Assert.Equal(new ShortcutMap(ListPanelShortcuts.Filter, true), grid[new TableIndex(0, 2)]);
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.ShowAll, true), grid[new TableIndex(0, 3)]);
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.ToggleVisibility, true), grid[new TableIndex(0, 4)]);
    }

    [Fact]
    public void Subscription_Grid_AfterToggle_ShowsHideEmptyShortcut()
    {
        var ctx = new AppContext(null!, null!, null!);
        ctx.ToggleHideEmptySubscriptions(); // now false
        var sut = new SubscriptionShortcutProvider(ctx);
        var grid = sut.Describe(Sub());
        Assert.Equal(new ShortcutMap(ListPanelShortcuts.HideEmpty, true), grid[new TableIndex(0, 3)]);
    }

    [Fact]
    public void Subscription_Grid_NullSelection_DisablesToggleVisibility()
    {
        var sut = new SubscriptionShortcutProvider(new AppContext(null!, null!, null!));
        var grid = sut.Describe(null);
        Assert.False(grid[new TableIndex(0, 4)].IsEnabled);
    }

    [Theory]
    [InlineData(FunctionAction.HideSubscription, true)]
    [InlineData(FunctionAction.ToggleSubscriptionVisibility, true)]
    [InlineData(FunctionAction.Start, false)]
    [InlineData(FunctionAction.Refresh, false)]
    public void Subscription_IsActionValid(FunctionAction action, bool expected)
    {
        var sut = new SubscriptionShortcutProvider(new AppContext(null!, null!, null!));
        Assert.Equal(expected, sut.IsActionValid(Sub(), action));
    }
}
