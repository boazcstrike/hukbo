using System.Collections.Immutable;
using Sandata.Core.Sensing;
using Sandata.Core.Simulation;

namespace Sandata.Core.Tests;

/// <summary>
/// Proves the two contact-tier range boundaries, ghost persistence with
/// increasing age, deterministic ordering regardless of input order, and the
/// generalised remembered-not-live rule via a door scenario. Mirrors
/// <c>WeaponLoweredRulesTests</c>' exact-threshold /
/// one-world-unit-beyond pairing style.
/// </summary>
public sealed class ContactMemoryTests
{
    // --- ClassifyTier boundaries ---------------------------------------

    [Fact]
    public void ExactlyAtIdentifyRange_IsIdentified()
    {
        var rangeSquared = (long)ContactMemory.IdentifyRangeWu * ContactMemory.IdentifyRangeWu;
        Assert.Equal(ContactTier.Identified, ContactMemory.ClassifyTier(rangeSquared));
    }

    [Fact]
    public void OneWorldUnitBeyondIdentifyRange_IsQuestionMark()
    {
        var oneBeyond = ContactMemory.IdentifyRangeWu + 1;
        var rangeSquared = (long)oneBeyond * oneBeyond;
        Assert.Equal(ContactTier.QuestionMark, ContactMemory.ClassifyTier(rangeSquared));
    }

    [Fact]
    public void ExactlyAtDetectRange_IsQuestionMark()
    {
        var rangeSquared = (long)ContactMemory.DetectRangeWu * ContactMemory.DetectRangeWu;
        Assert.Equal(ContactTier.QuestionMark, ContactMemory.ClassifyTier(rangeSquared));
    }

    [Fact]
    public void OneWorldUnitBeyondDetectRange_IsUnknown()
    {
        var oneBeyond = ContactMemory.DetectRangeWu + 1;
        var rangeSquared = (long)oneBeyond * oneBeyond;
        Assert.Equal(ContactTier.Unknown, ContactMemory.ClassifyTier(rangeSquared));
    }

    [Fact]
    public void ZeroRange_IsIdentified()
    {
        Assert.Equal(ContactTier.Identified, ContactMemory.ClassifyTier(0));
    }

    // --- Update: new contacts -------------------------------------------

    [Fact]
    public void Update_NewIdentifiedObservation_CreatesEntry()
    {
        var observations = new ContactObservation[]
        {
            new(EnemyEntityId: 7, HasLineOfSightThisTick: true, RangeSquaredWu: 0, CurrentCellIndex: 12),
        };

        var result = ContactMemory.Update(
            ImmutableArray<ContactMemoryEntry>.Empty, observations, currentTick: 100);

        var entry = Assert.Single(result);
        Assert.Equal(7ul, entry.EnemyEntityId);
        Assert.Equal(12, entry.LastKnownCellIndex);
        Assert.Equal((int)ContactTier.Identified, entry.ContactTier);
        Assert.Equal(100, entry.LastSeenTick);
    }

    [Fact]
    public void Update_NewObservationBeyondDetectRange_CreatesNoEntry()
    {
        var farRangeSquared = (long)(ContactMemory.DetectRangeWu + 1) * (ContactMemory.DetectRangeWu + 1);
        var observations = new ContactObservation[]
        {
            new(EnemyEntityId: 7, HasLineOfSightThisTick: true, RangeSquaredWu: farRangeSquared, CurrentCellIndex: 12),
        };

        var result = ContactMemory.Update(
            ImmutableArray<ContactMemoryEntry>.Empty, observations, currentTick: 100);

        Assert.Empty(result);
    }

    [Fact]
    public void Update_ObservationWithoutLineOfSight_CreatesNoEntry()
    {
        var observations = new ContactObservation[]
        {
            new(EnemyEntityId: 7, HasLineOfSightThisTick: false, RangeSquaredWu: 0, CurrentCellIndex: 12),
        };

        var result = ContactMemory.Update(
            ImmutableArray<ContactMemoryEntry>.Empty, observations, currentTick: 100);

        Assert.Empty(result);
    }

