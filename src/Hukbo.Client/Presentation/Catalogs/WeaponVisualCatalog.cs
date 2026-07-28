using Hukbo.Client.Presentation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// One weapon silhouette catalog entry: the shared <see cref="VisualCatalogEntry"/>
/// shape plus which <see cref="PawnWeaponRole"/> it belongs to and whether the
/// pawn-scale selection stream may ever choose it (weapon-visuals-design.md,
/// R-W1.1, R-W1.4). Every weapon carries exactly one
/// <see cref="PawnSelectable"/> silhouette — its documented period form; Kalis
/// additionally carries two inspector/armory-card-only later forms
/// (<c>l2</c>, <c>l3</c>) that this field permanently excludes from pawn-scale
/// drawing, both by <see cref="PawnSelectable"/> being <see langword="false"/>
/// and by the selection stream never returning them
/// (<see cref="WeaponVisualCatalog.PawnSilhouette"/> is hard-wired to
/// <see cref="WeaponVisualCatalog.KalisL1"/>, not a modulo pick over all
/// three).
/// </summary>
internal sealed record WeaponSilhouetteEntry(
    VisualCatalogEntry Catalog,
    PawnWeaponRole Weapon,
    bool PawnSelectable)
{
    // Positional-record property re-declarations, mirroring
    // AppearanceComponentEntry's own pattern: each initializer runs once,
    // against the primary constructor parameter of the same name, and never
    // re-runs on a `with` expression.
    public VisualCatalogEntry Catalog { get; init; } =
        Catalog ?? throw new ArgumentNullException(nameof(Catalog));

    public PawnWeaponRole Weapon { get; init; } = ValidateWeapon(Weapon);

    private static PawnWeaponRole ValidateWeapon(PawnWeaponRole weapon)
    {
        if (!Enum.IsDefined(weapon))
        {
            throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                "Weapon must be a defined PawnWeaponRole member.");
        }

        return weapon;
    }
}

/// <summary>
/// One weapon tint catalog entry: the shared <see cref="VisualCatalogEntry"/>
/// shape plus the blade and grip colors it draws through the existing
/// <c>PawnRenderer.DrawBlade</c> line composition (weapon-visuals-design.md,
/// R-W1.2, R-W1.7). Tints vary color and tone only — never geometry, offsets,
/// or thickness — so this type carries no width or length field at all; the
/// absence itself is the R-X.12 false-cause guard (a longer-looking blade on
/// an unchanged <c>WeaponId</c> would imply a mechanical reach difference that
/// does not exist).
/// </summary>
/// <param name="LashingBandColor">
/// VIS-011: the rattan lashing band accent color this tint carries, or
/// <see langword="null"/> for every tint that carries none — today, every
/// tint except the Wasay's <c>lashedWorn</c> entry (weapon-visuals-design.md,
/// R-W1.5). Presentation-only, tone-level, and Medium/High-tier gated by
/// <c>PawnRenderer</c> through <c>DetailTierGate</c>; never a new geometry
/// field, so the R-X.12 false-cause guard on this type still holds.
/// </param>
internal sealed record WeaponTintEntry(
    VisualCatalogEntry Catalog,
    PawnWeaponRole Weapon,
    Color BladeColor,
    Color GripColor,
    Color? LashingBandColor = null)
{
    public VisualCatalogEntry Catalog { get; init; } =
        Catalog ?? throw new ArgumentNullException(nameof(Catalog));

    public PawnWeaponRole Weapon { get; init; } = ValidateWeapon(Weapon);

    private static PawnWeaponRole ValidateWeapon(PawnWeaponRole weapon)
    {
        if (!Enum.IsDefined(weapon))
        {
            throw new ArgumentOutOfRangeException(
                nameof(weapon),
                weapon,
                "Weapon must be a defined PawnWeaponRole member.");
        }

        return weapon;
    }
}

