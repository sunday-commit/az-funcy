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

public class TagSelectionControllerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"funcy-tags-{Guid.NewGuid():N}.json");
    private readonly FakeView _view = new();

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private (TagSelectionController controller, FuncySettingsService service) Make(
        string[] initialColumns, params string[] catalog)
    {
        var service = new FuncySettingsService(new FuncySettings { TagColumns = initialColumns }, _path);
        var controller = new TagSelectionController(_view, service, new FakeCatalog(catalog), () => { });
        return (controller, service);
    }

    [Fact]
    public void Ctor_MarksSelectedTagsFromSettings()
    {
        Make(["System"], "System", "Env");

        Assert.True(_view.LastItems.Single(t => t.Name == "System").Selected);
        Assert.False(_view.LastItems.Single(t => t.Name == "Env").Selected);
    }

    [Fact]
    public void Toggle_ChecksTag_AppendsToTagColumns()
    {
        var (controller, service) = Make([], "System", "Env");
        _view.SelectedKey = "Env";

        controller.ToggleSelected();

        Assert.Equal(["Env"], service.Current.TagColumns);
        Assert.True(_view.LastItems.Single(t => t.Name == "Env").Selected);
    }

    [Fact]
    public void Toggle_Twice_RemovesTag()
    {
        var (controller, service) = Make(["System"], "System", "Env");
        _view.SelectedKey = "System";

        controller.ToggleSelected();

        Assert.Empty(service.Current.TagColumns);
        Assert.False(_view.LastItems.Single(t => t.Name == "System").Selected);
    }

    [Fact]
    public void Toggle_PreservesSelectionOrderAsColumnOrder()
    {
        var (controller, service) = Make([], "Env", "System");

        _view.SelectedKey = "Env";
        controller.ToggleSelected();
        _view.SelectedKey = "System";
        controller.ToggleSelected();

        // Column order follows the order tags were checked, not the alphabetical display order.
        Assert.Equal(["Env", "System"], service.Current.TagColumns);
    }

    [Fact]
    public void Ctor_KeepsSelectedTagMissingFromCatalog()
    {
        // A tag saved as a column but not in the (empty) catalog still shows, checked.
        Make(["Legacy"]);

        Assert.True(_view.LastItems.Single(t => t.Name == "Legacy").Selected);
    }

    private sealed class FakeCatalog(IReadOnlyList<string> keys) : ITagCatalog
    {
        public Task<IReadOnlyList<string>> GetDistinctTagKeysAsync() => Task.FromResult(keys);
    }

    private sealed class FakeView : IListPanelView<TagChoice>
    {
        public IReadOnlyList<TagChoice> LastItems { get; private set; } = [];
        public string SelectedKey { get; set; } = "";

        public void SetAll(IReadOnlyList<TagChoice> items) => LastItems = items;
        public void Upsert(TagChoice item) { }
        public void Remove(string key) { }
        public void SetUiStatus(UiStatusSnapshot uiStatusSnapshot) { }

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
