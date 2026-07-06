using Funcy.Core.Model;

namespace Funcy.Core.Interfaces;

public interface IFunctionAppManagementService
{
    Task StartFunction(FunctionAppDetails functionAppDetails);
    Task StopFunction(FunctionAppDetails functionAppDetails);
    Task SwapFunction(FunctionAppDetails functionAppDetails, FunctionAppSlotDetails functionAppSlot);
    Task SetFunctionDisabled(FunctionAppDetails functionAppDetails, string functionName, bool disabled);
}