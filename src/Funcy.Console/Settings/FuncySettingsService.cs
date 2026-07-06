using Funcy.Data;
using Microsoft.Extensions.Options;

namespace Funcy.Console.Settings;

public sealed class FuncySettingsService : IFuncySettingsService
{
    private readonly string _settingsPath;
    private readonly Lock _gate = new();
    private FuncySettings _current;

    public FuncySettingsService(IOptions<FuncySettings> options)
        : this(options.Value, Path.Combine(DatabaseConnectionFactory.GetDataDirectory(), "settings.json"))
    {
    }

    // Test seam: inject the initial value and target file directly.
    public FuncySettingsService(FuncySettings initial, string settingsPath)
    {
        _settingsPath = settingsPath;
        _current = Clone(initial);
    }

    public FuncySettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public Task UpdateAsync(Action<FuncySettings> mutate)
    {
        FuncySettings updated;
        lock (_gate)
        {
            updated = Clone(_current);
        }

        mutate(updated);
        FuncySettingsFile.Write(_settingsPath, updated);

        lock (_gate)
        {
            _current = updated;
        }

        return Task.CompletedTask;
    }

    private static FuncySettings Clone(FuncySettings source) => new()
    {
        TagColumns = [.. source.TagColumns],
        SubscriptionRefreshIntervalMinutes = source.SubscriptionRefreshIntervalMinutes,
        DefaultTagColumnWidth = source.DefaultTagColumnWidth,
        TagColumnWidths = new Dictionary<string, int>(source.TagColumnWidths)
    };
}
