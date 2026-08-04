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

    [Fact]
    public async Task RunAsync_WhenCommandReadsStandardInput_CompletesInsteadOfHanging()
    {
        // Pins that child processes get a closed stdin: a hidden interactive prompt (e.g. az
        // offering to install a missing extension) must fail fast instead of blocking the app.
        var runner = new ShellCommandRunner(TimeSpan.FromSeconds(10));
        var stopwatch = Stopwatch.StartNew();

        // 'sort' with no file argument reads stdin until EOF on every supported OS.
        var output = await runner.RunAsync("sort", string.Empty);

        Assert.Equal(string.Empty, output);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    private static string LongRunningCommand() => OperatingSystem.IsWindows() ? "ping" : "sleep";

    private static string LongRunningArguments() => OperatingSystem.IsWindows() ? "127.0.0.1 -n 10" : "10";
}
