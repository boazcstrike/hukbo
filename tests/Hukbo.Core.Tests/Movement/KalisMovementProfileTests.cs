using Hukbo.Core.Combat;
using Hukbo.Core.Movement;
using Hukbo.Core.Movement.Profiles;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Task K1 of <c>docs/plans/movement/kalis.md</c>: the two exported Kalis
/// rows — solo <c>KA</c> and shielded <c>KS</c> — pinned at their source,
/// on the exported static property rather than through the registry, and
/// then proved to be the very instances the registry composes. Every
/// asserted number is a <b>Provisional reconstruction: gameplay tuning; no
/// historical measurement</b>, drawn from
/// <c>docs/research/movement/kalis.md</c> section 7 and materialised by
/// <c>docs/plans/2026-07-30-weapon-movement-foundation-design.md</c>
/// section 13.
/// </summary>
/// <remarks>
/// <c>MovementProfileRegistrationTests</c> pins the same values as they
/// appear inside the registered V6 ruleset. This file pins them one step
/// earlier, at the profile files the Kalis and Tall Hardwood sessions own,
/// and pins the identity link between the two: a row silently rebuilt with
/// equal values but a different instance, or a registry that stopped
/// consuming the owned file, fails here and nowhere else. The shielded row
/// lives in <c>TallHardwoodMovementProfiles.cs</c>, which the Tall Hardwood
/// session owns; this file only verifies it, exactly as the plan's task K1
/// steps 5 through 8 require.
/// </remarks>
public sealed class KalisMovementProfileTests
{
    private static readonly CombatLoadout SoloKalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout ShieldedKalis =
        new(WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood);

    /// <summary>The canonical loadout index of solo Kalis, <c>KA</c>.</summary>
    private const int SoloCanonicalIndex = 2;

    /// <summary>
    /// The canonical loadout index of Kalis plus Tall Hardwood, <c>KS</c>.
    /// </summary>
    private const int ShieldedCanonicalIndex = 4;

    private static MovementRuleset V6 =>
        MovementPresetRegistry.Get(
            MovementPresetId.EquipmentRelativeFootworkV6);

    // ----- The solo row (K1 steps 1 through 4) -----

    [Fact]
    public void TheSoloRowIsKeyedByTheCompleteKalisLoadout()
    {
        Assert.Equal(SoloKalis, KalisMovementProfile.Row.Loadout);
    }

    /// <summary>
    /// Every solo <c>KA</c> value, including both ratio thresholds and all
    /// six opponent-distance offsets in canonical opponent order
    /// <c>KP, WA, KA, IT, KS, IS</c>. Provisional reconstruction: gameplay
    /// tuning; no historical measurement.
    /// </summary>
    [Fact]
    public void TheSoloRowCarriesEveryApprovedValue()
    {
        var row = KalisMovementProfile.Row;

        Assert.Equal(9_700, row.ForwardPaceBasisPoints);
        Assert.Equal(8_900, row.LateralPaceBasisPoints);
        Assert.Equal(7_600, row.BackwardPaceBasisPoints);
        Assert.Equal(3_300, row.CommittedPaceBasisPoints);
        Assert.Equal(12_000, row.PreferredDistanceBasisPoints);
        Assert.Equal(
            new[] { -500, -250, 0, 250, 250, 500 },
            row.OpponentDistanceOffsetBasisPoints);
        Assert.Equal(2, row.MaximumFacingStepsPerTick);
        Assert.Equal(1, row.CommittedFacingStepsPerTick);
        Assert.Equal(6_000, row.AccelerationBasisPointsPerTick);
        Assert.Equal(7_000, row.DecelerationBasisPointsPerTick);
        Assert.Equal(2, row.CommitmentTicks);
        Assert.Equal(2, row.RecoveryTicks);
        Assert.Equal(12_000, row.AllyClearanceBodyDiametersBasisPoints);
        Assert.Equal(15_000, row.DisengageEnemyToAllyBasisPoints);
        Assert.Equal(11_000, row.ReengageEnemyToAllyBasisPoints);
        Assert.Equal(12_500, row.PursuitSupportBodyDiametersBasisPoints);
    }

    // ----- The shielded row, owned by the Tall Hardwood session -----

    [Fact]
    public void TheShieldedRowIsKeyedByTheCompleteShieldedKalisLoadout()
    {
        Assert.Equal(
            ShieldedKalis, TallHardwoodMovementProfiles.KalisRow.Loadout);
    }

    /// <summary>
    /// Every shielded <c>KS</c> value. The preferred distance is 13,000, not
    /// the stale "1.10 reach" sentence in this plan's evidence table; the
    /// foundation handoff records that correction and the code wins.
    /// Provisional reconstruction: gameplay tuning; no historical
    /// measurement.
    /// </summary>
    [Fact]
    public void TheShieldedRowCarriesEveryApprovedValue()
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
    /// The shield row carries no facing penalty: both Kalis rows turn at the
    /// same two ordinary sectors and the same one committed sector. A shield
    /// changes distance, pace, and timing, never turn rate.
    /// </summary>
    [Fact]
    public void TheShieldRowCarriesNoFacingPenalty()
    {
        Assert.Equal(
            KalisMovementProfile.Row.MaximumFacingStepsPerTick,
            TallHardwoodMovementProfiles.KalisRow.MaximumFacingStepsPerTick);
        Assert.Equal(
            KalisMovementProfile.Row.CommittedFacingStepsPerTick,
            TallHardwoodMovementProfiles.KalisRow
                .CommittedFacingStepsPerTick);
    }

