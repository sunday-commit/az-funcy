using System.Diagnostics;

namespace Funcy.Infrastructure.Shell;

public record ToolValidationResult(bool IsValid, List<string> MissingTools, List<string> InstallInstructions);

// Checks that the external tools the app shells out to are present before the UI starts.
public interface IToolValidationService
{
    Task<ToolValidationResult> ValidateRequiredToolsAsync();
}

public class ToolValidationService : IToolValidationService
{
    private const int TimeoutSeconds = 10;

    public async Task<ToolValidationResult> ValidateRequiredToolsAsync()
    {
        var missingTools = new List<string>();
        var instructions = new List<string>();

        var azCliInstalled = await IsToolInstalledAsync("az", "--version");
        if (!azCliInstalled)
        {
            missingTools.Add("Azure CLI (az)");
            instructions.Add("Install Azure CLI: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli");
        }
        else
        {
            var graphExtensionInstalled = await IsGraphExtensionInstalledAsync();
            if (!graphExtensionInstalled)
            {
                missingTools.Add("Azure Resource Graph extension");
                instructions.Add("Install extension: az extension add --name resource-graph");
            }
        }

        return new ToolValidationResult(
            IsValid: missingTools.Count == 0,
            MissingTools: missingTools,
            InstallInstructions: instructions
        );
    }

    private async Task<bool> IsToolInstalledAsync(string command, string arguments)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));

            var psi = new ProcessStartInfo
            {
                FileName = ShellCommandRunner.GetShellExecutable(command),
                Arguments = ShellCommandRunner.GetShellArguments(command, arguments),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            // Closed stdin keeps a hidden interactive prompt from blocking the check.
            process.StandardInput.Close();

            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> IsGraphExtensionInstalledAsync()
    {
        return await IsToolInstalledAsync("az", "extension show --name resource-graph");
    }
}
