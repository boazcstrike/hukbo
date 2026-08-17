using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// One armor variant catalog entry: the shared <see cref="VisualCatalogEntry"/>
/// shape plus the authoritative <see cref="ArmorId"/> baseline it dresses
/// (armor-identity-design.md section 3, section 5). Armor identity — the
/// baseline itself, and the damage reduction and movement penalty it
/// carries — is decided entirely in <c>Hukbo.Core</c> from the scenario's
/// <c>CombatLoadout</c>. This type never changes that: it only ever names
/// which historically attested garment or corselet the client draws and
/// describes for a warrior whose baseline the simulation already assigned,
/// exactly as <c>ShieldSkinEntry</c> does for <see cref="PawnShieldRole"/>
/// and never for shield presence itself.
/// </summary>
internal sealed record ArmorVariantEntry(
    VisualCatalogEntry Catalog,
    ArmorId Baseline)
{
    // Positional-record property re-declarations, mirroring
    // ShieldSkinEntry's own pattern: each initializer runs once, against the
    // primary constructor parameter of the same name, and never re-runs on a
    // `with` expression.
    public VisualCatalogEntry Catalog { get; init; } =
        Catalog ?? throw new ArgumentNullException(nameof(Catalog));

    public ArmorId Baseline { get; init; } = ValidateBaseline(Baseline);

    private static ArmorId ValidateBaseline(ArmorId baseline)
    {
        if (!Enum.IsDefined(baseline))
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseline),
                baseline,
                "Baseline must be a defined ArmorId member.");
        }

        return baseline;
    }
}

/// <summary>
/// The <c>appearance.armorVariant.*</c> visual catalog (armor-identity-
/// design.md section 3, section 5): the cultural-identity, evidence-tier,
/// and citation layer drawn over the authoritative Core <see cref="ArmorId"/>
/// baseline, selected by one new salted stream per pawn
/// (<see cref="PresentationSalts.ArmorVariantSalt"/>). This follows the same
/// baseline-versus-variant split the design's section 3 states for the
/// shipped shield precedent: the baseline is a real simulation value folded
/// into three content hashes, and every variant row here costs nothing but
/// authoring, because it changes only what a warrior already wearing that
/// baseline looks like and what the inspector says about it.
///
/// Ten rows, matching the design's section 5 table exactly. Three of them —
/// <see cref="CordedRope"/>, <see cref="CarabaoHide"/>, and
/// <see cref="Hardwood"/> — carry <see cref="VisualEvidenceTier.DocumentedFormUncertain"/>
/// here even though the design document's own summary table (section 5)
/// lists them as "Documented". The design document is explicit, in its
/// section 1, that where the design and the underlying research document
/// disagree about a historical fact, the research document wins, and
/// <c>docs/research/HISTORICAL_1500s_ARMOR.md</c> section 4.1 grades all
/// three of the sources underneath those three rows — the corded-rope
/// corselet (section 4.4, Lavezaris and the Cebu 1565 skirmish account), the
/// carabao-hide corselet (section 4.4, Lavezaris), and the hardwood
/// corselet (section 4.4, Artieda) — as "Documented, form uncertain" in its
/// own confidence column. CLAUDE.md section 7 makes the research document's
/// grading binding for any historical claim; this catalog follows it over
/// the design's own summary table rather than "correcting" it back, so a
/// future reader who notices the mismatch does not re-introduce it.
///
/// No row exists for <see cref="ArmorId.LightOrganic"/>: the design's section
/// 4 marks it legacy and unfielded by any new preset, and <see cref="GetVariants"/>
/// returns an empty list for it — never throws — so a replay recorded under
/// an older preset still renders. No row exists for head protection either:
/// the design's section 5 states head protection is out of scope for this
/// package and drafts it as a separate, later design.
/// </summary>
internal static class ArmorVisualCatalog
{
    // ================= QuiltedCotton variants =================

    /// <summary>
    /// Baluti — the Visayan quilted fighting garment. Pigafetta's own 1521
    /// Visayan vocabulary glosses the word directly as quilted garments used
    /// for fighting: a contact-period eyewitness recording the word and its
    /// martial meaning together, not an inference from a later dictionary.
    /// This is the one body-armor term the design's section 9 clears the
    /// pair-form label bar for.
    /// </summary>
    public static readonly ArmorVariantEntry Baluti = new(
        new VisualCatalogEntry(
            "appearance.armorVariant.baluti",
            0,
            "Baluti — Quilted Armor",
            VisualEvidenceTier.Documented,
            VisualScopeTag.Visayan,
            "Visayas — 1521. Pigafetta's own Visayan vocabulary glosses " +
            "baluti directly as quilted garments used for fighting, a " +
            "contact-period eyewitness recording the word and its martial " +
            "meaning together. The one body-armor term that clears the " +
            "pair-form label bar (HISTORICAL_1500s_ARMOR.md section 7.1).",
            VisualDetailTier.Low),
        ArmorId.QuiltedCotton);

