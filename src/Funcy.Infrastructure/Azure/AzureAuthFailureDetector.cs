using Azure.Identity;

namespace Funcy.Infrastructure.Azure;

/// <summary>
/// Pure classification of Azure authentication failures. Kept deliberately conservative:
/// a false positive (flagging a transient network error as an expired session) is worse
/// than a false negative, because it would push the whole app into the "session expired"
/// state and hide a temporary glitch behind a re-login prompt.
/// </summary>
public static class AzureAuthFailureDetector
{
    // az CLI / Azure.Identity signatures that specifically indicate a missing or expired
    // interactive session (as opposed to a network hiccup, a throttle, or a missing tool).
    // Matched case-insensitively as substrings. Each entry is documented so the list can be
    // pruned or extended as az wording drifts.
    private static readonly string[] AuthSignatures =
    [
        // The generic remediation hint az prints whenever the cached credential is gone.
        "az login",

        // az's exact phrasing of the same hint, kept explicit in case the spacing above drifts.
        "please run 'az login'",

        // AADSTS700082: "The refresh token has expired due to inactivity." — the overnight case.
        "aadsts700082",

        // AADSTS70043: refresh token expired/invalid due to conditional-access sign-in frequency.
        "aadsts70043",

        // Plain-language variant emitted by both az and the SDK when the refresh token is dead.
        "refresh token has expired",

        // Surfaced when DefaultAzureCredential falls through to managed identity and finds none,
        // which in practice means the interactive (az) credential ahead of it was unavailable.
        "managedidentitycredential",
    ];

    /// <summary>
    /// True when the exception chain contains an Azure.Identity credential failure. Walks
    /// inner exceptions and <see cref="AggregateException"/> children so a failure buried
    /// under retry/aggregation wrappers is still recognised.
    /// </summary>
    public static bool IsAuthFailure(Exception? ex)
    {
        while (ex is not null)
        {
            // CredentialUnavailableException derives from AuthenticationFailedException, so the
            // single assignability check below covers both types the spec calls out.
            if (ex is AuthenticationFailedException or CredentialUnavailableException)
            {
                return true;
            }

            if (ex is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (IsAuthFailure(inner))
                    {
                        return true;
                    }
                }

                return false;
            }

            ex = ex.InnerException;
        }

        return false;
    }

    /// <summary>
    /// True when the text (an az CLI stdout/stderr dump or an exception message) carries a
    /// known session-expiry signature. Unknown errors return false on purpose.
    /// </summary>
    public static bool IsAuthFailure(string? outputOrMessage)
    {
        if (string.IsNullOrWhiteSpace(outputOrMessage))
        {
            return false;
        }

        foreach (var signature in AuthSignatures)
        {
            if (outputOrMessage.Contains(signature, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
