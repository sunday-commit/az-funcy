using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Core.Model;

namespace Funcy.Console.Ui.Controllers;

// C#
public sealed class FunctionAppListController : ListPanelControllerBase<FunctionAppDetails>
{
    private readonly FunctionStateCoordinator _coordinator;
    private readonly Action? _invalidate;
    private readonly IUiStatusState _uiStatusState;

    public FunctionAppListController(IListPanelView<FunctionAppDetails> view,
        FunctionStateCoordinator coordinator,
        IUiStatusState uiStatusState,
        Action? invalidate = null)
        : base(view)
    {
        _coordinator = coordinator;
        _invalidate = invalidate;
        _uiStatusState = uiStatusState;

        _coordinator.OnCacheInit += OnCacheInit;
        _coordinator.OnFunctionAppUpdated += OnUpdated;
        _coordinator.OnFunctionAppRemoved += OnRemoved;
        _uiStatusState.Changed += OnUiStatusChanged;
    }

    private void OnCacheInit(List<FunctionAppDetails> functionAppDetailsList)
    {
        View.SetAll(_coordinator.GetCachedFunctionAppDetails());
        _invalidate?.Invoke();
    }

    private void OnUiStatusChanged()
    {
        PushStatusToView(_uiStatusState.GetSnapshot());
        _invalidate?.Invoke();
    }

    private void OnUpdated(FunctionAppDetails updated)
    {
        View.Upsert(updated);
        _invalidate?.Invoke();
    }

    private void OnRemoved(FunctionAppDetails removed)
    {
        View.Remove(removed.Key);
        _invalidate?.Invoke();
    }

    public override void Dispose()
    {
        _coordinator.OnCacheInit -= OnCacheInit;
        _coordinator.OnFunctionAppUpdated -= OnUpdated;
        _coordinator.OnFunctionAppRemoved -= OnRemoved;
        _uiStatusState.Changed -= OnUiStatusChanged;
        base.Dispose();
    }
}