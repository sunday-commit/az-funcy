using Funcy.Core.Model;
using Funcy.Infrastructure.Azure;
using Xunit;

namespace Funcy.Tests.Azure;

public class ServiceBusInsightServiceTests
{
    private static ServiceBusConnectionResolver Resolver(params (string Key, string Value)[] settings)
        => new(settings.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase));

    private static FunctionDetails Function(string? queue = null, string? topic = null, string? subscription = null)
        => new()
        {
            FunctionAppName = "app",
            Name = "fn",
            Trigger = "serviceBusTrigger",
            QueueName = queue,
            TopicName = topic,
            SubscriptionName = subscription
        };

    [Fact]
    public void ResolveBindingNames_TopicAndSubscriptionPlaceholders_ResolvedFromSettings()
    {
        var resolver = Resolver(("OrderTopic", "orders"), ("OrderSub", "processor"));
        var function = Function(topic: "%OrderTopic%", subscription: "%OrderSub%");

        var (queueName, topicName, subscriptionName) = ServiceBusInsightService.ResolveBindingNames(resolver, function);

        Assert.Null(queueName);
        Assert.Equal("orders", topicName);
        Assert.Equal("processor", subscriptionName);
    }

    [Fact]
    public void ResolveBindingNames_QueuePlaceholder_ResolvedFromSettings()
    {
        var resolver = Resolver(("OrderQueue", "orders-queue"));
        var function = Function(queue: "%OrderQueue%");

        var (queueName, topicName, subscriptionName) = ServiceBusInsightService.ResolveBindingNames(resolver, function);

        Assert.Equal("orders-queue", queueName);
        Assert.Null(topicName);
        Assert.Null(subscriptionName);
    }

    [Fact]
    public void ResolveBindingNames_MissingSetting_LeavesNameRaw()
    {
        var resolver = Resolver(("SomethingElse", "value"));
        var function = Function(queue: "%OrderQueue%");

        var (queueName, _, _) = ServiceBusInsightService.ResolveBindingNames(resolver, function);

        Assert.Equal("%OrderQueue%", queueName);
    }

    [Fact]
    public void SelectCachedNamespaceId_WhenConfigurationIsUnchanged_KeepsFastPath()
    {
        const string namespaceId =
            "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/orders";

        var selected = ServiceBusInsightService.SelectCachedNamespaceId(namespaceId, "orders");

        Assert.Equal(namespaceId, selected);
    }

    [Fact]
    public void SelectCachedNamespaceId_WhenConfigurationChanged_InvalidatesCache()
    {
        const string namespaceId =
            "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/orders-old";

        var selected = ServiceBusInsightService.SelectCachedNamespaceId(namespaceId, "orders-new");

        Assert.Null(selected);
    }

    [Fact]
    public void SelectCachedNamespaceId_WhenConfigurationCannotBeResolved_KeepsFastPath()
    {
        const string namespaceId =
            "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/orders";

        var selected = ServiceBusInsightService.SelectCachedNamespaceId(namespaceId, null);

        Assert.Equal(namespaceId, selected);
    }
}
