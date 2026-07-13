using Azure;

namespace Funcy.Infrastructure.Azure;

public static class AzurePermissionError
{
    public static bool IsAccessDenied(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is RequestFailedException { Status: 403 })
            {
                return true;
            }

            var message = current.Message;
            if (message.Contains("AuthorizationFailed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("status code 403", StringComparison.OrdinalIgnoreCase)
                || message.Contains("403 (Forbidden)", StringComparison.OrdinalIgnoreCase)
                || message.Contains("does not have authorization to perform action", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string Required(string capability, string roleAndScope)
        => $"{capability} access denied. Required: {roleAndScope}.";
}
