using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The V7 pressure interrupt of the movement design sections 4.3, 4.5, 5.1
/// and 5.2, called directly on
/// <see cref="WeaponMovementRules.ShouldPressureInterrupt(FootworkPhase, int, int, int, int, int, int, int, int, int)"/>
/// and <see cref="WeaponMovementRules.ComputeWeightedPressure"/> with
/// hand-built scalar inputs: the inclusive threshold comparison, each of the
/// three signals alone on either side of the bar, a combination that only
/// clears the bar as a weighted sum, saturation at the shared signal ceiling,
/// the two degenerate ally counts, the transition-only rule, the
/// zero-threshold opt-out, and the step 1a ladder handoff into
/// <see cref="WeaponMovementRules.ResolveProvisionalFootwork"/>.
/// </summary>
/// <remarks>
/// Every weight, threshold, and count in this file is test scaffolding chosen
/// to make one rule visible, not gameplay tuning. None of it is read from the
/// shipped V7 registry and none of it asserts a shipped value: the registry's
/// provisional numbers are calibrated separately and are expected to move.
/// Only <see cref="WeaponMovementRules.SignalCeilingBasisPoints"/> and
/// <see cref="WeaponMovementRules.RatioBasisPointScale"/> are read from
/// production, because the saturation case is about those constants
/// themselves.
/// </remarks>
public sealed class FootworkPressureInterruptTests
{
    /// <summary>
    /// A scaffolding threshold of half a whole unit. Paired with a support
    /// ring of ten thousand allies it lets a single basis point of signal be
    /// expressed exactly, which is what the equality pair below needs.
    /// </summary>
    private const int HalfUnitThresholdBasisPoints = 5_000;

    /// <summary>
    /// A support ring sized so that one extra enemy moves the
    /// support-pressure signal by exactly one basis point.
    /// </summary>
    private const int BasisPointResolutionRing = 10_000;

    /// <summary>
    /// The whole weight, given to whichever signal a case isolates. The three
    /// production weights total exactly this much, as
    /// <c>MovementRuleset</c> validates, so handing it all to one signal is
    /// the honest way to read that signal on its own scale.
    /// </summary>
    private const int WholeWeightBasisPoints = 10_000;

    // A scaffolding weight split for the cases that need three live signals.
    // It totals WholeWeightBasisPoints, matching the ruleset's rule, and is
    // otherwise arbitrary.
    private const int SplitSupportWeight = 4_000;

    private const int SplitDamageWeight = 3_000;

    private const int SplitCollapseWeight = 3_000;

    // Ladder scaffolding for the step 1a handoff test. The only constraint
    // LoadoutMovementProfile validation places on the pair is that release
    // sits strictly below entry; neither threshold is reached by that test.
    private const int DisengageBasisPoints = 15_000;

    private const int ReengageBasisPoints = 10_000;

    private const int RecoveryTicks = 4;

    private static bool Fires(
        FootworkPhase priorPhase = FootworkPhase.Commit,
        int supportAllies = 1,
        int supportEnemies = 0,
        int priorSupportAllies = 0,
        int damageTakenLastTick = 0,
        int maximumHitPoints = 100,
        int supportPressureWeightBasisPoints = 0,
        int incomingDamageWeightBasisPoints = 0,
        int allyCollapseWeightBasisPoints = 0,
        int thresholdBasisPoints = HalfUnitThresholdBasisPoints) =>
        WeaponMovementRules.ShouldPressureInterrupt(
            priorPhase,
            supportAllies,
            supportEnemies,
            priorSupportAllies,
            damageTakenLastTick,
            maximumHitPoints,
            supportPressureWeightBasisPoints,
            incomingDamageWeightBasisPoints,
            allyCollapseWeightBasisPoints,
            thresholdBasisPoints);

