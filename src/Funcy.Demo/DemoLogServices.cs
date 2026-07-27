using Funcy.Core.Interfaces;
using Funcy.Core.Model;

namespace Funcy.Demo;

/// <summary>Every demo app resolves to a component id, so the logs panel never renders the
/// "no Application Insights configured" empty state.</summary>
internal sealed class DemoAppInsightsResolver : IAppInsightsResolver
{
    public Task<string?> ResolveResourceIdAsync(string functionAppArmId, CancellationToken cancellationToken)
    {
        // Derive the component from the app's own resource group so the two read as a matched pair.
        var segments = functionAppArmId.Split('/');
        var resourceGroup = Array.IndexOf(segments, "resourceGroups") is var i and >= 0 && i + 1 < segments.Length
            ? segments[i + 1]
            : "rg-demo-prd";
        var appName = segments.Length > 0 ? segments[^1] : "func-demo-prd";

        return Task.FromResult<string?>(
            $"/subscriptions/{DemoDataset.ProductionSubscriptionId}/resourceGroups/{resourceGroup}" +
            $"/providers/Microsoft.Insights/components/appi-{appName}");
    }
}

/// <summary>Serves canned telemetry for the logs panel, spread across the requested lookback so the
/// range shortcuts visibly change the result.</summary>
internal sealed class DemoLogQueryExecutor : ILogQueryExecutor
{
    public async Task<IReadOnlyList<LogEntryDetails>> QueryAsync(LogQueryRequest request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(DemoLatency.LogQuery, cancellationToken);

        var entries = DemoDataset.CreateLogEntries(request.FunctionName, DateTimeOffset.UtcNow, request.Lookback);

        // Honour the incremental poll window the same way the real query does, so repeated
        // refreshes return only what is newer than the last one rather than re-adding everything.
        if (request.Since is { } since)
        {
            entries = entries.Where(e => e.Timestamp > since).ToList();
        }

        entries.Sort();
        return entries.Take(request.MaxRows).ToList();
    }
}
