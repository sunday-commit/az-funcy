using Funcy.Console;
using Funcy.Core.Interfaces;
using Funcy.Data;
using Funcy.Demo;
using Funcy.Infrastructure.Azure;
using Funcy.Infrastructure.Shell;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Funcy.Tests.Demo;

// Guards the two properties that make a fabricated-data mode safe to ship: a normal run can never
// pick up a demo service, and a demo run can never reach Azure.
public class DemoRegistrationTests
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Data Source=functionapps.db"
        })
        .Build();

    private static readonly Type[] AzureBackedServices =
    [
        typeof(AzureFunctionService),
        typeof(FunctionAppManagementService),
        typeof(AppSettingsService),
        typeof(KeyVaultSecretResolver),
        typeof(AppInsightsResolver),
        typeof(LogQueryExecutor),
        typeof(ServiceBusInsightService),
        typeof(AzureResourceService),
        typeof(AzureSubscriptionService),
        typeof(AzureSessionMonitor),
        typeof(AzureCliSession),
        typeof(ToolValidationService)
    ];

    [Fact]
    public void WithoutDemoMode_NoDemoServiceIsRegistered()
    {
        var services = new ServiceCollection().AddFuncy(Config, demoMode: false);

        var fromDemoAssembly = services
            .Select(d => d.ImplementationType)
            .Where(t => t?.Assembly == typeof(DemoMode).Assembly)
            .ToList();

        Assert.Empty(fromDemoAssembly);
    }

    [Fact]
    public void WithDemoMode_NoAzureBackedServiceIsRegistered()
    {
        var services = new ServiceCollection().AddFuncy(Config, demoMode: true);

        var registered = services.Select(d => d.ImplementationType).ToList();

        foreach (var azureService in AzureBackedServices)
        {
            Assert.DoesNotContain(azureService, registered);
        }
    }

    // Nothing may be left that can authenticate or shell out: no credential, no ARM client, no
    // az CLI runner. A demo path this misses fails loudly as a missing dependency instead.
    [Fact]
    public void WithDemoMode_NoCredentialOrShellRunnerIsRegistered()
    {
        var services = new ServiceCollection().AddFuncy(Config, demoMode: true);

        var serviceTypes = services.Select(d => d.ServiceType).ToHashSet();

        Assert.DoesNotContain(typeof(IShellCommandRunner), serviceTypes);
        Assert.DoesNotContain(typeof(IAzureCliSession), serviceTypes);
        Assert.DoesNotContain(typeof(global::Azure.Core.TokenCredential), serviceTypes);
        Assert.DoesNotContain(typeof(global::Azure.Identity.DefaultAzureCredential), serviceTypes);
        Assert.DoesNotContain(typeof(global::Azure.ResourceManager.ArmClient), serviceTypes);
    }

    [Fact]
    public void WithDemoMode_EveryAzureFacingContractResolvesToADemoService()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddDemoMode()
            .BuildServiceProvider();

        var demoAssembly = typeof(DemoMode).Assembly;

        Assert.Equal(demoAssembly, provider.GetRequiredService<IAzureFunctionService>().GetType().Assembly);
        Assert.Equal(demoAssembly, provider.GetRequiredService<IFunctionAppManagementService>().GetType().Assembly);
        Assert.Equal(demoAssembly, provider.GetRequiredService<IAppSettingsService>().GetType().Assembly);
        Assert.Equal(demoAssembly, provider.GetRequiredService<IKeyVaultSecretResolver>().GetType().Assembly);
        Assert.Equal(demoAssembly, provider.GetRequiredService<IAppInsightsResolver>().GetType().Assembly);
        Assert.Equal(demoAssembly, provider.GetRequiredService<ILogQueryExecutor>().GetType().Assembly);
        Assert.Equal(demoAssembly, provider.GetRequiredService<IServiceBusInsightService>().GetType().Assembly);
        Assert.Equal(demoAssembly, provider.GetRequiredService<IAzureResourceService>().GetType().Assembly);
        Assert.Equal(demoAssembly, provider.GetRequiredService<ISubscriptionService>().GetType().Assembly);
        Assert.Equal(demoAssembly, provider.GetRequiredService<IAzureSessionMonitor>().GetType().Assembly);
        Assert.Equal(demoAssembly, provider.GetRequiredService<IToolValidationService>().GetType().Assembly);
    }

    [Fact]
    public void DemoModeInfo_ReflectsTheModeTheAppWasStartedIn()
    {
        var demo = new ServiceCollection().AddFuncy(Config, demoMode: true)
            .Single(d => d.ServiceType == typeof(DemoModeInfo)).ImplementationInstance;
        var normal = new ServiceCollection().AddFuncy(Config, demoMode: false)
            .Single(d => d.ServiceType == typeof(DemoModeInfo)).ImplementationInstance;

        Assert.Equal(new DemoModeInfo(true), demo);
        Assert.Equal(DemoModeInfo.Disabled, normal);
    }

    // A demo run must not touch the real inventory cache, settings or logs.
    [Fact]
    public void DemoMode_UsesItsOwnDataDirectoryUnderTheRealOne()
    {
        var real = DatabaseConnectionFactory.GetDataDirectory();
        var demo = DatabaseConnectionFactory.GetDataDirectory(ServiceRegistration.DataSubDirectory(true));

        Assert.NotEqual(real, demo);
        Assert.Equal(Path.Combine(real, DemoMode.DataDirectoryName), demo);
        Assert.Null(ServiceRegistration.DataSubDirectory(false));
    }
}
