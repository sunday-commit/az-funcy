using System.Text;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.ResourceManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Funcy.Console;
using Funcy.Console.Handlers;
using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Settings;
using Funcy.Console.Ui;
using Funcy.Console.Ui.Factory;
using Funcy.Console.Ui.State;
using Funcy.Core.Interfaces;
using Funcy.Data;
using Funcy.Infrastructure.Azure;
using Funcy.Infrastructure.Data;
using Funcy.Infrastructure.Shell;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using AppContext = Funcy.Console.AppContext;

Console.OutputEncoding = Encoding.UTF8;

var dataDirectory = DatabaseConnectionFactory.GetDataDirectory();
Directory.CreateDirectory(dataDirectory);

var settingsPath = Path.Combine(dataDirectory, "settings.json");
if (!File.Exists(settingsPath))
{
    await File.WriteAllTextAsync(settingsPath,
        """
        {
          "Funcy": {
            "TagColumns": [ "System" ],
            "SubscriptionRefreshIntervalMinutes": 60
          }
        }
        """);
}

var config = new ConfigurationBuilder()
    .SetBasePath(System.AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddJsonFile(settingsPath, optional: true, reloadOnChange: false)
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Serilog:WriteTo:0:Args:path"] = Path.Combine(dataDirectory, "logs", "funcy.log")
    })
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(config)
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseContentRoot(System.AppContext.BaseDirectory)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddSerilog();
    })
    .ConfigureServices((_, services) =>
    {
        services.Configure<FuncySettings>(config.GetSection("Funcy"));
        services.AddSingleton<IFuncySettingsService, FuncySettingsService>();
        services.AddTransient<ITagCatalog, TagCatalog>();
        services.AddMemoryCache();
        services.AddDbContextFactory<FunctionAppDbContext>(options =>
        {
            var connectionString = DatabaseConnectionFactory.CreateConnectionString(config);
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
        services.AddSingleton<AnimationHandler>();
        services.AddSingleton<IAnimationProvider>(sp => sp.GetRequiredService<AnimationHandler>());
        services.AddSingleton<FunctionStateCoordinator>();
        services.AddSingleton<IUiStatusState, UiStatusState>();
        services.AddSingleton<IUiErrorLog, UiErrorLog>();
        services.AddSingleton<AppContext>();
        services.AddTransient<FunctionStatusManager>();
        services.AddTransient<AzureSubscriptionService>();
        services.AddTransient<UiStateMarkupProvider>();
        services.AddTransient<AppOrchestrator>();
        services.AddTransient<ListPanelContextFactory>();
        services.AddTransient<ListPanelFactory>();
        services.AddTransient<IAzureFunctionService, AzureFunctionService>();
        services.AddTransient<IFunctionAppManagementService, FunctionAppManagementService>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IKeyVaultSecretResolver, KeyVaultSecretResolver>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IShellCommandRunner, ShellCommandRunner>();
        services.AddScoped<IAzureResourceService, AzureResourceService>();
        services.AddSingleton<IServiceBusInsightService, ServiceBusInsightService>();
        services.AddSingleton<DatabaseWriteCoordinator>();
        services.AddSingleton<TokenCredential, DefaultAzureCredential>();
        services.AddTransient<ToolValidationService>();
        services.AddTransient<SplashScreen>();
        services.AddTransient<SubscriptionProbeHandler>();
        services.AddSingleton<IAzureCliSession, AzureCliSession>();
        services.AddSingleton<IAzureSessionMonitor, AzureSessionMonitor>();
    })
    .Build();

var splashScreen = host.Services.GetRequiredService<SplashScreen>();

// Start the AnimationHandler for the splash screen spinner
var animationHandler = host.Services.GetRequiredService<AnimationHandler>();
var animationCts = new CancellationTokenSource();
var animationTask = animationHandler.StartAsync(animationCts.Token);

// Start background tasks that run during the splash screen
var dbMigrationTask = host.Services.MigrateDatabaseAsync(CancellationToken.None);
var appContext = host.Services.GetRequiredService<AppContext>();
var appContextInitTask = appContext.InitializeAppContext();

// Start the subscription probe as soon as subscriptions are loaded - awaited in the splash screen
var subscriptionProbeHandler = host.Services.GetRequiredService<SubscriptionProbeHandler>();
var probeTask = appContextInitTask.ContinueWith(
    t => t.IsCompletedSuccessfully
        ? subscriptionProbeHandler.ProbeAllSubscriptionsAsync(CancellationToken.None)
        : Task.CompletedTask,
    TaskScheduler.Default).Unwrap();

// Resolve the functionAppUpdateHandler for the continuation
var functionAppUpdateHandler = host.Services.GetRequiredService<FunctionAppUpdateHandler>();

var canContinue = await splashScreen.ShowAsync(
    [dbMigrationTask, appContextInitTask, probeTask],
    async () => await functionAppUpdateHandler.InitializeAsync());

if (!canContinue)
{
    await animationCts.CancelAsync();
    return;
}

// Monitor the az session in the background: proactive probe + reactive reports, plus in-app re-login.
// Started after the splash so it never delays startup and lives for the whole app lifetime.
var sessionMonitor = host.Services.GetRequiredService<IAzureSessionMonitor>();
sessionMonitor.ReAuthenticatedCallback = () => functionAppUpdateHandler.LoadAllDetailsAsync();
var sessionCts = new CancellationTokenSource();
var sessionMonitorTask = sessionMonitor.RunProbeLoopAsync(sessionCts.Token);

// The AnimationHandler keeps running for the AppOrchestrator
var mainMenuService = host.Services.GetRequiredService<AppOrchestrator>();
await mainMenuService.StartAsync();

// Stop the animation after the main menu is done
await animationCts.CancelAsync();
await animationTask;

// Stop the session monitor and cancel any in-flight re-login cleanly.
await sessionCts.CancelAsync();
(sessionMonitor as IDisposable)?.Dispose();
try
{
    await sessionMonitorTask;
}
catch (OperationCanceledException)
{
    // Expected on shutdown.
}

await host.RunAsync();
