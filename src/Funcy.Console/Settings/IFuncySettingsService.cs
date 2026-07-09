namespace Funcy.Console.Settings;

// Writable, in-memory-authoritative settings. Initialized from the bound IOptions value and
// updated at runtime; every update persists to settings.json. Consumers read Current so a
// committed change applies without restarting the app.
public interface IFuncySettingsService
{
    FuncySettings Current { get; }

    // Raised by UpdateAsync when a commit changes any column-shaping setting (TagColumns,
    // DefaultTagColumnWidth, TagColumnWidths, ShowServiceBusInAppList) so open panels can rebuild
    // their columns live.
    event Action? ColumnsChanged;

    // Raised by UpdateAsync after every commit (any setting), so an open settings panel can
    // rebuild its rows live — including when a sub-panel (the tag picker) made the change.
    event Action? Changed;

    Task UpdateAsync(Action<FuncySettings> mutate);
}
