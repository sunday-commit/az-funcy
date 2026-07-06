namespace Funcy.Core.Model;

public enum LogTypeFilter
{
    All,
    Traces,
    Exceptions,
    Requests,
}

public static class LogTypeFilterExtensions
{
    // Cycles All -> Traces -> Exceptions -> Requests -> All.
    public static LogTypeFilter Next(this LogTypeFilter filter) => filter switch
    {
        LogTypeFilter.All => LogTypeFilter.Traces,
        LogTypeFilter.Traces => LogTypeFilter.Exceptions,
        LogTypeFilter.Exceptions => LogTypeFilter.Requests,
        _ => LogTypeFilter.All,
    };

    public static bool Includes(this LogTypeFilter filter, LogItemType itemType) => filter switch
    {
        LogTypeFilter.All => true,
        LogTypeFilter.Traces => itemType == LogItemType.Trace,
        LogTypeFilter.Exceptions => itemType == LogItemType.Exception,
        LogTypeFilter.Requests => itemType == LogItemType.Request,
        _ => true,
    };

    public static string ToDisplayLabel(this LogTypeFilter filter) => filter switch
    {
        LogTypeFilter.All => "All",
        LogTypeFilter.Traces => "Traces",
        LogTypeFilter.Exceptions => "Exceptions",
        LogTypeFilter.Requests => "Requests",
        _ => "All",
    };
}
