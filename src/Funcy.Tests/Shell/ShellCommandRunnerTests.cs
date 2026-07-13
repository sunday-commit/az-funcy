using System.Diagnostics;
using Funcy.Infrastructure.Shell;
using Xunit;

namespace Funcy.Tests.Shell;

public class ShellCommandRunnerTests
{
    [Fact]
    public async Task RunAsync_WhenCallerCancels_StopsCommand()
    {
        var runner = new ShellCommandRunner(TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(LongRunningCommand(), LongRunningArguments(), cancellation.Token));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RunAsync_WhenTimeoutExpires_StopsCommandAndThrowsTimeoutException()
    {
        var runner = new ShellCommandRunner(TimeSpan.FromMilliseconds(100));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            runner.RunAsync(LongRunningCommand(), LongRunningArguments()));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    private static string LongRunningCommand() => OperatingSystem.IsWindows() ? "ping" : "sleep";

    private static string LongRunningArguments() => OperatingSystem.IsWindows() ? "127.0.0.1 -n 10" : "10";
}
