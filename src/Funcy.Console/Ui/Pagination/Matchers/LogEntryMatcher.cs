using Funcy.Core.Model;

namespace Funcy.Console.Ui.Pagination.Matchers;

// Free-text filter over a log entry: matches the message body, the item type and the severity
// so a user can narrow down to e.g. an order id, "exception" or "warning".
public class LogEntryMatcher : ISearchMatcher<LogEntryDetails>
{
    public bool TryMatch(LogEntryDetails entry, string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        return entry.Message.Contains(input, StringComparison.OrdinalIgnoreCase)
               || entry.ItemType.ToString().Contains(input, StringComparison.OrdinalIgnoreCase)
               || (entry.Severity?.Contains(input, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
