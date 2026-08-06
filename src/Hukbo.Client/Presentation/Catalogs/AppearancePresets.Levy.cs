using Hukbo.Client.Presentation;

namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// Whether a preset's recipe is safe to draw regardless of the pawn's
/// equipped weapon, or restricted to a specific loadout
/// (warrior-appearance-design.md preset roster: "Presets containing H1 (a
/// sheathed side blade) are restricted to Wasay-armed pawns, because the
/// research scopes H1 to figures whose main weapon is not a blade").
/// </summary>
internal enum AppearancePresetLoadoutCompatibility
{
    /// <summary>Draws for a pawn carrying any of the four weapons.</summary>
    Any,

    /// <summary>
    /// Draws only for a pawn whose <c>PawnWeaponRole</c> is
    /// <see cref="PawnWeaponRole.Wasay"/>. Every preset that renders the H1
    /// sheathed-side-blade accessory must carry this value —
    /// <see cref="AppearancePresetValidator"/> enforces the pairing.
    /// </summary>
    WasayOnly,
}

/// <summary>
/// Which social position a preset row depicts (leader-character-design.md
/// section 4.1, "Gate the leader appearance on actual leadership").
/// <see cref="AppearancePresets.SelectPreset"/> walks a different pool per
/// value instead of one undifferentiated pool, so the rows built for chiefs
/// and datus draw only for the entity the simulation actually elected to
/// lead its contingent.
/// </summary>
internal enum AppearancePresetStatus
{
    /// <summary>
    /// The default, carried by every preset row that ships today unless a
    /// row is explicitly marked <see cref="Leader"/>. Selected when
    /// <c>SelectPreset</c>'s <c>isLeader</c> argument is
    /// <see langword="false"/>.
    /// </summary>
    General,

    /// <summary>
    /// Carried by exactly three rows as of this milestone:
    /// <c>AppearancePresetsVisayan.Vis15</c> ("Visayan Datu"),
    /// <c>AppearancePresetsTagalog.Tag13</c> ("Tagalog Chief"), and
    /// <c>AppearancePresetsTagalog.Tag15</c> ("Tagalog Leader"). Selected
    /// only when <c>SelectPreset</c>'s <c>isLeader</c> argument is
    /// <see langword="true"/>; a block that ships no <see cref="Leader"/>
    /// row (Northern Luzon, generic levy) falls back to the
    /// <see cref="General"/> pool rather than inventing one.
    /// </summary>
    Leader,
}

/// <summary>
/// One preset's authored component recipe: exactly the categories the
/// warrior-appearance research and catalog define (category A stature/build
/// and category J palette are not recipe fields — A stays the independent
/// roll <c>PawnAppearanceFactory</c> already has, and J is folded into
/// whichever component a recipe names, not chosen separately). Six fields are
/// mandatory — every shipped preset renders hair, a head covering, a torso
/// garment, a lower garment, a sash/belt, and a condition — and three are
/// optional, matching the research: armor (F) and the accessory slot (H) are
/// absent from most presets, and adornment (I) is capped at
/// <see cref="AppearanceComponentCatalog.MaxAccentMarksPerPawn"/> entries and
/// empty on every non-elite preset. The constructor rejects a component
/// placed in the wrong field (an <see cref="AppearanceComponentEntry"/> whose
/// own <see cref="AppearanceComponentEntry.Category"/> does not match the
/// field it is assigned to) so a recipe can never silently mix up categories.
/// </summary>
internal sealed record AppearancePresetRecipe(
    AppearanceComponentEntry Hair,
    AppearanceComponentEntry HeadCovering,
    AppearanceComponentEntry TorsoGarment,
    AppearanceComponentEntry LowerGarment,
    AppearanceComponentEntry SashBelt,
    AppearanceComponentEntry Condition,
    AppearanceComponentEntry? Armor = null,
    AppearanceComponentEntry? Accessory = null,
    IReadOnlyList<AppearanceComponentEntry>? Adornments = null)
{
    // Positional-record property re-declarations, mirroring
    // AppearanceComponentEntry's own pattern: each initializer runs once,
    // against the primary constructor parameter of the same name.
    public AppearanceComponentEntry Hair { get; init; } =
        ValidateCategory(Hair, AppearanceComponentCategory.Hair, nameof(Hair));

    public AppearanceComponentEntry HeadCovering { get; init; } =
        ValidateCategory(HeadCovering, AppearanceComponentCategory.HeadCovering, nameof(HeadCovering));

    public AppearanceComponentEntry TorsoGarment { get; init; } =
        ValidateCategory(TorsoGarment, AppearanceComponentCategory.TorsoGarment, nameof(TorsoGarment));

    public AppearanceComponentEntry LowerGarment { get; init; } =
        ValidateCategory(LowerGarment, AppearanceComponentCategory.LowerGarment, nameof(LowerGarment));

    public AppearanceComponentEntry SashBelt { get; init; } =
        ValidateCategory(SashBelt, AppearanceComponentCategory.SashBelt, nameof(SashBelt));

    public AppearanceComponentEntry Condition { get; init; } =
        ValidateCategory(Condition, AppearanceComponentCategory.Condition, nameof(Condition));

    public AppearanceComponentEntry? Armor { get; init; } =
        Armor is null ? null : ValidateCategory(Armor, AppearanceComponentCategory.Armor, nameof(Armor));

    public AppearanceComponentEntry? Accessory { get; init; } =
        Accessory is null ? null : ValidateCategory(Accessory, AppearanceComponentCategory.Accessory, nameof(Accessory));

    public IReadOnlyList<AppearanceComponentEntry> Adornments { get; init; } =
        ValidateAdornments(Adornments);

    private static AppearanceComponentEntry ValidateCategory(
        AppearanceComponentEntry? entry,
        AppearanceComponentCategory expected,
        string paramName)
    {
        ArgumentNullException.ThrowIfNull(entry, paramName);
        if (entry.Category != expected)
        {
            throw new ArgumentException(
                $"'{entry.Catalog.Id}' belongs to category {entry.Category}, not the " +
                $"expected {expected} for recipe field '{paramName}'.",
                paramName);
        }

        return entry;
    }

    private static IReadOnlyList<AppearanceComponentEntry> ValidateAdornments(
        IReadOnlyList<AppearanceComponentEntry>? adornments)
    {
        if (adornments is null)
        {
            return [];
        }

        foreach (var adornment in adornments)
        {
            ValidateCategory(adornment, AppearanceComponentCategory.Adornment, nameof(adornments));
        }

        return adornments;
    }
}