    // --- Update: ghosts persist with increasing age ----------------------

    [Fact]
    public void Update_EnemyNotObservedThisTick_GhostPersistsUnchanged()
    {
        var existing = ImmutableArray.Create(
            new ContactMemoryEntry(EnemyEntityId: 3, LastKnownCellIndex: 50, ContactTier: (int)ContactTier.Identified, LastSeenTick: 10));

        // No observation at all for entity 3 this tick.
        var result = ContactMemory.Update(existing, ReadOnlySpan<ContactObservation>.Empty, currentTick: 20);

        var ghost = Assert.Single(result);
        Assert.Equal(3ul, ghost.EnemyEntityId);
        Assert.Equal(50, ghost.LastKnownCellIndex);
        Assert.Equal((int)ContactTier.Identified, ghost.ContactTier);
        Assert.Equal(10, ghost.LastSeenTick); // unchanged: this is the ghost's age anchor.

        // Age, computed by the caller, grows every tick the ghost is not
        // re-observed: at tick 20 the ghost is 10 ticks stale.
        Assert.Equal(10, 20 - ghost.LastSeenTick);
    }

    [Fact]
    public void Update_EnemyLostBeyondDetectRange_GhostPersistsUnchanged()
    {
        var existing = ImmutableArray.Create(
            new ContactMemoryEntry(EnemyEntityId: 3, LastKnownCellIndex: 50, ContactTier: (int)ContactTier.QuestionMark, LastSeenTick: 10));

        // Observed this tick, but now far enough away (or occluded) to
        // classify Unknown: the ghost must persist exactly as before, not be
        // overwritten and not be dropped.
        var farRangeSquared = (long)(ContactMemory.DetectRangeWu + 1) * (ContactMemory.DetectRangeWu + 1);
        var observations = new ContactObservation[]
        {
            new(EnemyEntityId: 3, HasLineOfSightThisTick: true, RangeSquaredWu: farRangeSquared, CurrentCellIndex: 999),
        };

        var result = ContactMemory.Update(existing, observations, currentTick: 30);

        var ghost = Assert.Single(result);
        Assert.Equal(50, ghost.LastKnownCellIndex); // not 999: the stale position, not the current one.
        Assert.Equal((int)ContactTier.QuestionMark, ghost.ContactTier);
        Assert.Equal(10, ghost.LastSeenTick);
    }

    // --- Update: re-observation refreshes the entry -----------------------

    [Fact]
    public void Update_EnemyReObservedCloser_RefreshesTierCellAndTick()
    {
        var existing = ImmutableArray.Create(
            new ContactMemoryEntry(EnemyEntityId: 3, LastKnownCellIndex: 50, ContactTier: (int)ContactTier.QuestionMark, LastSeenTick: 10));

        var observations = new ContactObservation[]
        {
            new(EnemyEntityId: 3, HasLineOfSightThisTick: true, RangeSquaredWu: 0, CurrentCellIndex: 60),
        };

        var result = ContactMemory.Update(existing, observations, currentTick: 40);

        var refreshed = Assert.Single(result);
        Assert.Equal(60, refreshed.LastKnownCellIndex);
        Assert.Equal((int)ContactTier.Identified, refreshed.ContactTier);
        Assert.Equal(40, refreshed.LastSeenTick);
    }

    // --- Update: determinism / ordering -----------------------------------

