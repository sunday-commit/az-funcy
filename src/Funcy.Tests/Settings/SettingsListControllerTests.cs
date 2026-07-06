using Funcy.Console.Settings;
using Funcy.Console.Ui;
using Funcy.Console.Ui.Controllers;
using Funcy.Console.Ui.Navigation;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Core.Model;
using Spectre.Console;
using Xunit;

namespace Funcy.Tests.Settings;

public class SettingsListControllerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"funcy-ctrl-{Guid.NewGuid():N}.json");
    private readonly FakeView _view = new();

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private (SettingsListController controller, FuncySettingsService service) Make()
    {
        var service = new FuncySettingsService(new FuncySettings { SubscriptionRefreshIntervalMinutes = 60 }, _path);
        var controller = new SettingsListController(_view, service, () => { });
        return (controller, service);
    }

    [Fact]
    public void Ctor_PopulatesOneRowPerDescriptor()
    {
        Make();
        Assert.Equal(SettingDescriptors.All.Count, _view.LastItems.Count);
    }

    [Fact]
    public void TryBeginEdit_ReturnsCurrentRawValue()
    {
        var (controller, _) = Make();
        _view.SelectedKey = "SubscriptionRefreshIntervalMinutes";

        var ok = controller.TryBeginEdit(out var key, out var value);

        Assert.True(ok);
        Assert.Equal("SubscriptionRefreshIntervalMinutes", key);
        Assert.Equal("60", value);
    }

    [Fact]
    public void TryBeginEdit_UnknownKey_ReturnsFalse()
    {
        var (controller, _) = Make();
        _view.SelectedKey = "NotASetting";

        Assert.False(controller.TryBeginEdit(out _, out _));
    }

    [Fact]
    public async Task CommitEditAsync_ValidValue_PersistsAndRebuildsRows()
    {
        var (controller, service) = Make();

        var error = await controller.CommitEditAsync("SubscriptionRefreshIntervalMinutes", "15");

        Assert.Null(error);
        Assert.Equal(15, service.Current.SubscriptionRefreshIntervalMinutes);
        var row = _view.LastItems.Single(i => i.Name == "SubscriptionRefreshIntervalMinutes");
        Assert.Equal("15", row.Value);
    }

    [Fact]
    public async Task CommitEditAsync_InvalidValue_ReturnsErrorAndDoesNotChange()
    {
        var (controller, service) = Make();

        var error = await controller.CommitEditAsync("SubscriptionRefreshIntervalMinutes", "-5");

        Assert.NotNull(error);
        Assert.Equal(60, service.Current.SubscriptionRefreshIntervalMinutes);
    }

    [Fact]
    public async Task CommitEditAsync_UnknownKey_ReturnsError()
    {
        var (controller, _) = Make();
        Assert.NotNull(await controller.CommitEditAsync("Nope", "x"));
    }

    private sealed class FakeView : IListPanelView<SettingItemDetails>
    {
        public IReadOnlyList<SettingItemDetails> LastItems { get; private set; } = [];
        public string SelectedKey { get; set; } = "";

        public void SetAll(IReadOnlyList<SettingItemDetails> items) => LastItems = items;
        public void Upsert(SettingItemDetails item) { }
        public void Remove(string key) { }
        public void SetUiStatus(UiStatusSnapshot uiStatusSnapshot) { }
        public void SetHeader(string header) { }
        public void SetEmptyStateMessage(string? message) { }

        public string GetSelectedItemKey() => SelectedKey;

        public Panel Panel => new("");
        public void HandleResize() { }
        public void HandleInput(ConsoleKeyInfo keyInfo) { }
        public void SetSearchText(string searchInputSearchText) { }
        public bool TryGetNavigationRequest(out NavigationRequest? navigationRequest) { navigationRequest = null; return false; }
        public bool TryGetActionNavigationRequest(out NavigationRequest? navigationRequest) { navigationRequest = null; return false; }
        public Dictionary<TableIndex, ShortcutMap> GetShortcuts() => new();
        public void SortViewBy(int keyInfoKey) { }
        public bool IsActionValid(FunctionAction action) => false;
        public void RenderCurrentView() { }
        public void RenderIfNeeded() { }
        public UiStatusSnapshot GetUiStatusSnapshot() => default;
    }
}
