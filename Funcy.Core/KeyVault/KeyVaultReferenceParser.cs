using Funcy.Core.Model;

namespace Funcy.Core.KeyVault;

// Pure parser for the two Key Vault reference formats an app setting value can take:
//   @Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/<name>[/<version>])
//   @Microsoft.KeyVault(VaultName=<vault>;SecretName=<name>[;SecretVersion=<version>])
// Anything that is not a well-formed reference returns false (a plain setting).
public static class KeyVaultReferenceParser
{
    private const string Prefix = "@Microsoft.KeyVault(";

    public static bool TryParse(string? value, out KeyVaultReference? reference)
    {
        reference = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) || !trimmed.EndsWith(')'))
        {
            return false;
        }

        var inner = trimmed[Prefix.Length..^1].Trim();
        if (inner.Length == 0)
        {
            return false;
        }

        const string secretUriKey = "SecretUri=";
        return inner.StartsWith(secretUriKey, StringComparison.OrdinalIgnoreCase)
            ? TryParseSecretUri(inner[secretUriKey.Length..].Trim(), out reference)
            : TryParseVaultName(inner, out reference);
    }

    private static bool TryParseSecretUri(string uriText, out KeyVaultReference? reference)
    {
        reference = null;
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !segments[0].Equals("secrets", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var secretName = segments[1];
        var version = segments.Length >= 3 ? segments[2] : null;
        var vaultName = uri.Host.Split('.')[0];
        if (string.IsNullOrEmpty(secretName) || string.IsNullOrEmpty(vaultName))
        {
            return false;
        }

        reference = new KeyVaultReference(vaultName, new Uri($"{uri.Scheme}://{uri.Host}"), secretName, version);
        return true;
    }

    private static bool TryParseVaultName(string inner, out KeyVaultReference? reference)
    {
        reference = null;
        string? vault = null, secret = null, version = null;

        foreach (var part in inner.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = part[..idx].Trim();
            var val = part[(idx + 1)..].Trim();

            if (key.Equals("VaultName", StringComparison.OrdinalIgnoreCase))
            {
                vault = val;
            }
            else if (key.Equals("SecretName", StringComparison.OrdinalIgnoreCase))
            {
                secret = val;
            }
            else if (key.Equals("SecretVersion", StringComparison.OrdinalIgnoreCase))
            {
                version = val;
            }
        }

        if (string.IsNullOrEmpty(vault) || string.IsNullOrEmpty(secret))
        {
            return false;
        }

        reference = new KeyVaultReference(vault, new Uri($"https://{vault}.vault.azure.net"), secret,
            string.IsNullOrEmpty(version) ? null : version);
        return true;
    }
}
