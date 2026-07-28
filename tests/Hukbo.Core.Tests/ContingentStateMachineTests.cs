using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// T9 unit-level coverage for <see cref="MovementRules"/>: the leader-and-
/// living-count forward scan, the cohesion duty cycle, the six
/// priority-ordered unit-state transition rules, and the six-gate movement
/// eligibility conjunction. Every fact here calls a <see cref="MovementRules"/>
/// static directly with hand-built arguments; none of them constructs a
/// <see cref="BattleSimulation"/>, so none of them can pass because a
/// scenario merely failed to reach the code under test. The behavioural,
/// whole-battle counterparts live in
/// <c>tests/Hukbo.Core.Tests/PersistentContingentTests.cs</c> (T11) and the
/// arrival-taper sweep lives in
/// <c>tests/Hukbo.Core.Tests/ArrivalTaperTests.cs</c> (T10); neither is
/// duplicated here.
/// </summary>
public sealed class ContingentStateMachineTests
{
    [Fact]
    public void ContingentStateFallsToNoneWhenNoMemberIsAlive()
    {
        var result = MovementRules.ResolveContingentState(
            previousState: ContingentState.Hold,
            livingCount: 0,
            initialCount: 20,
            spreadSquared: long.MaxValue,
            contactCount: 1,
            cohesionRadiusRaw: 100,
            closeFractionNumerator: 0,
            closeFractionDenominator: 1,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);

        Assert.Equal(ContingentState.None, result);
    }

    [Fact]
    public void BreakIsTerminalAndBeatsEveryOtherRule()
    {
        // Healthy ratio (no attrition), a member in contact under the (0, 1)
        // fraction (would otherwise select Close), an open window and a
        // spread far beyond cohesionRadiusRaw (would otherwise select Hold)
        // — every other rule is primed to fire, and Break still wins because
        // it is terminal.
        var result = MovementRules.ResolveContingentState(
            previousState: ContingentState.Break,
            livingCount: 20,
            initialCount: 20,
            spreadSquared: 1_000_000,
            contactCount: 1,
            cohesionRadiusRaw: 100,
            closeFractionNumerator: 0,
            closeFractionDenominator: 1,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);

        Assert.Equal(ContingentState.Break, result);
    }

    [Fact]
    public void AttritionBreakBeatsCloseOnContact()
    {
        // Case 1: the ratio trigger. livingCount * 4 <= initialCount, with a
        // member in contact under the (0, 1) fraction that would otherwise
        // select Close.
        var byRatio = MovementRules.ResolveContingentState(
            previousState: ContingentState.Advance,
            livingCount: 5,
            initialCount: 20,
            spreadSquared: 0,
            contactCount: 1,
            cohesionRadiusRaw: 100,
            closeFractionNumerator: 0,
            closeFractionDenominator: 1,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);
        Assert.Equal(ContingentState.Break, byRatio);

        // Case 2: the floor trigger, asserted independently of the ratio —
        // livingCount is below minimumCohesiveMembers while the ratio itself
        // is healthy (2 * 4 = 8 > 5).
        var byMinimum = MovementRules.ResolveContingentState(
            previousState: ContingentState.Advance,
            livingCount: 2,
            initialCount: 5,
            spreadSquared: 0,
            contactCount: 1,
            cohesionRadiusRaw: 100,
            closeFractionNumerator: 0,
            closeFractionDenominator: 1,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);
        Assert.Equal(ContingentState.Break, byMinimum);
    }

    [Fact]
    public void CloseOnContactBeatsTheGatheringTest()
    {
        var result = MovementRules.ResolveContingentState(
            previousState: ContingentState.Advance,
            livingCount: 20,
            initialCount: 20,
            spreadSquared: 1_000_000,
            contactCount: 1,
            cohesionRadiusRaw: 100,
            closeFractionNumerator: 0,
            closeFractionDenominator: 1,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);

        Assert.Equal(ContingentState.Close, result);
    }

