namespace Funcy.Infrastructure.Shell;

using System.Diagnostics;

public interface IShellCommandRunner
{
    Task<string> RunAsync(string command, string arguments, CancellationToken cancellationToken = default);
}

public sealed class ShellCommandRunner(TimeSpan? timeout = null) : IShellCommandRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _timeout = timeout ?? DefaultTimeout;

    public async Task<string> RunAsync(
        string command,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var psi = new ProcessStartInfo
        {
            FileName = GetShellExecutable(command),
            Arguments = GetShellArguments(command, arguments),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorsTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested
                                                 && !cancellationToken.IsCancellationRequested)
        {
            await StopProcessAsync(process);
            throw new TimeoutException($"Command '{command}' exceeded the {_timeout.TotalSeconds:0}-second timeout.");
        }
        catch (OperationCanceledException)
        {
            await StopProcessAsync(process);
            throw;
        }

        var output = await outputTask;
        var errors = await errorsTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command '{command} {arguments}' failed with exit code {process.ExitCode}:\n{errors}"
            );
        }

        return output.Trim();
    }

    private static async Task StopProcessAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
        }
        catch
        {
            // Best effort. The process may have exited between the checks.
        }
    }

    public static string GetShellExecutable(string command)
    {
        return OperatingSystem.IsWindows() ? "cmd.exe" : command;
    }

    public static string GetShellArguments(string command, string arguments)
    {
        return OperatingSystem.IsWindows() ? $"/c {command} {arguments}" : arguments;
    }
}
