using Azure.Identity;

namespace Funcy.Console.Ui;

internal record ErrorInfo(string Title, string? Detail, string[] Actions);

internal static class InitializationErrorResolver
{
    internal static ErrorInfo Resolve(Exception exception)
    {
        if (exception is CredentialUnavailableException credEx)
            return ResolveCredentialError(credEx);

        if (IsNotLoggedIn(exception.Message))
            return new ErrorInfo("You are not logged in to Azure", null, ["az login"]);

        return new ErrorInfo("Initialization failed", exception.Message, []);
    }

    private static ErrorInfo ResolveCredentialError(CredentialUnavailableException ex)
    {
        var message = ex.Message;

        if (IsExpiredOrRevoked(message))
        {
            return new ErrorInfo(
                "Your Azure session has expired",
                "Your credentials were revoked or your password has changed.",
                ["az logout", "az login"]);
        }

        if (IsNotLoggedIn(message))
        {
            return new ErrorInfo(
                "You are not logged in to Azure",
                null,
                ["az login"]);
        }

        return new ErrorInfo(
            "Azure authentication failed",
            null,
            ["az login"]);
    }

    private static bool IsExpiredOrRevoked(string message) =>
        message.Contains("AADSTS50173") ||
        message.Contains("AADSTS70043") ||
        message.Contains("TokensValidFrom") ||
        message.Contains("revoked") ||
        (message.Contains("expired") && message.Contains("token", StringComparison.OrdinalIgnoreCase));

    private static bool IsNotLoggedIn(string message) =>
        message.Contains("az login") ||
        message.Contains("not logged in", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Please run 'az login'");
}
