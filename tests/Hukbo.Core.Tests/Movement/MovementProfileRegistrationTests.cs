using System.Collections.Immutable;
using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Registration contract of <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/>:
/// the opt-in flag, the six canonical profile rows and their exact design
/// section 13 values, the rank-independent <see
/// cref="MovementRuleset.ResolveLoadoutProfile"/> lookup, the coupled
/// constructor validation, and the content-hash sensitivity of every profile
/// scalar and every signed offset cell. Every asserted profile value is a
/// provisional reconstruction — gameplay tuning, not a historical
/// measurement — pinned here so it cannot drift silently before the V6
/// digest ships.
/// </summary>
public sealed class MovementProfileRegistrationTests
{
    private static readonly CombatLoadout[] CanonicalLoadouts =
    [
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
        new(WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None),
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None),
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None),
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood),
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood),
    ];

    private static readonly MovementPresetId[] LegacyPresets =
    [
        MovementPresetId.IndependentPursuitV1,
        MovementPresetId.PersistentContingentsV2,
        MovementPresetId.PersistentContingentsV3,
        MovementPresetId.PersistentContingentsV4,
        MovementPresetId.PersistentContingentsV5,
    ];

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

    /// <summary>
    /// Rebuilds a V6-shaped ruleset from explicit rows so a test can perturb
    /// one value at a time. Every non-profile argument matches the registered
    /// V6 entry, which
    /// <see cref="RebuildingTheRegisteredRowsReproducesTheRegisteredContentHash"/>
    /// proves against the registry.
    /// </summary>
    private static MovementRuleset BuildV6Ruleset(
        ImmutableArray<LoadoutMovementProfile> profiles,
        int immediateRadiusBodyDiametersBasisPoints = 25_000,
        int supportRadiusBodyDiametersBasisPoints = 60_000) =>
        new(
            id: MovementPresetId.EquipmentRelativeFootworkV6,
            version: 1,
            cohesionRadiusMultiplier: 24,
            closeRadiusMultiplier: 16,
            closeFractionNumerator: 1,
            closeFractionDenominator: 2,
            minimumCohesiveMembers: 3,
            cohesionCycleTicks: 240,
            cohesionDutyTicks: 180,
            arrivalTaperMultiplier: 4,
            offsetUnit: 1024,
            narrowsCohesionScanToCohesionCapableContingents: true,
            selectsLeaderByRank: true,
            usesEquipmentRelativeFootwork: true,
            immediateRadiusBodyDiametersBasisPoints:
                immediateRadiusBodyDiametersBasisPoints,
            supportRadiusBodyDiametersBasisPoints:
                supportRadiusBodyDiametersBasisPoints,
            loadoutMovementProfiles: profiles);

    /// <summary>
    /// Clones one profile row, optionally decrementing one scalar (indexed 0
    /// through 14 in design section 4.2 declaration order, skipping the
    /// loadout key and the offset array) or one signed offset cell. A
    /// one-unit decrement stays inside every design 4.3 bound for every
    /// shipped row, so each variant constructs successfully and differs from
    /// its source in exactly one folded value.
    /// </summary>
    private static LoadoutMovementProfile Clone(
        LoadoutMovementProfile source,
        int scalarIndex = -1,
        int offsetCellIndex = -1)
    {
        var offsets = source.OpponentDistanceOffsetBasisPoints;
        if (offsetCellIndex >= 0)
        {
            offsets = offsets.SetItem(
                offsetCellIndex, offsets[offsetCellIndex] - 1);
        }

        int Adjust(int fieldIndex, int value) =>
            fieldIndex == scalarIndex ? value - 1 : value;

        return new LoadoutMovementProfile(
            source.Loadout,
            Adjust(0, source.ForwardPaceBasisPoints),
            Adjust(1, source.LateralPaceBasisPoints),
            Adjust(2, source.BackwardPaceBasisPoints),
            Adjust(3, source.CommittedPaceBasisPoints),
            Adjust(4, source.PreferredDistanceBasisPoints),
            offsets,
            Adjust(5, source.MaximumFacingStepsPerTick),
            Adjust(6, source.CommittedFacingStepsPerTick),
            Adjust(7, source.AccelerationBasisPointsPerTick),
            Adjust(8, source.DecelerationBasisPointsPerTick),
            Adjust(9, source.CommitmentTicks),
            Adjust(10, source.RecoveryTicks),
            Adjust(11, source.AllyClearanceBodyDiametersBasisPoints),
            Adjust(12, source.DisengageEnemyToAllyBasisPoints),
            Adjust(13, source.ReengageEnemyToAllyBasisPoints),
            Adjust(14, source.PursuitSupportBodyDiametersBasisPoints));
    }

    private static ImmutableArray<LoadoutMovementProfile> CloneRegisteredRows(
        int variantRowIndex = -1,
        int scalarIndex = -1,
        int offsetCellIndex = -1)
    {
        var builder = ImmutableArray.CreateBuilder<LoadoutMovementProfile>(
            MovementRuleset.CanonicalLoadoutCount);
        for (var row = 0; row < V6.LoadoutMovementProfiles.Length; row++)
        {
            builder.Add(row == variantRowIndex
                ? Clone(
                    V6.LoadoutMovementProfiles[row], scalarIndex, offsetCellIndex)
                : Clone(V6.LoadoutMovementProfiles[row]));
        }

        return builder.MoveToImmutable();
    }

    public static TheoryData<int, int> EveryRowAndScalar()
    {
        var data = new TheoryData<int, int>();
        for (var row = 0; row < 6; row++)
        {
            for (var scalar = 0; scalar < 15; scalar++)
            {
                data.Add(row, scalar);
            }
        }

        return data;
    }

    public static TheoryData<int, int> EveryRowAndOffsetCell()
    {
        var data = new TheoryData<int, int>();
        for (var row = 0; row < 6; row++)
        {
            for (var cell = 0; cell < 6; cell++)
            {
                data.Add(row, cell);
            }
        }

        return data;
    }

    [Fact]
    public void V6UsesEquipmentRelativeFootworkWithBothContextRadii()
    {
        Assert.True(V6.UsesEquipmentRelativeFootwork);
        Assert.Equal(25_000, V6.ImmediateRadiusBodyDiametersBasisPoints);
        Assert.Equal(60_000, V6.SupportRadiusBodyDiametersBasisPoints);
    }

    [Fact]
    public void V6CarriesExactlySixProfilesInCanonicalOrder()
    {
        Assert.Equal(
            MovementRuleset.CanonicalLoadoutCount,
            V6.LoadoutMovementProfiles.Length);
        Assert.Equal(
            CanonicalLoadouts,
            V6.LoadoutMovementProfiles.Select(profile => profile.Loadout));
    }

    [Fact]
    public void V6ProfileKeysAreUnique()
    {
        Assert.Equal(
            MovementRuleset.CanonicalLoadoutCount,
            V6.LoadoutMovementProfiles
                .Select(profile => profile.Loadout)
                .Distinct()
                .Count());
    }

    [Fact]
    public void EveryLegacyPresetCarriesNoEquipmentRelativeFootwork()
    {
        foreach (var preset in LegacyPresets)
        {
            var ruleset = MovementPresetRegistry.Get(preset);

            Assert.False(ruleset.UsesEquipmentRelativeFootwork);
            Assert.Equal(0, ruleset.ImmediateRadiusBodyDiametersBasisPoints);
            Assert.Equal(0, ruleset.SupportRadiusBodyDiametersBasisPoints);
            Assert.Empty(ruleset.LoadoutMovementProfiles);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ResolveLoadoutProfileResolvesEveryCanonicalLoadout(int index)
    {
        var resolved = V6.ResolveLoadoutProfile(CanonicalLoadouts[index]);

        Assert.Same(V6.LoadoutMovementProfiles[index], resolved);
        Assert.Equal(CanonicalLoadouts[index], resolved.Loadout);
    }

    /// <summary>
    /// Design 4.1: the profile key is equipment only. Two loadouts differing
    /// only in rank resolve to the same profile row instance.
    /// </summary>
    [Fact]
    public void LoadoutsDifferingOnlyInRankResolveToTheSameProfileRow()
    {
        var timawa = new CombatLoadout(
            WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None,
            RankId.Timawa);
        var datu = new CombatLoadout(
            WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None,
            RankId.Datu);

        Assert.Same(
            V6.ResolveLoadoutProfile(timawa),
            V6.ResolveLoadoutProfile(datu));
    }

    /// <summary>
    /// A key no profile row may carry fails loudly rather than inheriting
    /// another row's footwork. Kampilan and Wasay are two-handed and never
    /// pair with a shield, so those keys are unmapped by construction.
    /// </summary>
    [Theory]
    [InlineData(WeaponId.Kampilan, ShieldId.TallHardwood)]
    [InlineData(WeaponId.Wasay, ShieldId.TallHardwood)]
    public void ResolveLoadoutProfileThrowsForAnUnsupportedLoadout(
        WeaponId weapon, ShieldId shield)
    {
        var unsupported = new CombatLoadout(
            weapon, ArmorId.LightOrganic, shield);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => V6.ResolveLoadoutProfile(unsupported));
    }

    /// <summary>
    /// A legacy preset carries no profile rows, so every resolution under it
    /// throws instead of returning a default.
    /// </summary>
    [Fact]
    public void ResolveLoadoutProfileThrowsUnderEveryLegacyPreset()
    {
        foreach (var preset in LegacyPresets)
        {
            var ruleset = MovementPresetRegistry.Get(preset);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => ruleset.ResolveLoadoutProfile(CanonicalLoadouts[0]));
        }
    }

    /// <summary>
    /// Design 13 row <c>KP</c>, every scalar and every signed offset cell.
    /// Provisional reconstruction: gameplay tuning; no historical
    /// measurement.
    /// </summary>
    [Fact]
    public void TheKampilanRowCarriesTheDesignSectionThirteenValues()
    {
        AssertRow(
            V6.LoadoutMovementProfiles[0],
            forward: 9_800, lateral: 8_200, backward: 7_000, committed: 3_000,
            preferred: 11_500, offsets: [0, 0, 250, 500, 250, 500],
            turn: 2, committedTurn: 1, acceleration: 5_000,
            deceleration: 6_000, commitTicks: 3, recoveryTicks: 3,
            clearance: 15_000, disengage: 20_000, reengage: 12_500,
            pursuitSupport: 12_500);
    }

    /// <summary>
    /// Design 13 row <c>WA</c>, every scalar and every signed offset cell.
    /// Provisional reconstruction: gameplay tuning; no historical
    /// measurement.
    /// </summary>
    [Fact]
    public void TheWasayRowCarriesTheDesignSectionThirteenValues()
    {
        AssertRow(
            V6.LoadoutMovementProfiles[1],
            forward: 9_400, lateral: 7_400, backward: 6_400, committed: 2_500,
            preferred: 10_800, offsets: [500, 0, 250, 500, 250, 500],
            turn: 1, committedTurn: 1, acceleration: 4_000,
            deceleration: 5_000, commitTicks: 4, recoveryTicks: 4,
            clearance: 17_500, disengage: 20_000, reengage: 12_500,
            pursuitSupport: 10_000);
    }

    /// <summary>
    /// Design 13 row <c>KA</c>, every scalar and every signed offset cell.
    /// Provisional reconstruction: gameplay tuning; no historical
    /// measurement.
    /// </summary>
    [Fact]
    public void TheKalisRowCarriesTheDesignSectionThirteenValues()
    {
        AssertRow(
            V6.LoadoutMovementProfiles[2],
            forward: 9_700, lateral: 8_900, backward: 7_600, committed: 3_300,
            preferred: 12_000, offsets: [-500, -250, 0, 250, 250, 500],
            turn: 2, committedTurn: 1, acceleration: 6_000,
            deceleration: 7_000, commitTicks: 2, recoveryTicks: 2,
            clearance: 12_000, disengage: 15_000, reengage: 11_000,
            pursuitSupport: 12_500);
    }

    /// <summary>
    /// Design 13 row <c>IT</c>, every scalar and every signed offset cell.
    /// Its forward pace sits exactly at the inclusive 10,000 maximum.
    /// Provisional reconstruction: gameplay tuning; no historical
    /// measurement.
    /// </summary>
    [Fact]
    public void TheItakRowCarriesTheDesignSectionThirteenValues()
    {
        AssertRow(
            V6.LoadoutMovementProfiles[3],
            forward: 10_000, lateral: 9_300, backward: 8_100, committed: 4_000,
            preferred: 11_000, offsets: [-750, -500, -250, 0, 0, 250],
            turn: 2, committedTurn: 1, acceleration: 7_000,
            deceleration: 8_000, commitTicks: 2, recoveryTicks: 2,
            clearance: 11_500, disengage: 12_500, reengage: 10_000,
            pursuitSupport: 10_000);
    }

    /// <summary>
    /// Design 13 row <c>KS</c>, every scalar and every signed offset cell.
    /// The shielded Kalis preferred distance is 13,000, not the stale 1.10
    /// sentence in the Kalis plan (design section 13.3). Provisional
    /// reconstruction: gameplay tuning; no historical measurement.
    /// </summary>
    [Fact]
    public void TheShieldedKalisRowCarriesTheDesignSectionThirteenValues()
    {
        AssertRow(
            V6.LoadoutMovementProfiles[4],
            forward: 9_400, lateral: 8_400, backward: 6_700, committed: 3_000,
            preferred: 13_000, offsets: [-250, 0, 250, 500, 0, 250],
            turn: 2, committedTurn: 1, acceleration: 5_600,
            deceleration: 6_000, commitTicks: 3, recoveryTicks: 3,
            clearance: 14_000, disengage: 17_500, reengage: 11_000,
            pursuitSupport: 10_000);
    }

    /// <summary>
    /// Design 13 row <c>IS</c>, every scalar and every signed offset cell.
    /// Its reengage band of 11,000 sits above solo Itak's 10,000 (design
    /// section 13.3). Provisional reconstruction: gameplay tuning; no
    /// historical measurement.
    /// </summary>
    [Fact]
    public void TheShieldedItakRowCarriesTheDesignSectionThirteenValues()
    {
        AssertRow(
            V6.LoadoutMovementProfiles[5],
            forward: 9_700, lateral: 8_700, backward: 7_100, committed: 3_500,
            preferred: 10_000, offsets: [-500, -250, 0, 250, -250, 0],
            turn: 2, committedTurn: 1, acceleration: 6_500,
            deceleration: 7_000, commitTicks: 3, recoveryTicks: 3,
            clearance: 13_500, disengage: 15_000, reengage: 11_000,
            pursuitSupport: 8_000);
    }

    /// <summary>
    /// The identity baseline for every sensitivity case below: rebuilding
    /// the six registered rows field-for-field, through fresh constructor
    /// calls, reproduces the registered content hash exactly, because the
    /// ruleset folds values, not instances.
    /// </summary>
    [Fact]
    public void RebuildingTheRegisteredRowsReproducesTheRegisteredContentHash()
    {
        var rebuilt = BuildV6Ruleset(CloneRegisteredRows());

        Assert.Equal(V6.ContentHash, rebuilt.ContentHash);
    }

    /// <summary>
    /// Design 5.1: the fold is order-of-construction independent. Building
    /// the same six rows through a differently ordered sequence of
    /// constructor calls — here reverse canonical order — produces the same
    /// content hash, because the ruleset folds the canonical order it was
    /// validated to hold and sorts nothing at hash time.
    /// </summary>
    [Fact]
    public void ADifferentConstructorCallOrderProducesTheSameContentHash()
    {
        var rows = new LoadoutMovementProfile[
            MovementRuleset.CanonicalLoadoutCount];
        for (var row = rows.Length - 1; row >= 0; row--)
        {
            rows[row] = Clone(V6.LoadoutMovementProfiles[row]);
        }

        var rebuilt = BuildV6Ruleset([.. rows]);

        Assert.Equal(V6.ContentHash, rebuilt.ContentHash);
    }

    /// <summary>
    /// Design 5.1: changing any single profile scalar changes the V6 content
    /// hash. One case per scalar per row.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryRowAndScalar))]
    public void ChangingAnyProfileScalarChangesTheContentHash(
        int rowIndex, int scalarIndex)
    {
        var variant = BuildV6Ruleset(
            CloneRegisteredRows(rowIndex, scalarIndex: scalarIndex));

        Assert.NotEqual(V6.ContentHash, variant.ContentHash);
    }

    /// <summary>
    /// Design 5.1: changing any single signed offset cell changes the V6
    /// content hash — negative offsets fold as their two's-complement
    /// value, so a one-unit move across zero is still visible. One case per
    /// cell per row.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryRowAndOffsetCell))]
    public void ChangingAnyOpponentOffsetCellChangesTheContentHash(
        int rowIndex, int offsetCellIndex)
    {
        var variant = BuildV6Ruleset(
            CloneRegisteredRows(rowIndex, offsetCellIndex: offsetCellIndex));

        Assert.NotEqual(V6.ContentHash, variant.ContentHash);
    }

    [Fact]
    public void ChangingEitherContextRadiusChangesTheContentHash()
    {
        var immediateVariant = BuildV6Ruleset(
            CloneRegisteredRows(),
            immediateRadiusBodyDiametersBasisPoints: 24_999);
        var supportVariant = BuildV6Ruleset(
            CloneRegisteredRows(),
            supportRadiusBodyDiametersBasisPoints: 59_999);

        Assert.NotEqual(V6.ContentHash, immediateVariant.ContentHash);
        Assert.NotEqual(V6.ContentHash, supportVariant.ContentHash);
    }

    /// <summary>
    /// The coupled validation of design section 5: a preset without the
    /// footwork flag rejects a nonzero radius or a non-empty profile
    /// collection, and a preset with it rejects a zero radius, a wrong row
    /// count, and rows out of canonical order.
    /// </summary>
    [Fact]
    public void AFlagOffRulesetRejectsAnyEquipmentRelativeArgument()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildLegacyShapedRuleset(immediateRadius: 25_000));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildLegacyShapedRuleset(supportRadius: 60_000));
        Assert.Throws<ArgumentException>(
            () => BuildLegacyShapedRuleset(
                profiles: CloneRegisteredRows()));
    }

    [Fact]
    public void AFlagOnRulesetRejectsZeroRadiiAndMalformedRowSets()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildV6Ruleset(
                CloneRegisteredRows(),
                immediateRadiusBodyDiametersBasisPoints: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BuildV6Ruleset(
                CloneRegisteredRows(),
                supportRadiusBodyDiametersBasisPoints: 0));
        Assert.Throws<ArgumentException>(
            () => BuildV6Ruleset(
                ImmutableArray<LoadoutMovementProfile>.Empty));
        Assert.Throws<ArgumentException>(
            () => BuildV6Ruleset(CloneRegisteredRows().RemoveAt(5)));

        var swapped = CloneRegisteredRows();
        swapped = swapped
            .SetItem(0, Clone(V6.LoadoutMovementProfiles[1]))
            .SetItem(1, Clone(V6.LoadoutMovementProfiles[0]));
        Assert.Throws<ArgumentException>(() => BuildV6Ruleset(swapped));

        var duplicated = CloneRegisteredRows()
            .SetItem(1, Clone(V6.LoadoutMovementProfiles[0]));
        Assert.Throws<ArgumentException>(() => BuildV6Ruleset(duplicated));
    }

    private static MovementRuleset BuildLegacyShapedRuleset(
        int immediateRadius = 0,
        int supportRadius = 0,
        ImmutableArray<LoadoutMovementProfile>? profiles = null) =>
        new(
            id: MovementPresetId.PersistentContingentsV5,
            version: 1,
            cohesionRadiusMultiplier: 24,
            closeRadiusMultiplier: 16,
            closeFractionNumerator: 1,
            closeFractionDenominator: 2,
            minimumCohesiveMembers: 3,
            cohesionCycleTicks: 240,
            cohesionDutyTicks: 180,
            arrivalTaperMultiplier: 4,
            offsetUnit: 1024,
            narrowsCohesionScanToCohesionCapableContingents: true,
            selectsLeaderByRank: true,
            usesEquipmentRelativeFootwork: false,
            immediateRadiusBodyDiametersBasisPoints: immediateRadius,
            supportRadiusBodyDiametersBasisPoints: supportRadius,
            loadoutMovementProfiles:
                profiles ?? ImmutableArray<LoadoutMovementProfile>.Empty);

    private static void AssertRow(
        LoadoutMovementProfile row,
        int forward,
        int lateral,
        int backward,
        int committed,
        int preferred,
        int[] offsets,
        int turn,
        int committedTurn,
        int acceleration,
        int deceleration,
        int commitTicks,
        int recoveryTicks,
        int clearance,
        int disengage,
        int reengage,
        int pursuitSupport)
    {
        Assert.Equal(forward, row.ForwardPaceBasisPoints);
        Assert.Equal(lateral, row.LateralPaceBasisPoints);
        Assert.Equal(backward, row.BackwardPaceBasisPoints);
        Assert.Equal(committed, row.CommittedPaceBasisPoints);
        Assert.Equal(preferred, row.PreferredDistanceBasisPoints);
        Assert.Equal(offsets, row.OpponentDistanceOffsetBasisPoints);
        Assert.Equal(turn, row.MaximumFacingStepsPerTick);
        Assert.Equal(committedTurn, row.CommittedFacingStepsPerTick);
        Assert.Equal(acceleration, row.AccelerationBasisPointsPerTick);
        Assert.Equal(deceleration, row.DecelerationBasisPointsPerTick);
        Assert.Equal(commitTicks, row.CommitmentTicks);
        Assert.Equal(recoveryTicks, row.RecoveryTicks);
        Assert.Equal(clearance, row.AllyClearanceBodyDiametersBasisPoints);
        Assert.Equal(disengage, row.DisengageEnemyToAllyBasisPoints);
        Assert.Equal(reengage, row.ReengageEnemyToAllyBasisPoints);
        Assert.Equal(
            pursuitSupport, row.PursuitSupportBodyDiametersBasisPoints);
    }
}
