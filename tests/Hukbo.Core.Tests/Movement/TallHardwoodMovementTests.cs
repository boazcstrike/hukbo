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
    /// <c>ItakMovementTransitionTests.ZeroEnemiesNeverEntersAndNeverRemainsUnderEitherItakRow</c>.
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
    /// <c>ItakMovementProfileTests.TheShieldedItakEffectivePreferredDistanceCoversEveryOpponentColumn</c>;
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

    // ----- Helpers -----

    private static readonly CombatLoadout ShieldedKalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static readonly CombatLoadout ShieldedItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood);

    private static readonly CombatLoadout SoloItak =
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None);

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
}
