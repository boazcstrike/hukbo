using Hukbo.Core.Combat;

namespace Hukbo.Core.Movement.Profiles;

/// <summary>
/// The solo Wasay (<c>WA</c>) equipment-relative movement row, canonical
/// loadout index 1. Every value is a provisional reconstruction: gameplay
/// tuning; no historical measurement. Owned by the Wasay session from now
/// on — any later retuning of this row belongs to that session and, once the
/// V6 digest ships, requires appending a new preset version rather than
/// editing this one. Values are the single authority of
/// docs/plans/2026-07-30-weapon-movement-foundation-design.md section 13,
/// superseding any figure quoted in the weapon plans.
/// </summary>
public static class WasayMovementProfile
{
    /// <summary>
    /// Provisional reconstruction: gameplay tuning; no historical
    /// measurement. The opponent-distance offsets run in canonical opponent
    /// order <c>KP, WA, KA, IT, KS, IS</c>.
    /// </summary>
    public static LoadoutMovementProfile Row { get; } = new(
        new CombatLoadout(
            WeaponId.Wasay, ArmorId.LightOrganic, ShieldId.None),
        forwardPaceBasisPoints: 9_400,
        lateralPaceBasisPoints: 7_400,
        backwardPaceBasisPoints: 6_400,
        committedPaceBasisPoints: 2_500,
        preferredDistanceBasisPoints: 10_800,
        opponentDistanceOffsetBasisPoints: [500, 0, 250, 500, 250, 500],
        maximumFacingStepsPerTick: 1,
        committedFacingStepsPerTick: 1,
        accelerationBasisPointsPerTick: 4_000,
        decelerationBasisPointsPerTick: 5_000,
        commitmentTicks: 4,
        recoveryTicks: 4,
        allyClearanceBodyDiametersBasisPoints: 17_500,
        disengageEnemyToAllyBasisPoints: 20_000,
        reengageEnemyToAllyBasisPoints: 12_500,
        pursuitSupportBodyDiametersBasisPoints: 10_000);
}