/// <summary>
/// The <c>weapon.*</c> visual catalog (weapon-visuals-design.md;
/// implementation-plan-draft.md VIS-010 and, later, VIS-011): one primary
/// battle silhouette per weapon plus its bounded set of presentation-only
/// material tints. The design reserves two independent salted streams per
/// pawn (R-W1.3, R-W6.2) —
/// <see cref="PresentationSalts.WeaponSilhouetteSalt"/> for a future
/// per-weapon silhouette choice and <see cref="PresentationSalts.WeaponTintSalt"/>
/// for the tint choice this pass actually makes — but every weapon in this
/// catalog has exactly one pawn-scale silhouette, so the silhouette stream
/// is degenerate this pass: <see cref="PawnSilhouette"/> is a direct lookup,
/// not a salted pick, and <see cref="PresentationSalts.WeaponSilhouetteSalt"/>
/// stays reserved, unconsumed, for whenever a weapon ships a second
/// pawn-scale form. Only <see cref="SelectTint"/> actually mixes
/// <see cref="PresentationSalts.WeaponTintSalt"/> today. Weapon *identity*
/// — which <see cref="PawnWeaponRole"/> a pawn wields — always comes from
/// the authoritative Core <c>CombatLoadout</c>, never from the entity ID;
/// the tint stream here only ever picks a color variant of the weapon the
/// loadout already assigned (<c>PawnAppearanceFactoryTests</c> pins this
/// rule and this catalog never weakens it).
///
/// <b>VIS-010 milestone scope.</b> Kalis shipped first — the straight
/// <c>l1</c> silhouette (the only pawn-scale form; the strongest attestation
/// of the four weapons, Pigafetta's <i>calis</i> at Cebu in 1521), its two
/// inspector-only later forms <c>l2</c>/<c>l3</c>, and its two
/// presentation-only tints <c>freshIron</c>/<c>darkHilt</c>.
///
/// <b>VIS-011 scope.</b> Completes the catalog: Kampilan (<c>k1</c> pawn
/// silhouette, three tints, the inspector-only later form <c>k2</c>), Wasay
/// (<c>w1</c> pawn silhouette, three tints, one of which —
/// <c>lashedWorn</c> — carries the rattan lashing band accent at Medium/High
/// tier), and Itak (<c>i1</c> pawn silhouette, two tints — the honest
/// ceiling for the plainest weapon in the roster). Every
/// <see cref="PawnWeaponRole"/> now resolves through <see cref="GetTints"/>
/// and <see cref="PawnSilhouette"/> to its own catalog entries;
/// <see cref="ModelCategoryDefaultTint"/> — fallback step 3 — is therefore
/// unreachable by <see cref="SelectTint"/> for any defined
/// <see cref="PawnWeaponRole"/> today, exactly as
/// <c>ShieldVisualCatalog.Default</c> records for its own family-default
/// step: a real, distinct, testable chain step kept alive by the fallback
/// tests' delegate doubles rather than by any live gap in the catalog.
/// <c>PawnRenderer.DrawWeapon</c> and <c>DrawSecondaryEquipment</c> consume
/// the resolved tint for every weapon now, including the Wasay's head fill
/// and lashing band.
///
/// The Cordilleran head axe (research W2) remains entirely absent from this
/// catalog — no ID, no entry, no fallback target — per R-W1.4: an
/// identifier is a commitment surface, and this form must never be
/// reachable by any resolution path.
/// </summary>
internal static class WeaponVisualCatalog
{
    // ================= Palette (weapon material tones, VIS-010/VIS-011) ====

    // PROVISIONAL: a free implementer pick within the weapon design's
    // "warm ochre, lighter than the haft ochre" description (weapon-visuals-
    // design.md palette table), tuned so it clears both
    // ContrastEnvelope.MinimumGroundDistance and
    // ContrastEnvelope.MinimumClothingDistance against every reference color
    // WeaponVisualCatalogTests checks (OD-W1-b, decided here).
    public static readonly Color GripWarmOchre = new(204, 150, 90);

    // The existing charred-wood tone already drawn, unconditionally, for
    // every weapon's grip and for the shield face
    // (PawnRenderer.CharredWood / PawnAppearanceFactory's private
    // CharredWood, both (48, 40, 33)) — reused verbatim per the weapon
    // design's "existing charred-wood tone (shared with the shield face)"
    // instruction, not a freely tunable value. WeaponVisualCatalogTests
    // records its true ground-envelope relationship rather than asserting
    // past a structural shortfall it cannot fix by re-tuning a shared,
    // already-shipped color (the same situation DyePalette's GoldAccent and
    // TurmericYellow record against the faction-gold constant); final
    // reconciliation is VIS-033's remit.
    public static readonly Color CharredWoodBrown = new(48, 40, 33);

