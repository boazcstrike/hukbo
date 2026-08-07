using Hukbo.Core.Movement.Profiles;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The relationship between the two exported Kalis rows — solo <c>KA</c> and
/// shielded <c>KS</c>. A shield changes distance, pace, and timing, never turn
/// rate, and it never buys speed. Every asserted number is a <b>Provisional
/// reconstruction: gameplay tuning; no historical measurement</b>, drawn from
/// <c>docs/research/movement/kalis.md</c> section 7 and materialised by
/// <c>docs/archives/2026-08-07/2026-07-30-weapon-movement-foundation-design.md</c>
/// section 13.
/// </summary>
/// <remarks>
/// The literal values of both rows are pinned by
/// <c>MovementProfileRegistrationTests</c>, and the identity link proving that
/// those registered rows are the very instances the profile files export is
/// pinned by <see cref="MovementProfileRowContractTests"/> — along with the
/// loadout key, the rank independence, the hysteresis, and the effective
/// preferred distance of every canonical row. Task K1 asserted all of that
/// again here for the two Kalis rows alone; what is left is what only a
/// comparison between the two rows can say.
/// </remarks>
public sealed class KalisMovementProfileTests
{
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

}
