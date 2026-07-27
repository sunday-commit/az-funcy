using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Funcy.Console;
using Funcy.Console.Handlers;
using Funcy.Console.Ui;
using Funcy.Data;
using Funcy.Demo;
using Funcy.Infrastructure.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using AppContext = Funcy.Console.AppContext;

Console.OutputEncoding = Encoding.UTF8;

// Demo mode must be asked for explicitly; it is never entered as a fallback.
var demoMode = DemoMode.IsRequested(args);
// The flag is ours, not a configuration key, so keep it out of the host's command-line provider.
var hostArgs = args.Where(arg => arg != DemoMode.Flag).ToArray();

var dataDirectory = DatabaseConnectionFactory.GetDataDirectory(ServiceRegistration.DataSubDirectory(demoMode));
Directory.CreateDirectory(dataDirectory);

var settingsPath = Path.Combine(dataDirectory, "settings.json");
if (!File.Exists(settingsPath))
{
    // The demo ships with the Service Bus columns on: they are the point of the walkthrough, and a
    // demo run has its own settings file so this never changes what a real run shows.
    await File.WriteAllTextAsync(settingsPath, demoMode
        ? """
          {
            "Funcy": {
              "TagColumns": [ "System" ],
              "SubscriptionRefreshIntervalMinutes": 60,
              "ShowServiceBusInAppList": true
            }
          }
          """
        : """
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

var host = Host.CreateDefaultBuilder(hostArgs)
    .UseContentRoot(System.AppContext.BaseDirectory)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddSerilog();
    })
    .ConfigureServices((_, services) => services.AddFuncy(config, demoMode))
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
