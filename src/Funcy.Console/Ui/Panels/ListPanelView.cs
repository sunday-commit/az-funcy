using Funcy.Console.Handlers;
using Funcy.Console.Handlers.Models;
using Funcy.Console.Ui.Input;
using Funcy.Console.Ui.Navigation;
using Funcy.Console.Ui.Pagination;
using Funcy.Console.Ui.Pagination.Matchers;
using Funcy.Console.Ui.Pagination.Sorters;
using Funcy.Console.Ui.PanelLayout;
using Funcy.Console.Ui.PanelLayout.Renderers;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Console.Ui.Renderers;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.Panels;

public class ListPanelView<T> : IActionHandlingPanel, IListPanelView<T> where T : IComparable<T>, IHasKey
{
    private readonly ISorter<T> _sorter;
    private readonly ISearchMatcher<T> _searchMatcher;
    private readonly ILayoutRenderer<T> _layoutRenderer;
    private readonly IShortcutProvider<T> _shortcuts;
    private readonly IAnimationProvider _animationProvider;
    private readonly Func<T, NavigationRequest?>? _onEnterNavigation;
    private readonly Func<T, NavigationRequest?>? _onActionNavigation;
    private readonly Func<FunctionAction, T, InputActionResult?>? _onAction; 
    private readonly Func<UiStatusSnapshot, string?>? _emptyStateMessage;

    private readonly Dictionary<string, RowMarkup> _markupCache = [];
    private List<RowMarkup> _visibleRows = [];
    
    public Panel Panel { get; }
    
    private readonly ListPanelPaginator _paginator;
    private readonly ListPanelTableRenderer<T> _renderer;
    private string _searchText = "";
    private IReadOnlyList<T> _snapshot = [];
    private Dictionary<string, T> _itemIndex = new();
    private UiStatusSnapshot _uiStatus;


    public ListPanelView(ISearchMatcher<T> searchMatcher,
        ILayoutRenderer<T> layoutRenderer, IShortcutProvider<T> shortcuts, IAnimationProvider animationProvider, Func<T, NavigationRequest>? onEnterNavigation, string header,
        Func<FunctionAction, T, InputActionResult?>? onAction, Func<T, NavigationRequest>? onActionNavigation,
        Func<UiStatusSnapshot, string?>? emptyStateMessage = null)

    {
        _searchMatcher = searchMatcher;
        _layoutRenderer = layoutRenderer;
        _shortcuts = shortcuts;
        _animationProvider = animationProvider;
        _onEnterNavigation = onEnterNavigation;
        _onAction = onAction;
        _onActionNavigation = onActionNavigation;
        _emptyStateMessage = emptyStateMessage;
        _paginator = new ListPanelPaginator();
        
        var columnLayout = _layoutRenderer.CreateColumnLayout();
        _renderer = new ListPanelTableRenderer<T>(columnLayout);
        _sorter = new ListPanelSorter<T>(columnLayout);
        
        Panel = new Panel(_renderer.Table)
        {
            Width = 139
        }
            .Header(header, Justify.Center)
            .BorderColor(Color.Orange1);
    }
    
    public void SetItems(IReadOnlyList<T> items)
    {
        _snapshot = items;
        _paginator.UpdateTotalRows(items.Count);
        _itemIndex = items.ToDictionary(x => x.Key);
        BuildCache();
        RefreshView();
    }

    public void SetUiStatus(UiStatusSnapshot uiStatusSnapshot)
    {
        _uiStatus = uiStatusSnapshot;
        RenderCurrentView();
    }

    public void HandleResize()
    {
        _paginator.UpdateMaxVisibleRows();
        RefreshView();
    }
    
    public void HandleInput(ConsoleKeyInfo keyInfo)
    {
        var scrolled = keyInfo.Key switch
        {
            ConsoleKey.PageUp   => _paginator.PageUp(),
            ConsoleKey.PageDown   => _paginator.PageDown(),
            ConsoleKey.UpArrow   => _paginator.MoveUp(),
            ConsoleKey.DownArrow => _paginator.MoveDown(),
            _                     => false
        };

        if (scrolled)
        {
            RefreshView();
        }
        else
        {
            RenderCurrentView();
        }
    }
    
    public void SetSearchText(string searchText)
    {
        if (!_searchText.Equals(searchText.Trim()))
        {
            _searchText = searchText.Trim();
            RefreshView();    
        }
    }

