using Funcy.Console.Ui.State;

namespace Funcy.Console.Ui.Pagination.Matchers;

public class UiErrorMatcher : ISearchMatcher<UiErrorEntry>
{
    public bool TryMatch(UiErrorEntry entry, string input)
        => entry.Scope.Contains(input, StringComparison.OrdinalIgnoreCase)
           || entry.Message.Contains(input, StringComparison.OrdinalIgnoreCase);
}
