namespace Funcy.Demo;

/// <summary>
/// Artificial delays the demo services pace their answers with. Without them every panel would be
/// populated before the first frame is drawn, and the spinner, the "Refreshing 3/13" counter and the
/// progressive fill that the recording is meant to show would never appear. Kept short enough that
/// the demo stays brisk.
/// </summary>
internal static class DemoLatency
{
    public static readonly TimeSpan Inventory = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan PerAppDetails = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan PerAppCounts = TimeSpan.FromMilliseconds(180);
    public static readonly TimeSpan Settings = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan SecretResolve = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan LogQuery = TimeSpan.FromMilliseconds(600);
    public static readonly TimeSpan Action = TimeSpan.FromMilliseconds(900);
    public static readonly TimeSpan Swap = TimeSpan.FromSeconds(3);
}
