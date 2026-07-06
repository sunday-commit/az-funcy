using System.Globalization;

namespace Funcy.Core.Model;

// A single Application Insights telemetry row projected to a common shape. Default order is
// newest first so the freshest entries stay at the top of the log panel.
public sealed class LogEntryDetails : IComparable<LogEntryDetails>, IHasKey
{
    public required DateTimeOffset Timestamp { get; init; }
    public required LogItemType ItemType { get; init; }
    public string? Severity { get; init; }
    public required string Message { get; init; }
    public string? OperationId { get; init; }
    public required string Key { get; init; }

    public int CompareTo(LogEntryDetails? other)
    {
        if (other is null)
        {
            return 1;
        }

        // Newest first.
        var byTime = other.Timestamp.CompareTo(Timestamp);
        return byTime != 0 ? byTime : StringComparer.Ordinal.Compare(Key, other.Key);
    }

    // AI rows carry a unique itemId; when it is missing we fall back to a stable composite so
    // dedupe across overlapping polls still works.
    public static string BuildKey(string? itemId, DateTimeOffset timestamp, LogItemType itemType, string message)
    {
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            return itemId;
        }

        var ticks = timestamp.UtcTicks.ToString("x", CultureInfo.InvariantCulture);
        var hash = message.GetHashCode(StringComparison.Ordinal).ToString("x", CultureInfo.InvariantCulture);
        return $"{ticks}:{(int)itemType}:{hash}";
    }
}
