using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Pins the solo Wasay (<c>WA</c>) equipment-relative movement row: its exact
/// shipped values, its equipment-only loadout key, its canonical position at
/// index 1 of
/// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/>, and the
/// calibration range every one of its fields is approved to move inside.
/// Every value asserted here is a provisional reconstruction: gameplay tuning;
/// no historical measurement. The evidence behind the row, and the reasoning
/// for each figure, lives in docs/research/movement/wasay.md and in the
/// calibration table of docs/archives/2026-07-31/movement/wasay.md
/// section 5. Nothing in this file is a claim about how a
/// sixteenth-century warrior actually moved.
/// </summary>
public sealed class WasayMovementProfileTests
{
    /// <summary>
    /// The canonical loadout index of the solo Wasay row in the
    /// <c>KP, WA, KA, IT, KS, IS</c> order that
    /// <see cref="MovementRuleset.LoadoutMovementProfiles"/> is validated to
    /// hold.
    /// </summary>
    private const int CanonicalWasayIndex = 1;

    /// <summary>
    /// The equipment key of the row: a two-handed Wasay with light organic
    /// armor and no shield. Rank is deliberately omitted, because the profile
    /// key is equipment only.
    /// </summary>
    private static readonly CombatLoadout WasayLoadout = new(
        WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None);

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

    /// <summary>
    /// Every scalar of the shipped row, asserted one field at a time.
    /// Provisional reconstruction: gameplay tuning; no historical
    /// measurement. The opponent-distance offsets run in canonical opponent
    /// order <c>KP, WA, KA, IT, KS, IS</c>.
    /// </summary>
    [Fact]
    public void WasayProfileUsesApprovedProvisionalValues()
    {
        var row = WasayMovementProfile.Row;

        Assert.Equal(9_400, row.ForwardPaceBasisPoints);
        Assert.Equal(7_400, row.LateralPaceBasisPoints);
        Assert.Equal(6_400, row.BackwardPaceBasisPoints);
        Assert.Equal(2_500, row.CommittedPaceBasisPoints);
        Assert.Equal(10_800, row.PreferredDistanceBasisPoints);
        Assert.Equal(
            LoadoutMovementProfile.OpponentDistanceOffsetCount,
            row.OpponentDistanceOffsetBasisPoints.Length);
        Assert.Equal(
            new[] { 500, 0, 250, 500, 250, 500 },
            row.OpponentDistanceOffsetBasisPoints);
        Assert.Equal(1, row.MaximumFacingStepsPerTick);
        Assert.Equal(1, row.CommittedFacingStepsPerTick);
        Assert.Equal(4_000, row.AccelerationBasisPointsPerTick);
        Assert.Equal(5_000, row.DecelerationBasisPointsPerTick);
        Assert.Equal(4, row.CommitmentTicks);
        Assert.Equal(4, row.RecoveryTicks);
        Assert.Equal(17_500, row.AllyClearanceBodyDiametersBasisPoints);
        Assert.Equal(20_000, row.DisengageEnemyToAllyBasisPoints);
        Assert.Equal(12_500, row.ReengageEnemyToAllyBasisPoints);
        Assert.Equal(10_000, row.PursuitSupportBodyDiametersBasisPoints);
    }

    /// <summary>
    /// The row is keyed by equipment alone: a two-handed Wasay carried with
    /// light organic armor and no shield.
    /// </summary>
    [Fact]
    public void WasayProfileExportsApprovedLoadoutKey()
    {
        var row = WasayMovementProfile.Row;

        Assert.Equal(WasayLoadout, row.Loadout);
        Assert.Equal(WeaponId.Wasay, row.Loadout.Weapon);
        Assert.Equal(ArmorId.LightOrganic, row.Loadout.Armor);
        Assert.Equal(ShieldId.None, row.Loadout.Shield);
    }

