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
}
