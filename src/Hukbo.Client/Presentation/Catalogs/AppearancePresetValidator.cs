using Hukbo.Client.Presentation;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// The warrior-appearance preset validator (warrior-appearance-design.md,
/// "Minimum differentiation criterion"; implementation-plan-draft.md VIS-018
/// step 3: "the validator skeleton"). Five checks, each a standalone pure
/// function over an explicit <see cref="AppearancePresetEntry"/> list so a
/// test can exercise every one — including a synthetic, deliberately illegal
/// recipe — without touching the shipped catalog:
/// <list type="number">
/// <item>
/// <b>Scope tag present.</b> Free once <see cref="AppearancePresetEntry"/>'s
/// own constructor enforces <c>Catalog.ScopeTag == Block</c> and
/// <see cref="VisualCatalogValidator"/>'s structural pass (wired through
/// <see cref="ValidateStructure"/>) confirms every entry resolves a defined
/// <see cref="VisualScopeTag"/> member.
/// </item>
/// <item><see cref="ComputeWeakestLinkTier"/> — the weakest-link evidence
/// tier (design: "the weakest tier among its rendered components", category
/// K excluded, "presentation-only components (K) are excluded from the
/// weakest-link computation").</item>
/// <item><see cref="SatisfiesDifferentiation"/> — the pairwise
/// differentiation criterion, scoped within one regional block.</item>
/// <item><see cref="HasLoadoutPoolTotality"/> — every (block, weapon) pair
/// resolves at least one compatible preset.</item>
/// <item><see cref="KeepsFactionSignalDistance"/> — the color-blind
/// no-regression floor (R-W6.10, VIS-033): no garment, tint, skin tone, or
/// ground/grass/trample/dust shade may sit within
/// <see cref="ContrastEnvelope.MinimumFactionDyeDistance"/> of a fixed
/// faction constant, so the ground ring stays the only faction signal.
/// </item>
/// </list>
/// Plus the one combination rule expressible at this milestone:
/// <see cref="ValidateStructure"/> wires a <see cref="VisualCatalogCombinationRule"/>
/// that fails any preset rendering the H1 sheathed-side-blade accessory
/// without <see cref="AppearancePresetLoadoutCompatibility.WasayOnly"/> —
/// the check the VIS-018 task spec calls out by name ("a deliberately
/// illegal synthetic recipe fails"). The remaining research co-occurrence
/// rules and prohibitions (R-W3.4's full set) apply to the regional blocks
/// VIS-020/021/022 ship and are out of this skeleton's scope.
/// </summary>
internal static class AppearancePresetValidator
{
    /// <summary>
    /// A preset's <see cref="AppearancePresetRecipe.Accessory"/> renders the
    /// H1 sheathed-side-blade component
    /// (<see cref="AppearanceComponentCatalog.AccessoryH1SheathedSideBlade"/>)
    /// without carrying
    /// <see cref="AppearancePresetLoadoutCompatibility.WasayOnly"/>
    /// (warrior-appearance-design.md: "Presets containing H1 ... are
    /// restricted to Wasay-armed pawns").
    /// </summary>
    public const string ReasonAccessoryH1RequiresWasayOnlyLoadout =
        "accessoryH1RequiresWasayOnlyLoadout";

    /// <summary>
    /// The categories the differentiation criterion treats as
    /// silhouette-affecting (design: "Silhouette-affecting categories are:
    /// B (hair), C (head covering), D-class ..., E-class ..., F (armor
    /// layer), H1 ... and H2"). Every research category that renders through
    /// this milestone's catalog maps to exactly one
    /// <see cref="AppearanceComponentCategory"/> member, and both H1 and H2
    /// are the single <see cref="AppearanceComponentCategory.Accessory"/>
    /// category, so no per-value split is needed here.
    /// </summary>
    private static readonly IReadOnlyList<AppearanceComponentCategory> SilhouetteAffectingCategories =
    [
        AppearanceComponentCategory.Hair,
        AppearanceComponentCategory.HeadCovering,
        AppearanceComponentCategory.TorsoGarment,
        AppearanceComponentCategory.LowerGarment,
        AppearanceComponentCategory.Armor,
        AppearanceComponentCategory.Accessory,
    ];

