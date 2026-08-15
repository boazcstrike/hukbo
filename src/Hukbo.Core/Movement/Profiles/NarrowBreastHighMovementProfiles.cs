using Hukbo.Core.Combat;

namespace Hukbo.Core.Movement.Profiles;

/// <summary>
/// The two narrow-breast-high-shield equipment-relative movement rows:
/// shielded Kalis (<c>KS</c>, canonical loadout index 6) and shielded Itak
/// (<c>IS</c>, canonical loadout index 7). Registered only by
/// <see cref="MovementPresetId.ShieldEncumbranceV16"/>, the first movement
/// preset to carry eight canonical rows instead of six
/// (<see cref="MovementRuleset.ExtendedCanonicalLoadoutCount"/>). Every value
/// is a provisional reconstruction: gameplay tuning; no historical
/// measurement, under CLAUDE.md section 7. The shield itself — a breast-high
/// board a little more than half a <c>vara</c> wide — is Documented, form
/// uncertain (<c>docs/research/HISTORICAL_1500s_ARMOR.md:682, 780-781</c>);
/// the pace figures below claim no such standing.
/// </summary>
/// <remarks>
/// The four pace fields on both rows are chosen to sit strictly between the
/// corresponding solo row
/// (<see cref="KalisMovementProfile.Row"/>, <see cref="ItakMovementProfile.Row"/>)
/// and the corresponding slowed tall-shield row
/// (<see cref="TallHardwoodMovementProfiles.KalisRowV14"/>,
/// <see cref="TallHardwoodMovementProfiles.ItakRowV14"/>), so that the
/// shield-size design's pace ordering
/// <c>solo &gt; narrow-shield &gt; tall-shield</c>
/// (2026-08-15 shield-projectile-block design, section 6.1) holds for every
/// one of the four fields, on both weapons, with a visible margin at each
/// step. The remaining twelve fields on each row are linearly interpolated
/// between the same solo row and
/// <see cref="TallHardwoodMovementProfiles.KalisRow"/> or
/// <see cref="TallHardwoodMovementProfiles.ItakRow"/> — the shipped tall-shield
/// row, not the slowed V14 one, because a narrow shield's spacing and
/// footwork-timing behaviour is a physical consequence of carrying a smaller
/// board relative to carrying none at all, not a consequence of this preset's
/// deliberately exaggerated tall-shield pace penalty. <c>CommitmentTicks</c>
/// and <c>RecoveryTicks</c> are the one exception: both interpolate to the
/// solo row's own duration rather than a fractional tick count, because a
/// duration cannot be fractional and the smaller board is judged not to
/// extend either timer the way the broad board does.
/// </remarks>
public static class NarrowBreastHighMovementProfiles
{
    /// <summary>
    /// The shielded Kalis (<c>KS</c>) row, canonical loadout index 6.
    /// Provisional reconstruction: gameplay tuning; no historical measurement.
    /// The opponent-distance offsets run in canonical opponent order
    /// <c>KP, WA, KA, IT, KS, IS</c> — the original six-opponent columns; this
    /// preset does not extend
    /// <see cref="LoadoutMovementProfile.OpponentDistanceOffsetCount"/>, so
    /// neither narrow-shield loadout gets its own offset column.
    /// </summary>
    public static LoadoutMovementProfile KalisRow { get; } = new(
        new CombatLoadout(
            WeaponId.Kalis, ArmorId.LightOrganic, ShieldId.NarrowBreastHigh),
        forwardPaceBasisPoints: 9_550,
        lateralPaceBasisPoints: 8_650,
        backwardPaceBasisPoints: 7_150,
        committedPaceBasisPoints: 3_100,
        preferredDistanceBasisPoints: 12_500,
        opponentDistanceOffsetBasisPoints: [-375, -125, 125, 375, 125, 375],
        maximumFacingStepsPerTick: 2,
        committedFacingStepsPerTick: 1,
        accelerationBasisPointsPerTick: 5_800,
        decelerationBasisPointsPerTick: 6_500,
        commitmentTicks: 2,
        recoveryTicks: 2,
        allyClearanceBodyDiametersBasisPoints: 13_000,
        disengageEnemyToAllyBasisPoints: 16_250,
        reengageEnemyToAllyBasisPoints: 11_000,
        pursuitSupportBodyDiametersBasisPoints: 11_250);

    /// <summary>
    /// The shielded Itak (<c>IS</c>) row, canonical loadout index 7.
    /// Provisional reconstruction: gameplay tuning; no historical measurement.
    /// The opponent-distance offsets run in canonical opponent order
    /// <c>KP, WA, KA, IT, KS, IS</c>, for the same reason
    /// <see cref="KalisRow"/>'s do.
    /// </summary>
    public static LoadoutMovementProfile ItakRow { get; } = new(
        new CombatLoadout(
            WeaponId.Itak, ArmorId.LightOrganic, ShieldId.NarrowBreastHigh),
        forwardPaceBasisPoints: 9_850,
        lateralPaceBasisPoints: 9_000,
        backwardPaceBasisPoints: 7_600,
        committedPaceBasisPoints: 3_750,
        preferredDistanceBasisPoints: 10_500,
        opponentDistanceOffsetBasisPoints: [-625, -375, -125, 125, -125, 125],
        maximumFacingStepsPerTick: 2,
        committedFacingStepsPerTick: 1,
        accelerationBasisPointsPerTick: 6_750,
        decelerationBasisPointsPerTick: 7_500,
        commitmentTicks: 2,
        recoveryTicks: 2,
        allyClearanceBodyDiametersBasisPoints: 12_500,
        disengageEnemyToAllyBasisPoints: 13_750,
        reengageEnemyToAllyBasisPoints: 10_500,
        pursuitSupportBodyDiametersBasisPoints: 9_000);
}
