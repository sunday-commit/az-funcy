using System.Text.RegularExpressions;

namespace Funcy.Infrastructure.Azure;

/// <summary>
/// Extracts the device-login URL and code from the line az prints (to stderr, before the
/// command completes) during <c>az login --use-device-code</c>, e.g.:
/// "To sign in, use a web browser to open the page https://microsoft.com/devicelogin and
/// enter the code ABCD-EFGH to authenticate."
/// </summary>
public static partial class DeviceCodeParser
{
    // Tolerant of wording drift: we only anchor on the two stable facts — an https URL and a
    // code introduced by "enter the code". The code alphabet covers Azure's letter/digit codes
    // and the hyphenated variant some tenants emit.
    [GeneratedRegex(
        @"(?<url>https?://\S+).*?enter\s+the\s+code\s+(?<code>[A-Z0-9][A-Z0-9\-]{4,})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeviceCodeRegex();

    public static bool TryParse(string? line, out string url, out string code)
    {
        url = string.Empty;
        code = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = DeviceCodeRegex().Match(line);
        if (!match.Success)
        {
            return false;
        }

        // Trim trailing punctuation the sentence may append right after the URL.
        url = match.Groups["url"].Value.TrimEnd('.', ',', ')');
        code = match.Groups["code"].Value;
        return true;
    }
}
