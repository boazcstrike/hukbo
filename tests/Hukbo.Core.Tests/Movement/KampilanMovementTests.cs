using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The Kampilan-owned behaviour pins for the solo Kampilan (<c>KP</c>) row:
/// the preferred-engagement band, ally lane clearance, the direction-band and
/// committed pace caps, the turn budgets, retained-pace acceleration, the
/// commitment and recovery durations, and the local-count disengagement
/// hysteresis. Every numeric expectation is derived inline from
/// <see cref="KampilanMovementProfile.Row"/> and the scenario constants named
/// below, so a row change fails here with arithmetic a reader can follow.
/// Every tuning value is a provisional reconstruction: gameplay tuning; no
/// historical measurement. Evidence ledger:
/// docs/research/movement/kampilan.md.
/// </summary>
/// <remarks>
/// These tests assert the conventions the shared foundation actually
/// implemented, which differ from
/// the kampilan movement plan in two places the shared
/// handoff calls out by name. First, the preferred distance
/// is <em>not</em> a stop line: equality enters
/// <see cref="FootworkPhase.Engage"/> inclusively, and the agent keeps closing
/// to the post-movement reach gate. Second, the ordered ladder in
/// <see cref="WeaponMovementRules.ResolveProvisionalFootwork"/> places the
/// commitment and recovery continuations at steps 2 and 3, ahead of the ratio
/// checks at steps 4 and 5, so local pressure cannot cancel a commitment.
/// the weapon movement workstream plan outranks the weapon plan on
/// both points.
/// </remarks>
public sealed class KampilanMovementTests
{
    // The shared 1v1 probe constants, matching the derivation the foundation
    // pipeline tests use so that expected values are comparable across files.
    private const int AttackRangeRaw = 5 * FixedPoint.Scale;    // 5120
    private const int BodyRadiusRaw = FixedPoint.Scale / 2;     // 512
    private const int MovementSpeedRaw = FixedPoint.Scale / 2;  // 512

    // Kampilan against Kampilan, offset cell 0: 5120 * 11500 / 10000 = 5888.
    private const long PreferredAgainstKampilanRaw = 5_888;

    // Body diameter 1024; Kampilan's 15000 basis points is 1.5 diameters:
    // 1024 * 15000 / 10000 = 1536.
    private const long ClearanceRaw = 1_536;

    private static LoadoutMovementProfile Row => KampilanMovementProfile.Row;

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

    // ----- Preferred engagement band (plan section 6; README step 8) -----

    /// <summary>
    /// The band test is inclusive on squared values. One raw unit outside
    /// stays <see cref="FootworkPhase.Approach"/>; exact equality and one raw
    /// unit inside both reach <see cref="FootworkPhase.Engage"/>. No square
    /// root is taken and no floating point appears anywhere in the chain.
    /// </summary>
    [Theory]
    [InlineData(5_889L, false)]  // one raw unit outside
    [InlineData(5_888L, true)]   // exact equality
    [InlineData(5_887L, true)]   // one raw unit inside
    public void ThePreferredEngagementBandIsInclusiveAtExactEquality(
        long separationRaw, bool expectedInsideBand)
    {
        var radiusRaw = MovementRouteRules.EffectivePreferredDistanceRaw(
            AttackRangeRaw, Row, opponentCanonicalIndex: 0);
        Assert.Equal(PreferredAgainstKampilanRaw, radiusRaw);

        // The caller compares squared values inclusively:
        //   5888^2 = 34_668_544
        //   5889^2 = 34_680_321  (outside)
        //   5887^2 = 34_656_769  (inside)
        var separationSquared = Square(separationRaw);
        var radiusSquared = Square(radiusRaw);
        var insideBand = separationSquared <= radiusSquared;

        Assert.Equal(expectedInsideBand, insideBand);

        var (phase, ticksRemaining) = ResolveKampilanFootwork(
            priorPhase: FootworkPhase.None,
            priorTicksRemaining: 0,
            posture: TacticalPosture.Advance,
            supportAllies: 1,
            supportEnemies: 0,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: insideBand);

        Assert.Equal(
            expectedInsideBand ? FootworkPhase.Engage : FootworkPhase.Approach,
            phase);
        Assert.Equal(0, ticksRemaining);
    }

