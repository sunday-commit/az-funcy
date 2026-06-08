using Funcy.Core.Model;

namespace Funcy.Console.Ui.Panels.Interfaces;

public interface IListPanelView<T> : IListPanel where T : IComparable<T>, IHasKey
{
    // Replace the whole model (initial load / full refresh).
    void SetAll(IReadOnlyList<T> items);
    // Targeted single-item changes — only the affected row's markup is rebuilt.
    void Upsert(T item);
    void Remove(string key);
    void SetUiStatus(UiStatusSnapshot uiStatusSnapshot);
}