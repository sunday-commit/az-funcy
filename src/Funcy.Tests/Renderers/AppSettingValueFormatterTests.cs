using Funcy.Console.Ui;
using Funcy.Console.Ui.PanelLayout.Renderers;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Renderers;

public class AppSettingValueFormatterTests
{
    private static AppSettingDetails Plain(string value) =>
        new() { Name = "S", Value = value };

    private static AppSettingDetails KeyVault(SecretResolutionState state, string? resolved = null,
        string? resolutionError = null) =>
        new()
        {
            Name = "S",
            Value = "ref",
            KeyVaultReference = new KeyVaultReference("vault", new Uri("https://vault.vault.azure.net"), "s", null),
            ResolutionState = state,
            ResolvedValue = resolved,
            ResolutionErrorMessage = resolutionError
        };

    [Fact]
    public void Format_MaskedSetting_UsesFixedLengthMask()
    {
        var longValue = Plain(new string('x', 200));
        longValue.Masked = true;

        var shortValue = Plain("ab");
        shortValue.Masked = true;

        var forLong = AppSettingValueFormatter.Format(longValue);
        var forShort = AppSettingValueFormatter.Format(shortValue);

        // Mask must not leak the real value's length.
        Assert.Equal(AppSettingValueFormatter.Mask, forLong.Unselected);
        Assert.Equal(AppSettingValueFormatter.Mask, forShort.Unselected);
        Assert.Equal(forLong.Unselected, forShort.Unselected);
    }

    [Fact]
    public void Format_UnmaskedPlainValue_IsMarkupEscaped()
    {
        var item = Plain("[danger] value");
        item.Masked = false;

        var cells = AppSettingValueFormatter.Format(item);

        Assert.Equal("[[danger]] value", cells.Unselected);
        Assert.Equal(cells.Unselected, cells.Selected);
    }

    [Fact]
    public void Format_UnmaskedResolvedSecret_IsEscaped()
    {
        var item = KeyVault(SecretResolutionState.Resolved, "s3cr3t[x]");
        item.Masked = false;

        var cells = AppSettingValueFormatter.Format(item);

        Assert.Equal("s3cr3t[[x]]", cells.Unselected);
    }

    [Fact]
    public void Format_UnmaskedResolving_ShowsResolvingText()
    {
        var item = KeyVault(SecretResolutionState.Resolving);
        item.Masked = false;

        var cells = AppSettingValueFormatter.Format(item);

        Assert.Contains(AppSettingValueFormatter.ResolvingText, cells.Unselected);
        Assert.Contains(UiStyles.Hint, cells.Unselected);
    }

    [Fact]
    public void Format_UnmaskedFailed_ShowsSpecificError()
    {
        var item = KeyVault(SecretResolutionState.Failed, resolutionError: "Access denied. Required: Key Vault Secrets User.");
        item.Masked = false;

        var cells = AppSettingValueFormatter.Format(item);

        Assert.Contains("Key Vault Secrets User", cells.Unselected);
        Assert.Contains(UiStyles.Danger, cells.Unselected);
    }

    [Fact]
    public void Format_UnmaskedGenericFailure_DoesNotClaimAccessWasDenied()
    {
        var item = KeyVault(SecretResolutionState.Failed, resolutionError: "Could not resolve secret.");
        item.Masked = false;

        var cells = AppSettingValueFormatter.Format(item);

        Assert.Contains("Could not resolve secret", cells.Unselected);
        Assert.DoesNotContain("access denied", cells.Unselected, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_MaskedKeyVaultReference_StillMasked()
    {
        var item = KeyVault(SecretResolutionState.Resolved, "s3cr3t");
        item.Masked = true;

        var cells = AppSettingValueFormatter.Format(item);

        Assert.Equal(AppSettingValueFormatter.Mask, cells.Unselected);
    }
}
