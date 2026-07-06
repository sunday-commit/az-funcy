using Funcy.Console.Ui.Pagination.Matchers;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Matchers;

// Characterization: documents exactly which fields each matcher searches and its edge behavior.
public class MatcherEdgeCaseTests
{
    private static FunctionAppDetails App(
        string name,
        Dictionary<string, string>? tags = null,
        string[]? functions = null) => new()
    {
        Name = name,
        State = FunctionState.Running,
        ResourceGroup = "resource-group-xyz",
        Subscription = "subscription-xyz",
        Id = "id-xyz",
        Tags = tags ?? [],
        Functions = (functions ?? [])
            .Select(f => new FunctionDetails { Name = f, FunctionAppName = name, Trigger = "HttpTrigger" })
            .ToList()
    };

    // ---- FunctionAppMatcher ----

    [Fact]
    public void FunctionApp_DoesNotMatchOnResourceGroupOrSubscription()
    {
        var matcher = new FunctionAppMatcher(["System"]);
        var app = App("appA");
        Assert.False(matcher.TryMatch(app, "resource-group"));
        Assert.False(matcher.TryMatch(app, "subscription"));
    }

    [Fact]
    public void FunctionApp_DoesNotMatchOnTagColumnNotConfigured()
    {
        // "Team" tag exists on the app, but only "System" is a configured tag column.
        var matcher = new FunctionAppMatcher(["System"]);
        var app = App("appA", tags: new Dictionary<string, string> { { "Team", "Payments" } });
        Assert.False(matcher.TryMatch(app, "payments"));
    }

    [Fact]
    public void FunctionApp_MatchesOnConfiguredTagColumn()
    {
        var matcher = new FunctionAppMatcher(["Team"]);
        var app = App("appA", tags: new Dictionary<string, string> { { "Team", "Payments" } });
        Assert.True(matcher.TryMatch(app, "payments"));
    }

    [Fact]
    public void FunctionApp_EmptyTagColumns_StillMatchesOnName()
    {
        var matcher = new FunctionAppMatcher([]);
        Assert.True(matcher.TryMatch(App("PaymentApp"), "payment"));
    }

    [Fact]
    public void FunctionApp_WhitespaceInput_MatchesEverything()
    {
        // " ".Split(' ') => ["",""]; each term Contains("") is true for the name.
        var matcher = new FunctionAppMatcher(["System"]);
        Assert.True(matcher.TryMatch(App("appA"), "   "));
    }

    [Fact]
    public void FunctionApp_MultiTerm_MatchAcrossDifferentFields()
    {
        var matcher = new FunctionAppMatcher(["System"]);
        var app = App("PaymentApp",
            tags: new Dictionary<string, string> { { "System", "Billing" } },
            functions: ["ProcessInvoice"]);
        // one term per field: name + tag + function
        Assert.True(matcher.TryMatch(app, "payment billing invoice"));
    }

    [Fact]
    public void FunctionApp_Unicode_CaseInsensitiveMatch()
    {
        var matcher = new FunctionAppMatcher([]);
        Assert.True(matcher.TryMatch(App("Straße"), "straße"));
    }

    [Fact]
    public void FunctionApp_DoubleSpaceBetweenTerms_EmptyTermMatches()
    {
        // "payment  x" => ["payment","","x"]; empty term always matches, so overall depends on real terms.
        var matcher = new FunctionAppMatcher([]);
        Assert.True(matcher.TryMatch(App("PaymentX"), "payment  x"));
    }

    // ---- FunctionMatcher: only Name ----

    [Fact]
    public void Function_DoesNotMatchOnTrigger()
    {
        var matcher = new FunctionMatcher();
        var fn = new FunctionDetails { Name = "fn1", FunctionAppName = "appA", Trigger = "HttpTrigger" };
        Assert.False(matcher.TryMatch(fn, "http"));
    }

    [Fact]
    public void Function_DoesNotMatchOnFunctionAppName()
    {
        var matcher = new FunctionMatcher();
        var fn = new FunctionDetails { Name = "fn1", FunctionAppName = "PaymentApp", Trigger = "t" };
        Assert.False(matcher.TryMatch(fn, "payment"));
    }

    // ---- SubscriptionMatcher: only Name ----

    [Fact]
    public void Subscription_DoesNotMatchOnId()
    {
        var matcher = new SubscriptionMatcher();
        var sub = new SubscriptionDetails { Name = "Prod", Id = "guid-12345" };
        Assert.False(matcher.TryMatch(sub, "guid"));
    }

    [Fact]
    public void Subscription_EmptyInput_Matches()
        => Assert.True(new SubscriptionMatcher().TryMatch(new SubscriptionDetails { Name = "Prod", Id = "x" }, ""));

    // ---- FunctionAppSlotMatcher: Name or FullName ----

    [Fact]
    public void Slot_MatchesOnFullNameButNotName()
    {
        var matcher = new FunctionAppSlotMatcher();
        var slot = new FunctionAppSlotDetails { Id = "i", Name = "staging", FullName = "PaymentApp/staging", State = FunctionState.Running };
        Assert.True(matcher.TryMatch(slot, "paymentapp"));
    }

    [Fact]
    public void Slot_EmptyInput_Matches()
    {
        var matcher = new FunctionAppSlotMatcher();
        var slot = new FunctionAppSlotDetails { Id = "i", Name = "staging", FullName = "app/staging", State = FunctionState.Running };
        Assert.True(matcher.TryMatch(slot, ""));
    }
}
