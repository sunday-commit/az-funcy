using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Core.Model;

namespace Funcy.Console.Ui.Controllers;

// C#
public sealed class FunctionListController : ListPanelControllerBase<FunctionDetails>
{
    private readonly FunctionStateCoordinator _coordinator;
    private readonly Action? _invalidate;
    private readonly IUiStatusState _uiStatusState;
    private readonly string FunctionAppKey;

    public FunctionListController(IListPanelView<FunctionDetails> view,
        string appKey,
        IEnumerable<FunctionDetails> initial,
        FunctionStateCoordinator coordinator,
        IUiStatusState uiStatusState,
        Action? invalidate = null)
        : base(view)
    {
        _coordinator = coordinator;
        _invalidate = invalidate;
        _uiStatusState = uiStatusState;
        FunctionAppKey = appKey;
        
        View.SetAll(initial.ToList());
        PushStatusToView(_uiStatusState.GetSnapshot());
        _invalidate?.Invoke();
        
        _coordinator.OnFunctionListUpdated += OnListUpdated;
        _uiStatusState.Changed += OnUiStatusChanged;
    }

    private void OnListUpdated(string functionAppKey, List<FunctionDetails> updated)
    {
        if (string.Equals(FunctionAppKey, functionAppKey))
        {
            View.SetAll(updated);
            _invalidate?.Invoke();
        }

    }

    private void OnUiStatusChanged()
    {
        PushStatusToView(_uiStatusState.GetSnapshot());
        _invalidate?.Invoke();
    }

    public override void Dispose()
    {
        _coordinator.OnFunctionListUpdated -= OnListUpdated;
        _uiStatusState.Changed -= OnUiStatusChanged;
        base.Dispose();
    }
}
