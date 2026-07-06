using Funcy.Core.Model;

namespace Funcy.Core.Interfaces;

public interface IAppSettingsService
{
    // Fetches a function app's application settings (environment variables) by its ARM id.
    Task<IReadOnlyList<AppSettingDetails>> GetApplicationSettingsAsync(string appArmId, CancellationToken cancellationToken);
}
