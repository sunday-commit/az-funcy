using Azure;
using Funcy.Console.Ui;
using Funcy.Console.Ui.Controllers;
using Funcy.Console.Ui.Navigation;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Spectre.Console;
using Xunit;

namespace Funcy.Tests.Controllers;

public class FunctionLogsControllerTests
{
    [Fact]
    public async Task Resolve_WhenAccessIsDenied_ShowsRequiredPermission()
    {
        var view = new FakeView();
        using var controller = new FunctionLogsController(view, new UnusedExecutor(),
            new DeniedResolver(), new NoopClipboard(), "/app", "app", "function");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (view.EmptyState?.Contains("Website Contributor", StringComparison.Ordinal) != true)
        {
            await Task.Delay(10, timeout.Token);
        }

        Assert.Contains("Website Contributor", view.EmptyState);
    }

    [Fact]
    public async Task Query_WhenAccessIsDenied_ShowsRequiredPermission()
    {
        var view = new FakeView();
        using var controller = new FunctionLogsController(view, new DeniedExecutor(),
            new SuccessfulResolver(), new NoopClipboard(), "/app", "app", "function");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (view.EmptyState?.Contains("Monitoring Reader", StringComparison.Ordinal) != true)
        {
            await Task.Delay(10, timeout.Token);
        }

        Assert.Contains("Log Analytics Reader", view.EmptyState);
    }

    private sealed class DeniedResolver : IAppInsightsResolver
    {
        public Task<string?> ResolveResourceIdAsync(string functionAppArmId, CancellationToken cancellationToken)
            => throw new RequestFailedException(403, "Forbidden");
    }

    private sealed class SuccessfulResolver : IAppInsightsResolver
    {
        public Task<string?> ResolveResourceIdAsync(string functionAppArmId, CancellationToken cancellationToken)
            => Task.FromResult<string?>("/application-insights");
    }

    private sealed class UnusedExecutor : ILogQueryExecutor
    {
        public Task<IReadOnlyList<LogEntryDetails>> QueryAsync(LogQueryRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class DeniedExecutor : ILogQueryExecutor
    {
        public Task<IReadOnlyList<LogEntryDetails>> QueryAsync(LogQueryRequest request,
            CancellationToken cancellationToken) => throw new RequestFailedException(403, "Forbidden");
    }

    private sealed class NoopClipboard : IClipboardService
    {
        public Task<bool> TryCopyAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<string?> TryReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeView : IListPanelView<LogEntryDetails>
    {
        public string? EmptyState { get; private set; }
        public Panel Panel { get; } = new(new Text(""));
        public void SetAll(IReadOnlyList<LogEntryDetails> items) { }
        public void Upsert(LogEntryDetails item) { }
        public void Remove(string key) { }
        public void SetUiStatus(UiStatusSnapshot uiStatusSnapshot) { }
        public void SetHeader(string header) { }
        public void SetEmptyStateMessage(string? message) => EmptyState = message;
        public string GetSelectedItemKey() => "";
        public void HandleResize() { }
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
        public bool IsActionValid(FunctionAction action) => false;
        public void RenderCurrentView() { }
        public void RenderIfNeeded() { }
        public UiStatusSnapshot GetUiStatusSnapshot() => default;
    }
}
