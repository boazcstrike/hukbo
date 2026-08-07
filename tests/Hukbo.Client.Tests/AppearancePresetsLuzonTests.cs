using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins <see cref="AppearancePresetsNorthernLuzon"/> — the eight-preset
/// Northern Luzon block (implementation-plan-draft.md VIS-022) — and
/// re-runs <see cref="AppearancePresetValidator"/> over exactly this block's
/// own roster, the parallel-safe per-block test file pattern
/// <c>AppearancePresets.Levy.cs</c>'s own class remarks call for. Also covers
/// the roster-wide checks VIS-022's own automated-verification list names
/// that no single regional block's own test file is positioned to make: the
/// C6 exclusivity prohibition across the <em>whole</em> shipped roster, the
/// 53-preset count floor, block-assignment stability, and loadout-pool
/// totality across all four blocks — since this is the file VIS-022's own
/// task spec names as its one new test file.
/// </summary>
public sealed class AppearancePresetsLuzonTests
{
    // --- Roster shape ---

    [Fact]
    public void All_HasExactlyEightEntries()
    {
        Assert.Equal(8, AppearancePresetsNorthernLuzon.All.Count);
    }

    [Fact]
    public void All_MatchesTheDesignTableOrder()
    {
        Assert.Equal(
            [
                AppearancePresetsNorthernLuzon.Luz01.Catalog.Id,
                AppearancePresetsNorthernLuzon.Luz02.Catalog.Id,
                AppearancePresetsNorthernLuzon.Luz03.Catalog.Id,
                AppearancePresetsNorthernLuzon.Luz04.Catalog.Id,
                AppearancePresetsNorthernLuzon.Luz05.Catalog.Id,
                AppearancePresetsNorthernLuzon.Luz06.Catalog.Id,
                AppearancePresetsNorthernLuzon.Luz07.Catalog.Id,
                AppearancePresetsNorthernLuzon.Luz08.Catalog.Id,
            ],
            AppearancePresetsNorthernLuzon.All.Select(preset => preset.Catalog.Id));
    }

    [Theory]
    [InlineData("appearance.presetLuzon.luz01", 0)]
    [InlineData("appearance.presetLuzon.luz02", 1)]
    [InlineData("appearance.presetLuzon.luz03", 2)]
    [InlineData("appearance.presetLuzon.luz04", 3)]
    [InlineData("appearance.presetLuzon.luz05", 4)]
    [InlineData("appearance.presetLuzon.luz06", 5)]
    [InlineData("appearance.presetLuzon.luz07", 6)]
    [InlineData("appearance.presetLuzon.luz08", 7)]
    public void All_PinsTheExactShippedIdentifierAndIndex(string expectedId, int expectedIndex)
    {
        var preset = Assert.Single(AppearancePresetsNorthernLuzon.All, p => p.Catalog.Id == expectedId);

        Assert.Equal(expectedIndex, preset.Catalog.Index);
    }

    [Fact]
    public void All_EveryPresetCarriesTheCagayanBlockAndScopeTag()
    {
        foreach (var preset in AppearancePresetsNorthernLuzon.All)
        {
            Assert.Equal(VisualScopeTag.Cagayan, preset.Block);
            Assert.Equal(VisualScopeTag.Cagayan, preset.Catalog.ScopeTag);
        }
    }

    [Fact]
    public void All_OnlyLuz08IsWasayOnly()
    {
        foreach (var preset in AppearancePresetsNorthernLuzon.All)
        {
            var expected = preset.Catalog.Id == "appearance.presetLuzon.luz08"
                ? AppearancePresetLoadoutCompatibility.WasayOnly
                : AppearancePresetLoadoutCompatibility.Any;

            Assert.Equal(expected, preset.LoadoutCompatibility);
        }
    }

    // --- Structural + combination-rule validation ---

    [Fact]
    public void ValidateStructure_TheShippedNorthernLuzonRosterPassesEveryCheck()
    {
        var result = AppearancePresetValidator.ValidateStructure(
            "appearance.presetLuzon", AppearancePresetsNorthernLuzon.All);

        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
    }

    // --- Weakest-link tier (matches the design table per row) ---

