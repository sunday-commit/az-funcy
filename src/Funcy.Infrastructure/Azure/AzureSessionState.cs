namespace Funcy.Infrastructure.Azure;

public enum AzureSessionStatus
{
    Healthy,
    Expired,
    ReAuthenticating
}

/// <summary>
/// Immutable snapshot of the Azure session. Passed by value so the render thread reads a
/// consistent view without locking against the background monitor.
/// </summary>
public readonly record struct AzureSessionState(
    AzureSessionStatus Status,
    string? DeviceCodeUrl = null,
    string? DeviceCode = null,
    string? FailureNote = null)
{
    public static readonly AzureSessionState Healthy = new(AzureSessionStatus.Healthy);

    public bool IsHealthy => Status == AzureSessionStatus.Healthy;
}
