using Azure.Identity;
using Funcy.Infrastructure.Azure;
using Xunit;

namespace Funcy.Tests.Azure;

public class AzureAuthFailureDetectorTests
{
    [Theory]
    [InlineData("Please run 'az login' to setup account.")]
    [InlineData("ERROR: AADSTS700082: The refresh token has expired due to inactivity.")]
    [InlineData("AADSTS70043: The refresh token has expired or is invalid.")]
    [InlineData("The refresh token has expired. Please sign in again.")]
    [InlineData("ManagedIdentityCredential authentication unavailable.")]
    [InlineData("run AZ LOGIN again")]
    public void IsAuthFailure_KnownSignature_ReturnsTrue(string message)
        => Assert.True(AzureAuthFailureDetector.IsAuthFailure(message));

    [Theory]
    [InlineData("A connection attempt failed because the connected party did not properly respond.")]
    [InlineData("The operation was canceled due to a timeout.")]
    [InlineData("HTTP 503 Service Unavailable")]
    [InlineData("Name or service not known")]
    [InlineData("some totally unrelated output")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsAuthFailure_NonAuthText_ReturnsFalse(string? message)
        => Assert.False(AzureAuthFailureDetector.IsAuthFailure(message));

    [Fact]
    public void IsAuthFailure_AuthenticationFailedException_ReturnsTrue()
        => Assert.True(AzureAuthFailureDetector.IsAuthFailure(new AuthenticationFailedException("boom")));

    [Fact]
    public void IsAuthFailure_CredentialUnavailableException_ReturnsTrue()
        => Assert.True(AzureAuthFailureDetector.IsAuthFailure(new CredentialUnavailableException("no creds")));

    [Fact]
    public void IsAuthFailure_NestedInInnerException_ReturnsTrue()
    {
        var ex = new InvalidOperationException("wrapper",
            new Exception("mid", new AuthenticationFailedException("root")));

        Assert.True(AzureAuthFailureDetector.IsAuthFailure(ex));
    }

    [Fact]
    public void IsAuthFailure_InsideAggregateException_ReturnsTrue()
    {
        var ex = new AggregateException(
            new TimeoutException("slow"),
            new CredentialUnavailableException("no creds"));

        Assert.True(AzureAuthFailureDetector.IsAuthFailure(ex));
    }

    [Fact]
    public void IsAuthFailure_UnrelatedException_ReturnsFalse()
        => Assert.False(AzureAuthFailureDetector.IsAuthFailure(
            new InvalidOperationException("db locked", new TimeoutException())));

    [Fact]
    public void IsAuthFailure_NullException_ReturnsFalse()
        => Assert.False(AzureAuthFailureDetector.IsAuthFailure((Exception?)null));
}