    [Theory]
    [InlineData("appearance.presetLuzon.luz01", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetLuzon.luz02", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetLuzon.luz03", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetLuzon.luz04", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetLuzon.luz05", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetLuzon.luz06", VisualEvidenceTier.ProvisionalReconstruction)]
    [InlineData("appearance.presetLuzon.luz07", VisualEvidenceTier.DocumentedFormUncertain)]
    [InlineData("appearance.presetLuzon.luz08", VisualEvidenceTier.DocumentedFormUncertain)]
    public void ComputeWeakestLinkTier_MatchesTheDesignTablePerRow(
        string presetId,
        VisualEvidenceTier expectedTier)
    {
        var preset = Assert.Single(AppearancePresetsNorthernLuzon.All, p => p.Catalog.Id == presetId);

        Assert.Equal(expectedTier, AppearancePresetValidator.ComputeWeakestLinkTier(preset));
    }

    [Fact]
    public void ComputeWeakestLinkTier_MatchesTheCatalogEntryEvidenceTierForEveryRow()
    {
        // Every shipped preset's own VisualCatalogEntry.EvidenceTier must
        // equal what the weakest-link computation derives from its recipe —
        // the two must never drift apart.
        foreach (var preset in AppearancePresetsNorthernLuzon.All)
        {
            Assert.Equal(
                preset.Catalog.EvidenceTier,
                AppearancePresetValidator.ComputeWeakestLinkTier(preset));
        }
    }

    // --- Pairwise differentiation, within the Northern Luzon block ---

    [Fact]
    public void SatisfiesDifferentiation_HoldsForEveryPairInTheShippedNorthernLuzonRoster()
    {
        var presets = AppearancePresetsNorthernLuzon.All;
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
        // A negative control: cloning LUZ-01 verbatim (same block) must fail
        // the criterion, proving the test above is not vacuously true.
        var clone = AppearancePresetsNorthernLuzon.Luz01 with
        {
            Catalog = AppearancePresetsNorthernLuzon.Luz01.Catalog with
            {
                Id = "appearance.presetLuzon.clone",
                Index = 210,
            },
        };

        Assert.False(
            AppearancePresetValidator.SatisfiesDifferentiation(
                AppearancePresetsNorthernLuzon.Luz01, clone));
    }

    [Fact]
    public void SatisfiesDifferentiation_Luz01AndLuz03ShareEveryTraitButSashBeltAndCondition()
    {
        // LUZ-01 (B2, C5, D1, E1, G2, K1) versus LUZ-03 (B2, C5, D1, E1, G3,
        // K2): hair, head covering, and every other silhouette-affecting
        // field match — sash/belt and condition both differ, two countable
        // categories, sufficient per the criterion's "or" branch alone.
        Assert.Equal(
            AppearancePresetsNorthernLuzon.Luz01.Recipe.Hair.Catalog.Id,
            AppearancePresetsNorthernLuzon.Luz03.Recipe.Hair.Catalog.Id);
        Assert.Equal(
            AppearancePresetsNorthernLuzon.Luz01.Recipe.HeadCovering.Catalog.Id,
            AppearancePresetsNorthernLuzon.Luz03.Recipe.HeadCovering.Catalog.Id);
        Assert.NotEqual(
            AppearancePresetsNorthernLuzon.Luz01.Recipe.SashBelt.Catalog.Id,
            AppearancePresetsNorthernLuzon.Luz03.Recipe.SashBelt.Catalog.Id);
        Assert.NotEqual(
            AppearancePresetsNorthernLuzon.Luz01.Recipe.Condition.Catalog.Id,
            AppearancePresetsNorthernLuzon.Luz03.Recipe.Condition.Catalog.Id);

        Assert.True(
            AppearancePresetValidator.SatisfiesDifferentiation(
                AppearancePresetsNorthernLuzon.Luz01, AppearancePresetsNorthernLuzon.Luz03));
    }

    // --- Loadout-pool totality and the H1 filter ---

    [Fact]
    public void HasLoadoutPoolTotality_HoldsForTheShippedNorthernLuzonRosterAcrossEveryWeapon()
    {
        Assert.True(AppearancePresetValidator.HasLoadoutPoolTotality(AppearancePresetsNorthernLuzon.All));
    }

    [Fact]
    public void Luz08_IsExcludedForNonWasayWeaponsAndIncludedForWasay()
    {
        foreach (var weapon in Enum.GetValues<PawnWeaponRole>())
        {
            var pool = FilterCompatible(AppearancePresetsNorthernLuzon.All, weapon);
            var includesLuz08 = pool.Any(p => p.Catalog.Id == "appearance.presetLuzon.luz08");

            Assert.Equal(weapon == PawnWeaponRole.Wasay, includesLuz08);
        }
    }

    // --- Prohibition 1: C6 (feathered headdress) appears only in this block, roster-wide ---

    [Fact]
    public void All_OnlyLuz02Luz04AndLuz08RenderTheFeatheredHeaddressAcrossTheWholeShippedRoster()
    {
        var c6RowIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "appearance.presetLuzon.luz02",
            "appearance.presetLuzon.luz04",
            "appearance.presetLuzon.luz08",
        };

        foreach (var preset in AppearancePresets.All)
        {
            var rendersC6 = preset.Recipe.HeadCovering.Catalog.Id ==
                AppearanceComponentCatalog.HeadCoveringC6FeatheredHeaddress.Catalog.Id;

            Assert.Equal(c6RowIds.Contains(preset.Catalog.Id), rendersC6);
        }
    }

    [Fact]
    public void All_NoNorthernLuzonPresetRendersAPutongTattooOrGoldComponent()
    {
        // "No tattoo tone, no putong, no gold ensemble" (design, Northern
        // Luzon block preamble).
        foreach (var preset in AppearancePresetsNorthernLuzon.All)
        {
            Assert.NotEqual(
                AppearanceComponentCatalog.HeadCoveringC1PutongPlain.Catalog.Id,
                preset.Recipe.HeadCovering.Catalog.Id);
            Assert.NotEqual(
                AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged.Catalog.Id,
                preset.Recipe.HeadCovering.Catalog.Id);
            Assert.NotEqual(
                AppearanceComponentCatalog.LowerGarmentE2DyedGoldEdged.Catalog.Id,
                preset.Recipe.LowerGarment.Catalog.Id);
            Assert.Empty(preset.Recipe.Adornments);
        }
    }

    // --- Fallback chain ---

    [Theory]
    [InlineData("appearance.presetLuzon.luz01", "appearance.presetLevy.lev01")]
    [InlineData("appearance.presetLuzon.luz02", "appearance.presetLuzon.luz01")]
    [InlineData("appearance.presetLuzon.luz03", "appearance.presetLuzon.luz01")]
    [InlineData("appearance.presetLuzon.luz04", "appearance.presetLuzon.luz02")]
    [InlineData("appearance.presetLuzon.luz05", "appearance.presetLuzon.luz01")]
    [InlineData("appearance.presetLuzon.luz06", "appearance.presetLuzon.luz01")]
    [InlineData("appearance.presetLuzon.luz07", "appearance.presetLuzon.luz01")]
    [InlineData("appearance.presetLuzon.luz08", "appearance.presetLuzon.luz02")]
    public void All_MatchesTheDesignTableFallbackColumn(string presetId, string expectedFallbackId)
    {
        var preset = Assert.Single(AppearancePresetsNorthernLuzon.All, p => p.Catalog.Id == presetId);

        Assert.Equal(expectedFallbackId, preset.FallbackId);
    }

    // --- Roster-wide checks (VIS-022's own automated-verification list) ---

    [Fact]
    public void All_RosterCountIsAtLeastFiftyAndPinnedAtFiftyThree()
    {
        Assert.True(
            AppearancePresets.All.Count >= 50,
            $"Roster count {AppearancePresets.All.Count} fell below the R-W3.2 floor of 50.");
        Assert.Equal(53, AppearancePresets.All.Count);
    }

    [Fact]
    public void SelectBlock_IsStablePerSeedAndFactionAcrossManyProbes()
    {
        // Block-assignment stability (R-W3.5, R-W6.2): a fixed
        // (Scenario.Seed, FactionId) pair must always resolve the same
        // block, across repeated calls and across a broad sample of seeds
        // and faction identifiers.
        for (var factionId = 0; factionId < 4; factionId++)
        {
            for (ulong seed = 0; seed < 200; seed++)
            {
                var first = AppearancePresets.SelectBlock(seed, factionId);
                var second = AppearancePresets.SelectBlock(seed, factionId);

                Assert.Equal(first, second);
            }
        }
    }

    [Fact]
    public void GetCompatiblePresets_EveryBlockLoadoutPairResolvesAtLeastOnePresetAcrossAllFourBlocks()
    {
        // Pool totality across all four blocks and all loadouts, the last of
        // VIS-022's named automated-verification checks.
        var shippedBlocks = new[]
        {
            VisualScopeTag.Visayan,
            VisualScopeTag.Tagalog,
            VisualScopeTag.Cagayan,
            VisualScopeTag.UnscopedGeneric,
        };

        foreach (var block in shippedBlocks)
        {
            foreach (var weapon in Enum.GetValues<PawnWeaponRole>())
            {
                var pool = AppearancePresets.GetCompatiblePresets(block, weapon);

                Assert.NotEmpty(pool);
            }
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