    private static long Weigh(
        int supportAllies = 1,
        int supportEnemies = 0,
        int priorSupportAllies = 0,
        int damageTakenLastTick = 0,
        int maximumHitPoints = 100,
        int supportPressureWeightBasisPoints = 0,
        int incomingDamageWeightBasisPoints = 0,
        int allyCollapseWeightBasisPoints = 0) =>
        WeaponMovementRules.ComputeWeightedPressure(
            supportAllies,
            supportEnemies,
            priorSupportAllies,
            damageTakenLastTick,
            maximumHitPoints,
            supportPressureWeightBasisPoints,
            incomingDamageWeightBasisPoints,
            allyCollapseWeightBasisPoints);

    /// <summary>
    /// One warrior against a thousand enemies, the whole weight on support
    /// pressure: the signal saturates at the ceiling and the sum is as large
    /// as this predicate can be made.
    /// </summary>
    private static bool FiresUnderOverwhelmingPressure(
        FootworkPhase priorPhase,
        int thresholdBasisPoints) =>
        Fires(
            priorPhase: priorPhase,
            supportAllies: 1,
            supportEnemies: 1_000,
            supportPressureWeightBasisPoints: WholeWeightBasisPoints,
            thresholdBasisPoints: thresholdBasisPoints);

    // ----- 5.1: the inclusive threshold comparison -----

    /// <summary>
    /// Entry equality enters, matching step 5's entry rule: five thousand
    /// enemies to ten thousand allies is a support-pressure signal of exactly
    /// 5,000 basis points, and with the whole weight on that signal the
    /// weighted sum sits exactly on a 5,000 basis-point threshold.
    /// </summary>
    [Fact]
    public void AWeightedSumExactlyOnTheThresholdFires() =>
        Assert.True(
            Fires(
                supportAllies: BasisPointResolutionRing,
                supportEnemies: 5_000,
                supportPressureWeightBasisPoints: WholeWeightBasisPoints,
                thresholdBasisPoints: HalfUnitThresholdBasisPoints));

    /// <summary>
    /// One enemy fewer against the same ten thousand allies is a signal of
    /// 4,999 basis points — exactly one basis point below the case above,
    /// against the identical threshold. This pair is what pins the comparison
    /// as <c>&gt;=</c> rather than <c>&gt;</c>.
    /// </summary>
    [Fact]
    public void AWeightedSumOneBasisPointBelowTheThresholdDoesNotFire() =>
        Assert.False(
            Fires(
                supportAllies: BasisPointResolutionRing,
                supportEnemies: 4_999,
                supportPressureWeightBasisPoints: WholeWeightBasisPoints,
                thresholdBasisPoints: HalfUnitThresholdBasisPoints));

    // ----- 4.5: each signal alone, below and above the bar -----

    /// <summary>
    /// Signal A isolated: the whole weight on support pressure, the other two
    /// weights zero, so the sum is the enemy-to-ally ratio itself.
    /// </summary>
    [Theory]
    [InlineData(4_999, false)]
    [InlineData(5_001, true)]
    public void SupportPressureAloneDecidesOnEitherSideOfTheBar(
        int supportEnemies,
        bool expected) =>
        Assert.Equal(
            expected,
            Fires(
                supportAllies: BasisPointResolutionRing,
                supportEnemies: supportEnemies,
                supportPressureWeightBasisPoints: WholeWeightBasisPoints,
                thresholdBasisPoints: HalfUnitThresholdBasisPoints));

    /// <summary>
    /// Signal B isolated: the whole weight on incoming damage, against a
    /// ten-thousand-point warrior so one point of damage is one basis point.
    /// The lone support ally keeps signal A's divisor legal while its zero
    /// weight keeps it out of the sum.
    /// </summary>
    [Theory]
    [InlineData(4_999, false)]
    [InlineData(5_001, true)]
    public void IncomingDamageAloneDecidesOnEitherSideOfTheBar(
        int damageTakenLastTick,
        bool expected) =>
        Assert.Equal(
            expected,
            Fires(
                supportAllies: 1,
                damageTakenLastTick: damageTakenLastTick,
                maximumHitPoints: BasisPointResolutionRing,
                incomingDamageWeightBasisPoints: WholeWeightBasisPoints,
                thresholdBasisPoints: HalfUnitThresholdBasisPoints));

