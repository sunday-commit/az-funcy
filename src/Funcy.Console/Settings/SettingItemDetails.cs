using Funcy.Core.Model;

namespace Funcy.Console.Settings;

// UI-only row model for the settings list panel. Order preserves declaration order so the
// list default-sorts the way the descriptors are declared (see SettingDescriptors).
public sealed class SettingItemDetails : IComparable<SettingItemDetails>, IHasKey
{
    public required int Order { get; init; }
    public required string Name { get; init; }
    public required string Value { get; init; }
    public required string Description { get; init; }

    public string Key => Name;

    public int CompareTo(SettingItemDetails? other) => other is null ? 1 : Order.CompareTo(other.Order);
}
