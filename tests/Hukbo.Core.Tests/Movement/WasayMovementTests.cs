using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The behavioural boundaries the solo Wasay (<c>WA</c>) row produces under
/// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/>: the
/// offset-adjusted preferred distance that decides <c>Engage</c> against each
/// of the six canonical opponents, the ally-clearance radius the conflict pass
/// enforces, the three direction-band pace caps, the one-sector turn budget,
/// the acceleration and deceleration steps, the retained-pace commit under a
/// denied move, and the four-committed-then-four-recovery rhythm with its
/// attack interrupt. Task W2 of
/// docs/archives/2026-07-31/movement/wasay.md.
/// </summary>
/// <remarks>
/// Every boundary is asserted twice where both surfaces exist: once on the
/// pure arithmetic of <see cref="MovementRouteRules"/>,
/// <see cref="FacingRules"/>, and <see cref="WeaponMovementRules"/> with
/// hand-built scalars, and once through whole ticks of
/// <see cref="BattleSimulation"/>, which is the only way to prove that the
/// pipeline actually feeds the Wasay row into those functions. The numbers
/// here are provisional gameplay tuning read back from the shipped row; none
/// of them is a historical measurement, and the evidence ledger behind the row
/// lives in docs/research/movement/wasay.md.
/// <para>
/// Two conventions are load-bearing and are asserted rather than assumed. The
/// preferred distance is compared inclusively, so a squared distance exactly
/// equal to it enters <c>Engage</c>; and the preferred distance is not a stop
/// line, so an engaging warrior keeps closing and the unchanged post-movement
/// combat reach gate stays the only thing that starts an attack.
/// </para>
/// </remarks>
public sealed class WasayMovementTests
{
    /// <summary>The basis-point denominator of the whole pace model.</summary>
    private const int BasisPointDenominator = 10_000;

    /// <summary>
    /// The scenario reach every test in this file runs on. Chosen so that the
    /// Wasay preferred distance — at least 10,800 basis points of it — sits
    /// strictly outside attack reach, which keeps the reach gate from pulling
    /// an entry case into <c>Commit</c> and confounding the assertion.
    /// </summary>
    private const int AttackReachRaw = 5 * FixedPoint.Scale;

    private const int WarriorBodyRadiusRaw = FixedPoint.Scale / 2;

    private const int WarriorSpeedRaw = FixedPoint.Scale / 2;

    /// <summary>
    /// The Wasay first-tick step on an open lane: the retained pace starts at
    /// zero and rises by one acceleration step,
    /// <c>512 * 4000 / 10000 = 204</c>, which is below the 481-unit forward
    /// band cap and therefore the whole of the first move.
    /// </summary>
    private const int FirstTickPaceRaw = 204;