/// <summary>
/// One warrior-appearance preset: the shared <see cref="VisualCatalogEntry"/>
/// shape plus which regional block it belongs to, its authored component
/// recipe, its loadout restriction, its roster fallback, and its selection
/// rarity weight (warrior-appearance-design.md, "Preset roster"; R-W3.2,
/// R-W3.5). <see cref="Catalog"/>'s own <c>ScopeTag</c> is always exactly
/// <see cref="Block"/> — a preset's scope tag *is* the block it belongs to —
/// and the constructor enforces the two never drift apart.
/// </summary>
internal sealed record AppearancePresetEntry(
    VisualCatalogEntry Catalog,
    VisualScopeTag Block,
    AppearancePresetRecipe Recipe,
    AppearancePresetLoadoutCompatibility LoadoutCompatibility,
    string? FallbackId = null,
    int RarityWeight = 1,
    AppearancePresetStatus Status = AppearancePresetStatus.General)
{
    public VisualCatalogEntry Catalog { get; init; } =
        Catalog ?? throw new ArgumentNullException(nameof(Catalog));

    public VisualScopeTag Block { get; init; } = ValidateBlock(Block, Catalog);

    public AppearancePresetRecipe Recipe { get; init; } =
        Recipe ?? throw new ArgumentNullException(nameof(Recipe));

    public AppearancePresetLoadoutCompatibility LoadoutCompatibility { get; init; } =
        ValidateLoadoutCompatibility(LoadoutCompatibility);

    /// <summary>
    /// The catalog identifier of the preset this entry falls back to when it
    /// cannot be resolved, or <see langword="null"/> for a preset — LEV-01
    /// in this milestone's roster — that instead falls back through the
    /// generic chain (<see cref="VisualFallbackStep.ModelCategoryDefault"/>,
    /// then <see cref="VisualFallbackStep.DiagnosticPlaceholder"/>) rather
    /// than to another named preset (warrior-appearance-design.md: "block
    /// bases fall back to LEV-01, and LEV-01 falls back through the full
    /// resolution chain").
    /// </summary>
    public string? FallbackId { get; init; } = ValidateFallbackId(FallbackId);

    /// <summary>
    /// The selection weight this preset carries in
    /// <see cref="AppearancePresets.SelectPreset"/>'s weighted pool walk.
    /// Every preset in the VIS-018 milestone roster is uniform at the
    /// default of 1; the field exists now so VIS-020's elite and leader rows
    /// can carry a small <c>PROVISIONAL</c> rarity weight (target at most
    /// roughly 2% each, R-W3.14) without a breaking change to this record.
    /// </summary>
    public int RarityWeight { get; init; } = ValidateRarityWeight(RarityWeight);

    /// <summary>
    /// Which social position this row depicts — see
    /// <see cref="AppearancePresetStatus"/>. Defaults to
    /// <see cref="AppearancePresetStatus.General"/>; only the three shipped
    /// chief/datu rows carry <see cref="AppearancePresetStatus.Leader"/>.
    /// <see cref="AppearancePresets.SelectPreset"/> walks a pool built from
    /// this field rather than treating every row as interchangeable.
    /// </summary>
    public AppearancePresetStatus Status { get; init; } = ValidateStatus(Status);

    private static VisualScopeTag ValidateBlock(VisualScopeTag block, VisualCatalogEntry catalog)
    {
        if (!Enum.IsDefined(block))
        {
            throw new ArgumentOutOfRangeException(
                nameof(block),
                block,
                "Block must be a defined VisualScopeTag member.");
        }

        if (catalog.ScopeTag != block)
        {
            throw new ArgumentException(
                $"Preset '{catalog.Id}' declares Block={block} but its own " +
                $"Catalog.ScopeTag is {catalog.ScopeTag}; the two must agree " +
                "— a preset's scope tag is its block.",
                nameof(block));
        }

        return block;
    }

    private static AppearancePresetLoadoutCompatibility ValidateLoadoutCompatibility(
        AppearancePresetLoadoutCompatibility loadoutCompatibility)
    {
        if (!Enum.IsDefined(loadoutCompatibility))
        {
            throw new ArgumentOutOfRangeException(
                nameof(loadoutCompatibility),
                loadoutCompatibility,
                "Loadout compatibility must be a defined AppearancePresetLoadoutCompatibility member.");
        }

        return loadoutCompatibility;
    }

    private static string? ValidateFallbackId(string? fallbackId)
    {
        if (fallbackId is not null && string.IsNullOrWhiteSpace(fallbackId))
        {
            throw new ArgumentException(
                "FallbackId must be null or a non-blank catalog identifier.",
                nameof(fallbackId));
        }

        return fallbackId;
    }

    private static int ValidateRarityWeight(int rarityWeight)
    {
        if (rarityWeight < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rarityWeight),
                rarityWeight,
                "RarityWeight must be at least 1.");
        }

        return rarityWeight;
    }

    private static AppearancePresetStatus ValidateStatus(AppearancePresetStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Status must be a defined AppearancePresetStatus member.");
        }

        return status;
    }
}