    /// <summary>
    /// The sleeved, full-length cotton corselet Alvarado names for the
    /// Sarangani area on the Villalobos expedition. Documented, form
    /// uncertain: the material and the coverage are attested, the cut and
    /// construction are not.
    /// </summary>
    public static readonly ArmorVariantEntry SleevedCotton = new(
        new VisualCatalogEntry(
            "appearance.armorVariant.sleevedCotton",
            1,
            "Sleeved Cotton Corselet",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Sarangani and the islands around it — Villalobos expedition, " +
            "1543-1546. Alvarado, writing from Lisbon in 1548, names a " +
            "sleeved cotton corselet reaching to the feet. He gives " +
            "material and coverage, not cut or construction.",
            VisualDetailTier.Low),
        ArmorId.QuiltedCotton);

    /// <summary>
    /// Rattan named as an armor material by Artieda in 1573, grouped under
    /// the quilted-cotton baseline for gameplay purposes only.
    /// </summary>
    public static readonly ArmorVariantEntry Rattan = new(
        new VisualCatalogEntry(
            "appearance.armorVariant.rattan",
            2,
            "Rattan Armor",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Unspecified region — 1573. Artieda names rattan as an armor " +
            "material, alongside cotton-lined blanket armor, in the same " +
            "sentence. This entry binds the quilted baseline for gameplay " +
            "grouping only — it is not a claim that rattan and cotton are " +
            "the same material.",
            VisualDetailTier.Low),
        ArmorId.QuiltedCotton);

    /// <summary>
    /// Corded rope armor, attested independently at Cebu in 1565 and in
    /// Lavezaris's 1573 Luzon report. Documented, form uncertain per
    /// HISTORICAL_1500s_ARMOR.md section 4.1 — see this catalog's own
    /// top-level remarks on why this row does not follow the design
    /// document's summary table.
    /// </summary>
    public static readonly ArmorVariantEntry CordedRope = new(
        new VisualCatalogEntry(
            "appearance.armorVariant.cordedRope",
            3,
            "Corded Rope Corselet",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Attested at Cebu in 1565, in an account of a skirmish in " +
            "which fighters put on wooden corselets and corded " +
            "breastplates before engaging, and again in Lavezaris's 1573 " +
            "Luzon report of corselets of buffalo hide and knotted rope. " +
            "Graded Documented, form uncertain in " +
            "HISTORICAL_1500s_ARMOR.md section 4.1, not the plain " +
            "Documented the design document's own summary table lists; " +
            "the research document's grading is binding under CLAUDE.md " +
            "section 7. This entry binds the quilted baseline for " +
            "gameplay grouping only — it is not a claim that rope " +
            "cordage and cotton are the same material.",
            VisualDetailTier.Low),
        ArmorId.QuiltedCotton);

    // ================= HideCorselet variants =================

    /// <summary>
    /// The carabao/buffalo-hide corselet, attested independently by
    /// Lavezaris for Luzon in 1573 and by Cauchela and Aldave for Camarines
    /// on 17 July 1574. Documented, form uncertain per
    /// HISTORICAL_1500s_ARMOR.md section 4.1 — see this catalog's own
    /// top-level remarks.
    /// </summary>
    public static readonly ArmorVariantEntry CarabaoHide = new(
        new VisualCatalogEntry(
            "appearance.armorVariant.carabaoHide",
            4,
            "Carabao-Hide Corselet",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Lavezaris, Luzon, 29 June 1573, and Cauchela and Aldave, " +
            "Camarines, 17 July 1574, both attest hide corselets. Neither " +
            "source gives any construction detail. Graded Documented, " +
            "form uncertain in HISTORICAL_1500s_ARMOR.md section 4.1, not " +
            "the plain Documented the design document's own summary table " +
            "lists; the research document's grading is binding under " +
            "CLAUDE.md section 7.",
            VisualDetailTier.Low),
        ArmorId.HideCorselet);

    // ================= RigidCuirass variants =================