    // VIS-011: PROVISIONAL. A free implementer pick within the weapon
    // design's "a step lighter/duller than IronBlueBlack, inside the
    // envelope" description (palette table) — desaturated toward neutral
    // grey rather than merely lightened, which is what "duller" asks for on
    // top of "lighter". Tuned so it clears both
    // ContrastEnvelope.MinimumGroundDistance and
    // ContrastEnvelope.MinimumClothingDistance against every reference color
    // WeaponVisualCatalogTests checks — unlike the existing, already-shipped
    // DyePalette.IronBlueBlack, which does not clear every reference color
    // and records that honestly instead.
    public static readonly Color IronWornGrey = new(135, 138, 132);

    // VIS-011: PROVISIONAL. A free implementer pick within the weapon
    // design's "warm ochre in the existing palm/rattan range" description,
    // kept darker than GripWarmOchre per the palette table's own ordering
    // ("GripWarmOchre — plain grips — warm ochre, lighter than
    // PalmRattanOchre"). Tuned so it clears both envelope bounds against
    // every reference color WeaponVisualCatalogTests checks.
    public static readonly Color PalmRattanOchre = new(168, 122, 66);

    // VIS-011: PROVISIONAL. A free implementer pick within the weapon
    // design's "ochre distinct from both haft tones at Medium+" description
    // (the Wasay's own PalmRattanOchre and CharredWoodBrown haft tones) —
    // a brighter, more golden ochre so the lashing band reads as a band
    // rather than merging into either haft tone it can be drawn beside.
    // Tuned so it clears both envelope bounds against every reference color
    // WeaponVisualCatalogTests checks.
    public static readonly Color RattanLashingTone = new(214, 168, 84);

    // ================= Kalis silhouettes =================

    /// <summary>
    /// L1 — the straight, slim, one-handed default. The only pawn-scale
    /// Kalis silhouette; <see cref="PawnSilhouette"/> always returns this
    /// entry for <see cref="PawnWeaponRole.Kalis"/>.
    /// </summary>
    public static readonly WeaponSilhouetteEntry KalisL1 = new(
        new VisualCatalogEntry(
            "weapon.kalis.l1",
            0,
            "Kalis — Thrusting Blade",
            VisualEvidenceTier.Documented,
            VisualScopeTag.NotApplicable,
            "Cebu, 1521 (Pigafetta records calis). Slim, straight, " +
            "symmetric, one-handed — the conservative, documented-for-" +
            "name-and-class form. The only pawn-scale Kalis silhouette " +
            "(R-W1.4); see l2/l3 for the later half-waved and fully-waved " +
            "forms, inspector/armory-card only.",
            VisualDetailTier.Low),
        PawnWeaponRole.Kalis,
        PawnSelectable: true);

    /// <summary>
    /// L2 — the half-waved form. Provisional reconstruction; never a
    /// pawn-scale battle silhouette (R-W1.4). This entry exists so the
    /// inspector can honestly say the form exists and is a later
    /// attestation; the selection stream can never return it.
    /// </summary>
    public static readonly WeaponSilhouetteEntry KalisL2 = new(
        new VisualCatalogEntry(
            "weapon.kalis.l2",
            1,
            "Kalis — Thrusting Blade",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.NotApplicable,
            "Half-waved form, later attestation. Provisional " +
            "reconstruction; never a pawn-scale battle silhouette " +
            "(R-W1.4) — inspector/armory-card text only.",
            VisualDetailTier.High),
        PawnWeaponRole.Kalis,
        PawnSelectable: false);

