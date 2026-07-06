using Funcy.Core.Model;

namespace Funcy.Console.Ui.Input;

public record InputActionResult(
    FunctionAction Action,
    FunctionAppDetails FunctionAppDetails,
    FunctionAppSlotDetails? SlotDetails = null,
    FunctionDetails? FunctionDetails = null);