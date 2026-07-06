using System.Collections.Concurrent;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Microsoft.Extensions.Logging;

namespace Funcy.Infrastructure.Azure;

public class KeyVaultSecretResolver(TokenCredential credential, ILogger<KeyVaultSecretResolver> logger)
    : IKeyVaultSecretResolver
{
    // One SecretClient per vault URI, reused across resolutions. Resolved values are not
    // cached here — freshness beats caching for debugging, and the panel keeps revealed
    // values for its own lifetime.
    private readonly ConcurrentDictionary<string, SecretClient> _clients = new();

    public async Task<string> ResolveAsync(KeyVaultReference reference, CancellationToken cancellationToken)
    {
        var client = _clients.GetOrAdd(reference.VaultUri.AbsoluteUri, _ => new SecretClient(reference.VaultUri, credential));

        var response = await client.GetSecretAsync(reference.SecretName, reference.SecretVersion, cancellationToken);

        logger.LogInformation("Resolved Key Vault secret {SecretName} from {VaultName}", reference.SecretName,
            reference.VaultName);
        return response.Value.Value;
    }
}
