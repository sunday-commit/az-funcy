using Funcy.Console.Settings;
using Funcy.Console.Ui.Pagination.Matchers;
using Xunit;

namespace Funcy.Tests.Matchers;

public class SettingMatcherTests
{
    private readonly SettingMatcher _sut = new();

    private static SettingItemDetails MakeItem(string name, string description) =>
        new() { Order = 0, Name = name, Value = "value", Description = description };

    [Fact]
    public void Match_WhenNameContainsInput()
        => Assert.True(_sut.TryMatch(MakeItem("TagColumns", "columns shown"), "tag"));

    [Fact]
    public void Match_WhenDescriptionContainsInput()
        => Assert.True(_sut.TryMatch(MakeItem("TagColumns", "refresh interval"), "interval"));

    [Fact]
    public void CaseInsensitive_Matches()
        => Assert.True(_sut.TryMatch(MakeItem("TagColumns", "columns"), "TAGCOLUMNS"));

    [Fact]
    public void NoMatch_WhenNeitherContainsInput()
        => Assert.False(_sut.TryMatch(MakeItem("TagColumns", "columns"), "subscription"));

    [Fact]
    public void EmptyInput_ReturnsTrue()
        => Assert.True(_sut.TryMatch(MakeItem("TagColumns", "columns"), ""));
}
