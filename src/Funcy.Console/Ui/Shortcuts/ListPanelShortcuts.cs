namespace Funcy.Console.Ui.Shortcuts;

public static class ListPanelShortcuts
{
    public static readonly Shortcut Filter = new(ConsoleKey.F, "F", "Filter");
    public static readonly Shortcut Start = new(ConsoleKey.S, "S", "Start");
    public static readonly Shortcut Stop = new(ConsoleKey.T, "T", "Stop");
    public static readonly Shortcut Swap = new(ConsoleKey.W, "W", "Swap");
    public static readonly Shortcut DisableEnable = new(ConsoleKey.D, "D", "Disable/Enable");
    public static readonly Shortcut Refresh = new(ConsoleKey.R, "R", "Refresh");
    public static readonly Shortcut RefreshAll = new(ConsoleKey.A, "A", "Refresh all");
    public static readonly Shortcut ChangeSubscription = new(ConsoleKey.U, "U", "Subscription");
    public static readonly Shortcut HideEmpty = new(ConsoleKey.H, "H", "Hide empty");
    public static readonly Shortcut ShowAll = new(ConsoleKey.H, "H", "Show all");
    public static readonly Shortcut ToggleVisibility = new(ConsoleKey.X, "X", "Toggle hidden");
    public static readonly Shortcut Pin = new(ConsoleKey.P, "P", "Pin");
    public static readonly Shortcut Options = new(ConsoleKey.O, "O", "Options");
    public static readonly Shortcut Edit = new(ConsoleKey.Enter, "↩", "Edit");
}

public record TableIndex(int Row, int Column);
public record Shortcut(ConsoleKey Key, string DisplayChar, string Label);
public record ShortcutMap(Shortcut Shortcut, bool IsEnabled);