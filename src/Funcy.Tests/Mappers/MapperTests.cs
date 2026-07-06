using Funcy.Core.Model;
using Funcy.Data.Entities;
using Funcy.Infrastructure.Azure.Models;
using Funcy.Infrastructure.Mappers;
using Xunit;

namespace Funcy.Tests.Mappers;

public class MapperTests
{
    // ---- FunctionAppGraphRow -> FunctionAppDetails ----

    [Fact]
    public void GraphRow_Map_CopiesScalarFields()
    {
        var row = new FunctionAppGraphRow(
            Id: "/subscriptions/s/sites/appA",
            Name: "appA",
            State: "Running",
            Tags: new Dictionary<string, string> { { "System", "Billing" } },
            ResourceGroup: "rg-1",
            SubscriptionId: "sub-1");

        var details = row.Map();

        Assert.Equal("appA", details.Name);
        Assert.Equal("sub-1", details.Subscription);
        Assert.Equal("rg-1", details.ResourceGroup);
        Assert.Equal(FunctionState.Running, details.State);
        Assert.Equal("/subscriptions/s/sites/appA", details.Id);
        Assert.Equal("Billing", details.Tags["System"]);
    }

    [Fact]
    public void GraphRow_Map_ParsesStoppedState()
    {
        var row = new FunctionAppGraphRow("id", "appA", "Stopped", [], "rg", "sub");
        Assert.Equal(FunctionState.Stopped, row.Map().State);
    }

    [Fact]
    public void GraphRow_Map_InvalidState_Throws()
    {
        var row = new FunctionAppGraphRow("id", "appA", "NotAState", [], "rg", "sub");
        Assert.Throws<ArgumentException>(() => row.Map());
    }

    [Fact]
    public void GraphRow_Map_NullTags_BecomesEmptyDictionary()
    {
        var row = new FunctionAppGraphRow("id", "appA", "Running", null!, "rg", "sub");
        var details = row.Map();
        Assert.Empty(details.Tags);
    }

    [Fact]
    public void GraphRow_Map_LeavesFunctionsAndSlotsEmpty()
    {
        var row = new FunctionAppGraphRow("id", "appA", "Running", [], "rg", "sub");
        var details = row.Map();
        Assert.Empty(details.Functions);
        Assert.Empty(details.Slots);
    }

    // ---- FunctionApp entity -> FunctionAppDetails ----

    private static FunctionApp Entity() => new()
    {
        AzureId = "azure-id",
        Name = "appA",
        ResourceGroup = "rg",
        Subscription = "sub",
        State = FunctionState.Running,
        UpdatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        Tags =
        [
            new FunctionAppTag { FunctionAppId = 1, Key = "System", Value = "Billing" },
            new FunctionAppTag { FunctionAppId = 1, Key = "Team", Value = "Payments" }
        ],
        Functions =
        [
            new Function { AzureId = "fid", Name = "fn1", Trigger = "HttpTrigger" }
        ],
        Slots =
        [
            new FunctionAppSlot { AzureId = "sid", FullName = "appA/staging", Name = "staging", State = FunctionState.Stopped }
        ]
    };

    [Fact]
    public void Entity_Map_CopiesScalarFields()
    {
        var d = Entity().Map();
        Assert.Equal("azure-id", d.Id);
        Assert.Equal("appA", d.Name);
        Assert.Equal(FunctionState.Running, d.State);
        Assert.Equal("sub", d.Subscription);
        Assert.Equal("rg", d.ResourceGroup);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), d.LastUpdated);
    }

    [Fact]
    public void Entity_Map_ProjectsTagsIntoDictionary()
    {
        var d = Entity().Map();
        Assert.Equal(2, d.Tags.Count);
        Assert.Equal("Billing", d.Tags["System"]);
        Assert.Equal("Payments", d.Tags["Team"]);
    }

    [Fact]
    public void Entity_Map_ProjectsFunctions()
    {
        var d = Entity().Map();
        var fn = Assert.Single(d.Functions);
        Assert.Equal("fn1", fn.Name);
        Assert.Equal("HttpTrigger", fn.Trigger);
        // Characterization: FunctionDetailsMapper reads Function.FunctionApp?.Name, which is null
        // for a detached child entity, so FunctionAppName maps to empty string here.
        Assert.Equal(string.Empty, fn.FunctionAppName);
    }

    [Fact]
    public void Entity_Map_ProjectsSlots()
    {
        var d = Entity().Map();
        var slot = Assert.Single(d.Slots);
        Assert.Equal("sid", slot.Id);
        Assert.Equal("appA/staging", slot.FullName);
        Assert.Equal("staging", slot.Name);
        Assert.Equal(FunctionState.Stopped, slot.State);
    }

    // ---- Function entity -> FunctionDetails ----

    [Fact]
    public void Function_Map_UsesParentAppName_WhenSet()
    {
        var app = new FunctionApp { AzureId = "a", Name = "appA", ResourceGroup = "rg", Subscription = "sub", State = FunctionState.Running };
        var fn = new Function { AzureId = "f", Name = "fn1", Trigger = "Timer", FunctionApp = app };
        var d = fn.Map();
        Assert.Equal("appA", d.FunctionAppName);
        Assert.Equal("fn1", d.Name);
        Assert.Equal("Timer", d.Trigger);
    }

    [Fact]
    public void Function_Map_NullParent_EmptyAppName()
    {
        var fn = new Function { AzureId = "f", Name = "fn1", Trigger = "Timer", FunctionApp = null };
        Assert.Equal(string.Empty, fn.Map().FunctionAppName);
    }

    // ---- FunctionAppSlot entity -> FunctionAppSlotDetails ----

    [Fact]
    public void Slot_Map_CopiesFields_AndDefaultsIdleStatus()
    {
        var slot = new FunctionAppSlot { AzureId = "sid", FullName = "appA/prod", Name = "prod", State = FunctionState.Running };
        var d = slot.Map();
        Assert.Equal("sid", d.Id);
        Assert.Equal("appA/prod", d.FullName);
        Assert.Equal("prod", d.Name);
        Assert.Equal(FunctionState.Running, d.State);
        Assert.Equal(StatusType.Idle, d.Status.Status);
    }
}
