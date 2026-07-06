using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Model;

public class AppSettingDetailsTests
{
    private static AppSettingDetails Setting(string name) => new() { Name = name, Value = "v" };

    [Fact]
    public void Key_IsName()
    {
        Assert.Equal("MY_SETTING", Setting("MY_SETTING").Key);
    }

    [Fact]
    public void MaskedByDefault()
    {
        Assert.True(Setting("MY_SETTING").Masked);
        Assert.Equal(SecretResolutionState.Pending, Setting("MY_SETTING").ResolutionState);
    }

    [Fact]
    public void CompareTo_OrdersByNameOrdinal()
    {
        var items = new List<AppSettingDetails> { Setting("Bravo"), Setting("Alpha"), Setting("Charlie") };

        items.Sort();

        Assert.Equal(["Alpha", "Bravo", "Charlie"], items.Select(i => i.Name));
    }

    [Fact]
    public void CompareTo_NullSortsFirst()
    {
        Assert.Equal(1, Setting("Alpha").CompareTo(null));
    }

    [Fact]
    public void IsKeyVaultReference_ReflectsParsedReference()
    {
        var plain = new AppSettingDetails { Name = "A", Value = "v" };
        var kv = new AppSettingDetails
        {
            Name = "B",
            Value = "ref",
            KeyVaultReference = new KeyVaultReference("vault", new Uri("https://vault.vault.azure.net"), "s", null)
        };

        Assert.False(plain.IsKeyVaultReference);
        Assert.True(kv.IsKeyVaultReference);
    }
}