    private T? GetSelectedItem()
    {
        if (_visibleRows.Count == 0)
        {
            return default;
        }
        var selectedItemKey = _visibleRows[_paginator.SelectedIndex].Key;
        _itemIndex.TryGetValue(selectedItemKey, out var item);
        return item;
    }
    
    public string GetSelectedItemKey()
    {
        return GetSelectedItem()?.Key ?? "";
    }
    
    private void RefreshView()
    {
        RebuildVisibleRows();
        RenderCurrentView();
    }

    public void RenderCurrentView()
    {
        if (_visibleRows.Count == 0)
        {
            _renderer.RenderEmpty(GetEmptyStateMessage());
            return;
        }

        _renderer.Render(_visibleRows, _paginator.SelectedIndex, _animationProvider.GetAnimations());
    }

    private void RebuildVisibleRows()
    {
        var (appsToShow, totalCount) = GetVisibleItems();

        _visibleRows = appsToShow
            .Select(app => _markupCache[app.Key])
            .ToList();

        _paginator.UpdateTotalRows(totalCount);
    }

    private (IEnumerable<T> appsToShow, int totalCount) GetVisibleItems()
    {
        var sortedSnapshot = _sorter.Sort(_snapshot);
        
        if (string.IsNullOrWhiteSpace(_searchText))
        { 
            return (
                sortedSnapshot.Skip(_paginator.VisibleStartIndex).Take(_paginator.MaxVisibleRows),
                sortedSnapshot.Count
            );
        }

        var filtered = sortedSnapshot
            .Select(app => new { App = app, IsMatch = _searchMatcher.TryMatch(app, _searchText) })
            .Where(x => x.IsMatch)
            .ToList();
        
        var skip = filtered.Count < _paginator.MaxVisibleRows ? 0 : _paginator.VisibleStartIndex; //kanske kan skippas och vi kör _paginator.VisibleStartIndex bara enligt gippy

        return (
            filtered.Skip(skip).Take(_paginator.MaxVisibleRows).Select(x => x.App),
            filtered.Count
        );
    }
    
    private void BuildCache()
    {
        foreach (var app in _snapshot)
        {
            _markupCache[app.Key] = _layoutRenderer.CreateRowMarkup(app);
        }
    }

    private string? GetEmptyStateMessage()
    {
        if (!string.IsNullOrWhiteSpace(_searchText) || _snapshot.Count > 0)
        {
            return null;
        }

        return _emptyStateMessage?.Invoke(_uiStatus);
    }

    public bool TryGetNavigationRequest(out NavigationRequest? navigationRequest)
    {
        navigationRequest = null;
        if (_onEnterNavigation is null)
        {
            return false;
        }

        var selectedItem = GetSelectedItem();
        ArgumentNullException.ThrowIfNull(selectedItem);
        navigationRequest = _onEnterNavigation(selectedItem);
        return navigationRequest is not null;
    }
    
    public bool TryGetActionNavigationRequest(out NavigationRequest? navigationRequest)
    {
        navigationRequest = null;
        if (_onActionNavigation is null)
        {
            return false;
        }

        var selectedItem = GetSelectedItem();
        ArgumentNullException.ThrowIfNull(selectedItem);

        navigationRequest = _onActionNavigation(selectedItem);
        return navigationRequest is not null;
    }

    public Dictionary<TableIndex, ShortcutMap> GetShortcuts()
    {
        return _shortcuts.Describe(GetSelectedItem());
    }
    
    public UiStatusSnapshot GetUiStatusSnapshot() => _uiStatus;
    
    public bool IsActionValid(FunctionAction action)
    {
        var selectedItem = GetSelectedItem();
        return _shortcuts.IsActionValid(selectedItem, action);
    }
    
    public void SortViewBy(int keyInfoKey)
    {
        _sorter.Toggle(keyInfoKey);
        _renderer.ToggleSortingColumn(_sorter.CurrentColumn, _sorter.Desc);
        RefreshView();
    }

    public bool TryBuildAction(FunctionAction action, out InputActionResult? result)
    {
        result = null;
        if (_onAction is null)
            return false;

        var selected = GetSelectedItem();
        if (selected is null)
            return false;

        var built = _onAction(action, selected);
        if (built is null)
            return false;

        result = built;
        return true;
    }
}
