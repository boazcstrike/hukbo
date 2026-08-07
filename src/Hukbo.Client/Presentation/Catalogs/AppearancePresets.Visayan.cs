namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// The Visayan preset block — twenty presets, VIS-01 through VIS-20, exactly
/// as tabled in warrior-appearance-design.md's "Visayan block (20 presets,
/// scope tag: Visayan)" (implementation-plan-draft.md VIS-020). Ships as its
/// own file exposing its own <c>internal static class</c> with its own
/// <see cref="All"/> property, per the registration mechanism
/// <see cref="AppearancePresets"/>'s own class remarks document for
/// VIS-020/021/022 — parallel-safe alongside the Tagalog block (VIS-021) and
/// the Northern Luzon block plus faction assignment (VIS-022). This file
/// intentionally does not touch <c>AppearancePresets.Levy.cs</c>,
/// <see cref="AppearancePresets.All"/>, or the block-assignment table —
/// VIS-022 is the single, later, sequenced task that unions every block's
/// own roster there.
///
/// <b>RF-03 revision applied:</b> VIS-18 carries the explicit
/// "prosperous-freeman" scope marker with the single I4 (Gold Earrings)
/// accent only — its would-be E2 (richly dyed bahag) is replaced by the
/// plain E1 bahag, matching the revised design table row exactly
/// (implementation-plan-draft.md VIS-020 step 1).
///
/// <b>OD-5 (excluded):</b> C2, the earned red head wrap, ships in no row of
/// this block — the design table never names it and this file never
/// references <see cref="AppearanceComponentCatalog.HeadCoveringC1PutongPlain"/>'s
/// absent C2 sibling.
///
/// <b>Historical scope discipline:</b> every row below depicts the Visayas
/// specifically, never generalized to "the Philippines" — the region name in
/// each <see cref="VisualCatalogEntry.DisplayLabel"/> and the
/// <see cref="VisualScopeTag.Visayan"/> tag on every
/// <see cref="AppearancePresetEntry.Block"/>/<see cref="VisualCatalogEntry.ScopeTag"/>
/// pair are what keep that claim honest and inspector-visible.
/// </summary>
internal static class AppearancePresetsVisayan
{
    /// <summary>
    /// The selection weight every ordinary (non-elite, non-leader) Visayan
    /// preset carries. <see cref="AppearancePresetEntry.RarityWeight"/> has a
    /// hard floor of 1 (its own validator: "RarityWeight must be at least
    /// 1"), so the only way to make VIS-13/14/15 read as rare next to the
    /// other seventeen rows is to raise every ordinary row's own weight
    /// instead of lowering the rare ones' — this constant is that raise.
    /// PROVISIONAL: a gameplay-feel tuning choice, not a historical
    /// measurement, chosen so <see cref="EliteOrLeaderRarityWeight"/> lands
    /// at roughly 1% selection probability per row within this block's own
    /// loadout-filtered pool (seventeen rows at this weight plus three rows
    /// at the floor gives each elite/leader row 1 / (17 * 6 + 3) ≈ 0.95%),
    /// comfortably inside the design's "target at most roughly 2% each"
    /// (warrior-appearance-design.md step 2; R-W3.14).
    /// </summary>
    private const int StandardRarityWeight = 6;

    /// <summary>
    /// The selection weight VIS-13, VIS-14, and VIS-15 (the block's elite and
    /// datu/leader rows) carry — the floor <see cref="AppearancePresetEntry.RarityWeight"/>
    /// allows, made "small" only relative to <see cref="StandardRarityWeight"/>
    /// on every other row sharing this block's selection pool. PROVISIONAL
    /// (R-W3.14): see <see cref="StandardRarityWeight"/>'s remarks for the
    /// resulting probability.
    /// </summary>
    private const int EliteOrLeaderRarityWeight = 1;

