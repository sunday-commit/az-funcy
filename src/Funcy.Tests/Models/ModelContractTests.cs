using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Models;

// Characterization: locks the Key/CompareTo/IsSuccess contracts the panels and coordinator rely on.
public class ModelContractTests
{
    private static FunctionAppDetails App(string name) => new()
    {
        Name = name,
        State = FunctionState.Running,
        ResourceGroup = "rg",
        Subscription = "sub",
        Id = "id"
    };

    [Fact]
    public void FunctionAppDetails_Key_IsName()
        => Assert.Equal("appA", App("appA").Key);

    [Theory]
    [InlineData("a", "b", -1)]
    [InlineData("b", "a", 1)]
    [InlineData("a", "a", 0)]
    public void FunctionAppDetails_CompareTo_UsesOrdinalName(string a, string b, int expectedSign)
        => Assert.Equal(expectedSign, Math.Sign(App(a).CompareTo(App(b))));

    [Fact]
    public void FunctionAppDetails_CompareTo_Null_ReturnsOne()
        => Assert.Equal(1, App("a").CompareTo(null));

    [Fact]
    public void FunctionAppDetails_CompareTo_IsOrdinal_UppercaseSortsBeforeLowercase()
        // Ordinal: 'Z' (0x5A) < 'a' (0x61)
        => Assert.True(App("Zebra").CompareTo(App("apple")) < 0);

    [Fact]
    public void FunctionDetails_Key_IsAppNamePlusName()
        => Assert.Equal("appAfn1", new FunctionDetails { FunctionAppName = "appA", Name = "fn1", Trigger = "t" }.Key);

    [Fact]
    public void FunctionDetails_CompareTo_OrdersByAppThenName()
    {
        var a = new FunctionDetails { FunctionAppName = "appA", Name = "z", Trigger = "t" };
        var b = new FunctionDetails { FunctionAppName = "appB", Name = "a", Trigger = "t" };
        Assert.True(a.CompareTo(b) < 0); // app name wins even though 'z' > 'a'
    }

    [Fact]
    public void FunctionDetails_CompareTo_SameApp_OrdersByName()
    {
        var a = new FunctionDetails { FunctionAppName = "appA", Name = "a", Trigger = "t" };
        var b = new FunctionDetails { FunctionAppName = "appA", Name = "b", Trigger = "t" };
        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void FunctionDetails_CompareTo_Null_ReturnsOne()
        => Assert.Equal(1, new FunctionDetails { FunctionAppName = "a", Name = "b", Trigger = "t" }.CompareTo(null));

    [Fact]
    public void FunctionAppSlotDetails_Key_IsFullName()
    {
        var slot = new FunctionAppSlotDetails { Id = "i", FullName = "appA/staging", Name = "staging", State = FunctionState.Running };
        Assert.Equal("appA/staging", slot.Key);
    }

    [Fact]
    public void FunctionAppSlotDetails_CompareTo_OrdersByName_NotFullName()
    {
        var a = new FunctionAppSlotDetails { Id = "i", FullName = "z/a", Name = "a", State = FunctionState.Running };
        var b = new FunctionAppSlotDetails { Id = "i", FullName = "a/b", Name = "b", State = FunctionState.Running };
        Assert.True(a.CompareTo(b) < 0); // compares Name ("a" vs "b"), ignores FullName
    }

    [Fact]
    public void FunctionAppSlotDetails_CompareTo_Null_ReturnsOne()
    {
        var slot = new FunctionAppSlotDetails { Id = "i", FullName = "f", Name = "n", State = FunctionState.Running };
        Assert.Equal(1, slot.CompareTo(null));
    }

    [Fact]
    public void SubscriptionDetails_Key_IsName()
        => Assert.Equal("sub", new SubscriptionDetails { Name = "sub", Id = "id" }.Key);

    [Fact]
    public void SubscriptionDetails_CompareTo_OrdinalName()
    {
        var a = new SubscriptionDetails { Name = "a", Id = "1" };
        var b = new SubscriptionDetails { Name = "b", Id = "2" };
        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void SubscriptionDetails_CompareTo_Null_ReturnsOne()
        => Assert.Equal(1, new SubscriptionDetails { Name = "a", Id = "1" }.CompareTo(null));

    [Fact]
    public void FunctionAppFetchResult_IsSuccess_WhenDetailsPresent()
    {
        var result = new FunctionAppFetchResult("appA", App("appA"), FunctionAppUpdateKind.Details);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void FunctionAppFetchResult_IsFailure_WhenDetailsNull()
    {
        var result = new FunctionAppFetchResult("appA", null, FunctionAppUpdateKind.Details, "boom");
        Assert.False(result.IsSuccess);
        Assert.Equal("boom", result.ErrorMessage);
    }

    [Fact]
    public void FunctionAppUpdate_CarriesDetailsAndKind()
    {
        var app = App("appA");
        var update = new FunctionAppUpdate(app, FunctionAppUpdateKind.Inventory);
        Assert.Same(app, update.Details);
        Assert.Equal(FunctionAppUpdateKind.Inventory, update.UpdateKind);
    }

    [Fact]
    public void FunctionAppDetails_Defaults_AreEmptyCollectionsAndIdleStatus()
    {
        var app = App("appA");
        Assert.Empty(app.Tags);
        Assert.Empty(app.Slots);
        Assert.Empty(app.Functions);
        Assert.Equal(StatusType.Idle, app.Status.Status);
        Assert.Null(app.Status.Action);
        Assert.Equal("", app.AnimatingFrame);
    }
}
