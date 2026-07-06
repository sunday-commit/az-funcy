namespace Funcy.Core.Model;

// View-facing lifecycle of a Key Vault reference's secret value. Only meaningful for
// settings where IsKeyVaultReference is true.
public enum SecretResolutionState
{
    Pending,
    Resolving,
    Resolved,
    Failed
}
