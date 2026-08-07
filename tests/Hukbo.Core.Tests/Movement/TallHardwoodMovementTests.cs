using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The tall-hardwood-shield behaviour pins for the two shielded rows —
/// shielded Kalis (<c>KS</c>, canonical loadout index 4) and shielded Itak
/// (<c>IS</c>, canonical loadout index 5): the local-count disengagement
/// hysteresis, the offset-adjusted preferred-engagement band across all six
/// opponent columns, the facing budgets, the direction-band and committed pace
/// caps, retained-pace acceleration and deceleration, the commitment and
/// recovery ladder, the collision-denial pace clamp, ally lane clearance
/// against every other canonical row, the second-threat lane omission, and
/// route tie stability. Every numeric expectation is derived inline from
/// <see cref="TallHardwoodMovementProfiles"/> and the probe constants named
/// below, so a row change fails here with arithmetic a reader can follow.
/// Every tuning value is a provisional reconstruction: gameplay tuning; no
/// historical measurement. Evidence ledger:
/// docs/research/movement/tall-hardwood-shield.md.
/// </summary>
/// <remarks>
/// These tests assert the conventions the shared foundation actually
/// implemented rather than the variants the weapon plan proposes. Four matter
/// here. First, the preferred distance is not a stop line: equality enters
/// <see cref="FootworkPhase.Engage"/> inclusively and the agent keeps closing
/// to the post-movement reach gate. Second, the commitment and recovery
/// continuations sit at steps 2 and 3 of
/// <see cref="WeaponMovementRules.ResolveProvisionalFootwork"/>, ahead of the
/// ratio checks at steps 4 and 5, so local pressure cannot cancel a
/// commitment. Third, ally clearance rejects only a strictly closer endpoint,
/// so exact equality is clear. Fourth, neither shield row carries a facing
/// penalty: both turn at the solo budget of two sectors, because the research
/// candidate multiplier of 0.88 is unrepresentable in sixteen sectors and was
/// deliberately not adopted. Movement here responds to geometry and to counts
/// only — no assertion in this file reads which way a shield faces, and no
/// shield row grants a speed bonus.
/// </remarks>
public sealed class TallHardwoodMovementTests
{
    // The shared 1v1 probe constants, matching the derivation the foundation
    // pipeline tests and the two merged weapon sessions use, so expected
    // values are comparable across files.
    private const int AttackRangeRaw = 5 * FixedPoint.Scale;    // 5120
    private const int BodyRadiusRaw = FixedPoint.Scale / 2;     // 512
    private const int MovementSpeedRaw = FixedPoint.Scale / 2;  // 512

    /// <summary>The shielded Kalis (<c>KS</c>) row under test.</summary>
    private static LoadoutMovementProfile KalisShieldRow =>
        TallHardwoodMovementProfiles.KalisRow;

    /// <summary>The shielded Itak (<c>IS</c>) row under test.</summary>
    private static LoadoutMovementProfile ItakShieldRow =>
        TallHardwoodMovementProfiles.ItakRow;

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

    /// <summary>
    /// Selects a shield row from a theory discriminator, because
    /// <see cref="LoadoutMovementProfile"/> is not an
    /// <see cref="InlineDataAttribute"/> literal.
    /// </summary>
    private static LoadoutMovementProfile ShieldRow(bool shieldedKalis) =>
        shieldedKalis ? KalisShieldRow : ItakShieldRow;

    // ----- Disengagement entry, both rows (design 9.2; plan H3) -----

    /// <summary>
    /// Shielded Kalis enters disengagement at an enemy-to-ally support ratio
    /// of 17,500 basis points, which reduces to the exact integer pair seven
    /// enemies against four allies. Entry equality enters, one enemy short
    /// does not, and one enemy over does. Provisional reconstruction: gameplay
    /// tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// The comparison is <c>enemies * 10000 &gt;= allies * 17500</c>, which
    /// cancels to <c>enemies * 4 &gt;= allies * 7</c>:
    /// <c>6 * 10000 = 60000 &lt; 70000</c>, <c>7 * 10000 = 70000</c> exactly,
    /// and <c>8 * 10000 = 80000 &gt; 70000</c>.
    /// </remarks>
    [Theory]
    [InlineData(6, FootworkPhase.Engage)]     // one enemy short of the ratio
    [InlineData(7, FootworkPhase.Disengage)]  // exact equality enters
    [InlineData(8, FootworkPhase.Disengage)]  // one enemy past the ratio
    public void TheShieldedKalisEntersDisengagementAtSevenEnemiesPerFourAllies(
        int supportEnemies, FootworkPhase expectedPhase)
    {
        Assert.Equal(
            17_500, KalisShieldRow.DisengageEnemyToAllyBasisPoints);

        var (phase, ticksRemaining) = Resolve(
            KalisShieldRow,
            supportAllies: 4,
            supportEnemies: supportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);

        Assert.Equal(expectedPhase, phase);
        Assert.Equal(0, ticksRemaining);
    }

    /// <summary>
    /// Shielded Itak enters disengagement at 15,000 basis points, which
    /// reduces to the exact integer pair three enemies against two allies —
    /// the closer-repositioning row leaves earlier than the longer-spacing
    /// Kalis one. Provisional reconstruction: gameplay tuning; no historical
    /// measurement (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// <c>enemies * 10000 &gt;= allies * 15000</c> cancels to
    /// <c>enemies * 2 &gt;= allies * 3</c>:
    /// <c>2 * 10000 = 20000 &lt; 30000</c>, <c>3 * 10000 = 30000</c> exactly,
    /// and <c>4 * 10000 = 40000 &gt; 30000</c>.
    /// </remarks>
    [Theory]
    [InlineData(2, FootworkPhase.Engage)]     // one enemy short of the ratio
    [InlineData(3, FootworkPhase.Disengage)]  // exact equality enters
    [InlineData(4, FootworkPhase.Disengage)]  // one enemy past the ratio
    public void TheShieldedItakEntersDisengagementAtThreeEnemiesPerTwoAllies(
        int supportEnemies, FootworkPhase expectedPhase)
    {
        Assert.Equal(15_000, ItakShieldRow.DisengageEnemyToAllyBasisPoints);

        var (phase, ticksRemaining) = Resolve(
            ItakShieldRow,
            supportAllies: 2,
            supportEnemies: supportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);

        Assert.Equal(expectedPhase, phase);
        Assert.Equal(0, ticksRemaining);
    }

    /// <summary>
    /// Both shield rows release disengagement at 11,000 basis points, the
    /// exact integer pair eleven enemies against ten allies. Release equality
    /// leaves, one enemy short leaves as well, and one enemy over holds the
    /// warrior in disengagement. Provisional reconstruction: gameplay tuning;
    /// no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// The release is the strict comparison
    /// <c>enemies * 10000 &gt; allies * 11000</c>, which cancels to
    /// <c>enemies * 10 &gt; allies * 11</c>:
    /// <c>10 * 10000 = 100000 &lt; 110000</c> leaves,
    /// <c>11 * 10000 = 110000</c> is not strictly greater so it also leaves,
    /// and <c>12 * 10000 = 120000 &gt; 110000</c> remains.
    /// </remarks>
    [Theory]
    [InlineData(true, 10, FootworkPhase.Engage)]      // below, leaves
    [InlineData(true, 11, FootworkPhase.Engage)]      // equality leaves
    [InlineData(true, 12, FootworkPhase.Disengage)]   // above, remains
    [InlineData(false, 10, FootworkPhase.Engage)]
    [InlineData(false, 11, FootworkPhase.Engage)]
    [InlineData(false, 12, FootworkPhase.Disengage)]
    public void BothShieldRowsLeaveDisengagementAtElevenEnemiesPerTenAllies(
        bool shieldedKalis, int supportEnemies, FootworkPhase expectedPhase)
    {
        var row = ShieldRow(shieldedKalis);
        Assert.Equal(11_000, row.ReengageEnemyToAllyBasisPoints);

        var (phase, _) = Resolve(
            row,
            priorPhase: FootworkPhase.Disengage,
            supportAllies: 10,
            supportEnemies: supportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);

        Assert.Equal(expectedPhase, phase);
    }

    /// <summary>
    /// A ratio strictly between a row's release and entry thresholds preserves
    /// the prior state in both directions: an already-disengaging shield
    /// bearer stays disengaging and an engaged one does not begin. Provisional
    /// reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// Shielded Kalis with four allies: release bound
    /// <c>4 * 11000 = 44000</c>, entry bound <c>4 * 17500 = 70000</c>, and six
    /// enemies scale to <c>60000</c>, strictly inside both. Shielded Itak with
    /// four allies: release bound <c>44000</c>, entry bound
    /// <c>4 * 15000 = 60000</c>, and five enemies scale to <c>50000</c>,
    /// strictly inside both.
    /// </remarks>
    [Theory]
    [InlineData(true, 4, 6)]
    [InlineData(false, 4, 5)]
    public void ARatioStrictlyBetweenTheShieldThresholdsPreservesThePriorPhase(
        bool shieldedKalis, int supportAllies, int supportEnemies)
    {
        var row = ShieldRow(shieldedKalis);
        var scaledEnemies = (long)supportEnemies * 10_000;
        Assert.True(
            scaledEnemies >
                (long)supportAllies * row.ReengageEnemyToAllyBasisPoints);
        Assert.True(
            scaledEnemies <
                (long)supportAllies * row.DisengageEnemyToAllyBasisPoints);

        var (heldDisengaged, _) = Resolve(
            row,
            priorPhase: FootworkPhase.Disengage,
            supportAllies: supportAllies,
            supportEnemies: supportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);
        Assert.Equal(FootworkPhase.Disengage, heldDisengaged);

        var (stayedEngaged, _) = Resolve(
            row,
            priorPhase: FootworkPhase.Engage,
            supportAllies: supportAllies,
            supportEnemies: supportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);
        Assert.Equal(FootworkPhase.Engage, stayedEngaged);
    }

    /// <summary>
    /// With no living perceived enemy the ratio arithmetic alone never enters
    /// disengagement and never remains in it, under either shield row and
    /// whatever the prior phase. Nothing divides, so there is no
    /// zero-denominator path to guard. The counterpart on the Itak side is
    /// <c>ItakMovementTransitionTests</c>, method
    /// <c>ZeroEnemiesNeverEntersAndNeverRemainsUnderEitherItakRow</c>.
    /// </summary>
    [Theory]
    [InlineData(true, FootworkPhase.None)]
    [InlineData(true, FootworkPhase.Disengage)]
    [InlineData(false, FootworkPhase.None)]
    [InlineData(false, FootworkPhase.Disengage)]
    public void ZeroLivingEnemiesNeverDisengagesUnderEitherShieldRow(
        bool shieldedKalis, FootworkPhase priorPhase) =>
        Assert.Equal(
            (FootworkPhase.None, 0),
            Resolve(
                ShieldRow(shieldedKalis),
                priorPhase: priorPhase,
                supportAllies: 1,
                supportEnemies: 0));

