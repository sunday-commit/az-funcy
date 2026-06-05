namespace Funcy.Console.Settings;

public sealed class FuncySettings
{
    public string[] TagColumns { get; set; } = [];
    public int SubscriptionRefreshIntervalMinutes { get; set; } = 60;
    public int DefaultTagColumnWidth { get; set; } = 20;
    public Dictionary<string, int> TagColumnWidths { get; set; } = [];
}
