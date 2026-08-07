using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins <see cref="AppearancePresets"/> — originally the VIS-018 milestone's
/// five generic-levy presets (LEV-01/02/03/04/09) and its two selection
/// streams, extended by VIS-022 to the complete generic-levy block
/// (LEV-01..10) plus the full cross-block <see cref="AppearancePresets.All"/>
/// union and the four-entry <see cref="AppearancePresets.BlockAssignmentTable"/>
/// — and re-runs <see cref="AppearancePresetValidator"/> over the shipped
/// data (implementation-plan-draft.md VIS-018, VIS-022). Sibling regional
/// blocks (VIS-020/021/022) get their own per-block test files, per
/// <see cref="AppearancePresets"/>'s own class doc comment; tests here that
/// assert something about the whole roster or about the generic-levy block's
/// own ten presets stay in this file since both live in
/// <c>AppearancePresets.Levy.cs</c>.
/// </summary>
public sealed class AppearancePresetTests
{
    /// <summary>
    /// The ten generic-levy presets, LEV-01 through LEV-10, in the design
    /// table's own order — the same list production's own private
    /// <c>AppearancePresets.LevyPresets</c> field holds, reconstructed here
    /// from the ten public <c>Lev0N</c>/<c>Lev10</c> fields since tests
    /// cannot see a private production field.
    /// </summary>
    private static readonly IReadOnlyList<AppearancePresetEntry> LevyPresetsInOrder =
    [
        AppearancePresets.Lev01,
        AppearancePresets.Lev02,
        AppearancePresets.Lev03,
        AppearancePresets.Lev04,
        AppearancePresets.Lev05,
        AppearancePresets.Lev06,
        AppearancePresets.Lev07,
        AppearancePresets.Lev08,
        AppearancePresets.Lev09,
        AppearancePresets.Lev10,
    ];

    // --- Roster shape ---

    [Fact]
    public void All_HasExactlyFiftyThreePresetsAcrossEveryRegionalBlock()
    {
        // 20 Visayan + 15 Tagalog + 8 Northern Luzon + 10 generic levy = 53
        // (R-W3.2 pin, warrior-appearance-design.md "Preset roster").
        Assert.Equal(53, AppearancePresets.All.Count);
    }

    [Fact]
    public void All_ConcatenatesEveryRegionalBlockInDesignDocumentOrder()
    {
        var expected = AppearancePresetsVisayan.All
            .Concat(AppearancePresetsTagalog.All)
            .Concat(AppearancePresetsNorthernLuzon.All)
            .Concat(LevyPresetsInOrder)
            .Select(preset => preset.Catalog.Id);

        Assert.Equal(expected, AppearancePresets.All.Select(preset => preset.Catalog.Id));
    }

    [Fact]
    public void LevyPresets_MatchTheDesignTableOrder()
    {
        Assert.Equal(
            [
                AppearancePresets.Lev01.Catalog.Id,
                AppearancePresets.Lev02.Catalog.Id,
                AppearancePresets.Lev03.Catalog.Id,
                AppearancePresets.Lev04.Catalog.Id,
                AppearancePresets.Lev05.Catalog.Id,
                AppearancePresets.Lev06.Catalog.Id,
                AppearancePresets.Lev07.Catalog.Id,
                AppearancePresets.Lev08.Catalog.Id,
                AppearancePresets.Lev09.Catalog.Id,
                AppearancePresets.Lev10.Catalog.Id,
            ],
            LevyPresetsInOrder.Select(preset => preset.Catalog.Id));
    }

    [Theory]
    [InlineData("appearance.presetLevy.lev01", 0)]
    [InlineData("appearance.presetLevy.lev02", 1)]
    [InlineData("appearance.presetLevy.lev03", 2)]
    [InlineData("appearance.presetLevy.lev04", 3)]
    [InlineData("appearance.presetLevy.lev05", 4)]
    [InlineData("appearance.presetLevy.lev06", 5)]
    [InlineData("appearance.presetLevy.lev07", 6)]
    [InlineData("appearance.presetLevy.lev08", 7)]
    [InlineData("appearance.presetLevy.lev09", 8)]
    [InlineData("appearance.presetLevy.lev10", 9)]
    public void All_PinsTheExactShippedIdentifierAndIndex(string expectedId, int expectedIndex)
    {
        var preset = Assert.Single(AppearancePresets.All, p => p.Catalog.Id == expectedId);

        Assert.Equal(expectedIndex, preset.Catalog.Index);
    }