    /// <summary>
    /// Signal C isolated: the whole weight on ally collapse, out of a prior
    /// ring of ten thousand. Surviving 5,001 of them is a loss of 4,999
    /// basis points; surviving 4,999 is a loss of 5,001.
    /// </summary>
    [Theory]
    [InlineData(5_001, false)]
    [InlineData(4_999, true)]
    public void AllyCollapseAloneDecidesOnEitherSideOfTheBar(
        int supportAllies,
        bool expected) =>
        Assert.Equal(
            expected,
            Fires(
                supportAllies: supportAllies,
                priorSupportAllies: BasisPointResolutionRing,
                allyCollapseWeightBasisPoints: WholeWeightBasisPoints,
                thresholdBasisPoints: HalfUnitThresholdBasisPoints));

    // ----- 5.1: a combination that only fires as a sum -----

    /// <summary>
    /// Three signals under the shipped weight rule — a split totalling one
    /// whole unit — where no signal's own weighted contribution reaches the
    /// threshold but the three together clear it. This is the case that
    /// separates a weighted sum from three independent triggers: read as
    /// three triggers, none of these three inputs fires, and the warrior
    /// stays committed.
    /// </summary>
    /// <remarks>
    /// The arithmetic, in basis points of the weighted average: support
    /// pressure is 20,000 at weight 0.4, contributing 8,000; incoming damage
    /// is 20,000 at weight 0.3, contributing 6,000; ally collapse is 9,000 at
    /// weight 0.3, contributing 2,700. Each contribution is below the 9,000
    /// threshold and their sum, 16,700, is above it.
    /// </remarks>
    [Fact]
    public void SignalsBelowTheBarSeparatelyClearItTogether()
    {
        const int Threshold = 9_000;

        // Signal A alone: two enemies to the lone actor, nothing else.
        Assert.False(
            Fires(
                supportAllies: 1,
                supportEnemies: 2,
                supportPressureWeightBasisPoints: SplitSupportWeight,
                incomingDamageWeightBasisPoints: SplitDamageWeight,
                allyCollapseWeightBasisPoints: SplitCollapseWeight,
                thresholdBasisPoints: Threshold));

        // Signal B alone: twice the warrior's own maximum in one tick.
        Assert.False(
            Fires(
                supportAllies: 1,
                damageTakenLastTick: 200,
                maximumHitPoints: 100,
                supportPressureWeightBasisPoints: SplitSupportWeight,
                incomingDamageWeightBasisPoints: SplitDamageWeight,
                allyCollapseWeightBasisPoints: SplitCollapseWeight,
                thresholdBasisPoints: Threshold));

        // Signal C alone: nine of a ten-strong support ring gone.
        Assert.False(
            Fires(
                supportAllies: 1,
                priorSupportAllies: 10,
                supportPressureWeightBasisPoints: SplitSupportWeight,
                incomingDamageWeightBasisPoints: SplitDamageWeight,
                allyCollapseWeightBasisPoints: SplitCollapseWeight,
                thresholdBasisPoints: Threshold));

        // The same three inputs at once, on the same weights and threshold.
        Assert.True(
            Fires(
                supportAllies: 1,
                supportEnemies: 2,
                priorSupportAllies: 10,
                damageTakenLastTick: 200,
                maximumHitPoints: 100,
                supportPressureWeightBasisPoints: SplitSupportWeight,
                incomingDamageWeightBasisPoints: SplitDamageWeight,
                allyCollapseWeightBasisPoints: SplitCollapseWeight,
                thresholdBasisPoints: Threshold));
    }

    // ----- 5.1: saturation at the shared signal ceiling -----

