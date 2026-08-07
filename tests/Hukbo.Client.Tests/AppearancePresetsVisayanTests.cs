using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins <see cref="AppearancePresetsVisayan"/> — the twenty-preset Visayan
/// block (implementation-plan-draft.md VIS-020; warrior-appearance-
/// design.md, "Visayan block"). A per-block test file per VIS-018's
/// registration mechanism (see <c>AppearancePresetTests.cs</c>'s own class
/// remarks), so this file never touches <see cref="AppearancePresets.All"/>
/// or the shared generic-levy roster — only
/// <see cref="AppearancePresetsVisayan.All"/> and
/// <see cref="AppearancePresetValidator"/>, exactly as VIS-021 and VIS-022
/// exercise their own blocks independently.
/// </summary>
public sealed class AppearancePresetsVisayanTests
{
    // --- Roster shape ---

    [Fact]
    public void All_HasExactlyTwentyEntries()
    {
        Assert.Equal(20, AppearancePresetsVisayan.All.Count);
    }

    [Fact]
    public void All_MatchesTheDesignTableOrder()
    {
        Assert.Equal(
            [
                AppearancePresetsVisayan.Vis01.Catalog.Id,
                AppearancePresetsVisayan.Vis02.Catalog.Id,
                AppearancePresetsVisayan.Vis03.Catalog.Id,
                AppearancePresetsVisayan.Vis04.Catalog.Id,
                AppearancePresetsVisayan.Vis05.Catalog.Id,
                AppearancePresetsVisayan.Vis06.Catalog.Id,
                AppearancePresetsVisayan.Vis07.Catalog.Id,
                AppearancePresetsVisayan.Vis08.Catalog.Id,
                AppearancePresetsVisayan.Vis09.Catalog.Id,
                AppearancePresetsVisayan.Vis10.Catalog.Id,
                AppearancePresetsVisayan.Vis11.Catalog.Id,
                AppearancePresetsVisayan.Vis12.Catalog.Id,
                AppearancePresetsVisayan.Vis13.Catalog.Id,
                AppearancePresetsVisayan.Vis14.Catalog.Id,
                AppearancePresetsVisayan.Vis15.Catalog.Id,
                AppearancePresetsVisayan.Vis16.Catalog.Id,
                AppearancePresetsVisayan.Vis17.Catalog.Id,
                AppearancePresetsVisayan.Vis18.Catalog.Id,
                AppearancePresetsVisayan.Vis19.Catalog.Id,
                AppearancePresetsVisayan.Vis20.Catalog.Id,
            ],
            AppearancePresetsVisayan.All.Select(preset => preset.Catalog.Id));
    }

    [Theory]
    [InlineData("appearance.presetVisayan.vis01", 0)]
    [InlineData("appearance.presetVisayan.vis02", 1)]
    [InlineData("appearance.presetVisayan.vis03", 2)]
    [InlineData("appearance.presetVisayan.vis04", 3)]
    [InlineData("appearance.presetVisayan.vis05", 4)]
    [InlineData("appearance.presetVisayan.vis06", 5)]
    [InlineData("appearance.presetVisayan.vis07", 6)]
    [InlineData("appearance.presetVisayan.vis08", 7)]
    [InlineData("appearance.presetVisayan.vis09", 8)]
    [InlineData("appearance.presetVisayan.vis10", 9)]
    [InlineData("appearance.presetVisayan.vis11", 10)]
    [InlineData("appearance.presetVisayan.vis12", 11)]
    [InlineData("appearance.presetVisayan.vis13", 12)]
    [InlineData("appearance.presetVisayan.vis14", 13)]
    [InlineData("appearance.presetVisayan.vis15", 14)]
    [InlineData("appearance.presetVisayan.vis16", 15)]
    [InlineData("appearance.presetVisayan.vis17", 16)]
    [InlineData("appearance.presetVisayan.vis18", 17)]
    [InlineData("appearance.presetVisayan.vis19", 18)]
    [InlineData("appearance.presetVisayan.vis20", 19)]
    public void All_PinsTheExactShippedIdentifierAndIndex(string expectedId, int expectedIndex)
    {
        var preset = Assert.Single(AppearancePresetsVisayan.All, p => p.Catalog.Id == expectedId);

        Assert.Equal(expectedIndex, preset.Catalog.Index);
    }

