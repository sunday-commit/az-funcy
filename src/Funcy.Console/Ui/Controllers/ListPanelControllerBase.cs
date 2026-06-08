using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Core.Model;

namespace Funcy.Console.Ui.Controllers;

// C#
public interface IListController : IDisposable { }

public abstract class ListPanelControllerBase<T>(IListPanelView<T> view) : IListController
    where T : IComparable<T>, IHasKey
{
    protected readonly IListPanelView<T> View = view;

    protected void PushStatusToView(UiStatusSnapshot uiStatusSnapshot)
    {
        View.SetUiStatus(uiStatusSnapshot);
    }

    public virtual void Dispose() { /* unhook events etc */ }
}