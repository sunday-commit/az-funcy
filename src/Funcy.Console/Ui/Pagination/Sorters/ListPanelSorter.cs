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

        if (SupportsPinning)
        {
            var pinnedFirst = source.OrderByDescending(IsPinned);
            var withColumn = Desc ? pinnedFirst.ThenByDescending(sel) : pinnedFirst.ThenBy(sel);
            return withColumn.ToList();
        }

        var ordered = Desc
            ? source.OrderByDescending(sel)
            : source.OrderBy(sel);

        return ordered.ToList();
    }
}