    /// <summary>
    /// VIS-01. B1 knotted hair, C5 bare head, D1 bare chest, E1 undyed
    /// bahag, G2 cloth belt, K1 clean. The block's own family default — see
    /// <see cref="AppearancePresetEntry.FallbackId"/>'s Levy-file remarks —
    /// so it falls back to <see cref="AppearancePresets.Lev01"/> rather than
    /// to another Visayan row (design: "block bases fall back to LEV-01").
    /// Weakest-link tier: Documented, form uncertain (G2).
    /// </summary>
    public static readonly AppearancePresetEntry Vis01 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis01",
            0,
            "Visayan Warrior",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-01: B1 (Long Hair, Knotted), C5 (Bare Head), D1 (Bare-" +
            "Chested), E1 (Bahag — Loincloth, undyed cream), G2 (Cloth " +
            "Belt), K1 (Clean). The Visayan block's own family default. " +
            "Weakest-link tier Documented, form uncertain (G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLevy.lev01",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-02. B1, C5, D1 with I2 partial tattoos, E1, G3 cord belt, K1
    /// clean. Weakest-link tier: Provisional reconstruction (G3).
    /// </summary>
    public static readonly AppearancePresetEntry Vis02 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis02",
            1,
            "Visayan Warrior, Partial Tattoos",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.Visayan,
            "VIS-02: B1 (Long Hair, Knotted), C5 (Bare Head), D1 (Bare-" +
            "Chested) + I2 (Partial Tattoos), E1 (Bahag — Loincloth), G3 " +
            "(Cord Belt), K1 (Clean). Visayas-only tattoo scope (I2) — must " +
            "never appear on a Tagalog, Cagayan, or generic preset. " +
            "Weakest-link tier Provisional reconstruction (G3).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean,
            Adornments: [AppearanceComponentCatalog.AdornmentI2PartialTattoos]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis01",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-03. B1, C5, D1 with I1 full-body tattoos, E1, G1 red waist sash,
    /// K2 dusty. Weakest-link tier: Documented, form uncertain (G1).
    /// </summary>
    public static readonly AppearancePresetEntry Vis03 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis03",
            2,
            "Visayan Warrior, Full Tattoos",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-03: B1 (Long Hair, Knotted), C5 (Bare Head), D1 (Bare-" +
            "Chested) + I1 (Full-Body Tattoos), E1 (Bahag — Loincloth), G1 " +
            "(Red Waist Sash), K2 (Dusty / Muddy). Visayas-only tattoo " +
            "scope (I1), never presented as decoration divorced from its " +
            "earned meaning. Weakest-link tier Documented, form uncertain " +
            "(G1).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG1RedWaistSash,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy,
            Adornments: [AppearanceComponentCatalog.AdornmentI1FullBodyTattoos]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis01",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-04. B4 tucked hair, C1 putong (undyed cream), D1 with I2 partial
    /// tattoos, E1, G2, K1. Weakest-link tier: Documented, form uncertain
    /// (G2, I2).
    /// </summary>
    public static readonly AppearancePresetEntry Vis04 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis04",
            3,
            "Visayan Warrior, Head Wrap",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-04: B4 (Hair Tucked Under Head Wrap), C1 (Putong — Head " +
            "Wrap, undyed cream), D1 (Bare-Chested) + I2 (Partial " +
            "Tattoos), E1 (Bahag — Loincloth), G2 (Cloth Belt), K1 " +
            "(Clean). Weakest-link tier Documented, form uncertain (G2, " +
            "I2).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean,
            Adornments: [AppearanceComponentCatalog.AdornmentI2PartialTattoos]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis01",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-05. B4, C1 putong (indigo), D1 with I1 full tattoos, E1, G1 red
    /// waist sash, K1. Weakest-link tier: Documented, form uncertain (G1).
    /// </summary>
    public static readonly AppearancePresetEntry Vis05 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis05",
            4,
            "Visayan Warrior, Head Wrap and Tattoos",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-05: B4 (Hair Tucked Under Head Wrap), C1 (Putong — Head " +
            "Wrap, indigo), D1 (Bare-Chested) + I1 (Full-Body Tattoos), E1 " +
            "(Bahag — Loincloth), G1 (Red Waist Sash), K1 (Clean). " +
            "Weakest-link tier Documented, form uncertain (G1).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG1RedWaistSash,
            Condition: AppearanceComponentCatalog.ConditionK1Clean,
            Adornments: [AppearanceComponentCatalog.AdornmentI1FullBodyTattoos]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis04",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-06. B4, C1 putong (blue-black), D1, E1, G3 cord belt, K5
    /// battle-worn. Weakest-link tier: Provisional reconstruction (G3).
    /// </summary>
    public static readonly AppearancePresetEntry Vis06 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis06",
            5,
            "Visayan Warrior, Battle-Worn Head Wrap",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.Visayan,
            "VIS-06: B4 (Hair Tucked Under Head Wrap), C1 (Putong — Head " +
            "Wrap, blue-black), D1 (Bare-Chested), E1 (Bahag — " +
            "Loincloth), G3 (Cord Belt), K5 (Battle-Worn). Weakest-link " +
            "tier Provisional reconstruction (G3).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK5BattleWorn),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis04",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-07. B3 cropped hair, C5, D1 with I2 partial tattoos, E1, G2, K2
    /// dusty. Weakest-link tier: Documented, form uncertain (B3, G2, I2).
    /// </summary>
    public static readonly AppearancePresetEntry Vis07 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis07",
            6,
            "Visayan Warrior, Cropped Hair and Tattoos",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-07: B3 (Cropped Hair), C5 (Bare Head), D1 (Bare-Chested) " +
            "+ I2 (Partial Tattoos), E1 (Bahag — Loincloth), G2 (Cloth " +
            "Belt), K2 (Dusty / Muddy). Weakest-link tier Documented, " +
            "form uncertain (B3, G2, I2).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB3Cropped,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy,
            Adornments: [AppearanceComponentCatalog.AdornmentI2PartialTattoos]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis01",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-08. B1, C5, D4 abaca jacket, E1, G2, K1. Weakest-link tier:
    /// Documented, form uncertain (D4, G2).
    /// </summary>
    public static readonly AppearancePresetEntry Vis08 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis08",
            7,
            "Visayan Warrior, Abaca Jacket",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-08: B1 (Long Hair, Knotted), C5 (Bare Head), D4 (Abaca " +
            "Jacket), E1 (Bahag — Loincloth), G2 (Cloth Belt), K1 " +
            "(Clean). Must default to undyed abaca — silk versions were " +
            "elite trade goods. Weakest-link tier Documented, form " +
            "uncertain (D4, G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD4AbacaJacket,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis01",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-09. B4, C1 putong (undyed cream), D4 abaca jacket, E1, G2, K1.
    /// Weakest-link tier: Documented, form uncertain (D4, G2).
    /// </summary>
    public static readonly AppearancePresetEntry Vis09 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis09",
            8,
            "Visayan Warrior, Abaca Jacket and Head Wrap",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-09: B4 (Hair Tucked Under Head Wrap), C1 (Putong — Head " +
            "Wrap, undyed cream), D4 (Abaca Jacket), E1 (Bahag — " +
            "Loincloth), G2 (Cloth Belt), K1 (Clean). Weakest-link tier " +
            "Documented, form uncertain (D4, G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD4AbacaJacket,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis08",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-10. B1, C5, D4 abaca jacket, E1, G3 cord belt, K4 faded dye.
    /// Weakest-link tier: Provisional reconstruction (G3).
    /// </summary>
    public static readonly AppearancePresetEntry Vis10 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis10",
            9,
            "Visayan Warrior, Faded Abaca Jacket",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.Visayan,
            "VIS-10: B1 (Long Hair, Knotted), C5 (Bare Head), D4 (Abaca " +
            "Jacket), E1 (Bahag — Loincloth), G3 (Cord Belt), K4 (Faded " +
            "Dye). Weakest-link tier Provisional reconstruction (G3).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD4AbacaJacket,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK4FadedDye),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis08",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-11. B4, C1 putong (undyed cream), F2 corded fiber armor over D1
    /// bare chest with I2 partial tattoos (arms), E1, G2, K2 dusty.
    /// Weakest-link tier: Documented, form uncertain (F2, G2, I2).
    /// </summary>
    public static readonly AppearancePresetEntry Vis11 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis11",
            10,
            "Visayan Warrior, Corded Fiber Armor",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-11: B4 (Hair Tucked Under Head Wrap), C1 (Putong — Head " +
            "Wrap, undyed cream), F2 (Corded Fiber Armor) over D1 (Bare-" +
            "Chested) + I2 (Partial Tattoos, arms), E1 (Bahag — " +
            "Loincloth), G2 (Cloth Belt), K2 (Dusty / Muddy). Must not " +
            "issue F2 broadly — a minority kit. Weakest-link tier " +
            "Documented, form uncertain (F2, G2, I2).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy,
            Armor: AppearanceComponentCatalog.ArmorF2CordedFiberArmor,
            Adornments: [AppearanceComponentCatalog.AdornmentI2PartialTattoos]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis04",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-12. B1, C5, F2 corded fiber armor over D1 bare chest with I1 full
    /// tattoos (arms), E1, G1 red waist sash, K5 battle-worn. Weakest-link
    /// tier: Documented, form uncertain (F2, G1).
    /// </summary>
    public static readonly AppearancePresetEntry Vis12 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis12",
            11,
            "Visayan Warrior, Battle-Worn Corded Fiber Armor",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-12: B1 (Long Hair, Knotted), C5 (Bare Head), F2 (Corded " +
            "Fiber Armor) over D1 (Bare-Chested) + I1 (Full-Body " +
            "Tattoos, arms), E1 (Bahag — Loincloth), G1 (Red Waist " +
            "Sash), K5 (Battle-Worn). Weakest-link tier Documented, form " +
            "uncertain (F2, G1).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG1RedWaistSash,
            Condition: AppearanceComponentCatalog.ConditionK5BattleWorn,
            Armor: AppearanceComponentCatalog.ArmorF2CordedFiberArmor,
            Adornments: [AppearanceComponentCatalog.AdornmentI1FullBodyTattoos]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis01",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-13. B4, C3 gold-edged putong, D1 with I1 full tattoos, I4 gold
    /// earrings, E2 richly dyed gold-edged bahag, G1 red waist sash, K1
    /// clean. Elite row: carries <see cref="EliteOrLeaderRarityWeight"/>.
    /// Weakest-link tier: Documented, form uncertain (G1).
    /// </summary>
    public static readonly AppearancePresetEntry Vis13 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis13",
            12,
            "Visayan Elite, Gold-Edged Head Wrap",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-13: B4 (Hair Tucked Under Head Wrap), C3 (Putong — Head " +
            "Wrap, Gold-Edged), D1 (Bare-Chested) + I1 (Full-Body " +
            "Tattoos), I4 (Gold Earrings), E2 (Bahag — Loincloth, Richly " +
            "Dyed), G1 (Red Waist Sash), K1 (Clean). Elite scope — gold " +
            "and dyed cloth density, never a larger figure. Weakest-link " +
            "tier Documented, form uncertain (G1).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE2DyedGoldEdged,
            SashBelt: AppearanceComponentCatalog.SashBeltG1RedWaistSash,
            Condition: AppearanceComponentCatalog.ConditionK1Clean,
            Adornments:
            [
                AppearanceComponentCatalog.AdornmentI1FullBodyTattoos,
                AppearanceComponentCatalog.AdornmentI4GoldEarrings,
            ]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis05",
        RarityWeight: EliteOrLeaderRarityWeight);

    /// <summary>
    /// VIS-14. B4, C3 gold-edged putong, D1 with I1 full tattoos, I4 gold
    /// earrings and I5 gold necklace, E2 richly dyed gold-edged bahag, G1 red
    /// waist sash, H1 sheathed side blade, K1 clean. Elite row, restricted to
    /// Wasay-armed pawns (H1's own loadout rule) and carries
    /// <see cref="EliteOrLeaderRarityWeight"/>. Weakest-link tier: Documented,
    /// form uncertain (G1).
    /// </summary>
    public static readonly AppearancePresetEntry Vis14 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis14",
            13,
            "Visayan Elite, Side Blade",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-14: B4 (Hair Tucked Under Head Wrap), C3 (Putong — Head " +
            "Wrap, Gold-Edged), D1 (Bare-Chested) + I1 (Full-Body " +
            "Tattoos), I4 (Gold Earrings) + I5 (Gold Necklace), E2 " +
            "(Bahag — Loincloth, Richly Dyed), G1 (Red Waist Sash), H1 " +
            "(Sheathed Side Blade), K1 (Clean). Wasay-armed only (H1's " +
            "own rule). Elite scope. Weakest-link tier Documented, form " +
            "uncertain (G1).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE2DyedGoldEdged,
            SashBelt: AppearanceComponentCatalog.SashBeltG1RedWaistSash,
            Condition: AppearanceComponentCatalog.ConditionK1Clean,
            Accessory: AppearanceComponentCatalog.AccessoryH1SheathedSideBlade,
            Adornments:
            [
                AppearanceComponentCatalog.AdornmentI1FullBodyTattoos,
                AppearanceComponentCatalog.AdornmentI4GoldEarrings,
                AppearanceComponentCatalog.AdornmentI5GoldNecklace,
            ]),
        AppearancePresetLoadoutCompatibility.WasayOnly,
        FallbackId: "appearance.presetVisayan.vis13",
        RarityWeight: EliteOrLeaderRarityWeight);

    /// <summary>
    /// VIS-15. B4, C3 gold-edged putong, D1 with I1 full tattoos, I4 gold
    /// earrings and I5 gold necklace, E3 waist cloth, H2 draped shoulder
    /// cloth, G1 red waist sash, K1 clean. Datu/leader row: carries
    /// <see cref="EliteOrLeaderRarityWeight"/>. Weakest-link tier: Documented,
    /// form uncertain (G1, H2).
    /// </summary>
    public static readonly AppearancePresetEntry Vis15 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis15",
            14,
            "Visayan Datu",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-15: B4 (Hair Tucked Under Head Wrap), C3 (Putong — Head " +
            "Wrap, Gold-Edged), D1 (Bare-Chested) + I1 (Full-Body " +
            "Tattoos), I4 (Gold Earrings) + I5 (Gold Necklace), E3 " +
            "(Waist Cloth), H2 (Draped Shoulder Cloth), G1 (Red Waist " +
            "Sash), K1 (Clean). Datu/leader scope — must not read as a " +
            "poor man's garment; prefer E3 on leader presets. Weakest-" +
            "link tier Documented, form uncertain (G1, H2).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE3WaistCloth,
            SashBelt: AppearanceComponentCatalog.SashBeltG1RedWaistSash,
            Condition: AppearanceComponentCatalog.ConditionK1Clean,
            Accessory: AppearanceComponentCatalog.AccessoryH2DrapedShoulderCloth,
            Adornments:
            [
                AppearanceComponentCatalog.AdornmentI1FullBodyTattoos,
                AppearanceComponentCatalog.AdornmentI4GoldEarrings,
                AppearanceComponentCatalog.AdornmentI5GoldNecklace,
            ]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis13",
        RarityWeight: EliteOrLeaderRarityWeight,
        Status: AppearancePresetStatus.Leader);

    /// <summary>
    /// VIS-16. B1, C1 putong (undyed cream), D1 with I1 full tattoos, E1,
    /// G2, K1. Weakest-link tier: Documented, form uncertain (G2).
    /// </summary>
    public static readonly AppearancePresetEntry Vis16 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis16",
            15,
            "Visayan Warrior, Head Wrap and Full Tattoos",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-16: B1 (Long Hair, Knotted), C1 (Putong — Head Wrap, " +
            "undyed cream), D1 (Bare-Chested) + I1 (Full-Body Tattoos), " +
            "E1 (Bahag — Loincloth), G2 (Cloth Belt), K1 (Clean). " +
            "Weakest-link tier Documented, form uncertain (G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean,
            Adornments: [AppearanceComponentCatalog.AdornmentI1FullBodyTattoos]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis01",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-17. B3 cropped hair, C1 putong (indigo), D1, E1, G3 cord belt, K2
    /// dusty. Weakest-link tier: Provisional reconstruction (G3).
    /// </summary>
    public static readonly AppearancePresetEntry Vis17 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis17",
            16,
            "Visayan Warrior, Cropped Hair and Head Wrap",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.Visayan,
            "VIS-17: B3 (Cropped Hair), C1 (Putong — Head Wrap, indigo), " +
            "D1 (Bare-Chested), E1 (Bahag — Loincloth), G3 (Cord Belt), " +
            "K2 (Dusty / Muddy). Weakest-link tier Provisional " +
            "reconstruction (G3).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB3Cropped,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis07",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-18. B1, C5, D4 abaca jacket with I4 gold earrings, E1, G1 red
    /// waist sash, K1 clean. The prosperous-freeman row, revised per RF-03:
    /// the single I4 accent only — no I1/I2 tattoo, no E2 dyed bahag — so
    /// gold appears here without the row also claiming elite or datu/leader
    /// status. Weakest-link tier: Documented, form uncertain (D4, G1).
    /// </summary>
    public static readonly AppearancePresetEntry Vis18 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis18",
            17,
            "Visayan Prosperous Freeman",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-18: B1 (Long Hair, Knotted), C5 (Bare Head), D4 (Abaca " +
            "Jacket) + I4 (Gold Earrings), E1 (Bahag — Loincloth), G1 " +
            "(Red Waist Sash), K1 (Clean). Prosperous-freeman scope, " +
            "revised per RF-03: the single I4 accent only, E1 in place of " +
            "the elite E2. Weakest-link tier Documented, form uncertain " +
            "(D4, G1).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD4AbacaJacket,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG1RedWaistSash,
            Condition: AppearanceComponentCatalog.ConditionK1Clean,
            Adornments: [AppearanceComponentCatalog.AdornmentI4GoldEarrings]),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis08",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-19. B4, C1 putong (undyed cream), F2 corded fiber armor over D1
    /// bare chest, E1, G2, K4 faded dye. Weakest-link tier: Documented, form
    /// uncertain (F2, G2).
    /// </summary>
    public static readonly AppearancePresetEntry Vis19 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis19",
            18,
            "Visayan Warrior, Faded Corded Fiber Armor",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.Visayan,
            "VIS-19: B4 (Hair Tucked Under Head Wrap), C1 (Putong — Head " +
            "Wrap, undyed cream), F2 (Corded Fiber Armor) over D1 (Bare-" +
            "Chested), E1 (Bahag — Loincloth), G2 (Cloth Belt), K4 " +
            "(Faded Dye). Weakest-link tier Documented, form uncertain " +
            "(F2, G2).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB4TuckedUnderWrap,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC1PutongPlain,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK4FadedDye,
            Armor: AppearanceComponentCatalog.ArmorF2CordedFiberArmor),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis11",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// VIS-20. B1, C4 woven sun hat, D1, E1, G2, K2 dusty. The levy row —
    /// visually loud C4 kept off every elite/leader row per its own
    /// research note ("never on elite figures"). Weakest-link tier:
    /// Provisional reconstruction (C4).
    /// </summary>
    public static readonly AppearancePresetEntry Vis20 = new(
        new VisualCatalogEntry(
            "appearance.presetVisayan.vis20",
            19,
            "Visayan Levy, Sun Hat",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.Visayan,
            "VIS-20: B1 (Long Hair, Knotted), C4 (Woven Sun Hat), D1 " +
            "(Bare-Chested), E1 (Bahag — Loincloth), G2 (Cloth Belt), K2 " +
            "(Dusty / Muddy). Levy scope — C4's battlefield use is a " +
            "Provisional reconstruction, and it is levy flavor only, " +
            "never an elite or leader row. Weakest-link tier Provisional " +
            "reconstruction (C4).",
            VisualDetailTier.Low),
        VisualScopeTag.Visayan,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC4WovenSunHat,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetVisayan.vis01",
        RarityWeight: StandardRarityWeight);

    /// <summary>
    /// Every Visayan preset, in the design table's own VIS-01..20 order.
    /// VIS-022 unions this list into <see cref="AppearancePresets.All"/>; no
    /// caller in this milestone's roster references this property yet.
    /// </summary>
    public static IReadOnlyList<AppearancePresetEntry> All { get; } =
    [
        Vis01,
        Vis02,
        Vis03,
        Vis04,
        Vis05,
        Vis06,
        Vis07,
        Vis08,
        Vis09,
        Vis10,
        Vis11,
        Vis12,
        Vis13,
        Vis14,
        Vis15,
        Vis16,
        Vis17,
        Vis18,
        Vis19,
        Vis20,
    ];
}
