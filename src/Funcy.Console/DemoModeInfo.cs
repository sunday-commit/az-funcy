namespace Funcy.Console;

/// <summary>
/// Whether this process is running on canned demo data. Registered for both modes so the UI can ask
/// without knowing how the process was started; only <c>--demo</c> sets it to true.
/// </summary>
public sealed record DemoModeInfo(bool IsEnabled)
{
    public static readonly DemoModeInfo Disabled = new(false);
}
