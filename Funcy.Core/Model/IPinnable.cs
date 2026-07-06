namespace Funcy.Core.Model;

// Opt-in marker for list items that can be pinned to the top of a panel. The generic
// sorter checks for this so pinned-first ordering stays out of otherwise generic code.
public interface IPinnable
{
    bool IsPinned { get; }
}
