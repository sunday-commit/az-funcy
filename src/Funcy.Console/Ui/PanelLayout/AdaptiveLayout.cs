namespace Funcy.Console.Ui.PanelLayout;

// Central width policy for the list/top panels. Historically the table was pinned to a
// hardcoded 115 while the surrounding panel was 139, so the content stayed cramped no matter
// how wide the terminal was. The table width now tracks the console: consoleWidth minus the
// panel chrome/safety reserve, clamped to [MinTableWidth, MaxTableWidth]. Below the floor the
// table stays at MinTableWidth and Spectre truncates (a narrow terminal keeps working).
public static class AdaptiveLayout
{
    public const int MinTableWidth = 115;
    public const int MaxTableWidth = 180;

    // Border (2) + horizontal padding (2) taken by the Panel around the table.
    public const int PanelChrome = 4;

    // A couple of extra columns kept clear so the panel never spills past the terminal edge.
    public const int SafetyGap = 2;

    public static int ResolveTableWidth(int consoleWidth)
        => Math.Clamp(consoleWidth - PanelChrome - SafetyGap, MinTableWidth, MaxTableWidth);

    // Panel width for a given resolved table width, so the list and top panels stay aligned.
    public static int PanelWidth(int tableWidth) => tableWidth + PanelChrome;
}