    [Fact]
    public void All_EveryPresetCarriesTheVisayanBlockAndScopeTag()
    {
        foreach (var preset in AppearancePresetsVisayan.All)
        {
            Assert.Equal(VisualScopeTag.Visayan, preset.Block);
            Assert.Equal(VisualScopeTag.Visayan, preset.Catalog.ScopeTag);
        }
    }

    [Fact]
    public void Vis01_FallsBackToTheGenericLevyFamilyDefault()
    {
        // "block bases fall back to LEV-01" (warrior-appearance-design.md).
        Assert.Equal("appearance.presetLevy.lev01", AppearancePresetsVisayan.Vis01.FallbackId);
    }

    [Theory]
    [InlineData("appearance.presetVisayan.vis02", "appearance.presetVisayan.vis01")]
    [InlineData("appearance.presetVisayan.vis03", "appearance.presetVisayan.vis01")]
    [InlineData("appearance.presetVisayan.vis04", "appearance.presetVisayan.vis01")]
    [InlineData("appearance.presetVisayan.vis05", "appearance.presetVisayan.vis04")]
    [InlineData("appearance.presetVisayan.vis06", "appearance.presetVisayan.vis04")]
    [InlineData("appearance.presetVisayan.vis07", "appearance.presetVisayan.vis01")]
    [InlineData("appearance.presetVisayan.vis08", "appearance.presetVisayan.vis01")]
    [InlineData("appearance.presetVisayan.vis09", "appearance.presetVisayan.vis08")]
    [InlineData("appearance.presetVisayan.vis10", "appearance.presetVisayan.vis08")]
    [InlineData("appearance.presetVisayan.vis11", "appearance.presetVisayan.vis04")]
    [InlineData("appearance.presetVisayan.vis12", "appearance.presetVisayan.vis01")]
    [InlineData("appearance.presetVisayan.vis13", "appearance.presetVisayan.vis05")]
    [InlineData("appearance.presetVisayan.vis14", "appearance.presetVisayan.vis13")]
    [InlineData("appearance.presetVisayan.vis15", "appearance.presetVisayan.vis13")]
    [InlineData("appearance.presetVisayan.vis16", "appearance.presetVisayan.vis01")]
    [InlineData("appearance.presetVisayan.vis17", "appearance.presetVisayan.vis07")]
    [InlineData("appearance.presetVisayan.vis18", "appearance.presetVisayan.vis08")]
    [InlineData("appearance.presetVisayan.vis19", "appearance.presetVisayan.vis11")]
    [InlineData("appearance.presetVisayan.vis20", "appearance.presetVisayan.vis01")]
    public void EveryOtherVisayanPreset_FallsBackPerTheDesignTable(string presetId, string expectedFallbackId)
    {
        var preset = Assert.Single(AppearancePresetsVisayan.All, p => p.Catalog.Id == presetId);

        Assert.Equal(expectedFallbackId, preset.FallbackId);
    }

    [Fact]
    public void All_OnlyVis14IsWasayOnly_EveryOtherPresetIsAny()
    {
        // "H1 rows Wasay-only" (implementation-plan-draft.md VIS-020 goal) —
        // VIS-14 is the sole H1-bearing row in this block.
        foreach (var preset in AppearancePresetsVisayan.All)
        {
            var expected = preset.Catalog.Id == "appearance.presetVisayan.vis14"
                ? AppearancePresetLoadoutCompatibility.WasayOnly
                : AppearancePresetLoadoutCompatibility.Any;

            Assert.Equal(expected, preset.LoadoutCompatibility);
        }
    }

    // --- Structural + combination-rule validation ---

