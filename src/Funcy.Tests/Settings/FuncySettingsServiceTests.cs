using Funcy.Console.Settings;
using Xunit;

namespace Funcy.Tests.Settings;

public class FuncySettingsServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"funcy-svc-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private FuncySettingsService MakeService() =>
        new(new FuncySettings { SubscriptionRefreshIntervalMinutes = 60 }, _path);

    [Fact]
    public async Task UpdateAsync_UpdatesCurrent()
    {
        var service = MakeService();

        await service.UpdateAsync(s => s.SubscriptionRefreshIntervalMinutes = 15);

        Assert.Equal(15, service.Current.SubscriptionRefreshIntervalMinutes);
    }

    [Fact]
    public async Task UpdateAsync_PersistsToFile()
    {
        var service = MakeService();

        await service.UpdateAsync(s => s.TagColumns = ["System"]);

        var reloaded = new FuncySettingsService(new FuncySettings(), _path);
        // The file exists; a fresh read of it should contain the persisted value.
        Assert.True(File.Exists(_path));
        Assert.Contains("System", File.ReadAllText(_path));
        Assert.Empty(reloaded.Current.TagColumns); // fresh service uses its own initial, not the file
    }

    [Fact]
    public async Task UpdateAsync_DoesNotMutateSharedInstanceBeforeCommit()
    {
        var service = MakeService();
        var before = service.Current;

        await service.UpdateAsync(s => s.SubscriptionRefreshIntervalMinutes = 5);

        // Current is swapped for a new instance, the old snapshot is unchanged.
        Assert.Equal(60, before.SubscriptionRefreshIntervalMinutes);
        Assert.NotSame(before, service.Current);
    }

    [Fact]
    public async Task UpdateAsync_FiresColumnsChanged_WhenTagColumnsChange()
    {
        var service = MakeService();
        var fired = 0;
        service.ColumnsChanged += () => fired++;

        await service.UpdateAsync(s => s.TagColumns = ["System"]);

        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task UpdateAsync_FiresColumnsChanged_WhenWidthsChange()
    {
        var service = MakeService();
        var fired = 0;
        service.ColumnsChanged += () => fired++;

        await service.UpdateAsync(s => s.DefaultTagColumnWidth = 40);
        await service.UpdateAsync(s => s.TagColumnWidths = new Dictionary<string, int> { ["System"] = 12 });

        Assert.Equal(2, fired);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotFireColumnsChanged_WhenOnlyIrrelevantSettingChanges()
    {
        var service = MakeService();
        var fired = 0;
        service.ColumnsChanged += () => fired++;

        // Only the refresh interval changes; it does not shape the columns.
        await service.UpdateAsync(s => s.SubscriptionRefreshIntervalMinutes = 5);

        Assert.Equal(0, fired);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotFireColumnsChanged_WhenColumnValuesAreUnchanged()
    {
        var service = new FuncySettingsService(
            new FuncySettings { TagColumns = ["System"], DefaultTagColumnWidth = 20 }, _path);
        var fired = 0;
        service.ColumnsChanged += () => fired++;

        // Re-committing identical column values must not trigger a rebuild.
        await service.UpdateAsync(s =>
        {
            s.TagColumns = ["System"];
            s.DefaultTagColumnWidth = 20;
        });

        Assert.Equal(0, fired);
    }
}
