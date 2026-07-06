namespace Funcy.Console.Settings;

// Pure helpers for the TagColumns edit suggestions: normalizing the raw tag keys pulled from
// the cache into a distinct, sorted list and formatting the visible hint. Kept free of any I/O
// so the distinct/sort/dedup and truncation logic is directly testable.
public static class TagColumnSuggestions
{
    // Trims, drops blanks, dedupes case-insensitively (first-seen casing wins) and sorts.
    public static IReadOnlyList<string> Distinct(IEnumerable<string?> keys)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in keys)
        {
            var key = raw?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (seen.Add(key))
            {
                result.Add(key);
            }
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    // Builds the "(available: a, b, c)" hint, truncating an overly long list with an ellipsis.
    public static string FormatHint(IReadOnlyList<string> available, int maxKeys = 8)
    {
        if (available.Count == 0)
        {
            return "";
        }

        var shown = available.Take(maxKeys);
        var joined = string.Join(", ", shown);
        if (available.Count > maxKeys)
        {
            joined += ", …";
        }

        return $"(available: {joined})";
    }
}
