using Azure;
using Funcy.Infrastructure.Azure;
using Xunit;

namespace Funcy.Tests.Azure;

public class AzurePermissionErrorTests
{
    [Fact]
    public void IsAccessDenied_WithForbiddenRequest_ReturnsTrue()
        => Assert.True(AzurePermissionError.IsAccessDenied(new RequestFailedException(403, "Forbidden")));

    [Fact]
    public void IsAccessDenied_WithWrappedForbiddenRequest_ReturnsTrue()
        => Assert.True(AzurePermissionError.IsAccessDenied(
            new InvalidOperationException("outer", new RequestFailedException(403, "Forbidden"))));

    [Fact]
    public void IsAccessDenied_WithNotFoundRequest_ReturnsFalse()
        => Assert.False(AzurePermissionError.IsAccessDenied(new RequestFailedException(404, "Not found")));

    [Theory]
    [InlineData("AuthorizationFailed: The client does not have authorization to perform action")]
    [InlineData("Request failed with status code 403 (Forbidden)")]
    public void IsAccessDenied_WithAzureCliAuthorizationFailure_ReturnsTrue(string message)
        => Assert.True(AzurePermissionError.IsAccessDenied(new InvalidOperationException(message)));
}
