using Funcy.Console.Settings;
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
    private readonly IUiErrorLog _errorLog;
    private readonly TopPanel _topPanel;
    private readonly AppContext _appContext;
    private static readonly Markup EmptyMarkup = new("");
    private TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Stack<ListPanelContext> _contextStack = new();

    public readonly Layout MainLayout;
    private bool _searchMode;
    private bool _editMode;
    private string? _editError;
    private readonly SettingEditManager _editManager = new();
    private readonly ListPanelContextFactory _listPanelContextFactory;
    private readonly ITagCatalog _tagCatalog;
    private readonly IFuncySettingsService _settingsService;
    // Tag suggestions land here from a background fetch and are applied on the render thread.
    private IReadOnlyList<string>? _pendingSuggestions;

    // Settings whose columns are baked into the Function Apps panel; a live change rebuilds it.
    private const string TagColumnsKey = "TagColumns";

    private ListPanelContext Current => _contextStack.Peek();

    public MainContainer(ListPanelContextFactory listPanelContextFactory,
        IActionDispatcher actionDispatcher,
        IDetailsLoader detailsLoader,
        UiStateMarkupProvider uiStateMarkupProvider,
        IUiErrorLog errorLog,
        AppContext appContext,
        ITagCatalog tagCatalog,
        IFuncySettingsService settingsService)
    {
        _listPanelContextFactory = listPanelContextFactory;
        _actionDispatcher = actionDispatcher;
        _detailsLoader = detailsLoader;
        _uiStateMarkupProvider = uiStateMarkupProvider;
        _errorLog = errorLog;
        _appContext = appContext;
        _tagCatalog = tagCatalog;
        _settingsService = settingsService;
        _settingsService.ColumnsChanged += RebuildRootPanel;
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
        ApplyPendingEditSuggestions();
        Current.View.RenderIfNeeded();
        UpdateShortcuts();
        UpdateUiStatus();
    }

    private void OnErrorLogChanged() => _tcs.TrySetResult();

    // Applies a completed tag-suggestion fetch on the render thread. Called from HandleUpdate,
    // which the background fetch wakes via the trigger; this keeps the edit-cell mutation off
    // the fetch's thread pool thread.
    private void ApplyPendingEditSuggestions()
    {
        var pending = Interlocked.Exchange(ref _pendingSuggestions, null);
        if (pending is not null && _editMode)
        {
            _editManager.SetSuggestions(pending);
            SyncEditUi();
        }
    }

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
                key == ListPanelShortcuts.Issues.Key:
                IssuesView();
                break;

            case var key when
                key == ListPanelShortcuts.ClearIssues.Key:
                ClearIssues();
                break;

            case var key when
                key == ListPanelShortcuts.Pin.Key:
                TogglePin();
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

    // Column settings changed: rebuild the root Function Apps panel in place so its columns
    // update live. Fired by IFuncySettingsService.ColumnsChanged during a settings commit, which
    // runs on the input thread (same as HandleInput), so direct stack manipulation is safe. Only
    // the bottom (root) is swapped; upper panels are preserved. The settings panel sits on top,
    // so the swap is invisible until the user escapes back to the root.
    private void RebuildRootPanel()
    {
        var upper = new List<ListPanelContext>();
        while (_contextStack.Count > 1)
        {
            upper.Add(_contextStack.Pop());
        }

        // Dispose the old root controller to unhook its coordinator/UI-status subscriptions
        // (event-leak rule, PR #20) before replacing it with a freshly-built root.
        _contextStack.Pop().Controller.Dispose();
        _contextStack.Push(_listPanelContextFactory.CreateRoot(() => _tcs.TrySetResult()));

        for (var i = upper.Count - 1; i >= 0; i--)
        {
            _contextStack.Push(upper[i]);
        }
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

    private void ClearIssues()
    {
        // Clear is only meaningful inside the Issues panel.
        if (Current.Controller is not UiErrorListController)
        {
            return;
        }

        _errorLog.Clear();
    }

    private void EnterEditMode(string key, string currentValue)
    {
        _editMode = true;
        _editError = null;
        _editManager.Begin(key, currentValue);
        SyncEditUi();

        if (key == TagColumnsKey)
        {
            _ = LoadTagSuggestionsAsync();
        }
    }

    private async Task LoadTagSuggestionsAsync()
    {
        var keys = await _tagCatalog.GetDistinctTagKeysAsync();
        if (keys.Count == 0)
        {
            return;
        }

        Interlocked.Exchange(ref _pendingSuggestions, keys);
        // Wake the render loop so HandleUpdate applies the suggestions on the render thread.
        _tcs.TrySetResult();
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

    private void TogglePin()
    {
        if (!Current.View.IsActionValid(FunctionAction.Pin))
        {
            return;
        }

        var selectedKey = Current.View.GetSelectedItemKey();
        if (string.IsNullOrEmpty(selectedKey))
        {
            return;
        }

        _ = _detailsLoader.TogglePinAsync(selectedKey);
    }

    private void LoadDetails()
    {
        if (!Current.View.IsActionValid(FunctionAction.Refresh))
        {
            return;
        }

        // On the Functions panel, Refresh re-runs the controller's Service Bus count fetch
        // rather than reloading the (already loaded) function app details.
        if (Current.Controller is ICountRefreshable refreshable)
        {
            refreshable.Refresh();
            return;
        }

        _detailsLoader.LoadDetails(Current.View.GetSelectedItemKey());
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
        _errorLog.Changed -= OnErrorLogChanged;
        _settingsService.ColumnsChanged -= RebuildRootPanel;

        foreach (var context in _contextStack)
        {
            context.Controller.Dispose();
        }

        _contextStack.Clear();
    }
}