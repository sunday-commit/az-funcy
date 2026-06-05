using Funcy.Console.Handlers;
using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Ui.Contexts;
using Funcy.Console.Ui.Controllers;
using Funcy.Console.Ui.Navigation;
using Funcy.Console.Ui.Panels;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Core.Model;

namespace Funcy.Console.Ui.Factory;

public sealed class ListPanelContextFactory(
    FunctionStateCoordinator coordinator,
    ListPanelFactory listPanelFactory,
    IUiStatusState uiStatusState,
    AppContext appContext)
{
    public ListPanelContext CreateRoot(Action invalidate)
    {
        var panel = listPanelFactory.CreateFunctionAppPanel([]);
        var controller = new FunctionAppListController(
            (IListPanelView<FunctionAppDetails>)panel,
            coordinator,
            uiStatusState,
            invalidate: invalidate);

        return new ListPanelContext
        {
            View = panel,
            Controller = controller
        };
    }

    public ListPanelContext CreateSubscriptionPanel(Action invalidate)
    {
        var panel = listPanelFactory.CreateSubscriptionPanel();
        var view = (IListPanelView<SubscriptionDetails>)panel;

        var allSubs = appContext.GetSnapshot().ToList();
        var subsToShow = appContext.HideEmptySubscriptions
            ? allSubs.Where(s => s.Current || !appContext.IsSubscriptionHidden(s.Id)).ToList()
            : allSubs;
        
        var controller = new SnapshotListController<SubscriptionDetails>(view, subsToShow, invalidate);
        return new ListPanelContext
        {
            View = panel,
            Controller = controller
        };
    }

    public ListPanelContext CreateFromNavigation(NavigationRequest request, Action invalidate)
    {
        var panel = listPanelFactory.Create(request);
        var app = coordinator.TryGet(request.Key)
                  ?? throw new InvalidOperationException($"App not found: {request.Key}");

        switch (request.Target)
        {
            case PanelTarget.Functions:
            {
                var view = (IListPanelView<FunctionDetails>)panel;
                var controller = new FunctionListController(view, app.Key, app.Functions, coordinator, uiStatusState, invalidate);
                return new ListPanelContext
                {
                    View = panel,
                    Controller = controller
                };
            }
            case PanelTarget.Slots:
            {
                var view = (IListPanelView<FunctionAppSlotDetails>)panel;
                var controller = new SnapshotListController<FunctionAppSlotDetails>(view, app.Slots, invalidate);
                return new ListPanelContext
                {
                    View = panel,
                    Controller = controller
                };
            }
            default:
                throw new NotSupportedException($"Unknown target: {request.Target}");
        }
    }
}