    /// <summary>
    /// The (1, 2) and (0, 1) fractions produce different verdicts for the
    /// same single member in contact out of forty living: (1, 2) demands
    /// half the contingent (twenty) before entering Close, so one member is
    /// nowhere near enough, while (0, 1) — the fraction every registered
    /// preset carries today — collapses to "at least one member in
    /// contact" and closes on that same input. This is the provisional
    /// <c>PersistentContingentsV3</c> game-design choice from a later task,
    /// not a historical measurement.
    /// </summary>
    [Fact]
    public void OneMemberInContactOutOfFortyDoesNotCloseUnderOneOverTwoButDoesUnderZeroOverOne()
    {
        var underOneOverTwo = MovementRules.ResolveContingentState(
            previousState: ContingentState.Advance,
            livingCount: 40,
            initialCount: 40,
            spreadSquared: 0,
            contactCount: 1,
            cohesionRadiusRaw: 100,
            closeFractionNumerator: 1,
            closeFractionDenominator: 2,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);
        Assert.NotEqual(ContingentState.Close, underOneOverTwo);

        var underZeroOverOne = MovementRules.ResolveContingentState(
            previousState: ContingentState.Advance,
            livingCount: 40,
            initialCount: 40,
            spreadSquared: 0,
            contactCount: 1,
            cohesionRadiusRaw: 100,
            closeFractionNumerator: 0,
            closeFractionDenominator: 1,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);
        Assert.Equal(ContingentState.Close, underZeroOverOne);
    }

    /// <summary>
    /// Pins the exact entry-threshold boundary under a (1, 2) fraction and
    /// forty living members: <c>CeilDiv(40 * 1, 2) = 20</c>. Nineteen members
    /// in contact does not close; exactly twenty does, because the
    /// comparison is <c>&gt;=</c>.
    /// </summary>
    [Theory]
    [InlineData(19, false)]
    [InlineData(20, true)]
    public void ContactCountExactlyAtTheEntryThresholdCloses(
        int contactCount,
        bool expectedClose)
    {
        var result = MovementRules.ResolveContingentState(
            previousState: ContingentState.Advance,
            livingCount: 40,
            initialCount: 40,
            spreadSquared: 0,
            contactCount,
            cohesionRadiusRaw: 100,
            closeFractionNumerator: 1,
            closeFractionDenominator: 2,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);

        Assert.Equal(expectedClose, result == ContingentState.Close);
    }

    /// <summary>
    /// The exit threshold under a (1, 2) fraction and forty living members is
    /// <c>CeilDiv(40 * 1, 4) = 10</c>, strictly below the twenty-member entry
    /// threshold. A contingent already <see cref="ContingentState.Close"/>
    /// with fifteen members in contact — below entry but above exit — stays
    /// <see cref="ContingentState.Close"/>, proving the hysteresis band is
    /// live rather than the two thresholds having collapsed to one.
    /// </summary>
    [Fact]
    public void AContingentAlreadyCloseAboveTheExitThresholdStaysClose()
    {
        var result = MovementRules.ResolveContingentState(
            previousState: ContingentState.Close,
            livingCount: 40,
            initialCount: 40,
            spreadSquared: 0,
            contactCount: 15,
            cohesionRadiusRaw: 100,
            closeFractionNumerator: 1,
            closeFractionDenominator: 2,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);

        Assert.Equal(ContingentState.Close, result);
    }

    /// <summary>
    /// The same exit threshold of ten as the previous fact, but with nine
    /// members in contact — one below it. A contingent already
    /// <see cref="ContingentState.Close"/> leaves that state once contact
    /// falls below the exit threshold, and with an open window, passing
    /// geometric gates and no spread, rule 6 resolves the vacated slot to
    /// <see cref="ContingentState.Advance"/>.
    /// </summary>
    [Fact]
    public void AContingentAlreadyCloseBelowTheExitThresholdLeavesClose()
    {
        var result = MovementRules.ResolveContingentState(
            previousState: ContingentState.Close,
            livingCount: 40,
            initialCount: 40,
            spreadSquared: 0,
            contactCount: 9,
            cohesionRadiusRaw: 100,
            closeFractionNumerator: 1,
            closeFractionDenominator: 2,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);

        Assert.NotEqual(ContingentState.Close, result);
        Assert.Equal(ContingentState.Advance, result);
    }

