using Hukbo.Core.Combat;

namespace Hukbo.Core.Movement.Profiles;

/// <summary>
/// The two tall-hardwood-shield equipment-relative movement rows: shielded
/// Kalis (<c>KS</c>, canonical loadout index 4) and shielded Itak (<c>IS</c>,
/// canonical loadout index 5). Every value is a provisional reconstruction:
/// gameplay tuning; no historical measurement. Owned by the Tall Hardwood
/// session from now on — any later retuning of these rows belongs to that
/// session and, once the V6 digest ships, requires appending a new preset
/// version rather than editing this one. Values are the single authority of
/// the 2026-07-30 weapon-relative movement shared foundation design section 13,
/// superseding any figure quoted in the weapon plans; in particular the
/// shielded Kalis preferred distance is 13,000 and the shielded disengage
/// bands are 17,500 and 15,000, and shield rows carry no facing penalty —
/// both turn at the solo value of 2 (design sections 6.6 and 13.3).
/// </summary>
public static class TallHardwoodMovementProfiles
{
    /// <summary>
    /// The shielded Kalis (<c>KS</c>) row. Provisional reconstruction:
    /// gameplay tuning; no historical measurement. The opponent-distance
    /// offsets run in canonical opponent order <c>KP, WA, KA, IT, KS, IS</c>.
    /// </summary>
    public static LoadoutMovementProfile KalisRow { get; } = new(
        new CombatLoadout(
            WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood),
        forwardPaceBasisPoints: 9_400,
        lateralPaceBasisPoints: 8_400,
        backwardPaceBasisPoints: 6_700,
        committedPaceBasisPoints: 3_000,
        preferredDistanceBasisPoints: 13_000,
        opponentDistanceOffsetBasisPoints: [-250, 0, 250, 500, 0, 250],
        maximumFacingStepsPerTick: 2,
        committedFacingStepsPerTick: 1,
        accelerationBasisPointsPerTick: 5_600,
        decelerationBasisPointsPerTick: 6_000,
        commitmentTicks: 3,
        recoveryTicks: 3,
        allyClearanceBodyDiametersBasisPoints: 14_000,
        disengageEnemyToAllyBasisPoints: 17_500,
        reengageEnemyToAllyBasisPoints: 11_000,
        pursuitSupportBodyDiametersBasisPoints: 10_000);

    /// <summary>
    /// The shielded Itak (<c>IS</c>) row. Provisional reconstruction:
    /// gameplay tuning; no historical measurement. The opponent-distance
    /// offsets run in canonical opponent order <c>KP, WA, KA, IT, KS, IS</c>.
    /// Note that its reengage band of 11,000 sits above solo Itak's 10,000,
    /// so the shielded loadout leaves disengagement at higher enemy pressure
    /// than the solo one (design section 13.3).
    /// </summary>
    public static LoadoutMovementProfile ItakRow { get; } = new(
        new CombatLoadout(
            WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood),
        forwardPaceBasisPoints: 9_700,
        lateralPaceBasisPoints: 8_700,
        backwardPaceBasisPoints: 7_100,
        committedPaceBasisPoints: 3_500,
        preferredDistanceBasisPoints: 10_000,
        opponentDistanceOffsetBasisPoints: [-500, -250, 0, 250, -250, 0],
        maximumFacingStepsPerTick: 2,
        committedFacingStepsPerTick: 1,
        accelerationBasisPointsPerTick: 6_500,
        decelerationBasisPointsPerTick: 7_000,
        commitmentTicks: 3,
        recoveryTicks: 3,
        allyClearanceBodyDiametersBasisPoints: 13_500,
        disengageEnemyToAllyBasisPoints: 15_000,
        reengageEnemyToAllyBasisPoints: 11_000,
        pursuitSupportBodyDiametersBasisPoints: 8_000);

