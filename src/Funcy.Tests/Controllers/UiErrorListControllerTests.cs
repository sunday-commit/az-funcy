using Funcy.Console.Ui;
using Funcy.Console.Ui.Controllers;
using Funcy.Console.Ui.Navigation;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Console.Ui.State;
using Funcy.Core.Model;
using Spectre.Console;
using Xunit;

namespace Funcy.Tests.Controllers;

public class UiErrorListControllerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Ctor_SeedsViewWithCurrentSnapshot()
    {
        var log = new UiErrorLog();
        log.Report("app-a", "boom");

        var view = new FakeErrorView();
        using var controller = new UiErrorListController(view, log);

        Assert.Single(view.LastSetAll);
        Assert.Equal("boom", view.LastSetAll[0].Message);
    }

    [Fact]
    public async Task NewError_WhileOpen_IsPushedToView()
    {
        var log = new UiErrorLog();
        var view = new FakeErrorView();
        // The ctor also invokes invalidate once; only signal changes that happen afterwards.
        TaskCompletionSource updated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var controller = new UiErrorListController(view, log, invalidate: () => updated.TrySetResult());
        updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        log.Report("app-a", "boom");

        await updated.Task.WaitAsync(Timeout);
        Assert.Contains(view.LastSetAll, e => e.Message == "boom");
    }

    [Fact]
    public async Task Clear_EmptiesView()
    {
        var log = new UiErrorLog();
        log.Report("app-a", "boom");

        var view = new FakeErrorView();
        TaskCompletionSource updated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var controller = new UiErrorListController(view, log, invalidate: () => updated.TrySetResult());
        updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        log.Clear();

        await updated.Task.WaitAsync(Timeout);
        Assert.Empty(view.LastSetAll);
    }

    [Fact]
    public async Task Dispose_StopsReceivingUpdates()
    {
        var log = new UiErrorLog();
        var view = new FakeErrorView();
        var controller = new UiErrorListController(view, log);
        controller.Dispose();

        var callsBefore = view.SetAllCalls;
        log.Report("app-a", "boom");

        // Give any (unwanted) event a chance to arrive.
        await Task.Delay(50);
        Assert.Equal(callsBefore, view.SetAllCalls);
    }

    private sealed class FakeErrorView : IListPanelView<UiErrorEntry>
    {
        public IReadOnlyList<UiErrorEntry> LastSetAll { get; private set; } = [];
        public int SetAllCalls { get; private set; }

        public void SetAll(IReadOnlyList<UiErrorEntry> items)
        {
            LastSetAll = items;
            SetAllCalls++;
        }

        public void Upsert(UiErrorEntry item) => throw new NotSupportedException();
        public void Remove(string key) => throw new NotSupportedException();
        public void SetUiStatus(UiStatusSnapshot uiStatusSnapshot) { }

        public void HandleResize() { }
        public Panel Panel => new("");
        public void HandleInput(ConsoleKeyInfo keyInfo) { }
        public void SetSearchText(string searchInputSearchText) { }
        public bool TryGetNavigationRequest(out NavigationRequest? navigationRequest) { navigationRequest = null; return false; }
        public bool TryGetActionNavigationRequest(out NavigationRequest? navigationRequest) { navigationRequest = null; return false; }
        public Dictionary<TableIndex, ShortcutMap> GetShortcuts() => new();
        public void SortViewBy(int keyInfoKey) { }
        public bool IsActionValid(FunctionAction action) => false;
        public void RenderCurrentView() { }
        public void RenderIfNeeded() { }
        public string GetSelectedItemKey() => "";
        public UiStatusSnapshot GetUiStatusSnapshot() => default;
    }
}
