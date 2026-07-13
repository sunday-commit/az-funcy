using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Funcy.Core.Interfaces;
using Funcy.Infrastructure.Shell;

namespace Funcy.Infrastructure.Azure;

public interface IAppInsightsResourceIdLookup
{
    Task<string?> ResolveAsync(string functionAppArmId, CancellationToken cancellationToken);
}

public sealed class AppInsightsResolver(IAppInsightsResourceIdLookup lookup) : IAppInsightsResolver
{
    // Resolution is cached per function app for process lifetime. Caching the Task also collapses
    // concurrent first-time resolves into a single ARM + Resource Graph round trip.
    private readonly ConcurrentDictionary<string, Task<string?>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string?> ResolveResourceIdAsync(string functionAppArmId, CancellationToken cancellationToken)
    {
        Task<string?>? task = null;
        try
        {
            task = _cache.GetOrAdd(functionAppArmId, id => lookup.ResolveAsync(id, cancellationToken));
            return await task;
        }
        catch (OperationCanceledException)
        {
            RemoveFailedTask(functionAppArmId, task);
            throw;
        }
        catch
        {
            // Never crash the panel over telemetry resolution. Evict failures so reopening the
            // panel retries after a transient ARM or Azure CLI problem.
            RemoveFailedTask(functionAppArmId, task);
            return null;
        }
    }

    private void RemoveFailedTask(string functionAppArmId, Task<string?>? task)
    {
        if (task is not null)
        {
            _cache.TryRemove(new KeyValuePair<string, Task<string?>>(functionAppArmId, task));
        }
    }
}

public sealed class AppInsightsResourceIdLookup(
    ArmClient armClient,
    IShellCommandRunner commandRunner) : IAppInsightsResourceIdLookup
{
    public async Task<string?> ResolveAsync(string functionAppArmId, CancellationToken cancellationToken)
    {
        var instrumentationKey = await GetInstrumentationKeyAsync(functionAppArmId, cancellationToken);
        if (string.IsNullOrWhiteSpace(instrumentationKey))
        {
            return null;
        }

        return await MapKeyToResourceIdAsync(instrumentationKey, cancellationToken);
    }

    private async Task<string?> GetInstrumentationKeyAsync(string functionAppArmId, CancellationToken cancellationToken)
    {
        var site = armClient.GetWebSiteResource(ResourceIdentifier.Parse(functionAppArmId));
        var settings = await site.GetApplicationSettingsAsync(cancellationToken);
        var properties = settings.Value.Properties;

        if (properties.TryGetValue("APPLICATIONINSIGHTS_CONNECTION_STRING", out var connectionString))
        {
            var key = AppInsightsConnectionString.ParseInstrumentationKey(connectionString);
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key;
            }
        }

        return properties.TryGetValue("APPINSIGHTS_INSTRUMENTATIONKEY", out var legacyKey)
            ? legacyKey
            : null;
    }

    // The Application Insights component can live in any subscription, so the graph query is not
    // scoped to one. Mirrors the az CLI graph pattern used elsewhere in the infrastructure layer.
    private async Task<string?> MapKeyToResourceIdAsync(
        string instrumentationKey,
        CancellationToken cancellationToken)
    {
        var escapedKey = instrumentationKey.Replace("'", "");
        var query =
            "resources | where type =~ 'microsoft.insights/components' " +
            $"| where properties.InstrumentationKey == '{escapedKey}' " +
            "| project id";

        var args = $"graph query --first 1 -q \"{query.Replace("\"", "\\\"")}\" -o json";
        var json = await commandRunner.RunAsync("az", args, cancellationToken);

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
        {
            return null;
        }

        return data[0].TryGetProperty("id", out var id) ? id.GetString() : null;
    }
}
