using Funcy.Console.Settings;
using Funcy.Console.Ui.Panels.Interfaces;

namespace Funcy.Console.Ui.Controllers;

// Checklist of the available tag keys used to choose which tags appear as columns in the app
// list. Enter toggles the selected tag; each toggle persists TagColumns immediately (so the app
// list rebuilds live) and the order tags were checked becomes the column order.
public sealed class TagSelectionController : ListPanelControllerBase<TagChoice>, IToggleSelectionController
{
    private readonly IFuncySettingsService _settings;
    private readonly ITagCatalog _catalog;
    private readonly Action _invalidate;

    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _gate = new();

    // Ordered selection = column order. Seeded from the current setting so existing columns stay.
    private readonly List<string> _selected;
    private IReadOnlyList<string> _available = [];

    public TagSelectionController(
        IListPanelView<TagChoice> view,
        IFuncySettingsService settings,
        ITagCatalog catalog,
        Action invalidate) : base(view)
    {
        _settings = settings;
        _catalog = catalog;
        _invalidate = invalidate;
        _selected = [.. settings.Current.TagColumns];

        // Show the already-selected tags immediately; the full catalog fills in asynchronously.
        RebuildRows();
        invalidate();
        _ = LoadAvailableAsync();
    }

    private async Task LoadAvailableAsync()
    {
        var keys = await _catalog.GetDistinctTagKeysAsync();
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        lock (_gate)
        {
            _available = keys;
        }

        RebuildRows();
        _invalidate();
    }

    public void ToggleSelected()
    {
        var key = View.GetSelectedItemKey();
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        // Unchecking removes it; checking appends so the newest selection is the last column.
        if (_selected.RemoveAll(t => string.Equals(t, key, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            _selected.Add(key);
        }

        var snapshot = _selected.ToArray();
        _ = _settings.UpdateAsync(s => s.TagColumns = snapshot);

        RebuildRows();
        _invalidate();
    }

    private void RebuildRows()
    {
        IReadOnlyList<string> available;
        lock (_gate)
        {
            available = _available;
        }

        // Union of catalog keys and the selected ones (a selected tag may not be in the catalog if
        // its apps aren't cached yet), de-duped case-insensitively and sorted for a stable list.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        foreach (var name in available.Concat(_selected))
        {
            if (seen.Add(name))
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);

        var selectedSet = new HashSet<string>(_selected, StringComparer.OrdinalIgnoreCase);
        var rows = names
            .Select(n => new TagChoice { Name = n, Selected = selectedSet.Contains(n) })
            .ToList();

        View.SetAll(rows);
    }

    public override void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        base.Dispose();
    }
}
