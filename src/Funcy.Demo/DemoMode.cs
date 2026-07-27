namespace Funcy.Demo;

/// <summary>
/// Entry point for the canned-data demo used to record the README screencast.
/// </summary>
/// <remarks>
/// Demo mode shows fabricated data, so it is deliberately reachable only through an explicit
/// <c>--demo</c> argument. It is never a fallback: a failing Azure call surfaces as an error, it
/// does not degrade into fake numbers. The UI is marked as demo data while the mode is active.
/// </remarks>
public static class DemoMode
{
    public const string Flag = "--demo";

    /// <summary>Name of the data sub-directory demo mode keeps its database and settings in, so a
    /// demo run never touches the real inventory cache or the user's own settings.</summary>
    public const string DataDirectoryName = "demo";

    /// <summary>True only when the exact <c>--demo</c> argument was passed.</summary>
    public static bool IsRequested(string[] args) =>
        args.Any(arg => string.Equals(arg, Flag, StringComparison.Ordinal));
}
