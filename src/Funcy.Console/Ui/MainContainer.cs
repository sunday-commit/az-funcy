using Funcy.Console.Ui.ConsoleHelper;
using Funcy.Console.Ui.Contexts;
using Funcy.Console.Ui.Controllers;
using Funcy.Console.Ui.Factory;
using Funcy.Console.Ui.Panels;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Console.Ui.State;
using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui;

public sealed class MainContainer : IDisposable
{
    private readonly IActionDispatcher _actionDispatcher;
    private readonly IDetailsLoader _detailsLoader;
    private readonly UiStateMarkupProvider _uiStateMarkupProvider;
    private readonly IUiErrorLog _errorLog;
    private readonly TopPanel _topPanel;
    private readonly AppContext _appContext;
    private static readonly Markup EmptyMarkup = new("");
    private TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Stack<ListPanelContext> _contextStack = new();

    public readonly Layout MainLayout;
    private bool _searchMode;
    private readonly ListPanelContextFactory _listPanelContextFactory;

    private ListPanelContext Current => _contextStack.Peek();

    public MainContainer(ListPanelContextFactory listPanelContextFactory,
        IActionDispatcher actionDispatcher,
        IDetailsLoader detailsLoader,
        UiStateMarkupProvider uiStateMarkupProvider,
        IUiErrorLog errorLog,
        AppContext appContext)
    {
        _listPanelContextFactory = listPanelContextFactory;
        _actionDispatcher = actionDispatcher;
        _detailsLoader = detailsLoader;
        _uiStateMarkupProvider = uiStateMarkupProvider;
        _errorLog = errorLog;
        _appContext = appContext;
        _topPanel = new TopPanel(appContext);

        // New errors arrive on background threads; wake the render loop so the indicator updates.
        _errorLog.Changed += OnErrorLogChanged;

        var context = _listPanelContextFactory.CreateRoot(() => _tcs.TrySetResult());
        _contextStack.Push(context);
        
        MainLayout = new Layout("Main Layout")
            .SplitRows(
                new Layout("TopPanel").Size(5),
                new Layout("BodyPanel")
            );

        RefreshMainLayout();
    }
    
    public Task WaitForTriggerAsync()
    {
        return _tcs.Task;
    }

