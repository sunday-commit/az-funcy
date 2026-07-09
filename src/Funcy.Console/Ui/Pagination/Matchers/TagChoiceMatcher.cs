using Funcy.Console.Settings;

namespace Funcy.Console.Ui.Pagination.Matchers;

public class TagChoiceMatcher : ISearchMatcher<TagChoice>
{
    public bool TryMatch(TagChoice item, string input)
        => item.Name.Contains(input, StringComparison.OrdinalIgnoreCase);
}
