using Funcy.Console.Ui.Controllers;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Controllers;

public class LogBufferTests
{
    private static LogEntryDetails Entry(string key, DateTimeOffset ts, LogItemType type = LogItemType.Trace) => new()
    {
        Timestamp = ts,
        ItemType = type,
        Message = "m-" + key,
        Key = key,
    };

    private static readonly DateTimeOffset Base = new(2026, 7, 6, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Merge_AddsNewEntries_AndReportsAdded()
    {
        var buffer = new LogBuffer(100);

        var added = buffer.Merge([Entry("a", Base), Entry("b", Base.AddSeconds(1))]);

        Assert.True(added);
        Assert.Equal(2, buffer.Count);
    }

    [Fact]
    public void Merge_DedupesByKey_AcrossOverlappingPolls()
    {
        var buffer = new LogBuffer(100);
        buffer.Merge([Entry("a", Base), Entry("b", Base.AddSeconds(1))]);

        var added = buffer.Merge([Entry("b", Base.AddSeconds(1)), Entry("c", Base.AddSeconds(2))]);

        Assert.True(added);              // "c" is new
        Assert.Equal(3, buffer.Count);   // "b" not duplicated
    }

    [Fact]
    public void Merge_WhenOnlyDuplicates_ReturnsFalse()
    {
        var buffer = new LogBuffer(100);
        buffer.Merge([Entry("a", Base)]);

        Assert.False(buffer.Merge([Entry("a", Base)]));
    }

    [Fact]
    public void MaxTimestamp_TracksNewest()
    {
        var buffer = new LogBuffer(100);

        buffer.Merge([Entry("a", Base), Entry("c", Base.AddSeconds(5)), Entry("b", Base.AddSeconds(2))]);

        Assert.Equal(Base.AddSeconds(5), buffer.MaxTimestamp);
    }

    [Fact]
    public void Cap_DropsOldest_KeepingNewest()
    {
        var buffer = new LogBuffer(2);

        buffer.Merge([Entry("a", Base), Entry("b", Base.AddSeconds(1)), Entry("c", Base.AddSeconds(2))]);

        Assert.Equal(2, buffer.Count);
        var snapshot = buffer.Snapshot(LogTypeFilter.All);
        Assert.Equal("c", snapshot[0].Key); // newest first
        Assert.Equal("b", snapshot[1].Key);
        Assert.DoesNotContain(snapshot, e => e.Key == "a");
    }

    [Fact]
    public void Clear_EmptiesBuffer_AndResetsMaxTimestamp()
    {
        var buffer = new LogBuffer(100);
        buffer.Merge([Entry("a", Base), Entry("b", Base.AddSeconds(1))]);

        buffer.Clear();

        Assert.Equal(0, buffer.Count);
        Assert.Null(buffer.MaxTimestamp);
    }

    [Fact]
    public void Snapshot_FiltersByType_AndOrdersNewestFirst()
    {
        var buffer = new LogBuffer(100);
        buffer.Merge([
            Entry("t1", Base, LogItemType.Trace),
            Entry("e1", Base.AddSeconds(1), LogItemType.Exception),
            Entry("t2", Base.AddSeconds(2), LogItemType.Trace),
        ]);

        var exceptions = buffer.Snapshot(LogTypeFilter.Exceptions);
        Assert.Single(exceptions);
        Assert.Equal("e1", exceptions[0].Key);

        var traces = buffer.Snapshot(LogTypeFilter.Traces);
        Assert.Equal(["t2", "t1"], traces.Select(e => e.Key));
    }
}
