using Funcy.Console.Settings;
using Funcy.Console.Ui.Panels.Interfaces;

namespace Funcy.Console.Ui.Controllers;

// Builds settings rows from the current settings and commits inline edits back through the
// settings service. Runs entirely on the render/input thread (SetAll + invalidate), so it
// respects the render-thread-only rule for touching the view.
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
        RebuildRows();
        invalidate();
    }

    public bool TryBeginEdit(out string key, out string currentRawValue)
    {
        key = View.GetSelectedItemKey();
        currentRawValue = "";

        var descriptor = SettingDescriptors.Find(key);
        if (descriptor is null)
        {
            return false;
        }

        currentRawValue = descriptor.Format(_settingsService.Current);
        return true;
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

        await _settingsService.UpdateAsync(result.Apply!);
        RebuildRows();
        _invalidate();
        return null;
    }

    private void RebuildRows()
    {
        View.SetAll(SettingDescriptors.BuildRows(_settingsService.Current));
    }
}
