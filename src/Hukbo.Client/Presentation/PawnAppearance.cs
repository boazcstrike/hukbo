using Microsoft.Xna.Framework;

namespace Hukbo.Client.Presentation;

internal enum PawnWeaponRole
{
    GreatBlade,
    HeavyChopper,
    ThrustingBlade,
    Bolo,
}

internal enum PawnHeadTreatment
{
    CroppedHair,
    Headcloth,
    WrappedCloth,
}

internal readonly record struct PawnAppearance(
    PawnWeaponRole WeaponRole,
    float StatureMultiplier,
    float BuildMultiplier,
    PawnHeadTreatment HeadTreatment,
    Color ClothingColor,
    Color AccentColor,
    Color SkinColor,
    Color HeadTreatmentColor)
{
    // Player-facing labels use plain descriptors only. Per
    // docs/research/HISTORICAL_1500s_WEAPONS.md and CLAUDE.md section 7,
    // specific cultural identifications (Kampilan, Panabas, Kris) never
    // appear as an unqualified primary label; they surface only through
    // EvidenceNote, always marked PROVISIONAL.
    public string WeaponLabel =>
        WeaponRole switch
        {
            PawnWeaponRole.GreatBlade => "Great Blade",
            PawnWeaponRole.HeavyChopper => "Heavy Chopper",
            PawnWeaponRole.ThrustingBlade => "Thrusting Blade",
            PawnWeaponRole.Bolo => "Work Blade",
            _ => throw new ArgumentOutOfRangeException(
                nameof(WeaponRole),
                WeaponRole,
                null),
        };

    /// <summary>
    /// Provisional comparative name, if any. Always null or prefixed with
    /// "PROVISIONAL" — never presented as a confirmed historical
    /// identification.
    /// </summary>
    public string? EvidenceNote =>
        WeaponRole switch
        {
            PawnWeaponRole.GreatBlade =>
                "PROVISIONAL: comparable to Spanish-era accounts of the " +
                "kampilan.",
            PawnWeaponRole.HeavyChopper =>
                "PROVISIONAL: comparable to Spanish-era accounts of the " +
                "panabas.",
            PawnWeaponRole.ThrustingBlade =>
                "PROVISIONAL: comparable to Spanish-era accounts of the " +
                "kris.",
            PawnWeaponRole.Bolo => null,
            _ => throw new ArgumentOutOfRangeException(
                nameof(WeaponRole),
                WeaponRole,
                null),
        };
}
