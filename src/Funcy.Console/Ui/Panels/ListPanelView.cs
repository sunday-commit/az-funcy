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

    // Single source of truth for this panel, guarded by _gate. Items, their pre-rendered
    // markup, and the lazily-sorted view all live here — replacing the old controller-owned
    // ListPanelDataStore, the IReadOnlyList handoff, and a duplicate item index.
    private readonly Lock _gate = new();
    private readonly Dictionary<string, T> _items = new();
    private readonly Dictionary<string, RowMarkup> _markupCache = new(StringComparer.Ordinal);
    private List<T> _sortedCache = [];
    private bool _sortDirty = true;
    // Set by background model updates; consumed on the render thread in RenderIfNeeded so the
    // Spectre table is only ever written from one thread.
    private bool _needsRender;

    private List<RowMarkup> _visibleRows = [];

    public Panel Panel { get; }

    private readonly ListPanelPaginator _paginator;
    private readonly ListPanelTableRenderer<T> _renderer;
    private string _searchText = "";
    private UiStatusSnapshot _uiStatus;


    public ListPanelView(ISearchMatcher<T> searchMatcher,
        ILayoutRenderer<T> layoutRenderer, IShortcutProvider<T> shortcuts, IAnimationProvider animationProvider, Func<T, NavigationRequest>? onEnterNavigation, string header,
        Func<FunctionAction, T, InputActionResult?>? onAction, Func<T, NavigationRequest>? onActionNavigation,
        Func<UiStatusSnapshot, string?>? emptyStateMessage = null, Func<int>? windowHeight = null)

    {
        _searchMatcher = searchMatcher;
        _layoutRenderer = layoutRenderer;
        _shortcuts = shortcuts;
        _animationProvider = animationProvider;
        _onEnterNavigation = onEnterNavigation;
        _onAction = onAction;
        _onActionNavigation = onActionNavigation;
        _emptyStateMessage = emptyStateMessage;
        _paginator = new ListPanelPaginator(windowHeight);
        
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
    
    // SetAll/Upsert/Remove/SetUiStatus are invoked from background (controller) threads. They
    // only mutate the model and flag the view dirty; the controller's invalidate() then wakes
    // the render loop, which renders on the main thread via RenderIfNeeded.
    public void SetAll(IReadOnlyList<T> items)
    {
        lock (_gate)
        {
            _items.Clear();
            _markupCache.Clear();
            foreach (var item in items)
            {
                _items[item.Key] = item;
                _markupCache[item.Key] = _layoutRenderer.CreateRowMarkup(item);
            }

            _sortDirty = true;
            _needsRender = true;
        }
    }

    public void Upsert(T item)
    {
        lock (_gate)
        {
            _items[item.Key] = item;
            // Only this row's markup is rebuilt. This is what turns the old O(N²) refresh
            // (every row rebuilt on every single-item update) into O(1) work per change.
            _markupCache[item.Key] = _layoutRenderer.CreateRowMarkup(item);
            _sortDirty = true;
            _needsRender = true;
        }
    }

    public void Remove(string key)
    {
        lock (_gate)
        {
            if (!_items.Remove(key))
            {
                return;
            }

            _markupCache.Remove(key);
            _sortDirty = true;
            _needsRender = true;
        }
    }

    public void SetUiStatus(UiStatusSnapshot uiStatusSnapshot)
    {
        _uiStatus = uiStatusSnapshot;
        lock (_gate)
        {
            _needsRender = true;
        }
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
        var rows = _visibleRows;
        if (rows.Count == 0)
        {
            return default;
        }

        var selectedItemKey = rows[_paginator.SelectedIndex].Key;
        lock (_gate)
        {
            _items.TryGetValue(selectedItemKey, out var item);
            return item;
        }
    }
    
    public string GetSelectedItemKey()
    {
        return GetSelectedItem()?.Key ?? "";
    }

    // Keys of the rows currently windowed for render, in display order. Reads the same
    // list the renderer consumes, so it faithfully reflects filtering, the bypass, and order.
    public IReadOnlyList<string> GetVisibleKeys() => _visibleRows.Select(r => r.Key).ToList();

    private void RefreshView()
    {
        lock (_gate)
        {
            _needsRender = false;
        }

        RebuildVisibleRows();
        RenderCurrentView();
    }

    // Called on the render (main) thread. Background updates only flag the view dirty; the
    // rebuild + Spectre table mutation happens here so the table is never written concurrently.
    public void RenderIfNeeded()
    {
        lock (_gate)
        {
            if (!_needsRender)
            {
                return;
            }
        }

        RefreshView();
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
        List<RowMarkup> rows;
        int totalCount;

        lock (_gate)
        {
            var sorted = GetSortedItemsLocked();
            var candidates = FilterCandidatesLocked(sorted);

            totalCount = candidates.Count;

            // Keep short result sets visible: if everything fits, ignore the scroll offset.
            var skip = candidates.Count < _paginator.MaxVisibleRows ? 0 : _paginator.VisibleStartIndex;

            rows = candidates
                .Skip(skip)
                .Take(_paginator.MaxVisibleRows)
                // Bypassed rows are rendered on the fly (view-state cue, never highlighted);
                // matching rows reuse their cached, once-built markup.
                .Select(c => c.Bypassed ? _layoutRenderer.CreateBypassRowMarkup(c.Item) : _markupCache[c.Item.Key])
                .ToList();
        }

        _visibleRows = rows;
        _paginator.UpdateTotalRows(totalCount);
    }

    private readonly record struct Candidate(T Item, bool Bypassed);

    // Applies the search filter, then lets rows with an active operation (IOperationVisibility)
    // bypass a non-matching filter so an in-progress operation stays watchable. Bypassed rows
    // float to the top; matches keep their relative order below. Call while holding _gate.
    private List<Candidate> FilterCandidatesLocked(IReadOnlyList<T> sorted)
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            return sorted.Select(item => new Candidate(item, false)).ToList();
        }

        var matches = new List<Candidate>();
        var bypassed = new List<Candidate>();
        foreach (var item in sorted)
        {
            if (_searchMatcher.TryMatch(item, _searchText))
            {
                matches.Add(new Candidate(item, false));
            }
            else if (item is IOperationVisibility { HasActiveOperation: true })
            {
                bypassed.Add(new Candidate(item, true));
            }
        }

        bypassed.AddRange(matches);
        return bypassed;
    }

    // Sorts by the active column (falling back to the model's natural IComparable order) and
    // caches the result until the model or the sort column changes. Call while holding _gate.
    private List<T> GetSortedItemsLocked()
    {
        if (!_sortDirty)
        {
            return _sortedCache;
        }

        var items = _items.Values.ToList();
        items.Sort();                                 // natural order (stable base for column sort)
        _sortedCache = _sorter.Sort(items).ToList();  // active column, or unchanged if none
        _sortDirty = false;
        return _sortedCache;
    }
    
    private string? GetEmptyStateMessage()
    {
        int count;
        lock (_gate)
        {
            count = _items.Count;
        }

        if (!string.IsNullOrWhiteSpace(_searchText) || count > 0)
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
        if (selectedItem is null)
        {
            return false;
        }

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
        if (selectedItem is null)
        {
            return false;
        }

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
        lock (_gate)
        {
            _sortDirty = true;
        }

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
