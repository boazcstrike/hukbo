using Hukbo.Core.Combat;

namespace Hukbo.Core.Movement.Profiles;

/// <summary>
/// The solo Kalis (<c>KA</c>) equipment-relative movement row, canonical
/// loadout index 2. Every value is a provisional reconstruction: gameplay
/// tuning; no historical measurement. Owned by the Kalis session from now
/// on — any later retuning of this row belongs to that session and, once the
/// V6 digest ships, requires appending a new preset version rather than
/// editing this one. Values are the single authority of
/// docs/archives/2026-08-07/2026-07-30-weapon-movement-foundation-design.md section 13,
/// superseding any figure quoted in the weapon plans. The approved
/// calibration ranges these values sit inside, and the evidence they are
/// and are not derived from, are recorded in
/// docs/research/movement/kalis.md section 7; the task list that governs
/// this row is the kalis movement plan.
/// </summary>
public static class KalisMovementProfile
{
    /// <summary>
    /// Provisional reconstruction: gameplay tuning; no historical
    /// measurement. The opponent-distance offsets run in canonical opponent
    /// order <c>KP, WA, KA, IT, KS, IS</c>.
    /// </summary>
    public static LoadoutMovementProfile Row { get; } = new(
        new CombatLoadout(
            WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.None),
        forwardPaceBasisPoints: 9_700,
        lateralPaceBasisPoints: 8_900,
        backwardPaceBasisPoints: 7_600,
        committedPaceBasisPoints: 3_300,
        preferredDistanceBasisPoints: 12_000,
        opponentDistanceOffsetBasisPoints: [-500, -250, 0, 250, 250, 500],
        maximumFacingStepsPerTick: 2,
        committedFacingStepsPerTick: 1,
        accelerationBasisPointsPerTick: 6_000,
        decelerationBasisPointsPerTick: 7_000,
        commitmentTicks: 2,
        recoveryTicks: 2,
        allyClearanceBodyDiametersBasisPoints: 12_000,
        disengageEnemyToAllyBasisPoints: 15_000,
        reengageEnemyToAllyBasisPoints: 11_000,
        pursuitSupportBodyDiametersBasisPoints: 12_500);
}