/// <summary>
/// The <c>appearance.preset*</c> roster (warrior-appearance-design.md,
/// "Preset roster"; implementation-plan-draft.md VIS-018 milestone,
/// VIS-020/021/022 post-milestone) and its two selection streams — block
/// assignment and preset selection (design section "Deterministic
/// selection", R-W3.5). As of VIS-022 the roster is complete: <see cref="All"/>
/// unions all four regional blocks (Visayan, Tagalog, Northern Luzon, then
/// Generic levy — the design's own "Preset roster" section order) for 53
/// total presets, and <see cref="BlockAssignmentTable"/> carries all four
/// blocks a faction's match assignment can draw from (R-W3.2, R-W3.5).
///
/// <b>Generic levy block (VIS-018 shipped LEV-01/02/03/04/09; VIS-022 fills
/// in LEV-05 through LEV-08 and LEV-10, all <c>Wasay only</c> and all
/// carrying H1, at their already-reserved indices).</b> The filter and
/// validator machinery a <c>Wasay only</c> preset needs was already real and
/// tested at the VIS-018 milestone, exercised there with a synthetic H1
/// recipe in <c>AppearancePresetTests.cs</c>; the five real rows below now
/// exercise the same machinery with shipped data.
///
/// <b>Registration mechanism, for VIS-020/021/022:</b> each sibling regional
/// block (Visayan, Tagalog, Northern Luzon) ships as its own file —
/// <c>AppearancePresets.Visayan.cs</c>, <c>AppearancePresets.Tagalog.cs</c>,
/// <c>AppearancePresets.NorthernLuzon.cs</c> — each exposing its own roster
/// as an independent <c>internal static class AppearancePresetsXxx</c> with
/// its own <c>public static IReadOnlyList&lt;AppearancePresetEntry&gt;
/// All</c>. That independence is what made the three tasks parallel-safe:
/// each block's own roster validates, differentiates, and pins its own
/// identifiers against its own <c>.All</c> list in its own test file, with no
/// shared file to conflict over while VIS-020 and VIS-021 were in flight.
/// VIS-022 ("Northern Luzon block, remaining levy presets, and faction block
/// assignment") is the single, later, sequenced task that unions every
/// block's <c>.All</c> into <see cref="All"/> here, adds LEV-05 through
/// LEV-08 and LEV-10 to <em>this</em> file at their already-reserved indices,
/// and grows <see cref="BlockAssignmentTable"/> from the single-entry table
/// VIS-018 shipped to the real four-entry, per-faction, per-match assignment
/// the design describes.
///
/// <b>Same-block-allowed default (VIS-022's blocking decision, resolved by
/// this implementation).</b> The design leaves open whether two factions in
/// the same match may draw the same regional block. <see cref="SelectBlock"/>
/// mixes <c>Scenario.Seed</c> with each faction's own <c>factionId</c>
/// independently — nothing excludes two factions from landing on the same
/// table index — so "same block allowed" (the design's own recommended
/// default) falls out of the existing per-faction mixing with no extra
/// exclusion logic. Levy blending at a fixed ratio (the design's other
/// optional refinement) is not implemented this pass: none of VIS-022's
/// automated verification calls for it, and <see cref="SelectBlock"/> already
/// lets a faction draw the pure Generic-levy block outright via the table's
/// fourth entry, which is the coarsest form of "blended with levy" available
/// without a second selection stream. Recorded here for user review per the
/// task's own instruction.
/// </summary>
internal static class AppearancePresets
{
    // ================= Generic levy block (VIS-018 shipped 5 of 10; VIS-022 completes it) =================

