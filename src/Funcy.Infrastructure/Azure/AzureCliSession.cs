using System.Diagnostics;
using System.Text;
using Funcy.Infrastructure.Shell;

namespace Funcy.Infrastructure.Azure;

/// <summary>
/// Real <see cref="IAzureCliSession"/> backed by the az CLI. Uses <see cref="Process"/> directly
/// (rather than <see cref="ShellCommandRunner"/>) so the login flow can react to the device-code
/// line the instant az prints it to stderr, instead of after the long-running process exits.
/// </summary>
public sealed class AzureCliSession : IAzureCliSession
{
    public async Task<AzureCliResult> ProbeAccessTokenAsync(CancellationToken token)
    {
        return await RunAsync("account get-access-token --output json", onLine: null, token);
    }

    public async Task<AzureCliResult> LoginWithDeviceCodeAsync(
        Action<DeviceCodeInstruction> onDeviceCode, CancellationToken token)
    {
        var delivered = false;
        return await RunAsync("login --use-device-code --output json", line =>
        {
            // Parse only until the first match; the code is printed once, early, on stderr.
            if (delivered || !DeviceCodeParser.TryParse(line, out var url, out var code))
            {
                return;
            }

            delivered = true;
            onDeviceCode(new DeviceCodeInstruction(url, code));
        }, token);
    }

    private static async Task<AzureCliResult> RunAsync(
        string arguments, Action<string>? onLine, CancellationToken token)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ShellCommandRunner.GetShellExecutable("az"),
            Arguments = ShellCommandRunner.GetShellArguments("az", arguments),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();

        void Handle(string? data)
        {
            if (data is null)
            {
                return;
            }

            lock (output)
            {
                output.AppendLine(data);
            }

            onLine?.Invoke(data);
        }

        process.OutputDataReceived += (_, e) => Handle(e.Data);
        process.ErrorDataReceived += (_, e) => Handle(e.Data);

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(token);
        }
        catch (OperationCanceledException)
        {
            // Cancelled (app shutdown or timeout): kill the az process so it does not linger
            // waiting for the user to complete device-code auth in the browser.
            TryKill(process);
            throw;
        }
        catch (Exception ex)
        {
            // az missing / failed to start — non-auth by nature; surface as a failed result.
            return new AzureCliResult(false, ex.Message);
        }

        string combined;
        lock (output)
        {
            combined = output.ToString().Trim();
        }

        return new AzureCliResult(process.ExitCode == 0, combined);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort — the process may already be gone.
        }
    }
}
