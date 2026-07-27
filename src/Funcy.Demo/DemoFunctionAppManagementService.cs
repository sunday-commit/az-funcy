using Funcy.Core.Interfaces;
using Funcy.Core.Model;

namespace Funcy.Demo;

/// <summary>Applies start/stop/swap/disable to the in-memory estate. The delays are what make the
/// in-progress spinner and the short-lived success status visible in a recording.</summary>
internal sealed class DemoFunctionAppManagementService(DemoEstate estate) : IFunctionAppManagementService
{
    public async Task StartFunction(FunctionAppDetails functionAppDetails)
    {
        await Task.Delay(DemoLatency.Action);
        estate.SetState(functionAppDetails.Id, FunctionState.Running);
        functionAppDetails.LastUpdated = DateTime.UtcNow;
    }

    public async Task StopFunction(FunctionAppDetails functionAppDetails)
    {
        await Task.Delay(DemoLatency.Action);
        estate.SetState(functionAppDetails.Id, FunctionState.Stopped);
        functionAppDetails.LastUpdated = DateTime.UtcNow;
    }

    public async Task SwapFunction(FunctionAppDetails functionAppDetails, FunctionAppSlotDetails functionAppSlot)
    {
        // A real swap is the slowest action by far; keeping it slow here is what shows that the UI
        // stays responsive while one runs.
        await Task.Delay(DemoLatency.Swap);
        estate.Swap(functionAppDetails.Id, functionAppSlot.Name);
        functionAppDetails.LastUpdated = DateTime.UtcNow;
    }

    public async Task SetFunctionDisabled(FunctionAppDetails functionAppDetails, string functionName, bool disabled)
    {
        await Task.Delay(DemoLatency.Action);
        estate.SetFunctionDisabled(functionAppDetails.Id, functionName, disabled);
    }
}
