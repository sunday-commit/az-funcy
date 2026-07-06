namespace Funcy.Console.Settings;

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
}