    [Fact]
    public void Update_ResultIsAlwaysSortedAscendingByEnemyEntityId_RegardlessOfInputOrder()
    {
        var existing = ImmutableArray.Create(
            new ContactMemoryEntry(EnemyEntityId: 9, LastKnownCellIndex: 1, ContactTier: (int)ContactTier.Identified, LastSeenTick: 1),
            new ContactMemoryEntry(EnemyEntityId: 2, LastKnownCellIndex: 2, ContactTier: (int)ContactTier.Identified, LastSeenTick: 1));

        // Observations arrive in an order unrelated to id order, and include
        // one brand-new entity (5) that sorts between the two existing ones.
        var observations = new ContactObservation[]
        {
            new(EnemyEntityId: 9, HasLineOfSightThisTick: true, RangeSquaredWu: 0, CurrentCellIndex: 10),
            new(EnemyEntityId: 5, HasLineOfSightThisTick: true, RangeSquaredWu: 0, CurrentCellIndex: 30),
            new(EnemyEntityId: 2, HasLineOfSightThisTick: true, RangeSquaredWu: 0, CurrentCellIndex: 20),
        };

        var result = ContactMemory.Update(existing, observations, currentTick: 5);

        Assert.Equal(3, result.Length);
        Assert.Equal(2ul, result[0].EnemyEntityId);
        Assert.Equal(5ul, result[1].EnemyEntityId);
        Assert.Equal(9ul, result[2].EnemyEntityId);
    }

    [Fact]
    public void Update_DoesNotMutateExistingMemoryArgument()
    {
        var existing = ImmutableArray.Create(
            new ContactMemoryEntry(EnemyEntityId: 3, LastKnownCellIndex: 50, ContactTier: (int)ContactTier.Identified, LastSeenTick: 10));

        var observations = new ContactObservation[]
        {
            new(EnemyEntityId: 3, HasLineOfSightThisTick: true, RangeSquaredWu: 0, CurrentCellIndex: 999),
        };

        var beforeCellIndex = existing[0].LastKnownCellIndex;
        _ = ContactMemory.Update(existing, observations, currentTick: 40);

        Assert.Equal(beforeCellIndex, existing[0].LastKnownCellIndex);
    }

    [Fact]
    public void Update_EmptyMemoryAndNoObservations_ReturnsEmpty()
    {
        var result = ContactMemory.Update(
            ImmutableArray<ContactMemoryEntry>.Empty, ReadOnlySpan<ContactObservation>.Empty, currentTick: 0);

        Assert.Empty(result);
    }

    // --- ObserveOrRemember: a door opened out of sight is not observed
    //     until seen -------------------------------------------------------

    [Fact]
    public void ObserveOrRemember_DoorOpenedOutOfSight_RemembersLastKnownStateNotCurrentState()
    {
        // The operator last saw the door closed. Some other actor opens it
        // while the operator cannot see it — isObservedThisTick is false, so
        // the true current state (open) must not leak through.
        const bool rememberedClosed = false; // false == closed, by this test's own convention.
        const bool trueCurrentStateNowOpen = true;

        var believedState = ContactMemory.ObserveOrRemember(
            rememberedValue: rememberedClosed, observedValue: trueCurrentStateNowOpen, isObservedThisTick: false);

        Assert.False(believedState); // still believes it is closed.
    }

    [Fact]
    public void ObserveOrRemember_DoorSeenAgain_AdoptsTrueCurrentState()
    {
        const bool rememberedClosed = false;
        const bool trueCurrentStateNowOpen = true;

        var believedState = ContactMemory.ObserveOrRemember(
            rememberedValue: rememberedClosed, observedValue: trueCurrentStateNowOpen, isObservedThisTick: true);

        Assert.True(believedState); // now sees it is open.
    }

    // --- Task 88: the reused merge buffer -------------------------------

