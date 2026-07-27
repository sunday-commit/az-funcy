using Funcy.Console.Ui;
using Funcy.Console.Ui.Panels;
using Funcy.Core.Model;
using Funcy.Data;
using Funcy.Demo;
using Funcy.Infrastructure.Azure;
using Funcy.Infrastructure.Data;
using Funcy.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AppContext = Funcy.Console.AppContext;

namespace Funcy.Tests.Demo;

// Fabricated data must be labelled as such on screen, in every frame a recording could capture.
public sealed class DemoMarkingTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"funcy-demo-test-{Guid.NewGuid():N}.db");

    [Fact]
    public void DemoHeaderMarkup_SaysItIsNotARealTenant()
    {
        var markup = UiStyles.CreateDemoHeaderMarkup();

        Assert.Contains("DEMO", markup);
        Assert.Contains("not a real tenant", markup);
    }

    [Fact]
    public async Task TopPanel_InDemoMode_RendersTheDemoMarking()
    {
        var panel = new TopPanel(await CreateAppContextAsync(), isDemoMode: true, windowWidth: () => 160);

        Assert.Contains("DEMO DATA", MarkupText.Plain(panel.Panel));
    }

    [Fact]
    public async Task TopPanel_InNormalMode_HasNoDemoMarking()
    {
        var panel = new TopPanel(await CreateAppContextAsync(), isDemoMode: false, windowWidth: () => 160);

        Assert.DoesNotContain("DEMO", MarkupText.Plain(panel.Panel));
    }

    // The marking sits on the panel border, not in the status line, so it survives a status
    // message taking that line over.
    [Fact]
    public async Task TopPanel_DemoMarkingSurvivesAStatusMessage()
    {
        var panel = new TopPanel(await CreateAppContextAsync(), isDemoMode: true, windowWidth: () => 160);

        panel.SetUiStatusText(UiStyles.CreateStatusText("Validating all function apps"));

        var rendered = MarkupText.Plain(panel.Panel);
        Assert.Contains("DEMO DATA", rendered);
        Assert.Contains("Validating all function apps", rendered);
    }

    private async Task<AppContext> CreateAppContextAsync()
    {
        var factory = new TestDbContextFactory(_dbPath);
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        var appContext = new AppContext(
            new StubSubscriptionService(),
            factory,
            NullLogger<AppContext>.Instance,
            new DatabaseWriteCoordinator());
        await appContext.InitializeAppContext();
        return appContext;
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    private sealed class StubSubscriptionService : ISubscriptionService
    {
        public Task<List<SubscriptionDetails>> GetSubscriptions() =>
            Task.FromResult<List<SubscriptionDetails>>(
                [new SubscriptionDetails { Name = "Integration Platform - Production", Id = "sub-1", Current = true }]);
    }

    private sealed class TestDbContextFactory(string dbPath) : IDbContextFactory<FunctionAppDbContext>
    {
        public FunctionAppDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<FunctionAppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options);
    }
}
