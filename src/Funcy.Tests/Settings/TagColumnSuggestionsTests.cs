using Funcy.Console.Settings;
using Xunit;

namespace Funcy.Tests.Settings;

public class TagColumnSuggestionsTests
{
    [Fact]
    public void Distinct_Trims_DropsBlanks_DedupesCaseInsensitively_AndSorts()
    {
        var result = TagColumnSuggestions.Distinct(
            ["Team", " System ", "team", "", "  ", "CostCenter", "system", null]);

        // "team"/"system" collapse into the first-seen casing; blanks/null are dropped; sorted.
        Assert.Equal(["CostCenter", "System", "Team"], result);
    }

    [Fact]
    public void Distinct_Empty_ReturnsEmpty()
    {
        Assert.Empty(TagColumnSuggestions.Distinct([]));
    }

    [Fact]
    public void FormatHint_ListsAvailableKeys()
    {
        Assert.Equal("(available: CostCenter, Env, System)",
            TagColumnSuggestions.FormatHint(["CostCenter", "Env", "System"]));
    }

    [Fact]
    public void FormatHint_TruncatesLongLists()
    {
        var many = Enumerable.Range(1, 12).Select(i => $"Tag{i}").ToList();

        var hint = TagColumnSuggestions.FormatHint(many, maxKeys: 3);

        Assert.Equal("(available: Tag1, Tag2, Tag3, …)", hint);
    }

    [Fact]
    public void FormatHint_Empty_ReturnsEmptyString()
    {
        Assert.Equal("", TagColumnSuggestions.FormatHint([]));
    }
}
