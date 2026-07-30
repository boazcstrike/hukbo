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
/// attack interrupt. Task W2 of docs/plans/movement/wasay.md.
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
}
