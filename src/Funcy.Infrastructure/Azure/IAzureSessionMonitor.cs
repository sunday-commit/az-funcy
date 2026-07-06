namespace Funcy.Infrastructure.Azure;

/// <summary>
/// Tracks whether the Azure (az CLI) session is usable and drives in-app re-login. All members
/// are safe to call from any thread; <see cref="Changed"/> fires when the state moves and is
/// coalesced so a burst of reports wakes the render loop once.
/// </summary>
public interface IAzureSessionMonitor
{
    event Action? Changed;

    AzureSessionState State { get; }

    /// <summary>
    /// Invoked once after a successful in-app re-login so data can be repopulated without a
    /// restart. Set at startup wiring rather than via the constructor to avoid a DI cycle with
    /// the handler that both reports failures and performs the refresh.
    /// </summary>
    Func<Task>? ReAuthenticatedCallback { get; set; }

    /// <summary>Runs the proactive probe loop (every 5 minutes) until <paramref name="token"/> cancels.</summary>
    Task RunProbeLoopAsync(CancellationToken token);

    /// <summary>Runs a single probe now. Success → Healthy; auth failure → Expired; other failures
    /// leave the state unchanged. Exposed so tests can drive the state machine without sleeping.</summary>
    Task ProbeOnceAsync(CancellationToken token);

    /// <summary>Reactive report from a failed Azure call. Flips to Expired only if classified as auth.</summary>
    void ReportPossibleAuthFailure(Exception? ex);

    /// <summary>Reactive report from az CLI output / an error string. Flips to Expired only if classified as auth.</summary>
    void ReportPossibleAuthFailure(string? outputOrMessage);

    /// <summary>Starts the device-code re-login in the background. Ignored unless state is Expired.</summary>
    void BeginReLogin();
}
