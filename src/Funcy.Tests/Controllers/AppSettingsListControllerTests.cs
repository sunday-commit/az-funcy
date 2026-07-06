using Funcy.Console.Ui;
using Funcy.Console.Ui.Controllers;
using Funcy.Console.Ui.Navigation;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;

namespace Funcy.Tests.Controllers;

public class AppSettingsListControllerTests
{
    private static AppSettingDetails Plain(string name, string value) =>
        new() { Name = name, Value = value };

    private static AppSettingDetails KeyVault(string name) =>
        new()
        {
            Name = name,
            Value = "ref",
            KeyVaultReference = new KeyVaultReference("vault", new Uri("https://vault.vault.azure.net"), name, null)
        };

    private static AppSettingsListController Build(FakeAppSettingsView view, IAppSettingsService service,
        IKeyVaultSecretResolver resolver, AppSettingsEmptyState emptyState)
        => new(view, "/arm/id", "app", service, resolver, emptyState, NullLogger.Instance);

    [Fact]
    public void Load_WhenServiceReturnsSettings_PopulatesView()
    {
        var view = new FakeAppSettingsView();
        var service = new FakeAppSettingsService([Plain("A", "1"), Plain("B", "2")]);
        var emptyState = new AppSettingsEmptyState();

        _ = Build(view, service, new FakeSecretResolver("secret"), emptyState);

        Assert.Equal(2, view.LastSetAll.Count);
        Assert.Null(emptyState.Message);
    }

    [Fact]
    public void Load_WhenNoSettings_SetsEmptyMessage()
    {
        var view = new FakeAppSettingsView();
        var emptyState = new AppSettingsEmptyState();

        _ = Build(view, new FakeAppSettingsService([]), new FakeSecretResolver("secret"), emptyState);

        Assert.Empty(view.LastSetAll);
        Assert.NotNull(emptyState.Message);
    }

    [Fact]
    public void Load_WhenServiceThrows_SetsErrorMessageAndNoItems()
    {
        var view = new FakeAppSettingsView();
        var emptyState = new AppSettingsEmptyState();

        _ = Build(view, new ThrowingAppSettingsService(), new FakeSecretResolver("secret"), emptyState);

        Assert.Empty(view.LastSetAll);
        Assert.NotNull(emptyState.Message);
    }

    [Fact]
    public void ToggleMask_OnPlainSetting_TogglesMaskedFlagWithoutResolving()
    {
        var view = new FakeAppSettingsView();
        var resolver = new FakeSecretResolver("secret");
        var setting = Plain("A", "1");
        var controller = Build(view, new FakeAppSettingsService([setting]), resolver, new AppSettingsEmptyState());
        view.SelectedKey = "A";

        controller.ToggleSelectedMask();

        Assert.False(setting.Masked);
        Assert.Equal(0, resolver.CallCount);
        Assert.Contains(setting, view.Upserts);
    }

    [Fact]
    public void ToggleMask_OnKeyVaultReference_ResolvesSecret()
    {
        var view = new FakeAppSettingsView();
        var resolver = new FakeSecretResolver("resolved-value");
        var setting = KeyVault("A");
        var controller = Build(view, new FakeAppSettingsService([setting]), resolver, new AppSettingsEmptyState());
        view.SelectedKey = "A";

        controller.ToggleSelectedMask();

        Assert.False(setting.Masked);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal("resolved-value", setting.ResolvedValue);
        Assert.Equal(SecretResolutionState.Resolved, setting.ResolutionState);
    }

    [Fact]
    public void ToggleMask_RemaskThenReveal_KeepsResolvedValueAndDoesNotResolveAgain()
    {
        var view = new FakeAppSettingsView();
        var resolver = new FakeSecretResolver("resolved-value");
        var setting = KeyVault("A");
        var controller = Build(view, new FakeAppSettingsService([setting]), resolver, new AppSettingsEmptyState());
        view.SelectedKey = "A";

        controller.ToggleSelectedMask(); // reveal -> resolves
        controller.ToggleSelectedMask(); // re-mask
        controller.ToggleSelectedMask(); // reveal again -> instant

        Assert.False(setting.Masked);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal("resolved-value", setting.ResolvedValue);
    }

    [Fact]
    public void ToggleMask_WhenResolutionFails_SetsFailedState()
    {
        var view = new FakeAppSettingsView();
        var setting = KeyVault("A");
        var controller = Build(view, new FakeAppSettingsService([setting]), new ThrowingSecretResolver(),
            new AppSettingsEmptyState());
        view.SelectedKey = "A";

        controller.ToggleSelectedMask();

        Assert.Equal(SecretResolutionState.Failed, setting.ResolutionState);
        Assert.Null(setting.ResolvedValue);
    }

    private sealed class FakeAppSettingsService(IReadOnlyList<AppSettingDetails> settings) : IAppSettingsService
    {
        public Task<IReadOnlyList<AppSettingDetails>> GetApplicationSettingsAsync(string appArmId,
            CancellationToken cancellationToken) => Task.FromResult(settings);
    }

    private sealed class ThrowingAppSettingsService : IAppSettingsService
    {
        public Task<IReadOnlyList<AppSettingDetails>> GetApplicationSettingsAsync(string appArmId,
            CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    private sealed class FakeSecretResolver(string value) : IKeyVaultSecretResolver
    {
        public int CallCount { get; private set; }

        public Task<string> ResolveAsync(KeyVaultReference reference, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(value);
        }
    }

    private sealed class ThrowingSecretResolver : IKeyVaultSecretResolver
    {
        public Task<string> ResolveAsync(KeyVaultReference reference, CancellationToken cancellationToken)
            => throw new UnauthorizedAccessException("denied");
    }

    private sealed class FakeAppSettingsView : IListPanelView<AppSettingDetails>
    {
        public IReadOnlyList<AppSettingDetails> LastSetAll { get; private set; } = [];
        public List<AppSettingDetails> Upserts { get; } = [];
        public string SelectedKey { get; set; } = "";

        public void SetAll(IReadOnlyList<AppSettingDetails> items) => LastSetAll = items;
        public void Upsert(AppSettingDetails item) => Upserts.Add(item);
        public void Remove(string key) { }
        public void SetUiStatus(UiStatusSnapshot uiStatusSnapshot) { }

        public string GetSelectedItemKey() => SelectedKey;

        public void HandleResize() { }
        public Panel Panel { get; } = new(new Text(""));
        public void HandleInput(ConsoleKeyInfo keyInfo) { }
        public void SetSearchText(string searchInputSearchText) { }

        public bool TryGetNavigationRequest(out NavigationRequest? navigationRequest)
        {
            navigationRequest = null;
            return false;
        }

        public bool TryGetActionNavigationRequest(out NavigationRequest? navigationRequest)
        {
            navigationRequest = null;
            return false;
        }

        public Dictionary<TableIndex, ShortcutMap> GetShortcuts() => new();
        public void SortViewBy(int keyInfoKey) { }
        public bool IsActionValid(FunctionAction action) => true;
        public void RenderCurrentView() { }
        public void RenderIfNeeded() { }
        public UiStatusSnapshot GetUiStatusSnapshot() => default;
    }
}
