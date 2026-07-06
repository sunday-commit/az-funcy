using System.Text;
using Azure.Core;
using Azure.Identity;
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
        services.AddSingleton<AnimationHandler>();
        services.AddSingleton<IAnimationProvider>(sp => sp.GetRequiredService<AnimationHandler>());
        services.AddSingleton<FunctionStateCoordinator>();
        services.AddSingleton<IUiStatusState, UiStatusState>();
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
        services.AddScoped<IAzureResourceService, AzureResourceService>();
        services.AddSingleton<IServiceBusInsightService, ServiceBusInsightService>();
        services.AddSingleton<TokenCredential, DefaultAzureCredential>();
        services.AddTransient<ToolValidationService>();
        services.AddTransient<SplashScreen>();
        services.AddTransient<SubscriptionProbeHandler>();
        services.AddSingleton<IAzureCliSession, AzureCliSession>();
        services.AddSingleton<IAzureSessionMonitor, AzureSessionMonitor>();
    })
    .Build();

var splashScreen = host.Services.GetRequiredService<SplashScreen>();

// Starta AnimationHandler för splash screen spinner
var animationHandler = host.Services.GetRequiredService<AnimationHandler>();
var animationCts = new CancellationTokenSource();
var animationTask = animationHandler.StartAsync(animationCts.Token);

// Starta bakgrundsuppgifter som körs under splash screen
var dbMigrationTask = host.Services.MigrateDatabaseAsync(CancellationToken.None);
var appContext = host.Services.GetRequiredService<AppContext>();
var appContextInitTask = appContext.InitializeAppContext();

// Starta subscription probe direkt när subscriptions laddats - väntar i splash screen
var subscriptionProbeHandler = host.Services.GetRequiredService<SubscriptionProbeHandler>();
var probeTask = appContextInitTask.ContinueWith(
    t => t.IsCompletedSuccessfully
        ? subscriptionProbeHandler.ProbeAllSubscriptionsAsync(CancellationToken.None)
        : Task.CompletedTask,
    TaskScheduler.Default).Unwrap();

// Hämta functionAppUpdateHandler för continuation
var functionAppUpdateHandler = host.Services.GetRequiredService<FunctionAppUpdateHandler>();

var canContinue = await splashScreen.ShowAsync(
    [dbMigrationTask, appContextInitTask, probeTask],
    async () => await functionAppUpdateHandler.InitializeAsync());

if (!canContinue)
{
    await animationCts.CancelAsync();
    return;
}

// Övervaka az-sessionen i bakgrunden: proaktiv probe + reaktiva rapporter, samt in-app re-login.
// Startas efter splash så den aldrig fördröjer uppstarten och lever hela appens livstid.
var sessionMonitor = host.Services.GetRequiredService<IAzureSessionMonitor>();
sessionMonitor.ReAuthenticatedCallback = () => functionAppUpdateHandler.LoadAllDetailsAsync();
var sessionCts = new CancellationTokenSource();
var sessionMonitorTask = sessionMonitor.RunProbeLoopAsync(sessionCts.Token);

// AnimationHandler fortsätter köra för AppOrchestrator
var mainMenuService = host.Services.GetRequiredService<AppOrchestrator>();
await mainMenuService.StartAsync();

// Stoppa animation efter att huvudmenyn är klar
await animationCts.CancelAsync();
await animationTask;

// Stoppa sessionsövervakningen och avbryt ev. pågående re-login rent.
await sessionCts.CancelAsync();
(sessionMonitor as IDisposable)?.Dispose();
try
{
    await sessionMonitorTask;
}
catch (OperationCanceledException)
{
    // Förväntat vid nedstängning.
}

await host.RunAsync();
