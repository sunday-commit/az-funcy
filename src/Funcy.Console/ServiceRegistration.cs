using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.ResourceManager;
using Funcy.Console.Handlers;
using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Settings;
using Funcy.Console.Ui;
using Funcy.Console.Ui.Factory;
using Funcy.Console.Ui.State;
using Funcy.Core.Interfaces;
using Funcy.Data;
using Funcy.Demo;
using Funcy.Infrastructure.Azure;
using Funcy.Infrastructure.Data;
using Funcy.Infrastructure.Shell;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Funcy.Console;

public static class ServiceRegistration
{
    /// <summary>
    /// Registers everything the app needs. The Azure-backed services and the demo ones are mutually
    /// exclusive: <paramref name="demoMode"/> picks exactly one set, so demo data can never appear in
    /// a normal run and a real Azure call can never happen in a demo run.
    /// </summary>
    public static IServiceCollection AddFuncy(this IServiceCollection services, IConfiguration config,
        bool demoMode)
    {
        services.AddSingleton(demoMode ? new DemoModeInfo(true) : DemoModeInfo.Disabled);
        AddSharedServices(services, config, demoMode);

        if (demoMode)
        {
            services.AddDemoMode();
        }
        else
        {
            AddAzureServices(services);
        }

        return services;
    }

    private static void AddSharedServices(IServiceCollection services, IConfiguration config, bool demoMode)
    {
        services.Configure<FuncySettings>(config.GetSection("Funcy"));
        services.AddSingleton<IFuncySettingsService, FuncySettingsService>();
        services.AddTransient<ITagCatalog, TagCatalog>();
        services.AddMemoryCache();
        services.AddDbContextFactory<FunctionAppDbContext>(options =>
        {
            var connectionString = DatabaseConnectionFactory.CreateConnectionString(config, DataSubDirectory(demoMode));
            options.UseSqlite(connectionString)
                .UseLoggerFactory(LoggerFactory.Create(builder =>
                {
                    builder.AddSerilog().SetMinimumLevel(LogLevel.Information);
                })).EnableSensitiveDataLogging();
        });

        services.AddTransient<InputHandler>();
        services.AddSingleton<FunctionAppUpdateHandler>();
        services.AddTransient<ResizeHandler>();
        services.AddTransient<IActionDispatcher, FunctionActionHandler>();
        services.AddSingleton<AnimationHandler>();
        services.AddSingleton<IAnimationProvider>(sp => sp.GetRequiredService<AnimationHandler>());
        services.AddSingleton<FunctionStateCoordinator>();
        services.AddSingleton<IUiStatusState, UiStatusState>();
        services.AddSingleton<IUiErrorLog, UiErrorLog>();
        services.AddSingleton<AppContext>();
        services.AddTransient<FunctionStatusManager>();
        services.AddTransient<UiStateMarkupProvider>();
        services.AddTransient<AppOrchestrator>();
        services.AddTransient<ListPanelContextFactory>();
        services.AddTransient<ListPanelFactory>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<DatabaseWriteCoordinator>();
        services.AddTransient<SplashScreen>();
        services.AddTransient<SubscriptionProbeHandler>();
    }

    private static void AddAzureServices(IServiceCollection services)
    {
        services.AddSingleton<DefaultAzureCredential>();
        services.AddSingleton(sp =>
        {
            var credential = sp.GetRequiredService<DefaultAzureCredential>();
            return new ArmClient(credential);
        });
        services.AddSingleton(sp => new LogsQueryClient(sp.GetRequiredService<DefaultAzureCredential>()));
        services.AddSingleton<ILogQueryExecutor, LogQueryExecutor>();
        services.AddSingleton<IAppInsightsResourceIdLookup, AppInsightsResourceIdLookup>();
        services.AddSingleton<IAppInsightsResolver, AppInsightsResolver>();
        services.AddTransient<ISubscriptionService, AzureSubscriptionService>();
        services.AddTransient<IAzureFunctionService, AzureFunctionService>();
        services.AddTransient<IFunctionAppManagementService, FunctionAppManagementService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IKeyVaultSecretResolver, KeyVaultSecretResolver>();
        services.AddSingleton<IShellCommandRunner, ShellCommandRunner>();
        services.AddScoped<IAzureResourceService, AzureResourceService>();
        services.AddSingleton<IServiceBusInsightService, ServiceBusInsightService>();
        services.AddSingleton<TokenCredential, DefaultAzureCredential>();
        services.AddTransient<IToolValidationService, ToolValidationService>();
        services.AddSingleton<IAzureCliSession, AzureCliSession>();
        services.AddSingleton<IAzureSessionMonitor, AzureSessionMonitor>();
    }

    /// <summary>Demo mode keeps its database, settings and logs in their own sub-directory so a demo
    /// run never mutates the real inventory cache or the user's settings.</summary>
    public static string? DataSubDirectory(bool demoMode) => demoMode ? DemoMode.DataDirectoryName : null;
}
