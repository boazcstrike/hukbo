namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// The research category an appearance component option belongs to
/// (warrior-appearance-historical-research.md section 3; R-W3.1). Category A
/// (stature/build) has no catalog entries here — it stays the existing
/// independent salted roll in
/// <see cref="Hukbo.Client.Presentation.PawnAppearanceFactory"/> and
/// asserts nothing historical (warrior-appearance-design.md, "Component
/// catalog"). Category J (the dye palette) is <see cref="DyePalette"/>, a
/// color set rather than a silhouette/scope component. Every other research
/// category (B through I, plus K) is represented below as of VIS-019.
/// </summary>
internal enum AppearanceComponentCategory
{
    /// <summary>Category B — hair.</summary>
    Hair,

    /// <summary>Category C — head covering.</summary>
    HeadCovering,

    /// <summary>Category D — torso garment.</summary>
    TorsoGarment,

    /// <summary>Category E — lower garment.</summary>
    LowerGarment,

    /// <summary>Category G — belt, sash, and waist wrapping.</summary>
    SashBelt,

    /// <summary>Category H — shoulder and waist accessories.</summary>
    Accessory,

    /// <summary>
    /// Category K — weathering and condition. Game presentation only; no
    /// historical claim (R-W3.11).
    /// </summary>
    Condition,

    /// <summary>
    /// Category F — armor layer, added by VIS-019. Appended after the
    /// milestone (VIS-017) categories rather than inserted alphabetically so
    /// no previously shipped member's ordinal shifts (R-W6.2's
    /// never-renumber discipline applies to enum ordinals the same way it
    /// applies to catalog <see cref="VisualCatalogEntry.Index"/> values).
    /// </summary>
    Armor,

    /// <summary>
    /// Category I — social and status adornment, added by VIS-019. Only the
    /// four renderable options (I1, I2, I4, I5) get catalog entries; I3, I6,
    /// I7, and I8 are inspector-text-only per the research and never render,
    /// so they have no <see cref="AppearanceComponentEntry"/> at all.
    /// </summary>
    Adornment,
}

/// <summary>
/// The one of three rendering channels R-W3.6 allows a pawn-scale appearance
/// component to draw through (warrior-appearance-design.md, "Component
/// catalog"). Fine detail beyond these three — motifs, embroidery, jewelry
/// detail — is inspector text only and never gets a render channel. There is
/// deliberately no fourth "motif" member: R-W3.7 confines tattooing (I1/I2)
/// to <see cref="AppearanceRenderChannel.ColorBlock"/> tone shift only, so
/// "no motif channel exists" is true by construction rather than by a
/// separate rule.
/// </summary>
internal enum AppearanceRenderChannel
{
    /// <summary>
    /// A silhouette change: a wedge, a bump, a disk, a line — geometry, not
    /// color. Includes the sash/belt line, the side-blade accent, and the
    /// banded-versus-skirted lower-garment distinction, all named as
    /// silhouette elements in warrior-appearance-design.md.
    /// </summary>
    Silhouette,

    /// <summary>
    /// A color choice over existing geometry: skin tone, garment dye
    /// (<see cref="DyePalette"/>), armor material tone, condition
    /// desaturation, tattoo tone shift.
    /// </summary>
    ColorBlock,

    /// <summary>
    /// A small accent mark. At most
    /// <see cref="AppearanceComponentCatalog.MaxAccentMarksPerPawn"/> render
    /// per pawn, at most
    /// <see cref="AppearanceComponentCatalog.MaxAccentPixelSizeAtApparentScale1"/>
    /// pixels each at apparent scale 1 (R-W3.6, "Area cap"); a recipe with
    /// more accent-channel components than the cap allows renders only the
    /// first two and leaves the rest as inspector text — that overflow
    /// selection is a later preset-composition task's concern, not this
    /// catalog's.
    /// </summary>
    Accent,
}

/// <summary>
/// One appearance component catalog entry: the shared
/// <see cref="VisualCatalogEntry"/> shape plus the domain fields the
/// warrior-appearance design adds by composition (R-W3.1, R-W3.6, R-W3.7) —
/// the research category the option belongs to, which of the three allowed
/// render channels it draws through, and — for the subset of category-F
/// armor entries whose render channel is a torso-widening silhouette — the
/// pinned width factor that widening may not exceed.
/// </summary>
internal sealed record AppearanceComponentEntry(
    VisualCatalogEntry Catalog,
    AppearanceComponentCategory Category,
    AppearanceRenderChannel RenderChannel,
    float? ArmorWidthFactor = null)
{
    // Positional-record property re-declarations, mirroring
    // VisualCatalogEntry's own pattern: each initializer runs once, against
    // the primary constructor parameter of the same name, and never re-runs
    // on a `with` expression (which uses the compiler-generated copy
    // constructor instead).
    public VisualCatalogEntry Catalog { get; init; } =
        Catalog ?? throw new ArgumentNullException(nameof(Catalog));

    public AppearanceComponentCategory Category { get; init; } = ValidateCategory(Category);

    public AppearanceRenderChannel RenderChannel { get; init; } = ValidateRenderChannel(RenderChannel);

    /// <summary>
    /// The torso-capsule width multiplier this armor entry contributes, or
    /// <see langword="null"/> for every entry that is not a torso-widening
    /// armor option (every non-<see cref="AppearanceComponentCategory.Armor"/>
    /// entry, and the <see cref="AppearanceComponentCategory.Armor"/> entries
    /// whose silhouette lives elsewhere — F5's head-disk cap does not widen
    /// the torso at all). When present the value is bounded to
    /// <c>[1.0, <see cref="AppearanceComponentCatalog.MaxArmorWidthFactor"/>]</c>
    /// (design line: "Armor capsule widening is bounded inside the existing
    /// build-multiplier envelope (width factor at most 1.18)") — the same
    /// 1.18 ceiling <see cref="Hukbo.Client.Presentation.PawnAppearanceFactory"/>'s
    /// existing build roll already uses as its own maximum, so armor never
    /// asks a pawn to read wider than an already-possible heavy build.
    /// PROVISIONAL: the *bound* is the design's pinned value; each entry's
    /// individual width factor below the bound is this package's own
    /// gameplay tuning choice, not a historical measurement (R-W3.11-style
    /// caveat extended to armor bulk).
    /// </summary>
    public float? ArmorWidthFactor { get; init; } = ValidateArmorWidthFactor(ArmorWidthFactor, Category);

    private static AppearanceComponentCategory ValidateCategory(AppearanceComponentCategory category)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "Category must be a defined AppearanceComponentCategory member.");
        }

        return category;
    }

    private static AppearanceRenderChannel ValidateRenderChannel(AppearanceRenderChannel renderChannel)
    {
        if (!Enum.IsDefined(renderChannel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(renderChannel),
                renderChannel,
                "Render channel must be a defined AppearanceRenderChannel member.");
        }

        return renderChannel;
    }

    private static float? ValidateArmorWidthFactor(
        float? armorWidthFactor,
        AppearanceComponentCategory category)
    {
        if (armorWidthFactor is null)
        {
            return null;
        }

        if (category != AppearanceComponentCategory.Armor)
        {
            throw new ArgumentException(
                "Armor width factor may only be set on Armor-category entries.",
                nameof(armorWidthFactor));
        }

        if (armorWidthFactor < 1f || armorWidthFactor > AppearanceComponentCatalog.MaxArmorWidthFactor)
        {
            throw new ArgumentOutOfRangeException(
                nameof(armorWidthFactor),
                armorWidthFactor,
                "Armor width factor must be between 1.0 and " +
                $"{AppearanceComponentCatalog.MaxArmorWidthFactor} inclusive.");
        }

        return armorWidthFactor;
    }
}

