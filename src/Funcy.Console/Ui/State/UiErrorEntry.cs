using System.Globalization;
using Funcy.Core.Model;

namespace Funcy.Console.Ui.State;

// A single surfaced error. Sequence is a monotonic id assigned by the log; it keeps the panel
// key unique (several errors can share a timestamp/scope/message) and gives CompareTo a stable
// tie-break so ordering is deterministic.
public sealed record UiErrorEntry(long Sequence, DateTime TimestampUtc, string Scope, string Message)
    : IComparable<UiErrorEntry>, IHasKey
{
    public string Key => Sequence.ToString(CultureInfo.InvariantCulture);

    // Newest first: a later timestamp (then a higher sequence) sorts ahead.
    public int CompareTo(UiErrorEntry? other)
    {
        if (other is null)
        {
            return -1;
        }

        var byTime = other.TimestampUtc.CompareTo(TimestampUtc);
        return byTime != 0 ? byTime : other.Sequence.CompareTo(Sequence);
    }
}
