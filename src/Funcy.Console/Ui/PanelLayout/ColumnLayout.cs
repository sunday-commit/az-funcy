using Funcy.Console.Ui.Pagination.Sorters;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout;

public sealed record Column<T>(string Header, Func<T, object?>? Selector, int Width = 0, bool AnimationColumn = false, Justify? Alignment = null);

public sealed class ColumnLayout<T>(params Column<T>[] columns)
{
    public IReadOnlyList<Column<T>> Columns { get; } = columns;
}
