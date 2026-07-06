using Funcy.Core.Model;

namespace Funcy.Console.Ui.Pagination.Matchers;

public class AppSettingMatcher : ISearchMatcher<AppSettingDetails>
{
    // Name only — values may be masked, and matching on hidden values would leak that a
    // secret contains a given substring.
    public bool TryMatch(AppSettingDetails item, string input)
        => item.Name.Contains(input, StringComparison.OrdinalIgnoreCase);
}