    /// <summary>
    /// Three enemies to one ally is 30,000 basis points, exactly the ceiling.
    /// A million enemies to one ally is ten billion basis points before
    /// saturation, and must weigh exactly the same afterwards.
    /// </summary>
    [Fact]
    public void SupportPressureFarBeyondTheCeilingWeighsTheSameAsAtIt()
    {
        var atTheCeiling = Weigh(
            supportAllies: 1,
            supportEnemies: 3,
            supportPressureWeightBasisPoints: WholeWeightBasisPoints);
        var farBeyondTheCeiling = Weigh(
            supportAllies: 1,
            supportEnemies: 1_000_000,
            supportPressureWeightBasisPoints: WholeWeightBasisPoints);

        Assert.Equal(atTheCeiling, farBeyondTheCeiling);
        Assert.Equal(
            WeaponMovementRules.SignalCeilingBasisPoints *
                WeaponMovementRules.RatioBasisPointScale,
            atTheCeiling);

        // The predicate answers identically on both, and the ceiling is a
        // hard reach: a million enemies still cannot clear a threshold one
        // basis point above it.
        Assert.True(
            Fires(
                supportAllies: 1,
                supportEnemies: 1_000_000,
                supportPressureWeightBasisPoints: WholeWeightBasisPoints,
                thresholdBasisPoints: 30_000));
        Assert.False(
            Fires(
                supportAllies: 1,
                supportEnemies: 1_000_000,
                supportPressureWeightBasisPoints: WholeWeightBasisPoints,
                thresholdBasisPoints: 30_001));
    }

    /// <summary>
    /// Design section 5.1 states the ceiling's purpose directly: it stops one
    /// saturated signal from carrying the sum on its own, so the other two
    /// weights do not become decorative. Asserted here on a weight split of
    /// 0.4 — a saturated support-pressure signal reaches exactly 12,000 basis
    /// points of the weighted average and no further, whatever the ratio.
    /// Without the ceiling, a million enemies would contribute four billion.
    /// </summary>
    [Fact]
    public void ASaturatedSignalCannotCarryTheSumPastItsOwnWeight()
    {
        Assert.True(
            Fires(
                supportAllies: 1,
                supportEnemies: 1_000_000,
                supportPressureWeightBasisPoints: SplitSupportWeight,
                incomingDamageWeightBasisPoints: SplitDamageWeight,
                allyCollapseWeightBasisPoints: SplitCollapseWeight,
                thresholdBasisPoints: 12_000));

        Assert.False(
            Fires(
                supportAllies: 1,
                supportEnemies: 1_000_000,
                supportPressureWeightBasisPoints: SplitSupportWeight,
                incomingDamageWeightBasisPoints: SplitDamageWeight,
                allyCollapseWeightBasisPoints: SplitCollapseWeight,
                thresholdBasisPoints: 12_001));
    }

    // ----- 4.5: the two degenerate ally counts -----

    /// <summary>
    /// A prior support count of zero — a warrior on its first tick, before
    /// any count has been stamped — yields a zero collapse signal rather than
    /// dividing by zero. That this test returns a value at all is the
    /// assertion; the equality is what proves the branch returns zero rather
    /// than something else.
    /// </summary>
    [Fact]
    public void AZeroPriorSupportAllyCountYieldsAZeroCollapseSignal()
    {
        Assert.Equal(
            0L,
            Weigh(
                supportAllies: 5,
                priorSupportAllies: 0,
                allyCollapseWeightBasisPoints: WholeWeightBasisPoints));

        Assert.False(
            Fires(
                supportAllies: 5,
                priorSupportAllies: 0,
                allyCollapseWeightBasisPoints: WholeWeightBasisPoints,
                thresholdBasisPoints: 1));
    }

    /// <summary>
    /// A support ring that grew contributes zero, never a negative that would
    /// drag the weighted sum down and mask the other two signals. Checked
    /// with half the weight on support pressure so a negative collapse term
    /// would be plainly visible in the total.
    /// </summary>
    [Fact]
    public void AllyGrowthContributesZeroRatherThanANegative()
    {
        // Twenty enemies to ten allies is a support-pressure signal of
        // 20,000 basis points; at half weight it contributes 100,000,000 to
        // the scaled sum and is the only term that should appear.
        const long SupportTermOnly = 20_000L * 5_000;

        var grown = Weigh(
            supportAllies: 10,
            supportEnemies: 20,
            priorSupportAllies: 2,
            supportPressureWeightBasisPoints: 5_000,
            allyCollapseWeightBasisPoints: 5_000);
        var unchanged = Weigh(
            supportAllies: 10,
            supportEnemies: 20,
            priorSupportAllies: 10,
            supportPressureWeightBasisPoints: 5_000,
            allyCollapseWeightBasisPoints: 5_000);

        Assert.Equal(SupportTermOnly, grown);
        Assert.Equal(unchanged, grown);

        // Growth from a single prior ally to a full ring is the largest
        // negative the unclamped subtraction could produce, and still yields
        // exactly the support term.
        Assert.Equal(
            SupportTermOnly,
            Weigh(
                supportAllies: 10,
                supportEnemies: 20,
                priorSupportAllies: 1,
                supportPressureWeightBasisPoints: 5_000,
                allyCollapseWeightBasisPoints: 5_000));
    }