    /// <summary>
    /// L3 — the fully wavy form. The most recognizable kris silhouette in
    /// the later record, and per that same later record itself not the most
    /// common form there either. Provisional reconstruction; never a
    /// pawn-scale battle silhouette (R-W1.4).
    /// </summary>
    public static readonly WeaponSilhouetteEntry KalisL3 = new(
        new VisualCatalogEntry(
            "weapon.kalis.l3",
            2,
            "Kalis — Thrusting Blade",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.NotApplicable,
            "Fully wavy form. Iconic in the later kris record but, per " +
            "that record itself, not the most common form there either. " +
            "Provisional reconstruction; never a pawn-scale battle " +
            "silhouette (R-W1.4) — inspector/armory-card text only.",
            VisualDetailTier.High),
        PawnWeaponRole.Kalis,
        PawnSelectable: false);

    /// <summary>
    /// Every declared Kalis silhouette, in catalog order. <c>l2</c> and
    /// <c>l3</c> are listed for iteration and inspector text only — see
    /// their own remarks for why the pawn stream never reaches them.
    /// </summary>
    public static IReadOnlyList<WeaponSilhouetteEntry> KalisSilhouettes { get; } =
    [
        KalisL1,
        KalisL2,
        KalisL3,
    ];

    // ================= Kalis tints =================

    /// <summary>
    /// The fresh-iron tint: an iron-blue-black blade (identical to today's
    /// unconditional weapon blade color, matching <see cref="DyePalette.IronBlueBlack"/>)
    /// over a warm-ochre grip. Presentation-only; no historical claim.
    /// </summary>
    public static readonly WeaponTintEntry KalisTintFreshIron = new(
        new VisualCatalogEntry(
            "weapon.kalis.tint.freshIron",
            0,
            "Kalis — Thrusting Blade",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Iron-blue-black " +
            "blade, warm-ochre grip — a fresh-iron material read, not a " +
            "distinct weapon.",
            VisualDetailTier.Low),
        PawnWeaponRole.Kalis,
        BladeColor: DyePalette.IronBlueBlack,
        GripColor: GripWarmOchre);

    /// <summary>
    /// The dark-hilt tint: the same iron-blue-black blade over a
    /// charred-wood grip. Presentation-only; no historical claim.
    /// </summary>
    public static readonly WeaponTintEntry KalisTintDarkHilt = new(
        new VisualCatalogEntry(
            "weapon.kalis.tint.darkHilt",
            1,
            "Kalis — Thrusting Blade",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Iron-blue-black " +
            "blade, charred-wood hilt — a dark-hilt material read, not a " +
            "distinct weapon.",
            VisualDetailTier.Low),
        PawnWeaponRole.Kalis,
        BladeColor: DyePalette.IronBlueBlack,
        GripColor: CharredWoodBrown);

    private static readonly IReadOnlyList<WeaponTintEntry> KalisTints =
    [
        KalisTintFreshIron,
        KalisTintDarkHilt,
    ];

    // ================= Kampilan silhouettes (VIS-011) =================

    /// <summary>
    /// K1 — the bounded 1521 period silhouette. The only pawn-scale Kampilan
    /// silhouette; <see cref="PawnSilhouette"/> always returns this entry for
    /// <see cref="PawnWeaponRole.Kampilan"/>. The Medium-tier forward-
    /// widening geometry refinement proposed as OD-W1-c is deferred this
    /// pass rather than shipped: the blade draws at today's uniform width at
    /// every detail tier, and the possibility stays recorded here rather
    /// than in <c>PawnGeometry</c>.
    /// </summary>
    public static readonly WeaponSilhouetteEntry KampilanK1 = new(
        new VisualCatalogEntry(
            "weapon.kampilan.k1",
            0,
            "Kampilan — Great Blade",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "Mactan, 1521. Pigafetta records a large cutting sword used " +
            "against Magellan's landing party, but no local name for it; " +
            "kampilan is attached to this blade class by later tradition " +
            "(the shape itself is Documented, form uncertain). The " +
            "widening profile and truncated tip documented on later " +
            "objects are not drawn here — OD-W1-c's Medium-tier " +
            "forward-widening refinement is deferred; K1 draws at today's " +
            "uniform blade width at every tier. The only pawn-scale " +
            "Kampilan silhouette (R-W1.4); see k2 for the later " +
            "ornamented form, inspector/armory-card only.",
            VisualDetailTier.Low),
        PawnWeaponRole.Kampilan,
        PawnSelectable: true);

