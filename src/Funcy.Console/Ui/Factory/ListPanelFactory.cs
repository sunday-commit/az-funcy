using Funcy.Console.Handlers;
using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Settings;
using Funcy.Console.Ui.Input;
using Funcy.Console.Ui.Navigation;
using Funcy.Console.Ui.Pagination;
using Funcy.Console.Ui.Pagination.Matchers;
using Funcy.Console.Ui.PanelLayout.Renderers;
using Funcy.Console.Ui.Panels;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Core.Model;
using Microsoft.Extensions.Options;

namespace Funcy.Console.Ui.Factory;

public sealed class ListPanelFactory(
    FunctionStateCoordinator coordinator,
    IAnimationProvider animationProvider,
    AppContext appContext,
    IOptions<FuncySettings> settings,
    IUiStatusState uiStatusState)
{
    public IListPanel CreateFromList<T>(
        ISearchMatcher<T> matcher,
        ILayoutRenderer<T> layout,
        IShortcutProvider<T> shortcuts,
        Func<T, NavigationRequest>? onEnter,
        string header,
        Func<FunctionAction, T, InputActionResult?>? onAction = null,
        Func<T, NavigationRequest>? onActionNavigation = null,
        Func<UiStatusSnapshot, string?>? emptyStateMessage = null)
        where T : IComparable<T>, IHasKey
    {
        return new ListPanelView<T>(
            matcher,
            layout,
            shortcuts,
            animationProvider,
            onEnter,
            header,
            onAction,
            onActionNavigation,
            emptyStateMessage);
    }


    public IListPanel CreateFunctionAppPanel(IReadOnlyList<FunctionAppDetails> apps)
    {
        return CreateFromList(
            new FunctionAppMatcher(settings.Value.TagColumns),
            new FunctionAppLayoutRenderer(settings.Value.TagColumns, tag =>
                settings.Value.TagColumnWidths.TryGetValue(tag, out var w) ? w : settings.Value.DefaultTagColumnWidth),
            new FunctionAppShortcutProvider(uiStatusState),
            f => new NavigationRequest(PanelTarget.Functions, f.Key),
            "Azure Function Apps",
            (act, app) => act switch
            {
                FunctionAction.Start => new InputActionResult(FunctionAction.Start, app),
                FunctionAction.Stop  => new InputActionResult(FunctionAction.Stop, app),
                FunctionAction.Swap  => OnSwapAction(app),
                _ => null
            },
            f => new NavigationRequest(PanelTarget.Slots, f.Key));

    }

    private InputActionResult? OnSwapAction(FunctionAppDetails app)
    {
        if (app.Slots.Count != 1)
        {
            return null;
        }
        
        var slotDetails = app.Slots[0];
        return new InputActionResult(FunctionAction.Swap, app, slotDetails);
    }

    public IListPanel CreateSubscriptionPanel()
    {
        var allSubs = appContext.GetSnapshot();
        string header;

        if (appContext.HideEmptySubscriptions)
        {
            var hiddenCount = allSubs.Count(s => !s.Current && appContext.IsSubscriptionHidden(s.Id));
            header = hiddenCount > 0
                ? $"Switch Subscription [dim grey]({hiddenCount} hidden - H to show)[/]"
                : "Switch Subscription";
        }
        else
        {
            header = "Switch Subscription";
        }

        return CreateFromList(new SubscriptionMatcher(),
            new SubscriptionLayoutRenderer(),
            new SubscriptionShortcutProvider(appContext),
            s =>
            {
                appContext.ChangeSubscription(s.Key);
                return new NavigationRequest(PanelTarget.FunctionApps, s.Key);
            },
            header);
    }
    
    public IListPanel Create(NavigationRequest request)
    {
        var app = coordinator.TryGet(request.Key);
        if (app is null)
        {
            throw new InvalidOperationException($"App not found: {request.Key}");
        }
        switch (request.Target)
        {
            case PanelTarget.Functions:
            {
                return CreateFromList(new FunctionMatcher(),
                    new FunctionLayoutRenderer(),
                    new FunctionShortcutProvider(),
                    null,
                    "Azure Functions",
                    emptyStateMessage: uiStatus =>
                    {
                        var currentApp = coordinator.TryGet(request.Key);
                        return currentApp is null ? null : UiStyles.CreateFunctionsEmptyStateText(currentApp, uiStatus);
                    });
            }
            case PanelTarget.Slots:
            {
                return CreateFromList(new FunctionAppSlotMatcher(),
                    new FunctionAppSlotLayoutRenderer(),
                    new FunctionAppSlotShortcutProvider() { FunctionApp = app },
                    null,
                    "Azure Function App Slots",
                    (act, slot) => act == FunctionAction.Swap
                        ? new InputActionResult(FunctionAction.Swap, app, slot)
                        : null);

            }
            default:
                throw new NotSupportedException($"Unknown target: {request.Target}");
        }
    }
}