    // ----- 4.3: the transition-only rule -----

    /// <summary>
    /// The interrupt exists to preempt the attack lifecycle, and outside that
    /// lifecycle there is nothing to preempt. Without this clause a warrior
    /// under sustained pressure would re-charge its cooldown on every tick it
    /// spent disengaging and never attack again — a worse standoff than the
    /// one V7 exists to fix.
    /// </summary>
    [Theory]
    [InlineData(FootworkPhase.None)]
    [InlineData(FootworkPhase.Approach)]
    [InlineData(FootworkPhase.Engage)]
    [InlineData(FootworkPhase.Refuse)]
    [InlineData(FootworkPhase.Disengage)]
    [InlineData(FootworkPhase.Regroup)]
    [InlineData(FootworkPhase.Pursue)]
    public void NoPhaseOutsideTheAttackLifecycleEverInterrupts(
        FootworkPhase priorPhase) =>
        Assert.False(
            FiresUnderOverwhelmingPressure(
                priorPhase,
                thresholdBasisPoints: 1));

    [Theory]
    [InlineData(FootworkPhase.Commit)]
    [InlineData(FootworkPhase.Recover)]
    public void BothAttackLifecyclePhasesCanInterrupt(
        FootworkPhase priorPhase) =>
        Assert.True(
            FiresUnderOverwhelmingPressure(
                priorPhase,
                thresholdBasisPoints: 1));

    /// <summary>
    /// The two theories above name nine phases between them, which is every
    /// phase declared today. This sweep is what keeps that true: a tenth
    /// phase appended to the enum arrives here as non-interrupting, and
    /// anyone who intends it to interrupt has to say so in this test.
    /// </summary>
    [Fact]
    public void EveryDeclaredPhaseIsSweptAndOnlyTheLifecyclePairInterrupts()
    {
        foreach (var phase in Enum.GetValues<FootworkPhase>())
        {
            var expected =
                phase is FootworkPhase.Commit or FootworkPhase.Recover;

            Assert.Equal(
                expected,
                FiresUnderOverwhelmingPressure(
                    phase,
                    thresholdBasisPoints: 1));
        }
    }

    // ----- 4.2: the zero-threshold opt-out -----

    /// <summary>
    /// A row that registered no threshold never interrupts, whatever the
    /// pressure, which is what keeps every preset from V1 through V6 on the
    /// legacy ladder. Negative values are not reachable through validated
    /// construction and are checked here only so the guard is known to be
    /// <c>&lt;= 0</c> rather than <c>== 0</c>.
    /// </summary>
    [Theory]
    [InlineData(FootworkPhase.Commit, 0)]
    [InlineData(FootworkPhase.Commit, -1)]
    [InlineData(FootworkPhase.Commit, int.MinValue)]
    [InlineData(FootworkPhase.Recover, 0)]
    [InlineData(FootworkPhase.Recover, -1)]
    [InlineData(FootworkPhase.Recover, int.MinValue)]
    public void AZeroOrNegativeThresholdNeverFires(
        FootworkPhase priorPhase,
        int thresholdBasisPoints) =>
        Assert.False(
            FiresUnderOverwhelmingPressure(priorPhase, thresholdBasisPoints));

    // ----- 9.1 step 1a: the ladder handoff -----