    /// <summary>
    /// LEV-01. B1 knotted hair, C5 bare head, G2 cloth belt, K1 clean.
    /// Weakest-link tier: Documented, form uncertain (G2). The block's own
    /// family default and fallback target — see
    /// <see cref="AppearancePresetEntry.FallbackId"/>'s remarks — so it
    /// falls back through the generic chain rather than to another preset.
    /// </summary>
    public static readonly AppearancePresetEntry Lev01 = new(
        new VisualCatalogEntry(
            "appearance.presetLevy.lev01",
            0,
            "Levy Warrior",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.UnscopedGeneric,
            "LEV-01: B1 (Long Hair, Knotted), C5 (Bare Head), D1 (Bare-" +
            "Chested), E1 (Bahag — Loincloth), G2 (Cloth Belt), K1 (Clean). " +
            "Minimal kit, undyed cloth, no tattoos, no gold, no putong, no " +
            "regional markers — Unscoped-generic scope. Weakest-link tier " +
            "Documented, form uncertain (G2).",
            VisualDetailTier.Low),
        VisualScopeTag.UnscopedGeneric,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean),
        AppearancePresetLoadoutCompatibility.Any);

    /// <summary>
    /// LEV-02. B3 cropped hair, C5 bare head, G3 cord belt, K1 clean.
    /// Weakest-link tier: Provisional reconstruction (G3).
    /// </summary>
    public static readonly AppearancePresetEntry Lev02 = new(
        new VisualCatalogEntry(
            "appearance.presetLevy.lev02",
            1,
            "Levy Warrior, Cropped Hair",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.UnscopedGeneric,
            "LEV-02: B3 (Cropped Hair), C5 (Bare Head), D1 (Bare-Chested), " +
            "E1 (Bahag — Loincloth), G3 (Cord Belt), K1 (Clean). Minimal " +
            "kit, undyed cloth, no tattoos, no gold, no putong — Unscoped-" +
            "generic scope. Weakest-link tier Provisional reconstruction " +
            "(G3).",
            VisualDetailTier.Low),
        VisualScopeTag.UnscopedGeneric,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB3Cropped,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLevy.lev01");

    /// <summary>
    /// LEV-03. B1 knotted hair, C4 woven sun hat, G3 cord belt, K2 dusty.
    /// Weakest-link tier: Provisional reconstruction (C4, battle-wear use;
    /// G3).
    /// </summary>
    public static readonly AppearancePresetEntry Lev03 = new(
        new VisualCatalogEntry(
            "appearance.presetLevy.lev03",
            2,
            "Levy Warrior, Sun Hat",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.UnscopedGeneric,
            "LEV-03: B1 (Long Hair, Knotted), C4 (Woven Sun Hat), D1 (Bare-" +
            "Chested), E1 (Bahag — Loincloth), G3 (Cord Belt), K2 (Dusty / " +
            "Muddy). Levy flavor only — C4's battlefield use is a " +
            "Provisional reconstruction. Weakest-link tier Provisional " +
            "reconstruction (C4, G3).",
            VisualDetailTier.Low),
        VisualScopeTag.UnscopedGeneric,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC4WovenSunHat,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLevy.lev01");

    /// <summary>
    /// LEV-04. B3 cropped hair, C4 woven sun hat, G2 cloth belt, K2 dusty.
    /// Weakest-link tier: Provisional reconstruction (C4).
    /// </summary>
    public static readonly AppearancePresetEntry Lev04 = new(
        new VisualCatalogEntry(
            "appearance.presetLevy.lev04",
            3,
            "Levy Warrior, Cropped Hair and Sun Hat",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.UnscopedGeneric,
            "LEV-04: B3 (Cropped Hair), C4 (Woven Sun Hat), D1 (Bare-" +
            "Chested), E1 (Bahag — Loincloth), G2 (Cloth Belt), K2 (Dusty " +
            "/ Muddy). Levy flavor only. Weakest-link tier Provisional " +
            "reconstruction (C4).",
            VisualDetailTier.Low),
        VisualScopeTag.UnscopedGeneric,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB3Cropped,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC4WovenSunHat,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLevy.lev01");

    /// <summary>
    /// LEV-05. B1 knotted hair, C5 bare head, G3 cord belt, H1 sheathed side
    /// blade, K2 dusty. VIS-022: the first of the five Wasay-only rows the
    /// VIS-018 milestone reserved this index range for. Weakest-link tier:
    /// Provisional reconstruction (G3).
    /// </summary>
    public static readonly AppearancePresetEntry Lev05 = new(
        new VisualCatalogEntry(
            "appearance.presetLevy.lev05",
            4,
            "Levy Warrior, Side Blade",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.UnscopedGeneric,
            "LEV-05: B1 (Long Hair, Knotted), C5 (Bare Head), D1 (Bare-" +
            "Chested), E1 (Bahag — Loincloth), G3 (Cord Belt), H1 " +
            "(Sheathed Side Blade), K2 (Dusty / Muddy). Wasay-armed only " +
            "(H1's own rule). Weakest-link tier Provisional " +
            "reconstruction (G3).",
            VisualDetailTier.Low),
        VisualScopeTag.UnscopedGeneric,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK2DustyMuddy,
            Accessory: AppearanceComponentCatalog.AccessoryH1SheathedSideBlade),
        AppearancePresetLoadoutCompatibility.WasayOnly,
        FallbackId: "appearance.presetLevy.lev01");

    /// <summary>
    /// LEV-06. B3 cropped hair, C5 bare head, G2 cloth belt, H1 sheathed side
    /// blade, K3 sweat sheen. Weakest-link tier: Documented, form uncertain
    /// (B3, G2).
    /// </summary>
    public static readonly AppearancePresetEntry Lev06 = new(
        new VisualCatalogEntry(
            "appearance.presetLevy.lev06",
            5,
            "Levy Warrior, Cropped Hair and Side Blade",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.UnscopedGeneric,
            "LEV-06: B3 (Cropped Hair), C5 (Bare Head), D1 (Bare-Chested), " +
            "E1 (Bahag — Loincloth), G2 (Cloth Belt), H1 (Sheathed Side " +
            "Blade), K3 (Sweat Sheen). Wasay-armed only (H1's own rule). " +
            "Weakest-link tier Documented, form uncertain (B3, G2).",
            VisualDetailTier.Low),
        VisualScopeTag.UnscopedGeneric,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB3Cropped,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK3SweatSheen,
            Accessory: AppearanceComponentCatalog.AccessoryH1SheathedSideBlade),
        AppearancePresetLoadoutCompatibility.WasayOnly,
        FallbackId: "appearance.presetLevy.lev01");

    /// <summary>
    /// LEV-07. B1 knotted hair, C4 woven sun hat, G2 cloth belt, H1 sheathed
    /// side blade, K4 faded dye. Weakest-link tier: Provisional
    /// reconstruction (C4).
    /// </summary>
    public static readonly AppearancePresetEntry Lev07 = new(
        new VisualCatalogEntry(
            "appearance.presetLevy.lev07",
            6,
            "Levy Warrior, Sun Hat and Side Blade",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.UnscopedGeneric,
            "LEV-07: B1 (Long Hair, Knotted), C4 (Woven Sun Hat), D1 " +
            "(Bare-Chested), E1 (Bahag — Loincloth), G2 (Cloth Belt), H1 " +
            "(Sheathed Side Blade), K4 (Faded Dye). Wasay-armed only " +
            "(H1's own rule). Levy flavor only — C4's battlefield use is " +
            "a Provisional reconstruction. Weakest-link tier Provisional " +
            "reconstruction (C4).",
            VisualDetailTier.Low),
        VisualScopeTag.UnscopedGeneric,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC4WovenSunHat,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK4FadedDye,
            Accessory: AppearanceComponentCatalog.AccessoryH1SheathedSideBlade),
        AppearancePresetLoadoutCompatibility.WasayOnly,
        FallbackId: "appearance.presetLevy.lev01");

    /// <summary>
    /// LEV-08. B3 cropped hair, C4 woven sun hat, G3 cord belt, H1 sheathed
    /// side blade, K1 clean. Weakest-link tier: Provisional reconstruction
    /// (C4, G3).
    /// </summary>
    public static readonly AppearancePresetEntry Lev08 = new(
        new VisualCatalogEntry(
            "appearance.presetLevy.lev08",
            7,
            "Levy Warrior, Cropped Hair, Sun Hat, and Side Blade",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.UnscopedGeneric,
            "LEV-08: B3 (Cropped Hair), C4 (Woven Sun Hat), D1 (Bare-" +
            "Chested), E1 (Bahag — Loincloth), G3 (Cord Belt), H1 " +
            "(Sheathed Side Blade), K1 (Clean). Wasay-armed only (H1's " +
            "own rule). Levy flavor only. Weakest-link tier Provisional " +
            "reconstruction (C4, G3).",
            VisualDetailTier.Low),
        VisualScopeTag.UnscopedGeneric,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB3Cropped,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC4WovenSunHat,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean,
            Accessory: AppearanceComponentCatalog.AccessoryH1SheathedSideBlade),
        AppearancePresetLoadoutCompatibility.WasayOnly,
        FallbackId: "appearance.presetLevy.lev01");

    /// <summary>
    /// LEV-09. B3 cropped hair, C5 bare head, G2 cloth belt, K5 battle-worn.
    /// Weakest-link tier: Documented, form uncertain (G2).
    /// </summary>
    public static readonly AppearancePresetEntry Lev09 = new(
        new VisualCatalogEntry(
            "appearance.presetLevy.lev09",
            8,
            "Levy Warrior, Battle-Worn",
            VisualEvidenceTier.DocumentedFormUncertain,
            VisualScopeTag.UnscopedGeneric,
            "LEV-09: B3 (Cropped Hair), C5 (Bare Head), D1 (Bare-Chested), " +
            "E1 (Bahag — Loincloth), G2 (Cloth Belt), K5 (Battle-Worn). " +
            "Minimal kit, undyed cloth, no tattoos, no gold, no putong — " +
            "Unscoped-generic scope. Weakest-link tier Documented, form " +
            "uncertain (G2).",
            VisualDetailTier.Low),
        VisualScopeTag.UnscopedGeneric,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB3Cropped,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK5BattleWorn),
        AppearancePresetLoadoutCompatibility.Any,
        FallbackId: "appearance.presetLevy.lev01");

    /// <summary>
    /// LEV-10. B1 knotted hair, C4 woven sun hat, G3 cord belt, H1 sheathed
    /// side blade, K3 sweat sheen. The last of the five Wasay-only rows
    /// VIS-018 reserved this index range for. Weakest-link tier: Provisional
    /// reconstruction (C4, G3).
    /// </summary>
    public static readonly AppearancePresetEntry Lev10 = new(
        new VisualCatalogEntry(
            "appearance.presetLevy.lev10",
            9,
            "Levy Warrior, Sun Hat, Cord Belt, and Side Blade",
            VisualEvidenceTier.ProvisionalReconstruction,
            VisualScopeTag.UnscopedGeneric,
            "LEV-10: B1 (Long Hair, Knotted), C4 (Woven Sun Hat), D1 " +
            "(Bare-Chested), E1 (Bahag — Loincloth), G3 (Cord Belt), H1 " +
            "(Sheathed Side Blade), K3 (Sweat Sheen). Wasay-armed only " +
            "(H1's own rule). Levy flavor only. Weakest-link tier " +
            "Provisional reconstruction (C4, G3).",
            VisualDetailTier.Low),
        VisualScopeTag.UnscopedGeneric,
        new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HairB1LongHairKnotted,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC4WovenSunHat,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG3CordBelt,
            Condition: AppearanceComponentCatalog.ConditionK3SweatSheen,
            Accessory: AppearanceComponentCatalog.AccessoryH1SheathedSideBlade),
        AppearancePresetLoadoutCompatibility.WasayOnly,
        FallbackId: "appearance.presetLevy.lev01");

    /// <summary>
    /// Every shipped generic-levy preset, LEV-01 through LEV-10, in the
    /// design table's own order. VIS-018 shipped the five loadout-<c>Any</c>
    /// rows first (LEV-01..04, LEV-09); VIS-022 fills in the five
    /// Wasay-only rows (LEV-05..08, LEV-10) at their already-reserved
    /// indices. This list is the generic-levy block's own roster —
    /// <see cref="All"/> below is the full cross-block union.
    /// </summary>
    private static readonly IReadOnlyList<AppearancePresetEntry> LevyPresets =
    [
        Lev01,
        Lev02,
        Lev03,
        Lev04,
        Lev05,
        Lev06,
        Lev07,
        Lev08,
        Lev09,
        Lev10,
    ];

    /// <summary>
    /// Every shipped preset, across every regional block (warrior-appearance-
    /// design.md "Preset roster": Visayan, Tagalog, Northern Luzon, then
    /// Generic levy, 53 total). VIS-022 is the single, later, sequenced task
    /// that assembles this union — see this class's own remarks — from the
    /// three sibling blocks' own <c>.All</c> properties plus the generic-levy
    /// roster completed in this file.
    /// </summary>
    public static IReadOnlyList<AppearancePresetEntry> All { get; } =
    [
        .. AppearancePresetsVisayan.All,
        .. AppearancePresetsTagalog.All,
        .. AppearancePresetsNorthernLuzon.All,
        .. LevyPresets,
    ];

    // ================= Block assignment (per faction, per scenario) =================

    /// <summary>
    /// The allowed block-assignment table (warrior-appearance-design.md,
    /// "Deterministic selection", step 1): every faction draws from exactly
    /// one entry per match. Four entries as of VIS-022 — one per shipped
    /// regional block, in the design's own "Preset roster" order — so
    /// <see cref="SelectBlock"/> genuinely mixes <paramref name="scenarioSeed"/>
    /// and <paramref name="factionId"/> through
    /// <see cref="PresentationSalts.AppearanceBlockAssignmentSalt"/> and
    /// reduces modulo the table's count to select among all four. Two
    /// factions in the same match may land on the same index — the design's
    /// own recommended "same block allowed" default — because each faction's
    /// own <paramref name="factionId"/> only changes which index its own
    /// independent mix resolves to, never which indices are available to a
    /// different faction (see this class's own remarks, "Same-block-allowed
    /// default").
    /// </summary>
    private static readonly IReadOnlyList<VisualScopeTag> BlockAssignmentTable =
    [
        VisualScopeTag.Visayan,
        VisualScopeTag.Tagalog,
        VisualScopeTag.Cagayan,
        VisualScopeTag.UnscopedGeneric,
    ];

    /// <summary>
    /// The deterministic block-assignment stream (R-W3.5, R-W6.2): a pure,
    /// salted, allocation-free function of <paramref name="scenarioSeed"/>
    /// and <paramref name="factionId"/>, stable across frames and replays of
    /// the same seed. Mixes <c>scenarioSeed XOR
    /// PresentationSalts.AppearanceBlockAssignmentSalt XOR (ulong)factionId</c>
    /// through the SplitMix64 finalizer and reduces modulo
    /// <see cref="BlockAssignmentTable"/>'s count.
    /// </summary>
    public static VisualScopeTag SelectBlock(ulong scenarioSeed, int factionId)
    {
        var mixed = Mix(scenarioSeed ^
            PresentationSalts.AppearanceBlockAssignmentSalt ^
            unchecked((ulong)factionId));
        var index = (int)(mixed % (ulong)BlockAssignmentTable.Count);
        return BlockAssignmentTable[index];
    }

    // ================= Preset selection (per pawn) =================

    /// <summary>
    /// Every declared preset for <paramref name="block"/> whose
    /// <see cref="AppearancePresetEntry.LoadoutCompatibility"/> allows
    /// <paramref name="weapon"/> (design step 2, "filtered by loadout
    /// compatibility"). Pre-built once per (block, is-the-weapon-Wasay) pair
    /// at type initialization — never allocates on the per-frame call path —
    /// mirroring <see cref="ShieldVisualCatalog.GetSkins"/>'s own
    /// pre-built-array discipline.
    /// </summary>
    public static IReadOnlyList<AppearancePresetEntry> GetCompatiblePresets(
        VisualScopeTag block,
        PawnWeaponRole weapon) =>
        CompatiblePoolsByBlockAndWasay[(block, weapon == PawnWeaponRole.Wasay)];

    private static readonly IReadOnlyDictionary<(VisualScopeTag Block, bool IsWasay), IReadOnlyList<AppearancePresetEntry>>
        CompatiblePoolsByBlockAndWasay = BuildCompatiblePools();

    private static IReadOnlyDictionary<(VisualScopeTag, bool), IReadOnlyList<AppearancePresetEntry>>
        BuildCompatiblePools()
    {
        var result = new Dictionary<(VisualScopeTag, bool), IReadOnlyList<AppearancePresetEntry>>();
        foreach (var block in Enum.GetValues<VisualScopeTag>())
        {
            foreach (var isWasay in new[] { false, true })
            {
                var pool = new List<AppearancePresetEntry>();
                foreach (var preset in All)
                {
                    if (preset.Block != block)
                    {
                        continue;
                    }

                    if (preset.LoadoutCompatibility ==
                        AppearancePresetLoadoutCompatibility.WasayOnly && !isWasay)
                    {
                        continue;
                    }

                    pool.Add(preset);
                }

                result[(block, isWasay)] = pool;
            }
        }

        return result;
    }

    /// <summary>
    /// <see cref="CompatiblePoolsByBlockAndWasay"/> partitioned by
    /// <see cref="AppearancePresetEntry.Status"/> — a pool holding only the
    /// <paramref name="status"/> rows for each (block, is-the-weapon-Wasay)
    /// key. Pre-built once per status at type initialization, same
    /// allocation-free-per-frame discipline as
    /// <see cref="CompatiblePoolsByBlockAndWasay"/> itself.
    /// </summary>
    private static IReadOnlyDictionary<(VisualScopeTag, bool), IReadOnlyList<AppearancePresetEntry>>
        BuildStatusFilteredPools(AppearancePresetStatus status)
    {
        var result = new Dictionary<(VisualScopeTag, bool), IReadOnlyList<AppearancePresetEntry>>();
        foreach (var (key, pool) in CompatiblePoolsByBlockAndWasay)
        {
            result[key] = [.. pool.Where(preset => preset.Status == status)];
        }

        return result;
    }

    /// <summary>
    /// The general-pool half of the status split (leader-character-
    /// design.md section 4.1): every loadout-compatible preset per (block,
    /// is-the-weapon-Wasay) whose <see cref="AppearancePresetEntry.Status"/>
    /// is <see cref="AppearancePresetStatus.General"/> — today's pool minus
    /// the three leader rows.
    /// </summary>
    private static readonly IReadOnlyDictionary<(VisualScopeTag Block, bool IsWasay), IReadOnlyList<AppearancePresetEntry>>
        GeneralPoolsByBlockAndWasay = BuildStatusFilteredPools(AppearancePresetStatus.General);

    /// <summary>
    /// The leader-pool half of the status split (leader-character-
    /// design.md section 4.1): every loadout-compatible preset per (block,
    /// is-the-weapon-Wasay) whose <see cref="AppearancePresetEntry.Status"/>
    /// is <see cref="AppearancePresetStatus.Leader"/>. Empty for the
    /// Northern Luzon and generic-levy blocks, which ship no chief row —
    /// <see cref="SelectPreset"/> falls back to
    /// <see cref="GeneralPoolsByBlockAndWasay"/> rather than inventing one.
    /// </summary>
    private static readonly IReadOnlyDictionary<(VisualScopeTag Block, bool IsWasay), IReadOnlyList<AppearancePresetEntry>>
        LeaderPoolsByBlockAndWasay = BuildStatusFilteredPools(AppearancePresetStatus.Leader);

    /// <summary>
    /// The deterministic preset-selection stream (R-W3.5, R-W6.2): a pure,
    /// salted, allocation-free function of <paramref name="entityId"/>,
    /// <paramref name="block"/>, <paramref name="weapon"/>, and
    /// <paramref name="isLeader"/>, stable across frames and replays of the
    /// same seed. Mixes
    /// <c>entityId XOR PresentationSalts.AppearancePresetSelectionSalt</c>
    /// through the SplitMix64 finalizer and walks the selected pool's
    /// cumulative <see cref="AppearancePresetEntry.RarityWeight"/> — a strict
    /// generalization of plain modulo selection that behaves identically to
    /// it whenever every candidate's weight is the uniform default of 1 (the
    /// generic-levy and Northern Luzon blocks throughout; every non-elite,
    /// non-leader row in the Visayan and Tagalog blocks). No new salt is
    /// introduced by <paramref name="isLeader"/>: it only chooses which of
    /// two statically ordered pools is walked (leader-character-design.md
    /// section 4.1).
    ///
    /// <paramref name="isLeader"/> defaults to <see langword="false"/> so
    /// today's call sites keep compiling unchanged until they are updated to
    /// pass the pawn's real leadership value.
    ///
    /// When <paramref name="isLeader"/> is <see langword="true"/> and the
    /// block's loadout-filtered leader pool is empty (Northern Luzon,
    /// generic levy — neither ships a chief row), this falls back to the
    /// block's general pool rather than to <see cref="Lev01"/>, so a leader
    /// in one of those blocks still resolves a preset from its own block.
    /// Falls back to <see cref="Lev01"/> — the family default — only when
    /// the applicable pool (general, or leader-with-general-fallback) is
    /// empty; as of VIS-022 every shipped block carries at least one
    /// loadout-<see cref="AppearancePresetLoadoutCompatibility.Any"/> general
    /// preset, so this path is unreachable for any real
    /// <see cref="VisualScopeTag"/> block, but stays total rather than
    /// partial for <see cref="VisualScopeTag.NotApplicable"/> (no preset
    /// ever declares that as its <see cref="AppearancePresetEntry.Block"/>)
    /// and for whichever future roster first ships a block/loadout pair with
    /// no compatible preset.
    /// </summary>
    public static AppearancePresetEntry SelectPreset(
        ulong entityId,
        VisualScopeTag block,
        PawnWeaponRole weapon,
        bool isLeader = false)
    {
        var key = (block, weapon == PawnWeaponRole.Wasay);
        var pool = isLeader
            ? GetLeaderPoolWithGeneralFallback(key)
            : GeneralPoolsByBlockAndWasay[key];

        if (pool.Count == 0)
        {
            return Lev01;
        }

        var totalWeight = 0;
        foreach (var preset in pool)
        {
            totalWeight += preset.RarityWeight;
        }

        var mixed = Mix(entityId ^ PresentationSalts.AppearancePresetSelectionSalt);
        var roll = (int)(mixed % (ulong)totalWeight);

        var cumulative = 0;
        foreach (var preset in pool)
        {
            cumulative += preset.RarityWeight;
            if (roll < cumulative)
            {
                return preset;
            }
        }

        // Unreachable: roll is always < totalWeight by construction (modulo
        // above), and cumulative reaches totalWeight on the pool's last
        // entry, so the loop always returns before falling through. Kept as
        // a total, non-throwing fallback rather than an unreachable throw,
        // matching this codebase's fallback-chain discipline.
        return pool[^1];
    }

    /// <summary>
    /// The leader pool for <paramref name="key"/>, or the general pool for
    /// the same key when the leader pool is empty (leader-character-
    /// design.md section 4.1: "not a corner case" — Northern Luzon and
    /// generic levy ship no chief row on purpose).
    /// </summary>
    private static IReadOnlyList<AppearancePresetEntry> GetLeaderPoolWithGeneralFallback(
        (VisualScopeTag Block, bool IsWasay) key)
    {
        var leaderPool = LeaderPoolsByBlockAndWasay[key];
        return leaderPool.Count > 0 ? leaderPool : GeneralPoolsByBlockAndWasay[key];
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
