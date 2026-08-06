using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins <see cref="AppearancePresetsTagalog"/> — the fifteen-preset Tagalog
/// block (implementation-plan-draft.md VIS-021) — and re-runs
/// <see cref="AppearancePresetValidator"/> over exactly this block's own
/// roster, the parallel-safe per-block test file
/// <c>AppearancePresets.Levy.cs</c>'s own class remarks call for.
/// </summary>
public sealed class AppearancePresetsTagalogTests
{
    // --- Roster shape ---

    [Fact]
    public void All_HasExactlyFifteenEntries()
    {
        Assert.Equal(15, AppearancePresetsTagalog.All.Count);
    }

    [Fact]
    public void All_MatchesTheDesignTableOrder()
    {
        Assert.Equal(
            [
                AppearancePresetsTagalog.Tag01.Catalog.Id,
                AppearancePresetsTagalog.Tag02.Catalog.Id,
                AppearancePresetsTagalog.Tag03.Catalog.Id,
                AppearancePresetsTagalog.Tag04.Catalog.Id,
                AppearancePresetsTagalog.Tag05.Catalog.Id,
                AppearancePresetsTagalog.Tag06.Catalog.Id,
                AppearancePresetsTagalog.Tag07.Catalog.Id,
                AppearancePresetsTagalog.Tag08.Catalog.Id,
                AppearancePresetsTagalog.Tag09.Catalog.Id,
                AppearancePresetsTagalog.Tag10.Catalog.Id,
                AppearancePresetsTagalog.Tag11.Catalog.Id,
                AppearancePresetsTagalog.Tag12.Catalog.Id,
                AppearancePresetsTagalog.Tag13.Catalog.Id,
                AppearancePresetsTagalog.Tag14.Catalog.Id,
                AppearancePresetsTagalog.Tag15.Catalog.Id,
            ],
            AppearancePresetsTagalog.All.Select(preset => preset.Catalog.Id));
    }

    [Theory]
    [InlineData("appearance.presetTagalog.tag01", 0)]
    [InlineData("appearance.presetTagalog.tag02", 1)]
    [InlineData("appearance.presetTagalog.tag03", 2)]
    [InlineData("appearance.presetTagalog.tag04", 3)]
    [InlineData("appearance.presetTagalog.tag05", 4)]
    [InlineData("appearance.presetTagalog.tag06", 5)]
    [InlineData("appearance.presetTagalog.tag07", 6)]
    [InlineData("appearance.presetTagalog.tag08", 7)]
    [InlineData("appearance.presetTagalog.tag09", 8)]
    [InlineData("appearance.presetTagalog.tag10", 9)]
    [InlineData("appearance.presetTagalog.tag11", 10)]
    [InlineData("appearance.presetTagalog.tag12", 11)]
    [InlineData("appearance.presetTagalog.tag13", 12)]
    [InlineData("appearance.presetTagalog.tag14", 13)]
    [InlineData("appearance.presetTagalog.tag15", 14)]
    public void All_PinsTheExactShippedIdentifierAndIndex(string expectedId, int expectedIndex)
    {
        var preset = Assert.Single(AppearancePresetsTagalog.All, p => p.Catalog.Id == expectedId);

        Assert.Equal(expectedIndex, preset.Catalog.Index);
    }

    [Fact]
    public void All_EveryPresetCarriesTheTagalogBlockAndScopeTag()
    {
        foreach (var preset in AppearancePresetsTagalog.All)
        {
            Assert.Equal(VisualScopeTag.Tagalog, preset.Block);
            Assert.Equal(VisualScopeTag.Tagalog, preset.Catalog.ScopeTag);
        }
    }

    [Fact]
    public void All_OnlyTag08IsWasayOnly()
    {
        foreach (var preset in AppearancePresetsTagalog.All)
        {
            var expected = preset.Catalog.Id == "appearance.presetTagalog.tag08"
                ? AppearancePresetLoadoutCompatibility.WasayOnly
                : AppearancePresetLoadoutCompatibility.Any;

            Assert.Equal(expected, preset.LoadoutCompatibility);
        }
    }

    // --- Structural + combination-rule validation ---

    [Fact]
    public void ValidateStructure_TheShippedTagalogRosterPassesEveryCheck()
    {
        var result = AppearancePresetValidator.ValidateStructure(
            "appearance.presetTagalog", AppearancePresetsTagalog.All);

        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
    }

    // --- Weakest-link tier (matches the revised design table per row) ---