    /// <summary>
    /// Step 1a sits above step 2, which returns unconditionally for a prior
    /// <c>Commit</c>. A warrior three ticks into a committed blow therefore
    /// breaks off to <c>Disengage</c> with a zero timer when the interrupt
    /// fired, and decrements its timer as before when it did not. Nothing
    /// else about the inputs changes between the two rows.
    /// </summary>
    [Theory]
    [InlineData(true, FootworkPhase.Disengage, 0)]
    [InlineData(false, FootworkPhase.Commit, 2)]
    public void APriorCommitBreaksOffOnlyWhenTheInterruptFired(
        bool pressureInterruptFired,
        FootworkPhase expectedPhase,
        int expectedTicksRemaining) =>
        Assert.Equal(
            (expectedPhase, expectedTicksRemaining),
            WeaponMovementRules.ResolveProvisionalFootwork(
                isAlive: true,
                priorPhase: FootworkPhase.Commit,
                priorTicksRemaining: 3,
                posture: TacticalPosture.Hold,
                supportAllies: 1,
                supportEnemies: 0,
                disengageEnemyToAllyBasisPoints: DisengageBasisPoints,
                reengageEnemyToAllyBasisPoints: ReengageBasisPoints,
                recoveryTicks: RecoveryTicks,
                hasTarget: false,
                targetAtOrInsidePreferredDistance: false,
                pressureInterruptFired: pressureInterruptFired));

    // ----- The living-agent invariant that replaces argument guards -----

    /// <summary>
    /// <see cref="WeaponMovementRules.ShouldPressureInterrupt(FootworkPhase, int, int, int, int, int, int, int, int, int)"/>
    /// and <see cref="WeaponMovementRules.ComputeWeightedPressure"/> carry no
    /// argument guards, by decision: the predicate runs once per living agent
    /// per tick inside a stage the tick budget is measured against, and a
    /// per-agent per-tick guard was judged not worth its cost. Safety comes
    /// instead from the simulation calling them only on the living-agent
    /// path, where both divisors are non-zero by construction. This test is
    /// what stands in for the guards that are deliberately absent: it pins
    /// the two facts that invariant rests on, so that if either stops holding
    /// this test fails rather than a division by zero appearing inside the
    /// tick loop.
    /// </summary>
    [Fact]
    public void TheLivingAgentInvariantThatStandsInForTheAbsentGuardsHolds()
    {
        // Fact one, the support-pressure divisor: SupportAllies counts the
        // acting warrior itself, so it is at least one for any living agent.
        // Asserted against the type's real behaviour — an actor with no
        // neighbour at all — rather than against its documentation comment.
        var loneActor = new AgentState(
            entityId: 1,
            factionId: 0,
            xRaw: 0,
            yRaw: 0,
            maximumHitPoints: 100,
            movementSpeedRaw: FixedPoint.Scale / 2,
            perceptionRangeRaw: 200 * FixedPoint.Scale,
            attackRangeRaw: 5 * FixedPoint.Scale,
            damagePerAttack: 10,
            attackCooldownTicks: 1,
            loadout: new CombatLoadout(
                WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None));

        var contextRadiusSquared =
            MovementContextQuery.SquaredContextRadius(FixedPoint.Scale);
        var context = MovementContextQuery.Derive(
            [loneActor],
            loneActor,
            selectedTargetEntityId: null,
            contextRadiusSquared,
            contextRadiusSquared);

        Assert.Equal(0, context.ImmediateAllies);
        Assert.Equal(1, context.SupportAllies);

        // Fact two, the incoming-damage divisor: Scenario validation rejects
        // a MaximumHitPoints below one, so that divisor is at least one for
        // every scenario the simulation will accept.
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 2);

        Assert.Equal(
            nameof(Scenario.MaximumHitPoints),
            Assert.Throws<ArgumentOutOfRangeException>(
                () => (scenario with { MaximumHitPoints = 0 }).Validate())
                .ParamName);
        Assert.Equal(
            nameof(Scenario.MaximumHitPoints),
            Assert.Throws<ArgumentOutOfRangeException>(
                () => (scenario with { MaximumHitPoints = -1 }).Validate())
                .ParamName);

        // One is accepted, so one is the floor rather than merely a value
        // that happens to pass.
        (scenario with { MaximumHitPoints = 1 }).Validate();
    }
}
