using Funcy.Core.Model;

namespace Funcy.Console.Ui.Shortcuts;

public interface IShortcutProvider<in T> where T : IHasKey, IComparable<T>
{
    Dictionary<TableIndex, ShortcutMap> Describe(T? item);
    bool IsActionValid(T? getSelectedItem, FunctionAction action);
}