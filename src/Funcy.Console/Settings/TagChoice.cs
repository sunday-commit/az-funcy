using Funcy.Core.Model;

namespace Funcy.Console.Settings;

// One row in the tag-columns checklist: a tag key and whether it's currently shown as a column.
// Sorted alphabetically; selection order (the column order) is tracked by the controller.
public sealed class TagChoice : IComparable<TagChoice>, IHasKey
{
    public required string Name { get; init; }
    public required bool Selected { get; init; }

    public string Key => Name;

    public int CompareTo(TagChoice? other)
        => other is null ? 1 : StringComparer.OrdinalIgnoreCase.Compare(Name, other.Name);
}
