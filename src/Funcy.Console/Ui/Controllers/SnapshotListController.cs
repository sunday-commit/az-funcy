using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Core.Model;

namespace Funcy.Console.Ui.Controllers;

// C#
public sealed class SnapshotListController<T> : ListPanelControllerBase<T>
    where T : IComparable<T>, IHasKey
{
    public SnapshotListController(IListPanelView<T> view, IEnumerable<T> initial,
        Action? invalidate = null) : base(view)
    {
        View.SetAll(initial.ToList());
        invalidate?.Invoke();
    }
}