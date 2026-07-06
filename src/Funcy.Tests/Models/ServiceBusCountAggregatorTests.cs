using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Models;

public class ServiceBusCountAggregatorTests
{
    [Fact]
    public void Aggregate_NoServiceBusFunctions_ReportsNoServiceBus()
    {
        var app = App(
            Http("a"),
            Http("b"));

        var counts = ServiceBusCountAggregator.Aggregate(app);

        Assert.False(counts.HasServiceBusFunctions);
        Assert.False(counts.AllLoaded);
        Assert.Equal(0, counts.ActiveMessages);
        Assert.Equal(0, counts.DeadLetteredMessages);
    }

    [Fact]
    public void Aggregate_AllLoaded_SumsCountsAcrossServiceBusFunctions()
    {
        var app = App(
            Http("web"),
            Sb("one", active: 3, dlq: 1, ServiceBusCountStatus.Loaded),
            Sb("two", active: 10, dlq: 4, ServiceBusCountStatus.Loaded));

        var counts = ServiceBusCountAggregator.Aggregate(app);

        Assert.True(counts.HasServiceBusFunctions);
        Assert.True(counts.AllLoaded);
        Assert.Equal(13, counts.ActiveMessages);
        Assert.Equal(5, counts.DeadLetteredMessages);
    }

    [Fact]
    public void Aggregate_PartialFetch_IsNotAllLoaded()
    {
        var app = App(
            Sb("loaded", active: 5, dlq: 2, ServiceBusCountStatus.Loaded),
            Sb("loading", active: null, dlq: null, ServiceBusCountStatus.Loading));

        var counts = ServiceBusCountAggregator.Aggregate(app);

        Assert.True(counts.HasServiceBusFunctions);
        Assert.False(counts.AllLoaded);
        // Only the resolved function contributes to the running sum.
        Assert.Equal(5, counts.ActiveMessages);
        Assert.Equal(2, counts.DeadLetteredMessages);
    }

    [Fact]
    public void Aggregate_FailureMixedIn_IsNotAllLoaded()
    {
        var app = App(
            Sb("ok", active: 7, dlq: 0, ServiceBusCountStatus.Loaded),
            Sb("failed", active: null, dlq: null, ServiceBusCountStatus.Failed));

        var counts = ServiceBusCountAggregator.Aggregate(app);

        Assert.True(counts.HasServiceBusFunctions);
        Assert.False(counts.AllLoaded);
        Assert.Equal(7, counts.ActiveMessages);
        Assert.Equal(0, counts.DeadLetteredMessages);
    }

    private static FunctionAppDetails App(params FunctionDetails[] functions) => new()
    {
        Name = "app",
        State = FunctionState.Running,
        ResourceGroup = "rg",
        Subscription = "sub",
        Id = "id",
        Functions = [.. functions]
    };

    private static FunctionDetails Http(string name) => new()
    {
        FunctionAppName = "app",
        Name = name,
        Trigger = "HttpTrigger"
    };

    private static FunctionDetails Sb(string name, long? active, long? dlq, ServiceBusCountStatus status) => new()
    {
        FunctionAppName = "app",
        Name = name,
        Trigger = "ServiceBusTrigger",
        QueueName = "queue-" + name,
        ActiveMessages = active,
        DeadLetteredMessages = dlq,
        CountStatus = status
    };
}
