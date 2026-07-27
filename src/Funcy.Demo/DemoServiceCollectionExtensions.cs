using Funcy.Core.Interfaces;
using Funcy.Infrastructure.Azure;
using Funcy.Infrastructure.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace Funcy.Demo;

public static class DemoServiceCollectionExtensions
{
    /// <summary>
    /// Registers the canned-data services in place of every Azure-backed one. Nothing that talks to
    /// Azure or shells out to az is registered alongside them, so a path this does not cover fails
    /// as a missing dependency rather than quietly reaching a real tenant.
    /// </summary>
    public static IServiceCollection AddDemoMode(this IServiceCollection services)
    {
        services.AddSingleton<DemoEstate>();

        services.AddSingleton<IAzureFunctionService, DemoFunctionService>();
        services.AddSingleton<IFunctionAppManagementService, DemoFunctionAppManagementService>();
        services.AddSingleton<IAppSettingsService, DemoAppSettingsService>();
        services.AddSingleton<IKeyVaultSecretResolver, DemoKeyVaultSecretResolver>();
        services.AddSingleton<IAppInsightsResolver, DemoAppInsightsResolver>();
        services.AddSingleton<ILogQueryExecutor, DemoLogQueryExecutor>();
        services.AddSingleton<IServiceBusInsightService, DemoServiceBusInsightService>();
        services.AddSingleton<IAzureResourceService, DemoAzureResourceService>();
        services.AddSingleton<ISubscriptionService, DemoSubscriptionService>();
        services.AddSingleton<IAzureSessionMonitor, DemoSessionMonitor>();
        services.AddSingleton<IToolValidationService, DemoToolValidationService>();

        return services;
    }
}
