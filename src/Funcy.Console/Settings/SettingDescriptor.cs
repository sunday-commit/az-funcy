namespace Funcy.Console.Settings;

// How the settings panel lets the user change a setting:
//   Text          - inline free-text edit (numbers, key=value maps)
//   Toggle        - a boolean flipped in place with Enter, no typing
//   TagSelection  - opens a checklist of the available tag keys
public enum SettingKind
{
    Text,
    Toggle,
    TagSelection
}

// Result of parsing/validating raw edit input for a single setting. On success it carries the
// mutation to apply to a FuncySettings instance; on failure it carries a user-facing error.
public sealed record SettingParseResult(bool Success, string? Error, Action<FuncySettings>? Apply)
{
    public static SettingParseResult Ok(Action<FuncySettings> apply) => new(true, null, apply);
    public static SettingParseResult Fail(string error) => new(false, error, null);
}

// Declarative description of one editable setting. Adding a new setting means adding one entry
// to SettingDescriptors.All — the list panel, matcher and edit flow all work off this.
public sealed class SettingDescriptor
{
    public required string Name { get; init; }
    public required string Description { get; init; }

    // Renders the current value for display and as the pre-filled edit text (must round-trip
    // through Parse).
    public required Func<FuncySettings, string> Format { get; init; }

    // Parses+validates raw edit text into a mutation, or an error message.
    public required Func<string, SettingParseResult> Parse { get; init; }

    // Drives how the settings panel activates this row on Enter. Defaults to inline text edit.
    public SettingKind Kind { get; init; } = SettingKind.Text;

    // For Toggle settings: flips the underlying boolean. Ignored for other kinds.
    public Action<FuncySettings>? Toggle { get; init; }

    // For Toggle settings: reads the current boolean, so the panel can render On/Off.
    public Func<FuncySettings, bool>? IsOn { get; init; }
}
