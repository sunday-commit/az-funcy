using Funcy.Core.Model;

namespace Funcy.Core.Interfaces;

// Query Application Insights telemetry for a single function. Kept string-based so the Core
// stays free of Azure SDK types; the infrastructure implementation parses the resource id.
public interface ILogQueryExecutor
{
    Task<IReadOnlyList<LogEntryDetails>> QueryAsync(LogQueryRequest request, CancellationToken cancellationToken);
}

public sealed record LogQueryRequest(
    string AppInsightsResourceId,
    string FunctionAppName,
    string FunctionName,
    DateTimeOffset? Since,
    int MaxRows,
    TimeSpan Lookback);
