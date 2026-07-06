using Funcy.Console.Ui.PanelLayout.Renderers;
using Funcy.Core.Model;
using Funcy.Tests.TestSupport;
using Xunit;

namespace Funcy.Tests.PanelLayout;

public class LayoutRendererTests
{
    // ---- FunctionAppLayoutRenderer ----

    private static FunctionAppDetails App()
    {
        var app = new FunctionAppDetails
        {
            Name = "appA",
            State = FunctionState.Running,
            ResourceGroup = "rg",
            Subscription = "sub",
            Id = "id",
            Tags = { { "System", "Billing" } },
            AnimatingFrame = "*"
        };
        app.Status.Status = StatusType.Success;
        return app;
    }

    [Fact]
    public void FunctionApp_ColumnLayout_HasNameTagsStateStatusAndAnimationColumn()
    {
        var renderer = new FunctionAppLayoutRenderer(["System"], _ => 25);
        var layout = renderer.CreateColumnLayout();

        Assert.Equal(["Name", "System", "State", "Status", ""], layout.Columns.Select(c => c.Header));
        Assert.Equal(40, layout.Columns[0].Width);
        Assert.Equal(25, layout.Columns[1].Width); // tag width from getColumnWidth
        Assert.Equal(10, layout.Columns[2].Width);
        Assert.Equal(20, layout.Columns[3].Width);
        Assert.Equal(10, layout.Columns[4].Width);
        Assert.True(layout.Columns[4].AnimationColumn);   // last (animation) column
        Assert.Null(layout.Columns[4].Selector);          // animation column has no selector
    }

    [Fact]
    public void FunctionApp_ColumnLayout_NoTags_HasNameStateStatusAnimation()
    {
        var renderer = new FunctionAppLayoutRenderer([], _ => 25);
        var layout = renderer.CreateColumnLayout();
        Assert.Equal(["Name", "State", "Status", ""], layout.Columns.Select(c => c.Header));
    }

    [Fact]
    public void FunctionApp_RowMarkup_KeyMatchesAppKey()
    {
        var renderer = new FunctionAppLayoutRenderer(["System"], _ => 25);
        Assert.Equal("appA", renderer.CreateRowMarkup(App()).Key);
    }

    [Fact]
    public void FunctionApp_RowMarkup_UnselectedCells_RenderExpectedText()
    {
        var renderer = new FunctionAppLayoutRenderer(["System"], _ => 25);
        var row = renderer.CreateRowMarkup(App());

        Assert.Equal("appA", MarkupText.Plain(row.GetCell("Name", false)));
        Assert.Equal("Billing", MarkupText.Plain(row.GetCell("System", false)));
        Assert.Equal("Running", MarkupText.Plain(row.GetCell("State", false)));
        Assert.Equal("Success", MarkupText.Plain(row.GetCell("Status", false)));
    }

    [Fact]
    public void FunctionApp_RowMarkup_MissingTag_RendersEmpty()
    {
        var app = App();
        app.Tags.Clear();
        var renderer = new FunctionAppLayoutRenderer(["System"], _ => 25);
        var row = renderer.CreateRowMarkup(app);
        Assert.Equal("", MarkupText.Plain(row.GetCell("System", false)));
    }

    [Fact]
    public void FunctionApp_RowMarkup_SelectedNameCell_RendersName()
    {
        var renderer = new FunctionAppLayoutRenderer(["System"], _ => 25);
        var row = renderer.CreateRowMarkup(App());
        Assert.Equal("appA", MarkupText.Plain(row.GetCell("Name", true)));
    }

    [Fact]
    public void FunctionApp_ColumnLayout_ServiceBusOff_HasNoCountColumns()
    {
        var renderer = new FunctionAppLayoutRenderer([], _ => 25, showServiceBusCounts: false);
        var layout = renderer.CreateColumnLayout();

        Assert.DoesNotContain("Msgs", layout.Columns.Select(c => c.Header));
        Assert.DoesNotContain("DLQ", layout.Columns.Select(c => c.Header));
    }

    [Fact]
    public void FunctionApp_ColumnLayout_ServiceBusOn_AddsRightAlignedCountColumns()
    {
        var renderer = new FunctionAppLayoutRenderer([], _ => 25, showServiceBusCounts: true);
        var layout = renderer.CreateColumnLayout();

        Assert.Equal(["Name", "State", "Status", "Msgs", "DLQ", ""], layout.Columns.Select(c => c.Header));
        var msgs = layout.Columns.Single(c => c.Header == "Msgs");
        var dlq = layout.Columns.Single(c => c.Header == "DLQ");
        Assert.Equal(7, msgs.Width);
        Assert.Equal(7, dlq.Width);
        Assert.Equal(Spectre.Console.Justify.Right, msgs.Alignment);
        Assert.Equal(Spectre.Console.Justify.Right, dlq.Alignment);
        // Name column is shrunk by the two count widths to protect the fixed table budget.
        Assert.Equal(40 - 14, layout.Columns[0].Width);
    }

