using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Funcy.Core.Interfaces;
using Funcy.Core.KeyVault;
using Funcy.Core.Model;
using Microsoft.Extensions.Logging;

namespace Funcy.Infrastructure.Azure;

public class AppSettingsService(ArmClient client, ILogger<AppSettingsService> logger) : IAppSettingsService
{
    public async Task<IReadOnlyList<AppSettingDetails>> GetApplicationSettingsAsync(string appArmId,
        CancellationToken cancellationToken)
    {
        var resource = client.GetWebSiteResource(ResourceIdentifier.Parse(appArmId));
        var response = await resource.GetApplicationSettingsAsync(cancellationToken);

        var result = new List<AppSettingDetails>(response.Value.Properties.Count);
        foreach (var (name, value) in response.Value.Properties)
        {
            KeyVaultReferenceParser.TryParse(value, out var reference);
            result.Add(new AppSettingDetails { Name = name, Value = value ?? string.Empty, KeyVaultReference = reference });
        }

        // Log the count only — never the setting values.
        logger.LogInformation("Fetched {Count} application settings for {AppArmId}", result.Count, appArmId);
        return result;
    }
}