    /// <summary>
    /// The band is the offset-adjusted distance, not a flat reach multiple.
    /// The flat 1.15 multiple holds only for the two zero-offset opponents.
    /// Provisional reconstruction: gameplay tuning; no historical measurement.
    /// </summary>
    [Theory]
    [InlineData(0, 5_888L)]  // KP, offset    0: 5120 * 11500 / 10000
    [InlineData(1, 5_888L)]  // WA, offset    0: 5120 * 11500 / 10000
    [InlineData(2, 6_016L)]  // KA, offset +250: 5120 * 11750 / 10000
    [InlineData(3, 6_144L)]  // IT, offset +500: 5120 * 12000 / 10000
    [InlineData(4, 6_016L)]  // KS, offset +250: 5120 * 11750 / 10000
    [InlineData(5, 6_144L)]  // IS, offset +500: 5120 * 12000 / 10000
    public void ThePreferredBandShiftsWithTheOpponentOffsetCell(
        int opponentCanonicalIndex, long expectedRaw)
    {
        Assert.Equal(
            expectedRaw,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, Row, opponentCanonicalIndex));
    }

    /// <summary>
    /// A shield changes nothing about Kampilan's spacing beyond the weapon it
    /// is paired with: the Kalis cell and the shielded-Kalis cell agree, as do
    /// the Itak and shielded-Itak cells. The row carries no shield-conditional
    /// field and no facing penalty, so no directional or speed effect is
    /// reachable from this plan.
    /// </summary>
    [Fact]
    public void TheShieldContributesNothingToKampilanSpacingBeyondItsWeaponCell()
    {
        Assert.Equal(
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, Row, opponentCanonicalIndex: 2),
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, Row, opponentCanonicalIndex: 4));
        Assert.Equal(
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, Row, opponentCanonicalIndex: 3),
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, Row, opponentCanonicalIndex: 5));

        Assert.Equal(
            Row.OpponentDistanceOffsetBasisPoints[2],
            Row.OpponentDistanceOffsetBasisPoints[4]);
        Assert.Equal(
            Row.OpponentDistanceOffsetBasisPoints[3],
            Row.OpponentDistanceOffsetBasisPoints[5]);
    }

    // ----- Ally lane clearance (plan section 6; README clearance contract) --

    /// <summary>
    /// An ally endpoint exactly at the clearance radius does not deny the
    /// lane; one raw unit inside does. The conflict pass rejects only a
    /// strictly closer endpoint, so exact equality is clear.
    /// </summary>
    [Theory]
    [InlineData(1_535, false)]  // one raw unit inside  -> denied
    [InlineData(1_536, true)]   // exact equality       -> clear
    [InlineData(1_537, true)]   // one raw unit outside -> clear
    public void AnAllyExactlyAtTheClearanceRadiusDoesNotDenyTheLane(
        int separationRaw, bool expectedSecondAccepted)
    {
        var radiusRaw = MovementRouteRules.ClearanceRadiusRaw(
            BodyRadiusRaw, Row.AllyClearanceBodyDiametersBasisPoints);
        Assert.Equal(ClearanceRaw, radiusRaw);

        // Required separation squared is 1536^2 = 2_359_296.
        //   1535^2 = 2_356_225  (strictly closer, rejected)
        //   1536^2 = 2_359_296  (equal, accepted)
        //   1537^2 = 2_362_369  (further, accepted)
        var clearanceSquared = Square(radiusRaw);
        var proposals = new[]
        {
            new FriendlyClearanceProposal(
                1, FootworkPhase.Engage, 0, 0, clearanceSquared),
            new FriendlyClearanceProposal(
                2, FootworkPhase.Engage, separationRaw, 0, clearanceSquared),
        };
        var accepted = new bool[proposals.Length];

        MovementRouteRules.AcceptFriendlyClearanceConflicts(
            proposals, accepted);

        Assert.True(accepted[0]);
        Assert.Equal(expectedSecondAccepted, accepted[1]);
    }

    // ----- Pace caps and retained pace (plan K2 items 3, 5, 6, 9) -----

    /// <summary>
    /// The direction band selects the Kampilan cap: forward 98 percent within
    /// one sector, lateral 82 percent out to five, backward 70 percent beyond.
    /// Provisional reconstruction: gameplay tuning; no historical measurement.
    /// </summary>
    [Theory]
    [InlineData(0, 9_800, 501)]  // 512 * 9800 / 10000 = 501 (501.76 truncates)
    [InlineData(1, 9_800, 501)]
    [InlineData(2, 8_200, 419)]  // 512 * 8200 / 10000 = 419 (419.84 truncates)
    [InlineData(5, 8_200, 419)]
    [InlineData(6, 7_000, 358)]  // 512 * 7000 / 10000 = 358 (358.4 truncates)
    [InlineData(8, 7_000, 358)]
    public void TheDirectionBandsCapPaceAtTheApprovedRatios(
        int separationSectors, int expectedCapBasisPoints, int expectedPaceRaw)
    {
        var capBasisPoints = FacingRules.DirectionBandPaceCapBasisPoints(
            Row, separationSectors);

        Assert.Equal(expectedCapBasisPoints, capBasisPoints);
        Assert.Equal(
            expectedPaceRaw,
            MovementRouteRules.DesiredPaceRaw(MovementSpeedRaw, capBasisPoints));
    }

    /// <summary>
    /// While committed the band cap is clamped by the committed cap, which is
    /// lower than every band, so a committed Kampilan moves at 30 percent of
    /// the shared baseline whatever direction it faces.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(8)]
    public void TheCommittedClampIsTheMinimumOfBandAndCommittedCap(
        int separationSectors)
    {
        var bandBasisPoints = FacingRules.DirectionBandPaceCapBasisPoints(
            Row, separationSectors);
        var committedBasisPoints = Math.Min(
            bandBasisPoints, Row.CommittedPaceBasisPoints);

        Assert.Equal(3_000, committedBasisPoints);

        // 512 * 3000 / 10000 = 153 (153.6 truncates toward zero).
        Assert.Equal(
            153,
            MovementRouteRules.DesiredPaceRaw(
                MovementSpeedRaw, committedBasisPoints));
    }

    /// <summary>
    /// The normal turn budget is two of sixteen sectors and the committed
    /// budget is one. An exact eight-sector opposition is a tie, which turns
    /// canonical-clockwise.
    /// </summary>
    [Fact]
    public void TheNormalTurnBudgetIsTwoSectorsAndCommittedIsOne()
    {
        Assert.Equal(2, Row.MaximumFacingStepsPerTick);
        Assert.Equal(1, Row.CommittedFacingStepsPerTick);

        // East (0) to West (8) is eight steps either way; the tie turns
        // clockwise, so two steps lands on SouthEast (2) and one step on
        // EastSouthEast (1).
        Assert.Equal(
            Facing16.SouthEast,
            FacingRules.TurnToward(
                Facing16.East,
                Facing16.West,
                Row.MaximumFacingStepsPerTick,
                factionId: 0));
        Assert.Equal(
            Facing16.EastSouthEast,
            FacingRules.TurnToward(
                Facing16.East,
                Facing16.West,
                Row.CommittedFacingStepsPerTick,
                factionId: 0));
    }

    /// <summary>
    /// Retained pace rises by 5000 basis points of the baseline per tick and
    /// falls by 6000, never overshooting the target in either direction.
    /// </summary>
    [Fact]
    public void TheRetainedPaceRisesByFiveThousandAndFallsBySixThousandBasisPoints()
    {
        // 512 * 5000 / 10000 = 256 exactly; 512 * 6000 / 10000 = 307 (307.2).
        var accelerationStep = MovementRouteRules.PaceStepRaw(
            MovementSpeedRaw, Row.AccelerationBasisPointsPerTick);
        var decelerationStep = MovementRouteRules.PaceStepRaw(
            MovementSpeedRaw, Row.DecelerationBasisPointsPerTick);

        Assert.Equal(256, accelerationStep);
        Assert.Equal(307, decelerationStep);

        // An evenly divisible baseline proves the ratio without truncation.
        Assert.Equal(
            5_000,
            MovementRouteRules.PaceStepRaw(
                10_000, Row.AccelerationBasisPointsPerTick));
        Assert.Equal(
            6_000,
            MovementRouteRules.PaceStepRaw(
                10_000, Row.DecelerationBasisPointsPerTick));

        // Accelerating toward the forward pace 501: 0 -> 256 -> 501 -> 501.
        var pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            0, 501, accelerationStep, decelerationStep);
        Assert.Equal(256, pace);
        pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            pace, 501, accelerationStep, decelerationStep);
        Assert.Equal(501, pace);
        pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            pace, 501, accelerationStep, decelerationStep);
        Assert.Equal(501, pace);

        // Decelerating toward the committed pace 153: 501 -> 194 -> 153.
        pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            501, 153, accelerationStep, decelerationStep);
        Assert.Equal(194, pace);
        pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            pace, 153, accelerationStep, decelerationStep);
        Assert.Equal(153, pace);
    }

    /// <summary>
    /// Non-divisible inputs truncate toward zero, and the per-tick pace step
    /// keeps a one-raw-unit floor so a slow agent still accelerates.
    /// </summary>
    [Fact]
    public void ATruncatingSpeedFloorsThePaceAndKeepsTheOneUnitStep()
    {
        // 3 * 9800 / 10000 = 2 (2.94 truncates toward zero).
        Assert.Equal(
            2,
            MovementRouteRules.DesiredPaceRaw(3, Row.ForwardPaceBasisPoints));

        // 1 * 5000 / 10000 = 0, floored to one raw unit.
        Assert.Equal(
            1,
            MovementRouteRules.PaceStepRaw(
                1, Row.AccelerationBasisPointsPerTick));
        Assert.Equal(
            1,
            MovementRouteRules.PaceStepRaw(
                1, Row.DecelerationBasisPointsPerTick));
    }

    /// <summary>
    /// Every Kampilan multiplier is at most one whole, so no desired pace can
    /// exceed the shared human baseline at any speed. This is plan section 9
    /// acceptance bullet 8 at the pure layer; the per-tick displacement
    /// version is asserted by the scenario tests.
    /// </summary>
    [Theory]
    [InlineData(512)]
    [InlineData(10_000)]
    [InlineData(1)]
    public void NoKampilanDesiredPaceExceedsTheSharedBaseline(int baselineRaw)
    {
        int[] caps =
        [
            Row.ForwardPaceBasisPoints,
            Row.LateralPaceBasisPoints,
            Row.BackwardPaceBasisPoints,
            Row.CommittedPaceBasisPoints,
        ];

        Assert.All(
            caps,
            cap =>
            {
                Assert.True(cap <= 10_000);
                Assert.True(
                    MovementRouteRules.DesiredPaceRaw(baselineRaw, cap)
                        <= baselineRaw);
            });
    }

    // ----- Commitment and recovery lifecycle (plan section 6) -----

    /// <summary>
    /// An entry timer counts its own entry tick, so a commitment of three
    /// spans the attack tick plus two more, and the recovery of three that
    /// follows spans three whole ticks before the ladder falls through again.
    /// </summary>
    [Fact]
    public void ThreeCommittedTicksAreFollowedByThreeRecoveryTicks()
    {
        Assert.Equal(3, Row.CommitmentTicks);
        Assert.Equal(3, Row.RecoveryTicks);

        // Tick 1 of the commitment is the attack tick itself, written by the
        // pipeline as (Commit, CommitmentTicks). The ladder then produces:
        var step = ResolveKampilanFootwork(
            priorPhase: FootworkPhase.Commit,
            priorTicksRemaining: Row.CommitmentTicks,
            posture: TacticalPosture.Advance,
            supportAllies: 1,
            supportEnemies: 0,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);
        Assert.Equal((FootworkPhase.Commit, 2), step);   // committed tick 2

        step = Continue(step);
        Assert.Equal((FootworkPhase.Commit, 1), step);   // committed tick 3

        step = Continue(step);
        Assert.Equal((FootworkPhase.Recover, 3), step);  // recovery tick 1

        step = Continue(step);
        Assert.Equal((FootworkPhase.Recover, 2), step);  // recovery tick 2

        step = Continue(step);
        Assert.Equal((FootworkPhase.Recover, 1), step);  // recovery tick 3

        // The expiring recovery falls through to the ordinary ladder.
        step = Continue(step);
        Assert.Equal((FootworkPhase.Engage, 0), step);
    }

    /// <summary>
    /// The commitment and recovery continuations sit at steps 2 and 3 of the
    /// ladder, ahead of the ratio checks at steps 4 and 5, so local pressure
    /// that would otherwise enter disengagement cannot cancel a commitment.
    /// This is the shared implementation of the plan's "after any
    /// non-cancellable shared safety or commitment rule" clause.
    /// </summary>
    [Theory]
    [InlineData(FootworkPhase.Commit, 3, FootworkPhase.Commit, 2)]
    [InlineData(FootworkPhase.Commit, 1, FootworkPhase.Recover, 3)]
    [InlineData(FootworkPhase.Recover, 3, FootworkPhase.Recover, 2)]
    public void LocalPressureDoesNotCancelACommitmentOrItsRecovery(
        FootworkPhase priorPhase,
        int priorTicksRemaining,
        FootworkPhase expectedPhase,
        int expectedTicksRemaining)
    {
        // Four hostiles to one ally is far beyond the 2:1 entry threshold.
        var (phase, ticksRemaining) = ResolveKampilanFootwork(
            priorPhase: priorPhase,
            priorTicksRemaining: priorTicksRemaining,
            posture: TacticalPosture.Withdraw,
            supportAllies: 1,
            supportEnemies: 4,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);

        Assert.Equal(expectedPhase, phase);
        Assert.Equal(expectedTicksRemaining, ticksRemaining);
    }

    // ----- Local-count disengagement hysteresis (plan sections 6 and 7) ----

    /// <summary>
    /// Entry compares widened integer cross-products and equality enters, so
    /// exactly two hostiles per ally begins disengagement at every scale.
    /// One hostile short of the threshold does not.
    /// </summary>
    [Theory]
    [InlineData(1, 2, FootworkPhase.Disengage)]  // 20000 >= 20000, enters
    [InlineData(2, 4, FootworkPhase.Disengage)]  // 40000 >= 40000, enters
    [InlineData(3, 6, FootworkPhase.Disengage)]  // 60000 >= 60000, enters
    [InlineData(3, 5, FootworkPhase.Engage)]     // 50000 <  60000, does not
    public void DisengagementEntersAtExactlyTwoToOne(
        int supportAllies, int supportEnemies, FootworkPhase expectedPhase)
    {
        Assert.Equal(20_000, Row.DisengageEnemyToAllyBasisPoints);

        var (phase, _) = ResolveKampilanFootwork(
            priorPhase: FootworkPhase.None,
            priorTicksRemaining: 0,
            posture: TacticalPosture.Advance,
            supportAllies: supportAllies,
            supportEnemies: supportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);

        Assert.Equal(expectedPhase, phase);
    }

    /// <summary>
    /// Release equality leaves: an already-disengaging Kampilan at exactly
    /// five hostiles per four allies re-engages, while one hostile more holds
    /// it in disengagement.
    /// </summary>
    [Theory]
    [InlineData(4, 5, FootworkPhase.Engage)]      // 50000 > 50000 is false
    [InlineData(4, 6, FootworkPhase.Disengage)]   // 60000 > 50000 is true
    public void DisengagementReleasesAtExactlyFiveToFour(
        int supportAllies, int supportEnemies, FootworkPhase expectedPhase)
    {
        Assert.Equal(12_500, Row.ReengageEnemyToAllyBasisPoints);

        var (phase, _) = ResolveKampilanFootwork(
            priorPhase: FootworkPhase.Disengage,
            priorTicksRemaining: 0,
            posture: TacticalPosture.Advance,
            supportAllies: supportAllies,
            supportEnemies: supportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);

        Assert.Equal(expectedPhase, phase);
    }

    /// <summary>
    /// Strictly between the release and entry thresholds the prior state is
    /// preserved: a disengaging Kampilan stays disengaged and a committed one
    /// does not begin. Both ratios below lie between 5:4 and 2:1.
    /// </summary>
    [Theory]
    [InlineData(4, 6)]  // 1.50 : 1
    [InlineData(4, 7)]  // 1.75 : 1
    public void TheHysteresisBandPreservesThePriorDisengagementState(
        int supportAllies, int supportEnemies)
    {
        Assert.True(
            supportEnemies * 10_000
                > supportAllies * Row.ReengageEnemyToAllyBasisPoints);
        Assert.True(
            supportEnemies * 10_000
                < supportAllies * Row.DisengageEnemyToAllyBasisPoints);

        var (heldDisengaged, _) = ResolveKampilanFootwork(
            priorPhase: FootworkPhase.Disengage,
            priorTicksRemaining: 0,
            posture: TacticalPosture.Advance,
            supportAllies: supportAllies,
            supportEnemies: supportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);
        Assert.Equal(FootworkPhase.Disengage, heldDisengaged);

        var (stayedEngaged, _) = ResolveKampilanFootwork(
            priorPhase: FootworkPhase.None,
            priorTicksRemaining: 0,
            posture: TacticalPosture.Advance,
            supportAllies: supportAllies,
            supportEnemies: supportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);
        Assert.Equal(FootworkPhase.Engage, stayedEngaged);
    }

    /// <summary>
    /// With no perceived hostile the ratio arithmetic alone never enters or
    /// remains in disengagement, whatever the prior phase. Nothing divides, so
    /// there is no zero-denominator path to guard.
    /// </summary>
    [Theory]
    [InlineData(FootworkPhase.None)]
    [InlineData(FootworkPhase.Disengage)]
    public void ZeroPerceivedHostilesNeverDisengages(FootworkPhase priorPhase)
    {
        var (phase, ticksRemaining) = ResolveKampilanFootwork(
            priorPhase: priorPhase,
            priorTicksRemaining: 0,
            posture: TacticalPosture.Advance,
            supportAllies: 1,
            supportEnemies: 0,
            hasTarget: false,
            targetAtOrInsidePreferredDistance: false);

        Assert.NotEqual(FootworkPhase.Disengage, phase);
        Assert.Equal(FootworkPhase.None, phase);
        Assert.Equal(0, ticksRemaining);
    }

    /// <summary>
    /// The cross-products are widened before multiplying, so counts far beyond
    /// any reachable roster size neither overflow nor mis-compare.
    /// </summary>
    [Theory]
    [InlineData(1_000_000, 2_000_000, FootworkPhase.Disengage)]
    [InlineData(int.MaxValue, int.MaxValue, FootworkPhase.Engage)]
    public void TheRatioCrossProductsDoNotOverflowAtLargeCounts(
        int supportAllies, int supportEnemies, FootworkPhase expectedPhase)
    {
        var (phase, _) = ResolveKampilanFootwork(
            priorPhase: FootworkPhase.None,
            priorTicksRemaining: 0,
            posture: TacticalPosture.Advance,
            supportAllies: supportAllies,
            supportEnemies: supportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);

        Assert.Equal(expectedPhase, phase);
    }

    /// <summary>
    /// A commanding faction-wide advantage resolves an advancing posture, and
    /// that posture does not stop a locally outnumbered Kampilan from
    /// disengaging: the local ratio is read at ladder step 5, ahead of the
    /// posture branch at step 6. Faction totals cannot override local safety.
    /// </summary>
    [Fact]
    public void AGlobalFactionAdvantageDoesNotOverrideLocalTwoToOnePressure()
    {
        var posture = WeaponMovementRules.ResolveTacticalPosture(
            globalAllies: 10,
            globalEnemies: 2,
            contingentState: ContingentState.Advance,
            alliedRoleCoverage: 1,
            enemyRoleCoverage: 1);

        Assert.Equal(TacticalPosture.Advance, posture);

        var (phase, _) = ResolveKampilanFootwork(
            priorPhase: FootworkPhase.None,
            priorTicksRemaining: 0,
            posture: posture,
            supportAllies: 1,
            supportEnemies: 2,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);

        Assert.Equal(FootworkPhase.Disengage, phase);
    }

    /// <summary>
    /// The row resolves under V6 for every rank, and the profile the ladder
    /// reads is the one this file pins.
    /// </summary>
    [Fact]
    public void TheResolvedKampilanProfileDrivesTheseThresholds()
    {
        var resolved = V6.ResolveLoadoutProfile(
            new CombatLoadout(
                WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None));

        Assert.Same(Row, resolved);
    }

    // ----- Local context counts (plan section 6; README support radius) -----

    /// <summary>
    /// The support ratio reads the six-body-diameter support radius with the
    /// actor counted on the ally side. A dead ally contributes nowhere, an
    /// enemy exactly at the radius is inside it, and an enemy beyond
    /// perception is not observed at all.
    /// </summary>
    [Fact]
    public void TheSupportCountsIncludeTheActorAndExcludeDeadAndDistantAgents()
    {
        var scenario = CreateScenario();

        // 1024 * 25000 / 10000 = 2560 immediate; 1024 * 60000 / 10000 = 6144.
        var immediateRaw = MovementContextQuery.ContextRadiusRaw(
            BodyRadiusRaw, V6.ImmediateRadiusBodyDiametersBasisPoints);
        var supportRaw = MovementContextQuery.ContextRadiusRaw(
            BodyRadiusRaw, V6.SupportRadiusBodyDiametersBasisPoints);
        Assert.Equal(2_560L, immediateRaw);
        Assert.Equal(6_144L, supportRaw);

        var actor = CreateAgent(1, 0, 100_000, 51_200, scenario, KampilanKey);
        var livingAlly = CreateAgent(
            2, 0, 100_000 + 3_000, 51_200, scenario, KampilanKey);
        var deadAlly = CreateAgent(
            3, 0, 100_000 + 3_100, 51_200, scenario, KampilanKey);
        deadAlly.HitPoints = 0;
        var enemyAtRadius = CreateAgent(
            4, 1, 100_000 + (int)supportRaw, 51_200, scenario, KampilanKey);
        var enemyBeyondPerception = CreateAgent(
            5, 1, 100_000 + (200 * FixedPoint.Scale) + 1, 51_200, scenario,
            KampilanKey);

        AgentState[] agents =
            [actor, livingAlly, deadAlly, enemyAtRadius, enemyBeyondPerception];

        var solo = MovementContextQuery.Derive(
            new[] { actor },
            actor,
            selectedTargetEntityId: null,
            MovementContextQuery.SquaredContextRadius(immediateRaw),
            MovementContextQuery.SquaredContextRadius(supportRaw));
        Assert.Equal(1, solo.SupportAllies);
        Assert.Equal(0, solo.ImmediateAllies);
        Assert.Equal(0, solo.SupportEnemies);

        var context = MovementContextQuery.Derive(
            agents,
            actor,
            selectedTargetEntityId: enemyAtRadius.EntityId,
            MovementContextQuery.SquaredContextRadius(immediateRaw),
            MovementContextQuery.SquaredContextRadius(supportRaw));

        // The actor plus the one living ally; the dead ally counts nowhere.
        Assert.Equal(2, context.SupportAllies);

        // The enemy exactly at the support radius is inclusive; the one past
        // perception is never observed.
        Assert.Equal(1, context.SupportEnemies);
    }

    /// <summary>
    /// An exact distance tie between two eligible allied support references
    /// resolves on the lower stable entity identifier, never on span order.
    /// </summary>
    [Fact]
    public void TheNearestAllySupportReferenceTakesTheLowerEntityIdOnATie()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, 0, 100_000, 51_200, scenario, KampilanKey);
        var higherId = CreateAgent(
            7, 0, 100_000 + 2_000, 51_200, scenario, KampilanKey);
        var lowerId = CreateAgent(
            4, 0, 100_000 - 2_000, 51_200, scenario, KampilanKey);

        var context = Derive(scenario, actor, [actor, higherId, lowerId]);

        Assert.Equal(4UL, context.NearestAllyEntityId);
    }

    /// <summary>
    /// The derived context is a function of the set, not of the order it
    /// arrives in. Every permutation of the same candidates yields an equal
    /// <see cref="LocalMovementContext"/> field for field.
    /// </summary>
    [Fact]
    public void PermutedCandidateOrderDoesNotChangeTheDerivedContext()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, 0, 100_000, 51_200, scenario, KampilanKey);
        var ally = CreateAgent(
            4, 0, 100_000 - 2_000, 51_200, scenario, KampilanKey);
        var enemyNear = CreateAgent(
            6, 1, 100_000 + 2_400, 51_200, scenario, ItakKey);
        var enemyFar = CreateAgent(
            9, 1, 100_000 + 5_000, 51_200, scenario, KalisKey);

        var expected = Derive(
            scenario, actor, [actor, ally, enemyNear, enemyFar]);

        AgentState[][] permutations =
        [
            [enemyFar, enemyNear, ally, actor],
            [ally, enemyFar, actor, enemyNear],
            [enemyNear, actor, enemyFar, ally],
        ];

        Assert.All(
            permutations,
            permutation =>
                Assert.Equal(expected, Derive(scenario, actor, permutation)));
    }

    // ----- Whole-tick behaviour (plan K2 item 8, K3 step 2) -----

    /// <summary>
    /// Caller order is canonicalized on entity identifier, so the same battle
    /// supplied in reverse produces an identical ordered event stream and an
    /// identical state hash.
    /// </summary>
    [Fact]
    public void ReversedCallerInputProducesTheSameStateAndEventHash()
    {
        var scenario = CreateScenario();

        var forward = RunToCompletion(
            scenario,
            [
                CreateAgent(1, 0, 96_000, 51_200, scenario, KampilanKey),
                CreateAgent(2, 1, 108_000, 51_200, scenario, KampilanKey),
            ],
            ticks: 400);
        var reversed = RunToCompletion(
            scenario,
            [
                CreateAgent(2, 1, 108_000, 51_200, scenario, KampilanKey),
                CreateAgent(1, 0, 96_000, 51_200, scenario, KampilanKey),
            ],
            ticks: 400);

        Assert.Equal(forward.StateHash, reversed.StateHash);
        Assert.Equal(forward.EventStream, reversed.EventStream);
        Assert.Equal(forward.Outcome, reversed.Outcome);
    }

    /// <summary>
    /// An attack accepted while recovering restarts a whole three-tick
    /// commitment. The accepted-attack marker is a private pipeline field, so
    /// this is observable only through authoritative agent state after a tick.
    /// </summary>
    [Fact]
    public void AnAcceptedAttackDuringRecoveryRestartsAThreeTickCommitment()
    {
        // The shipped Kampilan cooldown of seven ticks is longer than the
        // three-tick commitment plus three-tick recovery, so a shipped-tuning
        // battle can never accept an attack while recovering. The interrupt is
        // a pipeline rule rather than a tuning outcome, so this probe shortens
        // the cooldown to one tick to make the rule observable at all. No
        // combat statistic is being asserted or changed.
        var scenario = CreateScenario();
        // A cooldown of four lands the next accepted attack on the first
        // recovery tick: commitment occupies the attack tick and two more,
        // recovery begins on the fourth.
        var west = CreateAgent(
            1, 0, 100_000, 51_200, scenario, KampilanKey,
            attackCooldownTicksOverride: 4);
        var east = CreateAgent(
            2, 1, 100_000 + 8_000, 51_200, scenario, KampilanKey,
            attackCooldownTicksOverride: 4);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        var sawRecovery = false;
        var sawRestartFromRecovery = false;

        for (var tick = 0; tick < 400; tick++)
        {
            var priorPhase = west.FootworkPhase;
            simulation.AdvanceOneTick();

            if (west.FootworkPhase == FootworkPhase.Recover)
            {
                sawRecovery = true;
            }

            if (priorPhase == FootworkPhase.Recover &&
                west.FootworkPhase == FootworkPhase.Commit)
            {
                sawRestartFromRecovery = true;
                Assert.Equal(
                    Row.CommitmentTicks, west.FootworkTicksRemaining);
            }

            Assert.True(west.FootworkTicksRemaining >= 0);
        }

        Assert.True(
            sawRecovery,
            "The probe never reached a recovery tick, so the interrupt could " +
            "not be observed.");
        Assert.True(
            sawRestartFromRecovery,
            "No accepted attack restarted a commitment out of recovery.");
    }

    // ----- Matchup and group coverage (plan sections 7, K4, K5) -----

    /// <summary>
    /// Every canonical opponent, generated from the shared matrix rather than
    /// a hand-maintained list. Each cell is asserted only against bounds the
    /// shared plan already fixes: repeated runs are identical, no step exceeds
    /// the shared baseline, every phase and posture is a declared member, and
    /// no timer goes negative. Outcome statistics are calibration evidence,
    /// not pass or fail criteria, and no win rate is asserted anywhere.
    /// </summary>
    [Theory]
    [MemberData(nameof(KampilanOneVersusOneCells))]
    public void KampilanFacesEveryCanonicalOpponentDeterministically(
        int firstIndex, int secondIndex)
    {
        foreach (var seed in ApprovedSeeds)
        {
            var scenario = CreateScenario(seed: seed);
            var first = MovementScenarioMatrix.CanonicalLoadouts[firstIndex];
            var second = MovementScenarioMatrix.CanonicalLoadouts[secondIndex];

            AgentState[] Build() =>
            [
                CreateAgent(1, 0, 96_000, 51_200, scenario, first),
                CreateAgent(2, 1, 108_000, 51_200, scenario, second),
            ];

            var run = RunToCompletion(scenario, Build(), ticks: 600);
            var repeat = RunToCompletion(scenario, Build(), ticks: 600);

            Assert.Equal(run.StateHash, repeat.StateHash);
            Assert.Equal(run.EventStream, repeat.EventStream);
            Assert.True(run.LegalSteps);
            Assert.True(run.LegalPhases);
        }
    }

    /// <summary>
    /// Two Kampilan allies crowded into one lane against a single enemy do not
    /// commit on the same tick. The stagger comes from actual positions and
    /// the shared clearance scan, not from any Kampilan pairing rule: there is
    /// no special two-Kampilan state and no synchronized turn-taking.
    /// </summary>
    [Fact]
    public void TwoKampilanAlliesStaggerTheirCommitmentsFromGeometry()
    {
        var scenario = CreateScenario();
        // Both allies begin well outside reach and share one approach lane,
        // the second trailing the first by rather less than the clearance
        // radius, so the stagger is produced by geometry alone.
        var first = CreateAgent(1, 0, 60_000, 51_200, scenario, KampilanKey);
        var second = CreateAgent(2, 0, 58_000, 51_200, scenario, KampilanKey);
        var enemy = CreateAgent(3, 1, 150_000, 51_200, scenario, KampilanKey);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, first, second, enemy);

        int? firstCommitTick = null;
        int? secondCommitTick = null;

        for (var tick = 0; tick < 400; tick++)
        {
            simulation.AdvanceOneTick();

            if (firstCommitTick is null &&
                first.FootworkPhase == FootworkPhase.Commit)
            {
                firstCommitTick = tick;
            }

            if (secondCommitTick is null &&
                second.FootworkPhase == FootworkPhase.Commit)
            {
                secondCommitTick = tick;
            }

            if (firstCommitTick is not null && secondCommitTick is not null)
            {
                break;
            }
        }

        Assert.NotNull(firstCommitTick);
        Assert.NotNull(secondCommitTick);
        Assert.NotEqual(firstCommitTick, secondCommitTick);
    }

    /// <summary>
    /// Kampilan-present groups at every small and mid size stay deterministic
    /// and inside the shared legality bounds. The plan's 100v100 and 250v250
    /// cases are mass workloads and belong to the benchmark script, not to a
    /// unit test inside the canonical gate.
    /// </summary>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 5)]
    [InlineData(4, 4)]
    [InlineData(5, 5)]
    [InlineData(8, 8)]
    public void AKampilanGroupScenarioStaysDeterministicAndBounded(
        int factionZeroCount, int factionOneCount)
    {
        var scenario = CreateScenario();

        AgentState[] Build()
        {
            var agents = new List<AgentState>();
            ulong entityId = 1;
            for (var index = 0; index < factionZeroCount; index++)
            {
                agents.Add(CreateAgent(
                    entityId++, 0, 96_000, 40_000 + (index * 1_800), scenario,
                    KampilanKey));
            }

            for (var index = 0; index < factionOneCount; index++)
            {
                agents.Add(CreateAgent(
                    entityId++, 1, 112_000, 40_000 + (index * 1_800), scenario,
                    index % 2 == 0 ? ItakKey : KampilanKey));
            }

            return [.. agents];
        }

        var run = RunToCompletion(scenario, Build(), ticks: 600);
        var repeat = RunToCompletion(scenario, Build(), ticks: 600);

        Assert.Equal(run.StateHash, repeat.StateHash);
        Assert.Equal(run.EventStream, repeat.EventStream);
        Assert.True(run.LegalSteps, run.StepFailure ?? "step");
        Assert.True(run.LegalPhases, run.PhaseFailure ?? "phase");
    }

    /// <summary>
    /// A Kampilan isolated in a locally outnumbered pocket disengages even
    /// while its faction holds a commanding headcount advantage. Local
    /// geometry decides, not the roster total.
    /// </summary>
    [Fact]
    public void LocallyOutnumberedKampilanDisengagesUnderAFavourableFactionTotal()
    {
        var scenario = CreateScenario();
        var agents = new List<AgentState>();
        ulong entityId = 1;

        // The isolated Kampilan, far from its own side.
        var isolated = CreateAgent(
            entityId++, 0, 40_000, 20_000, scenario, KampilanKey);
        agents.Add(isolated);

        // Three hostiles inside its support radius: 3 to 1 is past 2 to 1.
        for (var index = 0; index < 3; index++)
        {
            agents.Add(CreateAgent(
                entityId++, 1, 40_000 + 3_000, 20_000 + (index * 1_200),
                scenario, ItakKey));
        }

        // Seven more allies far away, so faction zero holds 8 against 3.
        for (var index = 0; index < 7; index++)
        {
            agents.Add(CreateAgent(
                entityId++, 0, 180_000, 80_000 + (index * 1_500), scenario,
                KampilanKey));
        }

        var simulation = BattleSimulation.CreateForTesting(
            scenario, [.. agents]);

        var sawDisengage = false;
        for (var tick = 0; tick < 200 && isolated.IsAlive; tick++)
        {
            simulation.AdvanceOneTick();
            if (isolated.FootworkPhase == FootworkPhase.Disengage)
            {
                sawDisengage = true;
                break;
            }
        }

        Assert.True(
            sawDisengage,
            "A Kampilan facing three local hostiles with a faction total of " +
            "eight against three never disengaged.");
    }

    // ----- Theory data -----

    /// <summary>
    /// The six one-versus-one cells containing Kampilan, generated from the
    /// shared canonical matrix rather than hand-maintained here.
    /// </summary>
    public static TheoryData<int, int> KampilanOneVersusOneCells()
    {
        var data = new TheoryData<int, int>();
        foreach (var pair in MovementScenarioMatrix.EnumerateOneVersusOnePairs())
        {
            if (pair.FirstLoadoutIndex == 0 || pair.SecondLoadoutIndex == 0)
            {
                data.Add(pair.FirstLoadoutIndex, pair.SecondLoadoutIndex);
            }
        }

        return data;
    }

    // ----- Helpers -----

    private static readonly ulong[] ApprovedSeeds = [1, 2, 3, 5, 8];

    private static readonly CombatLoadout KampilanKey =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout ItakKey =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout KalisKey =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None);

    private static CombatRuleset CombatRules =>
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV2);

    /// <summary>
    /// Every scenario names its combat preset and its movement preset
    /// explicitly. <c>PrecolonialPhilippinesV2</c> is the only preset that
    /// fields all six canonical loadouts, so it spans the whole matrix.
    /// </summary>
    private static Scenario CreateScenario(ulong seed = 1) =>
        new(
            Seed: seed,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 2_000)
        {
            MaximumHitPoints = 1_000_000,
            DamagePerAttack = 1,
            AttackRangeRaw = AttackRangeRaw,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = BodyRadiusRaw,
            MovementSpeedRaw = MovementSpeedRaw,
            AttackCooldownTicks = 5,
            LastStandThresholdAgents = 0,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };

    /// <summary>
    /// Builds one agent carrying its weapon's real reach, damage, and cooldown
    /// from the selected combat preset, exactly as a full-roster run does, so
    /// a matchup is not artificially reach-equal.
    /// </summary>
    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatLoadout loadout,
        int? attackCooldownTicksOverride = null)
    {
        var rules = CombatRules;
        var weapon = rules.HasWeaponProfiles
            ? rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield)
            : new WeaponProfile(
                scenario.DamagePerAttack,
                scenario.AttackRangeRaw,
                scenario.AttackCooldownTicks);

        return new AgentState(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            weapon.AttackRangeRaw,
            weapon.DamagePerAttack,
            attackCooldownTicksOverride ?? weapon.AttackCooldownTicks,
            loadout);
    }

    private static LocalMovementContext Derive(
        Scenario scenario, AgentState actor, AgentState[] agents)
    {
        var immediateRaw = MovementContextQuery.ContextRadiusRaw(
            scenario.BodyRadiusRaw,
            V6.ImmediateRadiusBodyDiametersBasisPoints);
        var supportRaw = MovementContextQuery.ContextRadiusRaw(
            scenario.BodyRadiusRaw,
            V6.SupportRadiusBodyDiametersBasisPoints);

        return MovementContextQuery.Derive(
            agents,
            actor,
            selectedTargetEntityId: null,
            MovementContextQuery.SquaredContextRadius(immediateRaw),
            MovementContextQuery.SquaredContextRadius(supportRaw));
    }

    /// <summary>
    /// Advances a battle and records the non-authoritative evidence the plan
    /// asks for, without altering a single authoritative field.
    /// </summary>
    private static RunEvidence RunToCompletion(
        Scenario scenario, AgentState[] agents, int ticks)
    {
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);
        var toleratedStepSquared =
            (Int128)(scenario.MovementSpeedRaw + 1) *
            (scenario.MovementSpeedRaw + 1);
        var previous = agents
            .ToDictionary(agent => agent.EntityId, agent => (agent.XRaw, agent.YRaw));
        var eventStream = new List<string>();
        var legalSteps = true;
        var legalPhases = true;
        string? stepFailure = null;
        string? phaseFailure = null;

        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.AdvanceOneTick();

            foreach (var agent in agents)
            {
                if (agent.IsAlive)
                {
                    var (priorX, priorY) = previous[agent.EntityId];
                    var deltaX = (long)agent.XRaw - priorX;
                    var deltaY = (long)agent.YRaw - priorY;
                    var movedSquared =
                        ((Int128)deltaX * deltaX) + ((Int128)deltaY * deltaY);

                    // The shipped step model scales the target delta by
                    // paceRaw divided by a truncated integer square root of
                    // the distance (design section 10.1). Because the root
                    // truncates downward, the per-axis cap is exact while the
                    // Euclidean magnitude may exceed the cap by less than one
                    // raw unit. Both bounds are asserted, at their real
                    // strengths.
                    if (Math.Abs(deltaX) > scenario.MovementSpeedRaw ||
                        Math.Abs(deltaY) > scenario.MovementSpeedRaw)
                    {
                        legalSteps = false;
                        stepFailure ??=
                            $"Agent {agent.EntityId} moved ({deltaX},{deltaY}) " +
                            $"on tick {tick}, exceeding the per-axis baseline " +
                            $"{scenario.MovementSpeedRaw}.";
                    }

                    if (movedSquared > toleratedStepSquared)
                    {
                        legalSteps = false;
                        stepFailure ??=
                            $"Agent {agent.EntityId} moved ({deltaX},{deltaY}) " +
                            $"on tick {tick}, squared {movedSquared}, beyond " +
                            $"the one-raw-unit truncation tolerance " +
                            $"{toleratedStepSquared}.";
                    }
                }

                previous[agent.EntityId] = (agent.XRaw, agent.YRaw);

                if (!Enum.IsDefined(agent.FootworkPhase) ||
                    !Enum.IsDefined(agent.TacticalPosture) ||
                    agent.FootworkTicksRemaining < 0)
                {
                    legalPhases = false;
                    phaseFailure ??=
                        $"Agent {agent.EntityId} on tick {tick} carried " +
                        $"phase {agent.FootworkPhase}, posture " +
                        $"{agent.TacticalPosture}, timer " +
                        $"{agent.FootworkTicksRemaining}.";
                }
            }

            foreach (var battleEvent in simulation.LastEvents)
            {
                eventStream.Add(
                    $"{battleEvent.Sequence}:{battleEvent.Tick}:" +
                    $"{battleEvent.Kind}:{battleEvent.SourceEntityId}:" +
                    $"{battleEvent.TargetEntityId ?? 0}:{battleEvent.Value}");
            }

            if (simulation.Outcome != BattleOutcome.Ongoing)
            {
                break;
            }
        }

        return new RunEvidence(
            simulation.ComputeStateHash(),
            eventStream,
            simulation.Outcome,
            legalSteps,
            legalPhases,
            stepFailure,
            phaseFailure);
    }

    private sealed record RunEvidence(
        ulong StateHash,
        List<string> EventStream,
        BattleOutcome Outcome,
        bool LegalSteps,
        bool LegalPhases,
        string? StepFailure,
        string? PhaseFailure);

    private static Int128 Square(long value) => (Int128)value * value;

    private static (FootworkPhase Phase, int TicksRemaining)
        ResolveKampilanFootwork(
            FootworkPhase priorPhase,
            int priorTicksRemaining,
            TacticalPosture posture,
            int supportAllies,
            int supportEnemies,
            bool hasTarget,
            bool targetAtOrInsidePreferredDistance) =>
        WeaponMovementRules.ResolveProvisionalFootwork(
            isAlive: true,
            priorPhase: priorPhase,
            priorTicksRemaining: priorTicksRemaining,
            posture: posture,
            supportAllies: supportAllies,
            supportEnemies: supportEnemies,
            disengageEnemyToAllyBasisPoints:
                Row.DisengageEnemyToAllyBasisPoints,
            reengageEnemyToAllyBasisPoints:
                Row.ReengageEnemyToAllyBasisPoints,
            recoveryTicks: Row.RecoveryTicks,
            hasTarget: hasTarget,
            targetAtOrInsidePreferredDistance:
                targetAtOrInsidePreferredDistance);

    private static (FootworkPhase Phase, int TicksRemaining) Continue(
        (FootworkPhase Phase, int TicksRemaining) prior) =>
        ResolveKampilanFootwork(
            priorPhase: prior.Phase,
            priorTicksRemaining: prior.TicksRemaining,
            posture: TacticalPosture.Advance,
            supportAllies: 1,
            supportEnemies: 0,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);
}
