using Funcy.Console.Ui.PanelLayout.Renderers;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Renderers;

public class FunctionLayoutRendererTests
{
    private readonly FunctionLayoutRenderer _sut = new();

    private static FunctionDetails MakeFunction(bool isDisabled) =>
        new() { Name = "ProcessPayment", FunctionAppName = "appA", Trigger = "HttpTrigger", IsDisabled = isDisabled };

    [Fact]
    public void ColumnLayout_ExposesTriggerInsightColumns()
    {
        var headers = _sut.CreateColumnLayout().Columns.Select(c => c.Header).ToList();

        // feat/function-disable-toggle contributes State; feat/servicebus-trigger-insight contributes Listens to / Msgs / DLQ.
        Assert.Equal(new[] { "Name", "Trigger", "State", "Listens to", "Msgs", "DLQ" }, headers);
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

    [Fact]
    public void CreateColumnLayout_ExposesStateColumn()
    {
        var layout = _sut.CreateColumnLayout();

        Assert.Contains(layout.Columns, c => c.Header == "State");
    }

    [Fact]
    public void StateColumn_Selector_ReflectsDisabledState()
    {
        var stateColumn = _sut.CreateColumnLayout().Columns.Single(c => c.Header == "State");

        Assert.Equal("Disabled", stateColumn.Selector!(MakeFunction(isDisabled: true)));
        Assert.Equal("Enabled", stateColumn.Selector!(MakeFunction(isDisabled: false)));
    }

    [Fact]
    public void CreateRowMarkup_IncludesStateCell()
    {
        var row = _sut.CreateRowMarkup(MakeFunction(isDisabled: true));

        Assert.True(row.Cells.ContainsKey("State"));
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
