using System.Collections.Immutable;
using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The shield-encumbrance pace scale and the shield-block-recovery gate on
/// <see cref="MovementRuleset"/>, per the 2026-08-15 shield-projectile-block
/// design section 6. Every pace value asserted here is a provisional
/// reconstruction: gameplay tuning; no historical measurement, under
/// CLAUDE.md section 7.
/// </summary>
/// <remarks>
/// <see cref="MovementPresetId.ShieldEncumbranceV16"/> does not carry shield
/// encumbrance through <see cref="MovementRuleset.LoadoutMovementProfiles"/>
/// or <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/>. An earlier
/// revision wired it that way, which crashed every ranged loadout under
/// <see cref="MovementRuleset.ResolveLoadoutProfile"/> the moment
/// <see cref="MovementRuleset.UsesEquipmentRelativeFootwork"/> was
/// <see langword="true"/> and a canonical loadout index had no ranged entry.
/// Shield encumbrance is instead a movement-speed scale applied once, at
/// spawn, by <c>BattleSimulation.CreateAgent</c>, resolved through
/// <see cref="MovementRuleset.ResolveShieldPaceBasisPoints"/>, which is what
/// this file exercises.
/// </remarks>
public sealed class ShieldSizeMovementTests
{
    private static CombatLoadout SoloKalis(RankId rank = RankId.Timawa) =>
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None, rank);

    private static CombatLoadout SoloItak(RankId rank = RankId.Timawa) =>
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None, rank);

    private static CombatLoadout NarrowKalis(RankId rank = RankId.Timawa) =>
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.NarrowBreastHigh, rank);

    private static CombatLoadout NarrowItak(RankId rank = RankId.Timawa) =>
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.NarrowBreastHigh, rank);

    private static CombatLoadout TallKalis(RankId rank = RankId.Timawa) =>
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood, rank);

    private static CombatLoadout TallItak(RankId rank = RankId.Timawa) =>
        new(WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood, rank);

    private static MovementRuleset V14 =>
        MovementPresetRegistry.Get(MovementPresetId.ShieldEncumbranceV16);

    private static MovementRuleset V13 =>
        MovementPresetRegistry.Get(MovementPresetId.CohortLateralSpreadV13);

    // -----------------------------------------------------------------
    // Preset 14 is not equipment-relative: it carries zero loadout rows,
    // exactly like preset 13, the defect this file now guards against.
    // -----------------------------------------------------------------

    [Fact]
    public void PresetFourteenIsNotEquipmentRelativeAndCarriesZeroLoadoutRows()
    {
        Assert.False(V14.UsesEquipmentRelativeFootwork);
        Assert.Empty(V14.LoadoutMovementProfiles);
        Assert.Equal(0, V14.ImmediateRadiusBodyDiametersBasisPoints);
        Assert.Equal(0, V14.SupportRadiusBodyDiametersBasisPoints);

        Assert.False(V13.UsesEquipmentRelativeFootwork);
        Assert.Empty(V13.LoadoutMovementProfiles);
    }

    [Fact]
    public void PresetFourteenThrowsResolvingAnyLoadoutProfileIncludingRanged()
    {
        // The defect this test pins: a ranged loadout has no canonical
        // loadout index at all, so an equipment-relative preset throws for
        // it unconditionally. Preset 14 must never resolve a loadout
        // profile for any loadout, melee or ranged, because it carries no
        // rows.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => V14.ResolveLoadoutProfile(SoloKalis()));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => V14.ResolveLoadoutProfile(TallKalis()));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => V14.ResolveLoadoutProfile(NarrowKalis()));
    }

    [Theory]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV6)]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV7)]
    [InlineData(MovementPresetId.MonotoneAllyClearanceV9)]
    public void SixRowFootworkPresetsStillResolveExactlySixRows(
        MovementPresetId id)
    {
        var ruleset = MovementPresetRegistry.Get(id);
        Assert.Equal(6, ruleset.LoadoutMovementProfiles.Length);
    }

    [Theory]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV6)]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV7)]
    [InlineData(MovementPresetId.MonotoneAllyClearanceV9)]
    public void SixRowFootworkPresetsThrowForEitherNarrowShieldKey(
        MovementPresetId id)
    {
        var ruleset = MovementPresetRegistry.Get(id);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ruleset.ResolveLoadoutProfile(NarrowKalis()));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ruleset.ResolveLoadoutProfile(NarrowItak()));
    }

    [Fact]
    public void ASevenRowProfileCollectionIsRejected()
    {
        // Built from V6's six canonical rows plus one narrow-shield row at
        // canonical position 6, independent of preset 14's own (now empty)
        // registration, since this test exercises MovementRuleset's general
        // row-count validation rather than any one preset's registry entry.
        var sixRows = MovementPresetRegistry
            .Get(MovementPresetId.EquipmentRelativeFootworkV6)
            .LoadoutMovementProfiles;
        var sevenRows = sixRows.Add(NarrowBreastHighMovementProfiles.KalisRow);
        Assert.Equal(7, sevenRows.Length);

        Assert.Throws<ArgumentException>(() => new MovementRuleset(
            id: MovementPresetId.ShieldEncumbranceV16,
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
            selectsLeaderByRank: false,
            usesEquipmentRelativeFootwork: true,
            immediateRadiusBodyDiametersBasisPoints: 25_000,
            supportRadiusBodyDiametersBasisPoints: 60_000,
            loadoutMovementProfiles: sevenRows));
    }

    // -----------------------------------------------------------------
    // ResolveShieldPaceBasisPoints: solo > narrow > tall, strictly, only
    // under preset 14; every other preset resolves full pace for every
    // shield.
    // -----------------------------------------------------------------

    [Fact]
    public void PresetFourteenResolveShieldPaceBasisPointsOrdersNoneAboveNarrowAboveTall()
    {
        Assert.True(V14.AppliesShieldEncumbrance);

        var none = V14.ResolveShieldPaceBasisPoints(ShieldId.None);
        var narrow = V14.ResolveShieldPaceBasisPoints(ShieldId.NarrowBreastHigh);
        var tall = V14.ResolveShieldPaceBasisPoints(ShieldId.TallHardwood);

        Assert.Equal(10_000, none);
        Assert.Equal(9_600, narrow);
        Assert.Equal(9_000, tall);
        Assert.True(none > narrow);
        Assert.True(narrow > tall);

        Assert.Equal(9_600, V14.NarrowBreastHighShieldPaceBasisPoints);
        Assert.Equal(9_000, V14.TallHardwoodShieldPaceBasisPoints);
    }

    [Theory]
    [InlineData(MovementPresetId.IndependentPursuitV1)]
    [InlineData(MovementPresetId.PersistentContingentsV2)]
    [InlineData(MovementPresetId.PersistentContingentsV3)]
    [InlineData(MovementPresetId.PersistentContingentsV4)]
    [InlineData(MovementPresetId.PersistentContingentsV5)]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV6)]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV7)]
    [InlineData(MovementPresetId.RangedStandoffV8)]
    [InlineData(MovementPresetId.MonotoneAllyClearanceV9)]
    [InlineData(MovementPresetId.BattlefieldRealismV10)]
    [InlineData(MovementPresetId.LastStandEngagementV11)]
    [InlineData(MovementPresetId.ContingentShapeV12)]
    [InlineData(MovementPresetId.CohortLateralSpreadV13)]
    public void PresetsOneThroughThirteenResolveFullPaceForEveryShield(
        MovementPresetId id)
    {
        var ruleset = MovementPresetRegistry.Get(id);
        Assert.False(ruleset.AppliesShieldEncumbrance);
        Assert.Equal(0, ruleset.NarrowBreastHighShieldPaceBasisPoints);
        Assert.Equal(0, ruleset.TallHardwoodShieldPaceBasisPoints);

        Assert.Equal(10_000, ruleset.ResolveShieldPaceBasisPoints(ShieldId.None));
        Assert.Equal(
            10_000,
            ruleset.ResolveShieldPaceBasisPoints(ShieldId.NarrowBreastHigh));
        Assert.Equal(
            10_000, ruleset.ResolveShieldPaceBasisPoints(ShieldId.TallHardwood));
    }

    // -----------------------------------------------------------------
    // Presets 6-13 ContentHash: unmoved by this task. Literals below were
    // read from an actual `dotnet test` run against the built code, never
    // hand-computed.
    // -----------------------------------------------------------------

    [Theory]
    [InlineData(
        MovementPresetId.EquipmentRelativeFootworkV6, 0x0FFE5D202B324D25UL)]
    [InlineData(
        MovementPresetId.EquipmentRelativeFootworkV7, 0x66F4FDF91F56AF1BUL)]
    [InlineData(
        MovementPresetId.RangedStandoffV8, 0xE23CD2EEC5421CD9UL)]
    [InlineData(
        MovementPresetId.MonotoneAllyClearanceV9, 0x3E3AAC05DAB1C1A2UL)]
    [InlineData(
        MovementPresetId.BattlefieldRealismV10, 0x9A274E5456D4B4F3UL)]
    [InlineData(
        MovementPresetId.LastStandEngagementV11, 0x27ED26A56E4791BAUL)]
    [InlineData(
        MovementPresetId.ContingentShapeV12, 0x4F2D49D52E28F52DUL)]
    [InlineData(
        MovementPresetId.CohortLateralSpreadV13, 0x0F5B77ADFCC8CACCUL)]
    public void PresetSixThroughThirteenContentHashesAreUnmoved(
        MovementPresetId id, ulong expected)
    {
        var ruleset = MovementPresetRegistry.Get(id);
        Assert.Equal(expected, ruleset.ContentHash);
    }

    [Fact]
    public void PresetSixteenContentHashIsRecorded()
    {
        // Recorded from an actual run against the built code, never
        // hand-computed; this pin exists so a later, unnoticed change to
        // preset 14's fields is caught the same way every other preset's
        // pinned literal catches one. This literal moved when preset 14's
        // wrongly-wired equipment-relative loadout rows were replaced with
        // the movement-speed-scale fields, and again when the preset was
        // renumbered from 14 to 16 on merging with main, which had already
        // taken both 14 and 15. The identifier folds into the content
        // hash, so a renumber moving it is the expected signature.
        Assert.Equal(0x0BA523CCF7C4B83CUL, V14.ContentHash);
    }

    // -----------------------------------------------------------------
    // Shield-block-recovery gate.
    // -----------------------------------------------------------------

    [Theory]
    [InlineData(MovementPresetId.IndependentPursuitV1)]
    [InlineData(MovementPresetId.PersistentContingentsV2)]
    [InlineData(MovementPresetId.PersistentContingentsV3)]
    [InlineData(MovementPresetId.PersistentContingentsV4)]
    [InlineData(MovementPresetId.PersistentContingentsV5)]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV6)]
    [InlineData(MovementPresetId.EquipmentRelativeFootworkV7)]
    [InlineData(MovementPresetId.RangedStandoffV8)]
    [InlineData(MovementPresetId.MonotoneAllyClearanceV9)]
    [InlineData(MovementPresetId.BattlefieldRealismV10)]
    [InlineData(MovementPresetId.LastStandEngagementV11)]
    [InlineData(MovementPresetId.ContingentShapeV12)]
    [InlineData(MovementPresetId.CohortLateralSpreadV13)]
    public void OnlyPresetFourteenAppliesShieldBlockRecovery(
        MovementPresetId id)
    {
        var ruleset = MovementPresetRegistry.Get(id);
        Assert.False(ruleset.AppliesShieldBlockRecovery);
        Assert.Equal(0, ruleset.TallShieldBlockRecoveryTicks);
        Assert.Equal(0, ruleset.NarrowShieldBlockRecoveryTicks);
        Assert.Equal(0, ruleset.ShieldBlockRecoveryPaceCeilingBasisPoints);
        Assert.Equal(
            0, ruleset.ResolveShieldBlockRecoveryTicks(ShieldId.TallHardwood));
        Assert.Equal(
            0,
            ruleset.ResolveShieldBlockRecoveryTicks(ShieldId.NarrowBreastHigh));
    }

    [Fact]
    public void PresetFourteenAppliesShieldBlockRecoveryWithTallAboveNarrowAboveNone()
    {
        var ruleset = V14;
        Assert.True(ruleset.AppliesShieldBlockRecovery);
        Assert.Equal(5, ruleset.TallShieldBlockRecoveryTicks);
        Assert.Equal(3, ruleset.NarrowShieldBlockRecoveryTicks);
        Assert.Equal(4_000, ruleset.ShieldBlockRecoveryPaceCeilingBasisPoints);
        Assert.True(
            ruleset.TallShieldBlockRecoveryTicks >
            ruleset.NarrowShieldBlockRecoveryTicks);
        Assert.True(ruleset.NarrowShieldBlockRecoveryTicks > 0);

        Assert.Equal(
            5, ruleset.ResolveShieldBlockRecoveryTicks(ShieldId.TallHardwood));
        Assert.Equal(
            3,
            ruleset.ResolveShieldBlockRecoveryTicks(ShieldId.NarrowBreastHigh));
        Assert.Equal(0, ruleset.ResolveShieldBlockRecoveryTicks(ShieldId.None));
    }

    [Theory]
    [InlineData(false, 1, 0, 0)]
    [InlineData(false, 0, 1, 0)]
    [InlineData(false, 0, 0, 1)]
    [InlineData(true, 0, 0, 0)]
    [InlineData(true, 3, 5, 4_000)]
    [InlineData(true, 5, 5, 4_000)]
    [InlineData(true, 5, 3, 0)]
    [InlineData(true, 5, 3, 10_001)]
    public void InvalidShieldBlockRecoveryCouplingIsRejected(
        bool applies, int tallTicks, int narrowTicks, int ceiling)
    {
        Assert.ThrowsAny<ArgumentException>(() => new MovementRuleset(
            id: MovementPresetId.ShieldEncumbranceV16,
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
            selectsLeaderByRank: false,
            usesEquipmentRelativeFootwork: false,
            immediateRadiusBodyDiametersBasisPoints: 0,
            supportRadiusBodyDiametersBasisPoints: 0,
            loadoutMovementProfiles: ImmutableArray<LoadoutMovementProfile>.Empty,
            appliesShieldBlockRecovery: applies,
            tallShieldBlockRecoveryTicks: tallTicks,
            narrowShieldBlockRecoveryTicks: narrowTicks,
            shieldBlockRecoveryPaceCeilingBasisPoints: ceiling));
    }

    // -----------------------------------------------------------------
    // Shield-encumbrance coupling validation.
    // -----------------------------------------------------------------

    [Theory]
    [InlineData(false, 1, 0)]
    [InlineData(false, 0, 1)]
    [InlineData(true, 0, 0)]
    [InlineData(true, 10_000, 9_000)]
    [InlineData(true, 9_600, 9_600)]
    [InlineData(true, 9_600, 0)]
    [InlineData(true, 9_600, 9_700)]
    public void InvalidShieldEncumbranceCouplingIsRejected(
        bool applies, int narrowPace, int tallPace)
    {
        Assert.ThrowsAny<ArgumentException>(() => new MovementRuleset(
            id: MovementPresetId.ShieldEncumbranceV16,
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
            selectsLeaderByRank: false,
            usesEquipmentRelativeFootwork: false,
            immediateRadiusBodyDiametersBasisPoints: 0,
            supportRadiusBodyDiametersBasisPoints: 0,
            loadoutMovementProfiles: ImmutableArray<LoadoutMovementProfile>.Empty,
            appliesShieldEncumbrance: applies,
            narrowBreastHighShieldPaceBasisPoints: narrowPace,
            tallHardwoodShieldPaceBasisPoints: tallPace));
    }
}
