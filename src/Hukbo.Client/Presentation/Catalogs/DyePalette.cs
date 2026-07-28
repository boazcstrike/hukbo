using Microsoft.Xna.Framework;

namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// The category-J natural-material dye palette
/// (warrior-appearance-historical-research.md section 3, category J),
/// pinned to the research's swatch table as named constants
/// (R-W3.1, R-W3.8). Every garment-drawing component draws from this closed
/// set only; faction color never appears on a garment — it stays on the
/// ground ring and outline
/// (<see cref="Hukbo.Client.UI.FactionColorPalette"/>).
///
/// The research swatch table lists ten rows. Eight carry a single pinned hex
/// value and are the named constants below. The remaining two — the
/// skin-tone range and the tattoo tone shift — are explicitly a
/// "project-defined range" rather than a fixed swatch (research section 3,
/// category J: "Skin tones | project-defined range"; "Tattoo-darkened skin |
/// skin tone shifted darker/cooler"), so there is no single hex value to pin
/// for either. Both are out of this file's scope: the skin-tone range is
/// already <c>PawnAppearanceFactory</c>'s existing skin selection, and the
/// tattoo tone shift is R-W3.7, owned by VIS-019.
///
/// <see cref="Hukbo.Client.Presentation.ContrastEnvelope.MinimumFactionDyeDistance"/> (VIS-005)
/// requires every dye constant to keep at least 80 channel-distance units
/// from every fixed faction constant (R-W6.10) — the requirements
/// traceability table (implementation-plan-draft.md) splits R-W3.8 between
/// this task ("palette pins") and VIS-033 ("faction distance"), so this file
/// pins the exact research values and <c>AppearanceComponentCatalogTests</c>
/// records the distance check faithfully rather than asserting past it.
/// <see cref="GoldAccent"/> and <see cref="TurmericYellow"/> do not clear
/// that distance from <c>FactionColorPalette</c>'s third ("other faction")
/// pawn color (231, 199, 84): both are historically pinned gold/yellow
/// tones and the third faction constant is itself gold, so the shortfall is
/// structural, not a transcription error. Deciding how to resolve it —
/// restricting where these two tones may render, revisiting the third
/// faction constant, or accepting the gap — belongs to VIS-033, not this
/// file.
/// </summary>
internal static class DyePalette
{
    /// <summary>
    /// Undyed abaca/cotton cream, the default cloth state. <c>#E7D8B7</c>.
    /// Evidence tier: Documented (the default cloth state throughout the
    /// 1521-1609 accounts).
    /// </summary>
    public static readonly Color UndyedCream = new(231, 216, 183);

    /// <summary>
    /// Indigo blue, from <i>tayum</i>/<i>tagum</i> (Indigofera). <c>#354D6B</c>.
    /// Evidence tier: blue garments Documented (Morga); the indigo-plant
    /// identification is Documented, form uncertain.
    /// </summary>
    public static readonly Color IndigoBlue = new(53, 77, 107);

    /// <summary>
    /// Deep blue-black, saturated indigo or mud-tannin black. <c>#2A3140</c>.
    /// Evidence tier: Documented (black garments, Morga).
    /// </summary>
    public static readonly Color DeepBlueBlack = new(42, 49, 64);

    /// <summary>
    /// Sappan red, from <i>sibukaw</i> (sappanwood). <c>#8F3F35</c>.
    /// Evidence tier: red garments and red status cloth Documented; the
    /// plant identification is Documented, form uncertain.
    /// </summary>
    public static readonly Color SappanRed = new(143, 63, 53);

    /// <summary>
    /// Turmeric yellow, from <i>dilaw</i> (turmeric). <c>#C9A23F</c>.
    /// Evidence tier: Documented, form uncertain — attested in the dye
    /// vocabulary record. The research flags this swatch "use sparingly — no
    /// 1500s account puts yellow on a warrior"; it does not clear
    /// <see cref="Hukbo.Client.Presentation.ContrastEnvelope.MinimumFactionDyeDistance"/> against the
    /// third faction constant (see the class remarks).
    /// </summary>
    public static readonly Color TurmericYellow = new(201, 162, 63);

    /// <summary>
    /// Bark brown, from bark cloth or hide. <c>#7A5A3A</c>. Evidence tier:
    /// Documented (hide and bark armor descriptions).
    /// </summary>
    public static readonly Color BarkBrown = new(122, 90, 58);

    /// <summary>
    /// Gold accent, from the gold-ornament corpus. <c>#D0A64A</c>. Evidence
    /// tier: Documented. Does not clear
    /// <see cref="Hukbo.Client.Presentation.ContrastEnvelope.MinimumFactionDyeDistance"/> against the
    /// third faction constant (see the class remarks).
    /// </summary>
    public static readonly Color GoldAccent = new(208, 166, 74);

    /// <summary>
    /// Iron blue-black, blade metal, shared with the weapons palette.
    /// <c>#384249</c>. Evidence tier: Documented.
    /// </summary>
    public static readonly Color IronBlueBlack = new(56, 66, 73);

    /// <summary>
    /// Every named dye constant, for iteration by the palette-pin and
    /// faction-distance tests. Declaration order matches the research
    /// swatch table.
    /// </summary>
    public static IReadOnlyList<Color> All { get; } =
    [
        UndyedCream,
        IndigoBlue,
        DeepBlueBlack,
        SappanRed,
        TurmericYellow,
        BarkBrown,
        GoldAccent,
        IronBlueBlack,
    ];
}
