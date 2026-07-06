namespace Funcy.Core.Model;

// Opt-in seam: a row that reports an active operation bypasses the text filter,
// so an in-progress (or just-finished) operation stays visible even when the
// filter no longer matches it. Self-clears once HasActiveOperation goes false.
public interface IOperationVisibility
{
    bool HasActiveOperation { get; }
}