/// <summary>
/// The full category A-through-K appearance component catalog
/// (implementation-plan-draft.md, VIS-019), extending the VIS-017 milestone
/// subset to every option the research (warrior-appearance-historical-
/// research.md section 3) documents as safe to depict at pawn scale.
///
/// <b>Exclusions, and why each has no catalog entry:</b>
/// <list type="bullet">
/// <item>
/// <b>C2</b> (red head wrap of the proven warrior) is deliberately absent.
/// OD-5 (implementation-plan-draft.md, resolved 2026-07-28) keeps the earned
/// battle-honor mechanic out of this pass — it needs per-pawn kill-record
/// state this milestone does not have — and defers it to a backlog item in
/// <c>docs/plans/TODO.md</c>. C2 must ship in no roster.
/// </item>
/// <item>
/// <b>H3</b> (betel kit pouch) and <b>H4</b> (javelin bundle / back quiver,
/// already governed by the weapons document) are not renderable at pawn
/// scale — H3 explicitly ("invisible at pawn scale — inspector flavor text
/// only"), H4 because it belongs to the weapons catalog, not this one. Category
/// H therefore ships exactly two renderable entries (H1, H2), matching the
/// research's own "four accessory options (two renderable)" tally.
/// </item>
/// <item>
/// <b>I3</b> (facial tattooing), <b>I6</b> (gold armlets), <b>I7</b> (gold
/// dental work), and <b>I8</b> (tooth filing/blackening) are inspector-text
/// only per the research ("not renderable at pawn scale"; I3's sources are
/// also in direct conflict on whether the face was tattooed at all). Category
/// I therefore ships exactly four renderable entries (I1, I2, I4, I5).
/// </item>
/// </list>
///
/// <b>Planner inconsistency 4 (adornment renderable count), resolved:</b>
/// R-W3.1 tallies category I as "8, three renderable" while this catalog
/// ships four <see cref="AppearanceComponentEntry"/> records for it (I1, I2,
/// I4, I5). Both numbers are correct at once: the research's own reconciled
/// tally (warrior-appearance-historical-research.md section 3, category I,
/// closing paragraph — a correction of an earlier draft's wrong count, its
/// own finding RF-07) reads "four are renderable at pawn scale (I1, I2, I4,
/// and I5, where the two tattoo options I1 and I2 share a single
/// skin-tone-shift channel)". I1 and I2 both draw through the identical
/// <see cref="AppearanceRenderChannel.ColorBlock"/> tone-shift channel — I2
/// is I1's tone shift applied to a smaller area (arms/upper torso only,
/// staged coverage), not a distinct channel — so the category contributes
/// three distinct <em>render channels</em> (the shared tattoo tone shift,
/// I4's gold-ear accent, I5's gold-collar accent) across four <em>catalog
/// entries</em>. "8, three renderable" and "four entries" reconcile exactly
/// once I1/I2 are read as one channel shared by two entries, which is the
/// reading this catalog encodes.
///
/// <b>R-X.6 pair-form labels:</b> only three families clear the pair-form
/// bar in this package — <b>Putong — Head Wrap</b> (C1, C3), <b>Bahag —
/// Loincloth</b> (E1, E2), and <b>Chinina — Collarless Jacket</b> (D2, D3).
/// Every other new entry whose research write-up names a pending or
/// unconfirmed indigenous term (<i>kandit</i> for G1, <i>barote</i> for F2,
/// <i>panika</i> for I4, <i>kamagi</i> for I5) uses a plain-English label
/// instead and keeps the pending term out of every player-facing and
/// inspector-facing string in this file entirely — not merely out of the
/// label — matching the research's own fallback instruction for each of
/// those terms. The <i>salakot</i> term is not used anywhere in this file,
/// full stop (C4's own research entry explains why).
/// </summary>
internal static class AppearanceComponentCatalog
{
    /// <summary>
    /// The armor-widening ceiling R-W3.6 pins ("Armor capsule widening is
    /// bounded inside the existing build-multiplier envelope (width factor
    /// at most 1.18)", warrior-appearance-design.md). Equal to
    /// <see cref="Hukbo.Client.Presentation.PawnAppearanceFactory"/>'s own
    /// existing maximum build roll, so this ceiling reuses a bound the system
    /// already lives with rather than inventing a new one.
    /// </summary>
    public const float MaxArmorWidthFactor = 1.18f;

    /// <summary>
    /// At most this many <see cref="AppearanceRenderChannel.Accent"/>-channel
    /// components render per pawn (R-W3.6, "Area cap"). A recipe naming more
    /// accent components than this is legal to author (excess accents are
    /// inspector text only); which two actually render is a later
    /// preset-composition task's concern.
    /// </summary>
    public const int MaxAccentMarksPerPawn = 2;

    /// <summary>
    /// At apparent scale 1, an accent mark is at most this many pixels
    /// (R-W3.6, "Area cap").
    /// </summary>
    public const int MaxAccentPixelSizeAtApparentScale1 = 2;