    [Fact]
    public void ValidateStructure_AllTwentyPassEveryCheck()
    {
        var result = AppearancePresetValidator.ValidateStructure(
            "appearance.presetVisayan", AppearancePresetsVisayan.All);

        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
    }

    // --- Weakest-link tier, per the revised design table ---

    [Theory]
    [InlineData("appearance.presetVisayan.vis01", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis02", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetVisayan.vis03", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis04", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis05", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis06", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetVisayan.vis07", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis08", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis09", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis10", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetVisayan.vis11", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis12", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis13", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis14", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis15", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis16", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis17", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetVisayan.vis18", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis19", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetVisayan.vis20", VisualEvidenceTier.ProvisionalReconstruction)]
    public void ComputeWeakestLinkTier_MatchesTheRevisedDesignTablePerRow(
        string presetId,
        VisualEvidenceTier expectedTier)
    {
        var preset = Assert.Single(AppearancePresetsVisayan.All, p => p.Catalog.Id == presetId);

        Assert.Equal(expectedTier, AppearancePresetValidator.ComputeWeakestLinkTier(preset));
        // The pinned Catalog.EvidenceTier must agree with the computed
        // weakest-link tier — no preset may headline a tier stronger than
        // its own weakest rendered component (design: "the weakest-link
        // rule").
        Assert.Equal(expectedTier, preset.Catalog.EvidenceTier);
    }

    // --- Pairwise differentiation, within the Visayan block ---

    [Fact]
    public void SatisfiesDifferentiation_HoldsForEveryPairInTheShippedVisayanRoster()
    {
        var presets = AppearancePresetsVisayan.All;
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
        // Negative control: cloning VIS-01 verbatim (same block) must fail
        // the criterion, proving the positive test above is not vacuous.
        var clone = AppearancePresetsVisayan.Vis01 with
        {
            Catalog = AppearancePresetsVisayan.Vis01.Catalog with
            {
                Id = "appearance.presetVisayan.clone",
                Index = 200,
            },
        };

        Assert.False(
            AppearancePresetValidator.SatisfiesDifferentiation(AppearancePresetsVisayan.Vis01, clone));
    }

    // --- Loadout-pool totality ---

    [Fact]
    public void HasLoadoutPoolTotality_HoldsForTheShippedVisayanRosterAcrossEveryWeapon()
    {
        Assert.True(AppearancePresetValidator.HasLoadoutPoolTotality(AppearancePresetsVisayan.All));
    }

    // --- Tattoo scope: I1/I2 never leave the Visayan block ---

    [Theory]
    [InlineData("appearance.presetVisayan.vis02")]
    [InlineData("appearance.presetVisayan.vis03")]
    [InlineData("appearance.presetVisayan.vis04")]
    [InlineData("appearance.presetVisayan.vis05")]
    [InlineData("appearance.presetVisayan.vis07")]
    [InlineData("appearance.presetVisayan.vis11")]
    [InlineData("appearance.presetVisayan.vis12")]
    [InlineData("appearance.presetVisayan.vis13")]
    [InlineData("appearance.presetVisayan.vis14")]
    [InlineData("appearance.presetVisayan.vis15")]
    [InlineData("appearance.presetVisayan.vis16")]
    public void PresetsRenderingTattoos_AreExactlyTheOnesListedAndAllScopeVisayan(string presetId)
    {
        var preset = Assert.Single(AppearancePresetsVisayan.All, p => p.Catalog.Id == presetId);

        var rendersTattoo = preset.Recipe.Adornments.Any(a =>
            a.Catalog.Id == AppearanceComponentCatalog.AdornmentI1FullBodyTattoos.Catalog.Id ||
            a.Catalog.Id == AppearanceComponentCatalog.AdornmentI2PartialTattoos.Catalog.Id);

        Assert.True(rendersTattoo);
        Assert.Equal(VisualScopeTag.Visayan, preset.Block);
    }

    [Fact]
    public void PresetsRenderingTattoos_AreExactlyTheElevenListedRowsAndNoOthers()
    {
        var expectedIds = new HashSet<string>(
            [
                "appearance.presetVisayan.vis02",
                "appearance.presetVisayan.vis03",
                "appearance.presetVisayan.vis04",
                "appearance.presetVisayan.vis05",
                "appearance.presetVisayan.vis07",
                "appearance.presetVisayan.vis11",
                "appearance.presetVisayan.vis12",
                "appearance.presetVisayan.vis13",
                "appearance.presetVisayan.vis14",
                "appearance.presetVisayan.vis15",
                "appearance.presetVisayan.vis16",
            ],
            StringComparer.Ordinal);

        var actualIds = AppearancePresetsVisayan.All
            .Where(preset => preset.Recipe.Adornments.Any(a =>
                a.Catalog.Id == AppearanceComponentCatalog.AdornmentI1FullBodyTattoos.Catalog.Id ||
                a.Catalog.Id == AppearanceComponentCatalog.AdornmentI2PartialTattoos.Catalog.Id))
            .Select(preset => preset.Catalog.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(), actualIds);
    }

    // --- Gold accent scope: elite/leader rows only, plus the VIS-18 I4 carve-out (RF-03) ---

    [Fact]
    public void StrictGoldComponents_RenderOnlyOnTheThreeEliteOrLeaderRows()
    {
        // C3 (gold-edged putong), E2 (richly dyed, gold-edged bahag), and I5
        // (gold necklace) are the "strict" gold-bearing components — unlike
        // I4, they carry no prosperous-freeman carve-out.
        var eliteOrLeaderIds = new HashSet<string>(
            [
                "appearance.presetVisayan.vis13",
                "appearance.presetVisayan.vis14",
                "appearance.presetVisayan.vis15",
            ],
            StringComparer.Ordinal);

        foreach (var preset in AppearancePresetsVisayan.All)
        {
            var rendersStrictGold =
                preset.Recipe.HeadCovering.Catalog.Id ==
                    AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged.Catalog.Id ||
                preset.Recipe.LowerGarment.Catalog.Id ==
                    AppearanceComponentCatalog.LowerGarmentE2DyedGoldEdged.Catalog.Id ||
                preset.Recipe.Adornments.Any(a =>
                    a.Catalog.Id == AppearanceComponentCatalog.AdornmentI5GoldNecklace.Catalog.Id);

            Assert.Equal(eliteOrLeaderIds.Contains(preset.Catalog.Id), rendersStrictGold);
        }
    }

    [Fact]
    public void I4GoldEarrings_RenderOnlyOnEliteOrLeaderRowsPlusTheVis18ProsperousFreemanCarveOut()
    {
        // RF-03: VIS-18 carries the single I4 accent as its own
        // prosperous-freeman carve-out, without qualifying as elite or
        // datu/leader.
        var allowedIds = new HashSet<string>(
            [
                "appearance.presetVisayan.vis13",
                "appearance.presetVisayan.vis14",
                "appearance.presetVisayan.vis15",
                "appearance.presetVisayan.vis18",
            ],
            StringComparer.Ordinal);

        foreach (var preset in AppearancePresetsVisayan.All)
        {
            var rendersI4 = preset.Recipe.Adornments.Any(a =>
                a.Catalog.Id == AppearanceComponentCatalog.AdornmentI4GoldEarrings.Catalog.Id);

            Assert.Equal(allowedIds.Contains(preset.Catalog.Id), rendersI4);
        }
    }

    [Fact]
    public void Vis18_CarriesOnlyTheSingleI4AccentAndPlainE1BahagPerRf03()
    {
        var vis18 = AppearancePresetsVisayan.Vis18;

        var adornmentIds = vis18.Recipe.Adornments.Select(a => a.Catalog.Id).ToArray();
        Assert.Single(adornmentIds);
        Assert.Equal(AppearanceComponentCatalog.AdornmentI4GoldEarrings.Catalog.Id, adornmentIds[0]);
        Assert.Equal(
            AppearanceComponentCatalog.LowerGarmentE1Bahag.Catalog.Id,
            vis18.Recipe.LowerGarment.Catalog.Id);
    }

    // --- C2 exclusion (OD-5) ---

    [Fact]
    public void NoPreset_RendersTheExcludedC2HeadWrap()
    {
        // C2 has no AppearanceComponentEntry at all (OD-5) — this is a
        // structural guard confirming no row's HeadCovering field resolves
        // to anything other than C1, C3, C4, or C5.
        var allowedHeadCoveringIds = new HashSet<string>(
            [
                AppearanceComponentCatalog.HeadCoveringC1PutongPlain.Catalog.Id,
                AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged.Catalog.Id,
                AppearanceComponentCatalog.HeadCoveringC4WovenSunHat.Catalog.Id,
                AppearanceComponentCatalog.HeadCoveringC5BareHead.Catalog.Id,
            ],
            StringComparer.Ordinal);

        foreach (var preset in AppearancePresetsVisayan.All)
        {
            Assert.Contains(preset.Recipe.HeadCovering.Catalog.Id, allowedHeadCoveringIds);
        }
    }

    // --- Rarity weights (R-W3.14) ---

    [Fact]
    public void EliteAndLeaderRows_CarryASmallerRarityWeightThanEveryOrdinaryRow()
    {
        var eliteOrLeaderIds = new HashSet<string>(
            [
                "appearance.presetVisayan.vis13",
                "appearance.presetVisayan.vis14",
                "appearance.presetVisayan.vis15",
            ],
            StringComparer.Ordinal);

        var eliteWeights = AppearancePresetsVisayan.All
            .Where(preset => eliteOrLeaderIds.Contains(preset.Catalog.Id))
            .Select(preset => preset.RarityWeight)
            .Distinct()
            .ToArray();
        var ordinaryWeights = AppearancePresetsVisayan.All
            .Where(preset => !eliteOrLeaderIds.Contains(preset.Catalog.Id))
            .Select(preset => preset.RarityWeight)
            .Distinct()
            .ToArray();

        var eliteWeight = Assert.Single(eliteWeights);
        var ordinaryWeight = Assert.Single(ordinaryWeights);
        Assert.True(eliteWeight >= 1, "RarityWeight must be at least 1.");
        Assert.True(
            eliteWeight < ordinaryWeight,
            "Elite/leader rows must carry a smaller RarityWeight than ordinary rows.");
    }

    [Fact]
    public void EliteAndLeaderRows_SelectionProbabilityIsAtMostRoughlyTwoPercentEach()
    {
        // R-W3.14: "target at most roughly 2% each" within this block's own
        // (unfiltered, all-loadouts) pool.
        var totalWeight = AppearancePresetsVisayan.All.Sum(preset => preset.RarityWeight);
        var eliteOrLeaderIds = new HashSet<string>(
            [
                "appearance.presetVisayan.vis13",
                "appearance.presetVisayan.vis14",
                "appearance.presetVisayan.vis15",
            ],
            StringComparer.Ordinal);

        foreach (var preset in AppearancePresetsVisayan.All.Where(p => eliteOrLeaderIds.Contains(p.Catalog.Id)))
        {
            var probability = (double)preset.RarityWeight / totalWeight;
            Assert.True(
                probability <= 0.02,
                $"{preset.Catalog.Id} selection probability {probability:P2} exceeds the 2% target.");
        }
    }

    // --- Leader status gate (L1) ---

    [Fact]
    public void OnlyVis15CarriesLeaderStatus_EveryOtherPresetIsGeneral()
    {
        // Vis13 and Vis14 are elite rows, not leader rows: prominence and
        // wealth, not office, so they stay General even though they share
        // Vis15's small RarityWeight (leader-character-design.md section
        // 4.1: "the elite rows ... stay in the general pool").
        foreach (var preset in AppearancePresetsVisayan.All)
        {
            var expected = preset.Catalog.Id == AppearancePresetsVisayan.Vis15.Catalog.Id
                ? AppearancePresetStatus.Leader
                : AppearancePresetStatus.General;

            Assert.Equal(expected, preset.Status);
        }
    }
}