    [Fact]
    public void FunctionApp_RowMarkup_ServiceBusOn_EmptyWhenCountsNotAllLoaded()
    {
        var app = App();
        app.Functions =
        [
            Sb("loaded", 4, 1, ServiceBusCountStatus.Loaded),
            Sb("loading", null, null, ServiceBusCountStatus.Loading)
        ];
        var renderer = new FunctionAppLayoutRenderer([], _ => 25, showServiceBusCounts: true);

        var row = renderer.CreateRowMarkup(app);

        Assert.Equal("", MarkupText.Plain(row.GetCell("Msgs", false)));
        Assert.Equal("", MarkupText.Plain(row.GetCell("DLQ", false)));
    }

    [Fact]
    public void FunctionApp_RowMarkup_ServiceBusOn_ShowsSummedCountsWhenLoaded()
    {
        var app = App();
        app.Functions =
        [
            Sb("one", 4, 1, ServiceBusCountStatus.Loaded),
            Sb("two", 10, 2, ServiceBusCountStatus.Loaded)
        ];
        var renderer = new FunctionAppLayoutRenderer([], _ => 25, showServiceBusCounts: true);

        var row = renderer.CreateRowMarkup(app);

        Assert.Equal("14", MarkupText.Plain(row.GetCell("Msgs", false)));
        Assert.Equal("3", MarkupText.Plain(row.GetCell("DLQ", false)));
    }

    [Fact]
    public void FunctionApp_RowMarkup_ServiceBusOn_NoServiceBusFunctions_RendersEmpty()
    {
        var app = App(); // App() has no functions
        var renderer = new FunctionAppLayoutRenderer([], _ => 25, showServiceBusCounts: true);

        var row = renderer.CreateRowMarkup(app);

        Assert.Equal("", MarkupText.Plain(row.GetCell("Msgs", false)));
        Assert.Equal("", MarkupText.Plain(row.GetCell("DLQ", false)));
    }

    private static FunctionDetails Sb(string name, long? active, long? dlq, ServiceBusCountStatus status) => new()
    {
        FunctionAppName = "appA",
        Name = name,
        Trigger = "ServiceBusTrigger",
        QueueName = "q-" + name,
        ActiveMessages = active,
        DeadLetteredMessages = dlq,
        CountStatus = status
    };

    // ---- FunctionLayoutRenderer ----

    [Fact]
    public void Function_ColumnLayout_IsNameTriggerStateAndServiceBusColumns()
    {
        // feat/function-disable-toggle: functions list gained the Enabled/Disabled State column.
        // feat/servicebus-trigger-insight: functions list gained Listens to / Msgs / DLQ.
        var layout = new FunctionLayoutRenderer().CreateColumnLayout();
        Assert.Equal(["Name", "Trigger", "State", "Listens to", "Msgs", "DLQ"], layout.Columns.Select(c => c.Header));
    }

    [Fact]
    public void Function_RowMarkup_RendersNameAndTrigger()
    {
        var fn = new FunctionDetails { Name = "fn1", FunctionAppName = "appA", Trigger = "HttpTrigger" };
        var row = new FunctionLayoutRenderer().CreateRowMarkup(fn);
        Assert.Equal("appAfn1", row.Key);
        Assert.Equal("fn1", MarkupText.Plain(row.GetCell("Name", false)));
        Assert.Equal("HttpTrigger", MarkupText.Plain(row.GetCell("Trigger", false)));
    }

    // ---- FunctionAppSlotLayoutRenderer ----

    [Fact]
    public void Slot_ColumnLayout_IsNameAndState()
    {
        var layout = new FunctionAppSlotLayoutRenderer().CreateColumnLayout();
        Assert.Equal(["Name", "State"], layout.Columns.Select(c => c.Header));
    }

    [Fact]
    public void Slot_RowMarkup_RendersNameAndState()
    {
        var slot = new FunctionAppSlotDetails { Id = "i", FullName = "appA/staging", Name = "staging", State = FunctionState.Stopped };
        var row = new FunctionAppSlotLayoutRenderer().CreateRowMarkup(slot);
        Assert.Equal("appA/staging", row.Key);
        Assert.Equal("staging", MarkupText.Plain(row.GetCell("Name", false)));
        Assert.Equal("Stopped", MarkupText.Plain(row.GetCell("State", false)));
    }

    // ---- SubscriptionLayoutRenderer ----

    [Fact]
    public void Subscription_ColumnLayout_IsNameOnly()
    {
        var layout = new SubscriptionLayoutRenderer().CreateColumnLayout();
        Assert.Equal(["Name"], layout.Columns.Select(c => c.Header));
    }

    [Fact]
    public void Subscription_RowMarkup_Current_AppendsCurrentTag()
    {
        var sub = new SubscriptionDetails { Name = "Prod", Id = "id", Current = true };
        var row = new SubscriptionLayoutRenderer().CreateRowMarkup(sub);
        Assert.Equal("Prod [current]", MarkupText.Plain(row.GetCell("Name", false)));
    }

    [Fact]
    public void Subscription_RowMarkup_NotCurrent_JustName()
    {
        var sub = new SubscriptionDetails { Name = "Dev", Id = "id", Current = false };
        var row = new SubscriptionLayoutRenderer().CreateRowMarkup(sub);
        Assert.Equal("Dev", MarkupText.Plain(row.GetCell("Name", false)));
    }
}
