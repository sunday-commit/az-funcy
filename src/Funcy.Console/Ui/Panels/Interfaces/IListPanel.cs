using Funcy.Console.Handlers.Models;
using Funcy.Console.Ui.Navigation;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Core.Model;
using Spectre.Console;

namespace Funcy.Console.Ui.Panels.Interfaces;

public interface IListPanel
{
    void HandleResize();
    Panel Panel { get; }
    void HandleInput(ConsoleKeyInfo keyInfo);
    void SetSearchText(string searchInputSearchText);
    bool TryGetNavigationRequest(out NavigationRequest? navigationRequest);
    bool TryGetActionNavigationRequest(out NavigationRequest? navigationRequest);
    Dictionary<TableIndex, ShortcutMap> GetShortcuts();
    void SortViewBy(int keyInfoKey);
    bool IsActionValid(FunctionAction action);
    void RenderCurrentView();
    // Renders pending background changes. Must be called on the render thread (the only
    // thread allowed to touch the Spectre table).
    void RenderIfNeeded();
    string GetSelectedItemKey();
    UiStatusSnapshot GetUiStatusSnapshot();
}