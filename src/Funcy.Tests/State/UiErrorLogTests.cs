using Funcy.Console.Ui.State;
using Xunit;

namespace Funcy.Tests.State;

public class UiErrorLogTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void Report_BeyondCapacity_KeepsOnlyNewestEntries()
    {
        var log = new UiErrorLog();

        // Capacity is 200; push more and verify the oldest are dropped.
        for (var i = 0; i < 250; i++)
        {
            log.Report("Sync", $"error-{i}");
        }

        var snapshot = log.GetSnapshot();

        Assert.Equal(200, snapshot.Count);
        // Newest first, and the first 50 (error-0..error-49) must have been evicted.
        Assert.Equal("error-249", snapshot[0].Message);
        Assert.Equal("error-50", snapshot[^1].Message);
    }

    [Fact]
    public void GetSnapshot_ReturnsEntriesNewestFirst()
    {
        var log = new UiErrorLog();

        log.Report("A", "first");
        log.Report("B", "second");
        log.Report("C", "third");

        var snapshot = log.GetSnapshot();

        Assert.Equal(["third", "second", "first"], snapshot.Select(e => e.Message));
    }

    [Fact]
    public async Task Report_RaisesChangedEvent()
    {
        var log = new UiErrorLog();
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        log.Changed += () => fired.TrySetResult();

        log.Report("Sync", "boom");

        await fired.Task.WaitAsync(Timeout);
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public async Task Clear_WhenNonEmpty_EmptiesAndRaisesChanged()
    {
        var log = new UiErrorLog();
        log.Report("Sync", "boom");
        await WaitForCount(log, 1);

        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        log.Changed += () => fired.TrySetResult();

        log.Clear();

        await fired.Task.WaitAsync(Timeout);
        Assert.Equal(0, log.Count);
        Assert.Empty(log.GetSnapshot());
    }

    [Fact]
    public void Clear_WhenEmpty_DoesNotRaiseChanged()
    {
        var log = new UiErrorLog();
        var fired = false;
        log.Changed += () => fired = true;

        log.Clear();

        Assert.False(fired);
    }

    [Fact]
    public async Task Report_FromManyThreads_KeepsEveryEntry()
    {
        var log = new UiErrorLog();
        const int perThread = 20;
        const int threads = 8; // 160 total, below capacity so none are dropped

        var tasks = Enumerable.Range(0, threads).Select(t => Task.Run(() =>
        {
            for (var i = 0; i < perThread; i++)
            {
                log.Report($"t{t}", $"msg-{i}");
            }
        }));

        await Task.WhenAll(tasks).WaitAsync(Timeout);

        Assert.Equal(threads * perThread, log.Count);
        // Sequences are unique -> keys are unique even under concurrent adds.
        var keys = log.GetSnapshot().Select(e => e.Key).ToHashSet();
        Assert.Equal(threads * perThread, keys.Count);
    }

    [Fact]
    public void CompareTo_OrdersNewestFirst()
    {
        var older = new UiErrorEntry(1, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), "A", "old");
        var newer = new UiErrorEntry(2, new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc), "A", "new");

        Assert.True(newer.CompareTo(older) < 0);
        Assert.True(older.CompareTo(newer) > 0);

        var list = new List<UiErrorEntry> { older, newer };
        list.Sort();
        Assert.Equal(newer, list[0]);
    }

    [Fact]
    public void CompareTo_SameTimestamp_HigherSequenceFirst()
    {
        var timestamp = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var first = new UiErrorEntry(1, timestamp, "A", "first");
        var second = new UiErrorEntry(2, timestamp, "A", "second");

        Assert.True(second.CompareTo(first) < 0);
    }

    private static async Task WaitForCount(UiErrorLog log, int expected)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (log.Count != expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }

        Assert.Equal(expected, log.Count);
    }
}