    public void ResetTrigger()
    {
        _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void RefreshMainLayout()
    {
        HandleUpdate();
        SyncSearchUi();

        MainLayout["TopPanel"].Update(_topPanel.Panel);
        MainLayout["BodyPanel"].Update(Current.View.Panel);
    }

    public void HandleUpdate()
    {
        // Render any pending background model changes here, on the render thread — the only
        // place allowed to touch the Spectre table. Background updates merely flag the view.
        Current.View.RenderIfNeeded();
        UpdateShortcuts();
        UpdateUiStatus();
    }

    private void OnErrorLogChanged() => _tcs.TrySetResult();

    private void UpdateUiStatus()
    {
        var uiStatusSnapshot = Current.View.GetUiStatusSnapshot();
        _topPanel.SetUiStatusText(_uiStateMarkupProvider.CreateMarkupFromUiStatusState(uiStatusSnapshot));
        _topPanel.SetErrorIndicator(UiStyles.CreateErrorIndicator(_errorLog.Count) ?? EmptyMarkup);
    }

    private void UpdateShortcuts()
    {
        var shortcuts = Current.View.GetShortcuts();
        _topPanel.UpdateShortcuts(shortcuts);
    }

    public void HandleInput(ConsoleKeyInfo keyInfo)
    {
        if (_searchMode)
        {
            _searchMode = Current.SearchInputManager.HandleInput(keyInfo);
            SyncSearchUi();
            return;
        }
        
        switch (keyInfo.Key)
        {
            case var key when key == ListPanelShortcuts.Filter.Key:
                EnterSearchMode();
                break;
            
            case var key when ConsoleKeyHelper.TryGetDigit(key) is { } digit:
                if (digit > 0)
                {
                    Current.View.SortViewBy(digit);
                }
                break;

            case var key when 
                key == ListPanelShortcuts.Start.Key ||
                key == ListPanelShortcuts.Stop.Key ||
                key == ListPanelShortcuts.Swap.Key:
                HandleActionKey(keyInfo.Key);
                break;
            
            case var key when
                key == ListPanelShortcuts.Refresh.Key:
                LoadDetails();
                break;

            case var key when
                key == ListPanelShortcuts.RefreshAll.Key:
                LoadAllDetails();
                break;
            
            case var key when
                key == ListPanelShortcuts.ChangeSubscription.Key:
                SubscriptionView();
                break;

            case var key when
                key == ListPanelShortcuts.HideEmpty.Key:
                ToggleSubscriptionFilter();
                break;

            case var key when
                key == ListPanelShortcuts.ToggleVisibility.Key:
                ToggleSelectedSubscriptionVisibility();
                break;

            case var key when
                key == ListPanelShortcuts.Issues.Key:
                IssuesView();
                break;

            case var key when
                key == ListPanelShortcuts.ClearIssues.Key:
                ClearIssues();
                break;

            case ConsoleKey.Delete:
                Current.SearchInputManager.ClearSearchText();
                SyncSearchUi();
                break;

            case ConsoleKey.Enter:
                TryPushNextPanelFromSelection();
                break;

            case ConsoleKey.Escape:
            case ConsoleKey.Spacebar:
                TryPopPanel();
                break;

            case ConsoleKey.PageUp:
            case ConsoleKey.PageDown:
            case ConsoleKey.UpArrow:
            case ConsoleKey.DownArrow:
                Current.View.HandleInput(keyInfo);
                UpdateShortcuts();
                break;

            default:
                Current.View.HandleInput(keyInfo);
                break;
        }
    }

    private void SubscriptionView()
    {
        if (!Current.View.IsActionValid(FunctionAction.ChangeSubscription))
        {
            return;
        }
        
        var nextContext = _listPanelContextFactory.CreateSubscriptionPanel(() => _tcs.TrySetResult());
        _contextStack.Push(nextContext);
        RefreshMainLayout();
    }

    private void IssuesView()
    {
        // Avoid stacking duplicate Issues panels if I is pressed while one is already open.
        if (Current.Controller is UiErrorListController)
        {
            return;
        }

        var nextContext = _listPanelContextFactory.CreateIssuesPanel(() => _tcs.TrySetResult());
        _contextStack.Push(nextContext);
        RefreshMainLayout();
    }

    private void ClearIssues()
    {
        // Clear is only meaningful inside the Issues panel.
        if (Current.Controller is not UiErrorListController)
        {
            return;
        }

        _errorLog.Clear();
    }

    private void ToggleSubscriptionFilter()
    {
        if (!Current.View.IsActionValid(FunctionAction.HideSubscription))
        {
            return;
        }

        _appContext.ToggleHideEmptySubscriptions();
        _contextStack.Pop().Controller.Dispose();
        SubscriptionView();
    }

    private void ToggleSelectedSubscriptionVisibility()
    {
        if (!Current.View.IsActionValid(FunctionAction.ToggleSubscriptionVisibility))
        {
            return;
        }

        var selectedKey = Current.View.GetSelectedItemKey();
        var sub = _appContext.GetSnapshot().FirstOrDefault(s => s.Key == selectedKey);
        if (sub is null) return;

        _appContext.ToggleSubscriptionVisibility(sub.Id);
        _contextStack.Pop();
        SubscriptionView();
    }

    private void LoadDetails()
    {
        var currentKey = Current.View.GetSelectedItemKey();

        if (!Current.View.IsActionValid(FunctionAction.Refresh))
        {
            return;
        }

        _detailsLoader.LoadDetails(currentKey);
    }

    private void LoadAllDetails()
    {
        if (!_detailsLoader.CanRefreshAll())
        {
            return;
        }

        _ = _detailsLoader.LoadAllDetailsAsync();
    }

    private void SyncSearchUi()
    {
        _topPanel.SetSearchText(Current.SearchInputManager.SearchMarkup);
        Current.View.SetSearchText(Current.SearchInputManager.SearchText);
    }
    
    private void EnterSearchMode()
    {
        _searchMode = true;
        Current.SearchInputManager.InitializeSearchMode();
        SyncSearchUi();
    }
    
    private void HandleActionKey(ConsoleKey key)
    {
        var action =
            key == ListPanelShortcuts.Start.Key ? FunctionAction.Start :
            key == ListPanelShortcuts.Stop.Key ? FunctionAction.Stop :
            FunctionAction.Swap;

        if (!Current.View.IsActionValid(action))
        {
            return;
        }
        
        var currentView = Current.View;

        if (currentView is IActionHandlingPanel actionPanel &&
            actionPanel.TryBuildAction(action, out var input)
            && input is not null)
        {
            _ = _actionDispatcher.Dispatch(input);
            return;
        }

        if (currentView.TryGetActionNavigationRequest(out var navRequest) && navRequest is not null)
        {
            var nextContext = _listPanelContextFactory.CreateFromNavigation(navRequest, () => _tcs.TrySetResult());
            _contextStack.Push(nextContext);
            RefreshMainLayout();
        }
    }
    
    private void TryPopPanel()
    {
        if (_contextStack.Count <= 1)
            return;

        _contextStack.Pop().Controller.Dispose();
        RefreshMainLayout();
    }

    private void TryPushNextPanelFromSelection()
    {
        if (Current.View.TryGetNavigationRequest(out var navigationRequest) && navigationRequest is not null)
        {
            if (navigationRequest.Target == PanelTarget.FunctionApps)
            {
                TryPopPanel();
            }
            else
            {
                var nextPanel = _listPanelContextFactory.CreateFromNavigation(navigationRequest, () => _tcs.TrySetResult());
                _contextStack.Push(nextPanel);
            }
            
            RefreshMainLayout();
        }
    }

    public void HandleResize()
    {
        Current.View.HandleResize();
    }

    public void HandleAnimation()
    {
        Current.View.RenderCurrentView();
        UpdateUiStatus();
    }

    public void Dispose()
    {
        _errorLog.Changed -= OnErrorLogChanged;

        foreach (var context in _contextStack)
        {
            context.Controller.Dispose();
        }

        _contextStack.Clear();
    }
}