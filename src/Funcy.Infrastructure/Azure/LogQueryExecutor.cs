using Azure;
using Azure.Core;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;

namespace Funcy.Infrastructure.Azure;

public sealed class LogQueryExecutor(LogsQueryClient client) : ILogQueryExecutor
{
    public async Task<IReadOnlyList<LogEntryDetails>> QueryAsync(LogQueryRequest request, CancellationToken cancellationToken)
    {
        var resourceId = ResourceIdentifier.Parse(request.AppInsightsResourceId);
        var query = LogQueryBuilder.Build(request.FunctionAppName, request.FunctionName, request.Since, request.MaxRows);

        // Bound the service-side scan: a wide window for the initial load, or just past the last
        // seen timestamp for incremental polls. The KQL also carries the incremental predicate.
        var end = DateTimeOffset.UtcNow.AddMinutes(1);
        var start = request.Since ?? end.AddMinutes(-30);
        var timeRange = new QueryTimeRange(start, end);

        Response<LogsQueryResult> response = await client.QueryResourceAsync(
            resourceId, query, timeRange, new LogsQueryOptions(), cancellationToken);

        var result = response.Value;
        if (result.Status != LogsQueryResultStatus.Success || result.Table is null)
        {
            return [];
        }

        var entries = new List<LogEntryDetails>(result.Table.Rows.Count);
        foreach (var row in result.Table.Rows)
        {
            entries.Add(MapRow(row));
        }

        return entries;
    }

    private static LogEntryDetails MapRow(LogsTableRow row)
    {
        var timestamp = row.GetDateTimeOffset("timestamp") ?? DateTimeOffset.MinValue;
        var itemId = row.GetString("itemId");
        var itemType = ParseItemType(row.GetString("entryType"));
        var message = row.GetString("message") ?? "";
        var operationId = row.GetString("operation_Id");
        var severity = ResolveSeverity(itemType, row.GetInt32("severityLevel"));

        return new LogEntryDetails
        {
            Timestamp = timestamp,
            ItemType = itemType,
            Severity = severity,
            Message = message,
            OperationId = operationId,
            Key = LogEntryDetails.BuildKey(itemId, timestamp, itemType, message),
        };
    }

    private static LogItemType ParseItemType(string? entryType) => entryType switch
    {
        "exception" => LogItemType.Exception,
        "request" => LogItemType.Request,
        _ => LogItemType.Trace,
    };

    private static string? ResolveSeverity(LogItemType itemType, int? severityLevel) => itemType switch
    {
        LogItemType.Exception => "Error",
        LogItemType.Request => "Info",
        _ => severityLevel switch
        {
            0 => "Verbose",
            1 => "Info",
            2 => "Warning",
            3 => "Error",
            4 => "Critical",
            _ => "Info",
        },
    };
}
