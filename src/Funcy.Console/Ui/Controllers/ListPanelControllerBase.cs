using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Core.Model;

namespace Funcy.Console.Ui.Controllers;

// C#
public interface IListController : IDisposable
{
    // Optional hooks for panels that own live data. Base class supplies no-op defaults so
    // existing controllers (which react to shortcuts elsewhere) are unaffected.
    void Refresh();
    void ToggleTypeFilter();
}

public abstract class ListPanelControllerBase<T>(IListPanelView<T> view) : IListController
    where T : IComparable<T>, IHasKey
{
    protected readonly IListPanelView<T> View = view;

    protected void PushStatusToView(UiStatusSnapshot uiStatusSnapshot)
    {
        View.SetUiStatus(uiStatusSnapshot);
    }

    public virtual void Refresh() { }
    public virtual void ToggleTypeFilter() { }

    public virtual void Dispose() { /* unhook events etc */ }
}