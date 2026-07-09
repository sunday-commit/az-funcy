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

    public event Action? ColumnsChanged;
    public event Action? Changed;

    public Task UpdateAsync(Action<FuncySettings> mutate)
    {
        FuncySettings previous;
        FuncySettings updated;
        lock (_gate)
        {
            previous = _current;
            updated = Clone(_current);
        }

        mutate(updated);
        FuncySettingsFile.Write(_settingsPath, updated);

        lock (_gate)
        {
            _current = updated;
        }

        if (ColumnsDiffer(previous, updated))
        {
            ColumnsChanged?.Invoke();
        }

        Changed?.Invoke();

        return Task.CompletedTask;
    }

    // True when a column-shaping setting differs, so a listener can rebuild affected panels.
    private static bool ColumnsDiffer(FuncySettings a, FuncySettings b)
    {
        if (!a.TagColumns.SequenceEqual(b.TagColumns))
        {
            return true;
        }

        if (a.DefaultTagColumnWidth != b.DefaultTagColumnWidth)
        {
            return true;
        }

        if (a.ShowServiceBusInAppList != b.ShowServiceBusInAppList)
        {
            return true;
        }

        if (a.TagColumnWidths.Count != b.TagColumnWidths.Count)
        {
            return true;
        }

        foreach (var (key, value) in a.TagColumnWidths)
        {
            if (!b.TagColumnWidths.TryGetValue(key, out var other) || other != value)
            {
                return true;
            }
        }

        return false;
    }

    private static FuncySettings Clone(FuncySettings source) => new()
    {
        TagColumns = [.. source.TagColumns],
        SubscriptionRefreshIntervalMinutes = source.SubscriptionRefreshIntervalMinutes,
        DefaultTagColumnWidth = source.DefaultTagColumnWidth,
        TagColumnWidths = new Dictionary<string, int>(source.TagColumnWidths),
        ShowServiceBusInAppList = source.ShowServiceBusInAppList
    };
}
