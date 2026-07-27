using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Funcy.Demo;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Funcy.Tests.Demo;

// The dataset is what the recording shows, so these pin the properties the walkthrough depends on:
// every panel has content, and the interesting states (dead letters, stopped app, slot, disabled
// function, empty subscription, failed counts) are all present.
public class DemoDatasetTests
{
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection().AddLogging().AddDemoMode().BuildServiceProvider();

    [Fact]
    public async Task ProductionSubscription_HasAppsAcrossSeveralResourceGroups()
    {
        using var provider = BuildProvider();
        var estate = provider.GetRequiredService<DemoEstate>();
        var current = estate.Subscriptions().Single(s => s.Current);

        var apps = estate.AppsFor(current.Id);

        Assert.True(apps.Count >= 10, "the app list needs enough rows to fill a terminal page");
        Assert.True(apps.Select(a => a.ResourceGroup).Distinct().Count() >= 5);
        Assert.All(apps, app => Assert.True(app.Tags.ContainsKey("System"),
            "the System tag backs the demo's tag column"));
        await Task.CompletedTask;
    }

    [Fact]
    public void Subscriptions_IncludeOneWithNoAppsSoHideEmptyHasAnEffect()
    {
        using var provider = BuildProvider();
        var estate = provider.GetRequiredService<DemoEstate>();

        var withoutApps = estate.Subscriptions().Where(s => !estate.HasApps(s.Id)).ToList();

        Assert.NotEmpty(withoutApps);
    }

    [Fact]
    public void Apps_CoverTheStatesTheWalkthroughShows()
    {
        using var provider = BuildProvider();
        var estate = provider.GetRequiredService<DemoEstate>();
        var apps = estate.AppsFor(DemoSubscriptionId(estate));

        Assert.Contains(apps, a => a.State == FunctionState.Stopped);
        Assert.Contains(apps, a => a.State == FunctionState.Running);
        Assert.Contains(apps, a => a.IsPinned);
        Assert.Contains(apps, a => a.Slots.Count > 0);
        Assert.Contains(apps.SelectMany(a => a.Functions), f => f.IsDisabled);
        Assert.Contains(apps.SelectMany(a => a.Functions), f => f.IsServiceBusTrigger);
        Assert.Contains(apps.SelectMany(a => a.Functions), f => f.Trigger == "TimerTrigger");
        Assert.Contains(apps.SelectMany(a => a.Functions), f => f.Trigger == "HttpTrigger");
    }

    // Counts must arrive through the count service, exactly as they do against Azure: the models
    // start out with nothing resolved.
    [Fact]
    public void ServiceBusFunctions_StartWithNoCountsResolved()
    {
        using var provider = BuildProvider();
        var estate = provider.GetRequiredService<DemoEstate>();

        var serviceBusFunctions = estate.AppsFor(DemoSubscriptionId(estate))
            .SelectMany(a => a.Functions)
            .Where(f => f.IsServiceBusTrigger)
            .ToList();

        Assert.NotEmpty(serviceBusFunctions);
        Assert.All(serviceBusFunctions, f =>
        {
            Assert.Null(f.ActiveMessages);
            Assert.Null(f.DeadLetteredMessages);
            Assert.Equal(ServiceBusCountStatus.None, f.CountStatus);
        });
    }

    [Fact]
    public async Task CountService_ResolvesDeadLettersForAtLeastOneApp()
    {
        using var provider = BuildProvider();
        var estate = provider.GetRequiredService<DemoEstate>();
        var counts = provider.GetRequiredService<IServiceBusInsightService>();

        var deadLettered = 0L;
        foreach (var app in estate.AppsFor(DemoSubscriptionId(estate)))
        {
            var serviceBusFunctions = app.Functions.Where(f => f.IsServiceBusTrigger).ToList();
            if (serviceBusFunctions.Count == 0)
            {
                continue;
            }

            var results = await counts.GetCountsAsync(app.Id, serviceBusFunctions, CancellationToken.None);
            deadLettered += results.Where(r => r.Success).Sum(r => r.DeadLetteredMessages ?? 0);
        }

        Assert.True(deadLettered > 0, "dead letters are the reason the Service Bus columns exist");
    }

