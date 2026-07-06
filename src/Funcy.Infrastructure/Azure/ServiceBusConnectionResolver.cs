namespace Funcy.Infrastructure.Azure;

// Resolves a Service Bus namespace name from a function app's application settings, following
// the same lookup rules the Azure Functions runtime uses for a serviceBusTrigger connection.
//
// Given a binding connection setting name (empty defaults to AzureWebJobsServiceBus) the resolver
// looks up, in order:
//   1. <name>__fullyQualifiedNamespace   (identity-based, e.g. "ns.servicebus.windows.net")
//   2. <name>                            (connection string, "Endpoint=sb://ns.servicebus.windows.net/;...")
//   3. AzureWebJobs<name>                (legacy prefixed connection string)
// A setting name wrapped in percent signs (%Other%) is itself an app setting whose value holds
// the real setting name; such indirection is resolved first.
public sealed class ServiceBusConnectionResolver(IReadOnlyDictionary<string, string> settings)
{
    public const string DefaultConnectionSetting = "AzureWebJobsServiceBus";

    public string? ResolveNamespace(string? connectionSetting)
    {
        var name = string.IsNullOrWhiteSpace(connectionSetting) ? DefaultConnectionSetting : connectionSetting;
        name = ResolveIndirection(name);

        if (TryGet($"{name}__fullyQualifiedNamespace", out var fqns))
        {
            return ExtractNamespace(fqns);
        }

        if (TryGet(name, out var connectionString))
        {
            return ExtractNamespace(connectionString);
        }

        if (TryGet($"AzureWebJobs{name}", out var legacy))
        {
            return ExtractNamespace(legacy);
        }

        return null;
    }

    // Resolves a %SettingName% placeholder against app settings; returns non-placeholder values
    // (queue/topic/subscription names) unchanged.
    public string? ResolveValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? value : ResolveIndirection(value);
    }

    // Extracts the leftmost namespace label from either a fully qualified namespace host or a
    // Service Bus connection string.
    public static string? ExtractNamespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string host;
        if (value.Contains("Endpoint=", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = value.Split(';')
                .Select(part => part.Trim())
                .FirstOrDefault(part => part.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase));
            if (endpoint is null)
            {
                return null;
            }

            host = endpoint["Endpoint=".Length..];
        }
        else
        {
            host = value;
        }

        host = host.Trim();

        var scheme = host.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            host = host[(scheme + 3)..];
        }

        host = host.TrimEnd('/');

        var slash = host.IndexOf('/');
        if (slash >= 0)
        {
            host = host[..slash];
        }

        var dot = host.IndexOf('.');
        if (dot > 0)
        {
            return host[..dot];
        }

        return host.Length > 0 ? host : null;
    }

    private string ResolveIndirection(string name)
    {
        var guard = 0;
        while (name.Length >= 2 && name[0] == '%' && name[^1] == '%' && guard++ < 5)
        {
            var key = name[1..^1];
            if (!TryGet(key, out var resolved))
            {
                break;
            }

            name = resolved;
        }

        return name;
    }

    private bool TryGet(string key, out string value)
    {
        foreach (var kvp in settings)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kvp.Value))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
