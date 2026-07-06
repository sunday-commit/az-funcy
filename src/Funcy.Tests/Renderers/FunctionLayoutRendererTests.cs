using Funcy.Console.Ui.PanelLayout.Renderers;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Renderers;

public class FunctionLayoutRendererTests
{
    private readonly FunctionLayoutRenderer _sut = new();

    [Fact]
    public void ColumnLayout_ExposesTriggerInsightColumns()
    {
        var headers = _sut.CreateColumnLayout().Columns.Select(c => c.Header).ToList();

        Assert.Equal(new[] { "Name", "Trigger", "Listens to", "Msgs", "DLQ" }, headers);
    }

    [Fact]
    public void RowMarkup_ContainsInsightCells()
    {
        var function = new FunctionDetails
        {
            FunctionAppName = "app",
            Name = "ProcessOrders",
            Trigger = "ServiceBusTrigger",
            TopicName = "orders",
            SubscriptionName = "sub1",
            ActiveMessages = 3,
            DeadLetteredMessages = 1,
            CountStatus = ServiceBusCountStatus.Loaded
        };

        var markup = _sut.CreateRowMarkup(function);

        Assert.True(markup.Cells.ContainsKey("Listens to"));
        Assert.True(markup.Cells.ContainsKey("Msgs"));
        Assert.True(markup.Cells.ContainsKey("DLQ"));
    }
}

public class FunctionDetailsTests
{
    [Fact]
    public void ListensTo_TopicSubscription_FormatsAsTopicSlashSubscription()
    {
        var function = MakeFunction(topic: "orders", subscription: "sub1");

        Assert.Equal("orders/sub1", function.ListensTo);
        Assert.True(function.IsServiceBusTrigger);
    }

    [Fact]
    public void ListensTo_Queue_ReturnsQueueName()
    {
        var function = MakeFunction(queue: "orders-queue");

        Assert.Equal("orders-queue", function.ListensTo);
        Assert.True(function.IsServiceBusTrigger);
    }

    [Fact]
    public void ListensTo_NonServiceBus_IsEmpty()
    {
        var function = MakeFunction();

        Assert.Equal(string.Empty, function.ListensTo);
        Assert.False(function.IsServiceBusTrigger);
    }

    private static FunctionDetails MakeFunction(string? queue = null, string? topic = null, string? subscription = null)
        => new()
        {
            FunctionAppName = "app",
            Name = "fn",
            Trigger = "Trigger",
            QueueName = queue,
            TopicName = topic,
            SubscriptionName = subscription
        };
}
