using Funcy.Console.Ui.Pagination.Matchers;
using Funcy.Console.Ui.State;
using Xunit;

namespace Funcy.Tests.Matchers;

public class UiErrorMatcherTests
{
    private static UiErrorEntry Entry(string scope, string message)
        => new(1, DateTime.UtcNow, scope, message);

    [Theory]
    [InlineData("my-app", "Timed out", "app")]      // scope match
    [InlineData("my-app", "Timed out", "timed")]     // message match
    [InlineData("my-app", "Timed out", "OUT")]       // case-insensitive
    public void TryMatch_MatchesScopeOrMessage(string scope, string message, string input)
    {
        var matcher = new UiErrorMatcher();
        Assert.True(matcher.TryMatch(Entry(scope, message), input));
    }

    [Fact]
    public void TryMatch_NoMatch_ReturnsFalse()
    {
        var matcher = new UiErrorMatcher();
        Assert.False(matcher.TryMatch(Entry("my-app", "Timed out"), "database"));
    }
}