    /// <summary>
    /// The signed per-channel offset R-W3.7's tattoo tone shift subtracts
    /// from a pawn's skin-tone color (each channel clamped to the byte range
    /// by whichever renderer applies it). <see cref="ToneShiftDelta.Red"/> is
    /// reduced more than <see cref="ToneShiftDelta.Blue"/> so the result
    /// reads relatively cooler as well as darker, matching the research's
    /// own "darker, cooler" description
    /// (category I, I1's "Safe to depict" note) and category J's "skin tone
    /// shifted darker/cooler" swatch-table row. PROVISIONAL: the research
    /// documents the practice and names the direction of the shift; it pins
    /// no specific numbers, so these three deltas are this package's own
    /// design-abstraction tuning choice, not a historical measurement
    /// (category J: "the tone-shift rendering is a design abstraction").
    /// This type, and the <see cref="TattooToneShift"/> value below, are what
    /// <see cref="DyePalette"/>'s own remarks mean by "the tattoo tone shift
    /// is R-W3.7, owned by VIS-019" — it is deliberately not a
    /// <see cref="DyePalette"/> member because it is an offset applied to the
    /// existing skin-tone range, not a fixed swatch.
    /// </summary>
    internal readonly record struct ToneShiftDelta(int Red, int Green, int Blue);

    /// <summary>
    /// The pinned tattoo tone-shift delta I1 and I2 both apply (I2 to a
    /// smaller area only — see the class remarks on planner inconsistency 4).
    /// </summary>
    public static readonly ToneShiftDelta TattooToneShift = new(Red: -36, Green: -30, Blue: -18);

    // ================= Category B — Hair (four options, all shipped) =================