    [Theory]
    [InlineData("appearance.presetTagalog.tag01", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetTagalog.tag02", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetTagalog.tag03", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetTagalog.tag04", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetTagalog.tag05", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetTagalog.tag06", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetTagalog.tag07", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetTagalog.tag08", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetTagalog.tag09", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetTagalog.tag10", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetTagalog.tag11", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetTagalog.tag12", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetTagalog.tag13", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetTagalog.tag14", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetTagalog.tag15", VisualEvidenceTier.DocumentedFormUncertain)]
    public void ComputeWeakestLinkTier_MatchesTheRevisedDesignTablePerRow(
        string presetId,
        VisualEvidenceTier expectedTier)
    {
        var preset = Assert.Single(AppearancePresetsTagalog.All, p => p.Catalog.Id == presetId);

        Assert.Equal(expectedTier, AppearancePresetValidator.ComputeWeakestLinkTier(preset));
    }

    [Fact]
    public void ComputeWeakestLinkTier_MatchesTheCatalogEntryEvidenceTierForEveryRow()
    {
        // Every shipped preset's own VisualCatalogEntry.EvidenceTier must
        // equal what the weakest-link computation derives from its recipe —
        // the two must never drift apart.
        foreach (var preset in AppearancePresetsTagalog.All)
        {
            Assert.Equal(
                preset.Catalog.EvidenceTier,
                AppearancePresetValidator.ComputeWeakestLinkTier(preset));
        }
    }

    // --- Pairwise differentiation, within the Tagalog block ---

    [Fact]
    public void SatisfiesDifferentiation_HoldsForEveryPairInTheShippedTagalogRoster()
    {
        var presets = AppearancePresetsTagalog.All;
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
    public void SatisfiesDifferentiation_Tag10AndTag12DifferOnlyInArmorButThatIsSilhouetteAffecting()
    {
        // TAG-10 and TAG-12 share hair, head covering, torso, lower
        // garment, sash/belt, and condition — the single-Armor-slot
        // resolution (F3 vs. F5, see AppearancePresets.Tagalog.cs's class
        // remarks) is the only thing that keeps them from being
        // recipe-identical, and Armor is a silhouette-affecting category so
        // that single difference alone must be sufficient.
        Assert.NotEqual(
            AppearancePresetsTagalog.Tag10.Recipe.Armor?.Catalog.Id,
            AppearancePresetsTagalog.Tag12.Recipe.Armor?.Catalog.Id);
        Assert.Equal(
            AppearancePresetsTagalog.Tag10.Recipe.Hair.Catalog.Id,
            AppearancePresetsTagalog.Tag12.Recipe.Hair.Catalog.Id);
        Assert.Equal(
            AppearancePresetsTagalog.Tag10.Recipe.Condition.Catalog.Id,
            AppearancePresetsTagalog.Tag12.Recipe.Condition.Catalog.Id);

        Assert.True(
            AppearancePresetValidator.SatisfiesDifferentiation(
                AppearancePresetsTagalog.Tag10, AppearancePresetsTagalog.Tag12));
    }

    // --- Loadout-pool totality and the H1 filter ---

    [Fact]
    public void HasLoadoutPoolTotality_HoldsForTheShippedTagalogRosterAcrossEveryWeapon()
    {
        Assert.True(AppearancePresetValidator.HasLoadoutPoolTotality(AppearancePresetsTagalog.All));
    }

    [Fact]
    public void Tag08_IsExcludedForNonWasayWeaponsAndIncludedForWasay()
    {
        foreach (var weapon in Enum.GetValues<PawnWeaponRole>())
        {
            var pool = FilterCompatible(AppearancePresetsTagalog.All, weapon);
            var includesTag08 = pool.Any(p => p.Catalog.Id == "appearance.presetTagalog.tag08");

            Assert.Equal(weapon == PawnWeaponRole.Wasay, includesTag08);
        }
    }

    // --- Prohibition 1/2/7: no C6, no I1/I2, no motif geometry ---

    [Fact]
    public void All_NoPresetRendersTheCagayanFeatheredHeaddressOrTheVisayanTattoos()
    {
        foreach (var preset in AppearancePresetsTagalog.All)
        {
            Assert.NotEqual(
                AppearanceComponentCatalog.HeadCoveringC6FeatheredHeaddress.Catalog.Id,
                preset.Recipe.HeadCovering.Catalog.Id);

            foreach (var adornment in preset.Recipe.Adornments)
            {
                Assert.NotEqual(AppearanceComponentCatalog.AdornmentI1FullBodyTattoos.Catalog.Id, adornment.Catalog.Id);
                Assert.NotEqual(AppearanceComponentCatalog.AdornmentI2PartialTattoos.Catalog.Id, adornment.Catalog.Id);
            }
        }
    }

    // --- Prohibition 3: the red chinina (D3) appears only on TAG-13 ---

    [Fact]
    public void All_OnlyTag13RendersTheRedChieflyChinina()
    {
        foreach (var preset in AppearancePresetsTagalog.All)
        {
            var rendersD3 = preset.Recipe.TorsoGarment.Catalog.Id ==
                AppearanceComponentCatalog.TorsoD3ChininaRedChiefly.Catalog.Id;
            var isTag13 = preset.Catalog.Id == "appearance.presetTagalog.tag13";

            Assert.Equal(isTag13, rendersD3);
        }
    }

    // --- Prohibition 6: gold only on elite/chief/leader rows, with the
    //     single-I4 TAG-14 prosperous-freeman carve-out (RF-03) ---

    [Fact]
    public void All_GoldComponentsOnlyAppearOnTag13Tag14AndTag15()
    {
        var goldRowIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "appearance.presetTagalog.tag13",
            "appearance.presetTagalog.tag14",
            "appearance.presetTagalog.tag15",
        };

        foreach (var preset in AppearancePresetsTagalog.All)
        {
            var rendersGold =
                preset.Recipe.HeadCovering.Catalog.Id ==
                    AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged.Catalog.Id ||
                preset.Recipe.LowerGarment.Catalog.Id ==
                    AppearanceComponentCatalog.LowerGarmentE2DyedGoldEdged.Catalog.Id ||
                preset.Recipe.Adornments.Any(a =>
                    a.Catalog.Id == AppearanceComponentCatalog.AdornmentI4GoldEarrings.Catalog.Id ||
                    a.Catalog.Id == AppearanceComponentCatalog.AdornmentI5GoldNecklace.Catalog.Id);

            Assert.Equal(goldRowIds.Contains(preset.Catalog.Id), rendersGold);
        }
    }

