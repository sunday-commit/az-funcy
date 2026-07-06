namespace Funcy.Console.Settings;

// Writable, in-memory-authoritative settings. Initialized from the bound IOptions value and
// updated at runtime; every update persists to settings.json. Consumers read Current so a
// committed change applies without restarting the app.
public interface IFuncySettingsService
{
    FuncySettings Current { get; }

    Task UpdateAsync(Action<FuncySettings> mutate);
}
