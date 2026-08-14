using System.Collections.Immutable;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Combat;
using Sandata.Core.Mathematics;
using Sandata.Core.Simulation;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 36 of Sandata's scaffold plan: simultaneous damage
/// application, instant death with no downed state, and mission outcome
/// resolution, per design section 5 stage 13 of
/// docs/plans/2026-08-07-sandata-scaffold-design.md.
/// </summary>
public sealed class DamageResolutionTests
{
    private static OperatorState BuildOperator(ulong entityId, int health, int faction) => new(
        EntityId: entityId,
        PositionX: FixedPoint.FromWhole((int)entityId),
        PositionY: FixedPoint.Zero,
        Facing: Facing16.East,
        AimAngle: Bam16.FromFacing16(Facing16.East),
        Health: health,
        Faction: faction,
        Intent: 0,
        IsCrouched: false,
        WeaponLowered: false,
        WeaponChainPhase: 0,
        WeaponChainRemainingTicks: 0,
        MagazineRounds: 0,
        CyclicFireAccumulator: 0,
        SuppressionCounter: 0);

    // -- Mutual kills. ---------------------------------------------------

    [Fact]
    public void TwoOperatorsKillingEachOtherOnTheSameTick_BothDie()
    {
        var before = ImmutableArray.Create(
            BuildOperator(1, health: 30, faction: 0),
            BuildOperator(2, health: 30, faction: 1));

        // Each operator addresses a lethal shot at the other, in the same
        // tick's damage batch.
        var damage = ImmutableArray.Create(
            new DamageInstance(SourceEntityId: 1, TargetEntityId: 2, Amount: 40),
            new DamageInstance(SourceEntityId: 2, TargetEntityId: 1, Amount: 40));

        var after = DamageResolution.ApplyDamage(before, damage);

        Assert.False(DamageResolution.IsAlive(after[0].Health));
        Assert.False(DamageResolution.IsAlive(after[1].Health));

        var deaths = DamageResolution.ResolveDeaths(before, after);
        Assert.Equal(new ulong[] { 1UL, 2UL }, deaths.AsSpan().ToArray());
    }

    [Fact]
    public void ResolveDeaths_ReturnsDeadEntityIds_SortedAscending()
    {
        // Deliberately built so a naive scan order (as the array appears)
        // would visit the higher id first if it were not sorted.
        var before = ImmutableArray.Create(
            BuildOperator(5, health: 10, faction: 1),
            BuildOperator(3, health: 10, faction: 0),
            BuildOperator(9, health: 10, faction: 1));
        var damage = ImmutableArray.Create(
            new DamageInstance(1, 9, 50),
            new DamageInstance(1, 5, 50),
            new DamageInstance(1, 3, 50));

        var after = DamageResolution.ApplyDamage(before, damage);
        var deaths = DamageResolution.ResolveDeaths(before, after);

        Assert.Equal(new ulong[] { 3UL, 5UL, 9UL }, deaths.AsSpan().ToArray());
    }

    // -- Accumulation before any death is resolved. -----------------------

    [Fact]
    public void DamageFromThreeSources_AccumulatesBeforeAnyDeathIsResolved()
    {
        // No single contribution is lethal on its own (20 < 50 health), but
        // the sum of all three is. If any single contribution were resolved
        // in isolation, the target would incorrectly survive.
        var before = ImmutableArray.Create(BuildOperator(1, health: 50, faction: 0));
        var damage = ImmutableArray.Create(
            new DamageInstance(SourceEntityId: 10, TargetEntityId: 1, Amount: 20),
            new DamageInstance(SourceEntityId: 11, TargetEntityId: 1, Amount: 20),
            new DamageInstance(SourceEntityId: 12, TargetEntityId: 1, Amount: 20));

        var accumulated = DamageResolution.Accumulate(damage);
        Assert.Equal(new AccumulatedDamage(1, 60), Assert.Single(accumulated.AsSpan().ToArray()));

        var after = DamageResolution.ApplyDamage(before, damage);
        Assert.Equal(-10, after[0].Health);
        Assert.False(DamageResolution.IsAlive(after[0].Health));

        var deaths = DamageResolution.ResolveDeaths(before, after);
        Assert.Equal(new ulong[] { 1UL }, deaths.AsSpan().ToArray());
    }

