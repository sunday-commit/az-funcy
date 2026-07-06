using Funcy.Console.Ui.ConsoleHelper;
using Funcy.Console.Ui.Contexts;
using Funcy.Console.Ui.Controllers;
using Funcy.Console.Ui.Factory;
using Funcy.Console.Ui.Input;
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
    private readonly TopPanel _topPanel;
    private readonly AppContext _appContext;
    private TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Stack<ListPanelContext> _contextStack = new();

    public readonly Layout MainLayout;
    private bool _searchMode;
    private bool _editMode;
    private string? _editError;
    private readonly SettingEditManager _editManager = new();
    private readonly ListPanelContextFactory _listPanelContextFactory;

    private ListPanelContext Current => _contextStack.Peek();

    public MainContainer(ListPanelContextFactory listPanelContextFactory,
        IActionDispatcher actionDispatcher,
        IDetailsLoader detailsLoader,
        UiStateMarkupProvider uiStateMarkupProvider,
        AppContext appContext)
    {
        _listPanelContextFactory = listPanelContextFactory;
        _actionDispatcher = actionDispatcher;
        _detailsLoader = detailsLoader;
        _uiStateMarkupProvider = uiStateMarkupProvider;
        _appContext = appContext;
        _topPanel = new TopPanel(appContext);

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

    private void UpdateUiStatus()
    {
        var uiStatusSnapshot = Current.View.GetUiStatusSnapshot();
        _topPanel.SetUiStatusText(_uiStateMarkupProvider.CreateMarkupFromUiStatusState(uiStatusSnapshot));
    }

    private void UpdateShortcuts()
    {
        var shortcuts = Current.View.GetShortcuts();
        _topPanel.UpdateShortcuts(shortcuts);
    }

    public void HandleInput(ConsoleKeyInfo keyInfo)
    {
        if (_editMode)
        {
            HandleEditInput(keyInfo);
            return;
        }

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

            case var key when key == ListPanelShortcuts.Options.Key:
                SettingsView();
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
                key == ListPanelShortcuts.Swap.Key ||
                key == ListPanelShortcuts.DisableEnable.Key:
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
                key == ListPanelShortcuts.View.Key:
                AppSettingsView();
                break;

            case var key when
                key == ListPanelShortcuts.Mask.Key:
                ToggleMask();
                break;

            case ConsoleKey.Delete:
                Current.SearchInputManager.ClearSearchText();
                SyncSearchUi();
                break;

            case ConsoleKey.Enter:
                if (Current.Controller is IEditablePanel editable
                    && editable.TryBeginEdit(out var editKey, out var currentValue))
                {
                    EnterEditMode(editKey, currentValue);
                }
                else
                {
                    TryPushNextPanelFromSelection();
                }
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

    private void AppSettingsView()
    {
        if (!Current.View.IsActionValid(FunctionAction.ViewAppSettings))
        {
            return;
        }

        var selectedKey = Current.View.GetSelectedItemKey();
        if (string.IsNullOrEmpty(selectedKey))
        {
            return;
        }

        var nextContext = _listPanelContextFactory.CreateAppSettingsPanel(selectedKey, () => _tcs.TrySetResult());
        _contextStack.Push(nextContext);
        RefreshMainLayout();
    }

    private void SettingsView()
    {
        // Avoid stacking a second settings panel when one is already open.
        if (Current.Controller is SettingsListController)
        {
            return;
        }

        var nextContext = _listPanelContextFactory.CreateSettingsPanel(() => _tcs.TrySetResult());
        _contextStack.Push(nextContext);
        RefreshMainLayout();
    }

    private void ToggleMask()
    {
        if (!Current.View.IsActionValid(FunctionAction.ToggleMask))
        {
            return;
        }

        if (Current.Controller is IMaskToggleController maskController)
        {
            maskController.ToggleSelectedMask();
        }
    }

    private void EnterEditMode(string key, string currentValue)
    {
        _editMode = true;
        _editError = null;
        _editManager.Begin(key, currentValue);
        SyncEditUi();
    }

    private void HandleEditInput(ConsoleKeyInfo keyInfo)
    {
        var result = _editManager.HandleInput(keyInfo);
        switch (result)
        {
            case EditInputResult.Commit:
                CommitEdit();
                break;
            case EditInputResult.Cancel:
                ExitEditMode();
                break;
            default:
                _editError = null;
                SyncEditUi();
                break;
        }
    }

    private void CommitEdit()
    {
        if (Current.Controller is not IEditablePanel editable)
        {
            ExitEditMode();
            return;
        }

        // Single-user, small file: the persistence is effectively synchronous, so blocking the
        // input thread here is acceptable and keeps the edit flow simple.
        var error = editable.CommitEditAsync(_editManager.Key, _editManager.Text).GetAwaiter().GetResult();
        if (error is not null)
        {
            // Keep the user in edit mode with their input intact so they can correct it.
            _editError = error;
            SyncEditUi();
            return;
        }

        ExitEditMode();
        RefreshMainLayout();
    }

    private void ExitEditMode()
    {
        _editMode = false;
        _editError = null;
        SyncSearchUi();
    }

    private void SyncEditUi()
    {
        _topPanel.SetSearchText(_editManager.GetMarkup(_editError));
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
            key == ListPanelShortcuts.Swap.Key ? FunctionAction.Swap :
            FunctionAction.ToggleDisabled;

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
        foreach (var context in _contextStack)
        {
            context.Controller.Dispose();
        }

        _contextStack.Clear();
    }
}