    [Fact]
    public void Tag14_CarriesExactlyTheSingleI4AccentAndNoOtherGoldComponent()
    {
        var tag14 = AppearancePresetsTagalog.Tag14;

        var adornmentId = Assert.Single(tag14.Recipe.Adornments).Catalog.Id;
        Assert.Equal(AppearanceComponentCatalog.AdornmentI4GoldEarrings.Catalog.Id, adornmentId);
        Assert.NotEqual(
            AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged.Catalog.Id,
            tag14.Recipe.HeadCovering.Catalog.Id);
        Assert.NotEqual(
            AppearanceComponentCatalog.LowerGarmentE2DyedGoldEdged.Catalog.Id,
            tag14.Recipe.LowerGarment.Catalog.Id);
        Assert.Equal(
            AppearanceComponentCatalog.LowerGarmentE1Bahag.Catalog.Id,
            tag14.Recipe.LowerGarment.Catalog.Id);
    }

    [Fact]
    public void Tag13AndTag15_CarryBothI4AndI5()
    {
        foreach (var preset in new[] { AppearancePresetsTagalog.Tag13, AppearancePresetsTagalog.Tag15 })
        {
            Assert.Equal(2, preset.Recipe.Adornments.Count);
            Assert.Contains(
                preset.Recipe.Adornments,
                a => a.Catalog.Id == AppearanceComponentCatalog.AdornmentI4GoldEarrings.Catalog.Id);
            Assert.Contains(
                preset.Recipe.Adornments,
                a => a.Catalog.Id == AppearanceComponentCatalog.AdornmentI5GoldNecklace.Catalog.Id);
        }
    }

    // --- Rarity weighting (R-W3.14: elite/leader rows carry a small,
    //     named PROVISIONAL weight) ---

    [Fact]
    public void Tag13AndTag15_CarryTheSmallElitePROVISIONALRarityWeight()
    {
        Assert.Equal(1, AppearancePresetsTagalog.Tag13.RarityWeight);
        Assert.Equal(1, AppearancePresetsTagalog.Tag15.RarityWeight);
    }

    [Fact]
    public void EveryOtherTagalogPreset_CarriesTheCommonRarityWeightGreaterThanOne()
    {
        foreach (var preset in AppearancePresetsTagalog.All)
        {
            if (preset.Catalog.Id is "appearance.presetTagalog.tag13" or "appearance.presetTagalog.tag15")
            {
                continue;
            }

            Assert.True(preset.RarityWeight > 1);
        }
    }

