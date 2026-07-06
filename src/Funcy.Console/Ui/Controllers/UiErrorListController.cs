using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Console.Ui.State;

namespace Funcy.Console.Ui.Controllers;

// Live view over the error log: new errors appear while the panel is open, and Clear empties it.
public sealed class UiErrorListController : ListPanelControllerBase<UiErrorEntry>
{
    private readonly IUiErrorLog _errorLog;
    private readonly Action? _invalidate;

    public UiErrorListController(IListPanelView<UiErrorEntry> view,
        IUiErrorLog errorLog,
        Action? invalidate = null)
        : base(view)
    {
        _errorLog = errorLog;
        _invalidate = invalidate;

        View.SetAll(_errorLog.GetSnapshot());
        _invalidate?.Invoke();

        _errorLog.Changed += OnChanged;
    }

    private void OnChanged()
    {
        View.SetAll(_errorLog.GetSnapshot());
        _invalidate?.Invoke();
    }

    public override void Dispose()
    {
        _errorLog.Changed -= OnChanged;
        base.Dispose();
    }
}