    /// <summary>
    /// The structural and combination-rule validation pass over
    /// <paramref name="presets"/> (visual-system-integration-design.md
    /// section 2's <see cref="VisualCatalogValidator"/>, extended with the
    /// H1/loadout combination rule above). Delegates identifier uniqueness,
    /// per-family index uniqueness, and mandatory-metadata presence to
    /// <see cref="VisualCatalogValidator.Validate(string, IReadOnlyList{VisualCatalogEntry}, VisualCatalogCombinationRule?)"/>
    /// over each preset's own <see cref="AppearancePresetEntry.Catalog"/>.
    /// </summary>
    public static VisualCatalogValidationResult ValidateStructure(
        string catalogId,
        IReadOnlyList<AppearancePresetEntry> presets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogId);
        ArgumentNullException.ThrowIfNull(presets);

        var byId = new Dictionary<string, AppearancePresetEntry>(StringComparer.Ordinal);
        var catalogEntries = new VisualCatalogEntry[presets.Count];
        for (var i = 0; i < presets.Count; i++)
        {
            catalogEntries[i] = presets[i].Catalog;
            byId[presets[i].Catalog.Id] = presets[i];
        }

        return VisualCatalogValidator.Validate(
            catalogId,
            catalogEntries,
            entry => CheckH1RequiresWasayOnly(entry, byId));
    }

    private static VisualCatalogCombinationViolation? CheckH1RequiresWasayOnly(
        VisualCatalogValidationEntry entry,
        IReadOnlyDictionary<string, AppearancePresetEntry> byId)
    {
        if (entry.Id is not { } id || !byId.TryGetValue(id, out var preset))
        {
            // No usable identifier, or not present in the lookup this call
            // built from the same list — the structural pass already
            // reports a missing/duplicate identifier for this case.
            return null;
        }

        var accessory = preset.Recipe.Accessory;
        var rendersH1 = accessory is not null &&
            string.Equals(
                accessory.Catalog.Id,
                AppearanceComponentCatalog.AccessoryH1SheathedSideBlade.Catalog.Id,
                StringComparison.Ordinal);

        if (rendersH1 && preset.LoadoutCompatibility != AppearancePresetLoadoutCompatibility.WasayOnly)
        {
            return new VisualCatalogCombinationViolation(
                ReasonAccessoryH1RequiresWasayOnlyLoadout,
                $"'{preset.Catalog.Id}' renders the H1 sheathed side-blade " +
                "accessory but does not carry LoadoutCompatibility." +
                "WasayOnly (warrior-appearance-design.md: H1 is restricted " +
                "to Wasay-armed pawns).");
        }

        return null;
    }

    /// <summary>
    /// The weakest tier among <paramref name="preset"/>'s rendered
    /// components — the highest-numbered (least certain)
    /// <see cref="VisualEvidenceTier"/> across every recipe field except
    /// <see cref="AppearancePresetRecipe.Condition"/>, which the research
    /// marks presentation-only and the design explicitly excludes from this
    /// computation ("presentation-only components (K) are excluded from the
    /// weakest-link computation").
    /// </summary>
    public static VisualEvidenceTier ComputeWeakestLinkTier(AppearancePresetEntry preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var weakest = VisualEvidenceTier.Documented;
        foreach (var component in GetWeakestLinkComponents(preset.Recipe))
        {
            if (component.Catalog.EvidenceTier > weakest)
            {
                weakest = component.Catalog.EvidenceTier;
            }
        }

        return weakest;
    }

    private static IEnumerable<AppearanceComponentEntry> GetWeakestLinkComponents(
        AppearancePresetRecipe recipe)
    {
        yield return recipe.Hair;
        yield return recipe.HeadCovering;
        yield return recipe.TorsoGarment;
        yield return recipe.LowerGarment;
        yield return recipe.SashBelt;

        if (recipe.Armor is { } armor)
        {
            yield return armor;
        }

        if (recipe.Accessory is { } accessory)
        {
            yield return accessory;
        }

        foreach (var adornment in recipe.Adornments)
        {
            yield return adornment;
        }
    }

    /// <summary>
    /// The minimum differentiation criterion between two presets in the same
    /// regional block (warrior-appearance-design.md, "Minimum
    /// differentiation criterion"): true when the pair differs in at least
    /// one silhouette-affecting category, or in at least two countable
    /// categories (every recipe category counts; there are no palette or
    /// stature/build recipe fields to exclude in this component model, so
    /// "countable" here is simply every field the differences below check).
    /// Callers are responsible for only comparing presets already known to
    /// share a block — the design scopes the criterion "within each block,
    /// not across the whole roster" and cross-block near-duplicates are
    /// accepted by design.
    /// </summary>
    public static bool SatisfiesDifferentiation(AppearancePresetEntry a, AppearancePresetEntry b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var diffs = GetCategoryDifferences(a.Recipe, b.Recipe);
        var silhouetteDiffers = diffs.Overlaps(SilhouetteAffectingCategories);
        return silhouetteDiffers || diffs.Count >= 2;
    }

    private static HashSet<AppearanceComponentCategory> GetCategoryDifferences(
        AppearancePresetRecipe a,
        AppearancePresetRecipe b)
    {
        var diffs = new HashSet<AppearanceComponentCategory>();

        AddIfDifferent(diffs, AppearanceComponentCategory.Hair, a.Hair, b.Hair);
        AddIfDifferent(diffs, AppearanceComponentCategory.HeadCovering, a.HeadCovering, b.HeadCovering);
        AddIfDifferent(diffs, AppearanceComponentCategory.TorsoGarment, a.TorsoGarment, b.TorsoGarment);
        AddIfDifferent(diffs, AppearanceComponentCategory.LowerGarment, a.LowerGarment, b.LowerGarment);
        AddIfDifferent(diffs, AppearanceComponentCategory.SashBelt, a.SashBelt, b.SashBelt);
        AddIfDifferent(diffs, AppearanceComponentCategory.Condition, a.Condition, b.Condition);
        AddIfDifferent(diffs, AppearanceComponentCategory.Armor, a.Armor, b.Armor);
        AddIfDifferent(diffs, AppearanceComponentCategory.Accessory, a.Accessory, b.Accessory);

        if (!AdornmentsEqual(a.Adornments, b.Adornments))
        {
            diffs.Add(AppearanceComponentCategory.Adornment);
        }

        return diffs;
    }

    private static void AddIfDifferent(
        HashSet<AppearanceComponentCategory> diffs,
        AppearanceComponentCategory category,
        AppearanceComponentEntry? a,
        AppearanceComponentEntry? b)
    {
        var aId = a?.Catalog.Id;
        var bId = b?.Catalog.Id;
        if (!string.Equals(aId, bId, StringComparison.Ordinal))
        {
            diffs.Add(category);
        }
    }

    private static bool AdornmentsEqual(
        IReadOnlyList<AppearanceComponentEntry> a,
        IReadOnlyList<AppearanceComponentEntry> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        var aIds = new List<string>(a.Count);
        foreach (var entry in a)
        {
            aIds.Add(entry.Catalog.Id);
        }

        var bIds = new List<string>(b.Count);
        foreach (var entry in b)
        {
            bIds.Add(entry.Catalog.Id);
        }

        aIds.Sort(StringComparer.Ordinal);
        bIds.Sort(StringComparer.Ordinal);

        for (var i = 0; i < aIds.Count; i++)
        {
            if (!string.Equals(aIds[i], bIds[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True when every (block, weapon) combination present in
    /// <paramref name="presets"/> resolves at least one loadout-compatible
    /// preset (warrior-appearance-design.md step 3, "loadout-pool totality
    /// (every block/loadout pair resolves a preset or a defined fallback)").
    /// This overload checks pool non-emptiness directly; every block this
    /// milestone's roster ships has at least one
    /// <see cref="AppearancePresetLoadoutCompatibility.Any"/> preset, so a
    /// defined single-preset fallback (<see cref="AppearancePresets.Lev01"/>)
    /// is never actually needed to satisfy totality — the fallback exists as
    /// insurance for a future roster, not as this check's escape hatch.
    /// </summary>
    public static bool HasLoadoutPoolTotality(IReadOnlyList<AppearancePresetEntry> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);

        var blocks = new HashSet<VisualScopeTag>();
        foreach (var preset in presets)
        {
            blocks.Add(preset.Block);
        }

        foreach (var block in blocks)
        {
            foreach (var weapon in Enum.GetValues<PawnWeaponRole>())
            {
                var hasCandidate = false;
                foreach (var preset in presets)
                {
                    if (preset.Block != block)
                    {
                        continue;
                    }

                    if (preset.LoadoutCompatibility == AppearancePresetLoadoutCompatibility.WasayOnly &&
                        weapon != PawnWeaponRole.Wasay)
                    {
                        continue;
                    }

                    hasCandidate = true;
                    break;
                }

                if (!hasCandidate)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // Backing array for FactionSignalConstants below — kept private so the
    // exposed surface is the mutation-safe IReadOnlyList<Color> view, while
    // this array is also what KeepsFactionSignalDistance passes straight to
    // ContrastEnvelope.IsWithinEnvelope's ReadOnlySpan<Color> parameter.
    private static readonly Color[] FactionConstantsArray =
    [
        FactionColorPalette.GetPawnColor(0),
        FactionColorPalette.GetPawnColor(1),
        FactionColorPalette.GetPawnColor(2),
    ];

    /// <summary>
    /// The fixed faction constants a presentation color must keep
    /// <see cref="ContrastEnvelope.MinimumFactionDyeDistance"/> away from
    /// (R-W6.10; visual-system-integration-design.md, "Color-blind shape
    /// redundancy": "no new variant may make garment or ground hues a
    /// competing faction signal"). Read through
    /// <see cref="FactionColorPalette.GetPawnColor"/> — factions 0 and 1 (the
    /// two battle factions) plus the "other faction" fallback color — rather
    /// than duplicated here, so this set can never drift from the one color
    /// <see cref="Hukbo.Client.Rendering.PawnRenderer"/> actually paints on
    /// the ground ring. Not itself a change to
    /// <see cref="FactionColorPalette"/> (VIS-005's prohibited scope; this
    /// task's own prohibited scope: "no change to the fixed faction
    /// constants") — a read-only projection of it.
    /// </summary>
    public static IReadOnlyList<Color> FactionSignalConstants { get; } =
        FactionConstantsArray;

    /// <summary>
    /// True when <paramref name="candidate"/> keeps at least
    /// <see cref="ContrastEnvelope.MinimumFactionDyeDistance"/> of channel
    /// distance from every color in <see cref="FactionSignalConstants"/> —
    /// the R-W6.10 no-regression floor this task (VIS-033) enforces. Pure
    /// and allocation free (delegates straight to
    /// <see cref="ContrastEnvelope.IsWithinEnvelope"/> over the cached
    /// constant array).
    ///
    /// This helper does not special-case
    /// <see cref="DyePalette.GoldAccent"/> or
    /// <see cref="DyePalette.TurmericYellow"/>, the one documented, accepted
    /// exception (<see cref="DyePalette"/> remarks): both are historically
    /// pinned gold/yellow tones that structurally cannot clear the distance
    /// from the third ("other faction") gold constant, and
    /// <c>AppearanceComponentCatalogTests</c> already records that honestly.
    /// Checking either of those two colors against the full
    /// <see cref="FactionSignalConstants"/> set through this helper
    /// therefore correctly reports <see langword="false"/>; a caller that
    /// needs to allow the documented exception checks against the two
    /// battle-faction constants only, exactly as that existing test does.
    /// </summary>
    public static bool KeepsFactionSignalDistance(Color candidate) =>
        ContrastEnvelope.IsWithinEnvelope(
            candidate,
            FactionConstantsArray,
            ContrastEnvelope.MinimumFactionDyeDistance);
}