    /// <summary>
    /// The actor counts as one support ally, so a lone shield bearer facing
    /// two enemies enters disengagement under both rows while one enemy leaves
    /// it engaged. Provisional reconstruction: gameplay tuning; no historical
    /// measurement (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// With <c>supportAllies</c> of one, shielded Kalis needs
    /// <c>enemies * 10000 &gt;= 17500</c> and shielded Itak
    /// <c>enemies * 10000 &gt;= 15000</c>. One enemy scales to
    /// <c>10000</c>, below both; two scale to <c>20000</c>, at or above both.
    /// </remarks>
    [Theory]
    [InlineData(true, 1, FootworkPhase.Engage)]
    [InlineData(true, 2, FootworkPhase.Disengage)]
    [InlineData(false, 1, FootworkPhase.Engage)]
    [InlineData(false, 2, FootworkPhase.Disengage)]
    public void ALoneShieldBearerCountsItselfAsOneSupportAlly(
        bool shieldedKalis, int supportEnemies, FootworkPhase expectedPhase)
    {
        var (phase, _) = Resolve(
            ShieldRow(shieldedKalis),
            supportAllies: 1,
            supportEnemies: supportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);

        Assert.Equal(expectedPhase, phase);
    }

    /// <summary>
    /// A dead ally contributes to no tally, and its exclusion is load bearing
    /// here: the shielded Kalis actor stands alone against two living enemies
    /// and enters disengagement, whereas counting the corpse beside it would
    /// have kept it engaged. The counts travel from
    /// <see cref="MovementContextQuery.Derive"/> into the ladder, because
    /// <see cref="WeaponMovementRules.ResolveProvisionalFootwork"/> takes
    /// plain integers and cannot see an agent array.
    /// </summary>
    /// <remarks>
    /// One living ally counted would give <c>2 * 10000 = 20000</c> against an
    /// entry bound of <c>2 * 17500 = 35000</c> and no entry. With the actor
    /// alone the bound is <c>17500</c> and <c>20000</c> clears it.
    /// </remarks>
    [Fact]
    public void ADeadAllyDoesNotCountTowardTheShieldSupportAllyTally()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(
            1, 0, 100_000, 51_200, scenario, ShieldedKalis);
        var deadAlly = CreateAgent(
            2, 0, 100_000 - 3_000, 51_200, scenario, ShieldedItak);
        deadAlly.HitPoints = 0;
        var firstEnemy = CreateAgent(
            3, 1, 100_000 + 2_000, 51_200, scenario, SoloItak);
        var secondEnemy = CreateAgent(
            4, 1, 100_000 + 2_400, 51_200, scenario, SoloItak);

        var context = Derive(
            scenario, actor, [actor, deadAlly, firstEnemy, secondEnemy]);

        Assert.Equal(1, context.SupportAllies);
        Assert.Equal(2, context.SupportEnemies);

