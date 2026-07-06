namespace Funcy.Infrastructure.Azure;

/// <summary>Outcome of an az CLI invocation. <see cref="Output"/> carries the combined
/// stdout/stderr so callers can classify a failure (see <see cref="AzureAuthFailureDetector"/>).</summary>
public sealed record AzureCliResult(bool Success, string Output);

/// <summary>Device-login instruction streamed from az before <c>az login</c> completes.</summary>
public sealed record DeviceCodeInstruction(string Url, string Code);

/// <summary>
/// Thin, injectable wrapper over the az CLI so the session monitor can be tested without a
/// real process. The static <see cref="Shell.ShellCommandRunner"/> stays untouched for its
/// other callers; this exists because re-login needs to stream the device-code line as it is
/// printed, which the fire-once runner cannot do.
/// </summary>
public interface IAzureCliSession
{
    /// <summary>Runs <c>az account get-access-token</c>. Success means the session is healthy;
    /// on failure the combined output lets the caller decide whether it is an auth failure.</summary>
    Task<AzureCliResult> ProbeAccessTokenAsync(CancellationToken token);

    /// <summary>Runs <c>az login --use-device-code</c>, invoking <paramref name="onDeviceCode"/>
    /// as soon as the instruction line is parsed from the stream (well before the process exits).</summary>
    Task<AzureCliResult> LoginWithDeviceCodeAsync(Action<DeviceCodeInstruction> onDeviceCode, CancellationToken token);
}
