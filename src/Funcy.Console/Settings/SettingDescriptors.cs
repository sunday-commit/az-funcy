namespace Funcy.Console.Settings;

// Single place where all editable settings are declared. Declaration order is the default
// list order. Add a new setting here and the whole settings view picks it up.
public static class SettingDescriptors
{
    public static readonly IReadOnlyList<SettingDescriptor> All =
    [
        new SettingDescriptor
        {
            Name = "TagColumns",
            Description = "Tag names shown as columns (comma-separated)",
            Format = s => string.Join(", ", s.TagColumns),
            Parse = SettingParsers.ParseTagColumns
        },
        new SettingDescriptor
        {
            Name = "SubscriptionRefreshIntervalMinutes",
            Description = "Minutes between Azure refreshes per subscription (0 = refresh on every switch)",
            Format = s => s.SubscriptionRefreshIntervalMinutes.ToString(),
            Parse = SettingParsers.ParseRefreshInterval
        },
        new SettingDescriptor
        {
            Name = "DefaultTagColumnWidth",
            Description = "Default width for tag columns (1-100)",
            Format = s => s.DefaultTagColumnWidth.ToString(),
            Parse = SettingParsers.ParseDefaultWidth
        },
        new SettingDescriptor
        {
            Name = "TagColumnWidths",
            Description = "Per-tag widths as key=value, comma-separated",
            Format = s => string.Join(", ", s.TagColumnWidths.Select(kv => $"{kv.Key}={kv.Value}")),
            Parse = SettingParsers.ParseTagColumnWidths
        },
        new SettingDescriptor
        {
            Name = "ShowServiceBusInAppList",
            Description = "Show Service Bus message counts in the app list",
            Format = s => s.ShowServiceBusInAppList ? "true" : "false",
            Parse = SettingParsers.ParseShowServiceBusInAppList
        }
    ];

    public static SettingDescriptor? Find(string name) =>
        All.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));

    public static IReadOnlyList<SettingItemDetails> BuildRows(FuncySettings settings) =>
        All.Select((d, i) => new SettingItemDetails
        {
            Order = i,
            Name = d.Name,
            Value = d.Format(settings),
            Description = d.Description
        }).ToList();
}