    // -- Outcome decided only after every death. --------------------------

    [Fact]
    public void Outcome_IsDecidedOnlyAfterEveryDeath_NotFromAnIntermediateRoster()
    {
        // Faction 0's only operator and faction 1's only operator kill each
        // other on the same tick. Resolving the outcome against either
        // faction's kill alone (an intermediate roster) would wrongly award
        // a one-sided victory; only the fully accumulated roster produces
        // the true Draw.
        var before = ImmutableArray.Create(
            BuildOperator(1, health: 10, faction: 0),
            BuildOperator(2, health: 10, faction: 1));
        var damage = ImmutableArray.Create(
            new DamageInstance(1, 2, 25),
            new DamageInstance(2, 1, 25));

        // The wrong, intermediate-roster reading: apply only faction 0's
        // shot and ask for the outcome before faction 1's shot is folded in.
        var onlyOneSideApplied = DamageResolution.ApplyDamage(
            before, ImmutableArray.Create(new DamageInstance(1, 2, 25)));
        Assert.Equal(MissionOutcome.Faction0Victory, OutcomeRules.Resolve(onlyOneSideApplied));

        // The correct reading: resolve against the fully accumulated roster.
        var fullyApplied = DamageResolution.ApplyDamage(before, damage);
        Assert.Equal(MissionOutcome.Draw, OutcomeRules.Resolve(fullyApplied));
    }

    [Fact]
    public void Outcome_OneFactionEliminated_IsVictoryForTheOther()
    {
        var before = ImmutableArray.Create(
            BuildOperator(1, health: 10, faction: 0),
            BuildOperator(2, health: 10, faction: 1),
            BuildOperator(3, health: 10, faction: 1));
        var damage = ImmutableArray.Create(new DamageInstance(2, 1, 50));

        var after = DamageResolution.ApplyDamage(before, damage);

        Assert.Equal(MissionOutcome.Faction1Victory, OutcomeRules.Resolve(after));
    }

    [Fact]
    public void Outcome_BothFactionsStillHaveALivingOperator_IsOngoing()
    {
        var before = ImmutableArray.Create(
            BuildOperator(1, health: 10, faction: 0),
            BuildOperator(2, health: 10, faction: 1));
        var damage = ImmutableArray.Create(new DamageInstance(2, 1, 5));

        var after = DamageResolution.ApplyDamage(before, damage);

        Assert.Equal(MissionOutcome.Ongoing, OutcomeRules.Resolve(after));
    }