    /// <summary>
    /// The shielded Kalis (<c>KS</c>) row used only by
    /// <see cref="MovementPresetId.ShieldEncumbranceV16"/>, canonical loadout
    /// index 4 there as in every earlier equipment-relative preset.
    /// Provisional reconstruction: gameplay tuning; no historical measurement.
    /// It is a distinct row from <see cref="KalisRow"/>, not an edit to it:
    /// V6 through V13 keep registering <see cref="KalisRow"/> unchanged, and
    /// only this preset's registry entry substitutes this row in its place, so
    /// no earlier preset's content hash or frozen trajectory digest moves. Its
    /// four pace fields are lowered below <see cref="KalisRow"/>'s so that the
    /// shield-size design's pace ordering
    /// <c>solo &gt; narrow-shield &gt; tall-shield</c>
    /// (2026-08-15 shield-projectile-block design, section 6.1) holds with a
    /// visible margin against
    /// <see cref="Movement.Profiles.NarrowBreastHighMovementProfiles.KalisRow"/>;
    /// every other field is carried forward from <see cref="KalisRow"/>
    /// unchanged, because the design only asks the broad shield to slow down,
    /// not to reposition or re-time its footwork.
    /// </summary>
    public static LoadoutMovementProfile KalisRowV14 { get; } = new(
        new CombatLoadout(
            WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.TallHardwood),
        forwardPaceBasisPoints: 9_000,
        lateralPaceBasisPoints: 8_000,
        backwardPaceBasisPoints: 6_300,
        committedPaceBasisPoints: 2_700,
        preferredDistanceBasisPoints: 13_000,
        opponentDistanceOffsetBasisPoints: [-250, 0, 250, 500, 0, 250],
        maximumFacingStepsPerTick: 2,
        committedFacingStepsPerTick: 1,
        accelerationBasisPointsPerTick: 5_600,
        decelerationBasisPointsPerTick: 6_000,
        commitmentTicks: 3,
        recoveryTicks: 3,
        allyClearanceBodyDiametersBasisPoints: 14_000,
        disengageEnemyToAllyBasisPoints: 17_500,
        reengageEnemyToAllyBasisPoints: 11_000,
        pursuitSupportBodyDiametersBasisPoints: 10_000);

    /// <summary>
    /// The shielded Itak (<c>IS</c>) row used only by
    /// <see cref="MovementPresetId.ShieldEncumbranceV16"/>, canonical loadout
    /// index 5 there as in every earlier equipment-relative preset.
    /// Provisional reconstruction: gameplay tuning; no historical measurement.
    /// It is a distinct row from <see cref="ItakRow"/>, not an edit to it, for
    /// the same reason <see cref="KalisRowV14"/> is distinct from
    /// <see cref="KalisRow"/>: V6 through V13 keep registering
    /// <see cref="ItakRow"/> unchanged. Its four pace fields are lowered below
    /// <see cref="ItakRow"/>'s so the pace ordering holds against
    /// <see cref="Movement.Profiles.NarrowBreastHighMovementProfiles.ItakRow"/>
    /// with a visible margin; every other field is carried forward from
    /// <see cref="ItakRow"/> unchanged.
    /// </summary>
    public static LoadoutMovementProfile ItakRowV14 { get; } = new(
        new CombatLoadout(
            WeaponId.Itak, ArmorId.LightOrganic, ShieldId.TallHardwood),
        forwardPaceBasisPoints: 9_300,
        lateralPaceBasisPoints: 8_300,
        backwardPaceBasisPoints: 6_700,
        committedPaceBasisPoints: 3_200,
        preferredDistanceBasisPoints: 10_000,
        opponentDistanceOffsetBasisPoints: [-500, -250, 0, 250, -250, 0],
        maximumFacingStepsPerTick: 2,
        committedFacingStepsPerTick: 1,
        accelerationBasisPointsPerTick: 6_500,
        decelerationBasisPointsPerTick: 7_000,
        commitmentTicks: 3,
        recoveryTicks: 3,
        allyClearanceBodyDiametersBasisPoints: 13_500,
        disengageEnemyToAllyBasisPoints: 15_000,
        reengageEnemyToAllyBasisPoints: 11_000,
        pursuitSupportBodyDiametersBasisPoints: 8_000);
}
