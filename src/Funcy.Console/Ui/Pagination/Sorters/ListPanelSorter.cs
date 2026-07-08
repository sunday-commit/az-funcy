using Funcy.Core.Model;
using Funcy.Console.Ui.PanelLayout;

namespace Funcy.Console.Ui.Pagination.Sorters;

public sealed class ListPanelSorter<T> : ISorter<T>
{
    // Pinnable items always float to the top, ahead of the active column sort. This stays a
    // primary key on top of every ordering so pinned-first holds under default and column sorts.
    private static readonly bool SupportsPinning = typeof(IPinnable).IsAssignableFrom(typeof(T));
    private static bool IsPinned(T item) => item is IPinnable { IsPinned: true };

    private readonly Dictionary<int, SortDescriptor<T>> _map;
    public int? CurrentColumn { get; private set; }
    public bool Desc { get; private set; }

    public ListPanelSorter(ColumnLayout<T> layout)
    {
        _map = layout.Columns
            .Select((col, idx) =>
            {
                var selector = col.Selector ?? (_ => null);
                return new { idx, desc = new SortDescriptor<T>(idx, selector) };
            })
            .ToDictionary(x => x.idx+1, x => x.desc);
    }

    public void Toggle(int columnIndex)
    {
        if (!_map.ContainsKey(columnIndex))
            return;

        if (CurrentColumn == columnIndex)
        {
            Desc = !Desc;
            if (!Desc)
            {
                CurrentColumn = null;
            }
        }
        else
        {
            CurrentColumn = columnIndex;
            Desc = false;
        }
    }

    public IReadOnlyList<T> Sort(IReadOnlyList<T> source)
    {
        if (CurrentColumn is null)
        {
            // No active column: preserve the caller's natural order, floating pinned items up.
            return SupportsPinning
                ? source.OrderByDescending(IsPinned).ToList()
                : source.ToList();
        }

        var sel = _map[CurrentColumn.Value].Selector;

        // Rows with no value for this column (null selector result — e.g. an app whose Service Bus
        // counts haven't resolved, or a function with no queue) always sink to the bottom, in both
        // directions. Otherwise an ascending sort leads with a block of blanks; the user wants the
        // first real value (0) at the top and the empties out of the way.
        IOrderedEnumerable<T> ordered = SupportsPinning
            ? source.OrderByDescending(IsPinned).ThenBy(x => sel(x) is null)
            : source.OrderBy(x => sel(x) is null);

        ordered = Desc ? ordered.ThenByDescending(sel) : ordered.ThenBy(sel);

        return ordered.ToList();
    }
}