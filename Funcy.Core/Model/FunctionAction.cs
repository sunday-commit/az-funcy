namespace Funcy.Core.Model;

public enum FunctionAction
{
    Start,
    Stop,
    Swap,
    ToggleDisabled,
    Refresh,
    RefreshAll,
    ChangeSubscription,
    HideSubscription,
    ToggleSubscriptionVisibility,
    Pin,
    ViewAppSettings,
    ToggleMask
}

public static class FunctionActionExtensions
{
    public static FunctionState GetFunctionState(this FunctionAction action)
    {
        return action switch
        {
            FunctionAction.Start => FunctionState.Running,
            FunctionAction.Stop => FunctionState.Stopped,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }
}