using Funcy.Core.Interfaces;
using Funcy.Core.Model;

namespace Funcy.Demo;

/// <summary>Application settings for the env-vars panel, including two Key Vault references so the
/// reveal and resolve flow has something to act on.</summary>
internal sealed class DemoAppSettingsService(DemoEstate estate) : IAppSettingsService
{
    public async Task<IReadOnlyList<AppSettingDetails>> GetApplicationSettingsAsync(string appArmId,
        CancellationToken cancellationToken)
    {
        await Task.Delay(DemoLatency.Settings, cancellationToken);

        var app = estate.TryGetApp(appArmId);
        var settings = DemoDataset.CreateSettings(app?.Name ?? "func-demo-prd");
        settings.Sort();
        return settings;
    }
}

/// <summary>Returns a fabricated secret for the demo's Key Vault references.</summary>
internal sealed class DemoKeyVaultSecretResolver : IKeyVaultSecretResolver
{
    public async Task<string> ResolveAsync(KeyVaultReference reference, CancellationToken cancellationToken)
    {
        await Task.Delay(DemoLatency.SecretResolve, cancellationToken);
        return $"demo-secret-value-for-{reference.SecretName}";
    }
}
