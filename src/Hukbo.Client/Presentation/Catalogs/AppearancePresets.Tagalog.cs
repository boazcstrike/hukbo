namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// The Tagalog preset block — fifteen presets, TAG-01 through TAG-15
/// (warrior-appearance-design.md "Preset roster", Tagalog block table as
/// revised per RF-03; implementation-plan-draft.md VIS-021) — shipped as its
/// own file exactly per <see cref="AppearancePresets"/>'s own class remarks
/// ("Registration mechanism, for VIS-020/021/022"): this file owns its own
/// <c>internal static class AppearancePresetsTagalog</c> with its own
/// <see cref="All"/>, parallel-safe against the sibling Visayan (VIS-020) and
/// Northern-Luzon/remaining-levy/block-assignment (VIS-022) tasks. Neither
/// <see cref="AppearancePresets.All"/> nor
/// <see cref="AppearancePresets"/>'s private <c>BlockAssignmentTable</c> is
/// touched here — VIS-022 unions every block's own roster into those two
/// places as its own later, sequenced edit.
///
/// <b>The chief and the red honor system.</b> TAG-13 (chief) is the only
/// preset in this block, or in the whole shipped roster so far, to render
/// <see cref="AppearanceComponentCatalog.TorsoD3ChininaRedChiefly"/> — the
/// research's Tagalog red-chinina headman marker (Morga, 1609). No other
/// Tagalog preset renders D3, and the block carries no tattoo component at
/// all (I1/I2 are Visayan-only, prohibition 2). Keeping the Tagalog red
/// chinina apart from the separate, and separately excluded, Visayan red
/// head-wrap honor mark (C2, OD-5) is prohibition 3 in the design's
/// "Regional grouping and the prohibitions" list, and this block's own
/// single D3 usage is what keeps that prohibition true by construction
/// rather than by a rule a test has to police after the fact.
///
/// <b>TAG-14's prosperous-freeman carve-out (RF-03).</b> The design's
/// prohibition 6 restricts every gold component (C3, I4, I5, and E2's gold
/// accent) to presets whose scope column marks elite, chief, leader, or —
/// as the single named exception — a "prosperous-freeman" row carrying
/// exactly one I4 accent and nothing else gold. TAG-14 is that row: its
/// recipe carries <see cref="AppearanceComponentCatalog.AdornmentI4GoldEarrings"/>
/// alone (no I5, no C3, no E2), matching the revised design table exactly
/// ("its E2 replaced by E1").
///
/// <b>TAG-12's single-Armor-slot resolution.</b> The design table's TAG-12
/// row reads "C5 + F5 shell-set helmet cap, F3 over D1" — naming both F3
/// (Hide Corselet) and F5 (Shell-Set Helmet), both category-F Armor entries.
/// <see cref="AppearancePresetRecipe"/> (defined in
/// <c>AppearancePresets.Levy.cs</c>, not owned by this task) carries exactly
/// one optional <see cref="AppearancePresetRecipe.Armor"/> slot, so the two
/// cannot both render on one preset. This block resolves the conflict by
/// scoping F3 to TAG-10 and TAG-11 (the row pair the design table itself
/// labels "(veteran)") and reserving F5 — the rarer of the two per its own
/// catalog remarks ("use on at most one or two presets — an exceptional
/// item in a single report family") — for TAG-12 alone (the row the table
/// labels "(rarity)"), rather than rendering neither or duplicating F3 a
/// third time. This is also load-bearing for the differentiation criterion:
/// TAG-10, TAG-11, and TAG-12 would otherwise collide on every field but
/// Armor, and TAG-10 versus a hypothetical F3-bearing TAG-12 would be
/// recipe-identical (zero category differences), which fails
/// <see cref="AppearancePresetValidator.SatisfiesDifferentiation"/>
/// outright. Both F3 and F5 ship as real, rendered recipe components across
/// the block's veteran-adjacent rows, exactly as the VIS-021 task spec
/// requires ("including the F3 hide corselet and F5 shell-set cap
/// components on the veteran rows") — just not both on the same preset.
/// </summary>
internal static class AppearancePresetsTagalog
{
    /// <summary>
    /// PROVISIONAL (R-W3.14, "target at most roughly 2% each"): the
    /// selection weight every non-chief, non-leader Tagalog preset carries.
    /// Paired with <see cref="EliteRarityWeight"/>, thirteen common rows at
    /// this weight plus two elite rows (TAG-13, TAG-15) at weight 1 give the
    /// elite rows roughly a 2% share of the weighted pool walk in
    /// <see cref="AppearancePresets.SelectPreset"/> for either loadout pool
    /// this block contributes to (14 Tagalog rows for a non-Wasay weapon:
    /// 12 &#215; 4 + 2 &#215; 1 = 50, so 1/50 = 2.0%; all 15 rows for Wasay:
    /// 13 &#215; 4 + 2 &#215; 1 = 54, so 1/54 &#8776; 1.85%). Not a
    /// historical measurement — a gameplay-feel tuning choice, matching the
    /// design's own "PROVISIONAL" caveat for rarity weights.
    /// </summary>
    private const int CommonRarityWeight = 4;

