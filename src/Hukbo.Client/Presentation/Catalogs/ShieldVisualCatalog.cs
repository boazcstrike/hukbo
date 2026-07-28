using Hukbo.Client.Presentation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// One shield skin catalog entry: the shared <see cref="VisualCatalogEntry"/>
/// shape plus which <see cref="PawnShieldRole"/> it belongs to and the face
/// tone it draws through <c>PawnRenderer.DrawShield</c>
/// (shield-visuals-design.md, R-W2.1, R-W2.3, R-W2.8). Skins vary face tone
/// only this pass — never the drawn rectangle, outline, or seam — so this
/// type carries no width, height, or offset field at all; the absence itself
/// is the R-X.12 false-cause guard the design's OD-10 amendment still keeps:
/// a skin may only ever change how the block looks, never what it covers.
/// </summary>
internal sealed record ShieldSkinEntry(
    VisualCatalogEntry Catalog,
    PawnShieldRole Shield,
    Color FaceColor)
{
    // Positional-record property re-declarations, mirroring
    // WeaponSilhouetteEntry/WeaponTintEntry's own pattern: each initializer
    // runs once, against the primary constructor parameter of the same name,
    // and never re-runs on a `with` expression.
    public VisualCatalogEntry Catalog { get; init; } =
        Catalog ?? throw new ArgumentNullException(nameof(Catalog));

    public PawnShieldRole Shield { get; init; } = ValidateShield(Shield);

    private static PawnShieldRole ValidateShield(PawnShieldRole shield)
    {
        if (!Enum.IsDefined(shield))
        {
            throw new ArgumentOutOfRangeException(
                nameof(shield),
                shield,
                "Shield must be a defined PawnShieldRole member.");
        }

        if (shield == PawnShieldRole.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shield),
                shield,
                "A shield skin entry may not target PawnShieldRole.None: " +
                "absence of equipment resolves to \"draw nothing\" before " +
                "the visual chain starts and never enters it (shield-" +
                "visuals-design.md, fallback chain).");
        }

        return shield;
    }
}

/// <summary>
/// The <c>shield.*</c> visual catalog (shield-visuals-design.md;
/// implementation-plan-draft.md VIS-013 milestone, VIS-014): the face-tone
/// skin set for <see cref="PawnShieldRole.TallHardwood"/>, selected by one
/// new salted stream per pawn (<see cref="PresentationSalts.ShieldSkinSalt"/>, R-W2.3).
/// Shield *presence* always comes from the authoritative Core
/// <c>CombatLoadout</c>, never from the entity ID; this stream only ever
/// picks a face tone for the shield the loadout already assigned
/// (<c>PawnAppearanceFactoryTests</c> pins that rule and this catalog never
/// weakens it).
///
/// <b>VIS-013 shipped S1 <see cref="MactanThin"/> alone, with the selection
/// stream's modulus fixed at 1 (a real stream, degenerate on its own
/// milestone task, exactly as <c>WeaponVisualCatalog</c>'s silhouette stream
/// was). VIS-014 (OD-10, resolved 2026-07-28, option (a)) appends S2
/// <see cref="MorgaFullBody"/>, S3 <see cref="BoxerCagayan"/>, and S5
/// <see cref="VisayanKalasag"/>, growing the modulus to 4 — the shield
/// design's evidence-cleared count, with no fifth skin authorized.</b>
/// <see cref="Default"/> (fallback chain step 2, family default) is the
/// unrolled fallback target and stays unreachable by
/// <see cref="SelectSkin"/> until a future task can actually produce a
/// resolution failure for it to catch.
/// </summary>
internal static class ShieldVisualCatalog
{
    // ================= Palette (S1 mactanThin, this pass) =================

    // PROVISIONAL: the lightest, palest tone within the "pale palm-wood
    // range" the shield design names for S1 (skin table, "Lightest face
    // tone of the four") — a free implementer pick within that description,
    // exactly like OD-W1-b's GripWarmOchre in the weapon catalog. Distinctly
    // warmer and paler than the existing charred-wood block
    // (WeaponVisualCatalog.CharredWoodBrown, (48, 40, 33)) and than
    // BarkBrown (DyePalette, (122, 90, 58)), which the design's own ordering
    // reserves for the darker, later skins still to come.
    //
    // ShieldVisualCatalogTests records its true contrast-envelope
    // relationship rather than asserting past it: PalmWoodPale clears every
    // ground shade and every clothing color except the Field Manual theme's
    // ground, whose own parchment-tan palette sits structurally close to any
    // pale wood tone — the same honest-recording situation
    // WeaponVisualCatalog.CharredWoodBrown and DyePalette's
    // GoldAccent/TurmericYellow already record against their own
    // structurally adjacent references. VIS-033 (theme contrast continuity)
    // owns reconciling it, not this file.
    public static readonly Color PalmWoodPale = new(222, 178, 108);