    /// <summary>
    /// The hardwood corselet Artieda names in 1573, explicitly marked as
    /// minority equipment by his own word "some". Documented, form
    /// uncertain per HISTORICAL_1500s_ARMOR.md section 4.1 — see this
    /// catalog's own top-level remarks.
    /// </summary>
    public static readonly ArmorVariantEntry Hardwood = new(
        new VisualCatalogEntry(
            "appearance.armorVariant.hardwood",
            5,
            "Hardwood Corselet",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Artieda, 1573: \"Some wear corselets, made of very hard " +
            "black wood resembling ebony.\" The word \"some\" is his own, " +
            "and it marks this as minority equipment. Graded Documented, " +
            "form uncertain in HISTORICAL_1500s_ARMOR.md section 4.1, not " +
            "the plain Documented the design document's own summary table " +
            "lists; the research document's grading is binding under " +
            "CLAUDE.md section 7.",
            VisualDetailTier.Low),
        ArmorId.RigidCuirass);

    /// <summary>
    /// The wood-and-buffalo-horn corselet Alvarado names for the Sarangani
    /// area in 1548.
    /// </summary>
    public static readonly ArmorVariantEntry WoodAndHorn = new(
        new VisualCatalogEntry(
            "appearance.armorVariant.woodAndHorn",
            6,
            "Wood-and-Buffalo-Horn Corselet",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Sarangani and the islands around it — Villalobos expedition, " +
            "1543-1546. Alvarado, 1548, names \"corselets made of wood " +
            "and buffalo horn\" among the region's defensive arms. " +
            "Material named, no thickness, closure, or cut described.",
            VisualDetailTier.Low),
        ArmorId.RigidCuirass);

    /// <summary>
    /// The bamboo-and-hardwood cuirass Alvarado names for the same 1548
    /// relation, described as covering the whole body.
    /// </summary>
    public static readonly ArmorVariantEntry BambooCuirass = new(
        new VisualCatalogEntry(
            "appearance.armorVariant.bambooCuirass",
            7,
            "Bamboo-and-Hardwood Cuirass",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Sarangani and the islands around it — Villalobos expedition, " +
            "1543-1546. Alvarado, 1548, names \"cuirasses made of bamboo " +
            "and hard wood, which entirely cover them\" among the " +
            "region's body-armor types. Coverage is attested; " +
            "construction is not.",
            VisualDetailTier.Low),
        ArmorId.RigidCuirass);

    // ================= ImportedIron variant =================

    /// <summary>
    /// The iron corselet, greaves, wristlets, gauntlets, and helmets
    /// Lavezaris reports for Camarines on 17 July 1574 — the same report
    /// William Henry Scott (1994) argues is a confusion with Japanese
    /// equipment his subject had recently encountered on the Ilocos coast.
    /// Neither position is settled; HISTORICAL_1500s_ARMOR.md section 4.6
    /// keeps it contested, and this note discloses the dispute rather than
    /// choosing a side.
    /// </summary>
    public static readonly ArmorVariantEntry ImportedIron = new(
        new VisualCatalogEntry(
            "appearance.armorVariant.importedIron",
            8,
            "Iron Corselet",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Camarines — 17 July 1574. Lavezaris reports the most " +
            "valiant fighters yet found in the region possess much good " +
            "armor: iron corselets, greaves, wristlets, gauntlets, and " +
            "helmets. William Henry Scott, Barangay (1994), argues this " +
            "is a confusion with Japanese equipment Salcedo's expedition " +
            "had recently encountered on the Ilocos coast. " +
            "HISTORICAL_1500s_ARMOR.md section 4.6 keeps the question " +
            "contested rather than resolving it either way, and this " +
            "entry's inclusion in the game does not settle the historical " +
            "dispute.",
            VisualDetailTier.Low),
        ArmorId.ImportedIron);

    // ================= None variant =================

    /// <summary>
    /// Bare torso — no body armor at all. Pigafetta's Mactan battle
    /// narrative, 1521, describes the defending force's weapons and shields
    /// in detail and names no body armor on any of them.
    /// </summary>
    public static readonly ArmorVariantEntry BareTorso = new(
        new VisualCatalogEntry(
            "appearance.armorVariant.bareTorso",
            9,
            "Bare Torso",
            VisualEvidenceTier.Documented,
            VisualScopeTag.NotApplicable,
            "Mactan — 1521. Pigafetta describes the Mactan force's " +
            "weapons and shields in detail and names no body armor on " +
            "any of them.",
            VisualDetailTier.Low),
        ArmorId.None);

    // ================= Roster =================

    private static readonly IReadOnlyList<ArmorVariantEntry> QuiltedCottonVariants =
    [
        Baluti,
        SleevedCotton,
        Rattan,
        CordedRope,
    ];