    /// <summary>
    /// K2 — the later ornamented form. Provisional reconstruction for any
    /// 1500s presence; never a pawn-scale battle silhouette (R-W1.4). This
    /// entry exists so the inspector can honestly say the ornamented form
    /// exists and is later; the selection stream can never return it.
    /// </summary>
    public static readonly WeaponSilhouetteEntry KampilanK2 = new(
        new VisualCatalogEntry(
            "weapon.kampilan.k2",
            1,
            "Kampilan — Great Blade",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.NotApplicable,
            "Later ornamented form: bifurcated pommel, tip spikelet, hair " +
            "tassels, and chain-mail guards are documented only on " +
            "18th-19th century objects, and the pommel creature motifs " +
            "are culture-specific in the later record. Provisional " +
            "reconstruction; never a pawn-scale battle silhouette " +
            "(R-W1.4) — inspector/armory-card text only in this pass " +
            "(OD-W1-a). If an armory-card drawing is ever made, any " +
            "pommel detail drawn is the plain non-zoomorphic curve, " +
            "never a creature motif.",
            VisualDetailTier.High),
        PawnWeaponRole.Kampilan,
        PawnSelectable: false);

    /// <summary>
    /// Every declared Kampilan silhouette, in catalog order. <c>k2</c> is
    /// listed for iteration and inspector text only — see its own remarks
    /// for why the pawn stream never reaches it.
    /// </summary>
    public static IReadOnlyList<WeaponSilhouetteEntry> KampilanSilhouettes { get; } =
    [
        KampilanK1,
        KampilanK2,
    ];

    // ================= Kampilan tints (VIS-011) =================

    /// <summary>
    /// The fresh-iron tint: an iron-blue-black blade over a charred-wood
    /// hilt. Presentation-only; no historical claim.
    /// </summary>
    public static readonly WeaponTintEntry KampilanTintFreshIron = new(
        new VisualCatalogEntry(
            "weapon.kampilan.tint.freshIron",
            0,
            "Kampilan — Great Blade",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Iron-blue-black " +
            "blade, charred-wood hilt — a fresh-iron material read, not a " +
            "distinct weapon.",
            VisualDetailTier.Low),
        PawnWeaponRole.Kampilan,
        BladeColor: DyePalette.IronBlueBlack,
        GripColor: CharredWoodBrown);

    /// <summary>
    /// The worn-iron tint: a worn-iron-grey blade over the same
    /// charred-wood hilt. Presentation-only; no historical claim.
    /// </summary>
    public static readonly WeaponTintEntry KampilanTintWornIron = new(
        new VisualCatalogEntry(
            "weapon.kampilan.tint.wornIron",
            1,
            "Kampilan — Great Blade",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Worn-iron-grey " +
            "blade, charred-wood hilt — a worn-iron material read, not a " +
            "distinct weapon.",
            VisualDetailTier.Low),
        PawnWeaponRole.Kampilan,
        BladeColor: IronWornGrey,
        GripColor: CharredWoodBrown);

    /// <summary>
    /// The ochre-hilt tint: the fresh-iron blade over a palm-rattan-ochre
    /// hilt. Presentation-only; no historical claim.
    /// </summary>
    public static readonly WeaponTintEntry KampilanTintOchreHilt = new(
        new VisualCatalogEntry(
            "weapon.kampilan.tint.ochreHilt",
            2,
            "Kampilan — Great Blade",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Iron-blue-black " +
            "blade, palm-rattan-ochre hilt — an ochre-hilt material read, " +
            "not a distinct weapon.",
            VisualDetailTier.Low),
        PawnWeaponRole.Kampilan,
        BladeColor: DyePalette.IronBlueBlack,
        GripColor: PalmRattanOchre);

    private static readonly IReadOnlyList<WeaponTintEntry> KampilanTints =
    [
        KampilanTintFreshIron,
        KampilanTintWornIron,
        KampilanTintOchreHilt,
    ];

    // ================= Wasay silhouette (VIS-011) =================

