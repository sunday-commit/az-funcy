namespace Funcy.Core.Interfaces;

// Resolves the Application Insights component ARM id backing a function app, or null when the
// app has no Application Insights configured / it cannot be resolved. Implementations cache
// the resolution per function app for process lifetime.
public interface IAppInsightsResolver
{
    Task<string?> ResolveResourceIdAsync(string functionAppArmId, CancellationToken cancellationToken);
}