    [Fact]
    public void Tag13AndTag15_ResolveToAtMostRoughlyTwoPercentOfEitherLoadoutPool()
    {
        var nonWasayPool = FilterCompatible(AppearancePresetsTagalog.All, PawnWeaponRole.Kalis);
        var wasayPool = FilterCompatible(AppearancePresetsTagalog.All, PawnWeaponRole.Wasay);

        AssertShareAtMostTwoPercent(nonWasayPool, "appearance.presetTagalog.tag13");
        AssertShareAtMostTwoPercent(nonWasayPool, "appearance.presetTagalog.tag15");
        AssertShareAtMostTwoPercent(wasayPool, "appearance.presetTagalog.tag13");
        AssertShareAtMostTwoPercent(wasayPool, "appearance.presetTagalog.tag15");
    }

    private static void AssertShareAtMostTwoPercent(
        IReadOnlyList<AppearancePresetEntry> pool,
        string presetId)
    {
        var totalWeight = pool.Sum(p => p.RarityWeight);
        var presetWeight = pool.Single(p => p.Catalog.Id == presetId).RarityWeight;

        Assert.True(
            (double)presetWeight / totalWeight <= 0.02,
            $"{presetId} share {presetWeight}/{totalWeight} exceeds the roughly-2% PROVISIONAL target.");
    }

    // --- Fallback chain ---

    [Theory]
    [InlineData("appearance.presetTagalog.tag01", "appearance.presetLevy.lev01")]
    [InlineData("appearance.presetTagalog.tag02", "appearance.presetTagalog.tag01")]
    [InlineData("appearance.presetTagalog.tag03", "appearance.presetTagalog.tag01")]
    [InlineData("appearance.presetTagalog.tag04", "appearance.presetTagalog.tag01")]
    [InlineData("appearance.presetTagalog.tag05", "appearance.presetTagalog.tag01")]
    [InlineData("appearance.presetTagalog.tag06", "appearance.presetTagalog.tag05")]
    [InlineData("appearance.presetTagalog.tag07", "appearance.presetTagalog.tag02")]
    [InlineData("appearance.presetTagalog.tag08", "appearance.presetTagalog.tag01")]
    [InlineData("appearance.presetTagalog.tag09", "appearance.presetTagalog.tag05")]
    [InlineData("appearance.presetTagalog.tag10", "appearance.presetTagalog.tag05")]
    [InlineData("appearance.presetTagalog.tag11", "appearance.presetTagalog.tag10")]
    [InlineData("appearance.presetTagalog.tag12", "appearance.presetTagalog.tag10")]
    [InlineData("appearance.presetTagalog.tag13", "appearance.presetTagalog.tag02")]
    [InlineData("appearance.presetTagalog.tag14", "appearance.presetTagalog.tag02")]
    [InlineData("appearance.presetTagalog.tag15", "appearance.presetTagalog.tag13")]
    public void All_MatchesTheDesignTableFallbackColumn(string presetId, string expectedFallbackId)
    {
        var preset = Assert.Single(AppearancePresetsTagalog.All, p => p.Catalog.Id == presetId);

        Assert.Equal(expectedFallbackId, preset.FallbackId);
    }

    // --- Leader status gate (L1) ---

    [Fact]
    public void OnlyTag13AndTag15CarryLeaderStatus_EveryOtherPresetIsGeneral()
    {
        var leaderIds = new HashSet<string>(
            [
                "appearance.presetTagalog.tag13",
                "appearance.presetTagalog.tag15",
            ],
            StringComparer.Ordinal);

        foreach (var preset in AppearancePresetsTagalog.All)
        {
            var expected = leaderIds.Contains(preset.Catalog.Id)
                ? AppearancePresetStatus.Leader
                : AppearancePresetStatus.General;

            Assert.Equal(expected, preset.Status);
        }
    }

    // --- Test helpers ---

    private static IReadOnlyList<AppearancePresetEntry> FilterCompatible(
        IReadOnlyList<AppearancePresetEntry> presets,
        PawnWeaponRole weapon) =>
        presets
            .Where(preset =>
                preset.LoadoutCompatibility != AppearancePresetLoadoutCompatibility.WasayOnly ||
                weapon == PawnWeaponRole.Wasay)
            .ToList();
}
