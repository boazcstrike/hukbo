using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The Tall Hardwood Shield session's own pin for the two shielded
/// equipment-relative movement rows: shielded Kalis
/// (<see cref="TallHardwoodMovementProfiles.KalisRow"/>, canonical loadout
/// index 4) and shielded Itak
/// (<see cref="TallHardwoodMovementProfiles.ItakRow"/>, canonical loadout
/// index 5). Every value asserted in this file is a provisional
/// reconstruction: gameplay tuning; no historical measurement. The evidence
/// ledger and the approved calibration ranges live in
/// docs/research/movement/tall-hardwood-shield.md; the rows themselves are
/// fixed by docs/archives/2026-08-07/2026-07-30-weapon-movement-foundation-design.md
/// section 13, which supersedes any figure quoted in the weapon plans.
/// </summary>
/// <remarks>
/// <para>
/// The registry-side equivalents of the two value assertions already exist as
/// <c>MovementProfileRegistrationTests</c>
/// <c>.TheShieldedKalisRowCarriesTheDesignSectionThirteenValues</c> and
/// <c>.TheShieldedItakRowCarriesTheDesignSectionThirteenValues</c>, which read
/// each row through <c>LoadoutMovementProfiles[4]</c> and
/// <c>LoadoutMovementProfiles[5]</c>. This file is the equipment-owned pin, and
/// it asserts the exported rows directly so that a change to either export is
/// caught in the file the Tall Hardwood session owns. The overlap is
/// deliberate, not accidental duplication.
/// </para>
/// <para>
/// The throw-side counterpart for the constructor's validation envelope is
/// <c>LoadoutMovementProfileTests</c>. Because these two rows are
/// already-constructed statics, this file cannot watch them being validated;
/// it asserts their values against the same ranges instead.
/// </para>
/// <para>
/// The unsupported-key throw for shielded Kampilan and shielded Wasay is also
/// covered from the registry side by
/// <c>MovementProfileRegistrationTests.ResolveLoadoutProfileThrowsForAnUnsupportedLoadout</c>,
/// and from the Kampilan side by
/// <c>KampilanMovementProfileTests.NoKampilanTallHardwoodRowResolves</c>. It is
/// repeated here because the shield is what makes those keys interesting, and
/// the shield rows are what a future reader would suspect of absorbing them.
/// </para>
/// </remarks>
public sealed class TallHardwoodMovementProfileTests
{
    /// <summary>
    /// The canonical scenario attack range of five world units, matching the
    /// movement pipeline fixtures and the range
    /// <c>ItakMovementProfileTests</c> uses.
    /// </summary>
    private const int AttackRangeRaw = 5 * FixedPoint.Scale;

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(
            MovementPresetId.EquipmentRelativeFootworkV6);