    [Fact]
    public void LevyPresets_EveryPresetCarriesTheUnscopedGenericBlockAndScopeTag()
    {
        foreach (var preset in LevyPresetsInOrder)
        {
            Assert.Equal(VisualScopeTag.UnscopedGeneric, preset.Block);
            Assert.Equal(VisualScopeTag.UnscopedGeneric, preset.Catalog.ScopeTag);
        }
    }

    [Fact]
    public void LevyPresets_OnlyLev05Through08And10AreWasayOnly()
    {
        // VIS-022 fills in the five Wasay-only rows the design table marks
        // "Wasay only" (all carrying H1); LEV-01/02/03/04/09 stay Any, as
        // VIS-018 shipped them.
        var wasayOnlyIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "appearance.presetLevy.lev05",
            "appearance.presetLevy.lev06",
            "appearance.presetLevy.lev07",
            "appearance.presetLevy.lev08",
            "appearance.presetLevy.lev10",
        };

        foreach (var preset in LevyPresetsInOrder)
        {
            var expected = wasayOnlyIds.Contains(preset.Catalog.Id)
                ? AppearancePresetLoadoutCompatibility.WasayOnly
                : AppearancePresetLoadoutCompatibility.Any;

            Assert.Equal(expected, preset.LoadoutCompatibility);
        }
    }

    [Fact]
    public void LevyPresets_EveryRecipeSharesTheLevyBlockConstants()
    {
        // "All rows are D1 bare chest, E1 cream bahag" (design, generic levy
        // block preamble); no armor and no adornments anywhere in the block,
        // but LEV-05..08/10 do carry the H1 accessory, so that field is not
        // asserted null here.
        foreach (var preset in LevyPresetsInOrder)
        {
            Assert.Equal(
                AppearanceComponentCatalog.TorsoD1BareChested.Catalog.Id,
                preset.Recipe.TorsoGarment.Catalog.Id);
            Assert.Equal(
                AppearanceComponentCatalog.LowerGarmentE1Bahag.Catalog.Id,
                preset.Recipe.LowerGarment.Catalog.Id);
            Assert.Null(preset.Recipe.Armor);
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
    [InlineData("appearance.presetLevy.lev05")]
    [InlineData("appearance.presetLevy.lev06")]
    [InlineData("appearance.presetLevy.lev07")]
    [InlineData("appearance.presetLevy.lev08")]
    [InlineData("appearance.presetLevy.lev09")]
    [InlineData("appearance.presetLevy.lev10")]
    public void EveryOtherLevyPreset_FallsBackToLev01(string presetId)
    {
        var preset = Assert.Single(AppearancePresets.All, p => p.Catalog.Id == presetId);

        Assert.Equal(AppearancePresets.Lev01.Catalog.Id, preset.FallbackId);
    }

    // --- Structural + combination-rule validation (VIS-018 step 3; re-run over the full VIS-022 roster) ---

    [Fact]
    public void ValidateStructure_TheFullShippedRosterPassesEveryCheck()
    {
        var result = AppearancePresetValidator.ValidateStructure(
            "appearance.preset", AppearancePresets.All);

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
        // LoadoutCompatibility.WasayOnly set, exactly as every real
        // LEV-05..08/10 preset (VIS-022) is authored below.
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
    [InlineData("appearance.presetLevy.lev05", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetLevy.lev06", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetLevy.lev07", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetLevy.lev08", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetLevy.lev09", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetLevy.lev10", VisualEvidenceTier.ProvisionalReconstruction)]
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

    // --- Pairwise differentiation, within the Levy block (VIS-018 step 3, extended by VIS-022) ---

    [Fact]
    public void SatisfiesDifferentiation_HoldsForEveryPairInTheShippedLevyRoster()
    {
        var presets = LevyPresetsInOrder;
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

    // --- Loadout-pool totality and the H1 filter machinery (VIS-018 step 3; full roster as of VIS-022) ---

    [Fact]
    public void HasLoadoutPoolTotality_HoldsForTheShippedRosterAcrossEveryWeapon()
    {
        Assert.True(AppearancePresetValidator.HasLoadoutPoolTotality(AppearancePresets.All));
    }

    [Fact]
    public void GetCompatiblePresets_ForUnscopedGenericIncludesAllTenPresetsForWasayAndFiveOtherwise()
    {
        // The generic-levy block's own ten presets: five are loadout Any
        // (LEV-01/02/03/04/09) and five are Wasay-only (LEV-05..08, LEV-10),
        // so the filtered pool is 5 for a non-Wasay weapon and 10 for Wasay.
        foreach (var weapon in Enum.GetValues<PawnWeaponRole>())
        {
            var pool = AppearancePresets.GetCompatiblePresets(VisualScopeTag.UnscopedGeneric, weapon);
            var expectedCount = weapon == PawnWeaponRole.Wasay ? 10 : 5;

            Assert.Equal(expectedCount, pool.Count);
        }
    }

    [Fact]
    public void GetCompatiblePresets_EveryRegionalBlockNowHasAtLeastOneCompatiblePresetPerWeapon()
    {
        // Before VIS-022 landed the Visayan/Tagalog/Cagayan blocks had no
        // shipped presets at all, so this pool used to be empty for every
        // weapon. Now that all four blocks are complete, every (block,
        // weapon) combination that a real preset can declare resolves at
        // least one compatible entry (R-W3.2 loadout-pool totality); only
        // VisualScopeTag.NotApplicable — never a real preset's own Block —
        // stays empty.
        foreach (var block in Enum.GetValues<VisualScopeTag>())
        {
            foreach (var weapon in Enum.GetValues<PawnWeaponRole>())
            {
                var pool = AppearancePresets.GetCompatiblePresets(block, weapon);

                if (block == VisualScopeTag.NotApplicable)
                {
                    Assert.Empty(pool);
                }
                else
                {
                    Assert.NotEmpty(pool);
                }
            }
        }
    }

    [Fact]
    public void FilterMachinery_ASyntheticWasayOnlyH1PresetIsExcludedForNonWasayWeaponsAndIncludedForWasay()
    {
        // The filter machinery the plan calls for, tested with a synthetic
        // H1 recipe layered on top of the shipped roster. Proves the
        // loadout filter — not just the validator's combination rule —
        // behaves for a Wasay-only entry.
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
    public void SelectBlock_AlwaysResolvesToOneOfTheFourShippedRegionalBlocks()
    {
        var shippedBlocks = new HashSet<VisualScopeTag>
        {
            VisualScopeTag.Visayan,
            VisualScopeTag.Tagalog,
            VisualScopeTag.Cagayan,
            VisualScopeTag.UnscopedGeneric,
        };

        for (var factionId = 0; factionId < 4; factionId++)
        {
            for (ulong seed = 0; seed < 50; seed++)
            {
                Assert.Contains(AppearancePresets.SelectBlock(seed, factionId), shippedBlocks);
            }
        }
    }

    [Fact]
    public void SelectBlock_CanAssignTwoDifferentFactionsTheSameBlockInTheSameMatch()
    {
        // The design's own recommended "same block allowed" default: nothing
        // in SelectBlock excludes two factions from resolving to the same
        // table index for one Scenario.Seed. This does not assert that every
        // seed produces a collision — only that at least one probed seed
        // does, proving no hidden distinctness constraint exists.
        var sawSameBlockForTwoFactions = false;
        for (ulong seed = 0; seed < 10_000 && !sawSameBlockForTwoFactions; seed++)
        {
            if (AppearancePresets.SelectBlock(seed, 0) == AppearancePresets.SelectBlock(seed, 1))
            {
                sawSameBlockForTwoFactions = true;
            }
        }

        Assert.True(sawSameBlockForTwoFactions, "Expected at least one probed seed to assign the same block to factions 0 and 1.");
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
        // VisualScopeTag.NotApplicable is not a real preset block — no
        // preset in the shipped roster, across all four regional blocks,
        // ever declares it as its own Block — so the pool is guaranteed
        // empty regardless of how many presets the roster ships.
        var resolved = AppearancePresets.SelectPreset(1, VisualScopeTag.NotApplicable, PawnWeaponRole.Kalis);

        Assert.Equal(AppearancePresets.Lev01.Catalog.Id, resolved.Catalog.Id);
    }

    [Fact]
    public void SelectPreset_ForAPopulatedRegionalBlockResolvesAPresetFromThatSameBlock()
    {
        // Now that VIS-022 has populated every regional block, selecting
        // against a real block (Visayan here) must resolve one of that
        // block's own presets rather than falling back to Lev01.
        var resolved = AppearancePresets.SelectPreset(1, VisualScopeTag.Visayan, PawnWeaponRole.Kalis);

        Assert.Equal(VisualScopeTag.Visayan, resolved.Block);
    }

    [Fact]
    public void SelectPreset_UniformWeightsBehaveAsPlainModuloOverThePoolCount()
    {
        // Every generic-levy preset carries the default RarityWeight of 1
        // (the block has no elite/leader row, unlike Visayan and Tagalog),
        // so the weighted walk over this block's own pool must reduce to
        // the same outcome plain modulo selection over the pool would give
        // for a fair, uniform table.
        foreach (var preset in LevyPresetsInOrder)
        {
            Assert.Equal(1, preset.RarityWeight);
        }
    }

    // --- Leader-gated selection (L1: leadership must decide the pool) ---

    [Fact]
    public void SelectPreset_LeaderTrueForVisayanAlwaysResolvesVis15()
    {
        // The Visayan block's leader pool holds exactly one row (VIS-15),
        // so the weighted walk resolves it for every probed entity id
        // regardless of the roll.
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var resolved = AppearancePresets.SelectPreset(
                entityId, VisualScopeTag.Visayan, PawnWeaponRole.Kalis, isLeader: true);

            Assert.Equal(AppearancePresetsVisayan.Vis15.Catalog.Id, resolved.Catalog.Id);
        }
    }

    [Fact]
    public void SelectPreset_LeaderTrueForTagalogResolvesOnlyTag13OrTag15()
    {
        var allowedIds = new HashSet<string>(
            [
                AppearancePresetsTagalog.Tag13.Catalog.Id,
                AppearancePresetsTagalog.Tag15.Catalog.Id,
            ],
            StringComparer.Ordinal);

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var resolved = AppearancePresets.SelectPreset(
                entityId, VisualScopeTag.Tagalog, PawnWeaponRole.Kalis, isLeader: true);

            Assert.Contains(resolved.Catalog.Id, allowedIds);
        }
    }

    [Fact]
    public void SelectPreset_NonLeaderNeverResolvesToAnyOfTheThreeLeaderRows()
    {
        var leaderIds = new HashSet<string>(
            [
                AppearancePresetsVisayan.Vis15.Catalog.Id,
                AppearancePresetsTagalog.Tag13.Catalog.Id,
                AppearancePresetsTagalog.Tag15.Catalog.Id,
            ],
            StringComparer.Ordinal);
        var blocks = new[] { VisualScopeTag.Visayan, VisualScopeTag.Tagalog };

        foreach (var block in blocks)
        {
            foreach (var weapon in Enum.GetValues<PawnWeaponRole>())
            {
                for (ulong entityId = 0; entityId < 200; entityId++)
                {
                    var resolved = AppearancePresets.SelectPreset(entityId, block, weapon, isLeader: false);

                    Assert.DoesNotContain(resolved.Catalog.Id, leaderIds);
                }
            }
        }
    }

    [Fact]
    public void SelectPreset_LeaderTrueForCagayanFallsBackToTheGeneralPoolRatherThanLev01()
    {
        // Northern Luzon ships no chief row at all (deliberately), so its
        // leader pool is empty and SelectPreset must fall back to the
        // block's general pool — still a Cagayan row — rather than all the
        // way to Lev01.
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            var resolved = AppearancePresets.SelectPreset(
                entityId, VisualScopeTag.Cagayan, PawnWeaponRole.Kalis, isLeader: true);

            Assert.Equal(VisualScopeTag.Cagayan, resolved.Block);
        }
    }

    [Fact]
    public void SelectPreset_DefaultIsLeaderFalseMatchesAnExplicitFalseCall()
    {
        // The trailing optional parameter must not silently change today's
        // three-argument call sites' meaning.
        var withDefault = AppearancePresets.SelectPreset(42, VisualScopeTag.Tagalog, PawnWeaponRole.Kalis);
        var withExplicitFalse = AppearancePresets.SelectPreset(
            42, VisualScopeTag.Tagalog, PawnWeaponRole.Kalis, isLeader: false);

        Assert.Equal(withExplicitFalse.Catalog.Id, withDefault.Catalog.Id);
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

    [Fact]
    public void AppearancePresetEntry_StatusDefaultsToGeneralWhenUnspecified()
    {
        Assert.Equal(AppearancePresetStatus.General, AppearancePresets.Lev01.Status);
    }

    [Fact]
    public void AppearancePresetEntry_RejectsAnUndefinedStatus()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AppearancePresetEntry(
            AppearancePresets.Lev01.Catalog,
            VisualScopeTag.UnscopedGeneric,
            AppearancePresets.Lev01.Recipe,
            AppearancePresetLoadoutCompatibility.Any,
            Status: (AppearancePresetStatus)99));
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
