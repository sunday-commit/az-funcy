using Funcy.Console.Ui.Shortcuts;
using Funcy.Core.Model;
using Funcy.Infrastructure.Azure;
using Spectre.Console;

namespace Funcy.Console.Ui;

public static class UiStyles
{
    private static readonly bool Unicode = AnsiConsole.Profile.Capabilities.Unicode;

    public const string Label = "bold yellow";
    public const string Shortcut = "bold purple_2";
    public const string Danger = "bold red";
    public const string Hint = "gray";
    public const string Sort = "yellow";
    public const string Warning = "bold orange1";
    public const string Bypass = "grey";

    private static readonly string ArrowUp = Unicode ? "↑" : "^";
    private static readonly string ArrowDown = Unicode ? "↓" : "v";
    private static readonly string WarningSign = Unicode ? "⚠" : "!";
    private static readonly string WarnGlyph = Unicode ? "⚠" : "!";
    public static readonly string PinGlyph = Unicode ? "★" : "*";

    // Marks a row kept visible by the operation-status bypass rather than a filter match.
    public static readonly string BypassGlyph = Unicode ? "•" : "*";

    public static Markup CreateLabelMarkup(string text) => new($"[{Label}]{text}[/]");

    public static Markup CreateShortcutMarkup(string shortcut, string description, bool isEnabled = true)
        => new($"[{(isEnabled ? Shortcut : Hint)}]<{shortcut}>[/] [{Hint}]{description}[/]");

    public static string CreateDangerText(string text) => $"[{Danger}]{text}[/]";

    public static string CreateHeaderText(string text, int? index, bool descending, bool isActiveColumn = false)
    {
        var arrow = isActiveColumn ? (descending ? ArrowDown : ArrowUp) : "";
        var sorting = index is not null ? $"[{Sort}]({index}) {arrow}[/]" : "";
        return $"[bold]{text}[/]{sorting}";
    }
    
    // Compact top-panel indicator, e.g. "⚠ 3 errors (I)". Null when there is nothing to show.
    public static Markup? CreateErrorIndicator(int count)
    {
        if (count <= 0)
        {
            return null;
        }

        var label = count == 1 ? "error" : "errors";
        return new Markup($"[{Danger}]{WarningSign} {count} {label}[/] [{Shortcut}]({ListPanelShortcuts.Issues.DisplayChar})[/]");
    }

    public static Markup CreateStatusText(string statusText)
    {
        return new Markup($"[{Color.CornflowerBlue}]{statusText}[/]");
    }

    public static Markup CreateSelectedCell(string text, string statusText = "")
        => new("[black on yellow]" + text + statusText + "[/]");

    // Dimmed name for a bypassed (non-matching, active-operation) row. Glyph prefix
    // signals the row is here because something is running on it, not a filter hit.
    public static Markup CreateBypassNameCell(string text)
        => new($"[{Bypass}]{BypassGlyph} {text}[/]");
    
    public static Markup CreateUnselectedCellWithStatus(string text, string statusText)
        => new(text + "[Aquamarine1]" + statusText + "[/]");
        

    public static Markup CreateStateCell(FunctionState state)
        => new($"[bold {UiHelper.GetStateColor(state)}]{state.ToDisplayLabel()}[/]");

    public static Markup CreateFunctionStateCell(bool isDisabled, bool isToggling)
    {
        var label = isDisabled ? "Disabled" : "Enabled";
        if (isToggling)
        {
            return new Markup($"[{Hint}]{label}...[/]");
        }

        return new Markup(isDisabled
            ? $"[{Danger}]{label}[/]"
            : $"[bold green]{label}[/]");
    }

    public static Markup CreateStatusCell(FunctionStatus status)
    {
        return new Markup($"[bold {UiHelper.GetStatusColor(status)}]{status.ToDisplayLabel()}[/]");
    }

    /// <summary>
    /// Danger/warning banner shown in the TopPanel status line when the Azure session is not
    /// healthy. Returns null when Healthy so the caller keeps the normal status text.
    /// </summary>
    public static Markup? CreateSessionBanner(AzureSessionState state)
    {
        var markup = CreateSessionBannerMarkup(state);
        return markup is null ? null : new Markup(markup);
    }

    /// <summary>Raw banner markup string (pure, testable); null when the session is Healthy.</summary>
    public static string? CreateSessionBannerMarkup(AzureSessionState state)
    {
        switch (state.Status)
        {
            case AzureSessionStatus.Expired:
                var note = string.IsNullOrEmpty(state.FailureNote) ? "" : $" ({state.FailureNote})";
                return $"[{Danger}]{WarnGlyph} Azure session expired - press L to re-login{note}[/]";

            case AzureSessionStatus.ReAuthenticating when state.DeviceCode is not null:
                return $"[{Warning}]Azure re-login: open {state.DeviceCodeUrl} and enter code {state.DeviceCode}[/]";

            case AzureSessionStatus.ReAuthenticating:
                return $"[{Warning}]Azure re-login: starting device-code sign-in...[/]";

            default:
                return null;
        }
    }

    public static string? CreateFunctionsEmptyStateText(FunctionAppDetails app, UiStatusSnapshot uiStatus)
    {
        if (app.Status is { Status: StatusType.InProgress, Action: FunctionAction.Start })
        {
            return $"[{Hint}]Function app is starting. Functions will load when startup completes.[/]";
        }

        if (uiStatus.IsInventoryValidating || uiStatus.IsDetailsRefreshing)
        {
            return $"[{Hint}]Functions are loading.[/]";
        }

        return app.State == FunctionState.Stopped
            ? $"[{Hint}]Function app is stopped. Start it to load functions.[/]"
            : null;
    }
}
