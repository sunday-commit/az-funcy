using Funcy.Console.Handlers;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Concurrency;

public class ServiceBusCountFetchPlannerTests
{
    [Fact]
    public void AppsToFetch_SettingDisabled_ReturnsEmpty()
    {
        var apps = new[] { AppWithSb("sb-app"), AppWithHttp("http-app") };

        var result = ServiceBusCountFetchPlanner.AppsToFetch(showServiceBusInAppList: false, apps);

        Assert.Empty(result);
    }

    [Fact]
    public void AppsToFetch_SettingEnabled_ReturnsOnlyAppsWithServiceBusFunctions()
    {
        var apps = new[] { AppWithSb("sb-app"), AppWithHttp("http-app"), AppWithNoFunctions("empty-app") };

        var result = ServiceBusCountFetchPlanner.AppsToFetch(showServiceBusInAppList: true, apps);

        Assert.Equal(["sb-app"], result.Select(a => a.Name));
    }

    private static FunctionAppDetails AppWithSb(string name) => App(name, new FunctionDetails
    {
        FunctionAppName = name,
        Name = "processor",
        Trigger = "ServiceBusTrigger",
        QueueName = "orders"
    });

    private static FunctionAppDetails AppWithHttp(string name) => App(name, new FunctionDetails
    {
        FunctionAppName = name,
        Name = "api",
        Trigger = "HttpTrigger"
    });

    private static FunctionAppDetails AppWithNoFunctions(string name) => App(name);

    private static FunctionAppDetails App(string name, params FunctionDetails[] functions) => new()
    {
        Name = name,
        State = FunctionState.Running,
        ResourceGroup = "rg",
        Subscription = "sub",
        Id = "id-" + name,
        Functions = [.. functions]
    };
}
