using Azure.Identity;
using Funcy.Infrastructure.Azure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Funcy.Tests.Azure;

public class AzureSessionMonitorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class FakeCliSession : IAzureCliSession
    {
        public AzureCliResult ProbeResult = new(true, "");
        public AzureCliResult LoginResult = new(true, "");
        public DeviceCodeInstruction? DeviceCodeToEmit;
        public TaskCompletionSource? LoginGate;

        public int ProbeCount;
        public int LoginCount;

        public Task<AzureCliResult> ProbeAccessTokenAsync(CancellationToken token)
        {
            Interlocked.Increment(ref ProbeCount);
            return Task.FromResult(ProbeResult);
        }

        public async Task<AzureCliResult> LoginWithDeviceCodeAsync(
            Action<DeviceCodeInstruction> onDeviceCode, CancellationToken token)
        {
            Interlocked.Increment(ref LoginCount);
            if (DeviceCodeToEmit is not null)
            {
                onDeviceCode(DeviceCodeToEmit);
            }

            if (LoginGate is not null)
            {
                await LoginGate.Task.WaitAsync(token);
            }

            return LoginResult;
        }
    }

    private static AzureSessionMonitor CreateMonitor(FakeCliSession cli)
        => new(cli, NullLogger<AzureSessionMonitor>.Instance);

    private static async Task WaitForStatusAsync(IAzureSessionMonitor monitor, AzureSessionStatus status)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (monitor.State.Status != status)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"State never reached {status}; was {monitor.State.Status}.");
            }

            await Task.Delay(5);
        }
    }

    [Fact]
    public async Task ProbeOnceAsync_ProbeSucceeds_StaysHealthy()
    {
        var cli = new FakeCliSession { ProbeResult = new AzureCliResult(true, "") };
        var monitor = CreateMonitor(cli);

        await monitor.ProbeOnceAsync(CancellationToken.None);

        Assert.Equal(AzureSessionStatus.Healthy, monitor.State.Status);
    }

    [Fact]
    public async Task ProbeOnceAsync_AuthFailure_BecomesExpired()
    {
        var cli = new FakeCliSession
        {
            ProbeResult = new AzureCliResult(false, "Please run 'az login' to setup account.")
        };
        var monitor = CreateMonitor(cli);

        await monitor.ProbeOnceAsync(CancellationToken.None);

        Assert.Equal(AzureSessionStatus.Expired, monitor.State.Status);
    }

    [Fact]
    public async Task ProbeOnceAsync_NonAuthFailure_KeepsHealthy()
    {
        var cli = new FakeCliSession
        {
            ProbeResult = new AzureCliResult(false, "Name or service not known")
        };
        var monitor = CreateMonitor(cli);

        await monitor.ProbeOnceAsync(CancellationToken.None);

        Assert.Equal(AzureSessionStatus.Healthy, monitor.State.Status);
    }

    [Fact]
    public void ReportPossibleAuthFailure_AuthException_BecomesExpired()
    {
        var monitor = CreateMonitor(new FakeCliSession());

        monitor.ReportPossibleAuthFailure(new AuthenticationFailedException("expired"));

        Assert.Equal(AzureSessionStatus.Expired, monitor.State.Status);
    }

    [Fact]
    public void ReportPossibleAuthFailure_NonAuth_StaysHealthy()
    {
        var monitor = CreateMonitor(new FakeCliSession());

        monitor.ReportPossibleAuthFailure(new TimeoutException("slow"));

        Assert.Equal(AzureSessionStatus.Healthy, monitor.State.Status);
    }

    [Fact]
    public void BeginReLogin_WhenHealthy_Ignored()
    {
        var cli = new FakeCliSession();
        var monitor = CreateMonitor(cli);

        monitor.BeginReLogin();

        Assert.Equal(AzureSessionStatus.Healthy, monitor.State.Status);
        Assert.Equal(0, cli.LoginCount);
    }

    [Fact]
    public async Task BeginReLogin_Success_BecomesHealthyAndRefreshesExactlyOnce()
    {
        var cli = new FakeCliSession
        {
            LoginResult = new AzureCliResult(true, ""),
            ProbeResult = new AzureCliResult(true, "")
        };
        var monitor = CreateMonitor(cli);
        var refreshCount = 0;
        monitor.ReAuthenticatedCallback = () =>
        {
            Interlocked.Increment(ref refreshCount);
            return Task.CompletedTask;
        };

        monitor.ReportPossibleAuthFailure("az login");
        Assert.Equal(AzureSessionStatus.Expired, monitor.State.Status);

        monitor.BeginReLogin();
        await WaitForStatusAsync(monitor, AzureSessionStatus.Healthy);

        Assert.Equal(1, cli.LoginCount);
        Assert.Equal(1, refreshCount);
    }

    [Fact]
    public async Task BeginReLogin_EmitsDeviceCodeToBanner()
    {
        var cli = new FakeCliSession
        {
            DeviceCodeToEmit = new DeviceCodeInstruction("https://microsoft.com/devicelogin", "ABCD1234"),
            LoginGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var monitor = CreateMonitor(cli);

        monitor.ReportPossibleAuthFailure("az login");
        monitor.BeginReLogin();

        // Login is held open by the gate so the device-code state is observable.
        var deadline = DateTime.UtcNow + Timeout;
        while (monitor.State.DeviceCode is null)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Device code was never surfaced.");
            }

            await Task.Delay(5);
        }

        Assert.Equal(AzureSessionStatus.ReAuthenticating, monitor.State.Status);
        Assert.Equal("https://microsoft.com/devicelogin", monitor.State.DeviceCodeUrl);
        Assert.Equal("ABCD1234", monitor.State.DeviceCode);

        cli.LoginGate!.SetResult();
        await WaitForStatusAsync(monitor, AzureSessionStatus.Healthy);
    }

    [Fact]
    public async Task BeginReLogin_LoginFails_ReturnsToExpired()
    {
        var cli = new FakeCliSession { LoginResult = new AzureCliResult(false, "device code declined") };
        var monitor = CreateMonitor(cli);

        monitor.ReportPossibleAuthFailure("az login");
        monitor.BeginReLogin();

        // ReAuthenticating -> back to Expired once login fails.
        await WaitForStatusAsync(monitor, AzureSessionStatus.Expired);
        Assert.Equal(1, cli.LoginCount);
        Assert.Equal("re-login failed", monitor.State.FailureNote);
    }

    [Fact]
    public async Task BeginReLogin_WhileReAuthenticating_SecondCallIgnored()
    {
        var cli = new FakeCliSession
        {
            LoginGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var monitor = CreateMonitor(cli);

        monitor.ReportPossibleAuthFailure("az login");
        monitor.BeginReLogin();
        await WaitForStatusAsync(monitor, AzureSessionStatus.ReAuthenticating);

        // Second press while a login is already running must be ignored.
        monitor.BeginReLogin();

        cli.LoginGate!.SetResult();
        await WaitForStatusAsync(monitor, AzureSessionStatus.Healthy);

        Assert.Equal(1, cli.LoginCount);
    }

    [Fact]
    public void Changed_RaisedOnTransition_AndSuppressedWhenStateUnchanged()
    {
        var monitor = CreateMonitor(new FakeCliSession());
        var raised = 0;
        monitor.Changed += () => Interlocked.Increment(ref raised);

        monitor.ReportPossibleAuthFailure("az login"); // Healthy -> Expired
        monitor.ReportPossibleAuthFailure("az login"); // Expired -> Expired (no change)

        var deadline = DateTime.UtcNow + Timeout;
        while (Volatile.Read(ref raised) == 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(5);
        }

        Assert.Equal(AzureSessionStatus.Expired, monitor.State.Status);
        Assert.True(Volatile.Read(ref raised) >= 1);
    }
}