    private static CombatLoadout ShieldedKalis(RankId rank = RankId.Timawa) =>
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood, rank);

    private static CombatLoadout ShieldedItak(RankId rank = RankId.Timawa) =>
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood, rank);

    private static CombatLoadout SoloKalis() =>
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None);

    private static CombatLoadout SoloItak() =>
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None);

    // ----- Literal row values (design section 13) -----

    /// <summary>
    /// Every scalar and every signed opponent-offset cell of the shielded
    /// Kalis row. The offsets run in canonical opponent order <c>KP, WA, KA,
    /// IT, KS, IS</c>. Provisional reconstruction: gameplay tuning; no
    /// historical measurement; see
    /// docs/research/movement/tall-hardwood-shield.md.
    /// </summary>
    [Fact]
    public void TheShieldedKalisRowCarriesItsApprovedProvisionalValues()
    {
        var row = TallHardwoodMovementProfiles.KalisRow;

        Assert.Equal(9_400, row.ForwardPaceBasisPoints);
        Assert.Equal(8_400, row.LateralPaceBasisPoints);
        Assert.Equal(6_700, row.BackwardPaceBasisPoints);
        Assert.Equal(3_000, row.CommittedPaceBasisPoints);
        Assert.Equal(13_000, row.PreferredDistanceBasisPoints);
        Assert.Equal(
            new[] { -250, 0, 250, 500, 0, 250 },
            row.OpponentDistanceOffsetBasisPoints);
        Assert.Equal(2, row.MaximumFacingStepsPerTick);
        Assert.Equal(1, row.CommittedFacingStepsPerTick);
        Assert.Equal(5_600, row.AccelerationBasisPointsPerTick);
        Assert.Equal(6_000, row.DecelerationBasisPointsPerTick);
        Assert.Equal(3, row.CommitmentTicks);
        Assert.Equal(3, row.RecoveryTicks);
        Assert.Equal(14_000, row.AllyClearanceBodyDiametersBasisPoints);
        Assert.Equal(17_500, row.DisengageEnemyToAllyBasisPoints);
        Assert.Equal(11_000, row.ReengageEnemyToAllyBasisPoints);
        Assert.Equal(10_000, row.PursuitSupportBodyDiametersBasisPoints);
    }

    /// <summary>
    /// Every scalar and every signed opponent-offset cell of the shielded Itak
    /// row, in the same canonical opponent order. Its reengage band of 11,000
    /// sits above solo Itak's 10,000, so the shielded loadout leaves
    /// disengagement at higher enemy pressure than the solo one. Provisional
    /// reconstruction: gameplay tuning; no historical measurement; see
    /// docs/research/movement/tall-hardwood-shield.md.
    /// </summary>
    [Fact]
    public void TheShieldedItakRowCarriesItsApprovedProvisionalValues()
    {
        var row = TallHardwoodMovementProfiles.ItakRow;

        Assert.Equal(9_700, row.ForwardPaceBasisPoints);
        Assert.Equal(8_700, row.LateralPaceBasisPoints);
        Assert.Equal(7_100, row.BackwardPaceBasisPoints);
        Assert.Equal(3_500, row.CommittedPaceBasisPoints);
        Assert.Equal(10_000, row.PreferredDistanceBasisPoints);
        Assert.Equal(
            new[] { -500, -250, 0, 250, -250, 0 },
            row.OpponentDistanceOffsetBasisPoints);
        Assert.Equal(2, row.MaximumFacingStepsPerTick);
        Assert.Equal(1, row.CommittedFacingStepsPerTick);
        Assert.Equal(6_500, row.AccelerationBasisPointsPerTick);
        Assert.Equal(7_000, row.DecelerationBasisPointsPerTick);
        Assert.Equal(3, row.CommitmentTicks);
        Assert.Equal(3, row.RecoveryTicks);
        Assert.Equal(13_500, row.AllyClearanceBodyDiametersBasisPoints);
        Assert.Equal(15_000, row.DisengageEnemyToAllyBasisPoints);
        Assert.Equal(11_000, row.ReengageEnemyToAllyBasisPoints);
        Assert.Equal(8_000, row.PursuitSupportBodyDiametersBasisPoints);
    }

    // ----- Keys and registry identity (design section 4.1) -----

    /// <summary>
    /// The exported key of each shield row is exactly its complete equipment
    /// triple. The profile is keyed on equipment alone, so the stored key
    /// always carries <see cref="RankId.Timawa"/>, the default rank the
    /// constructor writes whatever rank a caller supplies.
    /// </summary>
    [Fact]
    public void EachShieldRowExportsItsCompleteEquipmentKeyWithTheDefaultRank()
    {
        var kalisKey = TallHardwoodMovementProfiles.KalisRow.Loadout;
        var itakKey = TallHardwoodMovementProfiles.ItakRow.Loadout;

        Assert.Equal(WeaponId.Kalis, kalisKey.Weapon);
        Assert.Equal(ArmorId.LightOrganic, kalisKey.Armor);
        Assert.Equal(ShieldId.TallHardwood, kalisKey.Shield);
        Assert.Equal(RankId.Timawa, kalisKey.Rank);

        Assert.Equal(WeaponId.Itak, itakKey.Weapon);
        Assert.Equal(ArmorId.LightOrganic, itakKey.Armor);
        Assert.Equal(ShieldId.TallHardwood, itakKey.Shield);
        Assert.Equal(RankId.Timawa, itakKey.Rank);
    }

    /// <summary>
    /// The registry composes the exported rows themselves rather than copies
    /// of them, so the shielded Kalis row is the same instance held at
    /// canonical loadout index four and the same instance resolution returns.
    /// </summary>
    [Fact]
    public void TheShieldedKalisRowIsTheRegisteredCanonicalIndexFourInstance()
    {
        var row = TallHardwoodMovementProfiles.KalisRow;

        Assert.Same(row, V6.LoadoutMovementProfiles[4]);
        Assert.Same(row, V6.ResolveLoadoutProfile(row.Loadout));
        Assert.Same(row, V6.ResolveLoadoutProfile(ShieldedKalis()));
    }

    /// <summary>
    /// The same identity holds for the shielded Itak row at canonical loadout
    /// index five.
    /// </summary>
    [Fact]
    public void TheShieldedItakRowIsTheRegisteredCanonicalIndexFiveInstance()
    {
        var row = TallHardwoodMovementProfiles.ItakRow;

        Assert.Same(row, V6.LoadoutMovementProfiles[5]);
        Assert.Same(row, V6.ResolveLoadoutProfile(row.Loadout));
        Assert.Same(row, V6.ResolveLoadoutProfile(ShieldedItak()));
    }

    /// <summary>
    /// Rank is social standing and carries no movement meaning, so every rank
    /// resolves the one shielded Kalis row instance.
    /// </summary>
    [Theory]
    [InlineData(RankId.Datu)]
    [InlineData(RankId.Maharlika)]
    [InlineData(RankId.Timawa)]
    [InlineData(RankId.AlipingNamamahay)]
    [InlineData(RankId.Ayuey)]
    public void EveryRankResolvesTheSameShieldedKalisRowInstance(RankId rank)
    {
        Assert.Same(
            TallHardwoodMovementProfiles.KalisRow,
            V6.ResolveLoadoutProfile(ShieldedKalis(rank)));
    }

    /// <summary>
    /// Rank independence holds on the shielded Itak row as well, so the two
    /// shield rows behave identically in this respect.
    /// </summary>
    [Theory]
    [InlineData(RankId.Datu)]
    [InlineData(RankId.Maharlika)]
    [InlineData(RankId.Timawa)]
    [InlineData(RankId.AlipingNamamahay)]
    [InlineData(RankId.Ayuey)]
    public void EveryRankResolvesTheSameShieldedItakRowInstance(RankId rank)
    {
        Assert.Same(
            TallHardwoodMovementProfiles.ItakRow,
            V6.ResolveLoadoutProfile(ShieldedItak(rank)));
    }

    /// <summary>
    /// The canonical opponent index a shielded key maps to is the same index
    /// its profile occupies in the ruleset, which is what makes the sixth
    /// offset cell of every row the cell that faces shielded Itak.
    /// </summary>
    [Fact]
    public void EachShieldedKeyMapsToItsOwnCanonicalOpponentIndex()
    {
        Assert.Equal(4, MovementRouteRules.CanonicalOpponentIndex(
            ShieldedKalis()));
        Assert.Equal(5, MovementRouteRules.CanonicalOpponentIndex(
            ShieldedItak()));
    }

    /// <summary>
    /// Resolution of a shielded key never falls back to the solo row of the
    /// same weapon. This is the assertion that proves the runtime resolves a
    /// complete loadout rather than multiplying a solo row by a shield factor.
    /// </summary>
    [Fact]
    public void ResolvingAShieldedKeyNeverReturnsTheSoloRowOfTheSameWeapon()
    {
        var resolvedKalis = V6.ResolveLoadoutProfile(ShieldedKalis());
        var resolvedItak = V6.ResolveLoadoutProfile(ShieldedItak());

        Assert.NotSame(KalisMovementProfile.Row, resolvedKalis);
        Assert.NotSame(ItakMovementProfile.Row, resolvedItak);
        Assert.Same(TallHardwoodMovementProfiles.KalisRow, resolvedKalis);
        Assert.Same(TallHardwoodMovementProfiles.ItakRow, resolvedItak);
    }

    /// <summary>
    /// A tall-hardwood shield paired with a weapon that has no shielded row
    /// throws rather than inheriting another row's footwork. Only shielded
    /// Kalis and shielded Itak are mapped, so shielded Kampilan and shielded
    /// Wasay must both fail loudly.
    /// </summary>
    /// <remarks>
    /// The equivalent unsupported-armor case cannot be written honestly:
    /// <see cref="ArmorId"/> declares only <see cref="ArmorId.LightOrganic"/>,
    /// so there is no second armor value to pair a shield with, and casting a
    /// bogus enum value would assert against a state the type system already
    /// forbids. That case is therefore skipped rather than faked.
    /// </remarks>
    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    public void AShieldOnAWeaponWithNoShieldedRowThrowsRatherThanFallingBack(
        WeaponId weapon)
    {
        var unmapped = new CombatLoadout(
            weapon, ArmorId.LightOrganic, ShieldId.TallHardwood);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => V6.ResolveLoadoutProfile(unmapped));
    }

    /// <summary>
    /// The two shield rows are not one cloned generic shield row: they are
    /// distinct instances and they differ in value across pace, spacing, and
    /// pressure.
    /// </summary>
    [Fact]
    public void TheTwoShieldRowsAreDistinctInstancesAndDifferInValue()
    {
        var kalis = TallHardwoodMovementProfiles.KalisRow;
        var itak = TallHardwoodMovementProfiles.ItakRow;

        Assert.NotSame(kalis, itak);
        Assert.NotEqual(kalis.Loadout, itak.Loadout);
        Assert.NotEqual(
            kalis.ForwardPaceBasisPoints, itak.ForwardPaceBasisPoints);
        Assert.NotEqual(
            kalis.PreferredDistanceBasisPoints,
            itak.PreferredDistanceBasisPoints);
        Assert.NotEqual(
            kalis.DisengageEnemyToAllyBasisPoints,
            itak.DisengageEnemyToAllyBasisPoints);
    }

    // ----- The materialized solo-versus-shield envelope -----

    /// <summary>
    /// Shielded Kalis against solo Kalis, resolved through the V6 ruleset,
    /// field by field with the direction of each difference. Thirteen of the
    /// sixteen configured fields differ; the three ties are the two facing
    /// allowances and the reengage band, which stays at 11,000. The weapon
    /// plan's comparison table lists only eight rows: it omits committed pace,
    /// deceleration, commitment ticks, pursuit support, and the six
    /// opponent-distance offsets. Every figure is a provisional
    /// reconstruction: gameplay tuning; no historical measurement; see
    /// docs/research/movement/tall-hardwood-shield.md.
    /// </summary>
    [Fact]
    public void TheShieldedKalisRowDiffersFromSoloKalisInThirteenOfSixteenFields()
    {
        var shield = TallHardwoodMovementProfiles.KalisRow;
        var solo = V6.ResolveLoadoutProfile(SoloKalis());

        Assert.Same(KalisMovementProfile.Row, solo);

        Assert.Equal(
            -300,
            shield.ForwardPaceBasisPoints - solo.ForwardPaceBasisPoints);
        Assert.Equal(
            -500,
            shield.LateralPaceBasisPoints - solo.LateralPaceBasisPoints);
        Assert.Equal(
            -900,
            shield.BackwardPaceBasisPoints - solo.BackwardPaceBasisPoints);
        Assert.Equal(
            -300,
            shield.CommittedPaceBasisPoints - solo.CommittedPaceBasisPoints);
        Assert.Equal(
            1_000,
            shield.PreferredDistanceBasisPoints -
                solo.PreferredDistanceBasisPoints);
        Assert.Equal(
            new[] { 250, 250, 250, 250, -250, -250 },
            OffsetDifferences(shield, solo));
        Assert.Equal(
            -400,
            shield.AccelerationBasisPointsPerTick -
                solo.AccelerationBasisPointsPerTick);
        Assert.Equal(
            -1_000,
            shield.DecelerationBasisPointsPerTick -
                solo.DecelerationBasisPointsPerTick);
        Assert.Equal(1, shield.CommitmentTicks - solo.CommitmentTicks);
        Assert.Equal(1, shield.RecoveryTicks - solo.RecoveryTicks);
        Assert.Equal(
            2_000,
            shield.AllyClearanceBodyDiametersBasisPoints -
                solo.AllyClearanceBodyDiametersBasisPoints);
        Assert.Equal(
            2_500,
            shield.DisengageEnemyToAllyBasisPoints -
                solo.DisengageEnemyToAllyBasisPoints);
        Assert.Equal(
            -2_500,
            shield.PursuitSupportBodyDiametersBasisPoints -
                solo.PursuitSupportBodyDiametersBasisPoints);

        // The three ties, asserted as ties rather than left unstated.
        Assert.Equal(
            solo.MaximumFacingStepsPerTick, shield.MaximumFacingStepsPerTick);
        Assert.Equal(
            solo.CommittedFacingStepsPerTick,
            shield.CommittedFacingStepsPerTick);
        Assert.Equal(
            solo.ReengageEnemyToAllyBasisPoints,
            shield.ReengageEnemyToAllyBasisPoints);

        Assert.Equal(13, CountDifferingFields(shield, solo));
    }

    /// <summary>
    /// Shielded Itak against solo Itak, field by field with the direction of
    /// each difference. Fourteen of the sixteen configured fields differ; the
    /// only two ties are the facing allowances. Shielded Itak's reengage band
    /// rises by 1,000 where shielded Kalis's holds, which the weapon plan's
    /// comparison table omits along with committed pace, deceleration,
    /// commitment ticks, pursuit support, and the offsets. Every figure is a
    /// provisional reconstruction: gameplay tuning; no historical measurement;
    /// see docs/research/movement/tall-hardwood-shield.md.
    /// </summary>
    [Fact]
    public void TheShieldedItakRowDiffersFromSoloItakInFourteenOfSixteenFields()
    {
        var shield = TallHardwoodMovementProfiles.ItakRow;
        var solo = V6.ResolveLoadoutProfile(SoloItak());

        Assert.Same(ItakMovementProfile.Row, solo);

        Assert.Equal(
            -300,
            shield.ForwardPaceBasisPoints - solo.ForwardPaceBasisPoints);
        Assert.Equal(
            -600,
            shield.LateralPaceBasisPoints - solo.LateralPaceBasisPoints);
        Assert.Equal(
            -1_000,
            shield.BackwardPaceBasisPoints - solo.BackwardPaceBasisPoints);
        Assert.Equal(
            -500,
            shield.CommittedPaceBasisPoints - solo.CommittedPaceBasisPoints);
        Assert.Equal(
            -1_000,
            shield.PreferredDistanceBasisPoints -
                solo.PreferredDistanceBasisPoints);
        Assert.Equal(
            new[] { 250, 250, 250, 250, -250, -250 },
            OffsetDifferences(shield, solo));
        Assert.Equal(
            -500,
            shield.AccelerationBasisPointsPerTick -
                solo.AccelerationBasisPointsPerTick);
        Assert.Equal(
            -1_000,
            shield.DecelerationBasisPointsPerTick -
                solo.DecelerationBasisPointsPerTick);
        Assert.Equal(1, shield.CommitmentTicks - solo.CommitmentTicks);
        Assert.Equal(1, shield.RecoveryTicks - solo.RecoveryTicks);
        Assert.Equal(
            2_000,
            shield.AllyClearanceBodyDiametersBasisPoints -
                solo.AllyClearanceBodyDiametersBasisPoints);
        Assert.Equal(
            2_500,
            shield.DisengageEnemyToAllyBasisPoints -
                solo.DisengageEnemyToAllyBasisPoints);
        Assert.Equal(
            1_000,
            shield.ReengageEnemyToAllyBasisPoints -
                solo.ReengageEnemyToAllyBasisPoints);
        Assert.Equal(
            -2_000,
            shield.PursuitSupportBodyDiametersBasisPoints -
                solo.PursuitSupportBodyDiametersBasisPoints);

        // The only two ties, asserted as ties.
        Assert.Equal(
            solo.MaximumFacingStepsPerTick, shield.MaximumFacingStepsPerTick);
        Assert.Equal(
            solo.CommittedFacingStepsPerTick,
            shield.CommittedFacingStepsPerTick);

        Assert.Equal(14, CountDifferingFields(shield, solo));
    }

    /// <summary>
    /// Both shield rows shift all six opponent-distance offset cells by the
    /// same signed pattern relative to their solo counterparts: the four
    /// unshielded opponent columns move out by 250 basis points and the two
    /// shielded columns move in by 250. Provisional reconstruction: gameplay
    /// tuning; no historical measurement; see
    /// docs/research/movement/tall-hardwood-shield.md.
    /// </summary>
    [Fact]
    public void BothShieldRowsShiftEveryOpponentOffsetCellByTheSamePattern()
    {
        var expected = new[] { 250, 250, 250, 250, -250, -250 };

        Assert.Equal(
            expected,
            OffsetDifferences(
                TallHardwoodMovementProfiles.KalisRow,
                KalisMovementProfile.Row));
        Assert.Equal(
            expected,
            OffsetDifferences(
                TallHardwoodMovementProfiles.ItakRow,
                ItakMovementProfile.Row));
    }

    /// <summary>
    /// Each shield row asks for exactly 2,000 basis points of body diameter
    /// more ally clearance than its solo counterpart — the shield takes room
    /// in the line. Provisional reconstruction: gameplay tuning; no historical
    /// measurement; see docs/research/movement/tall-hardwood-shield.md.
    /// </summary>
    [Fact]
    public void EachShieldRowAsksForTwoThousandMoreAllyClearanceThanItsSoloRow()
    {
        Assert.Equal(
            2_000,
            TallHardwoodMovementProfiles.KalisRow
                .AllyClearanceBodyDiametersBasisPoints -
            KalisMovementProfile.Row.AllyClearanceBodyDiametersBasisPoints);
        Assert.Equal(
            2_000,
            TallHardwoodMovementProfiles.ItakRow
                .AllyClearanceBodyDiametersBasisPoints -
            ItakMovementProfile.Row.AllyClearanceBodyDiametersBasisPoints);
    }

    /// <summary>
    /// Each shield row recovers exactly one tick longer than its solo
    /// counterpart. Commitment also rises by exactly one tick, which the
    /// weapon plan's comparison table omits, so both durations are asserted
    /// here. Provisional reconstruction: gameplay tuning; no historical
    /// measurement; see docs/research/movement/tall-hardwood-shield.md.
    /// </summary>
    [Fact]
    public void EachShieldRowRecoversOneTickLongerThanItsSoloCounterpart()
    {
        Assert.Equal(
            1,
            TallHardwoodMovementProfiles.KalisRow.RecoveryTicks -
                KalisMovementProfile.Row.RecoveryTicks);
        Assert.Equal(
            1,
            TallHardwoodMovementProfiles.ItakRow.RecoveryTicks -
                ItakMovementProfile.Row.RecoveryTicks);

        Assert.Equal(
            1,
            TallHardwoodMovementProfiles.KalisRow.CommitmentTicks -
                KalisMovementProfile.Row.CommitmentTicks);
        Assert.Equal(
            1,
            TallHardwoodMovementProfiles.ItakRow.CommitmentTicks -
                ItakMovementProfile.Row.CommitmentTicks);
    }

    /// <summary>
    /// A shield never grants a speed bonus. Every pace field of both shield
    /// rows sits at or below the inclusive 10,000 basis-point human ceiling
    /// and at or below its solo counterpart's value. Provisional
    /// reconstruction: gameplay tuning; no historical measurement; see
    /// docs/research/movement/tall-hardwood-shield.md.
    /// </summary>
    [Fact]
    public void NeitherShieldRowGrantsASpeedBonusOverItsSoloCounterpart()
    {
        AssertNoSpeedBonus(
            TallHardwoodMovementProfiles.KalisRow, KalisMovementProfile.Row);
        AssertNoSpeedBonus(
            TallHardwoodMovementProfiles.ItakRow, ItakMovementProfile.Row);
    }

    /// <summary>
    /// Both shield rows sit inside the value ranges
    /// <see cref="LoadoutMovementProfile"/>'s constructor enforces: paces in
    /// the inclusive range one through 10,000, committed pace no greater than
    /// forward pace, facing steps in the inclusive range zero through eight,
    /// exactly six offset cells each within plus or minus 2,000 and never
    /// cancelling the preferred distance, positive durations and radii, and a
    /// reengage band strictly below the disengage band so hysteresis always
    /// exists. These rows are already-constructed statics, so the throwing
    /// side of that envelope is covered by <c>LoadoutMovementProfileTests</c>
    /// and this test asserts the values instead.
    /// </summary>
    [Fact]
    public void BothShieldRowsSitInsideTheConfiguredValueRanges()
    {
        AssertValueRanges(TallHardwoodMovementProfiles.KalisRow);
        AssertValueRanges(TallHardwoodMovementProfiles.ItakRow);
    }

    /// <summary>
    /// The product statement of the pair, read off the rows: shielded Kalis is
    /// the longer-spacing lane-control loadout and shielded Itak the closer
    /// repositioning one, and shielded Kalis tolerates greater enemy pressure
    /// before disengaging. Provisional reconstruction: gameplay tuning; no
    /// historical measurement; see
    /// docs/research/movement/tall-hardwood-shield.md.
    /// </summary>
    [Fact]
    public void ShieldedKalisHoldsALongerBandAndDisengagesUnderMorePressure()
    {
        var kalis = TallHardwoodMovementProfiles.KalisRow;
        var itak = TallHardwoodMovementProfiles.ItakRow;

        Assert.True(
            kalis.PreferredDistanceBasisPoints >
            itak.PreferredDistanceBasisPoints,
            "Shielded Kalis must hold the longer preferred band.");
        Assert.True(
            kalis.DisengageEnemyToAllyBasisPoints >
            itak.DisengageEnemyToAllyBasisPoints,
            "Shielded Kalis must require greater enemy pressure to " +
            "disengage than shielded Itak.");
    }

    // ----- No dynamic shield multiplier anywhere -----

    /// <summary>
    /// Neither the runtime nor profile construction applies a dynamic shield
    /// multiplier. Three observable consequences are asserted together: the
    /// row resolved for a shielded key is reference-identical to the exported
    /// row for every rank, so nothing is recomputed per resolution; the
    /// effective preferred distance is exactly the base band plus the
    /// opponent's offset cell scaled by combat reach, with no further factor;
    /// and the facing allowances stay at the solo value of two ordinary steps
    /// and one committed step, where the research candidate multiplier of 0.88
    /// would have truncated two sectors to one. That candidate was
    /// deliberately not adopted, being unrepresentable in a sixteen-sector
    /// model.
    /// </summary>
    [Fact]
    public void NoDynamicShieldMultiplierReachesEitherShieldRow()
    {
        foreach (var rank in AllRanks)
        {
            Assert.Same(
                TallHardwoodMovementProfiles.KalisRow,
                V6.ResolveLoadoutProfile(ShieldedKalis(rank)));
            Assert.Same(
                TallHardwoodMovementProfiles.ItakRow,
                V6.ResolveLoadoutProfile(ShieldedItak(rank)));
        }

        AssertUnscaledEffectiveDistance(TallHardwoodMovementProfiles.KalisRow);
        AssertUnscaledEffectiveDistance(TallHardwoodMovementProfiles.ItakRow);

        Assert.Equal(
            KalisMovementProfile.Row.MaximumFacingStepsPerTick,
            TallHardwoodMovementProfiles.KalisRow.MaximumFacingStepsPerTick);
        Assert.Equal(
            KalisMovementProfile.Row.CommittedFacingStepsPerTick,
            TallHardwoodMovementProfiles.KalisRow.CommittedFacingStepsPerTick);
        Assert.Equal(
            ItakMovementProfile.Row.MaximumFacingStepsPerTick,
            TallHardwoodMovementProfiles.ItakRow.MaximumFacingStepsPerTick);
        Assert.Equal(
            ItakMovementProfile.Row.CommittedFacingStepsPerTick,
            TallHardwoodMovementProfiles.ItakRow.CommittedFacingStepsPerTick);
    }

    // ----- Effective preferred distance per opponent column -----

    /// <summary>
    /// The shielded Kalis row across every canonical opponent column, at the
    /// canonical five-unit attack range of 5 * 1,024 = 5,120 raw. Its base
    /// preferred distance is 13,000 basis points and its offsets are
    /// [-250, 0, 250, 500, 0, 250], so each column is
    /// 5,120 * (13,000 + offset) / 10,000, truncating toward zero. Provisional
    /// reconstruction: gameplay tuning; no historical measurement; see
    /// docs/research/movement/tall-hardwood-shield.md.
    /// </summary>
    [Theory]
    [InlineData(0, 6_528L)] // KP: 5120 * 12750 / 10000
    [InlineData(1, 6_656L)] // WA: 5120 * 13000 / 10000
    [InlineData(2, 6_784L)] // KA: 5120 * 13250 / 10000
    [InlineData(3, 6_912L)] // IT: 5120 * 13500 / 10000
    [InlineData(4, 6_656L)] // KS: 5120 * 13000 / 10000
    [InlineData(5, 6_784L)] // IS: 5120 * 13250 / 10000
    public void TheShieldedKalisEffectiveDistanceCoversEveryOpponentColumn(
        int opponentCanonicalIndex, long expectedRaw)
    {
        Assert.Equal(
            expectedRaw,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw,
                TallHardwoodMovementProfiles.KalisRow,
                opponentCanonicalIndex));
    }

    /// <summary>
    /// The same six columns for the shielded Itak row, whose base preferred
    /// distance is 10,000 basis points and whose offsets are
    /// [-500, -250, 0, 250, -250, 0]. The Itak-owned counterpart of this
    /// theory is
    /// <c>ItakMovementProfileTests.TheShieldedItakEffectivePreferredDistanceCoversEveryOpponentColumn</c>;
    /// it is repeated here from the shield-owned perspective so that the two
    /// shield rows are pinned side by side in one place. Provisional
    /// reconstruction: gameplay tuning; no historical measurement; see
    /// docs/research/movement/tall-hardwood-shield.md.
    /// </summary>
    [Theory]
    [InlineData(0, 4_864L)] // KP: 5120 * 9500 / 10000
    [InlineData(1, 4_992L)] // WA: 5120 * 9750 / 10000
    [InlineData(2, 5_120L)] // KA: 5120 * 10000 / 10000
    [InlineData(3, 5_248L)] // IT: 5120 * 10250 / 10000
    [InlineData(4, 4_992L)] // KS: 5120 * 9750 / 10000
    [InlineData(5, 5_120L)] // IS: 5120 * 10000 / 10000
    public void TheShieldedItakEffectiveDistanceCoversEveryOpponentColumn(
        int opponentCanonicalIndex, long expectedRaw)
    {
        Assert.Equal(
            expectedRaw,
            MovementRouteRules.EffectivePreferredDistanceRaw(
                AttackRangeRaw,
                TallHardwoodMovementProfiles.ItakRow,
                opponentCanonicalIndex));
    }

    // ----- Helpers -----

    private static RankId[] AllRanks =>
    [
        RankId.Datu,
        RankId.Maharlika,
        RankId.Timawa,
        RankId.AlipingNamamahay,
        RankId.Ayuey,
    ];

    /// <summary>
    /// The signed cell-by-cell difference between a shield row's
    /// opponent-distance offsets and its solo counterpart's, in canonical
    /// opponent order.
    /// </summary>
    private static int[] OffsetDifferences(
        LoadoutMovementProfile shield, LoadoutMovementProfile solo)
    {
        var differences =
            new int[LoadoutMovementProfile.OpponentDistanceOffsetCount];
        for (var cell = 0; cell < differences.Length; cell++)
        {
            differences[cell] =
                shield.OpponentDistanceOffsetBasisPoints[cell] -
                solo.OpponentDistanceOffsetBasisPoints[cell];
        }

        return differences;
    }

    /// <summary>
    /// How many of the sixteen configured fields differ between two rows. The
    /// six offset cells count as the one field the constructor takes them as,
    /// so the maximum this can return is sixteen.
    /// </summary>
    private static int CountDifferingFields(
        LoadoutMovementProfile first, LoadoutMovementProfile second)
    {
        var offsetsDiffer = false;
        var differences = OffsetDifferences(first, second);
        foreach (var difference in differences)
        {
            if (difference != 0)
            {
                offsetsDiffer = true;
            }
        }

        var count = offsetsDiffer ? 1 : 0;
        count += Differs(
            first.ForwardPaceBasisPoints, second.ForwardPaceBasisPoints);
        count += Differs(
            first.LateralPaceBasisPoints, second.LateralPaceBasisPoints);
        count += Differs(
            first.BackwardPaceBasisPoints, second.BackwardPaceBasisPoints);
        count += Differs(
            first.CommittedPaceBasisPoints, second.CommittedPaceBasisPoints);
        count += Differs(
            first.PreferredDistanceBasisPoints,
            second.PreferredDistanceBasisPoints);
        count += Differs(
            first.MaximumFacingStepsPerTick,
            second.MaximumFacingStepsPerTick);
        count += Differs(
            first.CommittedFacingStepsPerTick,
            second.CommittedFacingStepsPerTick);
        count += Differs(
            first.AccelerationBasisPointsPerTick,
            second.AccelerationBasisPointsPerTick);
        count += Differs(
            first.DecelerationBasisPointsPerTick,
            second.DecelerationBasisPointsPerTick);
        count += Differs(first.CommitmentTicks, second.CommitmentTicks);
        count += Differs(first.RecoveryTicks, second.RecoveryTicks);
        count += Differs(
            first.AllyClearanceBodyDiametersBasisPoints,
            second.AllyClearanceBodyDiametersBasisPoints);
        count += Differs(
            first.DisengageEnemyToAllyBasisPoints,
            second.DisengageEnemyToAllyBasisPoints);
        count += Differs(
            first.ReengageEnemyToAllyBasisPoints,
            second.ReengageEnemyToAllyBasisPoints);
        count += Differs(
            first.PursuitSupportBodyDiametersBasisPoints,
            second.PursuitSupportBodyDiametersBasisPoints);
        return count;
    }

    private static int Differs(int first, int second) =>
        first == second ? 0 : 1;

    private static void AssertNoSpeedBonus(
        LoadoutMovementProfile shield, LoadoutMovementProfile solo)
    {
        Assert.InRange(shield.ForwardPaceBasisPoints, 1, 10_000);
        Assert.InRange(shield.LateralPaceBasisPoints, 1, 10_000);
        Assert.InRange(shield.BackwardPaceBasisPoints, 1, 10_000);
        Assert.InRange(shield.CommittedPaceBasisPoints, 1, 10_000);

        Assert.True(
            shield.ForwardPaceBasisPoints <= solo.ForwardPaceBasisPoints,
            "A shield may never raise forward pace.");
        Assert.True(
            shield.LateralPaceBasisPoints <= solo.LateralPaceBasisPoints,
            "A shield may never raise lateral pace.");
        Assert.True(
            shield.BackwardPaceBasisPoints <= solo.BackwardPaceBasisPoints,
            "A shield may never raise backward pace.");
        Assert.True(
            shield.CommittedPaceBasisPoints <= solo.CommittedPaceBasisPoints,
            "A shield may never raise committed pace.");
    }

    private static void AssertValueRanges(LoadoutMovementProfile row)
    {
        Assert.InRange(row.ForwardPaceBasisPoints, 1, 10_000);
        Assert.InRange(row.LateralPaceBasisPoints, 1, 10_000);
        Assert.InRange(row.BackwardPaceBasisPoints, 1, 10_000);
        Assert.InRange(row.CommittedPaceBasisPoints, 1, 10_000);
        Assert.True(
            row.CommittedPaceBasisPoints <= row.ForwardPaceBasisPoints,
            "Committed pace may never exceed forward pace.");
        Assert.InRange(row.PreferredDistanceBasisPoints, 1, int.MaxValue);

        Assert.Equal(
            LoadoutMovementProfile.OpponentDistanceOffsetCount,
            row.OpponentDistanceOffsetBasisPoints.Length);
        foreach (var cell in row.OpponentDistanceOffsetBasisPoints)
        {
            Assert.InRange(cell, -2_000, 2_000);
            Assert.InRange(
                row.PreferredDistanceBasisPoints + cell, 1, int.MaxValue);
        }

        Assert.InRange(row.MaximumFacingStepsPerTick, 0, 8);
        Assert.InRange(row.CommittedFacingStepsPerTick, 0, 8);
        Assert.InRange(row.AccelerationBasisPointsPerTick, 1, 10_000);
        Assert.InRange(row.DecelerationBasisPointsPerTick, 1, 10_000);
        Assert.InRange(row.CommitmentTicks, 1, int.MaxValue);
        Assert.InRange(row.RecoveryTicks, 1, int.MaxValue);
        Assert.InRange(
            row.AllyClearanceBodyDiametersBasisPoints, 1, int.MaxValue);
        Assert.InRange(
            row.PursuitSupportBodyDiametersBasisPoints, 1, int.MaxValue);

        Assert.True(
            row.ReengageEnemyToAllyBasisPoints <
            row.DisengageEnemyToAllyBasisPoints,
            "The reengage band must sit strictly below the disengage band " +
            "so hysteresis always exists.");
    }

    /// <summary>
    /// The effective preferred distance of one row is exactly combat reach
    /// times the base band plus the opponent's offset cell, divided by 10,000
    /// and truncated. No shield factor, and no other factor, is applied on
    /// top. The arithmetic is recomputed here in widened integer form rather
    /// than restated as a literal, so that a silent change to the formula is
    /// caught for every column at once.
    /// </summary>
    private static void AssertUnscaledEffectiveDistance(
        LoadoutMovementProfile row)
    {
        for (var column = 0;
            column < LoadoutMovementProfile.OpponentDistanceOffsetCount;
            column++)
        {
            var adjustedBasisPoints =
                row.PreferredDistanceBasisPoints +
                row.OpponentDistanceOffsetBasisPoints[column];
            var expected =
                (long)AttackRangeRaw * adjustedBasisPoints / 10_000L;

            Assert.Equal(
                expected,
                MovementRouteRules.EffectivePreferredDistanceRaw(
                    AttackRangeRaw, row, column));
        }
    }
}