    // The unresolvable app keeps the failed-count state on screen; it is a real state users hit.
    [Fact]
    public async Task CountService_ReportsFailureForTheAppWithoutAccess()
    {
        using var provider = BuildProvider();
        var estate = provider.GetRequiredService<DemoEstate>();
        var counts = provider.GetRequiredService<IServiceBusInsightService>();

        var failures = new List<ServiceBusCountResult>();
        foreach (var app in estate.AppsFor(DemoSubscriptionId(estate)))
        {
            var serviceBusFunctions = app.Functions.Where(f => f.IsServiceBusTrigger).ToList();
            if (serviceBusFunctions.Count == 0)
            {
                continue;
            }

            var results = await counts.GetCountsAsync(app.Id, serviceBusFunctions, CancellationToken.None);
            failures.AddRange(results.Where(r => !r.Success));
        }

        Assert.NotEmpty(failures);
        Assert.All(failures, f => Assert.Null(f.ActiveMessages));
    }

    [Fact]
    public async Task AppSettings_IncludeKeyVaultReferencesThatResolve()
    {
        using var provider = BuildProvider();
        var estate = provider.GetRequiredService<DemoEstate>();
        var settingsService = provider.GetRequiredService<IAppSettingsService>();
        var resolver = provider.GetRequiredService<IKeyVaultSecretResolver>();
        var app = estate.AppsFor(DemoSubscriptionId(estate)).First();

        var settings =
            await settingsService.GetApplicationSettingsAsync(app.Id, CancellationToken.None);

        var keyVaultSettings = settings.Where(s => s.IsKeyVaultReference).ToList();
        Assert.NotEmpty(keyVaultSettings);

        var resolved = await resolver.ResolveAsync(keyVaultSettings[0].KeyVaultReference!,
            CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(resolved));
    }

    [Fact]
    public async Task Logs_ReturnRowsWithinTheRequestedLookbackAndCoverEveryItemType()
    {
        using var provider = BuildProvider();
        var executor = provider.GetRequiredService<ILogQueryExecutor>();
        var lookback = TimeSpan.FromHours(1);
        var before = DateTimeOffset.UtcNow;

        var entries = await executor.QueryAsync(
            new LogQueryRequest("/subscriptions/x/providers/Microsoft.Insights/components/appi-demo",
                "func-orders-receiver-prd", "ReceiveOrder", Since: null, MaxRows: 200, lookback),
            CancellationToken.None);

        Assert.NotEmpty(entries);
        Assert.All(entries, e =>
        {
            Assert.True(e.Timestamp <= DateTimeOffset.UtcNow);
            Assert.True(e.Timestamp >= before - lookback);
        });
        Assert.Contains(entries, e => e.ItemType == LogItemType.Exception);
        Assert.Contains(entries, e => e.ItemType == LogItemType.Trace);
        Assert.Contains(entries, e => e.ItemType == LogItemType.Request);
    }

    [Fact]
    public async Task Logs_IncrementalPollOnlyReturnsNewerRows()
    {
        using var provider = BuildProvider();
        var executor = provider.GetRequiredService<ILogQueryExecutor>();
        var request = new LogQueryRequest("/subscriptions/x", "func-orders-receiver-prd", "ReceiveOrder",
            Since: DateTimeOffset.UtcNow, MaxRows: 200, TimeSpan.FromHours(1));

        var entries = await executor.QueryAsync(request, CancellationToken.None);

        Assert.All(entries, e => Assert.True(e.Timestamp > request.Since));
    }

    [Fact]
    public async Task StartAndStop_ChangeTheEstateTheListReadsFrom()
    {
        using var provider = BuildProvider();
        var estate = provider.GetRequiredService<DemoEstate>();
        var management = provider.GetRequiredService<IFunctionAppManagementService>();
        var stopped = estate.AppsFor(DemoSubscriptionId(estate)).First(a => a.State == FunctionState.Stopped);

        await management.StartFunction(stopped);
        Assert.Equal(FunctionState.Running, estate.TryGetApp(stopped.Id)!.State);

        await management.StopFunction(stopped);
        Assert.Equal(FunctionState.Stopped, estate.TryGetApp(stopped.Id)!.State);
    }

    [Fact]
    public async Task InventoryIsEmptyUntilTheFirstSync_ThenServesTheEstate()
    {
        using var provider = BuildProvider();
        var estate = provider.GetRequiredService<DemoEstate>();
        var functionService = provider.GetRequiredService<IAzureFunctionService>();
        var subscriptionId = DemoSubscriptionId(estate);

        Assert.Empty(await functionService.GetFunctionsFromDatabase(subscriptionId));

        await foreach (var _ in functionService.GetFunctionAppDetailsAsync(subscriptionId,
                           CancellationToken.None))
        {
        }

        Assert.NotEmpty(await functionService.GetFunctionsFromDatabase(subscriptionId));
    }

    private static string DemoSubscriptionId(DemoEstate estate) => estate.CurrentSubscriptionId;
}
