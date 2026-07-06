using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.PanelLayout.Renderers;

// The markup for the two variants of a value cell: Unselected (styled, coloured) and
// Selected (plain text later wrapped in the row highlight). Kept as strings so the logic
// is pure and directly testable.
public sealed record AppSettingValueCells(string Unselected, string Selected);

public static class AppSettingValueFormatter
{
    // Fixed length so a masked value never leaks the real value's length.
    public const string Mask = "••••••";
    public const string ResolvingText = "resolving…";
    public const string AccessDeniedText = "⚠ access denied";

    private const int MaxValueLength = 52;

    public static AppSettingValueCells Format(AppSettingDetails item)
    {
        if (item.Masked)
        {
            return new AppSettingValueCells(Mask, Mask);
        }

        if (item.IsKeyVaultReference)
        {
            return item.ResolutionState switch
            {
                SecretResolutionState.Resolved => EscapedValue(item.ResolvedValue ?? string.Empty),
                SecretResolutionState.Failed => new AppSettingValueCells(
                    $"[{UiStyles.Danger}]{AccessDeniedText}[/]", AccessDeniedText),
                _ => new AppSettingValueCells($"[{UiStyles.Hint}]{ResolvingText}[/]", ResolvingText)
            };
        }

        return EscapedValue(item.Value);
    }

    private static AppSettingValueCells EscapedValue(string value)
    {
        var escaped = Markup.Escape(Truncate(value));
        return new AppSettingValueCells(escaped, escaped);
    }

    private static string Truncate(string value)
        => value.Length <= MaxValueLength ? value : value[..MaxValueLength] + "…";
}
