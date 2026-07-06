using System.Globalization;

namespace Funcy.Console.Settings;

// Parse/validate helpers for each FuncySettings property. Kept separate from the descriptor
// declarations so the validation rules are unit-testable in isolation.
public static class SettingParsers
{
    public const int MinColumnWidth = 1;
    public const int MaxColumnWidth = 100;

    // Comma-separated tag names. Empty input clears the list; blank entries are rejected.
    public static SettingParseResult ParseTagColumns(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            return SettingParseResult.Ok(s => s.TagColumns = []);
        }

        var parts = trimmed.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Any(string.IsNullOrEmpty))
        {
            return SettingParseResult.Fail("Tag columns must be comma-separated non-empty names");
        }

        return SettingParseResult.Ok(s => s.TagColumns = parts);
    }

    // Whole minutes; 0 means "always refresh" (see FunctionAppUpdateHandler), so non-negative.
    public static SettingParseResult ParseRefreshInterval(string raw)
    {
        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            return SettingParseResult.Fail("Interval must be a non-negative whole number");
        }

        return SettingParseResult.Ok(s => s.SubscriptionRefreshIntervalMinutes = value);
    }

    public static SettingParseResult ParseDefaultWidth(string raw)
    {
        if (!TryParseWidth(raw.Trim(), out var value))
        {
            return SettingParseResult.Fail($"Width must be a whole number between {MinColumnWidth} and {MaxColumnWidth}");
        }

        return SettingParseResult.Ok(s => s.DefaultTagColumnWidth = value);
    }

    // Comma-separated key=value pairs, each value a valid column width. Empty input clears.
    public static SettingParseResult ParseTagColumnWidths(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            return SettingParseResult.Ok(s => s.TagColumnWidths = []);
        }

        var result = new Dictionary<string, int>();
        foreach (var pair in trimmed.Split(',', StringSplitOptions.TrimEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                return SettingParseResult.Fail("Use key=value pairs separated by commas");
            }

            var key = pair[..separator].Trim();
            var valueText = pair[(separator + 1)..].Trim();
            if (key.Length == 0 || !TryParseWidth(valueText, out var width))
            {
                return SettingParseResult.Fail($"Each width must be a whole number between {MinColumnWidth} and {MaxColumnWidth}");
            }

            result[key] = width;
        }

        return SettingParseResult.Ok(s => s.TagColumnWidths = result);
    }

    private static bool TryParseWidth(string text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
               && value is >= MinColumnWidth and <= MaxColumnWidth;
    }
}