    // VIS-014: PROVISIONAL. A free implementer pick within the shield
    // design's "mid light-wood tone" description for S2 (skin table) —
    // between PalmWoodPale above and the existing CharredWoodBrown, per the
    // table's own ordering of the four face tones from lightest to darkest.
    // Tuned so it clears both ContrastEnvelope.MinimumGroundDistance and
    // ContrastEnvelope.MinimumClothingDistance against every reference
    // color ShieldVisualCatalogTests checks (OD-W2-a companion pick, same
    // full-clearance discipline as WeaponVisualCatalog's VIS-011 tones).
    public static readonly Color LightHardwoodTan = new(188, 146, 88);

    // VIS-014: PROVISIONAL. A free implementer pick within the shield
    // design's "resin-brown face tone" description for S5 (skin table) —
    // distinct from both LightHardwoodTan above and the existing
    // WeaponVisualCatalog.CharredWoodBrown/PalmRattanOchre tones this skin
    // sits beside on a shielded pawn. Tuned so it clears both
    // ContrastEnvelope.MinimumGroundDistance and
    // ContrastEnvelope.MinimumClothingDistance against every reference
    // color ShieldVisualCatalogTests checks.
    public static readonly Color ResinBrownTone = new(175, 125, 65);

    // ================= TallHardwood skins =================

    /// <summary>
    /// S1 — the Mactan thin-wood shield (1521). Documented (existence,
    /// thinness, active use with evasive footwork); Documented, form
    /// uncertain (shape). Lightest face tone of the four; baseline
    /// proportions (no delta) within the shared aspect-ratio band.
    /// </summary>
    public static readonly ShieldSkinEntry MactanThin = new(
        new VisualCatalogEntry(
            "shield.tallHardwood.mactanThin",
            0,
            "Tall Hardwood Shield",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Mactan — 1521. Pigafetta records active use with evasive " +
            "footwork and describes the wood as thin — the catalog's " +
            "\"hardwood\" identifier slightly overstates that (R-W2.7); " +
            "the shape itself is Documented, form uncertain. Lightest " +
            "face tone of the four cleared tall-shield anchors; straight " +
            "rectangular outline; no accent line.",
            VisualDetailTier.Low),
        PawnShieldRole.TallHardwood,
        FaceColor: PalmWoodPale);

    /// <summary>
    /// S2 — the Morga full-body shield (Manila, 1609). Documented, form
    /// uncertain: light wood, head-to-foot coverage, inside-armhole
    /// fastening. Mid light-wood face tone, straight outline; proportion at
    /// the tall end of the shared aspect-ratio band
    /// (<c>PawnGeometry</c>'s per-skin delta, OD-10).
    /// </summary>
    public static readonly ShieldSkinEntry MorgaFullBody = new(
        new VisualCatalogEntry(
            "shield.tallHardwood.morgaFullBody",
            1,
            "Tall Hardwood Shield",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Manila — 1609. Morga records a light-wood shield with " +
            "head-to-foot coverage and inside-armhole fastening; " +
            "Documented, form uncertain. A secondary-source quotation " +
            "describing the same coverage reached this research through " +
            "secondary transmission and is withheld here until verified " +
            "against Blair & Robertson. Mid light-wood face tone; " +
            "straight rectangular outline; proportion at the tall end of " +
            "the shared tall-shield aspect-ratio band (OD-10).",
            VisualDetailTier.Low),
        PawnShieldRole.TallHardwood,
        FaceColor: LightHardwoodTan);

