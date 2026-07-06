using System.Globalization;
using System.Text;

namespace Funcy.Infrastructure.Azure;

// Builds a single KQL query unioning the traces/exceptions/requests tables into a common
// projection, scoped to one function. Pure and side-effect free so it can be unit tested.
public static class LogQueryBuilder
{
    public static string Build(string functionAppName, string functionName, DateTimeOffset? since, int maxRows)
    {
        var app = Escape(functionAppName);
        var function = Escape(functionName);
        // operation_Name identifies the function; cloud_RoleName guards against name collisions
        // across apps sharing a single Application Insights resource.
        var scope = $"where operation_Name == '{function}' and cloud_RoleName == '{app}'";

        var sb = new StringBuilder(512);
        sb.Append("union\n");
        sb.Append($"(traces | {scope} | project timestamp, itemId, entryType='trace', severityLevel, message, operation_Id),\n");
        sb.Append($"(exceptions | {scope} | project timestamp, itemId, entryType='exception', severityLevel=int(null), message=strcat(type, ': ', outerMessage), operation_Id),\n");
        sb.Append($"(requests | {scope} | project timestamp, itemId, entryType='request', severityLevel=int(null), message=strcat(name, ' -> ', tostring(resultCode), ' (', tostring(toint(duration)), 'ms)'), operation_Id)");

        if (since is { } sinceValue)
        {
            // Incremental poll: only rows newer than what we already hold.
            var iso = sinceValue.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
            sb.Append($"\n| where timestamp > datetime({iso})");
        }

        sb.Append("\n| order by timestamp desc");
        sb.Append($"\n| take {maxRows}");
        return sb.ToString();
    }

    // KQL single-quoted string escaping. Function/app names are interpolated into the query, so
    // guard against quote injection defensively even though these names are Azure-constrained.
    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("'", "\\'");
}
