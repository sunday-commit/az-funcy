using Microsoft.Extensions.Logging;

namespace Funcy.Infrastructure.Azure;

/// <summary>
/// Singleton that keeps the Azure session state current via a proactive probe and reactive
/// reports from failed calls, and orchestrates device-code re-login. No Spectre/UI types here:
/// it mutates state on background threads and coalesces a <see cref="Changed"/> notification so
/// the render loop can recompute the banner on the render thread (PR #24 threading rule).
/// </summary>
public sealed class AzureSessionMonitor : IAzureSessionMonitor, IDisposable
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(15);

    private readonly IAzureCliSession _cliSession;
    private readonly ILogger<AzureSessionMonitor> _logger;

    private readonly object _gate = new();
    private AzureSessionState _state = AzureSessionState.Healthy;

    private readonly CancellationTokenSource _disposeCts = new();
    private int _reLoginInProgress;
    private int _changeQueued;

    public AzureSessionMonitor(IAzureCliSession cliSession, ILogger<AzureSessionMonitor> logger)
    {
        _cliSession = cliSession;
        _logger = logger;
    }

    public event Action? Changed;

    public Func<Task>? ReAuthenticatedCallback { get; set; }

    public AzureSessionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public async Task RunProbeLoopAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _disposeCts.Token);
        while (!linked.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(ProbeInterval, linked.Token);
                await ProbeOnceAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let the loop die: a probe failure must not stop future probes.
                _logger.LogDebug(ex, "Azure session probe iteration failed");
            }
        }
    }

    public async Task ProbeOnceAsync(CancellationToken token)
    {
        // Do not let a scheduled probe disturb an in-progress re-login.
        if (State.Status == AzureSessionStatus.ReAuthenticating)
        {
            return;
        }

        var result = await _cliSession.ProbeAccessTokenAsync(token);
        if (result.Success)
        {
            SetHealthy();
            return;
        }

        if (AzureAuthFailureDetector.IsAuthFailure(result.Output))
        {
            SetExpired(null);
        }
        else
        {
            // Network glitch, throttle, missing tool: keep current state, log only. Flipping to
            // Expired here would be the false positive we explicitly want to avoid.
            _logger.LogDebug("Azure session probe failed without an auth signature: {Output}", result.Output);
        }
    }

    public void ReportPossibleAuthFailure(Exception? ex)
    {
        if (AzureAuthFailureDetector.IsAuthFailure(ex))
        {
            SetExpired(null);
        }
    }

    public void ReportPossibleAuthFailure(string? outputOrMessage)
    {
        if (AzureAuthFailureDetector.IsAuthFailure(outputOrMessage))
        {
            SetExpired(null);
        }
    }

    public void BeginReLogin()
    {
        if (State.Status != AzureSessionStatus.Expired)
        {
            return;
        }

        // Single-flight: ignore a second L press while a login is already running.
        if (Interlocked.CompareExchange(ref _reLoginInProgress, 1, 0) != 0)
        {
            return;
        }

        Transition(new AzureSessionState(AzureSessionStatus.ReAuthenticating));
        _ = Task.Run(ReLoginAsync);
    }

    private async Task ReLoginAsync()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
        cts.CancelAfter(LoginTimeout);

        try
        {
            var result = await _cliSession.LoginWithDeviceCodeAsync(OnDeviceCode, cts.Token);

            if (result.Success && await ConfirmHealthyAsync(cts.Token))
            {
                SetHealthy();
                await InvokeReAuthenticatedAsync();
            }
            else
            {
                SetExpired("re-login failed");
            }
        }
        catch (OperationCanceledException)
        {
            // Timed out or app shutting down.
            SetExpired("re-login timed out");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure re-login failed");
            SetExpired("re-login failed");
        }
        finally
        {
            Interlocked.Exchange(ref _reLoginInProgress, 0);
        }
    }

    private async Task<bool> ConfirmHealthyAsync(CancellationToken token)
    {
        // az exited 0; confirm a token is actually obtainable before clearing the banner.
        var probe = await _cliSession.ProbeAccessTokenAsync(token);
        return probe.Success;
    }

    private void OnDeviceCode(DeviceCodeInstruction info)
    {
        // Keep the ReAuthenticating status; surface the URL + code for the banner.
        Transition(new AzureSessionState(
            AzureSessionStatus.ReAuthenticating, info.Url, info.Code));
    }

    private async Task InvokeReAuthenticatedAsync()
    {
        var callback = ReAuthenticatedCallback;
        if (callback is null)
        {
            return;
        }

        try
        {
            await callback();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Post re-login refresh failed");
        }
    }

    private void SetHealthy() => Transition(AzureSessionState.Healthy);

    private void SetExpired(string? failureNote)
        => Transition(new AzureSessionState(AzureSessionStatus.Expired, FailureNote: failureNote));

    private void Transition(AzureSessionState next)
    {
        lock (_gate)
        {
            if (_state == next)
            {
                return;
            }

            _state = next;
        }

        QueueChanged();
    }

    private void QueueChanged()
    {
        // Coalesce bursts into a single notification (mirrors UiStatusState.QueueChanged).
        if (Interlocked.Exchange(ref _changeQueued, 1) == 1)
        {
            return;
        }

        ThreadPool.QueueUserWorkItem(static state =>
        {
            var self = (AzureSessionMonitor)state!;
            Interlocked.Exchange(ref self._changeQueued, 0);
            self.Changed?.Invoke();
        }, this);
    }

    public void Dispose()
    {
        try
        {
            _disposeCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }

        _disposeCts.Dispose();
    }
}