        var (phase, _) = Resolve(
            KalisShieldRow,
            supportAllies: context.SupportAllies,
            supportEnemies: context.SupportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);
        Assert.Equal(FootworkPhase.Disengage, phase);
    }

    /// <summary>
    /// An enemy outside the support radius contributes to no tally even while
    /// it remains inside perception, and the exclusion is load bearing: the
    /// lone shielded Kalis actor sees one enemy inside the radius and stays
    /// engaged, whereas counting the two distant ones would have entered
    /// disengagement. Radius membership itself is a
    /// <see cref="MovementRuleset"/> field shared by all six rows and no
    /// shield row can move it; the inclusive-at-the-radius convention is
    /// pinned by <c>MovementContextObservationTests</c>.
    /// </summary>
    /// <remarks>
    /// The support radius is <c>1024 * 60000 / 10000 = 6144</c> raw. One
    /// counted enemy gives <c>10000 &lt; 17500</c> and no entry; three would
    /// give <c>30000 &gt;= 17500</c> and enter.
    /// </remarks>
    [Fact]
    public void EnemiesBeyondTheSupportRadiusDoNotCountTowardTheShieldTally()
    {
        var scenario = CreateScenario();
        var supportRaw = MovementContextQuery.ContextRadiusRaw(
            BodyRadiusRaw, V6.SupportRadiusBodyDiametersBasisPoints);
        Assert.Equal(6_144L, supportRaw);

        var actor = CreateAgent(
            1, 0, 100_000, 51_200, scenario, ShieldedKalis);
        var nearEnemy = CreateAgent(
            2, 1, 100_000 + 2_000, 51_200, scenario, SoloItak);
        var farEnemy = CreateAgent(
            3, 1, 100_000 + (int)supportRaw + 1, 51_200, scenario, SoloItak);
        var fartherEnemy = CreateAgent(
            4, 1, 100_000 + (int)supportRaw + 4_000, 51_200, scenario,
            SoloItak);

        var context = Derive(
            scenario, actor, [actor, nearEnemy, farEnemy, fartherEnemy]);

        Assert.Equal(1, context.SupportAllies);
        Assert.Equal(1, context.SupportEnemies);

        var (phase, _) = Resolve(
            KalisShieldRow,
            supportAllies: context.SupportAllies,
            supportEnemies: context.SupportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);
        Assert.Equal(FootworkPhase.Engage, phase);
    }

    /// <summary>
    /// The ratio comparisons widen before multiplying, so counts far beyond
    /// any reachable roster neither overflow nor mis-compare under either
    /// shield row. Three hundred thousand enemies scaled by ten thousand is
    /// three billion, which no <see cref="int"/> can hold.
    /// </summary>
    /// <remarks>
    /// The source invariant is that
    /// <c>WeaponMovementRules.RatioBasisPointScale</c> is declared
    /// <c>private const long</c> and both cross-products are written
    /// <c>checked((long)supportAllies * threshold)</c>, so the products live
    /// in sixty-four bits: <c>300000 * 10000 = 3000000000</c> against
    /// <c>1 * 17500</c> enters, and <c>1 * 10000</c> against
    /// <c>300000 * 17500 = 5250000000</c> does not.
    /// </remarks>
    [Theory]
    [InlineData(true, 1, 300_000, FootworkPhase.Disengage)]
    [InlineData(true, 300_000, 1, FootworkPhase.Engage)]
    [InlineData(false, 1, 300_000, FootworkPhase.Disengage)]
    [InlineData(false, 300_000, 1, FootworkPhase.Engage)]
    public void TheShieldRatiosSurviveCountsThatOverflowThirtyTwoBits(
        bool shieldedKalis,
        int supportAllies,
        int supportEnemies,
        FootworkPhase expectedPhase)
    {
        var (phase, _) = Resolve(
            ShieldRow(shieldedKalis),
            supportAllies: supportAllies,
            supportEnemies: supportEnemies,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);

        Assert.Equal(expectedPhase, phase);
    }

    /// <summary>
    /// Local pressure cannot cancel a shield commitment or its recovery: the
    /// continuation steps sit at positions two and three of the ladder, ahead
    /// of the release and entry checks at four and five. Four enemies against
    /// one ally is far past both rows' entry thresholds and a
    /// <see cref="TacticalPosture.Withdraw"/> posture would disengage on its
    /// own, yet the lifecycle runs to its end.
    /// </summary>
    [Theory]
    [InlineData(true, FootworkPhase.Commit, 3, FootworkPhase.Commit, 2)]
    [InlineData(true, FootworkPhase.Commit, 1, FootworkPhase.Recover, 3)]
    [InlineData(true, FootworkPhase.Recover, 3, FootworkPhase.Recover, 2)]
    [InlineData(false, FootworkPhase.Commit, 3, FootworkPhase.Commit, 2)]
    [InlineData(false, FootworkPhase.Commit, 1, FootworkPhase.Recover, 3)]
    [InlineData(false, FootworkPhase.Recover, 3, FootworkPhase.Recover, 2)]
    public void LocalPressureDoesNotCancelAShieldCommitmentOrItsRecovery(
        bool shieldedKalis,
        FootworkPhase priorPhase,
        int priorTicksRemaining,
        FootworkPhase expectedPhase,
        int expectedTicksRemaining)
    {
        var (phase, ticksRemaining) = Resolve(
            ShieldRow(shieldedKalis),
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

    // ----- Effective preferred distance (design 4.4; plan H3) -----

    /// <summary>
    /// The shielded Kalis row against every canonical opponent column, at the
    /// canonical five-unit attack range of 5,120 raw. The row's preferred
    /// distance is 13,000 basis points with offsets
    /// <c>[-250, 0, 250, 500, 0, 250]</c>, so each effective value is
    /// <c>5120 * (13000 + offset) / 10000</c>, truncating toward zero. The
    /// flat 1.3 reach multiple holds only for the two zero-offset columns,
    /// Wasay and shielded Kalis. Provisional reconstruction: gameplay tuning;
    /// no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(0, 6_528L)]  // KP, offset -250: 5120 * 12750 / 10000
    [InlineData(1, 6_656L)]  // WA, offset    0: 5120 * 13000 / 10000
    [InlineData(2, 6_784L)]  // KA, offset +250: 5120 * 13250 / 10000
    [InlineData(3, 6_912L)]  // IT, offset +500: 5120 * 13500 / 10000
    [InlineData(4, 6_656L)]  // KS, offset    0: 5120 * 13000 / 10000
    [InlineData(5, 6_784L)]  // IS, offset +250: 5120 * 13250 / 10000
    public void TheShieldedKalisPreferredDistanceCoversEveryOpponentColumn(
        int opponentCanonicalIndex, long expectedRaw) =>
        Assert.Equal(
            expectedRaw,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, KalisShieldRow, opponentCanonicalIndex));

    /// <summary>
    /// The shielded Itak row against every canonical opponent column, from the
    /// shield-owned perspective. Its preferred distance is 10,000 basis points
    /// with offsets <c>[-500, -250, 0, 250, -250, 0]</c>, so each effective
    /// value is <c>5120 * (10000 + offset) / 10000</c>. The registry-side twin
    /// of this pin is
    /// <c>ItakMovementProfileTests</c>, method
    /// <c>TheShieldedItakEffectivePreferredDistanceCoversEveryOpponentColumn</c>;
    /// the duplication is deliberate, because the weapon plan requires a
    /// shield-owned pin. Provisional reconstruction: gameplay tuning; no
    /// historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(0, 4_864L)]  // KP, offset -500: 5120 *  9500 / 10000
    [InlineData(1, 4_992L)]  // WA, offset -250: 5120 *  9750 / 10000
    [InlineData(2, 5_120L)]  // KA, offset    0: 5120 * 10000 / 10000
    [InlineData(3, 5_248L)]  // IT, offset +250: 5120 * 10250 / 10000
    [InlineData(4, 4_992L)]  // KS, offset -250: 5120 *  9750 / 10000
    [InlineData(5, 5_120L)]  // IS, offset    0: 5120 * 10000 / 10000
    public void TheShieldedItakPreferredDistanceCoversEveryOpponentColumn(
        int opponentCanonicalIndex, long expectedRaw) =>
        Assert.Equal(
            expectedRaw,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, ItakShieldRow, opponentCanonicalIndex));

    /// <summary>
    /// Shielded Kalis holds the longer band and shielded Itak the closer one
    /// against every single opponent column, which is the product statement
    /// that Kalis is the lane-control pairing and Itak the repositioning one.
    /// Provisional reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void TheShieldedKalisBandExceedsTheShieldedItakBandInEveryColumn(
        int opponentCanonicalIndex) =>
        Assert.True(
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, KalisShieldRow, opponentCanonicalIndex) >
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, ItakShieldRow, opponentCanonicalIndex));

    /// <summary>
    /// The band test is inclusive on squared values, for both rows and against
    /// both a zero-offset and a nonzero-offset opponent column. One raw unit
    /// outside stays <see cref="FootworkPhase.Approach"/>; exact equality and
    /// one raw unit inside both reach <see cref="FootworkPhase.Engage"/>. No
    /// square root is taken and no floating point appears in the chain.
    /// Provisional reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// The caller compares squared values inclusively, so for the shielded
    /// Kalis band of 6,656 against Wasay the three probes are
    /// <c>6657^2 = 44315649</c> outside, <c>6656^2 = 44302336</c> equal, and
    /// <c>6655^2 = 44289025</c> inside.
    /// </remarks>
    [Theory]
    [InlineData(true, 1, 6_656L)]   // KS versus WA, offset cell zero
    [InlineData(true, 3, 6_912L)]   // KS versus IT, offset cell +500
    [InlineData(false, 2, 5_120L)]  // IS versus KA, offset cell zero
    [InlineData(false, 0, 4_864L)]  // IS versus KP, offset cell -500
    public void ThePreferredBandIsInclusiveAtExactEqualityForBothShieldRows(
        bool shieldedKalis, int opponentCanonicalIndex, long expectedRadiusRaw)
    {
        var row = ShieldRow(shieldedKalis);
        var radiusRaw = MovementRouteRules.EffectivePreferredDistanceRaw(
            AttackRangeRaw, row, opponentCanonicalIndex);
        Assert.Equal(expectedRadiusRaw, radiusRaw);

        var radiusSquared = Square(radiusRaw);
        (long SeparationRaw, bool ExpectedInside)[] probes =
        [
            (radiusRaw + 1, false),
            (radiusRaw, true),
            (radiusRaw - 1, true),
        ];

        Assert.All(
            probes,
            probe =>
            {
                var insideBand = Square(probe.SeparationRaw) <= radiusSquared;
                Assert.Equal(probe.ExpectedInside, insideBand);

                var (phase, ticksRemaining) = Resolve(
                    row,
                    hasTarget: true,
                    targetAtOrInsidePreferredDistance: insideBand);
                Assert.Equal(
                    probe.ExpectedInside
                        ? FootworkPhase.Engage
                        : FootworkPhase.Approach,
                    phase);
                Assert.Equal(0, ticksRemaining);
            });
    }

    /// <summary>
    /// The one and only channel through which an opponent's loadout reaches a
    /// shield bearer's spacing is a single offset cell selected by
    /// <see cref="MovementRouteRules.CanonicalOpponentIndex"/>: every effective
    /// preferred distance re-derives exactly from the row's own base plus that
    /// one cell, with no further scaling of any kind and no shield-conditional
    /// term. Provisional reconstruction: gameplay tuning; no historical
    /// measurement (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// The offsets are also symmetric about the shield split in the way the
    /// design intends and nowhere else: the actor's own weapon decides the
    /// base, and the opponent's triple decides only which of the six cells is
    /// read. Whether the opponent carries a shield changes the cell but never
    /// the actor's reach, which the whole-tick probe
    /// <c>AShieldedOpponentChangesOnlySpacingAndNotTargetOrReach</c> asserts
    /// against authoritative agent state.
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnlyTheOffsetCellCarriesTheOpponentIntoAShieldRowsSpacing(
        bool shieldedKalis)
    {
        var row = ShieldRow(shieldedKalis);

        Assert.All(
            MovementScenarioMatrix.CanonicalLoadouts,
            opponent =>
            {
                var opponentIndex =
                    MovementRouteRules.CanonicalOpponentIndex(opponent);
                var expectedRaw =
                    ((long)AttackRangeRaw *
                        (row.PreferredDistanceBasisPoints +
                            row.OpponentDistanceOffsetBasisPoints[
                                opponentIndex])) / 10_000;

                Assert.Equal(
                    expectedRaw,
                    MovementRouteRules.EffectivePreferredDistanceRaw(
                        AttackRangeRaw, row, opponentIndex));

                // Rank is social standing and never movement input, so the
                // same triple under a different rank selects the same cell.
                Assert.Equal(
                    opponentIndex,
                    MovementRouteRules.CanonicalOpponentIndex(
                        new CombatLoadout(
                            opponent.Weapon,
                            opponent.Armor,
                            opponent.Shield,
                            RankId.Datu)));
            });
    }

    // ----- Facing budgets (design 6.3; plan H4) -----

    /// <summary>
    /// Ordinary shield facing turns at most two of sixteen sectors per tick and
    /// committed facing at most one, for both rows. An exact eight-sector
    /// opposition is a tie, which turns clockwise in faction-canonical space.
    /// Provisional reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OrdinaryShieldFacingTurnsTwoSectorsAndCommittedFacingTurnsOne(
        bool shieldedKalis)
    {
        var row = ShieldRow(shieldedKalis);
        Assert.Equal(2, row.MaximumFacingStepsPerTick);
        Assert.Equal(1, row.CommittedFacingStepsPerTick);

        // East, sector 0, to West, sector 8, is eight steps either way; the
        // tie turns clockwise, so two steps lands on SouthEast, sector 2, and
        // one step on EastSouthEast, sector 1.
        Assert.Equal(
            Facing16.SouthEast,
            FacingRules.TurnToward(
                Facing16.East,
                Facing16.West,
                row.MaximumFacingStepsPerTick,
                factionId: 0));
        Assert.Equal(
            Facing16.EastSouthEast,
            FacingRules.TurnToward(
                Facing16.East,
                Facing16.West,
                row.CommittedFacingStepsPerTick,
                factionId: 0));
    }

    /// <summary>
    /// Neither shield row carries a facing penalty: both turn at the solo
    /// budget of two ordinary sectors and one committed sector, exactly as
    /// their solo counterparts do. The research candidate multiplier of 0.88
    /// was deliberately not adopted, being unrepresentable in sixteen
    /// sectors, so this equality is asserted explicitly rather than left
    /// implicit. A shield changes where a warrior may stand, never how fast it
    /// may look somewhere else, and never how fast it may move.
    /// </summary>
    [Fact]
    public void BothShieldRowsTurnAtTheSoloFacingBudgetWithNoShieldPenalty()
    {
        Assert.Equal(
            KalisMovementProfile.Row.MaximumFacingStepsPerTick,
            KalisShieldRow.MaximumFacingStepsPerTick);
        Assert.Equal(
            KalisMovementProfile.Row.CommittedFacingStepsPerTick,
            KalisShieldRow.CommittedFacingStepsPerTick);
        Assert.Equal(
            ItakMovementProfile.Row.MaximumFacingStepsPerTick,
            ItakShieldRow.MaximumFacingStepsPerTick);
        Assert.Equal(
            ItakMovementProfile.Row.CommittedFacingStepsPerTick,
            ItakShieldRow.CommittedFacingStepsPerTick);

        Assert.Equal(2, KalisShieldRow.MaximumFacingStepsPerTick);
        Assert.Equal(2, ItakShieldRow.MaximumFacingStepsPerTick);
    }

    // ----- Direction bands, pace caps, and retained pace (design 6.4, 6.5) --

    /// <summary>
    /// The direction band selects the shield row's own cap: forward within one
    /// sector of the facing, lateral out to five, backward beyond. Shielded
    /// Kalis runs 94, 84, and 67 percent of the shared baseline and shielded
    /// Itak 97, 87, and 71 percent. Every one of the six values sits below one
    /// whole, so no shield row can be faster than an unencumbered warrior.
    /// Provisional reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// At the 512-raw baseline the truncated desired paces are
    /// <c>512 * 9400 / 10000 = 481</c>, <c>512 * 8400 / 10000 = 430</c>, and
    /// <c>512 * 6700 / 10000 = 343</c> for shielded Kalis, and
    /// <c>512 * 9700 / 10000 = 496</c>, <c>512 * 8700 / 10000 = 445</c>, and
    /// <c>512 * 7100 / 10000 = 363</c> for shielded Itak.
    /// </remarks>
    [Theory]
    [InlineData(true, 0, 9_400, 481)]
    [InlineData(true, 1, 9_400, 481)]
    [InlineData(true, 2, 8_400, 430)]
    [InlineData(true, 5, 8_400, 430)]
    [InlineData(true, 6, 6_700, 343)]
    [InlineData(true, 8, 6_700, 343)]
    [InlineData(false, 0, 9_700, 496)]
    [InlineData(false, 1, 9_700, 496)]
    [InlineData(false, 2, 8_700, 445)]
    [InlineData(false, 5, 8_700, 445)]
    [InlineData(false, 6, 7_100, 363)]
    [InlineData(false, 8, 7_100, 363)]
    public void TheShieldDirectionBandsCapPaceAtTheRowsApprovedRatios(
        bool shieldedKalis,
        int separationSectors,
        int expectedCapBasisPoints,
        int expectedPaceRaw)
    {
        var capBasisPoints = FacingRules.DirectionBandPaceCapBasisPoints(
            ShieldRow(shieldedKalis), separationSectors);

        Assert.Equal(expectedCapBasisPoints, capBasisPoints);
        Assert.Equal(
            expectedPaceRaw,
            MovementRouteRules.DesiredPaceRaw(
                MovementSpeedRaw, capBasisPoints));
    }

    /// <summary>
    /// While committed the band cap is clamped by the row's committed cap,
    /// which sits below every band, so a committed shield bearer moves at 30
    /// percent of the shared baseline as shielded Kalis and 35 percent as
    /// shielded Itak whatever direction it faces. This is the whole of the
    /// weapon plan's "shared engaged-entry cap": it is the direction-band pace
    /// clamped by <c>CommittedPaceBasisPoints</c> and capped by
    /// <c>Scenario.MovementSpeedRaw</c>, not a separate field and not a named
    /// symbol. Provisional reconstruction: gameplay tuning; no historical
    /// measurement (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// <c>512 * 3000 / 10000 = 153</c> for shielded Kalis and
    /// <c>512 * 3500 / 10000 = 179</c> for shielded Itak, both truncating.
    /// </remarks>
    [Theory]
    [InlineData(true, 0, 3_000, 153)]
    [InlineData(true, 2, 3_000, 153)]
    [InlineData(true, 8, 3_000, 153)]
    [InlineData(false, 0, 3_500, 179)]
    [InlineData(false, 2, 3_500, 179)]
    [InlineData(false, 8, 3_500, 179)]
    public void TheCommittedClampIsTheMinimumOfTheShieldBandAndCommittedCap(
        bool shieldedKalis,
        int separationSectors,
        int expectedCommittedBasisPoints,
        int expectedPaceRaw)
    {
        var row = ShieldRow(shieldedKalis);
        var bandBasisPoints = FacingRules.DirectionBandPaceCapBasisPoints(
            row, separationSectors);
        var committedBasisPoints = Math.Min(
            bandBasisPoints, row.CommittedPaceBasisPoints);

        Assert.Equal(expectedCommittedBasisPoints, committedBasisPoints);
        Assert.Equal(
            expectedPaceRaw,
            MovementRouteRules.DesiredPaceRaw(
                MovementSpeedRaw, committedBasisPoints));
        Assert.True(
            MovementRouteRules.DesiredPaceRaw(
                MovementSpeedRaw, committedBasisPoints) <= MovementSpeedRaw);
    }

    /// <summary>
    /// Retained pace rises by at most the row's acceleration step per tick and
    /// falls by at most its deceleration step, never overshooting the target in
    /// either direction. Shielded Kalis accelerates at 5,600 basis points and
    /// decelerates at 6,000; shielded Itak at 6,500 and 7,000. Provisional
    /// reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// At the 512-raw baseline the shielded Kalis steps are
    /// <c>512 * 5600 / 10000 = 286</c> (286.72 truncates) and
    /// <c>512 * 6000 / 10000 = 307</c> (307.2). Accelerating toward the
    /// forward pace 481 therefore runs 0, 286, 481, and then holds;
    /// decelerating toward the committed pace 153 runs 481, 174, and then
    /// holds, because <c>481 - 307 = 174</c> is still above 153.
    /// </remarks>
    [Fact]
    public void TheShieldedKalisRetainedPaceRisesAndFallsByOneBoundedStep()
    {
        var accelerationStep = MovementRouteRules.PaceStepRaw(
            MovementSpeedRaw, KalisShieldRow.AccelerationBasisPointsPerTick);
        var decelerationStep = MovementRouteRules.PaceStepRaw(
            MovementSpeedRaw, KalisShieldRow.DecelerationBasisPointsPerTick);
        Assert.Equal(286, accelerationStep);
        Assert.Equal(307, decelerationStep);

        // An evenly divisible baseline proves the ratio without truncation.
        Assert.Equal(
            5_600,
            MovementRouteRules.PaceStepRaw(
                10_000, KalisShieldRow.AccelerationBasisPointsPerTick));
        Assert.Equal(
            6_000,
            MovementRouteRules.PaceStepRaw(
                10_000, KalisShieldRow.DecelerationBasisPointsPerTick));

        var pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            0, 481, accelerationStep, decelerationStep);
        Assert.Equal(286, pace);
        pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            pace, 481, accelerationStep, decelerationStep);
        Assert.Equal(481, pace);
        pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            pace, 481, accelerationStep, decelerationStep);
        Assert.Equal(481, pace);

        pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            481, 153, accelerationStep, decelerationStep);
        Assert.Equal(174, pace);
        pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            pace, 153, accelerationStep, decelerationStep);
        Assert.Equal(153, pace);
    }

    /// <summary>
    /// The shielded Itak counterpart of the same ramp. Provisional
    /// reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// <c>512 * 6500 / 10000 = 332</c> (332.8 truncates) and
    /// <c>512 * 7000 / 10000 = 358</c> (358.4). Accelerating toward the
    /// forward pace 496 runs 0, 332, 496; decelerating toward the committed
    /// pace 179 runs 496, 179 in one step, because <c>496 - 358 = 138</c> is
    /// already below 179 and the step never overshoots.
    /// </remarks>
    [Fact]
    public void TheShieldedItakRetainedPaceRisesAndFallsByOneBoundedStep()
    {
        var accelerationStep = MovementRouteRules.PaceStepRaw(
            MovementSpeedRaw, ItakShieldRow.AccelerationBasisPointsPerTick);
        var decelerationStep = MovementRouteRules.PaceStepRaw(
            MovementSpeedRaw, ItakShieldRow.DecelerationBasisPointsPerTick);
        Assert.Equal(332, accelerationStep);
        Assert.Equal(358, decelerationStep);

        var pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            0, 496, accelerationStep, decelerationStep);
        Assert.Equal(332, pace);
        pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            pace, 496, accelerationStep, decelerationStep);
        Assert.Equal(496, pace);

        pace = MovementRouteRules.AdvanceRetainedPaceRaw(
            496, 179, accelerationStep, decelerationStep);
        Assert.Equal(179, pace);
    }

    /// <summary>
    /// No shield desired pace exceeds the shared human baseline at any speed,
    /// because every pace multiplier on both rows is at most one whole. A
    /// shield never grants a speed bonus. Provisional reconstruction: gameplay
    /// tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(512)]
    [InlineData(10_000)]
    public void NoShieldDesiredPaceExceedsTheSharedHumanBaseline(
        int baselineRaw)
    {
        int[] caps =
        [
            KalisShieldRow.ForwardPaceBasisPoints,
            KalisShieldRow.LateralPaceBasisPoints,
            KalisShieldRow.BackwardPaceBasisPoints,
            KalisShieldRow.CommittedPaceBasisPoints,
            ItakShieldRow.ForwardPaceBasisPoints,
            ItakShieldRow.LateralPaceBasisPoints,
            ItakShieldRow.BackwardPaceBasisPoints,
            ItakShieldRow.CommittedPaceBasisPoints,
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

    /// <summary>
    /// A shield bearer cannot flip from the forward band to the backward band
    /// in one tick, because the band is selected by the separation between its
    /// facing and its travel direction and the facing itself advances by at
    /// most two sectors. Reaching a separation of six from a separation of zero
    /// therefore takes three ticks ordinarily and six while committed.
    /// </summary>
    /// <remarks>
    /// The retained-pace ramp is the weaker of the two guards, and at the
    /// 512-raw probe baseline it does not bind at all: the shielded Kalis
    /// forward-to-backward gap is <c>481 - 343 = 138</c> and its deceleration
    /// step is 307, so the pace itself completes that change in a single tick.
    /// The weapon plan's "no instant reverse" therefore holds through the
    /// facing budget rather than through the pace ramp, and this test asserts
    /// the guard that is real. The bound the ramp does supply — that a single
    /// tick moves the retained pace by at most one step and never past the
    /// target — is asserted alongside it.
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AShieldBearerCannotFlipFromTheForwardBandToTheBackwardBand(
        bool shieldedKalis)
    {
        var row = ShieldRow(shieldedKalis);
        var facing = Facing16.East;

        // Sector 6, SouthSouthWest, is the first backward-band separation.
        var travel = (Facing16)6;
        Assert.Equal(
            row.ForwardPaceBasisPoints,
            FacingRules.DirectionBandPaceCapBasisPoints(
                row, FacingRules.SectorSeparation(facing, Facing16.East)));
        Assert.Equal(
            row.BackwardPaceBasisPoints,
            FacingRules.DirectionBandPaceCapBasisPoints(
                row, FacingRules.SectorSeparation(facing, travel)));

        var ticks = 0;
        var current = facing;
        while (FacingRules.SectorSeparation(current, travel) > 1)
        {
            current = FacingRules.TurnToward(
                current, travel, row.MaximumFacingStepsPerTick, factionId: 0);
            ticks++;
            Assert.True(ticks <= 8, "The facing walk did not converge.");
        }

        // Six sectors at two per tick is three ticks, so no single tick can
        // carry a shield bearer from the forward band into the backward one.
        Assert.Equal(3, ticks);
        Assert.True(ticks > 1);

        // The ramp bound, at its real strength: one step, never overshooting.
        var accelerationStep = MovementRouteRules.PaceStepRaw(
            MovementSpeedRaw, row.AccelerationBasisPointsPerTick);
        var decelerationStep = MovementRouteRules.PaceStepRaw(
            MovementSpeedRaw, row.DecelerationBasisPointsPerTick);
        var forwardPace = MovementRouteRules.DesiredPaceRaw(
            MovementSpeedRaw, row.ForwardPaceBasisPoints);
        var backwardPace = MovementRouteRules.DesiredPaceRaw(
            MovementSpeedRaw, row.BackwardPaceBasisPoints);
        var stepped = MovementRouteRules.AdvanceRetainedPaceRaw(
            forwardPace, backwardPace, accelerationStep, decelerationStep);
        Assert.True(stepped >= forwardPace - decelerationStep);
        Assert.True(stepped >= backwardPace);
    }

    // ----- The commitment and recovery ladder (design 9.5, 9.6) -----

    /// <summary>
    /// Both shield rows carry a three-tick commitment and a three-tick
    /// recovery, and the ladder decrements them under the shared entry-tick
    /// convention: an entry timer counts its own entry tick, so a commitment
    /// of three spans the attack tick plus two more, and the recovery that
    /// follows spans three whole ticks before the ladder falls through again.
    /// This test proves the decrement and the recovery load;
    /// <c>CommitmentTicks == 3</c> itself is pinned as a row literal by
    /// <c>TallHardwoodMovementProfileTests</c>, because
    /// <see cref="WeaponMovementRules.ResolveProvisionalFootwork"/> has no
    /// commitment-ticks parameter and commit entry happens in
    /// <c>BattleSimulation</c>, gated on an accepted attack.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheShieldLadderCommitsThreeTicksAndThenRecoversThree(
        bool shieldedKalis)
    {
        var row = ShieldRow(shieldedKalis);
        Assert.Equal(3, row.CommitmentTicks);
        Assert.Equal(3, row.RecoveryTicks);

        // Tick one of the commitment is the attack tick itself, written by the
        // pipeline as (Commit, CommitmentTicks). The ladder then produces:
        var step = Resolve(
            row,
            priorPhase: FootworkPhase.Commit,
            priorTicksRemaining: row.CommitmentTicks,
            posture: TacticalPosture.Advance,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);
        Assert.Equal((FootworkPhase.Commit, 2), step);   // committed tick two

        step = Continue(row, step);
        Assert.Equal((FootworkPhase.Commit, 1), step);   // committed tick three

        step = Continue(row, step);
        Assert.Equal((FootworkPhase.Recover, 3), step);  // recovery tick one

        step = Continue(row, step);
        Assert.Equal((FootworkPhase.Recover, 2), step);  // recovery tick two

        step = Continue(row, step);
        Assert.Equal((FootworkPhase.Recover, 1), step);  // recovery tick three

        // The expiring recovery falls through to the ordinary ladder.
        step = Continue(row, step);
        Assert.Equal((FootworkPhase.Engage, 0), step);
    }

    /// <summary>
    /// The same ladder observed through whole ticks on a body-contact shielded
    /// Kalis duel, the row that had no whole-tick coverage anywhere before this
    /// file: the accepted attack enters <c>Commit</c> at three counting its
    /// entry tick, decrements twice, expires into a three-tick <c>Recover</c>,
    /// and resolves <c>Engage</c> once recovery expires. Cooldowns are pinned
    /// high between ticks so no second attack re-enters <c>Commit</c>
    /// mid-sequence. The shielded Itak counterpart is
    /// <c>ItakMovementTransitionTests</c>, method
    /// <c>TheShieldedItakLifecycleCommitsThreeTicksAndRecoversThree</c>.
    /// </summary>
    [Fact]
    public void AShieldedKalisDuelWalksThreeCommittedTicksIntoThreeRecoveries()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, 0, 92_160, 51_200, scenario, ShieldedKalis);
        var east = CreateAgent(2, 1, 93_184, 51_200, scenario, ShieldedKalis);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        simulation.AdvanceOneTick();
        Assert.Equal(FootworkPhase.Commit, west.FootworkPhase);
        Assert.Equal(3, west.FootworkTicksRemaining);
        Assert.Equal(FootworkPhase.Commit, east.FootworkPhase);
        Assert.Equal(3, east.FootworkTicksRemaining);

        var expected = new (FootworkPhase Phase, int Ticks)[]
        {
            (FootworkPhase.Commit, 2),
            (FootworkPhase.Commit, 1),
            (FootworkPhase.Recover, 3),
            (FootworkPhase.Recover, 2),
            (FootworkPhase.Recover, 1),
            (FootworkPhase.Engage, 0),
        };
        foreach (var (phase, ticks) in expected)
        {
            west.AttackCooldownRemaining = 100;
            east.AttackCooldownRemaining = 100;
            simulation.AdvanceOneTick();
            Assert.Equal(phase, west.FootworkPhase);
            Assert.Equal(ticks, west.FootworkTicksRemaining);
        }
    }

    // ----- Collision denial and the retained-pace clamp (design 6.5) -----

    /// <summary>
    /// A shield bearer whose every candidate lane is denied emits no movement
    /// and retains no pace, yet still turns toward its threat: facing is
    /// committed independently of the route, so a blocked warrior is not also
    /// a blind one. The provisional <see cref="FootworkPhase.Approach"/>
    /// finalises as <see cref="FootworkPhase.Refuse"/>, because a refused
    /// approach is not a safety phase to preserve.
    /// </summary>
    /// <remarks>
    /// The ally stands 200 raw from the actor, well inside the shielded Kalis
    /// clearance radius of 1,433, and the actor's first-tick pace of 286
    /// bounds every candidate endpoint to 286 raw from its start, so no
    /// endpoint can reach 1,433 from the ally and all three candidates fall.
    /// The threat stands due south, sector 4, and the ordinary two-sector
    /// budget carries the facing from East, sector 0, to SouthEast, sector 2.
    /// </remarks>
    [Fact]
    public void ARefusedShieldLaneZeroesRetainedPaceWhileFacingStillTurns()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, 0, 100_000, 51_200, scenario, ShieldedKalis);
        var crowdingAlly = CreateAgent(
            2, 0, 100_200, 51_200, scenario, ShieldedKalis);
        var southThreat = CreateAgent(
            3, 1, 100_000, 51_200 + 40_000, scenario, SoloItak);
        actor.MovementPaceRaw = 286;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, crowdingAlly, southThreat);

        Assert.Equal(Facing16.East, actor.Facing);
        simulation.AdvanceOneTick();

        Assert.Equal(100_000, actor.XRaw);
        Assert.Equal(51_200, actor.YRaw);
        Assert.Equal(0, actor.MovementPaceRaw);
        Assert.Equal(FootworkPhase.Refuse, actor.FootworkPhase);
        Assert.Equal(Facing16.SouthEast, actor.Facing);
        Assert.Equal(
            2, FacingRules.SectorSeparation(Facing16.East, actor.Facing));
    }

    /// <summary>
    /// Retained pace commits as the distance the collision stage actually
    /// granted, never as the pace the proposal was built on: on every tick of a
    /// shielded Kalis pressing against a body it cannot pass,
    /// <c>MovementPaceRaw</c> equals the truncated magnitude of that tick's
    /// displacement, a tick that moved nothing retains nothing, and at least
    /// one tick is granted strictly less than the retained-pace ramp asked for.
    /// That last assertion is what keeps the identity from being vacuous.
    /// </summary>
    /// <remarks>
    /// A merely closing duel never exercises this: two shield bearers settle
    /// apart at reach and every resolution reads
    /// <see cref="MovementResolution.Moved"/>, because the phase ladder stops
    /// them long before contact. This probe therefore starts the pair at exact
    /// tangency, 1,024 raw apart at a 512-raw body radius, and pins both
    /// cooldowns so neither enters <c>Commit</c> and both keep pressing in
    /// <see cref="FootworkPhase.Engage"/>. Unimpeded, the first tick would ramp
    /// from zero to <c>min(481, 0 + 286) = 286</c> raw along the forward band;
    /// the contact stage grants less, so the retained pace records the smaller
    /// figure.
    /// </remarks>
    [Fact]
    public void TheRetainedShieldPaceClampsToTheDistanceActuallyGranted()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, 0, 100_000, 51_200, scenario, ShieldedKalis);
        var east = CreateAgent(2, 1, 101_024, 51_200, scenario, ShieldedItak);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        var accelerationStep = MovementRouteRules.PaceStepRaw(
            MovementSpeedRaw, KalisShieldRow.AccelerationBasisPointsPerTick);
        var forwardPace = MovementRouteRules.DesiredPaceRaw(
            MovementSpeedRaw, KalisShieldRow.ForwardPaceBasisPoints);
        var sawReducedGrant = false;

        for (var tick = 0; tick < 40; tick++)
        {
            var priorPace = west.MovementPaceRaw;
            var priorX = west.XRaw;
            var priorY = west.YRaw;
            west.AttackCooldownRemaining = 100;
            east.AttackCooldownRemaining = 100;
            simulation.AdvanceOneTick();

            var deltaX = (long)west.XRaw - priorX;
            var deltaY = (long)west.YRaw - priorY;
            var movedRaw = (int)FixedPoint.IntegerSquareRoot(
                (deltaX * deltaX) + (deltaY * deltaY));

            Assert.Equal(FootworkPhase.Engage, west.FootworkPhase);
            Assert.Equal(movedRaw, west.MovementPaceRaw);
            if (deltaX == 0 && deltaY == 0)
            {
                Assert.Equal(0, west.MovementPaceRaw);
            }

            var unimpededPace = MovementRouteRules.AdvanceRetainedPaceRaw(
                priorPace,
                forwardPace,
                accelerationStep,
                MovementRouteRules.PaceStepRaw(
                    MovementSpeedRaw,
                    KalisShieldRow.DecelerationBasisPointsPerTick));
            if (west.MovementPaceRaw < unimpededPace)
            {
                sawReducedGrant = true;
            }
        }

        Assert.True(
            sawReducedGrant,
            "No tick granted the shielded Kalis less than its retained-pace " +
            "ramp asked for, so the clamp was never exercised.");
    }

    // ----- Mirroring and side parity (design 6.2, 10.3) -----

    /// <summary>
    /// A symmetric shielded Kalis duel stays an exact mirror while both live:
    /// positions reflect across the vertical centre line, facings reflect
    /// sector for sector in faction-canonical space, and pace and phase match
    /// tick after tick. Raw coordinates and raw facings are intentionally
    /// different between the two factions, which is why every assertion here is
    /// symmetry-normalised rather than a raw equality. Mirroring a shield
    /// bearer therefore changes nothing about its authoritative movement beyond
    /// the mirror itself.
    /// </summary>
    /// <remarks>
    /// Ties resolve in faction-canonical space, where faction one's X is
    /// negated, so a clockwise canonical tie reads counter-clockwise in world
    /// space. The facing assertion normalises through
    /// <c>(8 - sector + 16) % 16</c>, the same mirror
    /// <c>FacingRules.ToCanonicalSector</c> applies, rather than comparing
    /// world sectors. Damage is one against a million hit points so no clash
    /// asymmetry can remove either body from the geometry.
    /// </remarks>
    [Fact]
    public void AMirroredShieldedKalisDuelReflectsExactlyInCanonicalSpace()
    {
        var scenario = CreateScenario();
        var mapWidthRaw = scenario.MapWidth * FixedPoint.Scale;
        var west = CreateAgent(1, 0, 92_160, 51_200, scenario, ShieldedKalis);
        var east = CreateAgent(
            2, 1, mapWidthRaw - 92_160, 51_200, scenario, ShieldedKalis);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        for (var tick = 0; tick < 60; tick++)
        {
            simulation.AdvanceOneTick();

            Assert.True(west.IsAlive && east.IsAlive);
            Assert.Equal(mapWidthRaw, west.XRaw + east.XRaw);
            Assert.Equal(west.YRaw, east.YRaw);
            Assert.Equal(west.MovementPaceRaw, east.MovementPaceRaw);
            Assert.Equal(west.FootworkPhase, east.FootworkPhase);
            Assert.Equal(
                west.FootworkTicksRemaining, east.FootworkTicksRemaining);
            Assert.Equal((8 - (int)west.Facing + 16) % 16, (int)east.Facing);
        }
    }

    /// <summary>
    /// The first oblique side follows stable parity rather than geometry or
    /// equipment: <see cref="MovementRouteRules.SideAIsWorldClockwise"/> takes
    /// only a faction-local index and a faction identifier, so both shield rows
    /// make the same parity decision, and a mirrored pair makes the same
    /// decision in canonical space while reading as opposite in world space.
    /// </summary>
    /// <remarks>
    /// An even faction-local index is canonical-clockwise and an odd one
    /// canonical-counter-clockwise; mapping back to world space swaps the two
    /// rotations for faction one. Since the profile is not an argument, no
    /// shield row can bias which side a warrior probes first — which is what
    /// rules out a shield-pair rule masquerading as parity.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public void TheFirstObliqueSideFollowsStableParityUnderMirroring(
        int factionLocalIndex)
    {
        var factionZero = MovementRouteRules.SideAIsWorldClockwise(
            factionLocalIndex, factionId: 0);
        var factionOne = MovementRouteRules.SideAIsWorldClockwise(
            factionLocalIndex, factionId: 1);

        Assert.Equal((factionLocalIndex & 1) == 0, factionZero);
        Assert.Equal(!factionZero, factionOne);
    }

    // ----- Ally lane clearance (design 4.4, 10.5, 10.6) -----

    /// <summary>
    /// The larger of the two profiles' clearance radii controls every shield
    /// ally pairing, and the boundary is inclusive: an ally endpoint exactly at
    /// that radius does not deny the lane, and one raw unit inside does. Each
    /// expected radius is asserted as a literal so a silent change to the
    /// materialisation formula is caught, and derived through
    /// <see cref="MovementRouteRules.ClearanceRadiusRaw"/> from the row's own
    /// basis points. Provisional reconstruction: gameplay tuning; no historical
    /// measurement (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// At the 512-raw body radius the body diameter is 1,024, so each radius is
    /// <c>1024 * basisPoints / 10000</c>, truncating: shielded Kalis 14,000
    /// gives 1,433 (1433.6), shielded Itak 13,500 gives 1,382 (1382.4),
    /// Kampilan 15,000 gives 1,536 exactly, Wasay 17,500 gives 1,792 exactly,
    /// solo Kalis 12,000 gives 1,228 (1228.8), and solo Itak 11,500 gives
    /// 1,177 (1177.6). Every pair below therefore has a strictly larger member.
    /// </remarks>
    [Theory]
    [InlineData(true, "shieldedItak", 1_433, 1_382, 1_433)]
    [InlineData(true, "kampilan", 1_433, 1_536, 1_536)]
    [InlineData(true, "wasay", 1_433, 1_792, 1_792)]
    [InlineData(true, "soloKalis", 1_433, 1_228, 1_433)]
    [InlineData(true, "soloItak", 1_433, 1_177, 1_433)]
    [InlineData(false, "kampilan", 1_382, 1_536, 1_536)]
    [InlineData(false, "wasay", 1_382, 1_792, 1_792)]
    [InlineData(false, "soloKalis", 1_382, 1_228, 1_382)]
    [InlineData(false, "soloItak", 1_382, 1_177, 1_382)]
    public void TheLargerClearanceRadiusControlsEveryShieldAllyPairing(
        bool shieldedKalis,
        string allyRowName,
        int expectedShieldRadiusRaw,
        int expectedAllyRadiusRaw,
        int expectedControllingRadiusRaw)
    {
        var shieldRow = ShieldRow(shieldedKalis);
        var allyRow = NamedRow(allyRowName);

        var shieldRadiusRaw = MovementRouteRules.ClearanceRadiusRaw(
            BodyRadiusRaw, shieldRow.AllyClearanceBodyDiametersBasisPoints);
        var allyRadiusRaw = MovementRouteRules.ClearanceRadiusRaw(
            BodyRadiusRaw, allyRow.AllyClearanceBodyDiametersBasisPoints);

        Assert.Equal(expectedShieldRadiusRaw, shieldRadiusRaw);
        Assert.Equal(expectedAllyRadiusRaw, allyRadiusRaw);
        Assert.Equal(
            expectedControllingRadiusRaw,
            Math.Max(shieldRadiusRaw, allyRadiusRaw));

        (int SeparationRaw, bool ExpectedSecondAccepted)[] probes =
        [
            (expectedControllingRadiusRaw - 1, false),
            (expectedControllingRadiusRaw, true),
            (expectedControllingRadiusRaw + 1, true),
        ];

        Assert.All(
            probes,
            probe =>
            {
                var proposals = new[]
                {
                    new FriendlyClearanceProposal(
                        1, FootworkPhase.Engage, 0, 0, Square(shieldRadiusRaw)),
                    new FriendlyClearanceProposal(
                        2,
                        FootworkPhase.Engage,
                        probe.SeparationRaw,
                        0,
                        Square(allyRadiusRaw)),
                };
                var accepted = new bool[proposals.Length];

                MovementRouteRules.AcceptFriendlyClearanceConflicts(
                    proposals, accepted);

                Assert.True(accepted[0]);
                Assert.Equal(probe.ExpectedSecondAccepted, accepted[1]);

                // The independent oracle, written against the design text
                // rather than against production, must agree cell for cell.
                var oracle = NaiveConflictPassOracle.AcceptedEntityIds(
                    proposals);
                Assert.Equal(accepted[0], oracle.Contains(1UL));
                Assert.Equal(accepted[1], oracle.Contains(2UL));
            });
    }

    /// <summary>
    /// The controlling radius cannot depend on which of the two allies is the
    /// actor: the same pairing at the same separation resolves identically when
    /// the shield row holds the lower entity identifier and when it holds the
    /// higher one. Provisional reconstruction: gameplay tuning; no historical
    /// measurement (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// The shield row is deliberately paired with Wasay, whose 1,792 radius is
    /// the larger, and with solo Itak, whose 1,177 radius is the smaller, so
    /// the test covers both the case where the shield row controls and the case
    /// where it does not.
    /// </remarks>
    [Theory]
    [InlineData(true, "wasay", 1_792)]
    [InlineData(true, "soloItak", 1_433)]
    [InlineData(false, "wasay", 1_792)]
    [InlineData(false, "soloItak", 1_382)]
    public void TheControllingClearanceRadiusIgnoresWhichAllyIsTheActor(
        bool shieldedKalis, string allyRowName, int controllingRadiusRaw)
    {
        var shieldSquared = Square(
            MovementRouteRules.ClearanceRadiusRaw(
                BodyRadiusRaw,
                ShieldRow(shieldedKalis)
                    .AllyClearanceBodyDiametersBasisPoints));
        var allySquared = Square(
            MovementRouteRules.ClearanceRadiusRaw(
                BodyRadiusRaw,
                NamedRow(allyRowName).AllyClearanceBodyDiametersBasisPoints));

        foreach (var separationRaw in
            new[] { controllingRadiusRaw - 1, controllingRadiusRaw })
        {
            var shieldFirst = new[]
            {
                new FriendlyClearanceProposal(
                    1, FootworkPhase.Engage, 0, 0, shieldSquared),
                new FriendlyClearanceProposal(
                    2, FootworkPhase.Engage, separationRaw, 0, allySquared),
            };
            var allyFirst = new[]
            {
                new FriendlyClearanceProposal(
                    1, FootworkPhase.Engage, 0, 0, allySquared),
                new FriendlyClearanceProposal(
                    2, FootworkPhase.Engage, separationRaw, 0, shieldSquared),
            };
            var shieldFirstAccepted = new bool[2];
            var allyFirstAccepted = new bool[2];

            MovementRouteRules.AcceptFriendlyClearanceConflicts(
                shieldFirst, shieldFirstAccepted);
            MovementRouteRules.AcceptFriendlyClearanceConflicts(
                allyFirst, allyFirstAccepted);

            Assert.Equal(shieldFirstAccepted, allyFirstAccepted);
            Assert.Equal(
                separationRaw >= controllingRadiusRaw, shieldFirstAccepted[1]);
        }
    }

    /// <summary>
    /// Two shield bearers take distinct viable lanes when geometry permits and
    /// refuse rather than overlap when it does not. There is no shield-pair
    /// state, no synchronised turn-taking, and no wall: in the permitting
    /// geometry both move on the same tick with different displacements and
    /// stay at least the controlling radius apart, and in the denying geometry
    /// both stand, because the lane scan reads tick-start positions and neither
    /// warrior is privileged over the other.
    /// </summary>
    [Fact]
    public void TwoShieldBearersTakeDistinctLanesOrRefuseRatherThanOverlap()
    {
        var scenario = CreateScenario();
        var controllingRadiusRaw = Math.Max(
            MovementRouteRules.ClearanceRadiusRaw(
                BodyRadiusRaw,
                KalisShieldRow.AllyClearanceBodyDiametersBasisPoints),
            MovementRouteRules.ClearanceRadiusRaw(
                BodyRadiusRaw,
                ItakShieldRow.AllyClearanceBodyDiametersBasisPoints));
        Assert.Equal(1_433L, controllingRadiusRaw);

        // Permitting geometry: 3,000 raw apart, comfortably past 1,433.
        var roomyKalis = CreateAgent(
            1, 0, 96_000, 49_000, scenario, ShieldedKalis);
        var roomyItak = CreateAgent(
            2, 0, 96_000, 52_000, scenario, ShieldedItak);
        var roomyEnemy = CreateAgent(
            3, 1, 130_000, 51_200, scenario, SoloItak);
        var roomy = BattleSimulation.CreateForTesting(
            scenario, roomyKalis, roomyItak, roomyEnemy);

        roomy.AdvanceOneTick();

        var kalisDelta = (
            (long)roomyKalis.XRaw - 96_000, (long)roomyKalis.YRaw - 49_000);
        var itakDelta = (
            (long)roomyItak.XRaw - 96_000, (long)roomyItak.YRaw - 52_000);
        Assert.NotEqual((0L, 0L), kalisDelta);
        Assert.NotEqual((0L, 0L), itakDelta);
        Assert.NotEqual(kalisDelta, itakDelta);
        Assert.True(
            (Int128)CollisionGeometry.SquaredDistance(
                roomyKalis.XRaw,
                roomyKalis.YRaw,
                roomyItak.XRaw,
                roomyItak.YRaw) >= Square(controllingRadiusRaw));

        // Denying geometry: 400 raw apart, far inside 1,433, so no candidate
        // endpoint of either warrior can clear the other's tick-start body.
        var crowdedKalis = CreateAgent(
            1, 0, 96_000, 51_000, scenario, ShieldedKalis);
        var crowdedItak = CreateAgent(
            2, 0, 96_000, 51_400, scenario, ShieldedItak);
        var crowdedEnemy = CreateAgent(
            3, 1, 130_000, 51_200, scenario, SoloItak);
        var crowded = BattleSimulation.CreateForTesting(
            scenario, crowdedKalis, crowdedItak, crowdedEnemy);

        crowded.AdvanceOneTick();

        Assert.Equal(96_000, crowdedKalis.XRaw);
        Assert.Equal(51_000, crowdedKalis.YRaw);
        Assert.Equal(0, crowdedKalis.MovementPaceRaw);
        Assert.Equal(96_000, crowdedItak.XRaw);
        Assert.Equal(51_400, crowdedItak.YRaw);
        Assert.Equal(0, crowdedItak.MovementPaceRaw);
        Assert.Equal(FootworkPhase.Refuse, crowdedKalis.FootworkPhase);
        Assert.Equal(FootworkPhase.Refuse, crowdedItak.FootworkPhase);
    }

    // ----- Route tie stability (design 10.6) -----

    /// <summary>
    /// Storage order cannot decide a shield movement tie. Permuting the agent
    /// array handed to <c>BattleSimulation.CreateForTesting</c> while leaving
    /// every entity identifier unchanged produces identical authoritative state
    /// for every warrior, tick after tick, because the pipeline canonicalises
    /// on the stable identifier rather than on the caller's ordering.
    /// </summary>
    [Fact]
    public void PermutingTheCallerOrderChangesNoShieldMovementDecision()
    {
        var scenario = CreateScenario();

        AgentState[] Build() =>
        [
            CreateAgent(1, 0, 96_000, 49_500, scenario, ShieldedKalis),
            CreateAgent(2, 0, 96_000, 52_900, scenario, ShieldedItak),
            CreateAgent(3, 1, 112_000, 49_500, scenario, ShieldedItak),
            CreateAgent(4, 1, 112_000, 52_900, scenario, ShieldedKalis),
        ];

        int[][] permutations =
        [
            [0, 1, 2, 3],
            [3, 2, 1, 0],
            [2, 0, 3, 1],
            [1, 3, 0, 2],
        ];

        List<string>? expected = null;
        foreach (var permutation in permutations)
        {
            var agents = Build();
            var ordered = permutation.Select(slot => agents[slot]).ToArray();
            var simulation = BattleSimulation.CreateForTesting(
                scenario, ordered);

            for (var tick = 0; tick < 60; tick++)
            {
                simulation.AdvanceOneTick();
            }

            var observed = agents
                .OrderBy(agent => agent.EntityId)
                .Select(agent =>
                    $"{agent.EntityId}:{agent.XRaw}:{agent.YRaw}:" +
                    $"{agent.Facing}:{agent.MovementPaceRaw}:" +
                    $"{agent.FootworkPhase}:{agent.FootworkTicksRemaining}:" +
                    $"{agent.TacticalPosture}:{agent.HitPoints}")
                .ToList();

            if (expected is null)
            {
                expected = observed;
                continue;
            }

            Assert.Equal(expected, observed);
        }
    }

    /// <summary>
    /// Caller order is canonicalised on the entity identifier, so the same
    /// shield battle supplied in reverse produces an identical state hash, an
    /// identical ordered event stream, and an identical outcome. This is
    /// distinct from the mirroring test, which reflects the geometry, and from
    /// the permutation test, which compares authoritative fields rather than
    /// hashes.
    /// </summary>
    [Fact]
    public void ReversedCallerInputProducesTheSameShieldStateAndEventStream()
    {
        var scenario = CreateScenario();

        var forward = RunToCompletion(
            scenario,
            [
                CreateAgent(1, 0, 96_000, 51_200, scenario, ShieldedKalis),
                CreateAgent(2, 1, 108_000, 51_200, scenario, ShieldedItak),
            ],
            ticks: 300);
        var reversed = RunToCompletion(
            scenario,
            [
                CreateAgent(2, 1, 108_000, 51_200, scenario, ShieldedItak),
                CreateAgent(1, 0, 96_000, 51_200, scenario, ShieldedKalis),
            ],
            ticks: 300);

        Assert.Equal(forward.StateHash, reversed.StateHash);
        Assert.Equal(forward.EventStream, reversed.EventStream);
        Assert.Equal(forward.Outcome, reversed.Outcome);
        Assert.True(forward.LegalSteps, forward.StepFailure ?? "step");
        Assert.True(forward.LegalPhases, forward.PhaseFailure ?? "phase");
    }

    /// <summary>
    /// The conflict pass enforces its own total order rather than sorting
    /// defensively: proposals that do not arrive in strictly ascending entity
    /// order throw, which is the enforced form of the invariant the phase-then
    /// identifier ordering depends on. Equal identifiers throw for the same
    /// reason.
    /// </summary>
    [Fact]
    public void TheConflictPassRejectsProposalsOutOfAscendingEntityOrder()
    {
        var shieldSquared = Square(
            MovementRouteRules.ClearanceRadiusRaw(
                BodyRadiusRaw,
                KalisShieldRow.AllyClearanceBodyDiametersBasisPoints));

        FriendlyClearanceProposal Proposal(ulong entityId, int xRaw) =>
            new(entityId, FootworkPhase.Engage, xRaw, 0, shieldSquared);

        var descending = new[] { Proposal(2, 0), Proposal(1, 4_000) };
        var duplicated = new[] { Proposal(2, 0), Proposal(2, 4_000) };
        var accepted = new bool[2];

        Assert.Throws<ArgumentException>(
            () => MovementRouteRules.AcceptFriendlyClearanceConflicts(
                descending, accepted));
        Assert.Throws<ArgumentException>(
            () => MovementRouteRules.AcceptFriendlyClearanceConflicts(
                duplicated, accepted));

        // The ascending form of the same set is accepted and matches the
        // independent oracle, so the throw is an ordering guard rather than a
        // rejection of the geometry.
        var ascending = new[] { Proposal(1, 4_000), Proposal(2, 0) };
        MovementRouteRules.AcceptFriendlyClearanceConflicts(
            ascending, accepted);
        var oracle = NaiveConflictPassOracle.AcceptedEntityIds(ascending);
        Assert.Equal(accepted[0], oracle.Contains(1UL));
        Assert.Equal(accepted[1], oracle.Contains(2UL));
    }

    /// <summary>
    /// The conflict pass orders on phase safety first and only then on the
    /// stable identifier, and it does so with the shield rows' own radii: a
    /// disengaging shield bearer with the higher identifier is served before an
    /// engaging one with the lower, and the independent oracle agrees. Nothing
    /// about a shield changes the ordering; the phase does.
    /// </summary>
    [Fact]
    public void PhaseSafetyOutranksTheEntityIdentifierForShieldProposals()
    {
        var kalisSquared = Square(
            MovementRouteRules.ClearanceRadiusRaw(
                BodyRadiusRaw,
                KalisShieldRow.AllyClearanceBodyDiametersBasisPoints));
        var itakSquared = Square(
            MovementRouteRules.ClearanceRadiusRaw(
                BodyRadiusRaw,
                ItakShieldRow.AllyClearanceBodyDiametersBasisPoints));

        // Both want the same crowded pocket, 400 raw apart and far inside the
        // 1,433 controlling radius, so exactly one of them can be accepted.
        var proposals = new[]
        {
            new FriendlyClearanceProposal(
                1, FootworkPhase.Engage, 0, 0, kalisSquared),
            new FriendlyClearanceProposal(
                2, FootworkPhase.Disengage, 400, 0, itakSquared),
        };
        var accepted = new bool[proposals.Length];

        MovementRouteRules.AcceptFriendlyClearanceConflicts(
            proposals, accepted);

        Assert.False(accepted[0]);
        Assert.True(accepted[1]);
        Assert.Equal(
            0, MovementRouteRules.ConflictPhaseSafetyRank(
                FootworkPhase.Disengage));
        Assert.Equal(
            4, MovementRouteRules.ConflictPhaseSafetyRank(
                FootworkPhase.Engage));

        var oracle = NaiveConflictPassOracle.AcceptedEntityIds(proposals);
        Assert.DoesNotContain(1UL, oracle);
        Assert.Contains(2UL, oracle);
    }

    // ----- The second-threat lane omission (design 10.4) -----

    /// <summary>
    /// With two or more immediate enemies the direct lane is omitted only when
    /// its endpoint sits strictly closer to the second threat than the actor's
    /// tick-start position does; exact equality keeps the direct lane. The
    /// weapon plan omits this rule entirely, so it is covered here.
    /// Provisional reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// The shielded Kalis actor stands at X 100,000 with its target 2,000 raw
    /// due east. Its first-tick pace is <c>min(481, 0 + 286) = 286</c> and the
    /// direct step scales the delta exactly, <c>2000 * 286 / 2000 = 286</c>, so
    /// the direct endpoint is X 100,286. Placing the second threat at the
    /// perpendicular bisector of that step, X 100,143 and Y 2,000 south, makes
    /// the two squared distances equal at <c>143^2 + 2000^2 = 4020449</c>, and
    /// equality keeps the direct lane. Moving the second threat one raw unit
    /// east, to X 100,144, gives a start of <c>144^2 + 2000^2 = 4020736</c>
    /// against an endpoint of <c>142^2 + 2000^2 = 4020164</c>, strictly closer,
    /// and the direct lane is dropped for the clockwise oblique. One ally
    /// stands 3,000 raw west, close enough to keep the support ratio below the
    /// shielded Kalis entry threshold of 17,500 basis points — two enemies
    /// against two allies is 20,000 against 35,000 — and far enough that no
    /// candidate endpoint, none of which travels more than 286 raw, can breach
    /// its 1,433 clearance radius.
    /// </remarks>
    [Theory]
    [InlineData(143, 286, 0)]    // exact equality keeps the direct lane
    [InlineData(144, 264, 109)]  // strictly closer drops it for the oblique
    public void TheSecondThreatOmissionKeepsTheDirectLaneAtExactEquality(
        int secondThreatOffsetXRaw,
        int expectedDeltaXRaw,
        int expectedAbsoluteDeltaYRaw)
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, 0, 100_000, 51_200, scenario, ShieldedKalis);
        var supportAlly = CreateAgent(
            2, 0, 100_000 - 3_000, 51_200, scenario, ShieldedKalis);
        var target = CreateAgent(
            3, 1, 100_000 + 2_000, 51_200, scenario, SoloItak);
        var secondThreat = CreateAgent(
            4,
            1,
            100_000 + secondThreatOffsetXRaw,
            51_200 + 2_000,
            scenario,
            SoloItak);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, supportAlly, target, secondThreat);

        // The target sits inside the 5,120 reach, so an accepted attack would
        // overwrite the end-of-tick phase with Commit after movement had
        // already resolved under Engage. Pinning the cooldown keeps the
        // observable phase the one that chose the lane.
        actor.AttackCooldownRemaining = 100;
        simulation.AdvanceOneTick();

        Assert.Equal(3UL, actor.TargetEntityId);
        Assert.Equal(FootworkPhase.Engage, actor.FootworkPhase);
        Assert.Equal(expectedDeltaXRaw, actor.XRaw - 100_000);
        Assert.Equal(
            expectedAbsoluteDeltaYRaw, Math.Abs(actor.YRaw - 51_200));
    }

    /// <summary>
    /// A committed shield bearer's lone direct candidate is exempt from the
    /// second-threat omission: the same strictly-closer geometry that drops the
    /// direct lane out of <see cref="FootworkPhase.Engage"/> leaves it standing
    /// during <see cref="FootworkPhase.Commit"/>, because a commitment that
    /// omitted its only candidate would emit no movement at all.
    /// </summary>
    /// <remarks>
    /// The committed pace is <c>min(481, 153) = 153</c> and the first-tick ramp
    /// reaches it in one step, so the direct step is
    /// <c>2000 * 153 / 2000 = 153</c> raw due east and the lateral offset stays
    /// zero.
    /// </remarks>
    [Fact]
    public void TheCommitLoneDirectCandidateIsExemptFromTheSecondThreatRule()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, 0, 100_000, 51_200, scenario, ShieldedKalis);
        var supportAlly = CreateAgent(
            2, 0, 100_000 - 3_000, 51_200, scenario, ShieldedKalis);
        var target = CreateAgent(
            3, 1, 100_000 + 2_000, 51_200, scenario, SoloItak);
        var secondThreat = CreateAgent(
            4, 1, 100_000 + 144, 51_200 + 2_000, scenario, SoloItak);
        actor.FootworkPhase = FootworkPhase.Commit;
        actor.FootworkTicksRemaining = 5;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, supportAlly, target, secondThreat);

        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Commit, actor.FootworkPhase);
        Assert.Equal(153, actor.XRaw - 100_000);
        Assert.Equal(51_200, actor.YRaw);
        Assert.Equal(153, actor.MovementPaceRaw);
    }

    // ----- Preferred distance is not a stop line (design 9.1 step 8) -----

    /// <summary>
    /// The preferred distance is a phase boundary, not a stop line: a shield
    /// bearer sitting at exactly its effective preferred distance enters
    /// <see cref="FootworkPhase.Engage"/> and keeps closing toward the target's
    /// centre, leaving the post-movement reach gate authoritative. Provisional
    /// reconstruction: gameplay tuning; no historical measurement
    /// (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// The shielded Kalis band against a solo Itak is
    /// <c>5120 * 13500 / 10000 = 6912</c>, and the first-tick pace is
    /// <c>min(481, 0 + 286) = 286</c>, so the direct step is
    /// <c>6912 * 286 / 6912 = 286</c> raw due east rather than zero.
    /// </remarks>
    [Fact]
    public void AShieldBearerAtExactPreferredDistanceKeepsClosing()
    {
        var scenario = CreateScenario();
        var preferredRaw = MovementRouteRules.EffectivePreferredDistanceRaw(
            AttackRangeRaw, KalisShieldRow, opponentCanonicalIndex: 3);
        Assert.Equal(6_912L, preferredRaw);

        var actor = CreateAgent(1, 0, 100_000, 51_200, scenario, ShieldedKalis);
        var target = CreateAgent(
            2, 1, 100_000 + (int)preferredRaw, 51_200, scenario, SoloItak);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, target);

        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Engage, actor.FootworkPhase);
        Assert.Equal(286, actor.XRaw - 100_000);
        Assert.Equal(51_200, actor.YRaw);
    }

    /// <summary>
    /// The selected opponent's loadout changes only spacing. The same geometry
    /// against a solo Kalis and against a shielded Kalis selects the same
    /// target and leaves the actor's own reach untouched, and produces the same
    /// displacement, while the resolved phase differs because the offset cell
    /// differs. Provisional reconstruction: gameplay tuning; no historical
    /// measurement (docs/research/movement/tall-hardwood-shield.md).
    /// </summary>
    /// <remarks>
    /// The shielded Kalis band is 6,784 against a solo Kalis, cell +250, and
    /// 6,656 against a shielded Kalis, cell 0. A separation of 6,700 sits
    /// inside the first and outside the second, so the phase reads
    /// <see cref="FootworkPhase.Engage"/> against the solo opponent and
    /// <see cref="FootworkPhase.Approach"/> against the shielded one. Both
    /// phases build the same direct candidate toward the target's centre, which
    /// is why the step is identical, the clearest demonstration that the band
    /// is a label on the same movement rather than a brake.
    /// </remarks>
    [Fact]
    public void AShieldedOpponentChangesOnlySpacingAndNotTargetOrReach()
    {
        var scenario = CreateScenario();
        Assert.Equal(
            6_784L,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, KalisShieldRow, opponentCanonicalIndex: 2));
        Assert.Equal(
            6_656L,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, KalisShieldRow, opponentCanonicalIndex: 4));

        (AgentState Actor, AgentState Opponent) Run(CombatLoadout opponentKey)
        {
            var actor = CreateAgent(
                1, 0, 100_000, 51_200, scenario, ShieldedKalis);
            var opponent = CreateAgent(
                2, 1, 100_000 + 6_700, 51_200, scenario, opponentKey);
            BattleSimulation
                .CreateForTesting(scenario, actor, opponent)
                .AdvanceOneTick();
            return (actor, opponent);
        }

        var (againstSolo, _) = Run(SoloKalis);
        var (againstShielded, _) = Run(ShieldedKalis);

        Assert.Equal(2UL, againstSolo.TargetEntityId);
        Assert.Equal(2UL, againstShielded.TargetEntityId);
        Assert.Equal(
            againstSolo.AttackRangeRaw, againstShielded.AttackRangeRaw);
        Assert.Equal(AttackRangeRaw, againstSolo.AttackRangeRaw);
        Assert.Equal(againstSolo.XRaw, againstShielded.XRaw);
        Assert.Equal(againstSolo.YRaw, againstShielded.YRaw);

        Assert.Equal(FootworkPhase.Engage, againstSolo.FootworkPhase);
        Assert.Equal(FootworkPhase.Approach, againstShielded.FootworkPhase);
    }

    // ----- Global loadout composition (design 7.5, 8.1) -----

    /// <summary>
    /// Global loadout composition reaches movement through exactly one door,
    /// the contested-posture role-coverage tie-break, and through no other. It
    /// never changes a headcount, never changes a pace, and never changes an
    /// effective preferred distance —
    /// <see cref="MovementRouteRules.EffectivePreferredDistanceRaw"/> takes no
    /// composition argument at all. The loadout-agnostic tuning of the same
    /// branch table is covered by <c>TacticalPostureRulesTests</c>; this is the
    /// shield-row slice, and the branch itself takes no profile, so there is no
    /// shield-specific posture to assert beyond it.
    /// </summary>
    /// <remarks>
    /// Two shield bearers occupy only the shield-support role, so their
    /// coverage is one. Adding a Kampilan to the opposing side raises that side
    /// to two, which at equal headcounts sends the shield pair to
    /// <see cref="TacticalPosture.Yield"/> by branch eight and the mixed side
    /// to <see cref="TacticalPosture.Advance"/> by branch seven. Equal coverage
    /// falls through both to <see cref="TacticalPosture.Hold"/>.
    /// </remarks>
    [Fact]
    public void GlobalCompositionChangesOnlyThePostureTieBreakForShieldRows()
    {
        var shieldPair = new LoadoutCompositionCounts(
            Kampilan: 0,
            Wasay: 0,
            Kalis: 0,
            Itak: 0,
            KalisShield: 1,
            ItakShield: 1);
        var mixedPair = new LoadoutCompositionCounts(
            Kampilan: 1,
            Wasay: 0,
            Kalis: 0,
            Itak: 0,
            KalisShield: 1,
            ItakShield: 0);

        Assert.Equal(1, shieldPair.RoleCoverage);
        Assert.Equal(2, mixedPair.RoleCoverage);
        Assert.Equal(2, shieldPair.Total);
        Assert.Equal(2, mixedPair.Total);

        TacticalPosture Posture(int alliedCoverage, int enemyCoverage) =>
            WeaponMovementRules.ResolveTacticalPosture(
                globalAllies: 2,
                globalEnemies: 2,
                ContingentState.Advance,
                alliedCoverage,
                enemyCoverage);

        Assert.Equal(
            TacticalPosture.Hold,
            Posture(shieldPair.RoleCoverage, shieldPair.RoleCoverage));
        Assert.Equal(
            TacticalPosture.Yield,
            Posture(shieldPair.RoleCoverage, mixedPair.RoleCoverage));
        Assert.Equal(
            TacticalPosture.Advance,
            Posture(mixedPair.RoleCoverage, shieldPair.RoleCoverage));

        // Neither pace nor spacing moved with the composition, because neither
        // reads it: both quantities are functions of the row alone.
        Assert.Equal(9_400, KalisShieldRow.ForwardPaceBasisPoints);
        Assert.Equal(9_700, ItakShieldRow.ForwardPaceBasisPoints);
        Assert.Equal(
            6_656L,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, KalisShieldRow, opponentCanonicalIndex: 4));
        Assert.Equal(
            5_120L,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw, ItakShieldRow, opponentCanonicalIndex: 5));
    }

    /// <summary>
    /// The same statement observed through whole ticks: two battles differing
    /// only in the opposing faction's composition give the shield pair
    /// different postures while every warrior resolves the very same profile
    /// instance and the headcount is unchanged.
    /// </summary>
    [Fact]
    public void AWholeTickProbeShowsCompositionMovingPostureAndNothingElse()
    {
        var scenario = CreateScenario();

        (AgentState Kalis, AgentState Itak) Run(CombatLoadout secondEnemyKey)
        {
            var shieldKalis = CreateAgent(
                1, 0, 96_000, 49_500, scenario, ShieldedKalis);
            var shieldItak = CreateAgent(
                2, 0, 96_000, 52_900, scenario, ShieldedItak);
            var firstEnemy = CreateAgent(
                3, 1, 112_000, 49_500, scenario, ShieldedKalis);
            var secondEnemy = CreateAgent(
                4, 1, 112_000, 52_900, scenario, secondEnemyKey);
            var simulation = BattleSimulation.CreateForTesting(
                scenario, shieldKalis, shieldItak, firstEnemy, secondEnemy);
            for (var tick = 0; tick < 4; tick++)
            {
                simulation.AdvanceOneTick();
            }

            return (shieldKalis, shieldItak);
        }

        var (againstShields, _) = Run(ShieldedItak);
        var (againstMixed, _) = Run(SoloKampilan);

        Assert.Same(
            KalisShieldRow, V6.ResolveLoadoutProfile(againstShields.Loadout));
        Assert.Same(
            KalisShieldRow, V6.ResolveLoadoutProfile(againstMixed.Loadout));
        Assert.Equal(againstShields.Loadout, againstMixed.Loadout);
        Assert.NotEqual(
            againstShields.TacticalPosture, againstMixed.TacticalPosture);
    }

    // ----- Helpers -----

    private static readonly CombatLoadout ShieldedKalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static readonly CombatLoadout ShieldedItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static readonly CombatLoadout SoloItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout SoloKalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout SoloKampilan =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    /// <summary>
    /// Selects one of the four solo comparison rows from a theory literal,
    /// because <see cref="LoadoutMovementProfile"/> is not an
    /// <see cref="InlineDataAttribute"/> literal either.
    /// </summary>
    private static LoadoutMovementProfile NamedRow(string name) => name switch
    {
        "shieldedKalis" => KalisShieldRow,
        "shieldedItak" => ItakShieldRow,
        "kampilan" => KampilanMovementProfile.Row,
        "wasay" => WasayMovementProfile.Row,
        "soloKalis" => KalisMovementProfile.Row,
        "soloItak" => ItakMovementProfile.Row,
        _ => throw new ArgumentOutOfRangeException(
            nameof(name), name, "No canonical movement row carries this name."),
    };

    private static CombatRuleset CombatRules =>
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV2);

    /// <summary>
    /// Every scenario names its combat preset and its movement preset
    /// explicitly. <c>PrecolonialPhilippinesV2</c> is the only preset that
    /// fields both shielded loadouts, so it is the only one that can span the
    /// shield slice at all.
    /// </summary>
    private static Scenario CreateScenario(ulong seed = 1) =>
        new(
            Seed: seed,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 5_000)
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
    /// Builds one agent carrying the scenario's uniform reach rather than its
    /// weapon's shipped reach, the same choice
    /// <c>ItakMovementTransitionTests</c> makes, so that every derived
    /// endpoint and every effective preferred distance in this file follows
    /// from the single probe constant 5,120 and can be checked by hand.
    /// </summary>
    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatLoadout loadout,
        int? attackCooldownTicksOverride = null) =>
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
            attackCooldownTicksOverride ?? scenario.AttackCooldownTicks,
            loadout);

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

    private static Int128 Square(long value) => (Int128)value * value;

    /// <summary>
    /// Advances a battle and records the non-authoritative evidence the plan
    /// asks for, without altering a single authoritative field. The step
    /// legality bound carries the documented one-raw-unit truncation headroom
    /// both merged weapon sessions record: the shipped step model scales the
    /// target delta by the pace divided by a truncated integer square root, so
    /// the per-axis cap is exact while the Euclidean magnitude may exceed the
    /// cap by less than one raw unit.
    /// </summary>
    private static RunEvidence RunToCompletion(
        Scenario scenario, AgentState[] agents, int ticks)
    {
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);
        var toleratedStepSquared =
            (Int128)(scenario.MovementSpeedRaw + 1) *
            (scenario.MovementSpeedRaw + 1);
        var previous = agents.ToDictionary(
            agent => agent.EntityId, agent => (agent.XRaw, agent.YRaw));
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

    /// <summary>
    /// The profile-parameterised footwork ladder wrapper. The profile is the
    /// first parameter and both the prior timer and the posture are
    /// parameters, because this file drives four rows — two shielded and two
    /// solo comparison rows — through the same ladder.
    /// </summary>
    private static (FootworkPhase Phase, int TicksRemaining) Resolve(
        LoadoutMovementProfile profile,
        FootworkPhase priorPhase = FootworkPhase.None,
        int priorTicksRemaining = 0,
        TacticalPosture posture = TacticalPosture.Hold,
        int supportAllies = 1,
        int supportEnemies = 0,
        bool hasTarget = false,
        bool targetAtOrInsidePreferredDistance = false) =>
        WeaponMovementRules.ResolveProvisionalFootwork(
            isAlive: true,
            priorPhase,
            priorTicksRemaining,
            posture,
            supportAllies,
            supportEnemies,
            profile.DisengageEnemyToAllyBasisPoints,
            profile.ReengageEnemyToAllyBasisPoints,
            profile.RecoveryTicks,
            hasTarget,
            targetAtOrInsidePreferredDistance);

    /// <summary>
    /// Feeds one ladder result back in as the next tick's prior state, under a
    /// steady advancing posture with a target inside the band, so that a
    /// lifecycle walk reads as a sequence rather than as repeated setup.
    /// </summary>
    private static (FootworkPhase Phase, int TicksRemaining) Continue(
        LoadoutMovementProfile profile,
        (FootworkPhase Phase, int TicksRemaining) prior) =>
        Resolve(
            profile,
            priorPhase: prior.Phase,
            priorTicksRemaining: prior.TicksRemaining,
            posture: TacticalPosture.Advance,
            hasTarget: true,
            targetAtOrInsidePreferredDistance: true);
}