    /// <summary>
    /// Task 88: stage 5 hands one merge buffer to every operator's update in
    /// turn, so a large operator's result must not bleed into the small
    /// operator that follows it. Asserted on the <i>returned arrays</i>,
    /// against what the allocating overload produces for the identical
    /// inputs, rather than on the buffer's contents — a result built from a
    /// stale tail is still a well-formed array, and only comparing the
    /// contents catches it.
    /// </summary>
    [Fact]
    public void OneMergeBufferReusedAcrossOperators_GivesTheSameResultsAsAFreshBufferEachTime()
    {
        var crowdedMemory = ImmutableArray.Create(
            new ContactMemoryEntry(11UL, 111, (int)ContactTier.Identified, 40L),
            new ContactMemoryEntry(12UL, 112, (int)ContactTier.Identified, 41L),
            new ContactMemoryEntry(13UL, 113, (int)ContactTier.QuestionMark, 42L),
            new ContactMemoryEntry(14UL, 114, (int)ContactTier.QuestionMark, 43L));
        var crowdedObservations = new ContactObservation[]
        {
            new(EnemyEntityId: 12, HasLineOfSightThisTick: true, RangeSquaredWu: 0, CurrentCellIndex: 212),
            new(EnemyEntityId: 15, HasLineOfSightThisTick: true, RangeSquaredWu: 0, CurrentCellIndex: 215),
            new(EnemyEntityId: 16, HasLineOfSightThisTick: true, RangeSquaredWu: 0, CurrentCellIndex: 216),
        };

        var sparseMemory = ImmutableArray.Create(
            new ContactMemoryEntry(99UL, 199, (int)ContactTier.QuestionMark, 5L));
        var sparseObservations = new ContactObservation[]
        {
            new(EnemyEntityId: 99, HasLineOfSightThisTick: true, RangeSquaredWu: 0, CurrentCellIndex: 299),
        };

        // Sized for the crowded call, then reused for the sparse one, which
        // fills three of its seven slots. The other four hold the crowded
        // call's entries at that moment, which is exactly the stale tail a
        // wrong read length would return.
        var sharedBuffer = new ContactMemoryEntry[crowdedMemory.Length + crowdedObservations.Length];

        var crowdedExpected = ContactMemory.Update(crowdedMemory, crowdedObservations, currentTick: 100);
        var crowdedActual = ContactMemory.Update(crowdedMemory, crowdedObservations, currentTick: 100, sharedBuffer);
        Assert.Equal(crowdedExpected.ToArray(), crowdedActual.ToArray());

        var sparseExpected = ContactMemory.Update(sparseMemory, sparseObservations, currentTick: 101);
        var sparseActual = ContactMemory.Update(sparseMemory, sparseObservations, currentTick: 101, sharedBuffer);
        Assert.Equal(sparseExpected.ToArray(), sparseActual.ToArray());

        // Non-vacuous in both directions: the crowded result is longer than
        // the sparse one, so a length that carried over would be visible, and
        // the sparse result is exactly its own one entry rather than a
        // prefix of the crowded one.
        Assert.Equal(6, crowdedActual.Length);
        var only = Assert.Single(sparseActual);
        Assert.Equal(99UL, only.EnemyEntityId);
        Assert.Equal(299, only.LastKnownCellIndex);

        // And the crowded call itself is re-run afterwards against the now
        // sparse-dirtied buffer, so the bleed is checked in both directions
        // rather than only from large to small.
        Assert.Equal(
            crowdedExpected.ToArray(),
            ContactMemory.Update(crowdedMemory, crowdedObservations, currentTick: 100, sharedBuffer).ToArray());
    }

    /// <summary>
    /// Task 88: a caller-supplied buffer too short for the merge is not an
    /// error and not a truncated answer — the method allocates its own and
    /// returns the same result. This is what lets stage 5 grow its buffer
    /// lazily without a correctness cliff on the tick the roster grows.
    /// </summary>
    [Fact]
    public void AMergeBufferShorterThanTheMergeNeeds_StillProducesTheFullResult()
    {
        var existing = ImmutableArray.Create(
            new ContactMemoryEntry(11UL, 111, (int)ContactTier.Identified, 40L),
            new ContactMemoryEntry(12UL, 112, (int)ContactTier.Identified, 41L));
        var observations = new ContactObservation[]
        {
            new(EnemyEntityId: 13, HasLineOfSightThisTick: true, RangeSquaredWu: 0, CurrentCellIndex: 213),
        };

        var expected = ContactMemory.Update(existing, observations, currentTick: 100);
        var actual = ContactMemory.Update(
            existing, observations, currentTick: 100, new ContactMemoryEntry[1]);

        Assert.Equal(expected.ToArray(), actual.ToArray());
        Assert.Equal(3, actual.Length);
    }
}
