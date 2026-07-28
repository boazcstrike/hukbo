namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// The Northern Luzon preset block — eight presets, LUZ-01 through LUZ-08
/// (warrior-appearance-design.md "Preset roster", "Northern Luzon block (8
/// presets, scope tag: Cagayan)"; implementation-plan-draft.md VIS-022) —
/// shipped as its own file exactly per <see cref="AppearancePresets"/>'s own
/// class remarks ("Registration mechanism, for VIS-020/021/022"): this file
/// owns its own <c>internal static class AppearancePresetsNorthernLuzon</c>
/// with its own <see cref="All"/>, parallel-safe against the sibling Visayan
/// (VIS-020) and Tagalog (VIS-021) blocks, both already landed. Unlike those
/// two sibling tasks, VIS-022 is also the single, later, sequenced task that
/// unions every block's own roster — this one included — into
/// <see cref="AppearancePresets.All"/> and completes
/// <see cref="AppearancePresets"/>'s private block-assignment table; that
/// wiring lives in <c>AppearancePresets.Levy.cs</c>, not here.
///
/// <b>Regional discipline (design: "No tattoo tone, no putong, no gold
/// ensemble — the block follows the Boxer Codex Cagayan and Zambal
/// silhouettes only").</b> No row below references
/// <see cref="AppearanceComponentCatalog.HeadCoveringC1PutongPlain"/>,
/// <see cref="AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged"/>, any
/// category-I adornment, or any gold-bearing component. Head covering is
/// exactly <see cref="AppearanceComponentCatalog.HeadCoveringC5BareHead"/> or
/// <see cref="AppearanceComponentCatalog.HeadCoveringC6FeatheredHeaddress"/>
/// throughout.
///
/// <b>C6 exclusivity (prohibition 1).</b> LUZ-02, LUZ-04, and LUZ-08 are the
/// only presets in the whole shipped roster — across every block — that
/// render <see cref="AppearanceComponentCatalog.HeadCoveringC6FeatheredHeaddress"/>,
/// matching the catalog entry's own remark ("must never appear on a Visayan
/// or Tagalog preset") and the design's "the clearest cross-regional mashup
/// hazard in the whole system".
///
/// <b>LUZ-05's Zambal reference.</b> The design table's scope column reads
/// "Cagayan (Zambal-referenced)" for LUZ-05 — this row substitutes the B1
/// knotted-hair silhouette (documented pan-archipelago, see
/// <see cref="AppearanceComponentCatalog.HairB1LongHairKnotted"/>) for the
/// block's usual B2 loose hair, honoring the research's Zambal cross-
/// reference without inventing a component the catalog does not carry. It
/// still carries <see cref="VisualScopeTag.Cagayan"/> — <see cref="VisualScopeTag"/>
/// has no separate Zambal member, and the design does not ask for one.
///
/// <b>LUZ-07's rare veteran armor.</b> "(rare veteran)" in the design's scope
/// column describes <see cref="AppearanceComponentCatalog.ArmorF4WoodenBreastplate"/>'s
/// own rarity note ("Rare; must not combine with invented European-style
/// pauldrons or full coverage"), not an elite or leader status marker in the
/// sense R-W3.14 uses for rarity weighting — the design names no elite,
/// chief, datu, or leader row in this block, unlike the Visayan and Tagalog
/// tables. Every row here therefore carries the record's own default
/// <see cref="AppearancePresetEntry.RarityWeight"/> of 1, the same uniform
/// treatment the Tagalog block gave its own "(veteran)"-labeled rows
/// (TAG-10/11/12, none of which carry the small elite/leader weight either).
///
/// <b>LUZ-08's H1 restriction.</b> The block's sole
/// <see cref="AppearancePresetLoadoutCompatibility.WasayOnly"/> row, matching
/// the design table's "Wasay only" column and the H1/loadout combination rule
/// <see cref="AppearancePresetValidator"/> enforces.
///
/// <b>OD-3 (resolved 2026-07-28).</b> This file adds no Mindanao- or
/// Sulu-flavored preset — the Unscoped-generic levy block remains the sole
/// coverage for those regions this pass, per the accepted open decision.
/// </summary>
internal static class AppearancePresetsNorthernLuzon
{
    /// <summary>
    /// LUZ-01. B2 long loose hair, C5 bare head, D1 bare chest, E1 undyed
    /// bahag, G2 cloth belt, K1 clean. The block's own family default — see
    /// <see cref="AppearancePresetEntry.FallbackId"/>'s Levy-file remarks —
    /// so it falls back to <see cref="AppearancePresets.Lev01"/> rather than
    /// to another Northern Luzon row (design: "block bases fall back to
    /// LEV-01"). Weakest-link tier: Documented, form uncertain (G2).
    /// </summary>
    public static readonly AppearancePresetEntry Luz01 = new(
        new VisualCatalogEntry(
            "appearance.presetLuzon.luz01",
            0,
            "Cagayan Warrior",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Cagayan,
            "LUZ-01: B2 (Long Loose Hair), C5 (Bare Head), D1 (Bare-" +
            "Chested), E1 (Bahag — Loincloth, undyed cream), G2 (Cloth " +
            "Belt), K1 (Clean). The Northern Luzon block's own family " +
            "default. Weakest-link tier Documented, form uncertain (G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Cagayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB2LongLooseHair,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLevy.lev01");

    /// <summary>
    /// LUZ-02. B2, C6 feathered headdress, D1, E1, G2, K1. The block's only
    /// other bare-headed-versus-headdress base row; every other C6 preset in
    /// this block falls back to this one. Weakest-link tier: Documented, form
    /// uncertain (C6, G2).
    /// </summary>
    public static readonly AppearancePresetEntry Luz02 = new(
        new VisualCatalogEntry(
            "appearance.presetLuzon.luz02",
            1,
            "Cagayan Warrior, Feathered Headdress",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Cagayan,
            "LUZ-02: B2 (Long Loose Hair), C6 (Feathered Headdress), D1 " +
            "(Bare-Chested), E1 (Bahag — Loincloth), G2 (Cloth Belt), K1 " +
            "(Clean). C6 must never appear on a Visayan or Tagalog preset " +
            "— the clearest cross-regional mashup hazard in the whole " +
            "system. Weakest-link tier Documented, form uncertain (C6, " +
            "G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Cagayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB2LongLooseHair,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC6FeatheredHeaddress,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLuzon.luz01");

    /// <summary>
    /// LUZ-03. B2, C5, D1, E1, G3 cord belt, K2 dusty. Weakest-link tier:
    /// Provisional reconstruction (G3).
    /// </summary>
    public static readonly AppearancePresetEntry Luz03 = new(
        new VisualCatalogEntry(
            "appearance.presetLuzon.luz03",
            2,
            "Cagayan Warrior, Cord Belt",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.Cagayan,
            "LUZ-03: B2 (Long Loose Hair), C5 (Bare Head), D1 (Bare-" +
            "Chested), E1 (Bahag — Loincloth), G3 (Cord Belt), K2 (Dusty " +
            "/ Muddy). Weakest-link tier Provisional reconstruction (G3).",
            VisualDetailTier.Low),
        VisualScopeTag.Cagayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB2LongLooseHair,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLuzon.luz01");

    /// <summary>
    /// LUZ-04. B2, C6 feathered headdress, D1, E1, G3 cord belt, K5
    /// battle-worn. Weakest-link tier: Provisional reconstruction (G3).
    /// </summary>
    public static readonly AppearancePresetEntry Luz04 = new(
        new VisualCatalogEntry(
            "appearance.presetLuzon.luz04",
            3,
            "Cagayan Warrior, Battle-Worn Headdress",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.Cagayan,
            "LUZ-04: B2 (Long Loose Hair), C6 (Feathered Headdress), D1 " +
            "(Bare-Chested), E1 (Bahag — Loincloth), G3 (Cord Belt), K5 " +
            "(Battle-Worn). Weakest-link tier Provisional reconstruction " +
            "(G3).",
            VisualDetailTier.Low),
        VisualScopeTag.Cagayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB2LongLooseHair,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC6FeatheredHeaddress,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK5BattleWorn),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLuzon.luz02");

    /// <summary>
    /// LUZ-05. B1 knotted hair, C5, D1, E1, G2, K2 dusty. The design table's
    /// scope column reads "Cagayan (Zambal-referenced)" — see the class
    /// remarks. Weakest-link tier: Documented, form uncertain (G2).
    /// </summary>
    public static readonly AppearancePresetEntry Luz05 = new(
        new VisualCatalogEntry(
            "appearance.presetLuzon.luz05",
            4,
            "Cagayan Warrior, Zambal-Referenced",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Cagayan,
            "LUZ-05: B1 (Long Hair, Knotted), C5 (Bare Head), D1 (Bare-" +
            "Chested), E1 (Bahag — Loincloth), G2 (Cloth Belt), K2 (Dusty " +
            "/ Muddy). Zambal-referenced per the design table's own scope " +
            "note; carries VisualScopeTag.Cagayan — there is no separate " +
            "Zambal scope member. Weakest-link tier Documented, form " +
            "uncertain (G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Cagayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLuzon.luz01");

    /// <summary>
    /// LUZ-06. B3 cropped hair, C5, D1, E1, G3 cord belt, K1 clean.
    /// Weakest-link tier: Provisional reconstruction (G3).
    /// </summary>
    public static readonly AppearancePresetEntry Luz06 = new(
        new VisualCatalogEntry(
            "appearance.presetLuzon.luz06",
            5,
            "Cagayan Warrior, Cropped Hair",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.Cagayan,
            "LUZ-06: B3 (Cropped Hair), C5 (Bare Head), D1 (Bare-Chested), " +
            "E1 (Bahag — Loincloth), G3 (Cord Belt), K1 (Clean). Weakest-" +
            "link tier Provisional reconstruction (G3).",
            VisualDetailTier.Low),
        VisualScopeTag.Cagayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB3Cropped,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLuzon.luz01");

    /// <summary>
    /// LUZ-07. B2, C5, F4 wooden breastplate over D1 bare chest, E1, G2, K2
    /// dusty. The design table's scope column reads "Cagayan (rare
    /// veteran)" — see the class remarks on why this does not carry the
    /// small elite/leader rarity weight. Weakest-link tier: Documented, form
    /// uncertain (F4, G2).
    /// </summary>
    public static readonly AppearancePresetEntry Luz07 = new(
        new VisualCatalogEntry(
            "appearance.presetLuzon.luz07",
            6,
            "Cagayan Warrior, Wooden Breastplate",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Cagayan,
            "LUZ-07: B2 (Long Loose Hair), C5 (Bare Head), F4 (Wooden " +
            "Breastplate) over D1 (Bare-Chested), E1 (Bahag — Loincloth), " +
            "G2 (Cloth Belt), K2 (Dusty / Muddy). Rare veteran kit — F4's " +
            "own catalog note calls it rare; must not issue broadly. " +
            "Weakest-link tier Documented, form uncertain (F4, G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Cagayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB2LongLooseHair,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy,
            Armor: AppearanceComponentCatalog.ArmorF4WoodenBreastplate),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLuzon.luz01");

    /// <summary>
    /// LUZ-08. B2, C6 feathered headdress, D1, E1, H1 sheathed side blade,
    /// G2, K2 dusty. The block's sole Wasay-only row (H1's own loadout
    /// rule). Weakest-link tier: Documented, form uncertain (C6, G2).
    /// </summary>
    public static readonly AppearancePresetEntry Luz08 = new(
        new VisualCatalogEntry(
            "appearance.presetLuzon.luz08",
            7,
            "Cagayan Warrior, Side Blade",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Cagayan,
            "LUZ-08: B2 (Long Loose Hair), C6 (Feathered Headdress), D1 " +
            "(Bare-Chested), E1 (Bahag — Loincloth), H1 (Sheathed Side " +
            "Blade), G2 (Cloth Belt), K2 (Dusty / Muddy). Wasay-armed " +
            "only (H1's own rule). Weakest-link tier Documented, form " +
            "uncertain (C6, G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Cagayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB2LongLooseHair,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC6FeatheredHeaddress,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy,
            Accessory: AppearanceComponentCatalog.AccessoryH1SheathedSideBlade),
        AppearancePresetLoadoutCompatibility.WasayOnly,
        FallbackId: "appearance.presetLuzon.luz02");

    /// <summary>
    /// Every Northern Luzon preset, in the design table's own LUZ-01..08
    /// order. VIS-022 unions this list into
    /// <see cref="AppearancePresets.All"/> in the same edit that adds
    /// LEV-05..08/10 and completes the block-assignment table.
    /// </summary>
    public static IReadOnlyList<AppearancePresetEntry> All { get; } =
    [
        Luz01,
        Luz02,
        Luz03,
        Luz04,
        Luz05,
        Luz06,
        Luz07,
        Luz08,
    ];
}
