using Funcy.Console.Settings;

namespace Funcy.Console.Ui.Pagination.Matchers;

public class SettingMatcher : ISearchMatcher<SettingItemDetails>
{
    public bool TryMatch(SettingItemDetails item, string input)
    {
        return item.Name.Contains(input, StringComparison.OrdinalIgnoreCase)
               || item.Description.Contains(input, StringComparison.OrdinalIgnoreCase);
    }
}
