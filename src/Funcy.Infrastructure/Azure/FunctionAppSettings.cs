namespace Funcy.Infrastructure.Azure;

// Pure helpers for the "disable a single function" mechanism, which works by toggling the
// AzureWebJobs.<functionName>.Disabled app setting. Kept free of the ARM SDK so the mutation
// logic can be unit tested against a plain dictionary.
public static class FunctionAppSettings
{
    public static string DisabledKey(string functionName) => $"AzureWebJobs.{functionName}.Disabled";

    // Mutates the supplied settings in place: sets exactly the target function's Disabled flag
    // and leaves every other entry untouched. The caller must pass the full, freshly fetched
    // dictionary because Azure replaces the whole app-settings collection on update.
    public static void ApplyDisabledSetting(IDictionary<string, string> settings, string functionName, bool disabled)
    {
        settings[DisabledKey(functionName)] = disabled ? "true" : "false";
    }
}
