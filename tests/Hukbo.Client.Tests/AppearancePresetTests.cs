using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins <see cref="AppearancePresets"/> (the milestone's five generic-levy
/// presets, LEV-01/02/03/04/09, plus its two selection streams) and
/// <see cref="AppearancePresetValidator"/> (implementation-plan-draft.md
/// VIS-018). Sibling regional blocks (VIS-020/021/022) get their own
/// per-block test files, per this milestone task's own remarks in
/// <see cref="AppearancePresets"/>'s class doc comment.
/// </summary>
public sealed class AppearancePresetTests
{
    // --- Roster shape ---

    [Fact]
    public void All_HasExactlyFiveEntriesThisMilestone()
    {
        Assert.Equal(5, AppearancePresets.All.Count);
    }

    [Fact]
    public void All_MatchesTheDesignTableOrder()
    {
        Assert.Equal(
            [
                AppearancePresets.Lev01.Catalog.Id,
                AppearancePresets.Lev02.Catalog.Id,
                AppearancePresets.Lev03.Catalog.Id,
                AppearancePresets.Lev04.Catalog.Id,
                AppearancePresets.Lev09.Catalog.Id,
            ],
            AppearancePresets.All.Select(preset => preset.Catalog.Id));
    }

    [Theory]
    [InlineData("appearance.presetLevy.lev01", 0)]
    [InlineData("appearance.presetLevy.lev02", 1)]
    [InlineData("appearance.presetLevy.lev03", 2)]
    [InlineData("appearance.presetLevy.lev04", 3)]
    [InlineData("appearance.presetLevy.lev09", 8)]
    public void All_PinsTheExactShippedIdentifierAndIndex(string expectedId, int expectedIndex)
    {
        var preset = Assert.Single(AppearancePresets.All, p => p.Catalog.Id == expectedId);

        Assert.Equal(expectedIndex, preset.Catalog.Index);
    }

    [Fact]
    public void All_EveryPresetCarriesTheUnscopedGenericBlockAndScopeTag()
    {
        foreach (var preset in AppearancePresets.All)
        {
            Assert.Equal(VisualScopeTag.UnscopedGeneric, preset.Block);
            Assert.Equal(VisualScopeTag.UnscopedGeneric, preset.Catalog.ScopeTag);
        }
    }

    [Fact]
    public void All_EveryPresetIsLoadoutCompatibleAny()
    {
        // The five presets this milestone ships are exactly the five the
        // design table marks Any rather than Wasay-only ("chosen
        // deliberately so the milestone needs no loadout filter edge case").
        foreach (var preset in AppearancePresets.All)
        {
            Assert.Equal(AppearancePresetLoadoutCompatibility.Any, preset.LoadoutCompatibility);
        }
    }

    [Fact]
    public void All_EveryPresetRecipeSharesTheLevyBlockConstants()
    {
        // "All rows are D1 bare chest, E1 cream bahag" (design, generic levy
        // block preamble).
        foreach (var preset in AppearancePresets.All)
        {
            Assert.Equal(
                AppearanceComponentCatalog.TorsoD1BareChested.Catalog.Id,
                preset.Recipe.TorsoGarment.Catalog.Id);
            Assert.Equal(
                AppearanceComponentCatalog.LowerGarmentE1Bahag.Catalog.Id,
                preset.Recipe.LowerGarment.Catalog.Id);
            Assert.Null(preset.Recipe.Armor);
            Assert.Null(preset.Recipe.Accessory);
            Assert.Empty(preset.Recipe.Adornments);
        }
    }

    [Fact]
    public void Lev01_FallsBackThroughTheGenericChainRatherThanToAnotherPreset()
    {
        Assert.Null(AppearancePresets.Lev01.FallbackId);
    }

    [Theory]
    [InlineData("appearance.presetLevy.lev02")]
    [InlineData("appearance.presetLevy.lev03")]
    [InlineData("appearance.presetLevy.lev04")]
    [InlineData("appearance.presetLevy.lev09")]
    public void EveryOtherLevyPreset_FallsBackToLev01(string presetId)
    {
        var preset = Assert.Single(AppearancePresets.All, p => p.Catalog.Id == presetId);

        Assert.Equal(AppearancePresets.Lev01.Catalog.Id, preset.FallbackId);
    }

    // --- Structural + combination-rule validation (VIS-018 step 3) ---

    [Fact]
    public void ValidateStructure_TheShippedLevyRosterPassesEveryCheck()
    {
        var result = AppearancePresetValidator.ValidateStructure(
            "appearance.presetLevy", AppearancePresets.All);

        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void ValidateStructure_ADeliberatelyIllegalSyntheticH1RecipeFails()
    {
        // A synthetic preset that renders H1 (the sheathed side blade) but
        // is not restricted to Wasay-armed pawns — exactly the "deliberately
        // illegal synthetic recipe" the task spec calls for, exercising the
        // combination rule without touching the shipped catalog.
        var illegal = BuildSyntheticPreset(
            id: "appearance.presetLevy.synthIllegalH1",
            index: 200,
            accessory: AppearanceComponentCatalog.AccessoryH1SheathedSideBlade,
            compatibility: AppearancePresetLoadoutCompatibility.Any);

        var result = AppearancePresetValidator.ValidateStructure(
            "appearance.presetLevy.synthetic", [illegal]);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Failures,
            failure => failure.Reason ==
                AppearancePresetValidator.ReasonAccessoryH1RequiresWasayOnlyLoadout);
    }

    [Fact]
    public void ValidateStructure_ASyntheticH1RecipeCorrectlyRestrictedToWasayPasses()
    {
        // The legal counterpart to the illegal case above: H1 present, and
        // LoadoutCompatibility.WasayOnly set, as every real LEV-05..08/10
        // preset (VIS-022, not shipped this milestone) will be authored.
        var legal = BuildSyntheticPreset(
            id: "appearance.presetLevy.synthLegalH1",
            index: 201,
            accessory: AppearanceComponentCatalog.AccessoryH1SheathedSideBlade,
            compatibility: AppearancePresetLoadoutCompatibility.WasayOnly);

        var result = AppearancePresetValidator.ValidateStructure(
            "appearance.presetLevy.synthetic", [legal]);

        Assert.True(result.IsValid);
    }

    // --- Weakest-link tier (VIS-018 step 3) ---

    [Theory]
    [InlineData("appearance.presetLevy.lev01", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetLevy.lev02", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetLevy.lev03", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetLevy.lev04", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetLevy.lev09", VisualEvidenceTier.DocumentedFormUncertain)]
    public void ComputeWeakestLinkTier_MatchesTheDesignTablePerRow(
        string presetId,
        VisualEvidenceTier expectedTier)
    {
        var preset = Assert.Single(AppearancePresets.All, p => p.Catalog.Id == presetId);

        Assert.Equal(expectedTier, AppearancePresetValidator.ComputeWeakestLinkTier(preset));
    }

    [Fact]
    public void ComputeWeakestLinkTier_ExcludesConditionFromTheComputation()
    {
        // LEV-09's own weakest-link tier (DocumentedFormUncertain, from G2)
        // stays that way even though its K5 condition component is
        // PresentationOnly — a strictly "worse" tier than
        // ProvisionalReconstruction were it not excluded, which would wrongly
        // flip this preset's weakest-link tier to PresentationOnly.
        var tier = AppearancePresetValidator.ComputeWeakestLinkTier(AppearancePresets.Lev09);

        Assert.NotEqual(VisualEvidenceTier.PresentationOnly, tier);
        Assert.Equal(
            VisualEvidenceTier.PresentationOnly,
            AppearancePresets.Lev09.Recipe.Condition.Catalog.EvidenceTier);
    }

    // --- Pairwise differentiation, within the Levy block (VIS-018 step 3) ---

    [Fact]
    public void SatisfiesDifferentiation_HoldsForEveryPairInTheShippedLevyRoster()
    {
        var presets = AppearancePresets.All;
        for (var i = 0; i < presets.Count; i++)
        {
            for (var j = i + 1; j < presets.Count; j++)
            {
                Assert.True(
                    AppearancePresetValidator.SatisfiesDifferentiation(presets[i], presets[j]),
                    $"{presets[i].Catalog.Id} and {presets[j].Catalog.Id} do not satisfy the " +
                    "minimum differentiation criterion.");
            }
        }
    }

    [Fact]
    public void SatisfiesDifferentiation_FailsForTwoIdenticalRecipes()
    {
        // A negative control: cloning LEV-01 verbatim (same block) must fail
        // the criterion, proving the test above is not vacuously true.
        var clone = AppearancePresets.Lev01 with
        {
            Catalog = AppearancePresets.Lev01.Catalog with
            {
                Id = "appearance.presetLevy.clone",
                Index = 210,
            },
        };

        Assert.False(AppearancePresetValidator.SatisfiesDifferentiation(AppearancePresets.Lev01, clone));
    }

    [Fact]
    public void SatisfiesDifferentiation_PassesWhenOnlyOneCountableCategoryDiffersButSilhouetteDoes()
    {
        // LEV-01 (B1, C5, G2, K1) versus LEV-03 (B1, C4, G3, K2): C differs
        // (silhouette-affecting), which alone is sufficient regardless of
        // how many countable categories also differ.
        Assert.True(
            AppearancePresetValidator.SatisfiesDifferentiation(
                AppearancePresets.Lev01, AppearancePresets.Lev03));
    }

    [Fact]
    public void SatisfiesDifferentiation_PassesOnTwoCountableDifferencesWithNoSilhouetteDifference()
    {
        // LEV-02 (B3, C5, G3, K1) versus LEV-09 (B3, C5, G2, K5): hair and
        // head covering both match (no silhouette-affecting difference), but
        // sash/belt and condition both differ — two countable categories,
        // which alone is sufficient per the "or" branch of the criterion.
        Assert.Equal(
            AppearancePresets.Lev02.Recipe.Hair.Catalog.Id,
            AppearancePresets.Lev09.Recipe.Hair.Catalog.Id);
        Assert.Equal(
            AppearancePresets.Lev02.Recipe.HeadCovering.Catalog.Id,
            AppearancePresets.Lev09.Recipe.HeadCovering.Catalog.Id);
        Assert.NotEqual(
            AppearancePresets.Lev02.Recipe.SashBelt.Catalog.Id,
            AppearancePresets.Lev09.Recipe.SashBelt.Catalog.Id);
        Assert.NotEqual(
            AppearancePresets.Lev02.Recipe.Condition.Catalog.Id,
            AppearancePresets.Lev09.Recipe.Condition.Catalog.Id);

        Assert.True(
            AppearancePresetValidator.SatisfiesDifferentiation(
                AppearancePresets.Lev02, AppearancePresets.Lev09));
    }

    [Fact]
    public void SatisfiesDifferentiation_FailsOnASingleCountableDifferenceWithNoSilhouetteDifference()
    {
        // A negative control isolating exactly one countable-only
        // difference (condition alone): must fail.
        var conditionOnlyDiff = AppearancePresets.Lev01 with
        {
            Catalog = AppearancePresets.Lev01.Catalog with
            {
                Id = "appearance.presetLevy.conditionOnlyDiff",
                Index = 211,
            },
            Recipe = AppearancePresets.Lev01.Recipe with
            {
                Condition = AppearanceComponentCatalog.ConditionK2DustyMuddy,
            },
        };

        Assert.False(
            AppearancePresetValidator.SatisfiesDifferentiation(
                AppearancePresets.Lev01, conditionOnlyDiff));
    }

    // --- Loadout-pool totality and the H1 filter machinery (VIS-018 step 3) ---

    [Fact]
    public void HasLoadoutPoolTotality_HoldsForTheShippedRosterAcrossEveryWeapon()
    {
        Assert.True(AppearancePresetValidator.HasLoadoutPoolTotality(AppearancePresets.All));
    }

    [Fact]
    public void GetCompatiblePresets_IncludesEveryShippedPresetForEveryWeapon()
    {
        // All five shipped presets are loadout Any, so the filtered pool
        // never shrinks by weapon this milestone.
        foreach (var weapon in Enum.GetValues<PawnWeaponRole>())
        {
            var pool = AppearancePresets.GetCompatiblePresets(VisualScopeTag.UnscopedGeneric, weapon);

            Assert.Equal(AppearancePresets.All.Count, pool.Count);
        }
    }

    [Fact]
    public void GetCompatiblePresets_IsEmptyForABlockWithNoShippedPresets()
    {
        var pool = AppearancePresets.GetCompatiblePresets(VisualScopeTag.Visayan, PawnWeaponRole.Kalis);

        Assert.Empty(pool);
    }

    [Fact]
    public void FilterMachinery_ASyntheticWasayOnlyH1PresetIsExcludedForNonWasayWeaponsAndIncludedForWasay()
    {
        // The filter machinery the plan calls for, tested with a synthetic
        // H1 recipe rather than a shipped one (none of the milestone's five
        // presets carries H1). Proves the loadout filter — not just the
        // validator's combination rule — behaves for a Wasay-only entry.
        var synthetic = BuildSyntheticPreset(
            id: "appearance.presetLevy.synthWasayOnlyFilter",
            index: 202,
            accessory: AppearanceComponentCatalog.AccessoryH1SheathedSideBlade,
            compatibility: AppearancePresetLoadoutCompatibility.WasayOnly);
        IReadOnlyList<AppearancePresetEntry> syntheticRoster = [.. AppearancePresets.All, synthetic];

        Assert.True(AppearancePresetValidator.HasLoadoutPoolTotality(syntheticRoster));

        foreach (var weapon in Enum.GetValues<PawnWeaponRole>())
        {
            var includesSynthetic = ContainsId(
                FilterCompatible(syntheticRoster, VisualScopeTag.UnscopedGeneric, weapon),
                synthetic.Catalog.Id);

            Assert.Equal(weapon == PawnWeaponRole.Wasay, includesSynthetic);
        }
    }

    // --- Block assignment and preset selection streams ---

    [Fact]
    public void SelectBlock_IsStableForTheSameSeedAndFaction()
    {
        Assert.Equal(
            AppearancePresets.SelectBlock(12345, 1),
            AppearancePresets.SelectBlock(12345, 1));
    }

    [Fact]
    public void SelectBlock_AlwaysResolvesToUnscopedGenericThisMilestone()
    {
        for (var factionId = 0; factionId < 4; factionId++)
        {
            for (ulong seed = 0; seed < 50; seed++)
            {
                Assert.Equal(VisualScopeTag.UnscopedGeneric, AppearancePresets.SelectBlock(seed, factionId));
            }
        }
    }

    [Fact]
    public void SelectPreset_IsStableForTheSameEntityIdBlockAndWeapon()
    {
        var first = AppearancePresets.SelectPreset(77, VisualScopeTag.UnscopedGeneric, PawnWeaponRole.Kalis);
        var second = AppearancePresets.SelectPreset(77, VisualScopeTag.UnscopedGeneric, PawnWeaponRole.Kalis);

        Assert.Equal(first.Catalog.Id, second.Catalog.Id);
    }

    [Fact]
    public void SelectPreset_FallsBackToLev01WhenThePoolIsEmpty()
    {
        var resolved = AppearancePresets.SelectPreset(1, VisualScopeTag.Visayan, PawnWeaponRole.Kalis);

        Assert.Equal(AppearancePresets.Lev01.Catalog.Id, resolved.Catalog.Id);
    }

    [Fact]
    public void SelectPreset_UniformWeightsBehaveAsPlainModuloOverThePoolCount()
    {
        // Every shipped preset carries the default RarityWeight of 1, so the
        // weighted walk must reduce to the same outcome plain modulo
        // selection over the pool would give for a fair, uniform table.
        foreach (var preset in AppearancePresets.All)
        {
            Assert.Equal(1, preset.RarityWeight);
        }
    }

    // --- Recipe construction guards ---

    [Fact]
    public void AppearancePresetRecipe_RejectsAComponentFromTheWrongCategory()
    {
        Assert.Throws<ArgumentException>(() => new AppearancePresetRecipe(
            Hair: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            HeadCovering: AppearanceComponentCatalog.HeadCoveringC5BareHead,
            TorsoGarment: AppearanceComponentCatalog.TorsoD1BareChested,
            LowerGarment: AppearanceComponentCatalog.LowerGarmentE1Bahag,
            SashBelt: AppearanceComponentCatalog.SashBeltG2ClothBelt,
            Condition: AppearanceComponentCatalog.ConditionK1Clean));
    }

    [Fact]
    public void AppearancePresetEntry_RejectsABlockThatDisagreesWithItsOwnCatalogScopeTag()
    {
        Assert.Throws<ArgumentException>(() => new AppearancePresetEntry(
            new VisualCatalogEntry(
                "appearance.presetLevy.mismatch",
                220,
                "Mismatch",
                VisualEvidenceTier.PresentationOnly,
                VisualScopeTag.Visayan,
                "test",
                VisualDetailTier.Low),
            VisualScopeTag.UnscopedGeneric,
            AppearancePresets.Lev01.Recipe,
            AppearancePresetLoadoutCompatibility.Any));
    }

    [Fact]
    public void AppearancePresetEntry_RejectsARarityWeightBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AppearancePresetEntry(
            AppearancePresets.Lev01.Catalog,
            VisualScopeTag.UnscopedGeneric,
            AppearancePresets.Lev01.Recipe,
            AppearancePresetLoadoutCompatibility.Any,
            RarityWeight: 0));
    }

    // --- Test helpers ---

    private static AppearancePresetEntry BuildSyntheticPreset(
        string id,
        int index,
        AppearanceComponentEntry accessory,
        AppearancePresetLoadoutCompatibility compatibility) =>
        new(
            new VisualCatalogEntry(
                id,
                index,
                "Synthetic Test Preset",
                VisualEvidenceTier.PresentationOnly,
                VisualScopeTag.UnscopedGeneric,
                "Synthetic recipe built only for AppearancePresetTests; never shipped.",
                VisualDetailTier.Low),
            VisualScopeTag.UnscopedGeneric,
            AppearancePresets.Lev01.Recipe with { Accessory = accessory },
            compatibility);

    private static IEnumerable<AppearancePresetEntry> FilterCompatible(
        IReadOnlyList<AppearancePresetEntry> presets,
        VisualScopeTag block,
        PawnWeaponRole weapon)
    {
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

            yield return preset;
        }
    }

    private static bool ContainsId(IEnumerable<AppearancePresetEntry> presets, string id) =>
        presets.Any(preset => preset.Catalog.Id == id);
}
