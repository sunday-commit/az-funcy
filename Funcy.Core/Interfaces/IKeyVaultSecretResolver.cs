using Funcy.Core.Model;

namespace Funcy.Core.Interfaces;

public interface IKeyVaultSecretResolver
{
    // Resolves the actual secret value behind a Key Vault reference. Throws on failure
    // (e.g. missing RBAC, firewall) so callers can render an inline error state.
    Task<string> ResolveAsync(KeyVaultReference reference, CancellationToken cancellationToken);
}