    [Fact]
    public void AShutDutyCycleWindowForcesAdvanceOverHold()
    {
        var result = MovementRules.ResolveContingentState(
            previousState: ContingentState.Advance,
            livingCount: 20,
            initialCount: 20,
            spreadSquared: 1_000_000,
            contactCount: 0,
            cohesionRadiusRaw: 100,
            closeFractionNumerator: 0,
            closeFractionDenominator: 1,
            minimumCohesiveMembers: 3,
            windowOpen: false,
            geometricGatesPass: true);

        Assert.Equal(ContingentState.Advance, result);
    }

    /// <summary>
    /// The property that keeps the inspector honest: a geometric-gate
    /// denial forces <see cref="ContingentState.Advance"/> the same way a
    /// shut duty-cycle window does, even with the window open and the
    /// spread far beyond <c>cohesionRadiusRaw</c>, so a contingent whose
    /// members are in fact pursuing independently is never labelled
    /// <see cref="ContingentState.Hold"/>.
    /// </summary>
    [Fact]
    public void AGeometricGateDenialForcesAdvanceOverHold()
    {
        var result = MovementRules.ResolveContingentState(
            previousState: ContingentState.Advance,
            livingCount: 20,
            initialCount: 20,
            spreadSquared: 1_000_000,
            contactCount: 0,
            cohesionRadiusRaw: 100,
            closeFractionNumerator: 0,
            closeFractionDenominator: 1,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: false);

        Assert.Equal(ContingentState.Advance, result);
    }

