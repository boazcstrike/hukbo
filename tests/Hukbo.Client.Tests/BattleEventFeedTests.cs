using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
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

    /// <summary>
    /// T8: BattleSimulation now hands <c>LastEvents</c> callers a view over a
    /// buffer it owns and reuses -- see the double-buffer field comment above
    /// <c>_eventBufferA</c> in BattleSimulation.cs and the lifetime remarks on
    /// the <c>LastEvents</c> property itself. This test proves
    /// <see cref="BattleEventFeed.Ingest"/> observes each tick's events
    /// within the call rather than deferring to the parameter reference, by
    /// building a <c>List&lt;BattleEvent&gt;</c> the exact way the simulation
    /// does -- one shared buffer, cleared and refilled between ticks -- and
    /// ingesting it twice with a mutation in between.
    /// </summary>
    /// <remarks>
    /// A naive "retain and compare" <c>Ingest</c> that stored the
    /// <c>IReadOnlyList&lt;BattleEvent&gt;</c> parameter itself (or deferred
    /// reading it past the call, instead of copying each event's value out
    /// immediately) would fail this test: once <c>sharedBuffer</c> is cleared
    /// and refilled for "tick 2", any stored reference to it would silently
    /// stop reflecting tick 1's two events, and <c>feed.Entries</c> would
    /// come back missing them (or duplicating tick 2's) instead of holding
    /// all three in order. The real implementation passes because
    /// <c>Ingest</c> copies each <c>BattleEvent</c> value out of the
    /// parameter into its own list before returning, so it is unaffected by
    /// whatever the caller -- or the simulation -- does to that buffer next.
    /// </remarks>
    [Fact]
    public void Ingest_CopiesEventValuesRatherThanRetainingTheSourceBuffer()
    {
        var feed = new BattleEventFeed(10);
        var sharedBuffer = new List<BattleEvent> { CreateEvent(1, 1), CreateEvent(2, 1) };

        feed.Ingest(sharedBuffer);

        // Mimic BattleSimulation.AdvanceOneTick reusing its own backing
        // buffer for a later tick: clear it, then write fresh data into the
        // very same list instance.
        sharedBuffer.Clear();
        sharedBuffer.Add(CreateEvent(3, 2));

        feed.Ingest(sharedBuffer);

        Assert.Equal([1L, 2L, 3L], feed.Entries.Select(entry => entry.Sequence));
    }

    [Fact]
    public void Filters_CombineKindFactionActorAndTextWithAndSemantics()
    {
        var feed = new BattleEventFeed(10);
        feed.Ingest(
        [
            CreateEvent(
                1,
                1,
                BattleEventKind.Move,
                sourceEntityId: 7,
                factionId: 0),
            CreateEvent(
                2,
                2,
                BattleEventKind.Attack,
                sourceEntityId: 7,
                targetEntityId: 9,
                value: 3,
                factionId: 0),
            CreateEvent(
                3,
                3,
                BattleEventKind.Attack,
                sourceEntityId: 8,
                targetEntityId: 9,
                value: 3,
                factionId: 1),
        ]);

        feed.SetFilters(
            BattleEventKind.Attack,
            factionId: 0,
            actorId: 7,
            textQuery: "HIT #9");

        Assert.Equal(
            [2L],
            feed.FilteredEntries.Select(entry => entry.Sequence));
        Assert.True(feed.HasActiveFilters);
        Assert.Equal(3, feed.Entries.Count);
    }

    [Fact]
    public void TextFilter_PreservesTrailingSpaceForMultiWordInput()
    {
        var feed = new BattleEventFeed(10);
        feed.Ingest(
        [
            CreateEvent(
                1,
                1,
                BattleEventKind.Attack,
                sourceEntityId: 7,
                targetEntityId: 9,
                value: 3,
                factionId: 0),
        ]);

        feed.SetTextFilter("blue ");

        Assert.Equal("blue ", feed.TextFilter);
        Assert.Equal([1L], feed.FilteredEntries.Select(entry => entry.Sequence));

        feed.SetTextFilter(feed.TextFilter + "#7");

        Assert.Equal("blue #7", feed.TextFilter);
        Assert.Equal([1L], feed.FilteredEntries.Select(entry => entry.Sequence));

        feed.SetTextFilter(" ");

        Assert.Equal(string.Empty, feed.TextFilter);
        Assert.False(feed.HasActiveFilters);
    }

    [Fact]
    public void ClearFilters_RestoresAllEntriesInSequenceOrder()
    {
        var feed = CreatePopulatedFeed(capacity: 10, eventCount: 4);
        feed.SetFilters(
            BattleEventKind.Death,
            factionId: null,
            actorId: null,
            textQuery: "missing");

        Assert.Empty(feed.FilteredEntries);

        feed.ClearFilters();

        Assert.False(feed.HasActiveFilters);
        Assert.Equal(
            [1L, 2L, 3L, 4L],
            feed.FilteredEntries.Select(entry => entry.Sequence));
        Assert.Equal(4, feed.Entries.Count);
    }

    [Fact]
    public void Selection_NavigatesWithinFilteredOrderAndClamps()
    {
        var feed = new BattleEventFeed(10);
        feed.Ingest(
        [
            CreateEvent(1, 1, BattleEventKind.Move),
            CreateEvent(2, 2, BattleEventKind.Attack),
            CreateEvent(3, 3, BattleEventKind.Move),
            CreateEvent(4, 4, BattleEventKind.Move),
        ]);
        feed.SetKindFilter(BattleEventKind.Move);

        Assert.True(feed.Select(3, visibleRowCount: 2));
        Assert.Equal(3, feed.SelectedSequence);

        feed.MoveSelection(-1, visibleRowCount: 2);
        Assert.Equal(1, feed.SelectedSequence);
        feed.MoveSelection(-1, visibleRowCount: 2);
        Assert.Equal(1, feed.SelectedSequence);

        feed.SelectLast(visibleRowCount: 2);
        Assert.Equal(4, feed.SelectedSequence);
        feed.MoveSelection(1, visibleRowCount: 2);
        Assert.Equal(4, feed.SelectedSequence);

        feed.SelectFirst(visibleRowCount: 2);
        Assert.Equal(1, feed.SelectedSequence);
    }

    [Fact]
    public void Ingest_WhileInspectingPreservesSelectionAndScrollPosition()
    {
        var feed = CreatePopulatedFeed(capacity: 10, eventCount: 5);
        feed.Scroll(rowDelta: -2, visibleRowCount: 2);
        feed.Select(2, visibleRowCount: 2);
        var scrollStart = feed.GetScrollStart(visibleRowCount: 2);

        feed.Ingest([CreateEvent(6, 4), CreateEvent(7, 4)]);

        Assert.False(feed.IsPinnedToBottom);
        Assert.Equal(2, feed.SelectedSequence);
        Assert.Equal(scrollStart, feed.GetScrollStart(visibleRowCount: 2));
        Assert.Equal(2, feed.NewEventCount);
    }

    [Fact]
    public void ReturnToLatest_SelectsNewestMatchingEntryAndPinsList()
    {
        var feed = CreatePopulatedFeed(capacity: 10, eventCount: 5);
        feed.SetActorFilter(3);
        feed.Scroll(rowDelta: -1, visibleRowCount: 1);

        feed.ReturnToLatest(visibleRowCount: 1);

        Assert.True(feed.IsPinnedToBottom);
        Assert.Equal(3, feed.SelectedSequence);
        Assert.Equal(0, feed.NewEventCount);
        Assert.Equal(0, feed.GetScrollStart(visibleRowCount: 1));
    }

    [Fact]
    public void FilteringOutOrEvictingSelection_ClearsItSafely()
    {
        var feed = CreatePopulatedFeed(capacity: 3, eventCount: 3);
        feed.Select(2, visibleRowCount: 2);

        feed.SetActorFilter(3);
        Assert.Null(feed.SelectedSequence);

        feed.ClearFilters();
        feed.Select(1, visibleRowCount: 2);
        feed.Ingest([CreateEvent(4, 3)]);

        Assert.Null(feed.SelectedSequence);
    }

    [Fact]
    public void Navigation_WithNoMatches_IsANoOp()
    {
        var feed = CreatePopulatedFeed(capacity: 10, eventCount: 3);
        feed.SetTextFilter("no such event");

        feed.MoveSelection(1, visibleRowCount: 2);
        feed.SelectFirst(visibleRowCount: 2);
        feed.SelectLast(visibleRowCount: 2);
        feed.ReturnToLatest(visibleRowCount: 2);

        Assert.Null(feed.SelectedSequence);
        Assert.Empty(feed.GetVisibleEntries(visibleRowCount: 2).ToArray());
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
    public void Ingest_EvictedAnchorSubtractsMatchingEvictedEntries()
    {
        var feed = new BattleEventFeed(capacity: 8);
        feed.Ingest(
            Enumerable.Range(1, 8)
                .Select(index => CreateEvent(
                    index,
                    index,
                    index % 2 == 1
                        ? BattleEventKind.Move
                        : BattleEventKind.Attack))
                .ToArray());
        feed.SetKindFilter(BattleEventKind.Move);
        feed.Scroll(rowDelta: -1, visibleRowCount: 1);
        Assert.Equal(
            [5L],
            feed.GetVisibleEntries(visibleRowCount: 1)
                .ToArray()
                .Select(entry => entry.Sequence));

        feed.Ingest(
            Enumerable.Range(9, 6)
                .Select(index => CreateEvent(
                    index,
                    index,
                    index % 2 == 1
                        ? BattleEventKind.Move
                        : BattleEventKind.Attack))
                .ToArray());

        Assert.False(feed.IsPinnedToBottom);
        Assert.Equal(0, feed.GetScrollStart(visibleRowCount: 1));
        Assert.Equal(
            [7L],
            feed.GetVisibleEntries(visibleRowCount: 1)
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

    /// <summary>
    /// GUARD. The text filter formats every candidate event, and the formatter
    /// now dereferences the resolution as well as the weapon and the hit
    /// location. A record struct always exposes an implicit parameterless
    /// constructor, so <c>default(BattleEvent)</c> bypasses the factory
    /// validation that guarantees those three; the feed's defence-in-depth
    /// guard is what stops that reaching the formatter.
    /// </summary>
    [Fact]
    public void MatchesFilters_DoesNotThrowOnADefaultAttackEvent()
    {
        var feed = new BattleEventFeed(capacity: 8);
        feed.SetTextFilter("shoulder");

        feed.Ingest(new BattleEvent[3]);
        feed.Ingest([CreateEvent(9, 9, BattleEventKind.Attack)]);

        Assert.NotEmpty(feed.Entries);
        Assert.Empty(feed.FilteredEntries);

        feed.SetTextFilter("chest");

        Assert.Single(feed.FilteredEntries);
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

    private static BattleEvent CreateEvent(
        long sequence,
        long tick,
        BattleEventKind kind = BattleEventKind.Move,
        ulong? sourceEntityId = null,
        ulong? targetEntityId = null,
        int value = 1,
        int? factionId = null)
    {
        var source = sourceEntityId ?? (ulong)sequence;
        if (kind == BattleEventKind.Attack)
        {
            return BattleEvent.Attack(
                sequence,
                tick,
                source,
                targetEntityId ?? checked(source + 1),
                value,
                factionId ?? 0,
                WeaponId.Kampilan,
                ShieldId.None,
                BodyPart.Chest);
        }

        return BattleEvent.NonAttack(
            sequence,
            tick,
            kind,
            source,
            targetEntityId,
            value,
            factionId);
    }
}