    /// <summary>
    /// S3 — the Boxer Codex Cagayan shield (Manila, c. 1590–1595). Documented
    /// as a late-century visual depiction; Documented, form uncertain as
    /// construction evidence. The existing charred-wood tone, unchanged from
    /// today's block — this skin is the already-shipped block's named
    /// inspiration. A one-to-two-layout-pixel top/bottom edge inset
    /// (<c>PawnRenderer.DrawShield</c>) reads as the Codex's gentle curve,
    /// degrading to the straight block at Low tier; the vertical seam is
    /// kept at Medium tier alongside the curvature (OD-W2-c, resolved here:
    /// default retained).
    /// </summary>
    public static readonly ShieldSkinEntry BoxerCagayan = new(
        new VisualCatalogEntry(
            "shield.tallHardwood.boxerCagayan",
            2,
            "Tall Hardwood Shield",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Manila — c.1590–1595. A Boxer Codex depiction, Documented as " +
            "a late-century visual record; Documented, form uncertain as " +
            "construction evidence — the Codex guides silhouette and " +
            "color only. Tall, gently curved rectangular silhouette; " +
            "already the existing tall shield's named inspiration. " +
            "Existing charred-wood tone; slight outline curvature (one to " +
            "two layout pixels on the top and bottom edges); vertical " +
            "seam kept at Medium tier alongside it (OD-W2-c).",
            VisualDetailTier.Low),
        PawnShieldRole.TallHardwood,
        FaceColor: WeaponVisualCatalog.CharredWoodBrown);

    /// <summary>
    /// S5 — the Visayan kalasag form (Alcina 1668; Scott 1994, synthesis).
    /// Documented, form uncertain: long narrow body shield, light fibrous
    /// wood, rattan strengthening, resin coating. Resin-brown face tone; one
    /// horizontal rattan-binding accent line across the face at Medium+
    /// tier, replacing the vertical seam on this skin only
    /// (<c>PawnRenderer.DrawShield</c>); narrowest proportion within the
    /// shared aspect-ratio band (<c>PawnGeometry</c>'s per-skin delta,
    /// OD-10). The <i>kalasag</i> name is a provisional attachment pending
    /// vocabulary verification and never appears in the player-facing label
    /// — this entry ships under the plain descriptor like every other skin
    /// (OD-1, R-W2.6).
    /// </summary>
    public static readonly ShieldSkinEntry VisayanKalasag = new(
        new VisualCatalogEntry(
            "shield.tallHardwood.visayanKalasag",
            3,
            "Tall Hardwood Shield",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Visayas — 16th c. (synthesis; Alcina 1668, Scott 1994). Long " +
            "narrow body shield; light fibrous wood, rattan strengthening, " +
            "resin coating; Documented, form uncertain. The kalasag name " +
            "is a provisional attachment pending vocabulary verification " +
            "and does not appear in the player-facing label (OD-1). " +
            "Resin-brown face tone; one horizontal rattan-binding accent " +
            "line across the face at Medium+ tier, replacing the vertical " +
            "seam on this skin; narrowest proportion within the shared " +
            "aspect-ratio band (OD-10).",
            VisualDetailTier.Low),
        PawnShieldRole.TallHardwood,
        FaceColor: ResinBrownTone);

    private static readonly IReadOnlyList<ShieldSkinEntry> TallHardwoodSkinsList =
    [
        MactanThin,
        MorgaFullBody,
        BoxerCagayan,
        VisayanKalasag,
    ];

    private static readonly IReadOnlyList<ShieldSkinEntry> NoSkins = [];

    /// <summary>
    /// Every declared skin for <see cref="PawnShieldRole.TallHardwood"/>, in
    /// catalog (and selection-index) order. VIS-013 shipped only
    /// <see cref="MactanThin"/>, so the selection stream's modulus was 1 and
    /// every entity ID resolved to it; VIS-014 (OD-10) appends S2/S3/S5,
    /// growing the modulus to 4 — the shield design's cleared-anchor count,
    /// with no fifth skin authorized.
    /// </summary>
    public static IReadOnlyList<ShieldSkinEntry> TallHardwoodSkins => TallHardwoodSkinsList;

    /// <summary>
    /// Every declared skin for <paramref name="shield"/>, in catalog (and
    /// selection-index) order. A pre-built, shared array reference; never
    /// allocates. Empty for <see cref="PawnShieldRole.None"/>, which never
    /// legitimately reaches this lookup because the shield block itself is
    /// not drawn for it, but which must still resolve totally rather than
    /// throw (<see cref="SelectSkin"/> falls through to
    /// <see cref="ModelCategoryDefault"/> whenever the returned list is
    /// empty, exactly like <c>WeaponVisualCatalog.GetTints</c>).
    /// </summary>
    public static IReadOnlyList<ShieldSkinEntry> GetSkins(PawnShieldRole shield) =>
        shield switch
        {
            PawnShieldRole.TallHardwood => TallHardwoodSkinsList,
            PawnShieldRole.None => NoSkins,
            _ => throw new ArgumentOutOfRangeException(nameof(shield), shield, null),
        };

