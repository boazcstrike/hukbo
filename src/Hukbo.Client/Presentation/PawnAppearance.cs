using Microsoft.Xna.Framework;

namespace Hukbo.Client.Presentation;

internal enum PawnWeaponRole
{
    Kampilan,
    Wasay,
    Kalis,
    Itak,
}

/// <summary>
/// Whether this warrior draws a shield beside the torso. Resolved from the
/// authoritative loadout, never from the entity ID.
/// </summary>
internal enum PawnShieldRole
{
    None,
    TallHardwood,
}

/// <summary>
/// How far the evidence behind a weapon's Filipino name actually reaches.
/// Shown in the agent inspector so a spectator can tell a contemporary
/// attestation from a later reconstruction.
/// </summary>
internal enum WeaponEvidenceTier
{
    /// <summary>
    /// A contemporary source records the term itself for this weapon class.
    /// </summary>
    Documented,

    /// <summary>
    /// The weapon class is attested in the period; the exact form, and the
    /// attachment of this particular name to it, are not.
    /// </summary>
    DocumentedFormUncertain,

    /// <summary>
    /// The name is a reasoned reconstruction that no consulted source
    /// confirms for the period.
    /// </summary>
    ProvisionalReconstruction,
}

internal enum PawnHeadTreatment
{
    CroppedHair,
    Headcloth,
    WrappedCloth,
}

internal readonly record struct PawnAppearance(
    PawnWeaponRole WeaponRole,
    PawnShieldRole ShieldRole,
    float StatureMultiplier,
    float BuildMultiplier,
    PawnHeadTreatment HeadTreatment,
    Color ClothingColor,
    Color AccentColor,
    Color SkinColor,
    Color HeadTreatmentColor)
{
    /// <summary>
    /// The pair-form label: the Filipino name, an em dash, and a plain
    /// English descriptor. The descriptor is what the game guarantees; the
    /// Filipino name is what the tradition offers. A cultural identification
    /// never appears bare and unqualified — see CLAUDE.md section 7.
    /// </summary>
    public string WeaponLabel =>
        WeaponRole switch
        {
            PawnWeaponRole.Kampilan => "Kampilan — Great Blade",
            PawnWeaponRole.Wasay => "Wasay — War Axe",
            PawnWeaponRole.Kalis => "Kalis — Thrusting Blade",
            PawnWeaponRole.Itak => "Itak — Work Blade",
            _ => throw new ArgumentOutOfRangeException(
                nameof(WeaponRole),
                WeaponRole,
                null),
        };

    public WeaponEvidenceTier EvidenceTier =>
        WeaponRole switch
        {
            PawnWeaponRole.Kalis => WeaponEvidenceTier.Documented,
            PawnWeaponRole.Kampilan or PawnWeaponRole.Wasay =>
                WeaponEvidenceTier.DocumentedFormUncertain,
            PawnWeaponRole.Itak =>
                WeaponEvidenceTier.ProvisionalReconstruction,
            _ => throw new ArgumentOutOfRangeException(
                nameof(WeaponRole),
                WeaponRole,
                null),
        };

    public string EvidenceTierLabel =>
        EvidenceTier switch
        {
            WeaponEvidenceTier.Documented => "Documented",
            WeaponEvidenceTier.DocumentedFormUncertain =>
                "Documented, form uncertain",
            WeaponEvidenceTier.ProvisionalReconstruction =>
                "Provisional reconstruction",
            _ => throw new ArgumentOutOfRangeException(
                nameof(EvidenceTier),
                EvidenceTier,
                null),
        };

    /// <summary>
    /// One line on what the evidence actually says, and where it stops.
    /// Spanish accounts are evidence about equipment, not neutral
    /// ethnography, and these notes are written to keep that visible.
    /// </summary>
    public string EvidenceNote =>
        WeaponRole switch
        {
            PawnWeaponRole.Kampilan =>
                "Pigafetta records a large cutting sword at Mactan in 1521 " +
                "but no local name for it; kampilan is attached to this " +
                "blade class by later tradition.",
            PawnWeaponRole.Wasay =>
                "A hafted battle axe attested among Tausug and Ibanag " +
                "groups. No sixteenth-century lexical attestation was " +
                "located.",
            PawnWeaponRole.Kalis =>
                "Pigafetta recorded calis in the Visayas in 1521, and the " +
                "term recurs across vocabularies from 1612 onward.",
            PawnWeaponRole.Itak =>
                "A Tagalog term for a field and utility blade also used in " +
                "fighting. The specific early attestation is unconfirmed.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(WeaponRole),
                WeaponRole,
                null),
        };

    public string ShieldLabel =>
        ShieldRole switch
        {
            PawnShieldRole.None => "None",
            PawnShieldRole.TallHardwood => "Tall Hardwood Shield",
            _ => throw new ArgumentOutOfRangeException(
                nameof(ShieldRole),
                ShieldRole,
                null),
        };

    /// <summary>
    /// Whether this warrior fights with the off hand free. A two-handed
    /// weapon is always solo and is never labelled as such, because it has no
    /// second form to be confused with.
    /// </summary>
    public bool CarriesShield => ShieldRole != PawnShieldRole.None;
}