    /// <summary>
    /// W1 — the broad iron head, short hardwood haft silhouette. The only
    /// pawn-scale Wasay silhouette; <see cref="PawnSilhouette"/> always
    /// returns this entry for <see cref="PawnWeaponRole.Wasay"/>. The
    /// Cordilleran head axe (research W2) is regionally specific,
    /// late-documented, and excluded from this roster entirely (R-W1.4) —
    /// it receives no catalog identifier at all, so no ID exists here for
    /// any fallback to ever reach.
    /// </summary>
    public static readonly WeaponSilhouetteEntry WasayW1 = new(
        new VisualCatalogEntry(
            "weapon.wasay.w1",
            0,
            "Wasay — War Axe",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.NotApplicable,
            "A hafted battle axe attested among Tausug and Ibanag groups; " +
            "no sixteenth-century lexical attestation was located " +
            "(Documented, form uncertain). Broad iron head, short " +
            "hardwood haft — an everyman's tool-weapon, not elite kit. " +
            "The only pawn-scale Wasay silhouette (R-W1.4); the " +
            "Cordilleran head axe (research W2) is excluded from this " +
            "roster entirely and carries no catalog identifier.",
            VisualDetailTier.Low),
        PawnWeaponRole.Wasay,
        PawnSelectable: true);

    /// <summary>
    /// Every declared Wasay silhouette, in catalog order. Exactly one entry:
    /// the head axe (research W2) is deliberately never catalogued at all
    /// (R-W1.4), not merely excluded from the pawn stream.
    /// </summary>
    public static IReadOnlyList<WeaponSilhouetteEntry> WasaySilhouettes { get; } =
    [
        WasayW1,
    ];

    // ================= Wasay tints (VIS-011) =================

    /// <summary>
    /// The ochre-haft tint: an iron-blue-black head over a
    /// palm-rattan-ochre haft. Presentation-only; no historical claim.
    /// </summary>
    public static readonly WeaponTintEntry WasayTintOchreHaft = new(
        new VisualCatalogEntry(
            "weapon.wasay.tint.ochreHaft",
            0,
            "Wasay — War Axe",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Iron-blue-black " +
            "head, palm-rattan-ochre haft — a fresh-haft material read, " +
            "not a distinct weapon.",
            VisualDetailTier.Low),
        PawnWeaponRole.Wasay,
        BladeColor: DyePalette.IronBlueBlack,
        GripColor: PalmRattanOchre);

    /// <summary>
    /// The charred-haft tint: the same iron-blue-black head over a
    /// charred-wood haft. Presentation-only; no historical claim.
    /// </summary>
    public static readonly WeaponTintEntry WasayTintCharredHaft = new(
        new VisualCatalogEntry(
            "weapon.wasay.tint.charredHaft",
            1,
            "Wasay — War Axe",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Iron-blue-black " +
            "head, charred-wood haft — a dark-haft material read, not a " +
            "distinct weapon.",
            VisualDetailTier.Low),
        PawnWeaponRole.Wasay,
        BladeColor: DyePalette.IronBlueBlack,
        GripColor: CharredWoodBrown);

    /// <summary>
    /// The lashed-worn tint: a worn-iron-grey head over a
    /// palm-rattan-ochre haft, plus the rattan lashing band accent at the
    /// head-haft junction (R-W1.5). Rattan lashing is a ubiquitous
    /// documented hafting technique that asserts nothing specific about
    /// this object; the band's color is presentation-only, and the band
    /// itself draws at Medium/High detail tier only — never at Low
    /// (<c>PawnRenderer</c> gates it through <c>DetailTierGate</c>).
    /// </summary>
    public static readonly WeaponTintEntry WasayTintLashedWorn = new(
        new VisualCatalogEntry(
            "weapon.wasay.tint.lashedWorn",
            2,
            "Wasay — War Axe",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Worn-iron-grey " +
            "head, palm-rattan-ochre haft, plus a rattan lashing band at " +
            "the head-haft junction — rattan lashing is a ubiquitous " +
            "documented hafting technique that asserts nothing specific " +
            "about this object. The band draws at Medium/High detail " +
            "tier only (R-W1.5), never at Low, so it reads as a band, " +
            "not as damage or a new weapon part.",
            VisualDetailTier.Medium),
        PawnWeaponRole.Wasay,
        BladeColor: IronWornGrey,
        GripColor: PalmRattanOchre,
        LashingBandColor: RattanLashingTone);

