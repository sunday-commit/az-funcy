namespace Funcy.Core.Model;

public enum FunctionAction
{
    Start,
    Stop,
    Swap,
    Refresh,
    RefreshAll,
    ChangeSubscription,
    HideSubscription,
    ToggleSubscriptionVisibility
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