    private static readonly IReadOnlyList<ArmorVariantEntry> HideCorseletVariants =
    [
        CarabaoHide,
    ];

    private static readonly IReadOnlyList<ArmorVariantEntry> RigidCuirassVariants =
    [
        Hardwood,
        WoodAndHorn,
        BambooCuirass,
    ];

    private static readonly IReadOnlyList<ArmorVariantEntry> ImportedIronVariants =
    [
        ImportedIron,
    ];

    private static readonly IReadOnlyList<ArmorVariantEntry> NoneVariants =
    [
        BareTorso,
    ];

    private static readonly IReadOnlyList<ArmorVariantEntry> NoVariants = [];

    /// <summary>
    /// Every declared variant for <paramref name="armor"/>, in catalog (and
    /// selection-index) order. Empty for <see cref="ArmorId.LightOrganic"/>,
    /// the legacy baseline no new preset fields (design section 4), so a
    /// replay recorded under an older preset still resolves totally rather
    /// than throwing — <see cref="SelectVariant"/> falls through to
    /// <see cref="ModelCategoryDefault"/> whenever the returned list is
    /// empty, exactly like <c>ShieldVisualCatalog.GetSkins</c>. Throws for
    /// any other undefined <see cref="ArmorId"/> value.
    /// </summary>
    public static IReadOnlyList<ArmorVariantEntry> GetVariants(ArmorId armor) =>
        armor switch
        {
            ArmorId.LightOrganic => NoVariants,
            ArmorId.None => NoneVariants,
            ArmorId.QuiltedCotton => QuiltedCottonVariants,
            ArmorId.HideCorselet => HideCorseletVariants,
            ArmorId.RigidCuirass => RigidCuirassVariants,
            ArmorId.ImportedIron => ImportedIronVariants,
            _ => throw new ArgumentOutOfRangeException(nameof(armor), armor, null),
        };

    // ================= Fallback step 2/3: default =================

    /// <summary>
    /// <c>appearance.category.armorDefault</c> — the generic armor-variant
    /// fallback (fallback chain steps 2 and 3, family default and
    /// model-category default coinciding for this single-family catalog,
    /// exactly as <c>ShieldVisualCatalog.Default</c> and
    /// <c>ShieldVisualCatalog.ModelCategoryDefault</c> coincide for the
    /// single shielded role today). Carries no historical claim at all —
    /// <see cref="VisualEvidenceTier.PresentationOnly"/> — because it names
    /// no baseline of its own and exists purely so resolution can never
    /// fail totally. A single shared static instance, never rebuilt per
    /// call, so resolving every pawn every frame stays allocation-free in
    /// steady state.
    /// </summary>
    public static readonly ArmorVariantEntry Default = new(
        new VisualCatalogEntry(
            "appearance.category.armorDefault",
            0,
            "Armor",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "The generic armor-variant fallback: used only when a " +
            "warrior's own baseline has no declared variant row to select " +
            "from.",
            VisualDetailTier.Low),
        ArmorId.None);

    // ================= Selection =================

    /// <summary>
    /// The deterministic variant-selection stream: a pure, salted,
    /// allocation-free function of <paramref name="entityId"/> and
    /// <paramref name="armor"/>, stable across frames and replays. Mixes
    /// <paramref name="entityId"/> XOR
    /// <see cref="PresentationSalts.ArmorVariantSalt"/> through the
    /// SplitMix64 finalizer — the same pattern
    /// <c>ShieldVisualCatalog.SelectSkin</c> and
    /// <c>WeaponVisualCatalog.Mix</c> use, duplicated here per this
    /// codebase's own convention of one local mixer per presentation-salt
    /// consumer — and reduces the result modulo the baseline's variant
    /// count. Falls through to <see cref="Default"/> whenever the baseline
    /// has no variants (today, only <see cref="ArmorId.LightOrganic"/>);
    /// never throws for a defined <see cref="ArmorId"/>. Armor *identity*
    /// is untouched: this only ever selects among the variants of the
    /// baseline the caller already named, exactly as
    /// <paramref name="armor"/> was resolved from the authoritative Core
    /// <c>CombatLoadout</c> upstream.
    /// </summary>
    public static ArmorVariantEntry SelectVariant(ulong entityId, ArmorId armor)
    {
        var variants = GetVariants(armor);
        if (variants.Count == 0)
        {
            return Default;
        }

        var mixed = Mix(entityId ^ PresentationSalts.ArmorVariantSalt);
        var index = (int)(mixed % (ulong)variants.Count);
        return variants[index];
    }

    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
