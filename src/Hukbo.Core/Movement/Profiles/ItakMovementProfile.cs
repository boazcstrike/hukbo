using Hukbo.Core.Combat;

namespace Hukbo.Core.Movement.Profiles;

/// <summary>
/// The solo Itak (<c>IT</c>) equipment-relative movement row, canonical
/// loadout index 3. Every value is a provisional reconstruction: gameplay
/// tuning; no historical measurement. Owned by the Itak session from now
/// on — any later retuning of this row belongs to that session and, once the
/// V6 digest ships, requires appending a new preset version rather than
/// editing this one. Values are the single authority of
/// docs/archives/2026-08-07/2026-07-30-weapon-movement-foundation-design.md section 13,
/// superseding any figure quoted in the weapon plans. This row's forward
/// pace sits exactly at the inclusive 10,000 basis-point maximum, which is
/// why the profile's pace bound is inclusive at the top.
/// </summary>
public static class ItakMovementProfile
{
    /// <summary>
    /// Provisional reconstruction: gameplay tuning; no historical
    /// measurement. The opponent-distance offsets run in canonical opponent
    /// order <c>KP, WA, KA, IT, KS, IS</c>.
    /// </summary>
    public static LoadoutMovementProfile Row { get; } = new(
        new CombatLoadout(
            WeaponId.Itak, ArmorId.LightOrganic, ShieldId.None),
        forwardPaceBasisPoints: 10_000,
        lateralPaceBasisPoints: 9_300,
        backwardPaceBasisPoints: 8_100,
        committedPaceBasisPoints: 4_000,
        preferredDistanceBasisPoints: 11_000,
        opponentDistanceOffsetBasisPoints: [-750, -500, -250, 0, 0, 250],
        maximumFacingStepsPerTick: 2,
        committedFacingStepsPerTick: 1,
        accelerationBasisPointsPerTick: 7_000,
        decelerationBasisPointsPerTick: 8_000,
        commitmentTicks: 2,
        recoveryTicks: 2,
        allyClearanceBodyDiametersBasisPoints: 11_500,
        disengageEnemyToAllyBasisPoints: 12_500,
        reengageEnemyToAllyBasisPoints: 10_000,
        pursuitSupportBodyDiametersBasisPoints: 10_000);
}