    /// <summary>
    /// B1 — Long Hair, Knotted. Widely reported for Visayans and Tagalogs;
    /// treated as the default lowland style. Silhouette: a small round bump
    /// on the head disk. Must not generalize one knot style as
    /// pan-Philippine, or assign it gendered meaning at pawn scale.
    /// </summary>
    public static AppearanceComponentEntry HairB1LongHairKnotted { get; } = new(
        new VisualCatalogEntry(
            "appearance.hair.b1",
            0,
            "Long Hair, Knotted",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "1521-1609. Long hair gathered and fastened in a knot at the " +
            "crown or back of the head; everyday and battle wear alike. " +
            "Must not generalize one knot style as pan-Philippine or " +
            "assign it gendered meaning at pawn scale.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Hair,
        AppearanceRenderChannel.Silhouette);

    /// <summary>
    /// B2 — Long Loose Hair. Documented visually for the Boxer Codex
    /// Cagayan warrior plate, carrying spear and tall shield with loose
    /// hair — battle wear, not undress. Silhouette: a hair fringe extending
    /// slightly beyond the head disk. Scoped to Cagayan: the research is
    /// explicit that this is "a northern Luzon visual reference; do not make
    /// it the Visayan default", so this entry does not carry
    /// <see cref="VisualScopeTag.UnscopedGeneric"/>.
    /// </summary>
    public static AppearanceComponentEntry HairB2LongLooseHair { get; } = new(
        new VisualCatalogEntry(
            "appearance.hair.b2",
            1,
            "Long Loose Hair",
            VisualEvidenceTier.Documented,
            VisualScopeTag.Cagayan,
            "c. 1590 (Boxer Codex, Cagayan Warrior plate). Documented " +
            "visually on an armed warrior figure. Must not generalize: " +
            "this is a northern Luzon visual reference, not the Visayan " +
            "default.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Hair,
        AppearanceRenderChannel.Silhouette);

    /// <summary>
    /// B3 — Cropped Hair. Reported among some groups; regional distribution
    /// uncertain. Silhouette: a plain head disk with no knot bump. Must not
    /// generalize cropped hair as "commoner" and long hair as "elite" — the
    /// sources do not draw that line.
    /// </summary>
    public static AppearanceComponentEntry HairB3Cropped { get; } = new(
        new VisualCatalogEntry(
            "appearance.hair.b3",
            2,
            "Cropped Hair",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.UnscopedGeneric,
            "1582-1609 (Loarca). Hair cut to roughly shoulder length or " +
            "shorter. Must not generalize cropped hair as \"commoner\" and " +
            "long hair as \"elite\" — the sources do not draw that line.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Hair,
        AppearanceRenderChannel.Silhouette);

    /// <summary>
    /// B4 — Hair Fully Covered by Head Wrap. Hair braided or twisted and
    /// tucked entirely into a head wrap, so no hair silhouette shows: "head
    /// disk plus wrap wedge, no hair bump" — the research groups this with
    /// the bare-disk silhouette (B3/B4/C5), not with C1/C3's wedge, since the
    /// wedge itself is C1/C3's own contribution. This entry only ever
    /// appears alongside a C1 or C3 head-covering selection in a recipe
    /// (interaction of hair with C1, per the research); it carries
    /// <see cref="VisualScopeTag.UnscopedGeneric"/> for the same reason C1
    /// does (see that entry's remarks) rather than a single named region.
    /// </summary>
    public static AppearanceComponentEntry HairB4TuckedUnderWrap { get; } = new(
        new VisualCatalogEntry(
            "appearance.hair.b4",
            3,
            "Hair Tucked Under Head Wrap",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "Documented via the head wrap itself (see C1). Hair braided or " +
            "twisted and tucked entirely into the wrap; no hair silhouette " +
            "shows beneath it.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Hair,
        AppearanceRenderChannel.Silhouette);

    // ========= Category C — Head covering (six options, five shipped; C2 excluded, OD-5) =========

    /// <summary>
    /// C1 — Putong — Head Wrap (plain). The pair-form label clears R-X.6:
    /// Morga (1609) records <i>potong</i> as the Tagalog name, and the
    /// Visayan form <i>pudong</i> appears in the early Visayan lexical
    /// material Scott cites. Silhouette: a cloth-colored wedge or band
    /// across the head disk, in undyed cream, indigo, or black.
    /// <see cref="VisualScopeTag.UnscopedGeneric"/> here is a deliberate
    /// approximation: the research scopes this to "Visayas and Tagalog Luzon
    /// at minimum" — genuinely two regions, not one and not all four — and
    /// <see cref="VisualScopeTag"/> has no dual-region member. Tagging it
    /// exclusively Visayan (or exclusively Tagalog) would overclaim a single
    /// culture the sources do not support; tagging it
    /// <see cref="VisualScopeTag.UnscopedGeneric"/> is the more honest of the
    /// two imperfect choices, since that member's own definition is "not
    /// tied to one specific named culture" rather than "used everywhere".
    /// Keeping it out of the Cagayan and generic-levy blocks is enforced by
    /// which preset rows those blocks' rosters reference (implementation-
    /// plan-draft.md VIS-020 through VIS-023), not by this field.
    /// </summary>
    public static AppearanceComponentEntry HeadCoveringC1PutongPlain { get; } = new(
        new VisualCatalogEntry(
            "appearance.headCovering.c1",
            0,
            "Putong — Head Wrap",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "1521-1609 (Pigafetta; Loarca; Boxer Codex plates; Morga). A " +
            "narrow strip or kerchief of cloth wound tightly around the " +
            "head. Must not put it on every figure — bare heads (C5) are " +
            "equally documented.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.HeadCovering,
        AppearanceRenderChannel.Silhouette);

    // C2 (red head wrap of the proven warrior) is deliberately absent — see
    // the class remarks. Index 1 stays reserved for it, matching this
    // catalog's existing "gap for an unshipped research option" convention.

    /// <summary>
    /// C3 — Putong — Head Wrap (Gold-Edged). Head scarfs "edged with gold"
    /// on prominent men; Pigafetta's Humabon wore an embroidered scarf.
    /// Renders through <see cref="AppearanceRenderChannel.Accent"/> — the
    /// C1 wedge silhouette is this entry's shared base, and the gold edge is
    /// its own distinguishing contribution, matching the design's channel
    /// table which lists the wedge under Silhouette (C1/C3 together) and the
    /// gold edge separately under the Accent-marks list. Elite only; never
    /// on a slave-status figure (co-occurs with other category-I gold
    /// adornment, not with low status).
    /// </summary>
    public static AppearanceComponentEntry HeadCoveringC3PutongGoldEdged { get; } = new(
        new VisualCatalogEntry(
            "appearance.headCovering.c3",
            2,
            "Putong — Head Wrap (Gold-Edged)",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "1521-1582 (Pigafetta; Loarca). A one-pixel gold accent line on " +
            "the C1 wedge. Elite only; must not generalize to every figure " +
            "wearing a putong.",
            VisualDetailTier.High),
        AppearanceComponentCategory.HeadCovering,
        AppearanceRenderChannel.Accent);

    /// <summary>
    /// C4 — Woven Sun Hat. Everyday headgear is Documented, form uncertain;
    /// battlefield use is a Provisional reconstruction, which is the tier
    /// this entry pins because the catalog exists for pawns in battle.
    /// Silhouette: a wide flat disk over the head disk — visually loud, so
    /// this must stay a flavor option for levies, never elite figures. The
    /// now-familiar term <i>salakot</i> is not used anywhere in this file:
    /// its attestation in that form and spelling was not confirmed inside
    /// the research window, and the plain descriptor keeps the claim honest.
    /// </summary>
    public static AppearanceComponentEntry HeadCoveringC4WovenSunHat { get; } = new(
        new VisualCatalogEntry(
            "appearance.headCovering.c4",
            3,
            "Woven Sun Hat",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.UnscopedGeneric,
            "A broad woven palm or rattan hat against sun and rain; " +
            "hat-wearing is attested in the early lexical record, but no " +
            "1500s battle account in the window describes one. Levy " +
            "flavor only — never on elite figures (prohibition 5).",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.HeadCovering,
        AppearanceRenderChannel.Silhouette);

    /// <summary>
    /// C5 — Bare Head. No head covering; the hair silhouette from category B
    /// shows. Default head disk.
    /// </summary>
    public static AppearanceComponentEntry HeadCoveringC5BareHead { get; } = new(
        new VisualCatalogEntry(
            "appearance.headCovering.c5",
            4,
            "Bare Head",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "Boxer Codex plates include bare-headed armed figures; Morga " +
            "notes minimal covering generally.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.HeadCovering,
        AppearanceRenderChannel.Silhouette);

    /// <summary>
    /// C6 — Feathered Headdress (northern Luzon). The Boxer Codex Cagayan
    /// warrior wears a decorated headpiece with upright elements alongside
    /// long loose hair (B2). Silhouette: a small vertical accent above the
    /// head disk. Must never appear on a Visayan or Tagalog preset — the
    /// research calls this "the clearest cross-regional mashup hazard in the
    /// whole system".
    /// </summary>
    public static AppearanceComponentEntry HeadCoveringC6FeatheredHeaddress { get; } = new(
        new VisualCatalogEntry(
            "appearance.headCovering.c6",
            5,
            "Feathered Headdress",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Cagayan,
            "c. 1590 (Boxer Codex, Cagayan Warrior plate). A decorated " +
            "headpiece with upright elements. Must never appear on a " +
            "Visayan or Tagalog preset.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.HeadCovering,
        AppearanceRenderChannel.Silhouette);

    // ================= Category D — Torso garment (four options, all shipped) =================

    /// <summary>
    /// D1 — Bare-Chested. No upper garment; the single most common
    /// documented state for fighting men in the 1521 accounts. Renders as
    /// the base skin-tone torso fill, so it draws at Low tier already
    /// (warrior-appearance-design.md zoom table). Must not generalize
    /// bare-chested as low status — Humabon and Kolambu, both rulers, are
    /// described bare-chested with waist cloths and gold.
    /// </summary>
    public static AppearanceComponentEntry TorsoD1BareChested { get; } = new(
        new VisualCatalogEntry(
            "appearance.torso.d1",
            0,
            "Bare-Chested",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "1521-1609 (Pigafetta; Boxer Codex Visayan plates). Must not " +
            "generalize bare-chested as low status — Humabon and Kolambu, " +
            "both rulers, are described bare-chested with waist cloths " +
            "and gold.",
            VisualDetailTier.Low),
        AppearanceComponentCategory.TorsoGarment,
        AppearanceRenderChannel.ColorBlock);

    /// <summary>
    /// D2 — Chinina — Collarless Jacket (blue or black). The pair-form
    /// label clears R-X.6: Morga (1609) records both the garment and the
    /// name — a collarless jacket of <i>cangan</i> cloth, sewn in front,
    /// short-sleeved, reaching just past the waist, "some blue and some
    /// black". Scoped to Tagalog Luzon (Loarca's comparable Visayan garment
    /// is the separate D4 abaca jacket entry, not this one). Renders as an
    /// indigo or black torso capsule fill, folded in from Low tier like D1.
    /// Must not dress every faction in jackets — bare-chested is at least as
    /// common in the battle-relevant sources.
    /// </summary>
    public static AppearanceComponentEntry TorsoD2ChininaIndigoOrBlack { get; } = new(
        new VisualCatalogEntry(
            "appearance.torso.d2",
            1,
            "Chinina — Collarless Jacket",
            VisualEvidenceTier.Documented,
            VisualScopeTag.Tagalog,
            "c. 1590-1609 (Morga; Boxer Codex plates). A loose, hip-length, " +
            "short-sleeved jacket. Must not dress every faction in jackets " +
            "— bare-chested (D1) is at least as common in the sources.",
            VisualDetailTier.Low),
        AppearanceComponentCategory.TorsoGarment,
        AppearanceRenderChannel.ColorBlock);

    /// <summary>
    /// D3 — Chinina — Collarless Jacket (Red — Chiefly). Morga states that
    /// headmen wore red chininas. Sappan-red torso capsule fill; elite
    /// presets only. Must keep this Tagalog rule separate from the Visayan
    /// red honor mark (the C2 head wrap, itself excluded this pass) —
    /// merging the two systems is a cross-regional mashup.
    /// </summary>
    public static AppearanceComponentEntry TorsoD3ChininaRedChiefly { get; } = new(
        new VisualCatalogEntry(
            "appearance.torso.d3",
            2,
            "Chinina — Collarless Jacket (Red — Chiefly)",
            VisualEvidenceTier.Documented,
            VisualScopeTag.Tagalog,
            "1609 (Morga). Headmen wore red chininas. Elite presets only. " +
            "Must not merge with the separate Visayan red-head-wrap honor " +
            "system.",
            VisualDetailTier.Low),
        AppearanceComponentCategory.TorsoGarment,
        AppearanceRenderChannel.ColorBlock);

    /// <summary>
    /// D4 — Abaca Jacket (Visayan). Loarca's loose collarless jacket with
    /// tight sleeves whose skirts reach halfway down the leg, made of abaca
    /// gauze or colored silk. Plain-English label deliberately —
    /// <i>medriñaque</i> is a Spanish trade term, not a local garment name.
    /// Cream or pale-ochre torso capsule fill, slightly longer than D2's
    /// block. Must default to undyed abaca — the silk versions were elite
    /// trade goods.
    /// </summary>
    public static AppearanceComponentEntry TorsoD4AbacaJacket { get; } = new(
        new VisualCatalogEntry(
            "appearance.torso.d4",
            3,
            "Abaca Jacket",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "1582 (Loarca). A loose collarless jacket, tight sleeves, " +
            "skirts to mid-leg, abaca gauze or colored silk. Must default " +
            "to undyed abaca — silk versions were elite trade goods.",
            VisualDetailTier.Low),
        AppearanceComponentCategory.TorsoGarment,
        AppearanceRenderChannel.ColorBlock);

    // ================= Category E — Lower garment (three options, all shipped) =================

    /// <summary>
    /// E1 — Bahag, Loincloth (plain). Morga (1609) records <i>bahaque</i>;
    /// the term clears the pair-form bar. The universal male lower garment
    /// in the sources; nothing more to add — the safest single item in the
    /// system.
    /// </summary>
    public static AppearanceComponentEntry LowerGarmentE1Bahag { get; } = new(
        new VisualCatalogEntry(
            "appearance.lowerGarment.e1",
            0,
            "Bahag — Loincloth",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "1521-1609 (Pigafetta; Boxer Codex plates; Morga). A long " +
            "cloth wrapped around the waist and passed between the legs; " +
            "undyed cream default. Quality and dye marked status, the " +
            "garment itself did not.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.LowerGarment,
        AppearanceRenderChannel.Silhouette);

    /// <summary>
    /// E2 — Bahag — Loincloth (Richly Dyed). Morga describes the chief's
    /// bahag as richly dyed cloth, generally edged with gold; consistent
    /// with Pigafetta's 1521 description of Kolambu's embroidered waist
    /// cloth. Renders through <see cref="AppearanceRenderChannel.Accent"/> —
    /// the E1 band silhouette is this entry's shared base (the design's
    /// channel table lists "banded (E1/E2)" together under Silhouette), and
    /// the gold accent pixel is its own distinguishing contribution, listed
    /// separately under the Accent-marks list ("dyed-bahag gold pixel").
    /// Elite presets only. <see cref="VisualScopeTag.UnscopedGeneric"/>:
    /// documented for Tagalog chiefs specifically, but the research notes
    /// "elite dyed cloth is consistent across regions" and this catalog's
    /// own preset usage spans both the Visayan and Tagalog blocks, the same
    /// two-region situation as C1 (see that entry's remarks).
    /// </summary>
    public static AppearanceComponentEntry LowerGarmentE2DyedGoldEdged { get; } = new(
        new VisualCatalogEntry(
            "appearance.lowerGarment.e2",
            1,
            "Bahag — Loincloth (Richly Dyed)",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "1521-1609 (Pigafetta; Morga). The E1 band in deep red or " +
            "indigo with a gold accent pixel. Elite presets only.",
            VisualDetailTier.High),
        AppearanceComponentCategory.LowerGarment,
        AppearanceRenderChannel.Accent);

    /// <summary>
    /// E3 — Waist Cloth. Plain-English label — Pigafetta describes Kolambu
    /// covered from waist to knees by an embroidered cotton cloth, and Morga
    /// mentions a colored blanket wrapped at the waist, but no single
    /// indigenous term for this larger garment clears the pair-form bar in
    /// the research. Renders as a longer colored block at the capsule base,
    /// reading "skirted" rather than "banded" at pawn scale. Documented on
    /// rulers and prominent men only; must not read as a poor man's garment,
    /// and this catalog's own preset usage keeps it on leader presets (E1/E2
    /// are preferred for fighting presets, per the research's own guidance).
    /// </summary>
    public static AppearanceComponentEntry LowerGarmentE3WaistCloth { get; } = new(
        new VisualCatalogEntry(
            "appearance.lowerGarment.e3",
            2,
            "Waist Cloth",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "1521-1609 (Pigafetta; Morga). A larger cloth wrapped from " +
            "waist to knees, documented on rulers and prominent men. Must " +
            "not read as a poor man's garment; prefer E1/E2 on fighting " +
            "presets, E3 on leader presets.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.LowerGarment,
        AppearanceRenderChannel.Silhouette);

    // ================= Category F — Armor layer (five options, all shipped) =================

    /// <summary>
    /// F1 — Unarmored. The documented default: fighting in bahag, with or
    /// without a shield, exactly as Pigafetta's Mactan account (1521)
    /// describes. Renders no additional silhouette — categories A-E draw
    /// as-is — so this folds into the base torso fill at Low tier, mirroring
    /// D1's own pattern, with the baseline width factor (no widening at
    /// all). Must not generalize "unarmored at Mactan in 1521" into "never
    /// armored anywhere" — see F2 through F5.
    /// </summary>
    public static AppearanceComponentEntry ArmorF1Unarmored { get; } = new(
        new VisualCatalogEntry(
            "appearance.armor.f1",
            0,
            "Unarmored",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "1521 (Pigafetta, Mactan account). Fighting in bahag, with or " +
            "without a shield; the documented default. Must not " +
            "generalize into \"never armored anywhere\" — see F2-F5.",
            VisualDetailTier.Low),
        AppearanceComponentCategory.Armor,
        AppearanceRenderChannel.ColorBlock,
        ArmorWidthFactor: 1f);

    /// <summary>
    /// F2 — Corded Fiber Armor (Visayan). Plain-English label — Scott
    /// records the Visayan term <i>barote</i> for corded or quilted body
    /// armor, but this research could not independently confirm the term's
    /// attestation date inside the window, so the term stays out of every
    /// player- and inspector-facing string in this file. Body armor of
    /// thick braided abaca or bark cord, quilted or knotted tightly;
    /// Spaniards compared it to the cotton <i>escaupil</i> armor they knew
    /// from the Americas. Renders as a slightly widened torso capsule in
    /// pale ochre (PROVISIONAL width factor: the lightest of the four armor
    /// entries, since the research calls this "a minority kit", not a heavy
    /// covering). Must not issue it broadly — armor was a minority kit, and
    /// nothing supports uniform issue.
    /// </summary>
    public static AppearanceComponentEntry ArmorF2CordedFiberArmor { get; } = new(
        new VisualCatalogEntry(
            "appearance.armor.f2",
            1,
            "Corded Fiber Armor",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "1565-1576 (Legazpi-era relations); early Visayan lexical " +
            "record. Body armor of thick braided abaca or bark cord, " +
            "quilted or knotted. Must not issue it broadly — a minority " +
            "kit, not uniform issue.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Armor,
        AppearanceRenderChannel.Silhouette,
        ArmorWidthFactor: 1.06f);

    /// <summary>
    /// F3 — Hide Corselet. A Legazpi-era crown report describes men "well
    /// armed for Indians", with corselets of buffalo (carabao) hide. The
    /// quoted report concerns Luzon and this catalog's own preset usage is
    /// Tagalog-block only, though the research notes hide armor more broadly
    /// is described "without tight regional limits" — this entry is tagged
    /// to where it is actually deployed. Renders as a widened torso capsule
    /// in warm brown. Must not model it as heavy plate — it is stiffened
    /// hide over a bare or cloth-clad torso.
    /// </summary>
    public static AppearanceComponentEntry ArmorF3HideCorselet { get; } = new(
        new VisualCatalogEntry(
            "appearance.armor.f3",
            2,
            "Hide Corselet",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "1565-1576 (Legazpi-era relations, Blair and Robertson vols. " +
            "II-III). Corselets of carabao hide. Must not model as heavy " +
            "plate — stiffened hide over a bare or cloth-clad torso.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Armor,
        AppearanceRenderChannel.Silhouette,
        ArmorWidthFactor: 1.12f);

    /// <summary>
    /// F4 — Wooden Breastplate. The Legazpi-era relations mention wood
    /// corselets and bark elements alongside rope armor. The research treats
    /// the regional distribution as unspecific and rare; this catalog's own
    /// preset usage is Cagayan-block only, so this entry is tagged to where
    /// it is actually deployed. Renders as a widened capsule in charred-wood
    /// dark brown (PROVISIONAL width factor: the widest of the four armor
    /// entries, at the design's own 1.18 ceiling). Must not combine with
    /// invented European-style pauldrons or full coverage.
    /// </summary>
    public static AppearanceComponentEntry ArmorF4WoodenBreastplate { get; } = new(
        new VisualCatalogEntry(
            "appearance.armor.f4",
            3,
            "Wooden Breastplate",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Cagayan,
            "1565-1576 (Blair and Robertson vols. II-III). Wood corselets " +
            "and bark elements. Rare; must not combine with invented " +
            "European-style pauldrons or full coverage.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Armor,
        AppearanceRenderChannel.Silhouette,
        ArmorWidthFactor: MaxArmorWidthFactor);

    /// <summary>
    /// F5 — Shell-Set Helmet. The same Legazpi-era report describes helmets
    /// set with fish bones and stout shells said to resist anything but the
    /// arquebus. This entry does not widen the torso at all — it is a
    /// head-disk element, so <see cref="AppearanceComponentEntry.ArmorWidthFactor"/>
    /// stays <see langword="null"/> — but the design's channel table still
    /// places it under Silhouette ("pale head cap (F5)" / "shell-cap
    /// accent"), not Accent, so this entry follows that placement. Use on at
    /// most one or two presets — the research calls this "an exceptional
    /// item in a single report family".
    /// </summary>
    public static AppearanceComponentEntry ArmorF5ShellSetHelmet { get; } = new(
        new VisualCatalogEntry(
            "appearance.armor.f5",
            4,
            "Shell-Set Helmet",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "1565-1576 (Blair and Robertson vols. II-III). A helmet set " +
            "with fish bones and stout shells. Use on at most one or two " +
            "presets — an exceptional item in a single report family.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Armor,
        AppearanceRenderChannel.Silhouette);

    // ================= Category G — Belt, sash, and waist wrapping (three options, all shipped) =================

    /// <summary>
    /// G1 — Red Waist Sash (Visayan). Plain-English label per the
    /// research's own fallback instruction: Scott records the Visayan term
    /// <i>kandit</i>, but the research pins this "pending one review of the
    /// attestation chain" and instructs that the color rule, not the style
    /// name, is what should be endorsed for player-facing text until that
    /// review lands — so <i>kandit</i> stays out of every string in this
    /// file, the same treatment as F2's <i>barote</i> and category I's
    /// pending terms. Renders as a one-pixel red or indigo line across the
    /// capsule waist. Must not silently copy the earned-red rule documented
    /// for the head wrap (C2, excluded this pass) onto the sash — the
    /// sash's color symbolism is not established.
    /// </summary>
    public static AppearanceComponentEntry SashBeltG1RedWaistSash { get; } = new(
        new VisualCatalogEntry(
            "appearance.sashBelt.g1",
            0,
            "Red Waist Sash",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "Early Visayan lexical record; sash lines visible on Boxer " +
            "Codex figures. Must not copy the earned-red head-wrap rule " +
            "onto this sash — its color symbolism is not established.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.SashBelt,
        AppearanceRenderChannel.Silhouette);

    /// <summary>
    /// G2 — Cloth Belt. An undyed or dark waist tie securing the bahag and
    /// a dagger sheath; waist ties are ubiquitous in the Boxer Codex plates.
    /// </summary>
    public static AppearanceComponentEntry SashBeltG2ClothBelt { get; } = new(
        new VisualCatalogEntry(
            "appearance.sashBelt.g2",
            1,
            "Cloth Belt",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.UnscopedGeneric,
            "Documented visually (Boxer Codex plates); form uncertain. A " +
            "neutral waist line.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.SashBelt,
        AppearanceRenderChannel.Silhouette);

    /// <summary>
    /// G3 — Cord Belt. A belt of plaited plant cordage rather than cloth;
    /// cordage crafts are richly attested, but a cordage belt as warrior
    /// wear is inferred, not described. Keep this a minor variant.
    /// </summary>
    public static AppearanceComponentEntry SashBeltG3CordBelt { get; } = new(
        new VisualCatalogEntry(
            "appearance.sashBelt.g3",
            2,
            "Cord Belt",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.UnscopedGeneric,
            "Provisional reconstruction — cordage crafts are richly " +
            "attested, a cordage belt as warrior wear is inferred, not " +
            "described. An ochre waist line; keep this a minor variant.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.SashBelt,
        AppearanceRenderChannel.Silhouette);

    // ================= Category H — Shoulder and waist accessories (two renderable options) =================

    /// <summary>
    /// H1 — Sheathed Side Blade. A dagger or short blade in a wood or cloth
    /// sheath at the waist; Pigafetta describes gold-hilted daggers on
    /// Limasawa's ruler. This entry is the generic accessory-silhouette
    /// placement only — the player-facing pair-form label for the blade
    /// itself comes from whichever weapon catalog entry the loadout equips,
    /// not from this record. Restricted to Wasay-armed pawns in every
    /// preset that ships it (warrior-appearance-design.md), because the
    /// research scopes H1 to figures whose main weapon is not a blade. Gold
    /// hilts are elite-only (category I rules apply).
    /// </summary>
    public static AppearanceComponentEntry AccessoryH1SheathedSideBlade { get; } = new(
        new VisualCatalogEntry(
            "appearance.accessory.h1",
            0,
            "Sheathed Side Blade",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "1521-1569 (Pigafetta; Legazpi relations). A short dark accent " +
            "at the waist on figures whose main weapon is not the blade. " +
            "Gold hilts are elite-only.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Accessory,
        AppearanceRenderChannel.Silhouette);

    /// <summary>
    /// H2 — Draped Shoulder Cloth. A colored cloth or small blanket draped
    /// or slung over the shoulder or chest; drapes appear on Boxer Codex
    /// elite figures, and Morga describes colored blanket-cloths in Tagalog
    /// dress. Renders as a diagonal color stripe across the capsule. The
    /// research restricts this by social context (elite/leader presets),
    /// not by named region, and this catalog's own preset usage spans both
    /// the Visayan and Tagalog blocks — <see cref="VisualScopeTag.UnscopedGeneric"/>
    /// reflects that it makes no single-culture claim. Prefer on leader
    /// presets; sparing use elsewhere.
    /// </summary>
    public static AppearanceComponentEntry AccessoryH2DrapedShoulderCloth { get; } = new(
        new VisualCatalogEntry(
            "appearance.accessory.h2",
            1,
            "Draped Shoulder Cloth",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.UnscopedGeneric,
            "c. 1590-1609 (Boxer Codex elite figures; Morga). A colored " +
            "cloth draped or slung over the shoulder or chest. Prefer on " +
            "leader presets; sparing use elsewhere.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Accessory,
        AppearanceRenderChannel.Silhouette);

    // ================= Category I — Social and status adornment (four renderable options) =================

    /// <summary>
    /// I1 — Full-Body Tattoos (Visayan). Dense geometric tattooing covering
    /// torso, back, arms, and legs, leaving hands, feet, and generally the
    /// face bare — so defining that Spaniards called Visayans
    /// <i>Pintados</i>, "the painted ones". Renders as
    /// <see cref="TattooToneShift"/> applied to the full torso/limb skin
    /// tone. The single most important scope rule in the whole research
    /// document: Visayas only, never Tagalog, Cagayan, or generic presets.
    /// Never presented as decoration divorced from its earned meaning — tied
    /// to participation in raids and displays of courage, a readable war
    /// record on the skin. No motif channel exists (see
    /// <see cref="AppearanceRenderChannel"/>'s remarks) — motif-level detail
    /// is not attempted at pawn scale.
    /// </summary>
    public static AppearanceComponentEntry AdornmentI1FullBodyTattoos { get; } = new(
        new VisualCatalogEntry(
            "appearance.adornment.i1",
            0,
            "Full-Body Tattoos",
            VisualEvidenceTier.Documented,
            VisualScopeTag.Visayan,
            "1521-1604 (Pigafetta; Loarca; Boxer Codex; Chirino). Dense " +
            "body tattooing, earned and cumulative. Must never appear on " +
            "Tagalog, Cagayan, or generic presets; never presented as " +
            "decoration divorced from its earned meaning.",
            VisualDetailTier.Low),
        AppearanceComponentCategory.Adornment,
        AppearanceRenderChannel.ColorBlock);

    /// <summary>
    /// I2 — Partial Tattoos. The same <see cref="TattooToneShift"/> channel
    /// as I1, applied to arms and upper torso only (staged coverage growing
    /// with deeds, per the earned-coverage logic) — this is planner
    /// inconsistency 4's "shared skin-tone-shift channel", not a distinct
    /// render channel of its own. Same Visayas-only scope rule as I1.
    /// </summary>
    public static AppearanceComponentEntry AdornmentI2PartialTattoos { get; } = new(
        new VisualCatalogEntry(
            "appearance.adornment.i2",
            1,
            "Partial Tattoos",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "The I1 tone shift applied to arms/upper torso only; staged " +
            "coverage is attested in general terms, exact stages are not. " +
            "Same must-not-generalize rules as I1.",
            VisualDetailTier.Low),
        AppearanceComponentCategory.Adornment,
        AppearanceRenderChannel.ColorBlock);

    /// <summary>
    /// I4 — Gold Earrings. Plain-English label — the Visayan ornament
    /// vocabulary Scott records (for example <i>panika</i>) stays out of
    /// every player- and inspector-facing string in this file pending
    /// attestation review, the same treatment as G1's <i>kandit</i>. Large
    /// gold (and ivory) earrings, worn by men and women, documented from
    /// Pigafetta's first landfall onward; also object-documented (Ayala
    /// Museum "Gold of Ancestors" collection; Surigao Treasure). Renders as
    /// a single gold pixel beside the head disk. Archipelago-wide among
    /// lowland trading societies, matching
    /// <see cref="VisualScopeTag.UnscopedGeneric"/> cleanly. Density is
    /// status-graded; must never appear on a slave-status figure.
    /// </summary>
    public static AppearanceComponentEntry AdornmentI4GoldEarrings { get; } = new(
        new VisualCatalogEntry(
            "appearance.adornment.i4",
            3,
            "Gold Earrings",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "1521-1609 (Pigafetta; Loarca; Boxer Codex; Chirino; Morga); " +
            "also object-documented (Ayala Museum collection; Surigao " +
            "Treasure). Density is status-graded; must never appear on a " +
            "slave-status figure.",
            VisualDetailTier.High),
        AppearanceComponentCategory.Adornment,
        AppearanceRenderChannel.Accent);

    /// <summary>
    /// I5 — Gold Necklace. Plain-English label — the massive Visayan
    /// necklace type <i>kamagi</i> stays out of every player- and
    /// inspector-facing string in this file pending attestation review, the
    /// same treatment as I4's <i>panika</i>. Morga describes long chains of
    /// engraved gold links; Pigafetta saw necklaces on Cebu's ruler; the
    /// surviving pre-colonial gold corpus confirms the craft. Renders as a
    /// gold accent line at the capsule top. Archipelago-wide in trading
    /// polities, matching <see cref="VisualScopeTag.UnscopedGeneric"/>
    /// cleanly. Leaders and the wealthiest only.
    /// </summary>
    public static AppearanceComponentEntry AdornmentI5GoldNecklace { get; } = new(
        new VisualCatalogEntry(
            "appearance.adornment.i5",
            4,
            "Gold Necklace",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "1521-1609 (Pigafetta; Morga); object corpus is pre-1521 " +
            "archaeology (Ayala Museum collection). Leaders and the " +
            "wealthiest only.",
            VisualDetailTier.High),
        AppearanceComponentCategory.Adornment,
        AppearanceRenderChannel.Accent);

    // ================= Category K — Weathering and condition (five options, unchanged since VIS-017) =================

    /// <summary>
    /// K1 — Clean. The default condition; no visual modification. Game
    /// presentation, not history (R-W3.11) — never described to the player
    /// as historical detail.
    /// </summary>
    public static AppearanceComponentEntry ConditionK1Clean { get; } = new(
        new VisualCatalogEntry(
            "appearance.condition.k1",
            0,
            "Clean",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only default condition; no historical claim.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Condition,
        AppearanceRenderChannel.ColorBlock);

    /// <summary>
    /// K2 — Dusty / Muddy. Desaturates and darkens the lower garment and
    /// legs. Game presentation, not history (R-W3.11).
    /// </summary>
    public static AppearanceComponentEntry ConditionK2DustyMuddy { get; } = new(
        new VisualCatalogEntry(
            "appearance.condition.k2",
            1,
            "Dusty / Muddy",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Desaturate and darken " +
            "the lower garment and legs.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Condition,
        AppearanceRenderChannel.ColorBlock);

    /// <summary>
    /// K3 — Sweat Sheen. A slight highlight on bare skin; tropical
    /// plausibility only. Game presentation, not history (R-W3.11).
    /// </summary>
    public static AppearanceComponentEntry ConditionK3SweatSheen { get; } = new(
        new VisualCatalogEntry(
            "appearance.condition.k3",
            2,
            "Sweat Sheen",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. A slight highlight on " +
            "bare skin, tropical plausibility only.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Condition,
        AppearanceRenderChannel.ColorBlock);

    /// <summary>
    /// K4 — Faded Dye. Washed-out garment color on long-serving units. Game
    /// presentation, not history (R-W3.11).
    /// </summary>
    public static AppearanceComponentEntry ConditionK4FadedDye { get; } = new(
        new VisualCatalogEntry(
            "appearance.condition.k4",
            3,
            "Faded Dye",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Washed-out garment " +
            "color on long-serving units.",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Condition,
        AppearanceRenderChannel.ColorBlock);

    /// <summary>
    /// K5 — Battle-Worn. The combination of K2 and K4, plus disordered hair
    /// (the B1 knot rendered loosened, at High tier per
    /// warrior-appearance-design.md's zoom table). This entry pins the tone
    /// contribution's tier; the High-tier loosened-knot silhouette layering
    /// is rendering-implementation detail for a later task. Game
    /// presentation, not history (R-W3.11).
    /// </summary>
    public static AppearanceComponentEntry ConditionK5BattleWorn { get; } = new(
        new VisualCatalogEntry(
            "appearance.condition.k5",
            4,
            "Battle-Worn",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Combination of K2 and " +
            "K4 plus disordered hair (the B1 knot rendered loosened at " +
            "High tier).",
            VisualDetailTier.Medium),
        AppearanceComponentCategory.Condition,
        AppearanceRenderChannel.ColorBlock);

    /// <summary>
    /// Every appearance component entry, for iteration by the catalog
    /// tests. Declaration order matches the research's own category letter
    /// order (skipping A and J, neither of which has entries here): hair,
    /// head covering, torso, lower garment, armor, sash/belt, accessory,
    /// adornment, then condition.
    /// </summary>
    public static IReadOnlyList<AppearanceComponentEntry> All { get; } =
    [
        HairB1LongHairKnotted,
        HairB2LongLooseHair,
        HairB3Cropped,
        HairB4TuckedUnderWrap,
        HeadCoveringC1PutongPlain,
        HeadCoveringC3PutongGoldEdged,
        HeadCoveringC4WovenSunHat,
        HeadCoveringC5BareHead,
        HeadCoveringC6FeatheredHeaddress,
        TorsoD1BareChested,
        TorsoD2ChininaIndigoOrBlack,
        TorsoD3ChininaRedChiefly,
        TorsoD4AbacaJacket,
        LowerGarmentE1Bahag,
        LowerGarmentE2DyedGoldEdged,
        LowerGarmentE3WaistCloth,
        ArmorF1Unarmored,
        ArmorF2CordedFiberArmor,
        ArmorF3HideCorselet,
        ArmorF4WoodenBreastplate,
        ArmorF5ShellSetHelmet,
        SashBeltG1RedWaistSash,
        SashBeltG2ClothBelt,
        SashBeltG3CordBelt,
        AccessoryH1SheathedSideBlade,
        AccessoryH2DrapedShoulderCloth,
        AdornmentI1FullBodyTattoos,
        AdornmentI2PartialTattoos,
        AdornmentI4GoldEarrings,
        AdornmentI5GoldNecklace,
        ConditionK1Clean,
        ConditionK2DustyMuddy,
        ConditionK3SweatSheen,
        ConditionK4FadedDye,
        ConditionK5BattleWorn,
    ];
}