    private static readonly IReadOnlyList<WeaponTintEntry> WasayTints =
    [
        WasayTintOchreHaft,
        WasayTintCharredHaft,
        WasayTintLashedWorn,
    ];

    // ================= Itak silhouette (VIS-011) =================

    /// <summary>
    /// I1 — the short, broad, plain work blade. The only pawn-scale Itak
    /// silhouette and the only one that will ever exist here: the research
    /// carries nothing beyond this composite, and inventing additional
    /// "historical" itak variants is exactly what the research forbids.
    /// </summary>
    public static readonly WeaponSilhouetteEntry ItakI1 = new(
        new VisualCatalogEntry(
            "weapon.itak.i1",
            0,
            "Itak — Work Blade",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.NotApplicable,
            "Broad-blade class Documented, form uncertain from " +
            "Legazpi-era relations (1565-1569); the itak name's period " +
            "attestation remains unconfirmed, so the whole entry is a " +
            "composite Provisional reconstruction. The plainest " +
            "silhouette in the roster; no other forms exist or are " +
            "invented.",
            VisualDetailTier.Low),
        PawnWeaponRole.Itak,
        PawnSelectable: true);

    /// <summary>
    /// Every declared Itak silhouette, in catalog order. Exactly one entry —
    /// the research supports nothing more.
    /// </summary>
    public static IReadOnlyList<WeaponSilhouetteEntry> ItakSilhouettes { get; } =
    [
        ItakI1,
    ];

    // ================= Itak tints (VIS-011) =================

    /// <summary>
    /// The plain-ochre tint: an iron-blue-black blade over a warm-ochre
    /// grip. Presentation-only; no historical claim.
    /// </summary>
    public static readonly WeaponTintEntry ItakTintPlainOchre = new(
        new VisualCatalogEntry(
            "weapon.itak.tint.plainOchre",
            0,
            "Itak — Work Blade",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Iron-blue-black " +
            "blade, warm-ochre grip — a plain, everyday material read, " +
            "not a distinct weapon.",
            VisualDetailTier.Low),
        PawnWeaponRole.Itak,
        BladeColor: DyePalette.IronBlueBlack,
        GripColor: GripWarmOchre);

    /// <summary>
    /// The worn-field tint: a worn-iron-grey blade over a
    /// palm-rattan-ochre grip — the "used-up tool" read.
    /// Presentation-only; no historical claim.
    /// </summary>
    public static readonly WeaponTintEntry ItakTintWornField = new(
        new VisualCatalogEntry(
            "weapon.itak.tint.wornField",
            1,
            "Itak — Work Blade",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "Presentation-only; no historical claim. Worn-iron-grey " +
            "blade, palm-rattan-ochre grip — the \"used-up tool\" read, " +
            "not a distinct weapon.",
            VisualDetailTier.Low),
        PawnWeaponRole.Itak,
        BladeColor: IronWornGrey,
        GripColor: PalmRattanOchre);

    private static readonly IReadOnlyList<WeaponTintEntry> ItakTints =
    [
        ItakTintPlainOchre,
        ItakTintWornField,
    ];

    // ================= Fallback step 3: model-category default =================

    /// <summary>
    /// The generic bladed-weapon drawable (fallback chain step 3,
    /// <see cref="VisualFallbackStep.ModelCategoryDefault"/>,
    /// visual-system-integration-design.md section 4, R-W6.4): iron-blue-
    /// black blade, warm-ochre grip, Kalis-class proportions, per the
    /// weapon design's own description of this step. Used by
    /// <see cref="SelectTint"/> whenever a weapon's own tint list is empty.
    /// Every <see cref="PawnWeaponRole"/> ships its own tints as of VIS-011,
    /// so this step is unreachable by <see cref="SelectTint"/> for any
    /// defined role today — a real, distinct, testable chain step kept
    /// alive by the fallback tests' delegate doubles, exactly like
    /// <c>ShieldVisualCatalog.Default</c> records for its own family-default
    /// step. A single shared static instance, never rebuilt per call, so
    /// resolving every pawn every frame (<c>PawnAppearanceFactory.Create</c>)
    /// stays allocation-free in steady state.
    /// </summary>
    public static readonly WeaponTintEntry ModelCategoryDefaultTint = new(
        new VisualCatalogEntry(
            "weapon.category.bladedDefault",
            0,
            "Kalis — Thrusting Blade",
            VisualEvidenceTier.PresentationOnly,
            VisualScopeTag.NotApplicable,
            "The generic bladed-weapon fallback (model-category default, " +
            "fallback chain step 3): iron-blue-black blade, warm-ochre " +
            "grip, Kalis-class proportions. Used only when a weapon's own " +
            "catalog entries have not shipped yet or fail to resolve.",
            VisualDetailTier.Low),
        PawnWeaponRole.Kalis,
        BladeColor: DyePalette.IronBlueBlack,
        GripColor: GripWarmOchre);

