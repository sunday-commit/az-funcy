using Funcy.Console.Ui;
using Funcy.Infrastructure.Azure;
using Xunit;

namespace Funcy.Tests.Ui;

public class SessionBannerTests
{
    [Fact]
    public void CreateSessionBannerMarkup_Healthy_ReturnsNull()
        => Assert.Null(UiStyles.CreateSessionBannerMarkup(AzureSessionState.Healthy));

    [Fact]
    public void CreateSessionBannerMarkup_Expired_MentionsReLoginKey()
    {
        var markup = UiStyles.CreateSessionBannerMarkup(
            new AzureSessionState(AzureSessionStatus.Expired));

        Assert.NotNull(markup);
        Assert.Contains("expired", markup);
        Assert.Contains("press L to re-login", markup);
    }

    [Fact]
    public void CreateSessionBannerMarkup_ExpiredWithFailureNote_IncludesNote()
    {
        var markup = UiStyles.CreateSessionBannerMarkup(
            new AzureSessionState(AzureSessionStatus.Expired, FailureNote: "re-login failed"));

        Assert.NotNull(markup);
        Assert.Contains("(re-login failed)", markup);
    }

    [Fact]
    public void CreateSessionBannerMarkup_ReAuthenticatingWithCode_ShowsUrlAndCode()
    {
        var markup = UiStyles.CreateSessionBannerMarkup(
            new AzureSessionState(AzureSessionStatus.ReAuthenticating,
                "https://microsoft.com/devicelogin", "ABCD1234"));

        Assert.NotNull(markup);
        Assert.Contains("https://microsoft.com/devicelogin", markup);
        Assert.Contains("ABCD1234", markup);
    }

    [Fact]
    public void CreateSessionBannerMarkup_ReAuthenticatingWithoutCode_ShowsStarting()
    {
        var markup = UiStyles.CreateSessionBannerMarkup(
            new AzureSessionState(AzureSessionStatus.ReAuthenticating));

        Assert.NotNull(markup);
        Assert.Contains("starting", markup);
    }
}
