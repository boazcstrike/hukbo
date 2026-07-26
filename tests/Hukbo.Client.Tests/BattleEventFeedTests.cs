using Hukbo.Client.Presentation;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Tests;

public sealed class BattleEventFeedTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleEventFeed(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleEventFeed(-1));
    }

    [Fact]
    public void Ingest_PreservesSequenceOrder()
    {
        var feed = new BattleEventFeed(5);

        feed.Ingest([CreateEvent(1, 1), CreateEvent(2, 1), CreateEvent(3, 2)]);

        Assert.Equal([1L, 2L, 3L], feed.Entries.Select(entry => entry.Sequence));
    }

    [Fact]
    public void Ingest_DeduplicatesRepeatedLatestTick()
    {
        var feed = new BattleEventFeed(5);
        BattleEvent[] latest = [CreateEvent(1, 1), CreateEvent(2, 1)];

        feed.Ingest(latest);
        feed.Ingest(latest);

        Assert.Equal([1L, 2L], feed.Entries.Select(entry => entry.Sequence));
    }

    [Fact]
    public void Ingest_EvictsOldestBeyondCapacity()
    {
        var feed = new BattleEventFeed(3);

        feed.Ingest(
        [
            CreateEvent(1, 1),
            CreateEvent(2, 1),
            CreateEvent(3, 2),
            CreateEvent(4, 2),
            CreateEvent(5, 3),
        ]);

        Assert.Equal([3L, 4L, 5L], feed.Entries.Select(entry => entry.Sequence));
    }

    [Fact]
    public void Ingest_MultipleTicksRetainsEveryPublishedEvent()
    {
        var feed = new BattleEventFeed(10);

        feed.Ingest([CreateEvent(1, 1), CreateEvent(2, 1)]);
        feed.Ingest([CreateEvent(3, 2)]);
        feed.Ingest([CreateEvent(4, 3), CreateEvent(5, 3)]);

        Assert.Equal([1L, 2L, 3L, 4L, 5L], feed.Entries.Select(entry => entry.Sequence));
    }

    [Fact]
    public void Scroll_ClampsAtOldestAndNewest()
    {
        var feed = CreatePopulatedFeed(capacity: 10, eventCount: 6);

        feed.Scroll(rowDelta: -100, visibleRowCount: 3);
        Assert.Equal(0, feed.GetScrollStart(visibleRowCount: 3));
        Assert.False(feed.IsPinnedToBottom);

        feed.Scroll(rowDelta: 100, visibleRowCount: 3);
        Assert.Equal(3, feed.GetScrollStart(visibleRowCount: 3));
        Assert.True(feed.IsPinnedToBottom);
        Assert.Equal(
            [4L, 5L, 6L],
            feed.GetVisibleEntries(visibleRowCount: 3)
                .ToArray()
                .Select(entry => entry.Sequence));
    }

    [Fact]
    public void Ingest_StaysAtBottomWhenPinned()
    {
        var feed = CreatePopulatedFeed(capacity: 10, eventCount: 4);
        Assert.Equal(2, feed.GetScrollStart(visibleRowCount: 2));

        feed.Ingest([CreateEvent(5, 3)]);

        Assert.True(feed.IsPinnedToBottom);
        Assert.Equal(3, feed.GetScrollStart(visibleRowCount: 2));
        Assert.Equal(
            [4L, 5L],
            feed.GetVisibleEntries(visibleRowCount: 2)
                .ToArray()
                .Select(entry => entry.Sequence));
    }

    [Fact]
    public void Ingest_DoesNotStealPositionWhenScrolledUp()
    {
        var feed = CreatePopulatedFeed(capacity: 10, eventCount: 4);
        feed.Scroll(rowDelta: -1, visibleRowCount: 2);
        Assert.Equal(1, feed.GetScrollStart(visibleRowCount: 2));

        feed.Ingest([CreateEvent(5, 3)]);

        Assert.False(feed.IsPinnedToBottom);
        Assert.Equal(1, feed.GetScrollStart(visibleRowCount: 2));
        Assert.Equal(
            [2L, 3L],
            feed.GetVisibleEntries(visibleRowCount: 2)
                .ToArray()
                .Select(entry => entry.Sequence));
    }

    [Fact]
    public void Clear_ResetsHistoryAndSequence()
    {
        var feed = CreatePopulatedFeed(capacity: 3, eventCount: 3);
        feed.Scroll(rowDelta: -1, visibleRowCount: 1);

        feed.Clear();
        feed.Ingest([CreateEvent(1, 1)]);

        Assert.True(feed.IsPinnedToBottom);
        Assert.Equal(0, feed.GetScrollStart(visibleRowCount: 1));
        Assert.Equal(1L, Assert.Single(feed.Entries).Sequence);
    }

    private static BattleEventFeed CreatePopulatedFeed(int capacity, int eventCount)
    {
        var feed = new BattleEventFeed(capacity);
        feed.Ingest(
            Enumerable.Range(1, eventCount)
                .Select(index => CreateEvent(index, (index + 1) / 2))
                .ToArray());
        return feed;
    }

    private static BattleEvent CreateEvent(long sequence, long tick) =>
        new(
            sequence,
            tick,
            BattleEventKind.Move,
            SourceEntityId: (ulong)sequence,
            TargetEntityId: null,
            Value: 1,
            FactionId: null);
}
