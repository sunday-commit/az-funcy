namespace Funcy.Core.Interfaces;

// Resolves the Application Insights component ARM id backing a function app, or null when the
// app has no Application Insights configured / it cannot be resolved. Authorization failures may
// be propagated so the UI can distinguish missing configuration from missing access.
public interface IAppInsightsResolver
{
    Task<string?> ResolveResourceIdAsync(string functionAppArmId, CancellationToken cancellationToken);
}