    /// <summary>
    /// PROVISIONAL (R-W3.14): the small selection weight TAG-13 (chief) and
    /// TAG-15 (leader) carry — see <see cref="CommonRarityWeight"/>'s remarks
    /// for the resulting share arithmetic.
    /// </summary>
    private const int EliteRarityWeight = 1;

    // ================= Tagalog block (15 presets) =================

    /// <summary>
    /// TAG-01. B1 knotted hair, C5 bare head, D2 chinina (indigo), E1 bahag,
    /// G2 cloth belt, K1 clean. Weakest-link tier: Documented, form
    /// uncertain (G2). The block's own family default and fallback target,
    /// itself falling back to the generic-levy LEV-01 (design: "block bases
    /// fall back to LEV-01").
    /// </summary>
    public static readonly AppearancePresetEntry Tag01 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag01",
            0,
            "Tagalog Warrior",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "TAG-01: B1 (Long Hair, Knotted), C5 (Bare Head), D2 (Chinina " +
            "— Collarless Jacket, indigo), E1 (Bahag — Loincloth), G2 " +
            "(Cloth Belt), K1 (Clean). Tagalog scope; the block's own base " +
            "recipe. Weakest-link tier Documented, form uncertain (G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD2ChininaIndigoOrBlack,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLevy.lev01",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-02. B4 hair tucked under wrap, C1 putong (cream), D2 chinina
    /// (indigo), E1 bahag, G2 cloth belt, K1 clean. Weakest-link tier:
    /// Documented, form uncertain (G2).
    /// </summary>
    public static readonly AppearancePresetEntry Tag02 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag02",
            1,
            "Tagalog Warrior, Head Wrap",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "TAG-02: B4 (Hair Tucked Under Head Wrap), C1 (Putong — Head " +
            "Wrap, undyed cream), D2 (Chinina — Collarless Jacket, " +
            "indigo), E1 (Bahag — Loincloth), G2 (Cloth Belt), K1 " +
            "(Clean). Weakest-link tier Documented, form uncertain (G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD2ChininaIndigoOrBlack,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag01",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-03. B1 knotted hair, C5 bare head, D2 chinina (blue-black), E1
    /// bahag, G3 cord belt, K2 dusty. Weakest-link tier: Provisional
    /// reconstruction (G3).
    /// </summary>
    public static readonly AppearancePresetEntry Tag03 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag03",
            2,
            "Tagalog Warrior, Cord Belt",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.Tagalog,
            "TAG-03: B1 (Long Hair, Knotted), C5 (Bare Head), D2 (Chinina " +
            "— Collarless Jacket, blue-black), E1 (Bahag — Loincloth), G3 " +
            "(Cord Belt), K2 (Dusty / Muddy). Weakest-link tier " +
            "Provisional reconstruction (G3).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD2ChininaIndigoOrBlack,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag01",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-04. B3 cropped hair, C5 bare head, D2 chinina (blue-black), E1
    /// bahag, G2 cloth belt, K1 clean. Weakest-link tier: Documented, form
    /// uncertain (B3, G2).
    /// </summary>
    public static readonly AppearancePresetEntry Tag04 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag04",
            3,
            "Tagalog Warrior, Cropped Hair",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "TAG-04: B3 (Cropped Hair), C5 (Bare Head), D2 (Chinina — " +
            "Collarless Jacket, blue-black), E1 (Bahag — Loincloth), G2 " +
            "(Cloth Belt), K1 (Clean). Weakest-link tier Documented, form " +
            "uncertain (B3, G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB3Cropped,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD2ChininaIndigoOrBlack,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag01",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-05. B1 knotted hair, C5 bare head, D1 bare chest, E1 bahag, G2
    /// cloth belt, K2 dusty. Weakest-link tier: Documented, form uncertain
    /// (G2). A second block-local base — TAG-06, TAG-09, TAG-10 fall back
    /// here rather than to TAG-01.
    /// </summary>
    public static readonly AppearancePresetEntry Tag05 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag05",
            4,
            "Tagalog Warrior, Bare-Chested",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "TAG-05: B1 (Long Hair, Knotted), C5 (Bare Head), D1 (Bare-" +
            "Chested), E1 (Bahag — Loincloth), G2 (Cloth Belt), K2 (Dusty " +
            "/ Muddy). Weakest-link tier Documented, form uncertain (G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag01",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-06. B4 hair tucked under wrap, C1 putong, D1 bare chest, E1
    /// bahag, G2 cloth belt, K1 clean. Weakest-link tier: Documented, form
    /// uncertain (G2).
    /// </summary>
    public static readonly AppearancePresetEntry Tag06 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag06",
            5,
            "Tagalog Warrior, Bare-Chested with Head Wrap",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "TAG-06: B4 (Hair Tucked Under Head Wrap), C1 (Putong — Head " +
            "Wrap), D1 (Bare-Chested), E1 (Bahag — Loincloth), G2 (Cloth " +
            "Belt), K1 (Clean). Weakest-link tier Documented, form " +
            "uncertain (G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag05",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-07. B4 hair tucked under wrap, C1 putong (indigo), D2 chinina
    /// (indigo), E1 bahag, G3 cord belt, K4 faded dye. Weakest-link tier:
    /// Provisional reconstruction (G3).
    /// </summary>
    public static readonly AppearancePresetEntry Tag07 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag07",
            6,
            "Tagalog Warrior, Faded Dye",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.Tagalog,
            "TAG-07: B4 (Hair Tucked Under Head Wrap), C1 (Putong — Head " +
            "Wrap, indigo), D2 (Chinina — Collarless Jacket, indigo), E1 " +
            "(Bahag — Loincloth), G3 (Cord Belt), K4 (Faded Dye). " +
            "Weakest-link tier Provisional reconstruction (G3).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD2ChininaIndigoOrBlack,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK4FadedDye),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag02",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-08. B1 knotted hair, C5 bare head, D2 chinina (indigo), E1
    /// bahag, H1 sheathed side blade, G2 cloth belt, K2 dusty. Wasay-only
    /// (H1 restriction). Weakest-link tier: Documented, form uncertain
    /// (G2).
    /// </summary>
    public static readonly AppearancePresetEntry Tag08 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag08",
            7,
            "Tagalog Warrior, Sheathed Side Blade",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "TAG-08: B1 (Long Hair, Knotted), C5 (Bare Head), D2 (Chinina " +
            "— Collarless Jacket, indigo), E1 (Bahag — Loincloth), H1 " +
            "(Sheathed Side Blade), G2 (Cloth Belt), K2 (Dusty / Muddy). " +
            "Wasay-armed pawns only — H1 is restricted to figures whose " +
            "main weapon is not a blade. Weakest-link tier Documented, " +
            "form uncertain (G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD2ChininaIndigoOrBlack,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy,
            Accessory: AppearanceComponentCatalog.AccessoryH1SheathedSideBlade),
        AppearancePresetLoadoutCompatibility.WasayOnly,
        FallbackId: "appearance.presetTagalog.tag01",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-09. B3 cropped hair, C5 bare head, D1 bare chest, E1 bahag, G3
    /// cord belt, K4 faded dye. Weakest-link tier: Provisional
    /// reconstruction (G3).
    /// </summary>
    public static readonly AppearancePresetEntry Tag09 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag09",
            8,
            "Tagalog Warrior, Cropped Hair and Cord Belt",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.Tagalog,
            "TAG-09: B3 (Cropped Hair), C5 (Bare Head), D1 (Bare-Chested), " +
            "E1 (Bahag — Loincloth), G3 (Cord Belt), K4 (Faded Dye). " +
            "Weakest-link tier Provisional reconstruction (G3).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB3Cropped,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK4FadedDye),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag05",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-10. B1 knotted hair, C5 bare head, F3 hide corselet over D1 bare
    /// chest, E1 bahag, G2 cloth belt, K2 dusty. Tagalog (veteran).
    /// Weakest-link tier: Documented, form uncertain (F3, G2).
    /// </summary>
    public static readonly AppearancePresetEntry Tag10 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag10",
            9,
            "Tagalog Veteran, Hide Corselet",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "TAG-10: B1 (Long Hair, Knotted), C5 (Bare Head), F3 (Hide " +
            "Corselet) over D1 (Bare-Chested), E1 (Bahag — Loincloth), G2 " +
            "(Cloth Belt), K2 (Dusty / Muddy). Tagalog (veteran). " +
            "Weakest-link tier Documented, form uncertain (F3, G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy,
            Armor: AppearanceComponentCatalog.ArmorF3HideCorselet),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag05",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-11. B4 hair tucked under wrap, C1 putong, F3 hide corselet over
    /// D1 bare chest, E1 bahag, G2 cloth belt, K5 battle-worn. Tagalog
    /// (veteran). Weakest-link tier: Documented, form uncertain (F3, G2).
    /// </summary>
    public static readonly AppearancePresetEntry Tag11 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag11",
            10,
            "Tagalog Veteran, Head Wrap and Hide Corselet",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "TAG-11: B4 (Hair Tucked Under Head Wrap), C1 (Putong — Head " +
            "Wrap), F3 (Hide Corselet) over D1 (Bare-Chested), E1 (Bahag " +
            "— Loincloth), G2 (Cloth Belt), K5 (Battle-Worn). Tagalog " +
            "(veteran). Weakest-link tier Documented, form uncertain (F3, " +
            "G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK5BattleWorn,
            Armor: AppearanceComponentCatalog.ArmorF3HideCorselet),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag10",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-12. B1 knotted hair, C5 bare head, F5 shell-set helmet cap over
    /// D1 bare chest, E1 bahag, G2 cloth belt, K2 dusty. Tagalog (rarity) —
    /// the sole shipped user of F5 (see this file's class remarks, "TAG-12's
    /// single-Armor-slot resolution", for why F3 is not also rendered here).
    /// Weakest-link tier: Documented, form uncertain (F5, G2).
    /// </summary>
    public static readonly AppearancePresetEntry Tag12 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag12",
            11,
            "Tagalog Veteran, Shell-Set Helmet",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "TAG-12: B1 (Long Hair, Knotted), C5 (Bare Head) + F5 (Shell-" +
            "Set Helmet) over D1 (Bare-Chested), E1 (Bahag — Loincloth), " +
            "G2 (Cloth Belt), K2 (Dusty / Muddy). Tagalog (rarity) — an " +
            "exceptional item in a single report family, used on this one " +
            "preset only. Weakest-link tier Documented, form uncertain " +
            "(F5, G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy,
            Armor: AppearanceComponentCatalog.ArmorF5ShellSetHelmet),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag10",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-13. B4 hair tucked under wrap, C3 gold-edged putong, D3 red
    /// chinina (chiefly), E2 gold-edged bahag, I4 gold earrings + I5 gold
    /// necklace, G2 cloth belt, K1 clean. Tagalog (chief) — the roster's
    /// sole D3 user (prohibition 3). Weakest-link tier: Documented, form
    /// uncertain (G2).
    /// </summary>
    public static readonly AppearancePresetEntry Tag13 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag13",
            12,
            "Tagalog Chief",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "TAG-13: B4 (Hair Tucked Under Head Wrap), C3 (Putong — Head " +
            "Wrap, Gold-Edged), D3 (Chinina — Collarless Jacket, Red — " +
            "Chiefly), E2 (Bahag — Loincloth, Richly Dyed), I4 (Gold " +
            "Earrings) + I5 (Gold Necklace), G2 (Cloth Belt), K1 (Clean). " +
            "Tagalog (chief). The sole shipped preset rendering D3 — keeps " +
            "the Tagalog red-chinina headman marker apart from the " +
            "separate, excluded Visayan red head-wrap honor system " +
            "(prohibition 3). Weakest-link tier Documented, form uncertain " +
            "(G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged,
            TorsoGarment: AppearanceComponentCatalog.TorsoD3ChininaRedChiefly,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE2DyedGoldEdged,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean,
            Adornments: [
                AppearanceComponentCatalog.AdornmentI4GoldEarrings,
                AppearanceComponentCatalog.AdornmentI5GoldNecklace,
            ]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag02",
        RarityWeight: EliteRarityWeight,
        Status: AppearancePresetStatus.Leader);

    /// <summary>
    /// TAG-14. B1 knotted hair, C1 putong (cream), D2 chinina (indigo), E1
    /// bahag, I4 gold earrings (single accent only), G2 cloth belt, K1
    /// clean. Tagalog (prosperous-freeman) — the RF-03 carve-out: exactly
    /// one gold accent (I4) and no other gold component, its E2 replaced by
    /// E1 as the revised design table specifies. Weakest-link tier:
    /// Documented, form uncertain (G2).
    /// </summary>
    public static readonly AppearancePresetEntry Tag14 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag14",
            13,
            "Tagalog Warrior, Prosperous Freeman",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "TAG-14: B1 (Long Hair, Knotted), C1 (Putong — Head Wrap, " +
            "undyed cream), D2 (Chinina — Collarless Jacket, indigo), E1 " +
            "(Bahag — Loincloth), I4 (Gold Earrings), G2 (Cloth Belt), K1 " +
            "(Clean). Tagalog (prosperous-freeman), revised per RF-03: the " +
            "single I4 accent only, no I5, no C3, no E2 — the roster's one " +
            "prosperous-freeman carve-out to prohibition 6's elite/chief/" +
            "leader-only gold rule. Weakest-link tier Documented, form " +
            "uncertain (G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD2ChininaIndigoOrBlack,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean,
            Adornments: [AppearanceComponentCatalog.AdornmentI4GoldEarrings]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag02",
        RarityWeight: CommonRarityWeight);

    /// <summary>
    /// TAG-15. B4 hair tucked under wrap, C3 gold-edged putong, D2 chinina
    /// (indigo), E3 waist cloth, H2 draped shoulder cloth, I4 gold earrings
    /// + I5 gold necklace, G2 cloth belt, K1 clean. Tagalog (leader).
    /// Weakest-link tier: Documented, form uncertain (G2, H2).
    /// </summary>
    public static readonly AppearancePresetEntry Tag15 = new(
        new VisualCatalogEntry(
            "appearance.presetTagalog.tag15",
            14,
            "Tagalog Leader",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Tagalog,
            "TAG-15: B4 (Hair Tucked Under Head Wrap), C3 (Putong — Head " +
            "Wrap, Gold-Edged), D2 (Chinina — Collarless Jacket, indigo), " +
            "E3 (Waist Cloth), H2 (Draped Shoulder Cloth), I4 (Gold " +
            "Earrings) + I5 (Gold Necklace), G2 (Cloth Belt), K1 (Clean). " +
            "Tagalog (leader/datu). Weakest-link tier Documented, form " +
            "uncertain (G2, H2).",
            VisualDetailTier.Low),
        VisualScopeTag.Tagalog,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged,
            TorsoGarment: AppearanceComponentCatalog.TorsoD2ChininaIndigoOrBlack,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE3WaistCloth,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean,
            Accessory: AppearanceComponentCatalog.AccessoryH2DrapedShoulderCloth,
            Adornments: [
                AppearanceComponentCatalog.AdornmentI4GoldEarrings,
                AppearanceComponentCatalog.AdornmentI5GoldNecklace,
            ]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetTagalog.tag13",
        RarityWeight: EliteRarityWeight,
        Status: AppearancePresetStatus.Leader);

    /// <summary>
    /// Every preset this block ships, in design-table order (TAG-01 through
    /// TAG-15). VIS-022 unions this list into
    /// <see cref="AppearancePresets.All"/>; nothing in this file does that
    /// itself.
    /// </summary>
    public static IReadOnlyList<AppearancePresetEntry> All { get; } =
    [
        Tag01,
        Tag02,
        Tag03,
        Tag04,
        Tag05,
        Tag06,
        Tag07,
        Tag08,
        Tag09,
        Tag10,
        Tag11,
        Tag12,
        Tag13,
        Tag14,
        Tag15,
    ];
}
