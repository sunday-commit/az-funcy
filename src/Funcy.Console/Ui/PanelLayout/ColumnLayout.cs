using Funcy.Console.Ui.Pagination.Sorters;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout;

// Width is the configured (minimum) width. Flex columns grow beyond it to absorb the spare
// width when the table is wider than the sum of configured widths; fixed columns never move.
public sealed record Column<T>(string Header, Func<T, object?>? Selector, int Width = 0, bool AnimationColumn = false, Justify? Alignment = null, bool Flex = false);

public sealed class ColumnLayout<T>(params Column<T>[] columns)
{
    public IReadOnlyList<Column<T>> Columns { get; } = columns;

    // Resolves the display width of each column for a target table width. Fixed columns keep
    // their configured width (they never shrink); the spare width (targetWidth minus the sum of
    // configured widths, floored at 0) is split evenly across the flex columns, with any
    // remainder handed to the earlier flex columns. Result is aligned index-for-index with Columns.
    public IReadOnlyList<int> Resolve(int targetWidth)
    {
        var resolved = Columns.Select(c => c.Width).ToArray();

        var flexIndexes = new List<int>();
        for (var i = 0; i < Columns.Count; i++)
        {
            if (Columns[i].Flex)
            {
                flexIndexes.Add(i);
            }
        }

        if (flexIndexes.Count == 0)
        {
            return resolved;
        }

        var configuredSum = resolved.Sum();
        var extra = Math.Max(0, targetWidth - configuredSum);
        if (extra == 0)
        {
            return resolved;
        }

        var share = extra / flexIndexes.Count;
        var remainder = extra % flexIndexes.Count;
        foreach (var index in flexIndexes)
        {
            resolved[index] += share;
            if (remainder > 0)
            {
                resolved[index]++;
                remainder--;
            }
        }

        return resolved;
    }
}
