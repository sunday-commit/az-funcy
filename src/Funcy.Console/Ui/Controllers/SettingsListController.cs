using Funcy.Console.Settings;
using Funcy.Console.Ui.Panels.Interfaces;

namespace Funcy.Console.Ui.Controllers;

// What the shell must do after the settings panel handles Enter on the selected row.
public enum SettingActivation
{
    // The controller already applied the change (a toggle) — just re-render.
    Handled,
    // Begin an inline text edit (see the out key/rawValue).
    TextEdit,
    // Open the tag-columns checklist.
    OpenTagSelection
}

// Builds settings rows from the current settings and commits changes back through the settings
// service. Runs entirely on the render/input thread. Rows rebuild live off the service's Changed
// event, so a change made in a sub-panel (the tag picker) is reflected when the user returns.
public sealed class SettingsListController : ListPanelControllerBase<SettingItemDetails>, IEditablePanel
{
    private readonly IFuncySettingsService _settingsService;
    private readonly Action _invalidate;

    public SettingsListController(
        IListPanelView<SettingItemDetails> view,
        IFuncySettingsService settingsService,
        Action invalidate) : base(view)
    {
        _settingsService = settingsService;
        _invalidate = invalidate;
        _settingsService.Changed += OnSettingsChanged;
        RebuildRows();
        invalidate();
    }

    // Enter on the selected row: toggle a boolean in place, open the tag picker, or begin a text
    // edit, depending on the setting's kind.
    public SettingActivation ActivateSelected(out string key, out string rawValue)
    {
        key = View.GetSelectedItemKey();
        rawValue = "";

        var descriptor = SettingDescriptors.Find(key);
        if (descriptor is null)
        {
            return SettingActivation.Handled;
        }

        switch (descriptor.Kind)
        {
            case SettingKind.Toggle when descriptor.Toggle is not null:
                // UpdateAsync completes synchronously and raises Changed, which rebuilds the rows.
                _ = _settingsService.UpdateAsync(descriptor.Toggle);
                return SettingActivation.Handled;

            case SettingKind.TagSelection:
                return SettingActivation.OpenTagSelection;

            default:
                rawValue = descriptor.Format(_settingsService.Current);
                return SettingActivation.TextEdit;
        }
    }

    public async Task<string?> CommitEditAsync(string key, string rawValue)
    {
        var descriptor = SettingDescriptors.Find(key);
        if (descriptor is null)
        {
            return "Unknown setting";
        }

        var result = descriptor.Parse(rawValue);
        if (!result.Success)
        {
            return result.Error;
        }

        // Rows rebuild via the Changed event raised inside UpdateAsync.
        await _settingsService.UpdateAsync(result.Apply!);
        return null;
    }

    private void OnSettingsChanged()
    {
        RebuildRows();
        _invalidate();
    }

    private void RebuildRows()
    {
        View.SetAll(SettingDescriptors.BuildRows(_settingsService.Current));
    }

    public override void Dispose()
    {
        _settingsService.Changed -= OnSettingsChanged;
        base.Dispose();
    }
}
