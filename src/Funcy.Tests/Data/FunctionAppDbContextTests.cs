using Funcy.Core.Model;
using Funcy.Data;
using Funcy.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Funcy.Tests.Data;

// Exercises the real EF Core model against a temp-file SQLite database: migrations must apply
// cleanly and the FunctionApp aggregate (tags/functions/slots) must round-trip.
public sealed class FunctionAppDbContextTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"funcy-test-{Guid.NewGuid():N}.db");

    private FunctionAppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<FunctionAppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new FunctionAppDbContext(options);
    }

    [Fact]
    public void Migrations_ApplyCleanly()
    {
        using var db = NewContext();
        db.Database.Migrate();

        // A migrated database reports no pending migrations and a non-empty applied set.
        Assert.Empty(db.Database.GetPendingMigrations());
        Assert.NotEmpty(db.Database.GetAppliedMigrations());
    }

    [Fact]
    public void FunctionApp_Aggregate_RoundTrips()
    {
        using (var db = NewContext())
        {
            db.Database.Migrate();
            db.FunctionApps.Add(new FunctionApp
            {
                AzureId = "azure-1",
                Name = "appA",
                ResourceGroup = "rg",
                Subscription = "sub",
                State = FunctionState.Running,
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Tags = [new FunctionAppTag { Key = "System", Value = "Billing" }],
                Functions = [new Function { AzureId = "f1", Name = "fn1", Trigger = "HttpTrigger" }],
                Slots = [new FunctionAppSlot { AzureId = "s1", FullName = "appA/staging", Name = "staging", State = FunctionState.Stopped }]
            });
            db.SaveChanges();
        }

        using (var db = NewContext())
        {
            var app = db.FunctionApps
                .Include(a => a.Tags)
                .Include(a => a.Functions)
                .Include(a => a.Slots)
                .Single();

            Assert.Equal("appA", app.Name);
            Assert.Equal(FunctionState.Running, app.State);
            Assert.Equal("Billing", Assert.Single(app.Tags).Value);
            Assert.Equal("fn1", Assert.Single(app.Functions).Name);
            Assert.Equal("staging", Assert.Single(app.Slots).Name);
        }
    }

    [Fact]
    public void FunctionAppTag_CompositeKey_PreventsDuplicateKeysPerApp()
    {
        using var db = NewContext();
        db.Database.Migrate();

        var app = new FunctionApp
        {
            AzureId = "azure-2",
            Name = "appB",
            ResourceGroup = "rg",
            Subscription = "sub",
            State = FunctionState.Running,
            Tags =
            [
                new FunctionAppTag { Key = "System", Value = "One" },
                new FunctionAppTag { Key = "System", Value = "Two" }
            ]
        };

        // Composite PK (FunctionAppId, Key) rejects two tags with the same key on one app;
        // the conflict surfaces from the change tracker at Add time.
        Assert.ThrowsAny<Exception>(() =>
        {
            db.FunctionApps.Add(app);
            db.SaveChanges();
        });
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { /* best effort cleanup */ }
    }
}
