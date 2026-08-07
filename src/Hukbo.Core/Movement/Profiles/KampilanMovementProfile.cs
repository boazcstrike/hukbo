using Hukbo.Core.Combat;

namespace Hukbo.Core.Movement.Profiles;

/// <summary>
/// The solo Kampilan (<c>KP</c>) equipment-relative movement row, canonical
/// loadout index 0. Every value is a provisional reconstruction: gameplay
/// tuning; no historical measurement. Owned by the Kampilan session from now
/// on — any later retuning of this row belongs to that session and, once the
/// V6 digest ships, requires appending a new preset version rather than
/// editing this one. Values are the single authority of
/// docs/archives/2026-08-07/2026-07-30-weapon-movement-foundation-design.md section 13,
/// superseding any figure quoted in the weapon plans.
/// </summary>
public static class KampilanMovementProfile
{
    /// <summary>
    /// Provisional reconstruction: gameplay tuning; no historical
    /// measurement. The opponent-distance offsets run in canonical opponent
    /// order <c>KP, WA, KA, IT, KS, IS</c>.
    /// </summary>
    public static LoadoutMovementProfile Row { get; } = new(
        new CombatLoadout(
            WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None),
        forwardPaceBasisPoints: 9_800,
        lateralPaceBasisPoints: 8_200,
        backwardPaceBasisPoints: 7_000,
        committedPaceBasisPoints: 3_000,
        preferredDistanceBasisPoints: 11_500,
        opponentDistanceOffsetBasisPoints: [0, 0, 250, 500, 250, 500],
        maximumFacingStepsPerTick: 2,
        committedFacingStepsPerTick: 1,
        accelerationBasisPointsPerTick: 5_000,
        decelerationBasisPointsPerTick: 6_000,
        commitmentTicks: 3,
        recoveryTicks: 3,
        allyClearanceBodyDiametersBasisPoints: 15_000,
        disengageEnemyToAllyBasisPoints: 20_000,
        reengageEnemyToAllyBasisPoints: 12_500,
        pursuitSupportBodyDiametersBasisPoints: 12_500);
}
