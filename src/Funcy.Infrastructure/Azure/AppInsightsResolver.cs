using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Funcy.Core.Interfaces;
using Funcy.Infrastructure.Shell;

namespace Funcy.Infrastructure.Azure;

public sealed class AppInsightsResolver(ArmClient armClient) : IAppInsightsResolver
{
    // Resolution is cached per function app for process lifetime. Caching the Task also collapses
    // concurrent first-time resolves into a single ARM + Resource Graph round trip.
    private readonly ConcurrentDictionary<string, Task<string?>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Task<string?> ResolveResourceIdAsync(string functionAppArmId, CancellationToken cancellationToken)
        => _cache.GetOrAdd(functionAppArmId, id => ResolveCoreAsync(id, cancellationToken));

    private async Task<string?> ResolveCoreAsync(string functionAppArmId, CancellationToken cancellationToken)
    {
        try
        {
            var instrumentationKey = await GetInstrumentationKeyAsync(functionAppArmId, cancellationToken);
            if (string.IsNullOrWhiteSpace(instrumentationKey))
            {
                return null;
            }

            return await MapKeyToResourceIdAsync(instrumentationKey);
        }
        catch
        {
            // Never crash the panel over telemetry resolution; empty-state handles the null.
            return null;
        }
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
    private static async Task<string?> MapKeyToResourceIdAsync(string instrumentationKey)
    {
        var escapedKey = instrumentationKey.Replace("'", "");
        var query =
            "resources | where type =~ 'microsoft.insights/components' " +
            $"| where properties.InstrumentationKey == '{escapedKey}' " +
            "| project id";

        var args = $"graph query --first 1 -q \"{query.Replace("\"", "\\\"")}\" -o json";
        var json = await ShellCommandRunner.RunAsync("az", args);

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
        {
            return null;
        }

        return data[0].TryGetProperty("id", out var id) ? id.GetString() : null;
    }
}