    /// <summary>
    /// Three cases at one spread value strictly between <c>9/16</c> and
    /// <c>1</c> of <c>cohesionRadiusRaw</c> squared — 5,625 and 10,000 for
    /// a radius of 100 — where entering and remaining disagree.
    /// </summary>
    [Theory]
    [InlineData(ContingentState.Hold, ContingentState.Hold)]
    [InlineData(ContingentState.Advance, ContingentState.Advance)]
    // Close is reachable here only through rule 3 having lapsed this tick
    // (contactCount having fallen below its threshold); it is not a
    // "remaining in Hold" state, so it takes the higher entry bar exactly
    // like Advance and None do.
    [InlineData(ContingentState.Close, ContingentState.Advance)]
    public void TheHysteresisBandEntersAboveTheRadiusAndLeavesBelowThreeQuarters(
        ContingentState previousState,
        ContingentState expected)
    {
        const long cohesionRadiusRaw = 100;
        const long spreadSquared = 8_000;

        var result = MovementRules.ResolveContingentState(
            previousState,
            livingCount: 20,
            initialCount: 20,
            spreadSquared,
            contactCount: 0,
            cohesionRadiusRaw,
            closeFractionNumerator: 0,
            closeFractionDenominator: 1,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Pins the exact hysteresis threshold on the Hold-retention side of
    /// rule 6: <c>16 * spreadSquared == 9 * cohesionRadiusSquared</c> is NOT
    /// straggling, because the comparison is a strict greater-than. A
    /// contingent sitting exactly on the boundary therefore loses Hold and
    /// falls back to Advance rather than staying gathered.
    /// </summary>
    [Fact]
    public void TheHysteresisThresholdIsExclusiveAtExactEquality()
    {
        const long cohesionRadiusRaw = 100;
        // cohesionRadiusSquared = 10_000; 9 * 10_000 = 90_000 = 16 * 5_625,
        // so 5_625 sits exactly on the boundary.
        const long spreadSquaredAtBoundary = 5_625;

        var result = MovementRules.ResolveContingentState(
            previousState: ContingentState.Hold,
            livingCount: 20,
            initialCount: 20,
            spreadSquared: spreadSquaredAtBoundary,
            contactCount: 0,
            cohesionRadiusRaw,
            closeFractionNumerator: 0,
            closeFractionDenominator: 1,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);

        Assert.Equal(ContingentState.Advance, result);
    }

    /// <summary>
    /// A spread squared far past the point where <c>16 * spreadSquared</c>
    /// would have overflowed <see cref="long"/> under the old checked
    /// comparison. The widened comparison must return the answer implied by
    /// unbounded integer arithmetic — a spread that far past
    /// <c>cohesionRadiusRaw</c> is unambiguously straggling, so Hold is kept
    /// — instead of throwing <see cref="OverflowException"/>.
    /// </summary>
    [Fact]
    public void AnOverflowingSpreadIsUnambiguouslyStragglingRatherThanThrowing()
    {
        // long.MaxValue / 16 is the point past which 16 * spreadSquared
        // alone already exceeds long.MaxValue; this value is well past it
        // while cohesionRadiusSquared stays tiny.
        const long spreadSquaredWellPastOverflow = long.MaxValue / 8;
        const long cohesionRadiusRaw = 100;

        var result = MovementRules.ResolveContingentState(
            previousState: ContingentState.Hold,
            livingCount: 20,
            initialCount: 20,
            spreadSquared: spreadSquaredWellPastOverflow,
            contactCount: 0,
            cohesionRadiusRaw,
            closeFractionNumerator: 0,
            closeFractionDenominator: 1,
            minimumCohesiveMembers: 3,
            windowOpen: true,
            geometricGatesPass: true);

        Assert.Equal(ContingentState.Hold, result);
    }

    [Fact]
    public void TheDutyCycleWindowIsOpenExactlyTheDutyFractionOfEveryCycle()
    {
        const int cohesionCycleTicks = 240;
        const int cohesionDutyTicks = 180;
        const int slot = 5;

        var openCount = 0;
        for (var tick = 0; tick < cohesionCycleTicks; tick++)
        {
            if (MovementRules.IsCohesionWindowOpen(
                tick, slot, cohesionCycleTicks, cohesionDutyTicks))
            {
                openCount++;
            }
        }

        Assert.Equal(cohesionDutyTicks, openCount);

        var longestRun = 0;
        var currentRun = 0;
        for (var tick = 0; tick < cohesionCycleTicks * 3; tick++)
        {
            if (MovementRules.IsCohesionWindowOpen(
                tick, slot, cohesionCycleTicks, cohesionDutyTicks))
            {
                currentRun++;
                longestRun = Math.Max(longestRun, currentRun);
            }
            else
            {
                currentRun = 0;
            }
        }

        Assert.Equal(cohesionDutyTicks, longestRun);
    }

    [Fact]
    public void TheSixteenSlotPhasesAreDistinct()
    {
        const int cohesionCycleTicks = 240;

        var phases = Enumerable.Range(0, 16)
            .Select(slot => slot * cohesionCycleTicks / 16)
            .ToArray();

        Assert.Equal(phases.Length, phases.Distinct().Count());
    }

    [Fact]
    public void TheLeaderIsTheLowestLivingEntityIdInItsContingent()
    {
        var agents = new[]
        {
            CreateAgent(entityId: 5, factionId: 0, contingentId: 0),
            CreateAgent(entityId: 2, factionId: 0, contingentId: 0),
            CreateAgent(entityId: 9, factionId: 0, contingentId: 1),
            CreateAgent(entityId: 100, factionId: 1, contingentId: 0),
            CreateAgent(entityId: 50, factionId: 1, contingentId: 3),
            CreateAgent(entityId: 60, factionId: 1, contingentId: 3),
        };

        var leaderEntityIdsBySlot = new ulong[16];
        var livingCountsBySlot = new int[16];
        MovementRules.ScanContingentLeadersAndLivingCounts(
            agents, leaderEntityIdsBySlot, livingCountsBySlot, selectByRank: false);

        Assert.Equal(2UL, leaderEntityIdsBySlot[0]);
        Assert.Equal(2, livingCountsBySlot[0]);
        Assert.Equal(9UL, leaderEntityIdsBySlot[1]);
        Assert.Equal(1, livingCountsBySlot[1]);
        Assert.Equal(100UL, leaderEntityIdsBySlot[8]);
        Assert.Equal(1, livingCountsBySlot[8]);
        Assert.Equal(50UL, leaderEntityIdsBySlot[11]);
        Assert.Equal(2, livingCountsBySlot[11]);

        // An unoccupied slot stays at the zero sentinel.
        Assert.Equal(0UL, leaderEntityIdsBySlot[2]);
        Assert.Equal(0, livingCountsBySlot[2]);
    }

    [Fact]
    public void LeaderSelectionIsUnchangedByAgentArrayPermutation()
    {
        var a5 = CreateAgent(5, factionId: 0, contingentId: 0);
        var a2 = CreateAgent(2, factionId: 0, contingentId: 0);
        var a9 = CreateAgent(9, factionId: 0, contingentId: 1);
        var a100 = CreateAgent(100, factionId: 1, contingentId: 0);

        var orderingA = new[] { a5, a2, a9, a100 };
        var orderingB = new[] { a100, a9, a5, a2 };
        var orderingC = new[] { a2, a100, a5, a9 };

        var leadersA = new ulong[16];
        var livingA = new int[16];
        MovementRules.ScanContingentLeadersAndLivingCounts(
            orderingA, leadersA, livingA, selectByRank: false);

        var leadersB = new ulong[16];
        var livingB = new int[16];
        MovementRules.ScanContingentLeadersAndLivingCounts(
            orderingB, leadersB, livingB, selectByRank: false);

        var leadersC = new ulong[16];
        var livingC = new int[16];
        MovementRules.ScanContingentLeadersAndLivingCounts(
            orderingC, leadersC, livingC, selectByRank: false);

        Assert.Equal(leadersA, leadersB);
        Assert.Equal(leadersA, leadersC);
        Assert.Equal(livingA, livingB);
        Assert.Equal(livingA, livingC);
    }

    [Fact]
    public void TheLeaderIsPromotedToTheNextLowestLivingEntityIdOnDeath()
    {
        var a5 = CreateAgent(5, factionId: 0, contingentId: 0);
        var a2 = CreateAgent(2, factionId: 0, contingentId: 0);
        var a9 = CreateAgent(9, factionId: 0, contingentId: 0);
        var agents = new[] { a5, a2, a9 };

        var leaderEntityIdsBySlot = new ulong[16];
        var livingCountsBySlot = new int[16];
        MovementRules.ScanContingentLeadersAndLivingCounts(
            agents, leaderEntityIdsBySlot, livingCountsBySlot, selectByRank: false);
        Assert.Equal(2UL, leaderEntityIdsBySlot[0]);
        Assert.Equal(3, livingCountsBySlot[0]);

        a2.HitPoints = 0;
        MovementRules.ScanContingentLeadersAndLivingCounts(
            agents, leaderEntityIdsBySlot, livingCountsBySlot, selectByRank: false);
        Assert.Equal(5UL, leaderEntityIdsBySlot[0]);
        Assert.Equal(2, livingCountsBySlot[0]);

        a5.HitPoints = 0;
        a9.HitPoints = 0;
        MovementRules.ScanContingentLeadersAndLivingCounts(
            agents, leaderEntityIdsBySlot, livingCountsBySlot, selectByRank: false);
        Assert.Equal(0UL, leaderEntityIdsBySlot[0]);
        Assert.Equal(0, livingCountsBySlot[0]);
    }

    /// <summary>
    /// <c>selectByRank: true</c>, a single chief present: the chief leads
    /// even though a non-chief member of the same contingent carries a
    /// lower <see cref="AgentState.EntityId"/>.
    /// </summary>
    [Fact]
    public void WithSelectByRankASingleChiefLeadsRegardlessOfEntityId()
    {
        var agents = new[]
        {
            CreateAgent(entityId: 2, factionId: 0, contingentId: 0, rank: RankId.Timawa),
            CreateAgent(entityId: 9, factionId: 0, contingentId: 0, rank: RankId.Datu),
            CreateAgent(entityId: 5, factionId: 0, contingentId: 0, rank: RankId.Maharlika),
        };

        var leaderEntityIdsBySlot = new ulong[16];
        var livingCountsBySlot = new int[16];
        MovementRules.ScanContingentLeadersAndLivingCounts(
            agents, leaderEntityIdsBySlot, livingCountsBySlot, selectByRank: true);

        Assert.Equal(9UL, leaderEntityIdsBySlot[0]);
    }

    /// <summary>
    /// <c>selectByRank: true</c>, several chiefs present: the tie on
    /// <see cref="RankId.Datu"/> breaks on the lowest
    /// <see cref="AgentState.EntityId"/> among the tied chiefs.
    /// </summary>
    [Fact]
    public void WithSelectByRankTiedChiefsBreakOnTheLowestEntityId()
    {
        var agents = new[]
        {
            CreateAgent(entityId: 9, factionId: 0, contingentId: 0, rank: RankId.Datu),
            CreateAgent(entityId: 2, factionId: 0, contingentId: 0, rank: RankId.Datu),
            CreateAgent(entityId: 5, factionId: 0, contingentId: 0, rank: RankId.Timawa),
        };

        var leaderEntityIdsBySlot = new ulong[16];
        var livingCountsBySlot = new int[16];
        MovementRules.ScanContingentLeadersAndLivingCounts(
            agents, leaderEntityIdsBySlot, livingCountsBySlot, selectByRank: true);

        Assert.Equal(2UL, leaderEntityIdsBySlot[0]);
    }

    /// <summary>
    /// <c>selectByRank: true</c>, no chief present: the highest-ranking
    /// (lowest-numbered <see cref="RankId"/>) survivor wins, not the lowest
    /// <see cref="AgentState.EntityId"/>.
    /// </summary>
    [Fact]
    public void WithSelectByRankNoChiefPresentTheHighestRankingSurvivorWins()
    {
        var agents = new[]
        {
            CreateAgent(entityId: 2, factionId: 0, contingentId: 0, rank: RankId.Timawa),
            CreateAgent(entityId: 9, factionId: 0, contingentId: 0, rank: RankId.Maharlika),
            CreateAgent(entityId: 5, factionId: 0, contingentId: 0, rank: RankId.AlipingNamamahay),
        };

        var leaderEntityIdsBySlot = new ulong[16];
        var livingCountsBySlot = new int[16];
        MovementRules.ScanContingentLeadersAndLivingCounts(
            agents, leaderEntityIdsBySlot, livingCountsBySlot, selectByRank: true);

        Assert.Equal(9UL, leaderEntityIdsBySlot[0]);
    }

    /// <summary>
    /// <c>selectByRank: true</c>, the chief dies mid-battle: leadership
    /// passes to the next-ranking survivor on the following scan, because
    /// leadership is recomputed from scratch every tick rather than stored.
    /// </summary>
    [Fact]
    public void WithSelectByRankLeadershipPassesToTheNextRankingSurvivorOnTheChiefsDeath()
    {
        var chief = CreateAgent(entityId: 9, factionId: 0, contingentId: 0, rank: RankId.Datu);
        var heir = CreateAgent(entityId: 2, factionId: 0, contingentId: 0, rank: RankId.Maharlika);
        var agents = new[] { chief, heir };

        var leaderEntityIdsBySlot = new ulong[16];
        var livingCountsBySlot = new int[16];
        MovementRules.ScanContingentLeadersAndLivingCounts(
            agents, leaderEntityIdsBySlot, livingCountsBySlot, selectByRank: true);
        Assert.Equal(9UL, leaderEntityIdsBySlot[0]);

        chief.HitPoints = 0;
        MovementRules.ScanContingentLeadersAndLivingCounts(
            agents, leaderEntityIdsBySlot, livingCountsBySlot, selectByRank: true);
        Assert.Equal(2UL, leaderEntityIdsBySlot[0]);
    }

    /// <summary>
    /// <c>selectByRank: false</c> ignores hand-placed <see cref="RankId"/>
    /// data entirely and reproduces the <see cref="AgentState.EntityId"/>-only
    /// result — the proof that <c>PersistentContingentsV1</c> through
    /// <c>PersistentContingentsV4</c> produce an unmoved leader selection
    /// under hand-placed rank data, since <see cref="MovementRuleset.SelectsLeaderByRank"/>
    /// is <see langword="false"/> for all four.
    /// </summary>
    [Fact]
    public void SelectByRankFalseIgnoresHandPlacedRankDataEntirely()
    {
        var agents = new[]
        {
            CreateAgent(entityId: 9, factionId: 0, contingentId: 0, rank: RankId.Datu),
            CreateAgent(entityId: 2, factionId: 0, contingentId: 0, rank: RankId.Ayuey),
            CreateAgent(entityId: 5, factionId: 0, contingentId: 0, rank: RankId.Timawa),
        };

        var leaderEntityIdsBySlot = new ulong[16];
        var livingCountsBySlot = new int[16];
        MovementRules.ScanContingentLeadersAndLivingCounts(
            agents, leaderEntityIdsBySlot, livingCountsBySlot, selectByRank: false);

        // The Datu carries the highest rank but not the lowest entity id;
        // with selectByRank: false the lowest entity id wins regardless.
        Assert.Equal(2UL, leaderEntityIdsBySlot[0]);
    }

    /// <summary>
    /// <see cref="MovementRules.IsCohesionEligible"/>'s six gates are an
    /// unconditional conjunction, not a priority order: this sweeps every
    /// combination of the six named denials and asserts the method is
    /// <see langword="true"/> only in the single all-permitting combination
    /// (<c>mask == 0</c>), and <see langword="false"/> in every one of the
    /// sixty-three others, including each single denial on its own and
    /// every combination of them. There is no priority to assert between
    /// the six, unlike <see cref="MovementRules.ResolveContingentState"/>'s
    /// transition rules above, so this is a truth table rather than an
    /// ordering test.
    /// </summary>
    [Fact]
    public void CohesionEligibilityIsTheConjunctionOfAllSixGates()
    {
        for (var mask = 0; mask < 64; mask++)
        {
            var denyState = (mask & 1) != 0;
            var denyLeader = (mask & 2) != 0;
            var denyWindow = (mask & 4) != 0;
            var denyStraggler = (mask & 8) != 0;
            var denyMap = (mask & 16) != 0;
            var denyOverlap = (mask & 32) != 0;

            // Baseline state is Advance with straggling true, so gate 4
            // (Advance requires straggling) is independently exercisable by
            // denyStraggler without also touching gate 1.
            var state = denyState ? ContingentState.Close : ContingentState.Advance;
            var isLeader = denyLeader;
            var windowOpen = !denyWindow;
            var straggling = !denyStraggler;
            var squareFitsMap = !denyMap;
            var squareOverlapsAnother = denyOverlap;

            var expected = mask == 0;
            var result = MovementRules.IsCohesionEligible(
                state, isLeader, windowOpen, straggling, squareFitsMap, squareOverlapsAnother);

            Assert.True(
                result == expected,
                $"mask={mask} (state={state}, isLeader={isLeader}, " +
                $"windowOpen={windowOpen}, straggling={straggling}, " +
                $"squareFitsMap={squareFitsMap}, " +
                $"squareOverlapsAnother={squareOverlapsAnother}): " +
                $"expected {expected}, got {result}.");
        }
    }

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int contingentId,
        RankId rank = RankId.Timawa)
    {
        return new AgentState(
            entityId,
            factionId,
            xRaw: 0,
            yRaw: 0,
            maximumHitPoints: 100,
            movementSpeedRaw: 1,
            perceptionRangeRaw: 1,
            attackRangeRaw: 1,
            damagePerAttack: 1,
            attackCooldownTicks: 1,
            loadout: new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.None,
                rank),
            contingentId: contingentId);
    }
}
