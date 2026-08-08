namespace Sandata.Core.Weapons;

/// <summary>
/// Identifies one of the 38 firearms in the v0.1 roster: 24 rifles followed
/// by 14 pistols, in the order docs/research/2026-08-07-sandata-research-
/// consolidated.md section 3 lists them. Declaration only — task 22 authors
/// the <c>FirearmDefinition</c> row every member here indexes into.
/// </summary>
/// <remarks>
/// <b>Dense from zero, append-only.</b> Every numeric value is part of the
/// replay contract in exactly the sense <c>CLAUDE.md</c> section 5 states for
/// Hukbo and design section 4 states for Sandata: a saved replay, a golden
/// expectation, a recorded seed-1 baseline, and <c>FirearmCatalog</c>'s
/// content hash all name a weapon by this numeric value, not by its member
/// name. A member's value never changes once it has shipped, a member is
/// never reordered, and a retired value is never reused for a different
/// weapon. Renumbering — or adding a weapon anywhere but the end — is a new
/// preset version plus new golden expectations, never a routine edit.
/// <c>WeaponEnumTests</c> pins every value as a literal so an accidental
/// renumbering fails loudly.
/// </remarks>
public enum FirearmId
{
    // Rifles (24): AK pattern, AR pattern, and other service rifles.

    /// <summary>AK-47.</summary>
    Ak47 = 0,

    /// <summary>AKM.</summary>
    Akm = 1,

    /// <summary>AK-74M.</summary>
    Ak74M = 2,

    /// <summary>AK-12, 2018/2021 configuration.</summary>
    Ak12 = 3,

    /// <summary>AK-12, 2023 configuration. A distinct row from <see cref="Ak12"/>: the 2023 model deletes the two-round burst.</summary>
    Ak122023 = 4,

    /// <summary>AK-15.</summary>
    Ak15 = 5,

    /// <summary>M16A4.</summary>
    M16A4 = 6,

    /// <summary>M4. A distinct row from <see cref="M4A1"/>: this carries a three-round burst, not full auto.</summary>
    M4 = 7,

    /// <summary>M4A1. A distinct row from <see cref="M4"/>: this carries full auto, not a burst.</summary>
    M4A1 = 8,

    /// <summary>Mk 18 Mod 1.</summary>
    Mk18Mod1 = 9,

    /// <summary>M7.</summary>
    M7 = 10,

    /// <summary>XM8.</summary>
    Xm8 = 11,

    /// <summary>HK416 A5.</summary>
    Hk416A5 = 12,

    /// <summary>HK416F.</summary>
    Hk416F = 13,

    /// <summary>G36.</summary>
    G36 = 14,

    /// <summary>FN SCAR-L, Mk 16.</summary>
    Mk16ScarL = 15,

    /// <summary>FN SCAR-H, Mk 17.</summary>
    Mk17ScarH = 16,

    /// <summary>Steyr AUG A3. Bullpup mechanism group; no rotary selector.</summary>
    AugA3 = 17,

    /// <summary>IWI Tavor X95. Bullpup mechanism group.</summary>
    TavorX95 = 18,

    /// <summary>QBZ-191.</summary>
    Qbz191 = 19,

    /// <summary>QBZ-95-1. Bullpup mechanism group.</summary>
    Qbz951 = 20,

    /// <summary>L85A3. Bullpup mechanism group.</summary>
    L85A3 = 21,

    /// <summary>CZ BREN 2.</summary>
    CzBren2 = 22,

    /// <summary>Beretta ARX160.</summary>
    BerettaArx160 = 23,

    // Pistols (14).

    /// <summary>Beretta 92FS / M9.</summary>
    Beretta92Fs = 24,

    /// <summary>Beretta APX A1.</summary>
    BerettaApxA1 = 25,

    /// <summary>Glock 17 Gen5.</summary>
    Glock17Gen5 = 26,

    /// <summary>Glock 19 Gen5.</summary>
    Glock19Gen5 = 27,

    /// <summary>SIG Sauer M17.</summary>
    SigM17 = 28,

    /// <summary>SIG Sauer M18.</summary>
    SigM18 = 29,

    /// <summary>SIG Sauer P226.</summary>
    SigP226 = 30,

    /// <summary>Smith &amp; Wesson M&amp;P9 M2.0.</summary>
    SmithWessonMp9M20 = 31,

    /// <summary>Heckler &amp; Koch VP9.</summary>
    HkVp9 = 32,

    /// <summary>Heckler &amp; Koch USP.</summary>
    HkUsp = 33,

    /// <summary>CZ P-10 C.</summary>
    CzP10C = 34,

    /// <summary>Walther PDP, full-size 4-inch.</summary>
    WaltherPdpFs4 = 35,

    /// <summary>MP-443 Grach.</summary>
    Mp443Grach = 36,

    /// <summary>QSZ-92.</summary>
    Qsz92 = 37,
}
