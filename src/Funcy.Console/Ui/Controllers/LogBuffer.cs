using Funcy.Core.Model;

namespace Funcy.Console.Ui.Controllers;

// In-memory store of log entries, newest first. Dedupes by Key across overlapping polls and caps
// the retained set so a long-running view cannot grow without bound. Not thread safe by itself;
// the controller confines all access to its poll loop.
public sealed class LogBuffer(int capacity)
{
    private readonly Dictionary<string, LogEntryDetails> _entries = new(StringComparer.Ordinal);

    public DateTimeOffset? MaxTimestamp { get; private set; }

    // Merges new entries, ignoring duplicates. Returns true if anything new was added.
    public bool Merge(IEnumerable<LogEntryDetails> incoming)
    {
        var added = false;
        foreach (var entry in incoming)
        {
            if (_entries.TryAdd(entry.Key, entry))
            {
                added = true;
                if (MaxTimestamp is null || entry.Timestamp > MaxTimestamp)
                {
                    MaxTimestamp = entry.Timestamp;
                }
            }
        }

        if (added)
        {
            Cap();
        }

        return added;
    }

    public int Count => _entries.Count;

    // Drops every entry so the next poll refetches the whole window from scratch (used when the
    // lookback window changes and the retained set no longer matches the requested range).
    public void Clear()
    {
        _entries.Clear();
        MaxTimestamp = null;
    }

    // Newest-first snapshot filtered by type. LogEntryDetails.CompareTo already orders descending.
    public IReadOnlyList<LogEntryDetails> Snapshot(LogTypeFilter filter)
    {
        var items = _entries.Values.Where(e => filter.Includes(e.ItemType)).ToList();
        items.Sort();
        return items;
    }

    private void Cap()
    {
        if (_entries.Count <= capacity)
        {
            return;
        }

        var keep = _entries.Values.OrderBy(e => e).Take(capacity).ToList(); // newest first
        _entries.Clear();
        foreach (var entry in keep)
        {
            _entries[entry.Key] = entry;
        }
    }
}
