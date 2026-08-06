using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The Kampilan-owned pin for the solo Kampilan (<c>KP</c>) movement row.
/// Every value asserted here is a provisional reconstruction: gameplay tuning;
/// no historical measurement. The evidence ledger and the approved calibration
/// ranges live in docs/research/movement/kampilan.md; the row itself is fixed
/// by docs/plans/2026-07-30-weapon-movement-foundation-design.md section 13,
/// which supersedes any figure quoted in
/// docs/archives/2026-07-31/movement/kampilan.md.
/// </summary>
/// <remarks>
/// The registry-side equivalent of the value assertion already exists as
/// <c>MovementProfileRegistrationTests.TheKampilanRowCarriesTheDesignSectionThirteenValues</c>,
/// which reads the row through <c>LoadoutMovementProfiles[0]</c>. This file is
/// the equipment-owned pin named by
/// docs/archives/2026-07-31/movement/kampilan.md task K1,
/// and it asserts the exported <see cref="KampilanMovementProfile.Row"/>
/// directly so that a change to the export is caught in the file the Kampilan
/// session owns. The overlap is deliberate, not accidental duplication.
/// </remarks>
public sealed class KampilanMovementProfileTests
{
    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(MovementPresetId.EquipmentRelativeFootworkV6);

    /// <summary>
    /// Plan section 5, every scalar and every signed opponent-offset cell.
    /// The offsets run in canonical opponent order <c>KP, WA, KA, IT, KS,
    /// IS</c>. Provisional reconstruction: gameplay tuning; no historical
    /// measurement.
    /// </summary>
    [Fact]
    public void KampilanProfileUsesApprovedProvisionalValues()
    {
        var row = KampilanMovementProfile.Row;

        Assert.Equal(9_800, row.ForwardPaceBasisPoints);
        Assert.Equal(8_200, row.LateralPaceBasisPoints);
        Assert.Equal(7_000, row.BackwardPaceBasisPoints);
        Assert.Equal(3_000, row.CommittedPaceBasisPoints);
        Assert.Equal(11_500, row.PreferredDistanceBasisPoints);
        Assert.Equal(
            new[] { 0, 0, 250, 500, 250, 500 },
            row.OpponentDistanceOffsetBasisPoints);
        Assert.Equal(2, row.MaximumFacingStepsPerTick);
        Assert.Equal(1, row.CommittedFacingStepsPerTick);
        Assert.Equal(5_000, row.AccelerationBasisPointsPerTick);
        Assert.Equal(6_000, row.DecelerationBasisPointsPerTick);
        Assert.Equal(3, row.CommitmentTicks);
        Assert.Equal(3, row.RecoveryTicks);
        Assert.Equal(15_000, row.AllyClearanceBodyDiametersBasisPoints);
        Assert.Equal(20_000, row.DisengageEnemyToAllyBasisPoints);
        Assert.Equal(12_500, row.ReengageEnemyToAllyBasisPoints);
        Assert.Equal(12_500, row.PursuitSupportBodyDiametersBasisPoints);
    }

    /// <summary>
    /// The exported key is exactly <c>CombatLoadout(Kampilan, LightOrganic,
    /// None)</c>. The profile is keyed on equipment alone, so the stored key
    /// always carries <see cref="RankId.Timawa"/> whatever rank a caller
    /// supplies.
    /// </summary>
    [Fact]
    public void KampilanProfileExportsApprovedLoadoutKey()
    {
        var key = KampilanMovementProfile.Row.Loadout;

        Assert.Equal(WeaponId.Kampilan, key.Weapon);
        Assert.Equal(ArmorId.LightOrganic, key.Armor);
        Assert.Equal(ShieldId.None, key.Shield);
        Assert.Equal(RankId.Timawa, key.Rank);
    }

    /// <summary>
    /// Plan task K1 step 1: there is no Kampilan plus tall-hardwood fallback.
    /// The current combat configuration treats Kampilan as two-handed, so that
    /// key is unmapped by construction and must fail loudly rather than
    /// inherit another row's footwork. Whether Hukbo should keep the
    /// two-handed restriction is a separate product question this plan does
    /// not decide.
    /// </summary>
    [Fact]
    public void NoKampilanTallHardwoodRowResolves()
    {
        var shielded = new CombatLoadout(
            WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.TallHardwood);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => V6.ResolveLoadoutProfile(shielded));
    }

    /// <summary>
    /// The registry composes the exported row itself rather than a copy of it,
    /// so resolution under V6 returns the same instance this file pins. Rank
    /// is ignored by the resolver, so a Datu Kampilan resolves to the same
    /// row as a Timawa one.
    /// </summary>
    [Fact]
    public void TheRegisteredKampilanRowIsTheExportedRow()
    {
        var row = KampilanMovementProfile.Row;
        var datu = new CombatLoadout(
            WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None,
            RankId.Datu);

        Assert.Same(row, V6.LoadoutMovementProfiles[0]);
        Assert.Same(row, V6.ResolveLoadoutProfile(row.Loadout));
        Assert.Same(row, V6.ResolveLoadoutProfile(datu));
    }
}