    /// <summary>
    /// The shield never buys speed. Every pace band and the committed pace
    /// of the shielded row sit strictly below the solo row's, and the
    /// shielded row commits and recovers for strictly longer.
    /// </summary>
    [Fact]
    public void TheShieldedRowIsNeverFasterThanTheSoloRow()
    {
        var solo = KalisMovementProfile.Row;
        var shielded = TallHardwoodMovementProfiles.KalisRow;

        Assert.True(shielded.ForwardPaceBasisPoints < solo.ForwardPaceBasisPoints);
        Assert.True(shielded.LateralPaceBasisPoints < solo.LateralPaceBasisPoints);
        Assert.True(
            shielded.BackwardPaceBasisPoints < solo.BackwardPaceBasisPoints);
        Assert.True(
            shielded.CommittedPaceBasisPoints < solo.CommittedPaceBasisPoints);
        Assert.True(shielded.CommitmentTicks > solo.CommitmentTicks);
        Assert.True(shielded.RecoveryTicks > solo.RecoveryTicks);
    }

    // ----- Registry composition (K1 step 8) -----

    /// <summary>
    /// The registry composes the owned files rather than a private copy: the
    /// canonical <c>KA</c> and <c>KS</c> slots hold the very instances the
    /// profile files export.
    /// </summary>
    [Fact]
    public void TheRegistryComposesTheOwnedRowInstances()
    {
        Assert.Same(
            KalisMovementProfile.Row,
            V6.LoadoutMovementProfiles[SoloCanonicalIndex]);
        Assert.Same(
            TallHardwoodMovementProfiles.KalisRow,
            V6.LoadoutMovementProfiles[ShieldedCanonicalIndex]);
    }

    /// <summary>
    /// Both Kalis rows resolve under their exact complete
    /// <see cref="CombatLoadout"/> key, and the shielded key never falls
    /// back to the solo row.
    /// </summary>
    [Fact]
    public void BothKalisRowsResolveUnderTheirExactLoadoutKey()
    {
        Assert.Same(
            KalisMovementProfile.Row, V6.ResolveLoadoutProfile(SoloKalis));
        Assert.Same(
            TallHardwoodMovementProfiles.KalisRow,
            V6.ResolveLoadoutProfile(ShieldedKalis));
        Assert.NotSame(
            V6.ResolveLoadoutProfile(SoloKalis),
            V6.ResolveLoadoutProfile(ShieldedKalis));
    }

    /// <summary>
    /// Rank is social standing and carries no movement meaning: a Kalis
    /// warrior of any rank resolves the same row instance, for both the solo
    /// and the shielded key.
    /// </summary>
    [Theory]
    [InlineData(RankId.Datu)]
    [InlineData(RankId.Maharlika)]
    [InlineData(RankId.Timawa)]
    [InlineData(RankId.AlipingNamamahay)]
    [InlineData(RankId.Ayuey)]
    public void EveryKalisRankResolvesTheSameRows(RankId rank)
    {
        var solo = new CombatLoadout(
            WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None, rank);
        var shielded = new CombatLoadout(
            WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood, rank);

        Assert.Same(
            KalisMovementProfile.Row, V6.ResolveLoadoutProfile(solo));
        Assert.Same(
            TallHardwoodMovementProfiles.KalisRow,
            V6.ResolveLoadoutProfile(shielded));
    }

    // ----- The validator envelope both rows satisfy -----

    /// <summary>
    /// Hysteresis exists on both rows: the release threshold is strictly
    /// below the entry threshold, so no pair of counts can enter and leave
    /// disengagement on the same tick. The constructor enforces this; the
    /// assertion states which side of the boundary the Kalis rows chose.
    /// </summary>
    [Fact]
    public void BothRowsKeepTheReleaseStrictlyBelowTheEntry()
    {
        foreach (var row in KalisRows())
        {
            Assert.True(
                row.ReengageEnemyToAllyBasisPoints <
                    row.DisengageEnemyToAllyBasisPoints,
                $"{row.Loadout} has no disengagement hysteresis.");
        }
    }

    /// <summary>
    /// The committed pace never exceeds the forward pace on either row, so
    /// committing can only ever slow a Kalis warrior down.
    /// </summary>
    [Fact]
    public void CommittingNeverRaisesThePaceCeiling()
    {
        foreach (var row in KalisRows())
        {
            Assert.True(
                row.CommittedPaceBasisPoints <= row.ForwardPaceBasisPoints,
                $"{row.Loadout} speeds up while committed.");
        }
    }

    /// <summary>
    /// The six offset cells never cancel the preferred distance: against
    /// every canonical opponent, both rows keep a strictly positive
    /// effective preferred distance, and every cell stays inside the shared
    /// [-2000, 2000] envelope.
    /// </summary>
    [Fact]
    public void EveryOpponentOffsetLeavesAPositivePreferredDistance()
    {
        foreach (var row in KalisRows())
        {
            Assert.Equal(
                LoadoutMovementProfile.OpponentDistanceOffsetCount,
                row.OpponentDistanceOffsetBasisPoints.Length);

            foreach (var cell in row.OpponentDistanceOffsetBasisPoints)
            {
                Assert.InRange(cell, -2_000, 2_000);
                Assert.True(
                    row.PreferredDistanceBasisPoints + cell > 0,
                    $"{row.Loadout} cancels its preferred distance.");
            }
        }
    }

    private static LoadoutMovementProfile[] KalisRows() =>
    [
        KalisMovementProfile.Row,
        TallHardwoodMovementProfiles.KalisRow,
    ];
}