    // ================= Lookups and selection =================

    /// <summary>
    /// The pawn-scale silhouette entry for <paramref name="weapon"/>. Every
    /// <see cref="PawnWeaponRole"/> resolves to its own catalog entry as of
    /// VIS-011 — <see cref="KalisL1"/>, <see cref="KampilanK1"/>,
    /// <see cref="WasayW1"/>, or <see cref="ItakI1"/> — always the same
    /// entry for a given role: the silhouette stream is degenerate this
    /// pass (exactly one selectable entry per weapon), so this is a direct
    /// lookup rather than a modulo pick. The nullable return type stays for
    /// a future weapon whose catalog has not shipped yet, even though no
    /// defined <see cref="PawnWeaponRole"/> reaches that case today.
    /// </summary>
    public static WeaponSilhouetteEntry? PawnSilhouette(PawnWeaponRole weapon) =>
        weapon switch
        {
            PawnWeaponRole.Kampilan => KampilanK1,
            PawnWeaponRole.Wasay => WasayW1,
            PawnWeaponRole.Kalis => KalisL1,
            PawnWeaponRole.Itak => ItakI1,
            _ => throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null),
        };

    /// <summary>
    /// Every declared tint for <paramref name="weapon"/>, in catalog (and
    /// selection-index) order. Every <see cref="PawnWeaponRole"/> ships its
    /// own tints as of VIS-011, so this is never empty for a defined role
    /// today. A pre-built, shared array reference; never allocates.
    /// </summary>
    public static IReadOnlyList<WeaponTintEntry> GetTints(PawnWeaponRole weapon) =>
        weapon switch
        {
            PawnWeaponRole.Kampilan => KampilanTints,
            PawnWeaponRole.Wasay => WasayTints,
            PawnWeaponRole.Kalis => KalisTints,
            PawnWeaponRole.Itak => ItakTints,
            _ => throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null),
        };

    /// <summary>
    /// The deterministic tint-selection stream (R-W1.3, R-W6.2): a pure,
    /// salted, allocation-free function of <paramref name="entityId"/> and
    /// <paramref name="weapon"/>, stable across frames and replays. Mixes
    /// <paramref name="entityId"/> XOR <see cref="PresentationSalts.WeaponTintSalt"/>
    /// through the SplitMix64 finalizer (the same pattern
    /// <c>PawnAppearanceFactory.Mix</c> uses, duplicated here per this
    /// codebase's own convention of one local mixer per presentation-salt
    /// consumer) and reduces the result modulo the weapon's tint count.
    /// Falls through to <see cref="ModelCategoryDefaultTint"/> — fallback
    /// step 3 — whenever the weapon has no tints (unreachable for any
    /// defined <see cref="PawnWeaponRole"/> today, but kept as the total
    /// fallback totality requires); never throws, never returns
    /// <see langword="null"/>. Weapon *identity* is untouched: this
    /// only ever selects among the tints of the weapon the caller already
    /// named, exactly as <paramref name="weapon"/> was resolved from the
    /// authoritative Core loadout upstream.
    /// </summary>
    public static WeaponTintEntry SelectTint(ulong entityId, PawnWeaponRole weapon)
    {
        var tints = GetTints(weapon);
        if (tints.Count == 0)
        {
            return ModelCategoryDefaultTint;
        }

        var mixed = Mix(entityId ^ PresentationSalts.WeaponTintSalt);
        var index = (int)(mixed % (ulong)tints.Count);
        return tints[index];
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
