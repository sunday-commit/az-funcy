namespace Funcy.Console.Settings;

public sealed class FuncySettings
{
    public string[] TagColumns { get; set; } = [];
    public int SubscriptionRefreshIntervalMinutes { get; set; } = 60;
    public int DefaultTagColumnWidth { get; set; } = 20;
    public Dictionary<string, int> TagColumnWidths { get; set; } = [];

    // When enabled, the Function Apps list gains aggregated Msgs/DLQ columns summed across each
    // app's Service Bus-triggered functions. Off by default: it costs extra ARM calls per app.
    public bool ShowServiceBusInAppList { get; set; }
}