    // ================= Fallback step 2: family default =====================

    /// <summary>
    /// <c>shield.tallHardwood.default</c> — today's charred-wood block
    /// exactly as it drew before this task, unchanged
    /// (shield-visuals-design.md). The family's fallback target (fallback
    /// chain step 2, <see cref="VisualFallbackStep.FamilyDefault"/>), never a
    /// rolled skin; unreachable by <see cref="SelectSkin"/> until a future
    /// task can actually produce a resolution failure for it to catch.
    /// </summary>
    public static readonly ShieldSkinEntry Default = new(
        new VisualCatalogEntry(
            "shield.tallHardwood.default",
            0,
            "Tall Hardwood Shield",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "The current block exactly as it drew before this pass — the " +
            "family's fallback target (fallback chain step 2), never a " +
            "rolled skin.",
            VisualDetailTier.Low),
        PawnShieldRole.TallHardwood,
        FaceColor: WeaponVisualCatalog.CharredWoodBrown);

    // ================= Fallback step 3: model-category default =============

    /// <summary>
    /// The generic tall-shield-block drawable (fallback chain step 3,
    /// <see cref="VisualFallbackStep.ModelCategoryDefault"/>,
    /// shield-visuals-design.md fallback chain): the same charred-wood tone
    /// as <see cref="Default"/>. For this single-shield roster this step
    /// coincides with step 2 in effect, but it stays a distinct, testable
    /// chain step so the shield chain's shape matches the weapon and
    /// appearance chains, mirroring
    /// <see cref="WeaponVisualCatalog.ModelCategoryDefaultTint"/>'s own
    /// remark. A single shared static instance, never rebuilt per call, so
    /// resolving every pawn every frame
    /// (<c>PawnAppearanceFactory.Create</c>) stays allocation-free in
    /// steady state.
    /// </summary>
    public static readonly ShieldSkinEntry ModelCategoryDefault = new(
        new VisualCatalogEntry(
            "shield.category.tallDefault",
            0,
            "Tall Hardwood Shield",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "The generic tall-shield-block fallback (model-category " +
            "default, fallback chain step 3): the same charred-wood tone " +
            "as the family default. Used only when a shield role's own " +
            "catalog entries fail to resolve.",
            VisualDetailTier.Low),
        PawnShieldRole.TallHardwood,
        FaceColor: WeaponVisualCatalog.CharredWoodBrown);

    // ================= Selection =================

    /// <summary>
    /// The deterministic skin-selection stream (R-W2.3, R-W6.2): a pure,
    /// salted, allocation-free function of <paramref name="entityId"/> and
    /// <paramref name="shield"/>, stable across frames and replays. Mixes
    /// <paramref name="entityId"/> XOR
    /// <see cref="PresentationSalts.ShieldSkinSalt"/> through the SplitMix64
    /// finalizer (the same pattern <c>PawnAppearanceFactory.Mix</c> and
    /// <c>WeaponVisualCatalog.Mix</c> use, duplicated here per this
    /// codebase's own convention of one local mixer per presentation-salt
    /// consumer) and reduces the result modulo the shield role's skin count.
    /// Falls through to <see cref="ModelCategoryDefault"/> — fallback step 3
    /// — whenever the shield role has no skins (today, only
    /// <see cref="PawnShieldRole.None"/>, which the caller never actually
    /// draws a shield for); never throws, never returns
    /// <see langword="null"/>. Shield *identity* is untouched: this only
    /// ever selects among the skins of the shield role the caller already
    /// named, exactly as <paramref name="shield"/> was resolved from the
    /// authoritative Core loadout upstream.
    /// </summary>
    public static ShieldSkinEntry SelectSkin(ulong entityId, PawnShieldRole shield)
    {
        var skins = GetSkins(shield);
        if (skins.Count == 0)
        {
            return ModelCategoryDefault;
        }

        var mixed = Mix(entityId ^ PresentationSalts.ShieldSkinSalt);
        var index = (int)(mixed % (ulong)skins.Count);
        return skins[index];
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
