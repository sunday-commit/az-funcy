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
}