    // -- No downed or bleeding state: health has exactly two derived states. --

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-100)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(int.MaxValue)]
    public void IsAlive_PartitionsHealthIntoExactlyAliveOrDead(int health)
    {
        // The classification is the strict inequality itself, at every
        // boundary a real tick can reach: there is no third branch, no
        // window of health values, and no separate flag that this method
        // (or any caller of it) consults before deciding alive versus dead.
        Assert.Equal(health > 0, DamageResolution.IsAlive(health));
    }

    [Fact]
    public void EveryOperatorAfterDamage_IsClassifiedAsExactlyOneOfAliveOrDead()
    {
        // A property over a roster, not a single value: for every operator
        // that survives ApplyDamage, membership in "alive" and membership in
        // "dead" (both defined solely through IsAlive) are mutually
        // exclusive and jointly exhaustive — together they account for every
        // operator, with nothing left over for a third, downed or bleeding
        // category to occupy.
        var before = ImmutableArray.Create(
            BuildOperator(1, health: 5, faction: 0),
            BuildOperator(2, health: 100, faction: 0),
            BuildOperator(3, health: 1, faction: 1),
            BuildOperator(4, health: 40, faction: 1));
        var damage = ImmutableArray.Create(
            new DamageInstance(9, 1, 5),   // exactly lethal: health -> 0
            new DamageInstance(9, 3, 1),   // exactly lethal: health -> 0
            new DamageInstance(9, 4, 5));  // non-lethal: health -> 35

        var after = DamageResolution.ApplyDamage(before, damage);

        var aliveCount = after.Count(op => DamageResolution.IsAlive(op.Health));
        var deadCount = after.Count(op => !DamageResolution.IsAlive(op.Health));

        Assert.Equal(after.Length, aliveCount + deadCount);
        Assert.Equal(2, aliveCount);
        Assert.Equal(2, deadCount);
    }

    // -- Order-independence: permuting arrival order changes nothing. ------

    [Fact]
    public void ApplyDamage_PermutingArrivalOrder_ProducesIdenticalResult()
    {
        var operators = ImmutableArray.Create(
            BuildOperator(1, health: 100, faction: 0),
            BuildOperator(2, health: 100, faction: 1),
            BuildOperator(3, health: 100, faction: 1));

        var damage = ImmutableArray.Create(
            new DamageInstance(2, 1, 15),
            new DamageInstance(3, 1, 20),
            new DamageInstance(1, 2, 40),
            new DamageInstance(1, 3, 5),
            new DamageInstance(2, 3, 5));

        var forward = DamageResolution.ApplyDamage(operators, damage);
        var reversed = DamageResolution.ApplyDamage(operators, damage.Reverse().ToImmutableArray());

        // A third, unrelated permutation, not merely a reversal of the
        // first.
        var shuffled = ImmutableArray.Create(
            damage[2], damage[4], damage[0], damage[3], damage[1]);
        var shuffledResult = DamageResolution.ApplyDamage(operators, shuffled);

        Assert.Equal(forward.AsSpan().ToArray(), reversed.AsSpan().ToArray());
        Assert.Equal(forward.AsSpan().ToArray(), shuffledResult.AsSpan().ToArray());
    }

    [Fact]
    public void Accumulate_PermutingArrivalOrder_ProducesIdenticalTotals()
    {
        var damage = ImmutableArray.Create(
            new DamageInstance(1, 100, 7),
            new DamageInstance(2, 200, 3),
            new DamageInstance(3, 100, 11),
            new DamageInstance(4, 300, 2),
            new DamageInstance(5, 200, 13));

        var forward = DamageResolution.Accumulate(damage);
        var reversed = DamageResolution.Accumulate(damage.Reverse().ToImmutableArray());

        Assert.Equal(forward.AsSpan().ToArray(), reversed.AsSpan().ToArray());
        Assert.Contains(forward, entry => entry is { TargetEntityId: 100, TotalDamage: 18 });
        Assert.Contains(forward, entry => entry is { TargetEntityId: 200, TotalDamage: 16 });
        Assert.Contains(forward, entry => entry is { TargetEntityId: 300, TotalDamage: 2 });
    }

    [Fact]
    public void ResolveDeaths_PermutingArrivalOrder_ProducesIdenticalDeathList()
    {
        var before = ImmutableArray.Create(
            BuildOperator(1, health: 10, faction: 0),
            BuildOperator(2, health: 10, faction: 1),
            BuildOperator(3, health: 10, faction: 1));
        var damage = ImmutableArray.Create(
            new DamageInstance(2, 1, 50),
            new DamageInstance(1, 2, 50),
            new DamageInstance(1, 3, 5));

        var afterForward = DamageResolution.ApplyDamage(before, damage);
        var afterShuffled = DamageResolution.ApplyDamage(
            before, ImmutableArray.Create(damage[2], damage[0], damage[1]));

        var deathsForward = DamageResolution.ResolveDeaths(before, afterForward);
        var deathsShuffled = DamageResolution.ResolveDeaths(before, afterShuffled);

        Assert.Equal(deathsForward.AsSpan().ToArray(), deathsShuffled.AsSpan().ToArray());
        Assert.Equal(new ulong[] { 1UL, 2UL }, deathsForward.AsSpan().ToArray());
    }

    // -- Input validation. --------------------------------------------------

    [Fact]
    public void Accumulate_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        var damage = ImmutableArray.Create(new DamageInstance(1, 2, -1));

        Assert.Throws<ArgumentOutOfRangeException>(() => DamageResolution.Accumulate(damage));
    }

    [Fact]
    public void ApplyDamage_UnaddressedOperator_PassesThroughUnchanged()
    {
        var before = ImmutableArray.Create(
            BuildOperator(1, health: 20, faction: 0),
            BuildOperator(2, health: 20, faction: 1));
        var damage = ImmutableArray.Create(new DamageInstance(1, 2, 5));

        var after = DamageResolution.ApplyDamage(before, damage);

        Assert.Equal(before[0], after[0]);
    }
}
