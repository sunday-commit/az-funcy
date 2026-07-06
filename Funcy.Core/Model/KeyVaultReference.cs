namespace Funcy.Core.Model;

// A parsed @Microsoft.KeyVault(...) reference. Version is optional (null = latest).
public sealed record KeyVaultReference(string VaultName, Uri VaultUri, string SecretName, string? SecretVersion);