    /// <summary>
    /// Rank is social standing with no movement meaning, so two Wasay
    /// loadouts differing only in their rank resolve to the very same profile
    /// instance under the shipped preset — not merely to two equal copies of
    /// it.
    /// </summary>
    [Theory]
    [InlineData(RankId.Timawa)]
    [InlineData(RankId.Maharlika)]
    [InlineData(RankId.Datu)]
    public void EveryRankResolvesToTheSameWasayProfileInstance(RankId rank)
    {
        var ranked = new CombatLoadout(
            WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None, rank);

        var resolved = V6.ResolveLoadoutProfile(ranked);

        Assert.Same(WasayMovementProfile.Row, resolved);
    }

    /// <summary>
    /// The Wasay is two-handed and never pairs with a shield, so that key
    /// carries no profile row at all. It has to fail loudly rather than
    /// silently inherit the solo Wasay footwork.
    /// </summary>
    [Fact]
    public void AShieldedWasayLoadoutHasNoProfileAndThrows()
    {
        var shielded = new CombatLoadout(
            WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.TallHardwood);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => V6.ResolveLoadoutProfile(shielded));
    }

    /// <summary>
    /// The row occupies canonical position 1 of the shipped preset, which is
    /// the position <see cref="MovementRuleset.ResolveLoadoutProfile"/> indexes
    /// into for a solo Wasay.
    /// </summary>
    [Fact]
    public void TheShippedPresetCarriesTheWasayRowAtCanonicalIndexOne()
    {
        Assert.Same(
            WasayMovementProfile.Row,
            V6.LoadoutMovementProfiles[CanonicalWasayIndex]);
        Assert.Same(
            WasayMovementProfile.Row,
            V6.ResolveLoadoutProfile(WasayLoadout));
    }

    /// <summary>
    /// The previous shipped default preset carries no profile rows at all, so
    /// the Wasay row reaches the simulation only when the
    /// equipment-relative preset is selected. Resolution under the older
    /// preset throws instead of returning a default row.
    /// </summary>
    [Fact]
    public void TheWasayRowIsUnreachableUnderTheEarlierDefaultPreset()
    {
        var earlier = MovementPresetRegistry.Get(
            MovementPresetId.PersistentContingentsV4);

        Assert.False(earlier.UsesEquipmentRelativeFootwork);
        Assert.Empty(earlier.LoadoutMovementProfiles);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => earlier.ResolveLoadoutProfile(WasayLoadout));
    }

    /// <summary>
    /// Every scalar of the row sits inside the calibration range approved in
    /// the table of docs/archives/2026-07-31/movement/wasay.md section 5.
    /// A later retuning pass that moves a value outside its approved range
    /// fails here by name,
    /// rather than quietly shipping a row nobody agreed to. Both bounds are
    /// inclusive, and every range is a gameplay-tuning decision rather than a
    /// historical measurement.
    /// </summary>
    [Theory]
    [InlineData(nameof(LoadoutMovementProfile.ForwardPaceBasisPoints), 9_000, 9_800)]
    [InlineData(nameof(LoadoutMovementProfile.LateralPaceBasisPoints), 6_500, 8_200)]
    [InlineData(nameof(LoadoutMovementProfile.BackwardPaceBasisPoints), 5_500, 7_200)]
    [InlineData(nameof(LoadoutMovementProfile.CommittedPaceBasisPoints), 2_000, 3_500)]
    [InlineData(
        nameof(LoadoutMovementProfile.PreferredDistanceBasisPoints), 10_000, 12_000)]
    [InlineData(nameof(LoadoutMovementProfile.MaximumFacingStepsPerTick), 1, 2)]
    [InlineData(nameof(LoadoutMovementProfile.CommittedFacingStepsPerTick), 1, 1)]
    [InlineData(
        nameof(LoadoutMovementProfile.AccelerationBasisPointsPerTick), 3_000, 6_000)]
    [InlineData(
        nameof(LoadoutMovementProfile.DecelerationBasisPointsPerTick), 3_500, 7_000)]
    [InlineData(nameof(LoadoutMovementProfile.CommitmentTicks), 3, 5)]
    [InlineData(nameof(LoadoutMovementProfile.RecoveryTicks), 3, 5)]
    [InlineData(
        nameof(LoadoutMovementProfile.AllyClearanceBodyDiametersBasisPoints),
        15_000,
        20_000)]
    [InlineData(
        nameof(LoadoutMovementProfile.DisengageEnemyToAllyBasisPoints),
        15_000,
        20_000)]
    [InlineData(
        nameof(LoadoutMovementProfile.ReengageEnemyToAllyBasisPoints),
        10_000,
        15_000)]
    [InlineData(
        nameof(LoadoutMovementProfile.PursuitSupportBodyDiametersBasisPoints),
        8_000,
        12_500)]
    public void EveryWasayScalarSitsInsideItsApprovedCalibrationRange(
        string field, int lowestApproved, int highestApproved) =>
        Assert.InRange(ScalarByName(field), lowestApproved, highestApproved);

    /// <summary>
    /// Every signed opponent-distance offset cell sits inside the approved
    /// range of the same table, one case per canonical opponent.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EveryWasayOffsetCellSitsInsideItsApprovedCalibrationRange(
        int cell) =>
        Assert.InRange(
            WasayMovementProfile.Row.OpponentDistanceOffsetBasisPoints[cell],
            -2_000,
            2_000);

    /// <summary>
    /// Reads one scalar of the shipped row by property name, so the range
    /// table above names the field it constrains and a failing case reports
    /// that name.
    /// </summary>
    private static int ScalarByName(string field)
    {
        var row = WasayMovementProfile.Row;
        return field switch
        {
            nameof(LoadoutMovementProfile.ForwardPaceBasisPoints) =>
                row.ForwardPaceBasisPoints,
            nameof(LoadoutMovementProfile.LateralPaceBasisPoints) =>
                row.LateralPaceBasisPoints,
            nameof(LoadoutMovementProfile.BackwardPaceBasisPoints) =>
                row.BackwardPaceBasisPoints,
            nameof(LoadoutMovementProfile.CommittedPaceBasisPoints) =>
                row.CommittedPaceBasisPoints,
            nameof(LoadoutMovementProfile.PreferredDistanceBasisPoints) =>
                row.PreferredDistanceBasisPoints,
            nameof(LoadoutMovementProfile.MaximumFacingStepsPerTick) =>
                row.MaximumFacingStepsPerTick,
            nameof(LoadoutMovementProfile.CommittedFacingStepsPerTick) =>
                row.CommittedFacingStepsPerTick,
            nameof(LoadoutMovementProfile.AccelerationBasisPointsPerTick) =>
                row.AccelerationBasisPointsPerTick,
            nameof(LoadoutMovementProfile.DecelerationBasisPointsPerTick) =>
                row.DecelerationBasisPointsPerTick,
            nameof(LoadoutMovementProfile.CommitmentTicks) => row.CommitmentTicks,
            nameof(LoadoutMovementProfile.RecoveryTicks) => row.RecoveryTicks,
            nameof(LoadoutMovementProfile.AllyClearanceBodyDiametersBasisPoints) =>
                row.AllyClearanceBodyDiametersBasisPoints,
            nameof(LoadoutMovementProfile.DisengageEnemyToAllyBasisPoints) =>
                row.DisengageEnemyToAllyBasisPoints,
            nameof(LoadoutMovementProfile.ReengageEnemyToAllyBasisPoints) =>
                row.ReengageEnemyToAllyBasisPoints,
            nameof(LoadoutMovementProfile.PursuitSupportBodyDiametersBasisPoints) =>
                row.PursuitSupportBodyDiametersBasisPoints,
            _ => throw new ArgumentOutOfRangeException(
                nameof(field),
                field,
                "The calibration range table names a property the Wasay row " +
                "does not carry."),
        };
    }
}
