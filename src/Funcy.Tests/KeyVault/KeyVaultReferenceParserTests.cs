using Funcy.Core.KeyVault;
using Xunit;

namespace Funcy.Tests.KeyVault;

public class KeyVaultReferenceParserTests
{
    [Fact]
    public void TryParse_SecretUriWithVersion_ParsesAllParts()
    {
        var ok = KeyVaultReferenceParser.TryParse(
            "@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/db-password/abc123)", out var r);

        Assert.True(ok);
        Assert.NotNull(r);
        Assert.Equal("myvault", r!.VaultName);
        Assert.Equal("https://myvault.vault.azure.net/", r.VaultUri.AbsoluteUri);
        Assert.Equal("db-password", r.SecretName);
        Assert.Equal("abc123", r.SecretVersion);
    }

    [Fact]
    public void TryParse_SecretUriWithoutVersion_HasNullVersion()
    {
        var ok = KeyVaultReferenceParser.TryParse(
            "@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/db-password)", out var r);

        Assert.True(ok);
        Assert.Equal("db-password", r!.SecretName);
        Assert.Null(r.SecretVersion);
    }

    [Fact]
    public void TryParse_VaultNameFormatWithVersion_ParsesAllParts()
    {
        var ok = KeyVaultReferenceParser.TryParse(
            "@Microsoft.KeyVault(VaultName=myvault;SecretName=db-password;SecretVersion=abc123)", out var r);

        Assert.True(ok);
        Assert.Equal("myvault", r!.VaultName);
        Assert.Equal("https://myvault.vault.azure.net/", r.VaultUri.AbsoluteUri);
        Assert.Equal("db-password", r.SecretName);
        Assert.Equal("abc123", r.SecretVersion);
    }

    [Fact]
    public void TryParse_VaultNameFormatWithoutVersion_HasNullVersion()
    {
        var ok = KeyVaultReferenceParser.TryParse(
            "@Microsoft.KeyVault(VaultName=myvault;SecretName=db-password)", out var r);

        Assert.True(ok);
        Assert.Equal("db-password", r!.SecretName);
        Assert.Null(r.SecretVersion);
    }

    [Theory]
    [InlineData("PlainConnectionString")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("@Microsoft.KeyVault()")]
    [InlineData("@Microsoft.KeyVault(SecretUri=not-a-uri)")]
    [InlineData("@Microsoft.KeyVault(VaultName=myvault)")]
    [InlineData("@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/keys/foo/v1)")]
    public void TryParse_Malformed_ReturnsFalse(string? value)
    {
        var ok = KeyVaultReferenceParser.TryParse(value, out var r);

        Assert.False(ok);
        Assert.Null(r);
    }

    [Fact]
    public void TryParse_IsCaseInsensitiveOnKeys()
    {
        var ok = KeyVaultReferenceParser.TryParse(
            "@Microsoft.KeyVault(vaultname=myvault;secretname=db-password)", out var r);

        Assert.True(ok);
        Assert.Equal("myvault", r!.VaultName);
        Assert.Equal("db-password", r.SecretName);
    }
}
