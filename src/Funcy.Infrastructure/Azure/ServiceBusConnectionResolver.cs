namespace Funcy.Infrastructure.Azure;

using Funcy.Core.KeyVault;

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
        var value = ResolveConnectionValue(connectionSetting);
        return KeyVaultReferenceParser.TryParse(value, out _) ? null : ExtractNamespace(value);
    }

    // Returns the raw app-setting value the namespace is extracted from — a fully qualified
    // namespace host, a Service Bus connection string, or a @Microsoft.KeyVault(...) reference the
    // caller must resolve to its secret before extracting. Null when no matching setting exists.
    // Kept separate from ResolveNamespace so a Key Vault-backed connection string (ARM returns the
    // reference expression, not the secret) can be resolved before ExtractNamespace runs.
    public string? ResolveConnectionValue(string? connectionSetting)
    {
        var name = string.IsNullOrWhiteSpace(connectionSetting) ? DefaultConnectionSetting : connectionSetting;
        name = ResolveIndirection(name);

        if (TryGet($"{name}__fullyQualifiedNamespace", out var fqns))
        {
            return fqns;
        }

        if (TryGet(name, out var connectionString))
        {
            return connectionString;
        }

        if (TryGet($"AzureWebJobs{name}", out var legacy))
        {
            return legacy;
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
