using Funcy.Console.Ui.Pagination.Matchers;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Matchers;

public class AppSettingMatcherTests
{
    private readonly AppSettingMatcher _sut = new();

    private static AppSettingDetails Setting(string name, string value) =>
        new() { Name = name, Value = value };

    [Fact]
    public void Match_WhenNameContainsInput()
    {
        Assert.True(_sut.TryMatch(Setting("ConnectionString", "server=x"), "connection"));
    }

    [Fact]
    public void NoMatch_WhenOnlyValueContainsInput()
    {
        // Values may be masked; matching on them would leak that a secret contains a substring.
        Assert.False(_sut.TryMatch(Setting("ConnectionString", "topsecret"), "topsecret"));
    }

    [Fact]
    public void CaseInsensitive_MatchesUpperCase()
    {
        Assert.True(_sut.TryMatch(Setting("ApiKey", "abc"), "APIKEY"));
    }

    [Fact]
    public void EmptyInput_ReturnsTrue()
    {
        Assert.True(_sut.TryMatch(Setting("ApiKey", "abc"), ""));
    }
}