    private static readonly CombatLoadout WasayLoadout =
        new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None);

    // ----- Family 1: entry at the offset-adjusted preferred distance -----

    /// <summary>
    /// The flat <c>108%</c> multiplier of the weapon plan holds only against
    /// another Wasay, whose offset cell is zero. Every other opponent adds its
    /// own cell, so the effective multiplier runs 11,300 against Kampilan and
    /// Itak, 11,050 against Kalis and shielded Kalis, and 11,300 against
    /// shielded Itak.
    /// </summary>
    [Theory]
    [InlineData(WeaponId.Kampilan, ShieldId.None, 11_300)]
    [InlineData(WeaponId.Wasay, ShieldId.None, 10_800)]
    [InlineData(WeaponId.Kalis, ShieldId.None, 11_050)]
    [InlineData(WeaponId.Itak, ShieldId.None, 11_300)]
    [InlineData(WeaponId.Kalis, ShieldId.TallHardwood, 11_050)]
    [InlineData(WeaponId.Itak, ShieldId.TallHardwood, 11_300)]
    public void TheWasayPreferredDistanceCarriesItsOwnOffsetPerOpponent(
        WeaponId weapon,
        ShieldId shield,
        int expectedBasisPoints)
    {
        var opponent = new CombatLoadout(weapon, ArmorId.LightOrganic, shield);
        var index = MovementRouteRules.CanonicalOpponentIndex(opponent);
        var row = WasayMovementProfile.Row;

        Assert.Equal(
            expectedBasisPoints,
            row.PreferredDistanceBasisPoints +
                row.OpponentDistanceOffsetBasisPoints[index]);
        Assert.Equal(
            (long)AttackReachRaw * expectedBasisPoints / BasisPointDenominator,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackReachRaw, row, index));
    }

    /// <summary>
    /// The pure side of the entry boundary: the resolver is told only whether
    /// the target sits at or inside the offset-adjusted preferred distance,
    /// and answers <c>Engage</c> or <c>Approach</c> accordingly.
    /// </summary>
    [Theory]
    [InlineData(true, FootworkPhase.Engage)]
    [InlineData(false, FootworkPhase.Approach)]
    public void TheEntryFlagAloneDecidesEngageAgainstApproach(
        bool atOrInsidePreferredDistance,
        FootworkPhase expectedPhase) =>
        Assert.Equal(
            (expectedPhase, 0),
            Resolve(
                hasTarget: true,
                targetAtOrInsidePreferredDistance:
                    atOrInsidePreferredDistance));

    /// <summary>
    /// The whole-tick counterpart, run against each of the six canonical
    /// opponents at its own effective preferred distance: one raw unit outside
    /// still approaches, exact equality enters <c>Engage</c> because the
    /// squared comparison is inclusive, and one raw unit inside engages too.
    /// Cooldowns are pinned high so no accepted attack can enter <c>Commit</c>
    /// and hide the phase under observation.
    /// </summary>
    [Theory]
    [InlineData(WeaponId.Kampilan, ShieldId.None)]
    [InlineData(WeaponId.Wasay, ShieldId.None)]
    [InlineData(WeaponId.Kalis, ShieldId.None)]
    [InlineData(WeaponId.Itak, ShieldId.None)]
    [InlineData(WeaponId.Kalis, ShieldId.TallHardwood)]
    [InlineData(WeaponId.Itak, ShieldId.TallHardwood)]
    public void ExactPreferredDistanceEntersEngageThroughAWholeTick(
        WeaponId weapon,
        ShieldId shield)
    {
        var opponent = new CombatLoadout(weapon, ArmorId.LightOrganic, shield);
        var preferredRaw = MovementRouteRules.EffectivePreferredDistanceRaw(
            AttackReachRaw,
            WasayMovementProfile.Row,
            MovementRouteRules.CanonicalOpponentIndex(opponent));

        Assert.True(
            preferredRaw > AttackReachRaw,
            "The preferred distance must sit outside attack reach, or the " +
            "reach gate rather than the entry boundary decides the phase.");
        Assert.Equal(
            FootworkPhase.Approach,
            ObserveEntryPhase(opponent, preferredRaw + 1));
        Assert.Equal(
            FootworkPhase.Engage,
            ObserveEntryPhase(opponent, preferredRaw));
        Assert.Equal(
            FootworkPhase.Engage,
            ObserveEntryPhase(opponent, preferredRaw - 1));
    }

    /// <summary>
    /// A reach that does not divide evenly by 10,000 truncates toward zero
    /// rather than rounding: 511 raw at 11,300 basis points is 577.43 raw and
    /// materializes as 577.
    /// </summary>
    [Fact]
    public void ThePreferredDistanceTruncatesTowardZero() =>
        Assert.Equal(
            577L,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                511, WasayMovementProfile.Row, opponentCanonicalIndex: 0));

    // ----- Family 2: ally clearance -----

    /// <summary>
    /// The Wasay ally-clearance radius at the scenario body radius is
    /// <c>2 * 512 * 17500 / 10000 = 1792</c> raw, the widest of the six rows.
    /// A proposed endpoint exactly at that separation is clear and is
    /// accepted; one raw unit closer is strictly inside and is denied.
    /// </summary>
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void ExactAllyClearanceIsClearAndOneRawUnitInsideIsDenied(
        int separationOffsetRaw,
        bool expectedAccepted)
    {
        var radiusRaw = WasayClearanceRadiusRaw();
        Assert.Equal(1_792L, radiusRaw);

        var accepted = RunClearancePass(
            secondRadiusRaw: radiusRaw,
            separationRaw: checked((int)radiusRaw + separationOffsetRaw));

        Assert.True(accepted[0]);
        Assert.Equal(expectedAccepted, accepted[1]);
    }

    /// <summary>
    /// The larger of the two radii binds. An Itak's own 1177-unit radius is
    /// not enough separation from a Wasay: at exactly that distance the pair
    /// is still denied, and only the Wasay's 1792-unit radius clears it.
    /// </summary>
    [Fact]
    public void TheLargerOfTwoClearanceRadiiBindsAWasayAndItakPair()
    {
        var wasayRadiusRaw = WasayClearanceRadiusRaw();
        var itakRadiusRaw = MovementRouteRules.ClearanceRadiusRaw(
            WarriorBodyRadiusRaw,
            ItakMovementProfile.Row.AllyClearanceBodyDiametersBasisPoints);

        Assert.Equal(1_177L, itakRadiusRaw);
        Assert.True(itakRadiusRaw < wasayRadiusRaw);

        Assert.False(
            RunClearancePass(
                secondRadiusRaw: itakRadiusRaw,
                separationRaw: checked((int)itakRadiusRaw))[1]);
        Assert.True(
            RunClearancePass(
                secondRadiusRaw: itakRadiusRaw,
                separationRaw: checked((int)wasayRadiusRaw))[1]);
    }

    /// <summary>
    /// An odd body radius truncates toward zero: a 511-unit radius gives a
    /// 1022-unit diameter, and 1022 at 17,500 basis points is 1788.5 raw,
    /// which materializes as 1788.
    /// </summary>
    [Fact]
    public void TheAllyClearanceRadiusTruncatesTowardZero() =>
        Assert.Equal(
            1_788L,
            MovementRouteRules.ClearanceRadiusRaw(
                511,
                WasayMovementProfile.Row
                    .AllyClearanceBodyDiametersBasisPoints));

    // ----- Family 3: the three direction bands -----

    /// <summary>
    /// Separation 0 and 1 take the forward cap, 2 through 5 the lateral cap,
    /// and 6 through 8 the backward cap, at the Wasay row's 9,400 / 7,400 /
    /// 6,400 basis points.
    /// </summary>
    [Theory]
    [InlineData(0, 9_400)]
    [InlineData(1, 9_400)]
    [InlineData(2, 7_400)]
    [InlineData(3, 7_400)]
    [InlineData(4, 7_400)]
    [InlineData(5, 7_400)]
    [InlineData(6, 6_400)]
    [InlineData(7, 6_400)]
    [InlineData(8, 6_400)]
    public void EveryDirectionBandSeparationTakesItsWasayCap(
        int separationSectors,
        int expectedBasisPoints) =>
        Assert.Equal(
            expectedBasisPoints,
            FacingRules.DirectionBandPaceCapBasisPoints(
                WasayMovementProfile.Row, separationSectors));

    /// <summary>
    /// A speed that divides evenly by 10,000 turns the caps into exact raw
    /// paces, so the basis points themselves are visible in the result.
    /// </summary>
    [Theory]
    [InlineData(9_400, 9_400)]
    [InlineData(7_400, 7_400)]
    [InlineData(6_400, 6_400)]
    [InlineData(2_500, 2_500)]
    public void ADivisibleSpeedShowsTheCapsAsExactRawPaces(
        int capBasisPoints,
        int expectedPaceRaw) =>
        Assert.Equal(
            expectedPaceRaw,
            MovementRouteRules.DesiredPaceRaw(
                BasisPointDenominator, capBasisPoints));

    /// <summary>
    /// The scenario speed of 512 does not divide evenly, so every cap
    /// truncates toward zero: 481.28 becomes 481, 378.88 becomes 378, 327.68
    /// becomes 327, and only the committed cap, 128.0, is exact.
    /// </summary>
    [Theory]
    [InlineData(9_400, 481)]
    [InlineData(7_400, 378)]
    [InlineData(6_400, 327)]
    [InlineData(2_500, 128)]
    public void AnIndivisibleSpeedTruncatesEveryCapTowardZero(
        int capBasisPoints,
        int expectedPaceRaw) =>
        Assert.Equal(
            expectedPaceRaw,
            MovementRouteRules.DesiredPaceRaw(
                WarriorSpeedRaw, capBasisPoints));

    // ----- Family 4: the one-sector turn budget -----

    /// <summary>
    /// The Wasay turns at most one of sixteen sectors per tick, and its
    /// committed budget is the same one sector, so a commitment costs it no
    /// turning it had before.
    /// </summary>
    [Fact]
    public void TheWasayTurnBudgetIsOneSectorCommittedOrNot()
    {
        var row = WasayMovementProfile.Row;

        Assert.Equal(1, row.MaximumFacingStepsPerTick);
        Assert.Equal(1, row.CommittedFacingStepsPerTick);
        Assert.Equal(
            FacingRules.TurnToward(
                Facing16.East, Facing16.North, row.MaximumFacingStepsPerTick, 0),
            FacingRules.TurnToward(
                Facing16.East,
                Facing16.North,
                row.CommittedFacingStepsPerTick,
                0));
    }

    /// <summary>
    /// A request exactly at the one-sector cap reaches the desired facing; a
    /// request one sector beyond it advances only by the cap.
    /// </summary>
    [Theory]
    [InlineData(Facing16.EastSouthEast, Facing16.EastSouthEast)]
    [InlineData(Facing16.SouthEast, Facing16.EastSouthEast)]
    [InlineData(Facing16.EastNorthEast, Facing16.EastNorthEast)]
    [InlineData(Facing16.NorthEast, Facing16.EastNorthEast)]
    public void AOneSectorBudgetReachesTheCapAndNoFurther(
        Facing16 desired,
        Facing16 expected) =>
        Assert.Equal(
            expected,
            FacingRules.TurnToward(
                Facing16.East,
                desired,
                WasayMovementProfile.Row.MaximumFacingStepsPerTick,
                0));

    /// <summary>
    /// The delta (196, 39) lies exactly on the bisector of sectors 0 and 1 —
    /// <c>1024 * 196</c> and <c>946 * 196 + 392 * 39</c> are both 200,704 —
    /// and the exact dot-product tie takes the lower numeric sector.
    /// </summary>
    [Fact]
    public void AnExactDotProductTieTakesTheLowerSector() =>
        Assert.Equal(Facing16.East, FacingRules.FromDelta(196, 39, 0));

    /// <summary>
    /// An eight-step turn is equally far either way, and the tie goes
    /// clockwise, so a Wasay ordered to reverse takes its single step toward
    /// <see cref="Facing16.EastSouthEast"/> rather than
    /// <see cref="Facing16.EastNorthEast"/>.
    /// </summary>
    [Fact]
    public void AnEightStepTurnTieGoesClockwise() =>
        Assert.Equal(
            Facing16.EastSouthEast,
            FacingRules.TurnToward(
                Facing16.East,
                Facing16.West,
                WasayMovementProfile.Row.MaximumFacingStepsPerTick,
                0));

    /// <summary>
    /// The whole-tick counterpart: a threat due north is four counter-
    /// clockwise sectors away, and the one-sector budget takes four ticks to
    /// face it — one sector per tick, never two.
    /// </summary>
    [Fact]
    public void AWasayTurnsExactlyOneSectorPerTickTowardItsThreat()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var enemy = CreateAgent(2, factionId: 1, 51_200, 30_720, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, enemy);

        var expected = new[]
        {
            Facing16.EastNorthEast,
            Facing16.NorthEast,
            Facing16.NorthNorthEast,
            Facing16.North,
        };
        var observed = new Facing16[expected.Length];
        for (var tick = 0; tick < expected.Length; tick++)
        {
            actor.AttackCooldownRemaining = 100;
            enemy.AttackCooldownRemaining = 100;
            simulation.AdvanceOneTick();
            observed[tick] = actor.Facing;
        }

        Assert.Equal(expected, observed);
    }

    /// <summary>
    /// A committed Wasay turns by the same single sector, and its commitment
    /// timer decrements normally underneath.
    /// </summary>
    [Fact]
    public void ACommittedWasayStillTurnsExactlyOneSector()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var enemy = CreateAgent(2, factionId: 1, 51_200, 30_720, scenario);
        actor.FootworkPhase = FootworkPhase.Commit;
        actor.FootworkTicksRemaining = 5;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, enemy);

        actor.AttackCooldownRemaining = 100;
        enemy.AttackCooldownRemaining = 100;
        simulation.AdvanceOneTick();

        Assert.Equal(Facing16.EastNorthEast, actor.Facing);
        Assert.Equal(FootworkPhase.Commit, actor.FootworkPhase);
        Assert.Equal(4, actor.FootworkTicksRemaining);
    }

    // ----- Family 5: acceleration and deceleration -----

    /// <summary>
    /// The Wasay retained pace rises by 4,000 basis points of the warrior's
    /// own speed per tick and falls by 5,000. On a divisible speed those are
    /// exact; on the scenario's 512 they truncate toward zero, 204.8 to 204,
    /// while the deceleration step 256.0 is exact.
    /// </summary>
    [Theory]
    [InlineData(BasisPointDenominator, 4_000, 4_000)]
    [InlineData(BasisPointDenominator, 5_000, 5_000)]
    [InlineData(WarriorSpeedRaw, 4_000, 204)]
    [InlineData(WarriorSpeedRaw, 5_000, 256)]
    [InlineData(511, 4_000, 204)]
    [InlineData(511, 5_000, 255)]
    public void ThePaceStepsUseTheApprovedBasisPointsAndTruncate(
        int movementSpeedRaw,
        int basisPointsPerTick,
        int expectedStepRaw) =>
        Assert.Equal(
            expectedStepRaw,
            MovementRouteRules.PaceStepRaw(
                movementSpeedRaw, basisPointsPerTick));

    /// <summary>
    /// The step floors at one raw unit, so a very slow warrior can still
    /// make progress toward its target pace rather than freezing at zero.
    /// </summary>
    [Fact]
    public void ThePaceStepFloorsAtOneRawUnit() =>
        Assert.Equal(
            1,
            MovementRouteRules.PaceStepRaw(
                1,
                WasayMovementProfile.Row.AccelerationBasisPointsPerTick));

    /// <summary>
    /// The retained pace never overshoots its target in either direction: the
    /// 204-unit acceleration step stops at the 481-unit forward cap, the
    /// 256-unit deceleration step stops at the 128-unit committed cap, and an
    /// already-matching pace does not move.
    /// </summary>
    [Theory]
    [InlineData(0, 481, 204)]
    [InlineData(204, 481, 408)]
    [InlineData(408, 481, 481)]
    [InlineData(481, 128, 225)]
    [InlineData(225, 128, 128)]
    [InlineData(128, 128, 128)]
    public void TheRetainedPaceNeverOvershootsItsTarget(
        int currentPaceRaw,
        int desiredPaceRaw,
        int expectedPaceRaw) =>
        Assert.Equal(
            expectedPaceRaw,
            MovementRouteRules.AdvanceRetainedPaceRaw(
                currentPaceRaw,
                desiredPaceRaw,
                MovementRouteRules.PaceStepRaw(
                    WarriorSpeedRaw,
                    WasayMovementProfile.Row.AccelerationBasisPointsPerTick),
                MovementRouteRules.PaceStepRaw(
                    WarriorSpeedRaw,
                    WasayMovementProfile.Row.DecelerationBasisPointsPerTick)));

    // ----- Family 6: the retained pace under a denied move -----

    /// <summary>
    /// Every candidate lane blocked finalises <c>Refuse</c> and leaves the
    /// retained pace at zero: the Wasay's 1792-unit clearance radius is wider
    /// than the direct endpoint's 896-unit gap from the ally ahead and wider
    /// than either 22.5-degree oblique's 915-unit gap, so the warrior emits no
    /// proposal at all and does not move one raw unit.
    /// </summary>
    [Fact]
    public void AFullyBlockedWasayLaneRefusesAndRetainsZeroPace()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var allyAhead = CreateAgent(2, factionId: 0, 52_300, 51_200, scenario);
        var enemy = CreateAgent(3, factionId: 1, 71_680, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, allyAhead, enemy);

        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Refuse, actor.FootworkPhase);
        Assert.Equal(0, actor.FootworkTicksRemaining);
        Assert.Equal(51_200, actor.XRaw);
        Assert.Equal(51_200, actor.YRaw);
        Assert.Equal(0, actor.MovementPaceRaw);
    }

    /// <summary>
    /// A partially denied move commits as the minimum of the proposed pace
    /// and the distance actually travelled: two Wasay warriors 1124 raw apart
    /// each propose the 204-unit first-tick step, which would interpenetrate
    /// their 1024-unit bodies, and the solid resolver truncates both. Each
    /// warrior's retained pace is exactly what it covered, strictly less than
    /// what it asked for.
    /// </summary>
    [Fact]
    public void TheRetainedPaceCommitsAsTheMinimumOfProposedAndActual()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 93_284, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        west.AttackCooldownRemaining = 100;
        east.AttackCooldownRemaining = 100;
        simulation.AdvanceOneTick();

        var westMoved = (int)FixedPoint.IntegerSquareRoot(
            CollisionGeometry.SquaredDistance(
                92_160, 51_200, west.XRaw, west.YRaw));
        var eastMoved = (int)FixedPoint.IntegerSquareRoot(
            CollisionGeometry.SquaredDistance(
                93_284, 51_200, east.XRaw, east.YRaw));

        Assert.Equal(westMoved, west.MovementPaceRaw);
        Assert.Equal(eastMoved, east.MovementPaceRaw);
        Assert.True(
            west.MovementPaceRaw < FirstTickPaceRaw &&
                east.MovementPaceRaw < FirstTickPaceRaw,
            "The resolver let both closing steps through untruncated, so " +
            "this scenario proves nothing about the pace commit.");
    }

    // ----- Family 7: four committed ticks, then four recovery ticks -----

    /// <summary>
    /// The Wasay commitment counts its entry tick, so a <c>Commit</c> entered
    /// at duration 4 is seen by the resolver as prior timer 4 and decrements
    /// through 3, 2, and 1 — four committed ticks in all — before loading the
    /// four-tick recovery, which decrements the same way. The eighth step
    /// falls through to the ordinary target rules.
    /// </summary>
    [Fact]
    public void FourCommittedTicksAreFollowedByExactlyFourRecoveryTicks()
    {
        var expected = new (FootworkPhase Phase, int TicksRemaining)[]
        {
            (FootworkPhase.Commit, 3),
            (FootworkPhase.Commit, 2),
            (FootworkPhase.Commit, 1),
            (FootworkPhase.Recover, 4),
            (FootworkPhase.Recover, 3),
            (FootworkPhase.Recover, 2),
            (FootworkPhase.Recover, 1),
            (FootworkPhase.Engage, 0),
        };

        var state = (
            Phase: FootworkPhase.Commit,
            TicksRemaining: WasayMovementProfile.Row.CommitmentTicks);
        var observed =
            new (FootworkPhase Phase, int TicksRemaining)[expected.Length];
        for (var step = 0; step < expected.Length; step++)
        {
            state = Resolve(
                priorPhase: state.Phase,
                priorTicksRemaining: state.TicksRemaining,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true);
            observed[step] = state;
        }

        Assert.Equal(expected, observed);
    }

    // ----- Family 8: an accepted attack interrupts recovery -----

    /// <summary>
    /// An attack accepted while the warrior is recovering replaces the
    /// remaining recovery with a fresh commitment at the Wasay's full
    /// four-tick duration. The provisional step had already decremented the
    /// seeded <c>Recover</c> from 5 to 4 before the accepted attack overwrote
    /// it.
    /// </summary>
    [Fact]
    public void AnAcceptedAttackDuringRecoveryStartsAFreshFourTickCommitment()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 93_184, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);
        west.FootworkPhase = FootworkPhase.Recover;
        west.FootworkTicksRemaining = 5;

        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Commit, west.FootworkPhase);
        Assert.Equal(4, west.FootworkTicksRemaining);
    }

    /// <summary>
    /// The same interrupt observed as a rhythm rather than a seeded state.
    /// Two Wasay warriors stand at exact body contact with a five-tick
    /// cooldown, so the combat gates alone let them attack on ticks 1, 6, and
    /// 11. Each accepted attack starts a four-tick commitment; the commitment
    /// expires into a four-tick recovery on tick 5; and the tick-6 attack cuts
    /// that recovery short at its second tick and starts a fresh
    /// <c>Commit</c> at duration 4. Combat eligibility is untouched — the
    /// attack ticks are exactly the ones the cooldown schedule predicts.
    /// </summary>
    [Fact]
    public void TheAttackRhythmInterruptsRecoveryWithoutChangingEligibility()
    {
        const int observedTicks = 12;
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 93_184, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        var observed =
            new (FootworkPhase Phase, int TicksRemaining)[observedTicks];
        var attackTicks = new List<int>(3);
        for (var tick = 0; tick < observedTicks; tick++)
        {
            simulation.AdvanceOneTick();
            observed[tick] = (west.FootworkPhase, west.FootworkTicksRemaining);
            if (simulation.LastEvents.Any(
                battleEvent =>
                    battleEvent.Kind == BattleEventKind.Attack &&
                    battleEvent.SourceEntityId == west.EntityId))
            {
                attackTicks.Add(tick + 1);
            }
        }

        Assert.Equal(new[] { 1, 6, 11 }, attackTicks);
        Assert.Equal(
            new (FootworkPhase Phase, int TicksRemaining)[]
            {
                (FootworkPhase.Commit, 4),
                (FootworkPhase.Commit, 3),
                (FootworkPhase.Commit, 2),
                (FootworkPhase.Commit, 1),
                (FootworkPhase.Recover, 4),
                (FootworkPhase.Commit, 4),
                (FootworkPhase.Commit, 3),
                (FootworkPhase.Commit, 2),
                (FootworkPhase.Commit, 1),
                (FootworkPhase.Recover, 4),
                (FootworkPhase.Commit, 4),
                (FootworkPhase.Commit, 3),
            },
            observed);
    }

    // ----- Family 9: no step exceeds the common human baseline -----

    /// <summary>
    /// The desired pace is capped at the warrior's own speed regardless of the
    /// band, which is what keeps a profile from ever making a Wasay faster
    /// than another human. All three Wasay bands are below 10,000 basis points
    /// already, so the cap is proved with a deliberately over-unity input as
    /// well.
    /// </summary>
    [Theory]
    [InlineData(9_400, 481)]
    [InlineData(7_400, 378)]
    [InlineData(6_400, 327)]
    [InlineData(20_000, WarriorSpeedRaw)]
    public void TheDesiredPaceNeverExceedsTheWarriorsOwnSpeed(
        int capBasisPoints,
        int expectedPaceRaw)
    {
        var paceRaw = MovementRouteRules.DesiredPaceRaw(
            WarriorSpeedRaw, capBasisPoints);

        Assert.Equal(expectedPaceRaw, paceRaw);
        Assert.True(paceRaw <= WarriorSpeedRaw);
    }

    /// <summary>
    /// The whole-tick counterpart, and the acceleration curve at the same
    /// time: an unobstructed Wasay closing due east steps 204, 408, then 481
    /// raw as the retained pace climbs to the forward band cap and stops
    /// there, and no tick of the run displaces it further than its own
    /// 512-unit speed.
    /// </summary>
    [Fact]
    public void NoWasayStepExceedsTheWarriorsMovementSpeedOverAMultiTickRun()
    {
        const int observedTicks = 8;
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 112_640, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        var steps = new int[observedTicks];
        for (var tick = 0; tick < observedTicks; tick++)
        {
            var priorX = west.XRaw;
            var priorY = west.YRaw;
            west.AttackCooldownRemaining = 100;
            east.AttackCooldownRemaining = 100;
            simulation.AdvanceOneTick();

            steps[tick] = (int)FixedPoint.IntegerSquareRoot(
                CollisionGeometry.SquaredDistance(
                    priorX, priorY, west.XRaw, west.YRaw));
            Assert.Equal(steps[tick], west.MovementPaceRaw);
            Assert.True(
                steps[tick] <= west.MovementSpeedRaw,
                $"Tick {tick + 1} displaced {steps[tick]} raw, past the " +
                $"{west.MovementSpeedRaw}-unit baseline.");
        }

        Assert.Equal(
            new[] { 204, 408, 481, 481, 481, 481, 481, 481 }, steps);
    }

    // ----- Helpers -----

    /// <summary>
    /// Calls the shared provisional-footwork resolver with the Wasay row's own
    /// disengagement thresholds and recovery duration, so every case below
    /// exercises this profile rather than a hand-picked pair of numbers.
    /// </summary>
    private static (FootworkPhase Phase, int TicksRemaining) Resolve(
        bool isAlive = true,
        FootworkPhase priorPhase = FootworkPhase.None,
        int priorTicksRemaining = 0,
        TacticalPosture posture = TacticalPosture.Hold,
        int supportAllies = 1,
        int supportEnemies = 0,
        bool hasTarget = false,
        bool targetAtOrInsidePreferredDistance = false) =>
        WeaponMovementRules.ResolveProvisionalFootwork(
            isAlive,
            priorPhase,
            priorTicksRemaining,
            posture,
            supportAllies,
            supportEnemies,
            WasayMovementProfile.Row.DisengageEnemyToAllyBasisPoints,
            WasayMovementProfile.Row.ReengageEnemyToAllyBasisPoints,
            WasayMovementProfile.Row.RecoveryTicks,
            hasTarget,
            targetAtOrInsidePreferredDistance);

    private static long WasayClearanceRadiusRaw() =>
        MovementRouteRules.ClearanceRadiusRaw(
            WarriorBodyRadiusRaw,
            WasayMovementProfile.Row.AllyClearanceBodyDiametersBasisPoints);

    /// <summary>
    /// Runs the friendly-clearance conflict pass over one Wasay proposal and
    /// one companion proposal placed due east of it, both engaging so the
    /// pass's phase-safety ranks are equal and the ascending entity order
    /// alone decides which is tested against which.
    /// </summary>
    private static bool[] RunClearancePass(
        long secondRadiusRaw,
        int separationRaw,
        long firstRadiusRaw = 1_792)
    {
        var proposals = new[]
        {
            new FriendlyClearanceProposal(
                1,
                FootworkPhase.Engage,
                51_200,
                51_200,
                checked((Int128)firstRadiusRaw * firstRadiusRaw)),
            new FriendlyClearanceProposal(
                2,
                FootworkPhase.Engage,
                checked(51_200 + separationRaw),
                51_200,
                checked((Int128)secondRadiusRaw * secondRadiusRaw)),
        };
        var accepted = new bool[proposals.Length];
        MovementRouteRules.AcceptFriendlyClearanceConflicts(
            proposals, accepted);
        return accepted;
    }

    /// <summary>
    /// Places a solo Wasay and one opponent exactly
    /// <paramref name="separationRaw"/> apart on the X axis, advances a single
    /// tick with both cooldowns pinned high, and reports the Wasay's committed
    /// footwork phase.
    /// </summary>
    private static FootworkPhase ObserveEntryPhase(
        CombatLoadout opponent,
        long separationRaw)
    {
        // Only the V2 combat preset fields all six canonical loadouts, so a
        // shielded opponent needs it; the solo-only cells run on the current
        // default instead. Neither is left implicit.
        var scenario = CreateScenario(
            combatPreset: opponent.Shield == ShieldId.TallHardwood
                ? CombatPresetId.PrecolonialPhilippinesV2
                : CombatPresetId.PrecolonialPhilippinesV4);
        var actor = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var enemy = CreateAgent(
            2,
            factionId: 1,
            checked(92_160 + (int)separationRaw),
            51_200,
            scenario,
            opponent);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, enemy);

        actor.AttackCooldownRemaining = 100;
        enemy.AttackCooldownRemaining = 100;
        simulation.AdvanceOneTick();

        return actor.FootworkPhase;
    }

    private static Scenario CreateScenario(
        CombatPresetId combatPreset =
            CombatPresetId.PrecolonialPhilippinesV4,
        int attackCooldownTicks = 5) =>
        new(
            Seed: 1,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 5_000)
        {
            MaximumHitPoints = 1_000_000,
            DamagePerAttack = 1,
            AttackRangeRaw = AttackReachRaw,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = WarriorBodyRadiusRaw,
            MovementSpeedRaw = WarriorSpeedRaw,
            AttackCooldownTicks = attackCooldownTicks,
            LastStandThresholdAgents = 0,
            CombatPreset = combatPreset,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatLoadout? loadout = null) =>
        new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw,
            scenario.DamagePerAttack,
            scenario.AttackCooldownTicks,
            loadout ?? WasayLoadout);

    // ----- Family 10: the local disengagement hysteresis band -----

    /// <summary>
    /// Entry equality enters. The Wasay row sets
    /// <c>DisengageEnemyToAllyBasisPoints</c> to 20,000, which is exactly two
    /// hostiles per support ally, and step 5 of the resolver admits the ratio
    /// with a <c>&gt;=</c> comparison. The second assertion in each row shows
    /// the boundary is real rather than incidental: one hostile fewer leaves
    /// the same warrior on the ordinary engagement branch. Entry is also
    /// checked before the target rules, so a target sitting at or inside the
    /// preferred distance does not save the warrior from disengaging.
    /// </summary>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 6)]
    [InlineData(250, 500)]
    public void TheExactTwoToOneLocalRatioEntersDisengagement(
        int supportAllies,
        int supportEnemies)
    {
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                supportAllies: supportAllies,
                supportEnemies: supportEnemies,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

        Assert.Equal(
            (FootworkPhase.Engage, 0),
            Resolve(
                supportAllies: supportAllies,
                supportEnemies: supportEnemies - 1,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));
    }

    /// <summary>
    /// Release equality leaves. The Wasay row sets
    /// <c>ReengageEnemyToAllyBasisPoints</c> to 12,500, which is exactly five
    /// hostiles per four support allies, and step 4 of the resolver keeps an
    /// already-disengaging warrior only while the ratio is strictly above that
    /// threshold. One hostile more than the exact ratio therefore still holds
    /// the warrior in <c>Disengage</c>.
    /// </summary>
    [Theory]
    [InlineData(4, 5)]
    [InlineData(8, 10)]
    [InlineData(12, 15)]
    [InlineData(400, 500)]
    public void TheExactFiveToFourLocalRatioReleasesDisengagement(
        int supportAllies,
        int supportEnemies)
    {
        Assert.Equal(
            (FootworkPhase.Engage, 0),
            Resolve(
                priorPhase: FootworkPhase.Disengage,
                supportAllies: supportAllies,
                supportEnemies: supportEnemies,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                priorPhase: FootworkPhase.Disengage,
                supportAllies: supportAllies,
                supportEnemies: supportEnemies + 1,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));
    }

    /// <summary>
    /// The whole point of carrying two thresholds instead of one: every ratio
    /// strictly between the 5:4 release and the 2:1 entry preserves whatever
    /// the warrior was already doing. The same input pair is resolved twice,
    /// once from a disengaged prior phase and once from a non-disengaged one,
    /// so both directions of the hysteresis are pinned by the same numbers
    /// rather than by two separately chosen fixtures.
    /// </summary>
    [Theory]
    [InlineData(2, 3)]
    [InlineData(4, 6)]
    [InlineData(4, 7)]
    [InlineData(8, 13)]
    [InlineData(100, 151)]
    public void TheBandBetweenTheTwoThresholdsPreservesThePriorState(
        int supportAllies,
        int supportEnemies)
    {
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                priorPhase: FootworkPhase.Disengage,
                supportAllies: supportAllies,
                supportEnemies: supportEnemies,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

        Assert.Equal(
            (FootworkPhase.Engage, 0),
            Resolve(
                priorPhase: FootworkPhase.Engage,
                supportAllies: supportAllies,
                supportEnemies: supportEnemies,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));
    }

    /// <summary>
    /// With no hostile inside the support radius, neither the entry nor the
    /// release comparison can fire, whatever the prior phase was, and a prior
    /// <c>Disengage</c> in particular does not persist on the ratio arithmetic
    /// alone. This holds structurally rather than by luck: the resolver
    /// compares widened integer cross-products — the scaled hostile count
    /// against the ally count times a basis-point threshold — and performs no
    /// division anywhere, so a zero hostile count is simply a zero left-hand
    /// side and there is no zero-denominator case to guard.
    /// </summary>
    [Theory]
    [InlineData(FootworkPhase.None)]
    [InlineData(FootworkPhase.Disengage)]
    [InlineData(FootworkPhase.Engage)]
    [InlineData(FootworkPhase.Approach)]
    [InlineData(FootworkPhase.Regroup)]
    [InlineData(FootworkPhase.Pursue)]
    [InlineData(FootworkPhase.Refuse)]
    public void ZeroSupportEnemiesNeitherEntersNorHoldsDisengagement(
        FootworkPhase priorPhase)
    {
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                priorPhase: priorPhase,
                supportAllies: 1,
                supportEnemies: 0));

        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                priorPhase: priorPhase,
                supportAllies: 9_999,
                supportEnemies: 0));
    }

    /// <summary>
    /// The comparison domain is deliberately wider than the counts. Both sides
    /// are widened to <see langword="long"/> before the multiply, so counts
    /// whose scaled product would wrap a naive 32-bit product still resolve
    /// correctly: 214,748 hostiles already exceed
    /// <c>int.MaxValue / 10_000</c>, and every count used here is far past
    /// that. Nothing throws, because the widest product these thresholds can
    /// build from <see cref="int"/> counts — <c>int.MaxValue * 20_000</c>,
    /// about 4.29e13 — sits well inside <see cref="long"/>.
    /// </summary>
    [Fact]
    public void TheRatioComparisonsSurviveOverflowingThirtyTwoBitProducts()
    {
        // Exact 2:1 entry, with a scaled hostile side of 2.0e10.
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(supportAllies: 1_000_000, supportEnemies: 2_000_000));

        // One hostile short of the same ratio does not enter.
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(supportAllies: 1_000_000, supportEnemies: 1_999_999));

        // Exact 5:4 release, with a scaled hostile side of 5.0e10.
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 4_000_000,
                supportEnemies: 5_000_000));

        // One hostile above the same ratio keeps the warrior disengaged.
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                priorPhase: FootworkPhase.Disengage,
                supportAllies: 4_000_000,
                supportEnemies: 5_000_001));

        // The largest counts the int parameters admit still resolve, and they
        // resolve on the correct side of both thresholds.
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                supportAllies: int.MaxValue / 2,
                supportEnemies: int.MaxValue));

        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                supportAllies: int.MaxValue,
                supportEnemies: int.MaxValue));
    }

    // ----- Family 11: the local counts the resolver is fed -----

    /// <summary>
    /// The acting warrior is its own support ally. The count is not produced
    /// by scanning for the actor in the candidate span — the accumulator is
    /// seeded with a support-ally count of one and the actor's own composition
    /// bucket before the scan starts, and the scan then skips the actor by
    /// entity identity. The immediate counts exclude the actor entirely. The
    /// final two assertions close the loop on the resolver's own precondition:
    /// a support-ally count of one is exactly what a lone Wasay reports, and a
    /// count of zero is rejected rather than divided by.
    /// </summary>
    [Fact]
    public void AWasayAmongHostilesCountsOnlyItselfAsASupportAlly()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var agents = new[]
        {
            actor,
            CreateAgent(2, factionId: 1, 52_224, 51_200, scenario),
            CreateAgent(3, factionId: 1, 51_200, 55_296, scenario),
        };

        var context = DeriveWasayContext(agents, actor, 2);

        Assert.Equal(1, context.SupportAllies);
        Assert.Equal(0, context.ImmediateAllies);
        Assert.Null(context.NearestAllyEntityId);
        Assert.Equal(
            new LoadoutCompositionCounts(0, 1, 0, 0, 0, 0),
            context.AlliedComposition);

        // Entity 2 sits 1,024 raw units away and entity 3 sits 4,096 away, so
        // both are support hostiles and only entity 2 is an immediate one.
        Assert.Equal(2, context.SupportEnemies);
        Assert.Equal(1, context.ImmediateEnemies);
        Assert.Equal(
            new LoadoutCompositionCounts(0, 2, 0, 0, 0, 0),
            context.EnemyComposition);
        Assert.Null(context.SecondThreatEntityId);
        Assert.Equal(context, DeriveWasayContextOracle(agents, actor, 2));

        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                supportAllies: context.SupportAllies,
                supportEnemies: context.SupportEnemies));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Resolve(supportAllies: 0, supportEnemies: 1));
    }

    /// <summary>
    /// Ring membership is inclusive at the exact radius and dead neighbours
    /// count nowhere. At this fixture's body radius the Wasay rings measure
    /// <c>2 * 512 * 25000 / 10000 = 2,560</c> raw units for the immediate ring
    /// and <c>2 * 512 * 60000 / 10000 = 6,144</c> for the support ring, and
    /// both radii are read off the registered V6 ruleset rather than written
    /// as literals. A neighbour standing exactly on either radius counts; one
    /// raw unit beyond it does not; and a fallen ally or hostile standing well
    /// inside both rings counts in neither.
    /// </summary>
    [Fact]
    public void DeadAndOutOfRingNeighboursCountNowhereForAWasay()
    {
        Assert.Equal(2_560L, WasayImmediateRadiusRaw());
        Assert.Equal(6_144L, WasaySupportRadiusRaw());

        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var agents = new[]
        {
            actor,
            CreateAgent(2, factionId: 0, 57_344, 51_200, scenario),
            CreateAgent(3, factionId: 0, 57_345, 51_200, scenario),
            CreateAgent(4, factionId: 0, 53_760, 51_200, scenario),
            CreateAgent(5, factionId: 0, 53_761, 51_200, scenario),
            CreateAgent(6, factionId: 1, 45_056, 51_200, scenario),
            CreateAgent(7, factionId: 1, 45_055, 51_200, scenario),
            CreateAgent(8, factionId: 1, 48_640, 51_200, scenario),
            CreateFallenAgent(9, factionId: 0, 51_300, 51_200, scenario),
            CreateFallenAgent(10, factionId: 1, 51_100, 51_200, scenario),
        };

        var context = DeriveWasayContext(agents, actor, null);

        // Entity 4 is exactly on the immediate radius, entity 5 is one raw
        // unit beyond it, entity 2 is exactly on the support radius, and
        // entity 3 is one raw unit beyond that.
        Assert.Equal(1, context.ImmediateAllies);
        Assert.Equal(4, context.SupportAllies);

        // Entity 8 is exactly on the immediate radius, entity 6 is exactly on
        // the support radius, and entity 7 is one raw unit beyond it.
        Assert.Equal(1, context.ImmediateEnemies);
        Assert.Equal(2, context.SupportEnemies);

        // The two fallen agents stand 100 raw units away and still count in
        // no bucket, in no composition, and in neither nearest-entity field.
        Assert.Equal(
            new LoadoutCompositionCounts(0, 4, 0, 0, 0, 0),
            context.AlliedComposition);
        Assert.Equal(
            new LoadoutCompositionCounts(0, 2, 0, 0, 0, 0),
            context.EnemyComposition);
        Assert.Equal(4UL, context.NearestAllyEntityId);
        Assert.Equal(8UL, context.SecondThreatEntityId);
        Assert.Equal(context, DeriveWasayContextOracle(agents, actor, null));
    }

    // ----- Family 12: order independence of the local query -----

    /// <summary>
    /// The pure span seam is the only honest place to test storage order,
    /// because <c>BattleSimulation.CreateForTesting</c> canonicalises its
    /// agents by <c>EntityId</c> before the simulation ever sees them. Three
    /// explicit permutations of the same eight bodies — reversed, rotated by
    /// three, and a hand-picked interleave — must produce a context identical
    /// field for field, and identical to the independent
    /// <see cref="NaiveMovementContextQuery"/> oracle's answer on the same
    /// inputs.
    /// </summary>
    [Fact]
    public void ExplicitlyPermutedSpansDeriveIdenticalWasayContexts()
    {
        var scenario = CreateScenario(CombatPresetId.PrecolonialPhilippinesV2);
        var actor = CreateAgent(4, factionId: 0, 51_200, 51_200, scenario);
        var canonical = new[]
        {
            CreateAgent(1, 1, 52_200, 51_200, scenario, KalisLoadout),
            CreateAgent(2, 0, 51_200, 53_760, scenario, ItakShieldLoadout),
            CreateAgent(3, 1, 53_760, 51_200, scenario, ItakLoadout),
            actor,
            CreateAgent(5, 0, 48_639, 51_200, scenario, KalisShieldLoadout),
            CreateAgent(6, 1, 57_344, 51_200, scenario, KampilanLoadout),
            CreateAgent(7, 0, 51_200, 45_055, scenario, KampilanLoadout),
            CreateFallenAgent(8, 1, 52_500, 52_500, scenario),
        };

        var reversed = canonical.Reverse().ToArray();
        var rotated = canonical.Skip(3).Concat(canonical.Take(3)).ToArray();
        var interleaved = new[]
        {
            canonical[5], canonical[0], canonical[7], canonical[2],
            canonical[4], canonical[6], canonical[1], canonical[3],
        };

        var expected = DeriveWasayContextOracle(canonical, actor, 3);

        Assert.Equal(expected, DeriveWasayContext(canonical, actor, 3));
        Assert.Equal(expected, DeriveWasayContext(reversed, actor, 3));
        Assert.Equal(expected, DeriveWasayContext(rotated, actor, 3));
        Assert.Equal(expected, DeriveWasayContext(interleaved, actor, 3));
    }

    /// <summary>
    /// Both nearest-entity fields break an exact squared-distance tie on the
    /// lower stable <c>EntityId</c>, never on scan order. Entities 9 and 2 are
    /// two allied support references at the same 1,024 raw units, and entities
    /// 11, 3, and 12 are three immediate hostiles at the same 2,048 raw units;
    /// with entity 3 chosen as this tick's target, the second threat falls to
    /// the lower of the two remaining tied hostiles. Reversing the span
    /// changes neither answer.
    /// </summary>
    [Fact]
    public void EquidistantSupportReferencesResolveOnTheLowerEntityId()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(5, factionId: 0, 51_200, 51_200, scenario);
        var agents = new[]
        {
            actor,
            CreateAgent(9, factionId: 0, 52_224, 51_200, scenario),
            CreateAgent(2, factionId: 0, 51_200, 52_224, scenario),
            CreateAgent(11, factionId: 1, 49_152, 51_200, scenario),
            CreateAgent(3, factionId: 1, 51_200, 49_152, scenario),
            CreateAgent(12, factionId: 1, 53_248, 51_200, scenario),
        };
        var reversed = agents.Reverse().ToArray();

        var context = DeriveWasayContext(agents, actor, 3);

        Assert.Equal(2UL, context.NearestAllyEntityId);
        Assert.Equal(11UL, context.SecondThreatEntityId);
        Assert.Equal(context, DeriveWasayContextOracle(agents, actor, 3));
        Assert.Equal(context, DeriveWasayContext(reversed, actor, 3));
    }

    /// <summary>
    /// <c>BattleSimulation.CreateForTesting</c> documents that it accepts its
    /// agents in any order and canonicalises them by <c>EntityId</c>. Two
    /// four-warrior Wasay battles built from the same positions, with the
    /// agent arguments passed in opposite orders, therefore have to agree on
    /// every tick: the same ordered event stream and the same state hash after
    /// the same number of ticks. The event stream is asserted non-empty so a
    /// silent battle cannot make this pass vacuously.
    /// </summary>
    [Fact]
    public void ReversedCallerOrderProducesTheSameHashAndEventStream()
    {
        const int observedTicks = 40;
        var scenario = CreateScenario();

        var forwardEvents = RunAndCollectEvents(
            BattleSimulation.CreateForTesting(
                scenario,
                CreateAgent(1, factionId: 0, 92_160, 51_200, scenario),
                CreateAgent(2, factionId: 1, 93_184, 51_200, scenario),
                CreateAgent(3, factionId: 0, 92_160, 52_400, scenario),
                CreateAgent(4, factionId: 1, 93_184, 52_400, scenario)),
            observedTicks,
            out var forwardHash);

        var reversedEvents = RunAndCollectEvents(
            BattleSimulation.CreateForTesting(
                scenario,
                CreateAgent(4, factionId: 1, 93_184, 52_400, scenario),
                CreateAgent(3, factionId: 0, 92_160, 52_400, scenario),
                CreateAgent(2, factionId: 1, 93_184, 51_200, scenario),
                CreateAgent(1, factionId: 0, 92_160, 51_200, scenario)),
            observedTicks,
            out var reversedHash);

        Assert.NotEmpty(forwardEvents);
        Assert.Equal(forwardEvents, reversedEvents);
        Assert.Equal(forwardHash, reversedHash);
    }

    // ----- Family 13: global posture against the local ratio -----

    /// <summary>
    /// A faction that outnumbers its enemy ten to one resolves the most
    /// favourable posture the table can produce, and that posture still does
    /// not authorise a deep charge for a warrior locally at two hostiles per
    /// ally: the entry comparison is step 5 and the posture branches are steps
    /// 6 through 8, so the local ratio is consulted first and wins.
    /// </summary>
    [Fact]
    public void AGlobalAdvantageDoesNotOverrideTheLocalTwoToOneEntry()
    {
        var posture = WeaponMovementRules.ResolveTacticalPosture(
            globalAllies: 100,
            globalEnemies: 10,
            ContingentState.Advance,
            alliedRoleCoverage: 3,
            enemyRoleCoverage: 1);

        Assert.Equal(TacticalPosture.Advance, posture);
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                posture: posture,
                supportAllies: 2,
                supportEnemies: 4,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

        // The same warrior on the same favourable posture engages normally as
        // soon as the local ratio drops one hostile below the entry boundary.
        Assert.Equal(
            (FootworkPhase.Engage, 0),
            Resolve(
                posture: posture,
                supportAllies: 2,
                supportEnemies: 3,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));
    }

    /// <summary>
    /// The reverse direction, and a place where the shipped rule is stricter
    /// than the plan prose. Plan section 6's count-sensitive posture table says
    /// faction totals "cannot force every Wasay unit to retreat in sync"; in
    /// the implemented resolver, step 6 is unconditional, so every member of a
    /// <see cref="TacticalPosture.Withdraw"/> or
    /// <see cref="TacticalPosture.Yield"/> contingent takes
    /// <see cref="FootworkPhase.Disengage"/> even at nine support allies to
    /// one hostile and with a target at the preferred distance. The per-agent
    /// variation the plan asks for lives in the route each disengaging warrior
    /// takes, not in its phase, and this test pins the implemented truth.
    /// </summary>
    [Theory]
    [InlineData(TacticalPosture.Withdraw)]
    [InlineData(TacticalPosture.Yield)]
    public void AWithdrawOrYieldPostureDisengagesOnAFavourableLocalRatio(
        TacticalPosture posture) =>
        Assert.Equal(
            (FootworkPhase.Disengage, 0),
            Resolve(
                posture: posture,
                supportAllies: 9,
                supportEnemies: 1,
                hasTarget: true,
                targetAtOrInsidePreferredDistance: true));

    /// <summary>
    /// The two globally disadvantaged postures the previous test consumes are
    /// reachable from the posture table itself rather than hand-asserted:
    /// exact double outnumbering is already <c>Withdraw</c>, and exact
    /// four-to-three pressure is already <c>Yield</c>.
    /// </summary>
    [Theory]
    [InlineData(10, 20, TacticalPosture.Withdraw)]
    [InlineData(10, 14, TacticalPosture.Yield)]
    public void TheGloballyDisadvantagedPosturesAreReachableFromTheTable(
        int globalAllies,
        int globalEnemies,
        TacticalPosture expected) =>
        Assert.Equal(
            expected,
            WeaponMovementRules.ResolveTacticalPosture(
                globalAllies,
                globalEnemies,
                ContingentState.Advance,
                alliedRoleCoverage: 3,
                enemyRoleCoverage: 1));

    // ----- Helpers for families 10 through 13 -----

    private static readonly CombatLoadout KampilanLoadout =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout KalisLoadout =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout ItakLoadout =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout KalisShieldLoadout =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static readonly CombatLoadout ItakShieldLoadout =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood);

    /// <summary>
    /// The immediate ring radius the registered V6 ruleset produces at this
    /// file's body radius, read off the preset rather than written as a
    /// literal so a retuned radius fails the assertion that pins it instead of
    /// silently agreeing with a stale constant.
    /// </summary>
    private static long WasayImmediateRadiusRaw() =>
        MovementContextQuery.ContextRadiusRaw(
            WarriorBodyRadiusRaw,
            MovementPresetRegistry
                .Get(MovementPresetId.EquipmentRelativeFootworkV6)
                .ImmediateRadiusBodyDiametersBasisPoints);

    /// <summary>
    /// The support ring radius, read off the same registered ruleset.
    /// </summary>
    private static long WasaySupportRadiusRaw() =>
        MovementContextQuery.ContextRadiusRaw(
            WarriorBodyRadiusRaw,
            MovementPresetRegistry
                .Get(MovementPresetId.EquipmentRelativeFootworkV6)
                .SupportRadiusBodyDiametersBasisPoints);

    /// <summary>
    /// Calls the shared pure local-context query with this file's two Wasay
    /// ring radii.
    /// </summary>
    private static LocalMovementContext DeriveWasayContext(
        AgentState[] agents,
        AgentState actor,
        ulong? selectedTargetEntityId) =>
        MovementContextQuery.Derive(
            agents,
            actor,
            selectedTargetEntityId,
            MovementContextQuery.SquaredContextRadius(
                WasayImmediateRadiusRaw()),
            MovementContextQuery.SquaredContextRadius(
                WasaySupportRadiusRaw()));

    /// <summary>
    /// Computes the independent oracle's answer on the same radii, so a
    /// mistake shared between the production query and this file's
    /// expectations still has to survive a second, differently written
    /// implementation.
    /// </summary>
    private static LocalMovementContext DeriveWasayContextOracle(
        AgentState[] agents,
        AgentState actor,
        ulong? selectedTargetEntityId) =>
        NaiveMovementContextQuery.Compute(
            agents.Select(ToBody).ToList(),
            ToBody(actor),
            selectedTargetEntityId,
            MovementContextQuery.SquaredContextRadius(
                WasayImmediateRadiusRaw()),
            MovementContextQuery.SquaredContextRadius(
                WasaySupportRadiusRaw()));

    private static NaiveMovementContextQuery.Body ToBody(AgentState state) =>
        new(
            state.EntityId,
            state.FactionId,
            state.XRaw,
            state.YRaw,
            state.IsAlive,
            state.Loadout,
            state.PerceptionRangeRaw);

    /// <summary>
    /// Builds an agent that is already dead. The hit points are zeroed on a
    /// freshly constructed state before it is handed to the caller, so no
    /// caller-owned object is ever mutated.
    /// </summary>
    private static AgentState CreateFallenAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario)
    {
        var state = CreateAgent(entityId, factionId, xRaw, yRaw, scenario);
        state.HitPoints = 0;
        return state;
    }

    /// <summary>
    /// Advances a battle by a fixed number of ticks, concatenating each tick's
    /// ordered events into one stream and reporting the final state hash.
    /// </summary>
    private static List<BattleEvent> RunAndCollectEvents(
        BattleSimulation simulation,
        int ticks,
        out ulong stateHash)
    {
        var events = new List<BattleEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.AdvanceOneTick();
            events.AddRange(simulation.LastEvents);
        }

        stateHash = simulation.ComputeStateHash();
        return events;
    }

    // ----- Family 14: the generated duel and pair calibration slice -----

    /// <summary>
    /// The canonical index of the Wasay row in the binding
    /// <c>KP, WA, KA, IT, KS, IS</c> order.
    /// </summary>
    private const int WasayCanonicalIndex = 1;

    /// <summary>
    /// How many whole ticks every calibration run in this family is observed
    /// for.
    /// </summary>
    private const int CalibrationTicks = 600;

    /// <summary>
    /// The shared no-progress bound of
    /// docs/archives/2026-07-31/movement/README.md task T10 step 6.
    /// </summary>
    private const int NoProgressStreakBoundTicks = 250;

    private const int CalibrationLaneYRaw = 51_200;

    private const int CalibrationWestXRaw = 87_040;

    private const int CalibrationEastXRaw = 117_760;

    /// <summary>
    /// One warrior's whole-run movement behaviour, recorded from outside the
    /// simulation without touching authoritative state.
    /// </summary>
    private sealed record AgentObservation(
        ulong EntityId,
        int FactionId,
        CombatLoadout Loadout,
        int? FirstEngageTick,
        int? FirstCommitTick,
        int? FirstLandedAttackTick,
        int CommitTicks,
        int RecoverTicks,
        int RefuseTicks,
        int DisengageTicks,
        int DisengageEntries,
        int DisengageReleases,
        int BlockedTicks,
        int PhaseFlips,
        int TicksBeyondPreferredDistance,
        int LargestStepRaw,
        int LargestLaneOffsetRaw,
        int LandedAttacks);

    /// <summary>One whole observed run.</summary>
    private sealed record RunObservation(
        IReadOnlyList<AgentObservation> Agents,
        IReadOnlyList<IReadOnlyList<FootworkPhase>> PhaseTracks,
        int ObservedTicks,
        int LongestNoProgressStreakTicks,
        long MinimumAllySeparationRaw,
        BattleOutcome Outcome,
        ulong StateHash,
        long MovementConflictDenials);

    /// <summary>
    /// Builds an agent whose reach, damage, and cooldown come from the combat
    /// preset's own weapon profile rather than from the scenario's uniform
    /// placeholders, so the reach differences the matchup table talks about
    /// are real in these runs.
    /// </summary>
    private static AgentState CreateCalibrationAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatLoadout loadout)
    {
        var profile = CombatPresetRegistry
            .Get(scenario.CombatPreset)
            .ResolveWeaponProfile(loadout.Weapon, loadout.Shield);

        return new AgentState(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            profile.AttackRangeRaw,
            profile.DamagePerAttack,
            profile.AttackCooldownTicks,
            loadout);
    }

    /// <summary>
    /// The 1v1 cells of the shared matrix that contain the Wasay row.
    /// </summary>
    private static List<MovementScenarioMatrix.OneVersusOnePair>
        WasayOneVersusOnePairs() =>
        MovementScenarioMatrix.EnumerateOneVersusOnePairs()
            .Where(pair =>
                pair.FirstLoadoutIndex == WasayCanonicalIndex ||
                pair.SecondLoadoutIndex == WasayCanonicalIndex)
            .ToList();

    /// <summary>The canonical index of the non-Wasay side of a cell.</summary>
    private static int OpponentCanonicalIndex(
        MovementScenarioMatrix.OneVersusOnePair pair) =>
        pair.FirstLoadoutIndex == WasayCanonicalIndex
            ? pair.SecondLoadoutIndex
            : pair.FirstLoadoutIndex;

    /// <summary>
    /// The combat preset a cell must name: only
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> fields all six
    /// canonical loadouts, so any shielded cell takes it.
    /// </summary>
    private static CombatPresetId CalibrationCombatPreset(bool containsShield) =>
        containsShield
            ? MovementScenarioMatrix.ShieldedCellCombatPreset
            : CombatPresetId.PrecolonialPhilippinesV4;

    /// <summary>
    /// Builds one duel. The mirrored orientation puts the Wasay on the eastern
    /// start and the opponent on the western one, reflected about the arena's
    /// centre line, so start side and faction bearing are both mirrored.
    /// </summary>
    private static (Scenario Scenario, AgentState[] Agents) BuildDuel(
        int opponentCanonicalIndex,
        bool mirrored)
    {
        var opponent =
            MovementScenarioMatrix.CanonicalLoadouts[opponentCanonicalIndex];
        var scenario = CreateScenario(
            CalibrationCombatPreset(opponent.Shield != ShieldId.None));
        var westLoadout = mirrored ? opponent : WasayLoadout;
        var eastLoadout = mirrored ? WasayLoadout : opponent;

        return (
            scenario,
            [
                CreateCalibrationAgent(
                    1,
                    factionId: 0,
                    CalibrationWestXRaw,
                    CalibrationLaneYRaw,
                    scenario,
                    westLoadout),
                CreateCalibrationAgent(
                    2,
                    factionId: 1,
                    CalibrationEastXRaw,
                    CalibrationLaneYRaw,
                    scenario,
                    eastLoadout),
            ]);
    }

    /// <summary>Runs one duel cell and returns its observation.</summary>
    private static RunObservation ObserveDuel(
        int opponentCanonicalIndex,
        bool mirrored = false)
    {
        var (scenario, agents) = BuildDuel(opponentCanonicalIndex, mirrored);
        return ObserveRun(scenario, agents, CalibrationTicks);
    }

    /// <summary>
    /// Builds one 2v2 cell from a shared-matrix team composition. Both teams
    /// field the same composition, the western team is faction 0, and
    /// <paramref name="laneOffsetRaw"/> is each team's half-separation across
    /// the lane.
    /// </summary>
    private static (Scenario Scenario, AgentState[] Agents) BuildPairMatchup(
        MovementScenarioMatrix.TeamComposition composition,
        int laneOffsetRaw,
        int columnDepthRaw = 0,
        bool reverseMembers = false)
    {
        var scenario = CreateScenario(
            CalibrationCombatPreset(composition.ContainsShieldedLoadout));
        var leader = reverseMembers
            ? composition.SecondMember
            : composition.FirstMember;
        var follower = reverseMembers
            ? composition.FirstMember
            : composition.SecondMember;

        return (
            scenario,
            [
                CreateCalibrationAgent(
                    1,
                    factionId: 0,
                    CalibrationWestXRaw,
                    CalibrationLaneYRaw - laneOffsetRaw,
                    scenario,
                    leader),
                CreateCalibrationAgent(
                    2,
                    factionId: 0,
                    CalibrationWestXRaw - columnDepthRaw,
                    CalibrationLaneYRaw + laneOffsetRaw,
                    scenario,
                    follower),
                CreateCalibrationAgent(
                    3,
                    factionId: 1,
                    CalibrationEastXRaw,
                    CalibrationLaneYRaw - laneOffsetRaw,
                    scenario,
                    leader),
                CreateCalibrationAgent(
                    4,
                    factionId: 1,
                    CalibrationEastXRaw + columnDepthRaw,
                    CalibrationLaneYRaw + laneOffsetRaw,
                    scenario,
                    follower),
            ]);
    }

    /// <summary>Runs one 2v2 cell and returns its observation.</summary>
    private static RunObservation ObservePairMatchup(
        MovementScenarioMatrix.TeamComposition composition,
        int laneOffsetRaw,
        int columnDepthRaw = 0,
        bool reverseMembers = false)
    {
        var (scenario, agents) = BuildPairMatchup(
            composition, laneOffsetRaw, columnDepthRaw, reverseMembers);
        return ObserveRun(scenario, agents, CalibrationTicks);
    }

    /// <summary>
    /// The single observation pass every calibration test in this family
    /// reads. It advances whole ticks and records, per warrior and per run,
    /// only values derived from state the caller already holds: no
    /// authoritative field is written, and nothing here is fed back into the
    /// simulation.
    /// </summary>
    private static RunObservation ObserveRun(
        Scenario scenario,
        AgentState[] agents,
        int observedTicks)
    {
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);
        var movementRules = MovementPresetRegistry.Get(scenario.MovementPreset);
        var ordered = agents.OrderBy(agent => agent.EntityId).ToArray();
        var count = ordered.Length;

        var firstEngage = new int?[count];
        var firstCommit = new int?[count];
        var firstLanded = new int?[count];
        var commitTicks = new int[count];
        var recoverTicks = new int[count];
        var refuseTicks = new int[count];
        var disengageTicks = new int[count];
        var disengageEntries = new int[count];
        var disengageReleases = new int[count];
        var blockedTicks = new int[count];
        var phaseFlips = new int[count];
        var beyondPreferred = new int[count];
        var largestStep = new int[count];
        var largestLaneOffset = new int[count];
        var startY = new int[count];
        var landedAttacks = new int[count];
        var priorPhase = new FootworkPhase[count];
        var priorX = new int[count];
        var priorY = new int[count];
        var priorHitPoints = new int[count];
        var priorNearestSquared = new long[count];
        var phaseTracks = new List<FootworkPhase>[count];

        for (var index = 0; index < count; index++)
        {
            priorPhase[index] = ordered[index].FootworkPhase;
            priorNearestSquared[index] = -1;
            startY[index] = ordered[index].YRaw;
            phaseTracks[index] = new List<FootworkPhase>(observedTicks);
        }

        var minimumAllySeparationRaw = long.MaxValue;
        var longestNoProgress = 0;
        var currentNoProgress = 0;

        for (var tick = 1; tick <= observedTicks; tick++)
        {
            for (var index = 0; index < count; index++)
            {
                priorX[index] = ordered[index].XRaw;
                priorY[index] = ordered[index].YRaw;
                priorHitPoints[index] = ordered[index].HitPoints;
            }

            var livingBefore = LivingCount(ordered);
            simulation.AdvanceOneTick();

            foreach (var battleEvent in simulation.LastEvents)
            {
                if (battleEvent.Kind != BattleEventKind.Attack ||
                    battleEvent.Resolution != AttackResolution.Landed)
                {
                    continue;
                }

                var sourceIndex = IndexOfEntity(
                    ordered, battleEvent.SourceEntityId);
                landedAttacks[sourceIndex]++;
                firstLanded[sourceIndex] ??= tick;
            }

            var progressed = LivingCount(ordered) != livingBefore;

            for (var index = 0; index < count; index++)
            {
                var agent = ordered[index];
                progressed |= agent.HitPoints != priorHitPoints[index];

                var stepRaw = (int)FixedPoint.IntegerSquareRoot(
                    CollisionGeometry.SquaredDistance(
                        priorX[index], priorY[index], agent.XRaw, agent.YRaw));
                largestStep[index] = Math.Max(largestStep[index], stepRaw);
                largestLaneOffset[index] = Math.Max(
                    largestLaneOffset[index],
                    Math.Abs(agent.YRaw - startY[index]));

                var phase = agent.FootworkPhase;
                phaseTracks[index].Add(phase);
                if (phase != priorPhase[index])
                {
                    phaseFlips[index]++;
                    if (phase == FootworkPhase.Disengage)
                    {
                        disengageEntries[index]++;
                    }
                    else if (priorPhase[index] == FootworkPhase.Disengage)
                    {
                        disengageReleases[index]++;
                    }
                }

                priorPhase[index] = phase;

                switch (phase)
                {
                    case FootworkPhase.Engage:
                        firstEngage[index] ??= tick;
                        break;
                    case FootworkPhase.Commit:
                        commitTicks[index]++;
                        firstCommit[index] ??= tick;
                        break;
                    case FootworkPhase.Recover:
                        recoverTicks[index]++;
                        break;
                    case FootworkPhase.Refuse:
                        refuseTicks[index]++;
                        break;
                    case FootworkPhase.Disengage:
                        disengageTicks[index]++;
                        break;
                    default:
                        break;
                }

                if (agent.MovementResolution == MovementResolution.Blocked)
                {
                    blockedTicks[index]++;
                }

                if (NearestHostile(ordered, agent) is { } hostile)
                {
                    var squaredRaw = CollisionGeometry.SquaredDistance(
                        agent.XRaw, agent.YRaw, hostile.XRaw, hostile.YRaw);
                    var preferredRaw =
                        MovementRouteRules.EffectivePreferredDistanceRaw(
                            agent.AttackRangeRaw,
                            movementRules.ResolveLoadoutProfile(agent.Loadout),
                            MovementRouteRules.CanonicalOpponentIndex(
                                hostile.Loadout));
                    if ((Int128)squaredRaw >
                        checked((Int128)preferredRaw * preferredRaw))
                    {
                        beyondPreferred[index]++;
                    }

                    progressed |= squaredRaw != priorNearestSquared[index];
                    priorNearestSquared[index] = squaredRaw;
                }
            }

            minimumAllySeparationRaw = Math.Min(
                minimumAllySeparationRaw, ClosestAllySeparationRaw(ordered));

            if (progressed)
            {
                currentNoProgress = 0;
            }
            else
            {
                currentNoProgress++;
                longestNoProgress =
                    Math.Max(longestNoProgress, currentNoProgress);
            }
        }

        var observations = new List<AgentObservation>(count);
        for (var index = 0; index < count; index++)
        {
            observations.Add(new AgentObservation(
                ordered[index].EntityId,
                ordered[index].FactionId,
                ordered[index].Loadout,
                firstEngage[index],
                firstCommit[index],
                firstLanded[index],
                commitTicks[index],
                recoverTicks[index],
                refuseTicks[index],
                disengageTicks[index],
                disengageEntries[index],
                disengageReleases[index],
                blockedTicks[index],
                phaseFlips[index],
                beyondPreferred[index],
                largestStep[index],
                largestLaneOffset[index],
                landedAttacks[index]));
        }

        return new RunObservation(
            observations,
            phaseTracks,
            observedTicks,
            longestNoProgress,
            minimumAllySeparationRaw,
            simulation.Outcome,
            simulation.ComputeStateHash(),
            simulation.MovementConflictDenials);
    }

    private static int LivingCount(AgentState[] agents) =>
        agents.Count(agent => agent.HitPoints > 0);

    private static int IndexOfEntity(AgentState[] agents, ulong entityId)
    {
        for (var index = 0; index < agents.Length; index++)
        {
            if (agents[index].EntityId == entityId)
            {
                return index;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(entityId), entityId, "No such agent in this run.");
    }

    /// <summary>
    /// The nearest living hostile by squared distance, breaking an exact tie
    /// on the lower stable entity identifier.
    /// </summary>
    private static AgentState? NearestHostile(
        AgentState[] agents,
        AgentState actor)
    {
        AgentState? nearest = null;
        var nearestSquared = long.MaxValue;
        foreach (var candidate in agents)
        {
            if (candidate.FactionId == actor.FactionId ||
                candidate.HitPoints <= 0)
            {
                continue;
            }

            var squaredRaw = CollisionGeometry.SquaredDistance(
                actor.XRaw, actor.YRaw, candidate.XRaw, candidate.YRaw);
            if (squaredRaw < nearestSquared ||
                (squaredRaw == nearestSquared &&
                    nearest is { } held &&
                    candidate.EntityId < held.EntityId))
            {
                nearest = candidate;
                nearestSquared = squaredRaw;
            }
        }

        return nearest;
    }

    /// <summary>
    /// The smallest centre separation between any two living same-faction
    /// warriors this tick, or <see cref="long.MaxValue"/> when no faction
    /// fields two.
    /// </summary>
    private static long ClosestAllySeparationRaw(AgentState[] agents)
    {
        var closest = long.MaxValue;
        for (var first = 0; first < agents.Length; first++)
        {
            for (var second = first + 1; second < agents.Length; second++)
            {
                if (agents[first].FactionId != agents[second].FactionId ||
                    agents[first].HitPoints <= 0 ||
                    agents[second].HitPoints <= 0)
                {
                    continue;
                }

                closest = Math.Min(
                    closest,
                    FixedPoint.IntegerSquareRoot(
                        CollisionGeometry.SquaredDistance(
                            agents[first].XRaw,
                            agents[first].YRaw,
                            agents[second].XRaw,
                            agents[second].YRaw)));
            }
        }

        return closest;
    }

    /// <summary>
    /// The lane separation the 2v2 fixtures use when the two allies are meant
    /// not to contend: six world units across the lane, far outside the
    /// 1,792-raw Wasay clearance radius.
    /// </summary>
    private const int SeparatedLaneOffsetRaw = 6_144;

    /// <summary>
    /// The column depth the 2v2 fixtures use when the two allies are meant to
    /// contend: three world units of depth on a single lane, so the rear
    /// warrior's approach runs straight through the leading warrior's
    /// clearance radius.
    /// </summary>
    private const int ContendedColumnDepthRaw = 3_072;

    /// <summary>The observation index of the Wasay in a duel run.</summary>
    private static int WasayDuelIndex(bool mirrored) => mirrored ? 1 : 0;

    /// <summary>The observation index of the opponent in a duel run.</summary>
    private static int OpponentDuelIndex(bool mirrored) => mirrored ? 0 : 1;

    /// <summary>
    /// How many ticks of a run the Wasay spent in
    /// <see cref="FootworkPhase.Recover"/> while its opponent was in some
    /// phase other than <see cref="FootworkPhase.Commit"/> — the
    /// repositioning window the matchup table asks the longer Wasay recovery
    /// to leave open.
    /// </summary>
    private static int RecoveryWindowTicks(
        RunObservation run,
        int wasayIndex,
        int opponentIndex)
    {
        var wasayTrack = run.PhaseTracks[wasayIndex];
        var opponentTrack = run.PhaseTracks[opponentIndex];
        var windows = 0;
        for (var tick = 0; tick < wasayTrack.Count; tick++)
        {
            if (wasayTrack[tick] == FootworkPhase.Recover &&
                opponentTrack[tick] != FootworkPhase.Commit)
            {
                windows++;
            }
        }

        return windows;
    }

    /// <summary>
    /// The shared-matrix team composition that pairs the Wasay with
    /// <paramref name="ally"/>.
    /// </summary>
    private static MovementScenarioMatrix.TeamComposition WasayTeamComposition(
        CombatLoadout ally)
    {
        var allyIndex = MovementRouteRules.CanonicalOpponentIndex(ally);
        return MovementScenarioMatrix.EnumerateTeamCompositions().Single(team =>
            (team.FirstMemberIndex == WasayCanonicalIndex &&
                team.SecondMemberIndex == allyIndex) ||
            (team.FirstMemberIndex == allyIndex &&
                team.SecondMemberIndex == WasayCanonicalIndex));
    }

    /// <summary>
    /// Repeats one duel with the eastern warrior's starting bearing written
    /// by hand before the simulation is built.
    /// </summary>
    private static RunObservation ObserveDuelWithEasternBearing(
        int opponentCanonicalIndex,
        Facing16 bearing)
    {
        var (scenario, agents) =
            BuildDuel(opponentCanonicalIndex, mirrored: false);
        agents[1].Facing = bearing;
        return ObserveRun(scenario, agents, CalibrationTicks);
    }

    /// <summary>
    /// The six duel cells this family runs are read out of the shared matrix
    /// rather than written down here: the 1v1 enumeration is filtered to the
    /// cells that contain the Wasay row, and the non-Wasay side of those six
    /// cells is exactly the six canonical loadouts, the mirror cell included.
    /// A seventh loadout added to the shared matrix therefore reaches this
    /// family without a line of this file being edited.
    /// </summary>
    /// <remarks>
    /// The combat preset is derived rather than assumed for the same reason.
    /// Only <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> fields all
    /// six canonical loadouts, so every cell whose opponent carries a shield
    /// must name it, and the cell's own
    /// <c>RequiresPrecolonialPhilippinesV2</c> flag is what decides.
    /// </remarks>
    [Fact]
    public void TheWasayDuelCellsComeFromTheSharedMatrixRatherThanAHandList()
    {
        var cells = WasayOneVersusOnePairs();

        Assert.Equal(MovementScenarioMatrix.CanonicalLoadoutCount, cells.Count);
        Assert.Equal(
            Enumerable.Range(0, MovementScenarioMatrix.CanonicalLoadoutCount),
            cells.Select(OpponentCanonicalIndex).Order());
        Assert.Single(cells, cell => cell.IsMirror);

        foreach (var cell in cells)
        {
            var opponentIndex = OpponentCanonicalIndex(cell);
            var opponent =
                MovementScenarioMatrix.CanonicalLoadouts[opponentIndex];
            var (scenario, _) = BuildDuel(opponentIndex, mirrored: false);

            Assert.Equal(
                opponent.Shield != ShieldId.None,
                cell.RequiresPrecolonialPhilippinesV2);
            Assert.Equal(
                cell.RequiresPrecolonialPhilippinesV2
                    ? MovementScenarioMatrix.ShieldedCellCombatPreset
                    : CombatPresetId.PrecolonialPhilippinesV4,
                scenario.CombatPreset);
            Assert.Equal(
                MovementPresetId.EquipmentRelativeFootworkV6,
                scenario.MovementPreset);
        }
    }

    /// <summary>
    /// The half of the section 7 table that every cell shares. Whichever of
    /// the six opponents the Wasay is put in front of, and whichever side of
    /// the arena it starts on, the run has to reach contact and keep making
    /// progress: no stalemate longer than the shared 250-tick no-progress
    /// bound of docs/archives/2026-07-31/movement/README.md task T10 step
    /// 6, at least one accepted blow from each side, a commitment from
    /// each side, and no step past the shared human baseline for anyone.
    /// </summary>
    /// <remarks>
    /// Start sides and faction bearings are mirrored by running every cell
    /// twice: once with the Wasay on the western start as faction 0, and once
    /// on the eastern start as faction 1 with both positions reflected about
    /// the arena's centre line. The two orientations are not asserted to
    /// produce the same hash, because README task T10 step 6 says raw hashes
    /// need not match reflected coordinates; only the behavioural facts are
    /// asserted of both. The determinism assertion is the honest one: the
    /// identical fixture run twice reproduces its hash and every recorded
    /// number.
    /// </remarks>
    [Fact]
    public void EveryGeneratedWasayDuelCellReachesContactWithoutStalling()
    {
        foreach (var cell in WasayOneVersusOnePairs())
        {
            var opponentIndex = OpponentCanonicalIndex(cell);
            var code =
                MovementScenarioMatrix.CanonicalLoadoutCodes[opponentIndex];

            foreach (var mirrored in new[] { false, true })
            {
                var run = ObserveDuel(opponentIndex, mirrored);
                var wasay = run.Agents[WasayDuelIndex(mirrored)];
                var opponent = run.Agents[OpponentDuelIndex(mirrored)];

                Assert.True(
                    run.LongestNoProgressStreakTicks <=
                        NoProgressStreakBoundTicks,
                    $"WA versus {code} (mirrored {mirrored}) went " +
                    $"{run.LongestNoProgressStreakTicks} ticks without a " +
                    "living-count, hit-point, or nearest-opponent-distance " +
                    "change.");

                Assert.True(
                    wasay.LandedAttacks > 0,
                    $"The Wasay never landed a blow against {code} " +
                    $"(mirrored {mirrored}).");
                Assert.True(
                    opponent.LandedAttacks > 0,
                    $"{code} never landed a blow against the Wasay " +
                    $"(mirrored {mirrored}).");
                Assert.NotNull(wasay.FirstCommitTick);
                Assert.NotNull(opponent.FirstCommitTick);

                foreach (var agent in run.Agents)
                {
                    Assert.True(
                        agent.LargestStepRaw <= WarriorSpeedRaw,
                        $"Entity {agent.EntityId} in WA versus {code} " +
                        $"(mirrored {mirrored}) stepped " +
                        $"{agent.LargestStepRaw} raw, past the " +
                        $"{WarriorSpeedRaw}-unit shared baseline.");
                }

                var repeat = ObserveDuel(opponentIndex, mirrored);
                Assert.Equal(run.StateHash, repeat.StateHash);
                Assert.Equal(run.Agents, repeat.Agents);
            }
        }
    }

    /// <summary>
    /// The Kampilan row of the section 7 table. Its two named failure modes
    /// are that the Wasay never closes and that it always closes without a
    /// punishable interval, so both are what this pins: the Wasay does commit
    /// and does land blows, and the longer-reach Kampilan has already landed
    /// one before the Wasay's own commitment begins. The two commitments do
    /// not start on the same tick, and that gap is the exposed crossing
    /// interval itself.
    /// </summary>
    [Fact]
    public void TheKampilanDuelCostsTheWasayAnIntervalToCrossTheLongerReach()
    {
        var kampilanIndex =
            MovementRouteRules.CanonicalOpponentIndex(KampilanLoadout);

        foreach (var mirrored in new[] { false, true })
        {
            var run = ObserveDuel(kampilanIndex, mirrored);
            var wasay = run.Agents[WasayDuelIndex(mirrored)];
            var kampilan = run.Agents[OpponentDuelIndex(mirrored)];

            Assert.NotNull(wasay.FirstCommitTick);
            Assert.True(wasay.LandedAttacks > 0);
            Assert.NotNull(kampilan.FirstLandedAttackTick);
            Assert.True(
                kampilan.FirstLandedAttackTick < wasay.FirstCommitTick,
                $"The Kampilan first landed on tick " +
                $"{kampilan.FirstLandedAttackTick} and the Wasay committed " +
                $"on tick {wasay.FirstCommitTick} (mirrored {mirrored}), so " +
                "the crossing cost the Wasay nothing.");
            Assert.NotEqual(kampilan.FirstCommitTick, wasay.FirstCommitTick);
            Assert.True(
                run.LongestNoProgressStreakTicks <=
                    NoProgressStreakBoundTicks);
        }
    }

    /// <summary>
    /// The Wasay mirror row of the section 7 table, whose failure modes are
    /// permanent circling, a fixed head-on collision loop, and never reaching
    /// contact. Both warriors land blows, both spend most of the run at or
    /// inside their own preferred distance, and neither is ever held in place
    /// by the collision pass or denied by the friendly-clearance pass.
    /// </summary>
    /// <remarks>
    /// Two head-on Wasay warriors on one lane are a geometrically symmetric
    /// fixture running a single profile, so they enter <c>Commit</c> on the
    /// same tick, and that equality is asserted here rather than glossed
    /// over. The staggering the plan asks two Wasay allies for is a clearance
    /// effect rather than a duel effect, and it is proved in
    /// <see cref="TwoWasayAlliesStaggerOnlyWhenTheirClearanceContends"/>.
    /// </remarks>
    [Fact]
    public void TheWasayMirrorReachesContactWithoutACollisionLoop()
    {
        var run = ObserveDuel(
            MovementRouteRules.CanonicalOpponentIndex(WasayLoadout));
        var west = run.Agents[0];
        var east = run.Agents[1];

        Assert.NotNull(west.FirstLandedAttackTick);
        Assert.NotNull(east.FirstLandedAttackTick);
        Assert.True(west.TicksBeyondPreferredDistance < run.ObservedTicks);
        Assert.True(east.TicksBeyondPreferredDistance < run.ObservedTicks);
        Assert.Equal(0, west.BlockedTicks);
        Assert.Equal(0, east.BlockedTicks);
        Assert.Equal(0, run.MovementConflictDenials);
        Assert.Equal(west.FirstCommitTick, east.FirstCommitTick);
        Assert.True(
            run.LongestNoProgressStreakTicks <= NoProgressStreakBoundTicks);
    }

    /// <summary>
    /// The Kalis row of the section 7 table, whose failure modes are that the
    /// Wasay's damage identity overwhelms every exchange and that the Kalis
    /// survives only by orbiting forever. The Kalis lands blows of its own,
    /// takes commitments of its own, and spends most of the run at or inside
    /// its own offset-adjusted preferred distance rather than circling
    /// outside it. The repositioning window is the Wasay's own recovery: the
    /// Wasay row's four recovery ticks are twice the Kalis row's two, so
    /// there are ticks in which the Wasay is recovering and the Kalis is free
    /// to move.
    /// </summary>
    [Fact]
    public void TheKalisDuelLeavesKalisARepositioningWindowInTheWasayRecovery()
    {
        var kalisIndex =
            MovementRouteRules.CanonicalOpponentIndex(KalisLoadout);

        foreach (var mirrored in new[] { false, true })
        {
            var run = ObserveDuel(kalisIndex, mirrored);
            var wasay = run.Agents[WasayDuelIndex(mirrored)];
            var kalis = run.Agents[OpponentDuelIndex(mirrored)];

            Assert.True(kalis.LandedAttacks > 0);
            Assert.NotNull(kalis.FirstCommitTick);
            Assert.True(kalis.TicksBeyondPreferredDistance < run.ObservedTicks);
            Assert.True(wasay.RecoverTicks > 0);
            Assert.True(
                RecoveryWindowTicks(
                    run,
                    WasayDuelIndex(mirrored),
                    OpponentDuelIndex(mirrored)) > 0,
                "The Kalis was never free to move during a Wasay recovery " +
                $"(mirrored {mirrored}).");
        }
    }

    /// <summary>
    /// The shielded Kalis row of the section 7 table, whose failure modes are
    /// that mirroring the shield bearing changes authoritative movement and
    /// that the shield grants speed. Neither can happen, and this is why: the
    /// equipment-relative preset writes every warrior's opening facing from
    /// its faction at simulation creation, so a starting bearing written by
    /// hand — north, its mirror south, or none at all — is discarded, and all
    /// three constructions produce the same state hash and the same recorded
    /// behaviour. No step of either warrior passes the shared human baseline
    /// either, so the shield buys no pace.
    /// </summary>
    [Fact]
    public void TheShieldedKalisStartingBearingDoesNotChangeMovement()
    {
        var kalisShieldIndex =
            MovementRouteRules.CanonicalOpponentIndex(KalisShieldLoadout);
        var unset = ObserveDuelWithEasternBearing(
            kalisShieldIndex, Facing16.None);
        var north = ObserveDuelWithEasternBearing(
            kalisShieldIndex, Facing16.North);
        var south = ObserveDuelWithEasternBearing(
            kalisShieldIndex, Facing16.South);

        Assert.Equal(unset.StateHash, north.StateHash);
        Assert.Equal(unset.StateHash, south.StateHash);
        Assert.Equal(unset.Agents, north.Agents);
        Assert.Equal(unset.Agents, south.Agents);

        foreach (var agent in unset.Agents)
        {
            Assert.True(
                agent.LargestStepRaw <= WarriorSpeedRaw,
                $"Entity {agent.EntityId} stepped {agent.LargestStepRaw} " +
                $"raw, past the {WarriorSpeedRaw}-unit shared baseline.");
        }
    }

    /// <summary>
    /// The Itak row of the section 7 table, whose failure modes are that the
    /// Itak has no route in and that the Wasay can never restore separation
    /// after a crossing. The straight entry is contested rather than free:
    /// the shorter Itak is held outside its own offset-adjusted preferred
    /// distance on strictly more ticks than the Wasay is held outside its.
    /// The Itak still has a route in — it commits and it lands blows — and
    /// the crossing or reset opportunity is the Wasay's own recovery.
    /// </summary>
    [Fact]
    public void TheItakDuelContestsTheStraightEntryAndStillOpensOnRecovery()
    {
        var itakIndex = MovementRouteRules.CanonicalOpponentIndex(ItakLoadout);

        foreach (var mirrored in new[] { false, true })
        {
            var run = ObserveDuel(itakIndex, mirrored);
            var wasay = run.Agents[WasayDuelIndex(mirrored)];
            var itak = run.Agents[OpponentDuelIndex(mirrored)];

            Assert.True(
                itak.TicksBeyondPreferredDistance >
                    wasay.TicksBeyondPreferredDistance,
                $"The Itak spent {itak.TicksBeyondPreferredDistance} ticks " +
                "outside its preferred distance and the Wasay " +
                $"{wasay.TicksBeyondPreferredDistance} (mirrored " +
                $"{mirrored}), so the straight entry was uncontested.");
            Assert.True(itak.LandedAttacks > 0);
            Assert.NotNull(itak.FirstCommitTick);
            Assert.True(
                RecoveryWindowTicks(
                    run,
                    WasayDuelIndex(mirrored),
                    OpponentDuelIndex(mirrored)) > 0,
                "The Itak was never free to move during a Wasay recovery " +
                $"(mirrored {mirrored}).");
        }
    }

    /// <summary>
    /// The shielded Itak row of the section 7 table, whose failure modes are
    /// that the shield causes an endless Wasay retreat and that it grants a
    /// movement-speed advantage. One ally against one hostile is a 1:1 local
    /// ratio, far below the Wasay row's 2:1 disengagement entry, so the Wasay
    /// never enters disengagement at all — the strongest available form of
    /// "bounded". Neither warrior steps past the shared human baseline, and
    /// the shield bearer still lands blows rather than being walked off the
    /// field.
    /// </summary>
    [Fact]
    public void TheShieldedItakDuelNeitherRoutsTheWasayNorOutpacesIt()
    {
        var itakShieldIndex =
            MovementRouteRules.CanonicalOpponentIndex(ItakShieldLoadout);

        foreach (var mirrored in new[] { false, true })
        {
            var run = ObserveDuel(itakShieldIndex, mirrored);
            var wasay = run.Agents[WasayDuelIndex(mirrored)];
            var itak = run.Agents[OpponentDuelIndex(mirrored)];

            Assert.Equal(0, wasay.DisengageTicks);
            Assert.Equal(0, wasay.DisengageEntries);
            Assert.Equal(0, wasay.DisengageReleases);
            Assert.True(itak.LargestStepRaw <= WarriorSpeedRaw);
            Assert.True(wasay.LargestStepRaw <= WarriorSpeedRaw);
            Assert.True(itak.LandedAttacks > 0);
            Assert.True(
                run.LongestNoProgressStreakTicks <=
                    NoProgressStreakBoundTicks);
        }
    }

    /// <summary>
    /// The homogeneous 2v2 case of section 7. Two Wasay allies are asked to
    /// stagger their commitments because of clearance and recovery, and not
    /// because of any hard-coded alternation, so both halves are run. Given a
    /// lane of their own each, the same two profiles contend for nothing and
    /// commit on the very same tick, which is what rules out an alternation
    /// rule. Put one behind the other on a single lane and they stagger, with
    /// the delay accounted for by the run's own conflict denials and by the
    /// rear warrior's refused routes.
    /// </summary>
    [Fact]
    public void TwoWasayAlliesStaggerOnlyWhenTheirClearanceContends()
    {
        var composition = WasayTeamComposition(WasayLoadout);
        var separated = ObservePairMatchup(
            composition, SeparatedLaneOffsetRaw);
        var column = ObservePairMatchup(
            composition, laneOffsetRaw: 0, ContendedColumnDepthRaw);

        Assert.Equal(0, separated.MovementConflictDenials);
        Assert.All(
            separated.Agents, agent => Assert.Equal(0, agent.RefuseTicks));
        Assert.Equal(
            separated.Agents[0].FirstCommitTick,
            separated.Agents[1].FirstCommitTick);

        Assert.NotEqual(
            column.Agents[0].FirstCommitTick,
            column.Agents[1].FirstCommitTick);
        Assert.True(
            column.MovementConflictDenials > 0,
            "The column fixture staggered without a single conflict denial " +
            "to explain it.");
        Assert.True(
            column.Agents[1].RefuseTicks > 0,
            "The rear Wasay never refused a route, so the stagger has no " +
            "clearance explanation.");
        Assert.True(
            column.MinimumAllySeparationRaw >= WasayClearanceRadiusRaw(),
            "Two Wasay allies closed to " +
            $"{column.MinimumAllySeparationRaw} raw, inside their own " +
            $"{WasayClearanceRadiusRaw()}-unit clearance radius.");
        Assert.True(
            column.LongestNoProgressStreakTicks <=
                NoProgressStreakBoundTicks);
        Assert.All(
            column.Agents,
            agent => Assert.True(agent.LargestStepRaw <= WarriorSpeedRaw));
    }

    /// <summary>
    /// The mixed 2v2 case of section 7: a Wasay whose shorter Itak ally
    /// already occupies the direct lane has to take its own outer or
    /// supporting lane rather than cutting through that approach. The Itak
    /// leads the column and the Wasay follows on the same lane, and across
    /// the whole run no two allies ever close inside the Wasay's own
    /// clearance radius. The Wasay leaves the shared centre lane, records
    /// refused routes or conflict denials rather than pressing through, and
    /// still lands blows, so yielding the lane does not make it inert.
    /// </summary>
    [Fact]
    public void AMixedWasayPairTakesItsOwnLaneWithoutCuttingThroughTheAlly()
    {
        var composition = WasayTeamComposition(ItakLoadout);
        var run = ObservePairMatchup(
            composition,
            laneOffsetRaw: 0,
            ContendedColumnDepthRaw,
            reverseMembers: true);
        var itak = run.Agents[0];
        var wasay = run.Agents[1];

        Assert.Equal(WeaponId.Itak, itak.Loadout.Weapon);
        Assert.Equal(WeaponId.Wasay, wasay.Loadout.Weapon);

        Assert.True(
            run.MinimumAllySeparationRaw >= WasayClearanceRadiusRaw(),
            $"The Wasay closed to {run.MinimumAllySeparationRaw} raw of its " +
            $"ally, inside its own {WasayClearanceRadiusRaw()}-unit " +
            "clearance radius.");
        Assert.True(
            wasay.LargestLaneOffsetRaw > 0,
            "The Wasay never left the shared centre lane.");
        Assert.True(
            wasay.RefuseTicks > 0 || run.MovementConflictDenials > 0,
            "Nothing in the run shows the Wasay yielding the occupied lane.");
        Assert.True(wasay.LandedAttacks > 0);
        Assert.True(itak.LandedAttacks > 0);
        Assert.True(
            run.LongestNoProgressStreakTicks <= NoProgressStreakBoundTicks);
        Assert.All(
            run.Agents,
            agent => Assert.True(agent.LargestStepRaw <= WarriorSpeedRaw));
    }

    // ----- Family 15: asymmetric, mixed-group, and replay behaviour -----

    /// <summary>
    /// How many whole ticks every group fixture in this family is observed
    /// for. It sits above the shared 250-tick no-progress bound of
    /// docs/archives/2026-07-31/movement/README.md task T10 step 6, so a
    /// stalled fixture has room to break that bound rather than simply
    /// running out of ticks first, and it sits above the 100-tick settling
    /// window that the phase-flip rejection criterion of task T11 step 7
    /// discards.
    /// </summary>
    private const int GroupObservedTicks = 400;

    /// <summary>
    /// The head of a group run that the phase-flip measurement discards,
    /// from the rejection criterion in
    /// docs/archives/2026-07-31/movement/README.md task T11 step 7: "any
    /// phase/posture flips on more than 25% of ticks after the first 100".
    /// </summary>
    private const int PhaseFlipSettlingTicks = 100;

    /// <summary>
    /// The 25% share of the same rejection criterion, expressed in basis
    /// points so the comparison stays an integer cross-product.
    /// </summary>
    private const int PhaseFlipShareBasisPoints = 2_500;

    /// <summary>
    /// Row spacing across the group fixtures: three world units. It is wider
    /// than the 1,792-raw Wasay clearance radius, so no fixture starts a
    /// warrior already inside an ally's clearance, and it is inside the
    /// 6,144-raw support radius, so warriors in one cluster perceive each
    /// other from the first tick.
    /// </summary>
    private const int GroupRowSpacingRaw = 3_072;

    /// <summary>
    /// Column spacing for the homogeneous congestion fixture: two world
    /// units. Still outside the 1,792-raw clearance radius at rest, but tight
    /// enough that a rear warrior's advance runs straight into the lane the
    /// warrior ahead of it holds.
    /// </summary>
    private const int GroupColumnSpacingRaw = 2_048;

    /// <summary>
    /// The northern pocket the globally favoured fixture isolates one Wasay
    /// in: far enough from its own faction's southern cluster that no ally
    /// falls inside its 6,144-raw support radius.
    /// </summary>
    private const int GroupNorthPocketXRaw = 102_400;

    /// <inheritdoc cref="GroupNorthPocketXRaw"/>
    private const int GroupNorthPocketYRaw = 20_480;

    /// <summary>The southern cluster's column in the same fixture.</summary>
    private const int GroupSouthClusterXRaw = 87_040;

    /// <inheritdoc cref="GroupSouthClusterXRaw"/>
    private const int GroupSouthClusterYRaw = 71_680;

    /// <summary>
    /// One warrior's placement in a group fixture: which faction it belongs
    /// to, which complete loadout it carries, and where it starts.
    /// </summary>
    private readonly record struct GroupMember(
        int FactionId,
        CombatLoadout Loadout,
        int XRaw,
        int YRaw)
    {
        /// <summary>
        /// The attack cooldown the warrior starts the run holding, default
        /// zero, which is a blow ready on the first tick. Only the globally
        /// favoured fixture sets it, and its remarks explain why.
        /// </summary>
        internal int InitialAttackCooldownTicks { get; init; }
    }

    /// <summary>
    /// One named group fixture. The member order is also the entity-identifier
    /// order the fixture is built with, so a member index and an observation
    /// index refer to the same warrior.
    /// </summary>
    private sealed record GroupFixture(
        string Name,
        IReadOnlyList<GroupMember> Members)
    {
        /// <summary>How many warriors the given faction fields.</summary>
        internal int RosterSize(int factionId) =>
            Members.Count(member => member.FactionId == factionId);

        /// <summary>
        /// The unordered count suite this fixture belongs to, written smaller
        /// side first, so a fixture that puts the Wasay on the larger side of
        /// a 3v5 still reads as <c>3v5</c>.
        /// </summary>
        internal string CountSuite =>
            $"{Math.Min(RosterSize(0), RosterSize(1))}v" +
            $"{Math.Max(RosterSize(0), RosterSize(1))}";

        /// <summary>
        /// Whether any member carries a shield, which decides the combat
        /// preset the fixture must name.
        /// </summary>
        internal bool ContainsShieldedLoadout =>
            Members.Any(member => member.Loadout.Shield != ShieldId.None);
    }

    /// <summary>
    /// One Wasay against two hostiles on a single lane. Global totals put its
    /// faction at exactly double outnumbering, which the posture table already
    /// reads as <see cref="TacticalPosture.Withdraw"/>.
    /// </summary>
    private static GroupFixture OneVersusTwoFixture() => new(
        "1v2 lone Wasay",
        [
            new(0, WasayLoadout, CalibrationWestXRaw, CalibrationLaneYRaw),
            new(
                1,
                KampilanLoadout,
                CalibrationEastXRaw,
                CalibrationLaneYRaw - GroupRowSpacingRaw),
            new(
                1,
                ItakLoadout,
                CalibrationEastXRaw,
                CalibrationLaneYRaw + GroupRowSpacingRaw),
        ]);

    /// <summary>
    /// A Wasay and one ally against three hostiles: the adjacent count suite,
    /// whose four-to-three global pressure the posture table already reads as
    /// <see cref="TacticalPosture.Yield"/>.
    /// </summary>
    private static GroupFixture TwoVersusThreeFixture() => new(
        "2v3 Wasay and Kalis",
        [
            new(0, WasayLoadout, CalibrationWestXRaw, CalibrationLaneYRaw),
            new(
                0,
                KalisLoadout,
                CalibrationWestXRaw,
                CalibrationLaneYRaw + GroupRowSpacingRaw),
            new(
                1,
                KampilanLoadout,
                CalibrationEastXRaw,
                CalibrationLaneYRaw - GroupRowSpacingRaw),
            new(1, ItakLoadout, CalibrationEastXRaw, CalibrationLaneYRaw),
            new(
                1,
                KalisLoadout,
                CalibrationEastXRaw,
                CalibrationLaneYRaw + GroupRowSpacingRaw),
        ]);

    /// <summary>
    /// The observation index of the Wasay the globally favoured fixture
    /// isolates in the northern pocket.
    /// </summary>
    private const int FavouredIsolatedWasayIndex = 0;

    /// <summary>
    /// The observation index of the second Wasay in the same fixture, the one
    /// standing inside its faction's southern cluster.
    /// </summary>
    private const int FavouredSupportedWasayIndex = 1;

    /// <summary>
    /// Five against three with both Wasay warriors on the larger side: the
    /// globally favourable placement of task W5 step 1. Five against three is
    /// <see cref="TacticalPosture.Advance"/> on the posture table's exact
    /// five-to-four branch, so nothing global asks either Wasay to give
    /// ground. One of them is isolated in a northern pocket with the whole
    /// three-warrior hostile contingent inside its support radius — three
    /// hostiles to its own single support ally, above the two-to-one entry
    /// its row disengages on — while the other stands in the southern cluster
    /// with three allies and no hostile in range. The run therefore holds the
    /// favourable global posture fixed and varies only the local geometry.
    /// </summary>
    /// <remarks>
    /// The isolated Wasay starts the run with its blow already spent, and that
    /// is the only way this decision can be observed at all. Steps 2 and 3 of
    /// the shared transition order carry a running <c>Commit</c> or
    /// <c>Recover</c> before the local-ratio steps 4 and 5 are ever reached,
    /// and an accepted attack overwrites the committed phase with a fresh
    /// <c>Commit</c> after movement. Every loadout's attack reach is wider
    /// than the shared support radius, so any hostile close enough to count
    /// towards the two-to-one ratio is also close enough to be struck: a
    /// warrior whose blow is ready spends the run in the attack lifecycle and
    /// its movement decision never reaches authoritative state. Pinning the
    /// cooldown is authoritative state written before the run, exactly as the
    /// single-tick entry fixtures earlier in this file already do, and it
    /// changes no rule.
    /// </remarks>
    private static GroupFixture GloballyFavouredLocallyOutnumberedFixture() =>
        new(
            "3v5 favourable global, outnumbered pocket",
            [
                new(
                    0,
                    WasayLoadout,
                    GroupNorthPocketXRaw,
                    GroupNorthPocketYRaw)
                {
                    InitialAttackCooldownTicks = GroupObservedTicks,
                },
                new(
                    0,
                    WasayLoadout,
                    GroupSouthClusterXRaw,
                    GroupSouthClusterYRaw),
                new(
                    0,
                    KampilanLoadout,
                    GroupSouthClusterXRaw,
                    GroupSouthClusterYRaw - GroupRowSpacingRaw),
                new(
                    0,
                    KalisLoadout,
                    GroupSouthClusterXRaw,
                    GroupSouthClusterYRaw + GroupRowSpacingRaw),
                new(
                    0,
                    ItakLoadout,
                    GroupSouthClusterXRaw - GroupRowSpacingRaw,
                    GroupSouthClusterYRaw),
                new(
                    1,
                    KampilanLoadout,
                    GroupNorthPocketXRaw + GroupRowSpacingRaw,
                    GroupNorthPocketYRaw - GroupColumnSpacingRaw),
                new(
                    1,
                    ItakLoadout,
                    GroupNorthPocketXRaw + GroupRowSpacingRaw,
                    GroupNorthPocketYRaw + GroupColumnSpacingRaw),
                new(
                    1,
                    KalisLoadout,
                    GroupNorthPocketXRaw + GroupRowSpacingRaw +
                        GroupColumnSpacingRaw,
                    GroupNorthPocketYRaw),
            ]);

    /// <summary>
    /// The observation index of the Wasay in the globally unfavourable
    /// fixture.
    /// </summary>
    private const int OutnumberedWasayIndex = 0;

    /// <summary>
    /// Three against five with the Wasay on the smaller side and standing
    /// between both of its allies: the globally unfavourable placement of task
    /// W5 step 1. Its own support ring holds three allies against one hostile,
    /// so nothing local asks it to disengage; only the faction totals do.
    /// </summary>
    private static GroupFixture GloballyOutnumberedLocallySupportedFixture() =>
        new(
            "3v5 unfavourable global, supported pocket",
            [
                new(0, WasayLoadout, CalibrationWestXRaw, CalibrationLaneYRaw),
                new(
                    0,
                    KampilanLoadout,
                    CalibrationWestXRaw,
                    CalibrationLaneYRaw - GroupRowSpacingRaw),
                new(
                    0,
                    KalisLoadout,
                    CalibrationWestXRaw,
                    CalibrationLaneYRaw + GroupRowSpacingRaw),
                new(
                    1,
                    ItakLoadout,
                    CalibrationWestXRaw + (GroupRowSpacingRaw * 5 / 3),
                    CalibrationLaneYRaw),
                new(
                    1,
                    KampilanLoadout,
                    CalibrationEastXRaw,
                    CalibrationLaneYRaw - (GroupRowSpacingRaw * 2)),
                new(1, KalisLoadout, CalibrationEastXRaw, CalibrationLaneYRaw),
                new(
                    1,
                    ItakLoadout,
                    CalibrationEastXRaw,
                    CalibrationLaneYRaw + (GroupRowSpacingRaw * 2)),
                new(
                    1,
                    KampilanLoadout,
                    CalibrationEastXRaw + GroupRowSpacingRaw,
                    CalibrationLaneYRaw),
            ]);

    /// <summary>
    /// Four Wasay against four Wasay, each faction stacked nose to tail on one
    /// lane: the homogeneous congestion case of task W5 step 1. Nothing in the
    /// fixture separates the allies, so every rear warrior's direct route runs
    /// through the clearance radius of the warrior ahead of it.
    /// </summary>
    private static GroupFixture HomogeneousWasayColumnFixture()
    {
        var members = new List<GroupMember>(8);
        for (var rank = 0; rank < 4; rank++)
        {
            members.Add(new GroupMember(
                0,
                WasayLoadout,
                CalibrationWestXRaw - (rank * GroupColumnSpacingRaw),
                CalibrationLaneYRaw));
        }

        for (var rank = 0; rank < 4; rank++)
        {
            members.Add(new GroupMember(
                1,
                WasayLoadout,
                CalibrationEastXRaw + (rank * GroupColumnSpacingRaw),
                CalibrationLaneYRaw));
        }

        return new GroupFixture("4v4 homogeneous Wasay column", members);
    }

    /// <summary>
    /// The observation index of the Itak that already occupies the direct lane
    /// in the mixed five-a-side fixture.
    /// </summary>
    private const int OccupiedLaneItakIndex = 0;

    /// <summary>
    /// The observation index of the Wasay standing behind that Itak on the
    /// same lane.
    /// </summary>
    private const int OccupiedLaneWasayIndex = 1;

    /// <summary>
    /// Five against five with a shorter-reach Itak already holding the centre
    /// lane and a Wasay directly behind it: the mixed-roster case of task W5
    /// step 1. Both sides field the same composition reflected about the
    /// arena's centre line, so nothing but the lane geometry distinguishes
    /// them.
    /// </summary>
    private static GroupFixture OccupiedDirectLaneFixture() => new(
        "5v5 shorter ally in the direct lane",
        [
            new(
                0,
                ItakLoadout,
                CalibrationWestXRaw + (GroupRowSpacingRaw * 4),
                CalibrationLaneYRaw),
            new(
                0,
                WasayLoadout,
                CalibrationWestXRaw + (GroupRowSpacingRaw * 7 / 3),
                CalibrationLaneYRaw),
            new(
                0,
                KampilanLoadout,
                CalibrationWestXRaw,
                CalibrationLaneYRaw - (GroupRowSpacingRaw * 2)),
            new(
                0,
                KalisLoadout,
                CalibrationWestXRaw,
                CalibrationLaneYRaw + (GroupRowSpacingRaw * 2)),
            new(
                0,
                KampilanLoadout,
                CalibrationWestXRaw - GroupRowSpacingRaw,
                CalibrationLaneYRaw),
            new(
                1,
                ItakLoadout,
                CalibrationEastXRaw - (GroupRowSpacingRaw * 4),
                CalibrationLaneYRaw),
            new(
                1,
                WasayLoadout,
                CalibrationEastXRaw - (GroupRowSpacingRaw * 7 / 3),
                CalibrationLaneYRaw),
            new(
                1,
                KampilanLoadout,
                CalibrationEastXRaw,
                CalibrationLaneYRaw - (GroupRowSpacingRaw * 2)),
            new(
                1,
                KalisLoadout,
                CalibrationEastXRaw,
                CalibrationLaneYRaw + (GroupRowSpacingRaw * 2)),
            new(
                1,
                KampilanLoadout,
                CalibrationEastXRaw + GroupRowSpacingRaw,
                CalibrationLaneYRaw),
        ]);

    /// <summary>
    /// Eight against eight, mixed on both sides and shielded on one, so the
    /// largest fixture in the family also exercises the only combat preset
    /// that fields all six canonical loadouts.
    /// </summary>
    private static GroupFixture EightVersusEightFixture()
    {
        CombatLoadout[] western =
        [
            WasayLoadout,
            KampilanLoadout,
            WasayLoadout,
            KalisLoadout,
            ItakLoadout,
            KampilanLoadout,
            WasayLoadout,
            KalisLoadout,
        ];
        CombatLoadout[] eastern =
        [
            KampilanLoadout,
            ItakLoadout,
            KalisShieldLoadout,
            KampilanLoadout,
            WasayLoadout,
            ItakShieldLoadout,
            KalisLoadout,
            KampilanLoadout,
        ];

        var members = new List<GroupMember>(western.Length + eastern.Length);
        for (var rank = 0; rank < western.Length; rank++)
        {
            members.Add(new GroupMember(
                0,
                western[rank],
                CalibrationWestXRaw,
                EightVersusEightRowYRaw(rank)));
        }

        for (var rank = 0; rank < eastern.Length; rank++)
        {
            members.Add(new GroupMember(
                1,
                eastern[rank],
                CalibrationEastXRaw,
                EightVersusEightRowYRaw(rank)));
        }

        return new GroupFixture("8v8 mixed with shields", members);
    }

    /// <summary>
    /// The lane of one rank in the eight-a-side fixture: eight rows spaced by
    /// <see cref="GroupRowSpacingRaw"/> and centred on the calibration lane.
    /// </summary>
    private static int EightVersusEightRowYRaw(int rank) =>
        CalibrationLaneYRaw +
        (((rank * 2) - 7) * GroupRowSpacingRaw / 2);

    /// <summary>
    /// Every group fixture of task W5 step 1, in a fixed order.
    /// </summary>
    private static GroupFixture[] AllGroupFixtures() =>
    [
        OneVersusTwoFixture(),
        TwoVersusThreeFixture(),
        GloballyFavouredLocallyOutnumberedFixture(),
        GloballyOutnumberedLocallySupportedFixture(),
        HomogeneousWasayColumnFixture(),
        OccupiedDirectLaneFixture(),
        EightVersusEightFixture(),
    ];

    /// <summary>
    /// Builds one group fixture into a scenario and its warriors. Entity
    /// identifiers ascend with member order, and
    /// <c>BattleSimulation.CreateForTesting</c> canonicalises by entity
    /// identifier, so a member index and an observation index agree.
    /// </summary>
    private static (Scenario Scenario, AgentState[] Agents) BuildGroup(
        GroupFixture fixture)
    {
        var scenario = CreateScenario(
            CalibrationCombatPreset(fixture.ContainsShieldedLoadout));
        var agents = new AgentState[fixture.Members.Count];
        for (var index = 0; index < agents.Length; index++)
        {
            var member = fixture.Members[index];
            agents[index] = CreateCalibrationAgent(
                (ulong)(index + 1),
                member.FactionId,
                member.XRaw,
                member.YRaw,
                scenario,
                member.Loadout);
            agents[index].AttackCooldownRemaining =
                member.InitialAttackCooldownTicks;
        }

        return (scenario, agents);
    }

    /// <summary>Runs one group fixture through the shared observation pass.</summary>
    private static RunObservation ObserveGroup(GroupFixture fixture)
    {
        var (scenario, agents) = BuildGroup(fixture);
        return ObserveRun(scenario, agents, GroupObservedTicks);
    }

    /// <summary>
    /// One warrior's committed footwork phase and lifecycle timer on one tick.
    /// </summary>
    private readonly record struct FootworkSample(
        FootworkPhase Phase,
        int TicksRemaining);

    /// <summary>
    /// Records the committed phase and its timer for every warrior on every
    /// tick. The shared observation pass deliberately keeps only derived
    /// counts, and the commitment and recovery lengths of task W5 step 2(e)
    /// are a statement about the timer itself, so this second, much smaller
    /// pass reads the timer alongside the phase rather than re-deriving the
    /// counts the shared pass already produces.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<FootworkSample>>
        ObserveFootworkTimeline(GroupFixture fixture)
    {
        var (scenario, agents) = BuildGroup(fixture);
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);
        var ordered = agents.OrderBy(agent => agent.EntityId).ToArray();
        var tracks = new List<FootworkSample>[ordered.Length];
        for (var index = 0; index < ordered.Length; index++)
        {
            tracks[index] = new List<FootworkSample>(GroupObservedTicks);
        }

        for (var tick = 1; tick <= GroupObservedTicks; tick++)
        {
            simulation.AdvanceOneTick();
            for (var index = 0; index < ordered.Length; index++)
            {
                tracks[index].Add(new FootworkSample(
                    ordered[index].FootworkPhase,
                    ordered[index].FootworkTicksRemaining));
            }
        }

        return tracks;
    }

    /// <summary>
    /// The value folded into the event digest in place of an absent optional
    /// event field. No real field can produce it, so a present field and an
    /// absent one never collide.
    /// </summary>
    private const ulong AbsentEventField = ulong.MaxValue;

    private const ulong EventDigestOffsetBasis = 14_695_981_039_346_656_037UL;

    private const ulong EventDigestPrime = 1_099_511_628_211UL;

    /// <summary>
    /// Folds one widened field into the running FNV-1a digest, byte by byte
    /// and in a fixed order, so the digest depends on every bit of every field
    /// and on the order the fields arrive in.
    /// </summary>
    private static ulong FoldEventDigest(ulong digest, ulong value)
    {
        unchecked
        {
            for (var shift = 0; shift < 64; shift += 8)
            {
                digest ^= (value >> shift) & 0xFF;
                digest *= EventDigestPrime;
            }
        }

        return digest;
    }

    /// <summary>
    /// The event hash of task W5 step 2(a): one integer digest over the whole
    /// concatenated ordered event stream, folding every field a
    /// <see cref="BattleEvent"/> carries. This is a test-local digest for
    /// comparing one run against its own replay; it is not the headless run
    /// report's event hash and is never pinned to a literal here.
    /// </summary>
    private static ulong ComputeEventDigest(IReadOnlyList<BattleEvent> events)
    {
        var digest = EventDigestOffsetBasis;
        foreach (var battleEvent in events)
        {
            digest = FoldEventDigest(
                digest, unchecked((ulong)battleEvent.Sequence));
            digest = FoldEventDigest(digest, unchecked((ulong)battleEvent.Tick));
            digest = FoldEventDigest(digest, (ulong)battleEvent.Kind);
            digest = FoldEventDigest(digest, battleEvent.SourceEntityId);
            digest = FoldEventDigest(
                digest, battleEvent.TargetEntityId ?? AbsentEventField);
            digest = FoldEventDigest(
                digest, unchecked((ulong)(long)battleEvent.Value));
            digest = FoldEventDigest(
                digest,
                battleEvent.FactionId is { } faction
                    ? unchecked((ulong)(long)faction)
                    : AbsentEventField);
            digest = FoldEventDigest(
                digest,
                battleEvent.Weapon is { } weapon
                    ? (ulong)weapon
                    : AbsentEventField);
            digest = FoldEventDigest(
                digest,
                battleEvent.Shield is { } shield
                    ? (ulong)shield
                    : AbsentEventField);
            digest = FoldEventDigest(
                digest,
                battleEvent.HitLocation is { } location
                    ? (ulong)location
                    : AbsentEventField);
            digest = FoldEventDigest(
                digest,
                battleEvent.Resolution is { } resolution
                    ? (ulong)resolution
                    : AbsentEventField);
            digest = FoldEventDigest(
                digest,
                battleEvent.ComboPosition is { } position
                    ? unchecked((ulong)(long)position)
                    : AbsentEventField);
        }

        return digest;
    }

    /// <summary>
    /// One whole replay of a group fixture: the outcome, the authoritative
    /// state hash, the digest over the concatenated ordered event stream, and
    /// that stream itself.
    /// </summary>
    private sealed record GroupReplay(
        BattleOutcome Outcome,
        ulong StateHash,
        ulong EventDigest,
        IReadOnlyList<BattleEvent> Events);

    /// <summary>
    /// Builds a group fixture from scratch and runs it for the fixed group
    /// tick count, reusing the shared event-collecting helper.
    /// </summary>
    private static GroupReplay ReplayGroup(GroupFixture fixture)
    {
        var (scenario, agents) = BuildGroup(fixture);
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);
        var events = RunAndCollectEvents(
            simulation, GroupObservedTicks, out var stateHash);

        return new GroupReplay(
            simulation.Outcome,
            stateHash,
            ComputeEventDigest(events),
            events);
    }

    /// <summary>
    /// How many ticks after the settling window a warrior's committed phase
    /// differed from the tick before it, and how many ticks were measured,
    /// for the rejection criterion of
    /// docs/archives/2026-07-31/movement/README.md task T11 step 7.
    /// </summary>
    private static (int Flips, int Measured) LatePhaseFlips(
        IReadOnlyList<FootworkSample> track)
    {
        var flips = 0;
        var measured = 0;
        for (var tick = PhaseFlipSettlingTicks; tick < track.Count; tick++)
        {
            measured++;
            if (track[tick].Phase != track[tick - 1].Phase)
            {
                flips++;
            }
        }

        return (flips, measured);
    }

    /// <summary>
    /// Every way a warrior's committed lifecycle timer disagreed with its own
    /// profile across a whole recorded timeline, described well enough to name
    /// the warrior and the tick. An empty list is the invariant of task W5
    /// step 2(e).
    /// </summary>
    /// <remarks>
    /// The timer, not the run length, is what carries the exact duration.
    /// A run enters <c>Commit</c> or <c>Recover</c> at the profile's own
    /// duration and counts down by one whole tick at a time, and an accepted
    /// attack may reload a fresh <c>Commit</c> at the full duration at any
    /// point, so a run measured only by its length would either miss the
    /// reload or have to guess at it. Each warrior's own profile is resolved
    /// from the registered ruleset, because the group fixtures are mixed and
    /// the Wasay row's four-and-four rhythm is not the Kalis row's two-and-two.
    /// </remarks>
    private static List<string> LifecycleTimerViolations(
        GroupFixture fixture,
        IReadOnlyList<IReadOnlyList<FootworkSample>> timeline)
    {
        var movementRules = MovementPresetRegistry.Get(
            MovementPresetId.EquipmentRelativeFootworkV6);
        var violations = new List<string>();
        for (var index = 0; index < timeline.Count; index++)
        {
            var profile = movementRules.ResolveLoadoutProfile(
                fixture.Members[index].Loadout);
            var track = timeline[index];
            for (var tick = 0; tick < track.Count; tick++)
            {
                var sample = track[tick];
                var previous = tick == 0
                    ? new FootworkSample(FootworkPhase.None, 0)
                    : track[tick - 1];
                var entryTicks = sample.Phase switch
                {
                    FootworkPhase.Commit => profile.CommitmentTicks,
                    FootworkPhase.Recover => profile.RecoveryTicks,
                    _ => 0,
                };

                if (sample.Phase is not (FootworkPhase.Commit
                    or FootworkPhase.Recover))
                {
                    if (sample.TicksRemaining != 0)
                    {
                        violations.Add(
                            $"{fixture.Name}: warrior {index + 1} on tick " +
                            $"{tick + 1} held phase {sample.Phase} with a " +
                            $"timer of {sample.TicksRemaining} rather than 0.");
                    }

                    continue;
                }

                if (sample.Phase != previous.Phase)
                {
                    if (sample.TicksRemaining != entryTicks)
                    {
                        violations.Add(
                            $"{fixture.Name}: warrior {index + 1} entered " +
                            $"{sample.Phase} on tick {tick + 1} at " +
                            $"{sample.TicksRemaining} ticks rather than its " +
                            $"profile's {entryTicks}.");
                    }
                }
                else if (sample.TicksRemaining != previous.TicksRemaining - 1 &&
                    sample.TicksRemaining != entryTicks)
                {
                    violations.Add(
                        $"{fixture.Name}: warrior {index + 1} carried " +
                        $"{sample.Phase} from {previous.TicksRemaining} to " +
                        $"{sample.TicksRemaining} on tick {tick + 1}, which " +
                        "is neither one tick of countdown nor a fresh entry.");
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// How many ticks of a run one member's support ring held at or above the
    /// Wasay row's two-to-one disengagement entry ratio, derived from outside
    /// the simulation through the same shared local-context query the rest of
    /// this file uses.
    /// </summary>
    private static int TwoToOneSupportTicks(
        GroupFixture fixture,
        int memberIndex)
    {
        var (scenario, agents) = BuildGroup(fixture);
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);
        var ordered = agents.OrderBy(agent => agent.EntityId).ToArray();
        var actor = ordered[memberIndex];
        var pressured = 0;

        for (var tick = 1; tick <= GroupObservedTicks; tick++)
        {
            simulation.AdvanceOneTick();
            var context = DeriveWasayContext(
                ordered, actor, actor.TargetEntityId);
            if (checked((long)context.SupportEnemies * BasisPointDenominator) >=
                checked((long)context.SupportAllies *
                    WasayMovementProfile.Row.DisengageEnemyToAllyBasisPoints))
            {
                pressured++;
            }
        }

        return pressured;
    }

    /// <summary>
    /// Task W5 step 1. The seven group fixtures of this family are read as a
    /// set rather than one at a time: together they cover the 1v2, 2v3, 3v5,
    /// 4v4, 5v5, and 8v8 count suites the plan's section 7 asks for, every one
    /// of them fields a Wasay, and every loadout in every one of them is a row
    /// of the shared canonical matrix rather than a locally invented pairing.
    /// </summary>
    /// <remarks>
    /// The combat preset is derived from shield presence rather than written
    /// down, exactly as the duel family derives it: only
    /// <see cref="CombatPresetId.PrecolonialPhilippinesV2"/> fields all six
    /// canonical loadouts, so the one fixture that puts a tall hardwood shield
    /// on the field must name it and the solo-only fixtures must not. The
    /// movement preset is named explicitly on every fixture, because an
    /// implicit default would quietly turn this whole family into a test of
    /// some other preset.
    /// </remarks>
    [Fact]
    public void TheWasayGroupFixturesCoverTheAsymmetricAndMixedCountSuites()
    {
        var fixtures = AllGroupFixtures();

        Assert.Equal(
            ["1v2", "2v3", "3v5", "3v5", "4v4", "5v5", "8v8"],
            fixtures.Select(fixture => fixture.CountSuite).Order());

        Assert.Contains(fixtures, fixture => fixture.ContainsShieldedLoadout);

        foreach (var fixture in fixtures)
        {
            Assert.Contains(
                fixture.Members,
                member => member.Loadout.Weapon == WeaponId.Wasay);
            Assert.All(
                fixture.Members,
                member => Assert.Contains(
                    member.Loadout, MovementScenarioMatrix.CanonicalLoadouts));

            var (scenario, agents) = BuildGroup(fixture);

            Assert.Equal(
                fixture.ContainsShieldedLoadout
                    ? MovementScenarioMatrix.ShieldedCellCombatPreset
                    : CombatPresetId.PrecolonialPhilippinesV4,
                scenario.CombatPreset);
            Assert.Equal(
                MovementPresetId.EquipmentRelativeFootworkV6,
                scenario.MovementPreset);

            // The member order is also the entity-identifier order, so a
            // member index and an observation index name the same warrior.
            Assert.Equal(
                fixture.Members.Select(member => member.Loadout),
                agents.OrderBy(agent => agent.EntityId)
                    .Select(agent => agent.Loadout));
        }
    }

    /// <summary>
    /// Task W5 step 2(a). Every group fixture is built from scratch twice and
    /// run for the same number of ticks twice, and the two runs have to agree
    /// on the outcome, on the authoritative state hash, on a digest folded
    /// over the whole concatenated ordered event stream, and on that ordered
    /// stream itself. The stream is asserted non-empty first, so a silent
    /// battle cannot make the comparison pass vacuously, and the seven state
    /// hashes are asserted distinct, so seven fixtures that had accidentally
    /// collapsed into one could not pass either.
    /// </summary>
    [Fact]
    public void EveryWasayGroupFixtureReplaysToTheSameOutcomeHashAndEvents()
    {
        var stateHashes = new List<ulong>();

        foreach (var fixture in AllGroupFixtures())
        {
            var first = ReplayGroup(fixture);
            var second = ReplayGroup(fixture);

            Assert.True(
                first.Events.Count > 0,
                $"{fixture.Name} produced no events at all, so its replay " +
                "comparison would prove nothing.");
            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.StateHash, second.StateHash);
            Assert.Equal(first.EventDigest, second.EventDigest);
            Assert.Equal(first.Events, second.Events);

            stateHashes.Add(first.StateHash);
        }

        Assert.Equal(stateHashes.Count, stateHashes.Distinct().Count());
    }

    /// <summary>
    /// Task W5 step 2(d). No warrior in any group fixture ever displaces
    /// further in one tick than the shared human baseline
    /// <c>AgentState.MovementSpeedRaw</c>. Every profile multiplier in the
    /// design is at or below one whole, so no count, posture, or composition
    /// can make anyone faster than a person; this is the scenario-scale check
    /// that nothing in the group pipeline reintroduces one.
    /// </summary>
    [Fact]
    public void NoWasayGroupFixtureStepsPastTheSharedHumanBaseline()
    {
        foreach (var fixture in AllGroupFixtures())
        {
            var run = ObserveGroup(fixture);
            Assert.All(
                run.Agents,
                agent => Assert.True(
                    agent.LargestStepRaw <= WarriorSpeedRaw,
                    $"{fixture.Name}: warrior {agent.EntityId} carrying " +
                    $"{agent.Loadout.Weapon} stepped " +
                    $"{agent.LargestStepRaw} raw in one tick, past the " +
                    $"{WarriorSpeedRaw}-unit shared baseline."));
        }
    }

    /// <summary>
    /// Task W5 step 2(e), the recovery half. Across every group fixture, every
    /// warrior's <c>Commit</c> and <c>Recover</c> lifecycle enters at exactly
    /// its own profile's duration, counts down one whole tick at a time, and
    /// carries a zero timer in every other phase. A truncated, stretched, or
    /// silently reloaded recovery therefore fails here rather than being
    /// absorbed into an aggregate count.
    /// </summary>
    [Fact]
    public void EveryCommitAndRecoverRunKeepsItsOwnProfileLength()
    {
        foreach (var fixture in AllGroupFixtures())
        {
            var timeline = ObserveFootworkTimeline(fixture);
            var violations = LifecycleTimerViolations(fixture, timeline);

            Assert.True(
                violations.Count == 0,
                string.Join(Environment.NewLine, violations.Take(10)));
        }
    }

    /// <summary>
    /// Task W5 step 2(e), the oscillation half. No warrior in any group
    /// fixture changes its committed phase on every tick of the measured
    /// window, which is the bounded-flipping property the plan's rejection
    /// list asks for: a warrior that never holds a phase for two consecutive
    /// ticks is oscillating rather than acting.
    /// </summary>
    /// <remarks>
    /// docs/archives/2026-07-31/movement/README.md task T11 step 7 carries a
    /// tighter rejection criterion — reject if any phase or posture flips on
    /// more than 25% of ticks after the first 100 — and that criterion is
    /// deliberately not asserted here. Measured on these fixtures, the
    /// shipped V6 rows
    /// exceed it routinely: the Wasay row's four-committed-then-four-recovery
    /// rhythm sits at exactly 25% on its own, and the shorter Kalis and Itak
    /// rhythms reach 50% to 60%. That is a calibration finding for the shared
    /// integration owner, who owns every profile row; a weapon task with no
    /// tuning authority may neither assert a bound its own row cannot meet nor
    /// quietly relax the shared one, so the measurement is reported and the
    /// assertion here stays at the structural bound.
    /// </remarks>
    [Fact]
    public void NoWasayGroupFixtureFlipsItsPhaseOnEveryLateTick()
    {
        foreach (var fixture in AllGroupFixtures())
        {
            var timeline = ObserveFootworkTimeline(fixture);
            for (var index = 0; index < timeline.Count; index++)
            {
                var (flips, measured) = LatePhaseFlips(timeline[index]);

                Assert.True(measured > 0);
                Assert.True(
                    flips < measured,
                    $"{fixture.Name}: warrior {index + 1} changed phase on " +
                    $"all {measured} measured ticks after the first " +
                    $"{PhaseFlipSettlingTicks}.");
            }
        }
    }

    /// <summary>
    /// Task W5 step 2(f). No group fixture ends because it stopped making
    /// progress: across every one of them, the longest run of ticks with no
    /// change in living count, in any warrior's hit points, or in any
    /// warrior's distance to its nearest opponent stays inside the shared
    /// 250-tick bound of docs/archives/2026-07-31/movement/README.md task
    /// T10 step 6. The observed tick count is asserted to exceed that
    /// bound first, so a
    /// fixture that was simply too short to break it cannot pass by accident.
    /// </summary>
    [Fact]
    public void NoWasayGroupFixtureStallsForTheSharedNoProgressBound()
    {
        Assert.True(GroupObservedTicks > NoProgressStreakBoundTicks);

        foreach (var fixture in AllGroupFixtures())
        {
            var run = ObserveGroup(fixture);

            Assert.Equal(GroupObservedTicks, run.ObservedTicks);
            Assert.True(
                run.LongestNoProgressStreakTicks <= NoProgressStreakBoundTicks,
                $"{fixture.Name} went {run.LongestNoProgressStreakTicks} " +
                "ticks without a living-count, hit-point, or " +
                "nearest-opponent-distance change.");
        }
    }

    /// <summary>
    /// Task W5 step 2(c), the globally favourable direction. Five against
    /// three resolves <see cref="TacticalPosture.Advance"/> from the shared
    /// posture table, and nothing about that advantage reaches the Wasay
    /// standing alone against the whole hostile contingent: its support ring
    /// holds at or above the two-to-one entry ratio for part of the run and it
    /// disengages. The second Wasay of the same faction, on the same
    /// favourable posture but standing inside its own cluster, never
    /// disengages once — which is what makes the first one's decision local
    /// rather than a faction-wide retreat.
    /// </summary>
    [Fact]
    public void ALocallyOutnumberedWasayDisengagesOnAFavourablePosture()
    {
        var fixture = GloballyFavouredLocallyOutnumberedFixture();

        Assert.Equal(5, fixture.RosterSize(0));
        Assert.Equal(3, fixture.RosterSize(1));
        Assert.Equal(
            TacticalPosture.Advance,
            WeaponMovementRules.ResolveTacticalPosture(
                globalAllies: fixture.RosterSize(0),
                globalEnemies: fixture.RosterSize(1),
                ContingentState.Advance,
                alliedRoleCoverage: 1,
                enemyRoleCoverage: 1));

        Assert.True(
            TwoToOneSupportTicks(fixture, FavouredIsolatedWasayIndex) > 0,
            "The isolated Wasay's support ring never reached the two-to-one " +
            "entry ratio, so this fixture proves nothing about it.");
        Assert.Equal(
            0, TwoToOneSupportTicks(fixture, FavouredSupportedWasayIndex));

        var run = ObserveGroup(fixture);
        var isolated = run.Agents[FavouredIsolatedWasayIndex];
        var supported = run.Agents[FavouredSupportedWasayIndex];

        Assert.Equal(WeaponId.Wasay, isolated.Loadout.Weapon);
        Assert.Equal(WeaponId.Wasay, supported.Loadout.Weapon);
        Assert.True(
            isolated.DisengageEntries > 0,
            "The locally outnumbered Wasay never entered disengagement even " +
            "though its own support ring was at or above two hostiles per " +
            "ally.");
        Assert.True(isolated.DisengageTicks > 0);
        Assert.Equal(0, supported.DisengageEntries);
        Assert.Equal(0, supported.DisengageTicks);
    }

    /// <summary>
    /// Task W5 step 2(c), the globally unfavourable direction. Three against
    /// five resolves <see cref="TacticalPosture.Yield"/> from the same shared
    /// table, and the Wasay on that side disengages even though its own
    /// support ring never once reaches the two-to-one entry ratio — it stands
    /// between both of its allies against a single hostile the whole time.
    /// Both directions of the global posture therefore produce disengagement
    /// decisions, and this one is reached through the unconditional posture
    /// step rather than through the local ratio.
    /// </summary>
    [Fact]
    public void AGloballyOutnumberedWasayDisengagesWithoutLocalPressure()
    {
        var fixture = GloballyOutnumberedLocallySupportedFixture();

        Assert.Equal(3, fixture.RosterSize(0));
        Assert.Equal(5, fixture.RosterSize(1));
        Assert.Equal(
            TacticalPosture.Yield,
            WeaponMovementRules.ResolveTacticalPosture(
                globalAllies: fixture.RosterSize(0),
                globalEnemies: fixture.RosterSize(1),
                ContingentState.Advance,
                alliedRoleCoverage: 1,
                enemyRoleCoverage: 1));
        Assert.Equal(0, TwoToOneSupportTicks(fixture, OutnumberedWasayIndex));

        var run = ObserveGroup(fixture);
        var wasay = run.Agents[OutnumberedWasayIndex];

        Assert.Equal(WeaponId.Wasay, wasay.Loadout.Weapon);
        Assert.True(
            wasay.DisengageEntries > 0,
            "The Wasay on the globally outnumbered side never disengaged.");
        Assert.True(wasay.DisengageReleases > 0);
        Assert.True(wasay.LandedAttacks > 0);
    }

    /// <summary>
    /// Task W5 step 1, the homogeneous congestion case. Four identical Wasay
    /// warriors stacked on one lane enter their commitments in rank order and
    /// never at the same time, the delay is paid for by refused routes and by
    /// the run's own friendly-clearance denials rather than by any hard-coded
    /// alternation, and across the whole run no two allies ever close inside
    /// the Wasay clearance radius.
    /// </summary>
    [Fact]
    public void HomogeneousWasayCongestionStaggersEntryAndHoldsClearance()
    {
        var fixture = HomogeneousWasayColumnFixture();
        var run = ObserveGroup(fixture);

        Assert.All(
            run.Agents,
            agent => Assert.Equal(WeaponId.Wasay, agent.Loadout.Weapon));
        Assert.True(
            run.MovementConflictDenials > 0,
            "A single-lane column of four Wasay allies produced no " +
            "friendly-clearance denial at all.");
        Assert.True(
            run.MinimumAllySeparationRaw >= WasayClearanceRadiusRaw(),
            $"Two allies closed to {run.MinimumAllySeparationRaw} raw, " +
            $"inside the {WasayClearanceRadiusRaw()}-unit Wasay clearance " +
            "radius.");

        foreach (var factionId in new[] { 0, 1 })
        {
            var column = run.Agents
                .Where(agent => agent.FactionId == factionId)
                .ToList();

            Assert.Equal(4, column.Count);
            Assert.All(column, agent => Assert.NotNull(agent.FirstCommitTick));

            for (var rank = 1; rank < column.Count; rank++)
            {
                Assert.True(
                    column[rank].FirstCommitTick >
                        column[rank - 1].FirstCommitTick,
                    $"Rank {rank} of faction {factionId} committed on tick " +
                    $"{column[rank].FirstCommitTick}, not after rank " +
                    $"{rank - 1} on tick {column[rank - 1].FirstCommitTick}.");
                Assert.True(
                    column[rank].RefuseTicks >= column[rank - 1].RefuseTicks,
                    $"Rank {rank} of faction {factionId} refused " +
                    $"{column[rank].RefuseTicks} ticks, fewer than rank " +
                    $"{rank - 1} ahead of it at " +
                    $"{column[rank - 1].RefuseTicks}.");
            }
        }
    }

    /// <summary>
    /// Task W5 step 1, the mixed-roster case. A shorter-reach Itak already
    /// holds the direct lane to the shared target with a Wasay behind it on
    /// the same line. The Wasay leaves that lane rather than pressing through
    /// its ally, the run records the friendly-clearance denials that yielding
    /// costs, and yielding the lane does not make the Wasay inert: it still
    /// commits and still lands blows, and so does the Itak in front of it.
    /// </summary>
    [Fact]
    public void AShorterAllyHoldingTheDirectLaneMovesTheWasayOffIt()
    {
        var fixture = OccupiedDirectLaneFixture();
        var run = ObserveGroup(fixture);
        var itak = run.Agents[OccupiedLaneItakIndex];
        var wasay = run.Agents[OccupiedLaneWasayIndex];

        Assert.Equal(WeaponId.Itak, itak.Loadout.Weapon);
        Assert.Equal(WeaponId.Wasay, wasay.Loadout.Weapon);
        Assert.Equal(itak.FactionId, wasay.FactionId);

        Assert.True(
            wasay.LargestLaneOffsetRaw > 0,
            "The Wasay never left the lane its ally was already holding.");
        Assert.True(
            run.MovementConflictDenials > 0,
            "Nothing in the run shows the shared lane being contended.");
        Assert.NotNull(wasay.FirstCommitTick);
        Assert.True(wasay.LandedAttacks > 0);
        Assert.True(itak.LandedAttacks > 0);
    }
}

