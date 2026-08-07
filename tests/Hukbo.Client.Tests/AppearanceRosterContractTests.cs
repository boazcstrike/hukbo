using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Tests;

/// <summary>
/// VIS-039 — the post-milestone full-roster content contract
/// (implementation-plan-draft.md VIS-039; warrior-appearance-design.md
/// "Regional grouping and the prohibitions"; warrior-appearance-historical-
/// research.md section 4). Unlike the per-block test files (VIS-020/021/022),
/// which each pin their own block's own roster in isolation, this file's own
/// concern is exactly the four checks the task spec names as the "post-
/// milestone content contract" and that no single block's own file is
/// positioned to make on its own:
/// <list type="number">
/// <item>One negative test per one of the research's ten prohibited
/// combinations (section 4, "Prohibited combinations"; R-X.8), each proving a
/// deliberately illegal synthetic recipe trips the same check that the real
/// 53-preset roster never trips.</item>
/// <item>The R-W3.2 preset-count floor (at least 50, pinned at 53).</item>
/// <item>The minimum-differentiation criterion, iterated within each
/// regional block across the <em>whole</em> roster — never across block
/// boundaries, per the design's own scoping decision (RF-02).</item>
/// <item>Loadout-pool totality across every shipped block and every
/// weapon.</item>
/// </list>
/// Plus the pawn-scale exclusion suite for every inspector-only entry
/// (R-W3.2, R-W3.4, R-X.8, R-W1.4): components that carry no
/// <see cref="AppearanceComponentEntry"/> at all (C2, H3, I3, I6, I7, I8) can
/// never be selected by <see cref="AppearancePresets.SelectPreset"/> or
/// appear in any shipped recipe, because every recipe field is populated
/// exclusively from <see cref="AppearanceComponentCatalog"/>'s own static
/// entries.
///
/// This file never touches <see cref="AppearancePresetTests"/>,
/// <see cref="AppearancePresetsVisayanTests"/>,
/// <see cref="AppearancePresetsTagalogTests"/>, or
/// <see cref="AppearancePresetsLuzonTests"/> — those four own their own
/// blocks' structural pins, weakest-link tiers, and fallback chains. The
/// line-by-line historical review of the roster against the research is a
/// human review task tracked in VIS-043, not a test (R-W3 acceptance note).
/// </summary>
public sealed class AppearanceRosterContractTests
{
    private static readonly HashSet<VisualScopeTag> ShippedBlocks =
    [
        VisualScopeTag.Visayan,
        VisualScopeTag.Tagalog,
        VisualScopeTag.Cagayan,
        VisualScopeTag.UnscopedGeneric,
    ];

    // Elite/chief/leader rows only — the roster's own strict gold-bearing set
    // (AppearancePresetsVisayanTests.StrictGoldComponents_..., the Tagalog
    // block's own "chief"/"leader" scope labels). Vis18 and Tag14 are the
    // roster's separate prosperous-freeman I4-only carve-out, not elite, and
    // are deliberately excluded from this set.
    private static readonly HashSet<string> EliteChiefOrLeaderPresetIds = new(StringComparer.Ordinal)
    {
        "appearance.presetVisayan.vis13",
        "appearance.presetVisayan.vis14",
        "appearance.presetVisayan.vis15",
        "appearance.presetTagalog.tag13",
        "appearance.presetTagalog.tag15",
    };

    // --- R-W3.2: preset-count floor ---

    [Fact]
    public void All_RosterCountMeetsTheFiftyPresetFloorAndPinsAtFiftyThree()
    {
        Assert.True(
            AppearancePresets.All.Count >= 50,
            $"Roster count {AppearancePresets.All.Count} fell below the R-W3.2 floor of 50.");
        Assert.Equal(53, AppearancePresets.All.Count);
    }

    // --- R-X.8: ten prohibited combinations, one negative test each ---

    [Fact]
    public void Prohibition01_CagayanFeatheredHeaddressNeverAppearsOutsideTheCagayanBlock()
    {
        // Deliberately illegal recipe: TAG-01 cloned with the Cagayan-only
        // C6 feathered headdress swapped in while staying in the Tagalog
        // block — "the clearest cross-regional mashup hazard in the whole
        // system" (research, prohibition 1).
        var illegal = AppearancePresetsTagalog.Tag01 with
        {
            Recipe = AppearancePresetsTagalog.Tag01.Recipe with
            {
                HeadCovering = AppearanceComponentCatalog.HeadCoveringC6FeatheredHeaddress,
            },
        };
        Assert.True(RendersFeatheredHeaddressOutsideCagayan(illegal));

        foreach (var preset in AppearancePresets.All)
        {
            Assert.False(
                RendersFeatheredHeaddressOutsideCagayan(preset),
                $"{preset.Catalog.Id} renders the Cagayan-only feathered headdress outside the Cagayan block.");
        }
    }

    private static bool RendersFeatheredHeaddressOutsideCagayan(AppearancePresetEntry preset) =>
        preset.Block != VisualScopeTag.Cagayan &&
        preset.Recipe.HeadCovering.Catalog.Id ==
            AppearanceComponentCatalog.HeadCoveringC6FeatheredHeaddress.Catalog.Id;

    [Fact]
    public void Prohibition02_FullBodyOrPartialTattoosNeverAppearOutsideTheVisayanBlock()
    {
        // Deliberately illegal recipe: LEV-01 cloned with the Visayas-only
        // full-body tattoo adornment added, while staying in the generic
        // levy (Unscoped-generic) block — the Pintados scope rule
        // (prohibition 2).
        var illegal = AppearancePresets.Lev01 with
        {
            Recipe = AppearancePresets.Lev01.Recipe with
            {
                Adornments = [AppearanceComponentCatalog.AdornmentI1FullBodyTattoos],
            },
        };
        Assert.True(RendersTattooOutsideVisayan(illegal));

        foreach (var preset in AppearancePresets.All)
        {
            Assert.False(
                RendersTattooOutsideVisayan(preset),
                $"{preset.Catalog.Id} renders a tattoo adornment outside the Visayan block.");
        }
    }

    private static bool RendersTattooOutsideVisayan(AppearancePresetEntry preset) =>
        preset.Block != VisualScopeTag.Visayan &&
        preset.Recipe.Adornments.Any(a =>
            a.Catalog.Id == AppearanceComponentCatalog.AdornmentI1FullBodyTattoos.Catalog.Id ||
            a.Catalog.Id == AppearanceComponentCatalog.AdornmentI2PartialTattoos.Catalog.Id);

    [Fact]
    public void Prohibition03_TheTwoRedStatusSystemsNeverBlend()
    {
        // Half one: the red putong (C2) — the Visayan honor mark — ships in
        // no roster at all (OD-5), which structurally guarantees it can
        // never appear on a Tagalog preset either. No AppearanceComponentEntry
        // exists for it, so this is a direct catalog-absence proof rather
        // than a recipe scan.
        Assert.DoesNotContain(
            AppearanceComponentCatalog.All,
            entry => entry.Catalog.Id == "appearance.headCovering.c2");

        // Half two: the red chinina (D3) — the separate, documented Tagalog
        // headman marker — must never appear on a Visayan preset. Deliberately
        // illegal recipe: VIS-01 cloned with D3 swapped in for its plain D1
        // torso garment while staying in the Visayan block.
        var illegal = AppearancePresetsVisayan.Vis01 with
        {
            Recipe = AppearancePresetsVisayan.Vis01.Recipe with
            {
                TorsoGarment = AppearanceComponentCatalog.TorsoD3ChininaRedChiefly,
            },
        };
        Assert.True(RendersRedChininaOutsideTagalog(illegal));

        foreach (var preset in AppearancePresets.All)
        {
            Assert.False(
                RendersRedChininaOutsideTagalog(preset),
                $"{preset.Catalog.Id} renders the Tagalog red chinina outside the Tagalog block.");
        }
    }

    private static bool RendersRedChininaOutsideTagalog(AppearancePresetEntry preset) =>
        preset.Block != VisualScopeTag.Tagalog &&
        preset.Recipe.TorsoGarment.Catalog.Id ==
            AppearanceComponentCatalog.TorsoD3ChininaRedChiefly.Catalog.Id;

    [Fact]
    public void Prohibition04_NoBrassBronzeMailOrGreavesArmorExistsInTheCatalog()
    {
        // Category F ships exactly five entries (F1-F5); none may name the
        // excluded materials (prohibition 4, "category F exclusion list").
        // "plate" alone is deliberately not banned as a bare substring — F4's
        // legitimate "Wooden Breastplate" label would false-positive on it —
        // so the forbidden set below names only the excluded materials and
        // constructions themselves.
        var forbiddenTerms = new[] { "brass", "bronze", "chain mail", "chainmail", "greaves" };

        // Deliberately illegal probe label, proving the term filter below can
        // actually detect a violation rather than being vacuously true.
        const string illegalProbeLabel = "Brass Plate Cuirass with Iron Greaves";
        Assert.Contains(forbiddenTerms, term => illegalProbeLabel.Contains(term, StringComparison.OrdinalIgnoreCase));

        var armorEntries = AppearanceComponentCatalog.All
            .Where(entry => entry.Category == AppearanceComponentCategory.Armor)
            .ToList();
        Assert.Equal(5, armorEntries.Count);

        foreach (var entry in armorEntries)
        {
            foreach (var term in forbiddenTerms)
            {
                Assert.DoesNotContain(term, entry.Catalog.DisplayLabel, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(term, entry.Catalog.Notes, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Prohibition05_TheSalakotTermIsAbsentAndTheSunHatNeverAppearsOnAnEliteChiefOrLeaderPreset()
    {
        // Half one: the term "salakot" fails confirmation inside the
        // research window and must not appear anywhere in the catalog or
        // roster, player-facing or inspector-facing (prohibition 5).
        foreach (var entry in AppearanceComponentCatalog.All)
        {
            Assert.DoesNotContain("salakot", entry.Catalog.DisplayLabel, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("salakot", entry.Catalog.Notes, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var preset in AppearancePresets.All)
        {
            Assert.DoesNotContain("salakot", preset.Catalog.DisplayLabel, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("salakot", preset.Catalog.Notes, StringComparison.OrdinalIgnoreCase);
        }

        // Half two: the woven sun hat (C4) is levy flavor at most and must
        // never dress an elite, chief, or leader preset. Deliberately
        // illegal recipe: the Visayan datu/leader preset (VIS-15) cloned
        // with C4 swapped in for its documented gold-edged putong.
        var illegal = AppearancePresetsVisayan.Vis15 with
        {
            Recipe = AppearancePresetsVisayan.Vis15.Recipe with
            {
                HeadCovering = AppearanceComponentCatalog.HeadCoveringC4WovenSunHat,
            },
        };
        Assert.True(RendersSunHatOnEliteChiefOrLeader(illegal));

        foreach (var preset in AppearancePresets.All)
        {
            Assert.False(
                RendersSunHatOnEliteChiefOrLeader(preset),
                $"{preset.Catalog.Id} renders the woven sun hat despite being an elite/chief/leader preset.");
        }
    }

    private static bool RendersSunHatOnEliteChiefOrLeader(AppearancePresetEntry preset) =>
        EliteChiefOrLeaderPresetIds.Contains(preset.Catalog.Id) &&
        preset.Recipe.HeadCovering.Catalog.Id == AppearanceComponentCatalog.HeadCoveringC4WovenSunHat.Catalog.Id;

    [Fact]
    public void Prohibition06_GoldComponentsNeverAppearOnAGenericLevyPreset()
    {
        // Deliberately illegal recipe: LEV-01 cloned with a gold earring
        // adornment added, while staying in the generic-levy block —
        // "never in the LEV block" (design, "Regional grouping and the
        // prohibitions", item 6).
        var illegal = AppearancePresets.Lev01 with
        {
            Recipe = AppearancePresets.Lev01.Recipe with
            {
                Adornments = [AppearanceComponentCatalog.AdornmentI4GoldEarrings],
            },
        };
        Assert.True(RendersGoldComponent(illegal.Recipe));
        Assert.Equal(VisualScopeTag.UnscopedGeneric, illegal.Block);

        foreach (var preset in AppearancePresets.All.Where(p => p.Block == VisualScopeTag.UnscopedGeneric))
        {
            Assert.False(
                RendersGoldComponent(preset.Recipe),
                $"{preset.Catalog.Id} is a generic-levy preset but renders a gold component.");
        }
    }

    private static bool RendersGoldComponent(AppearancePresetRecipe recipe) =>
        recipe.HeadCovering.Catalog.Id == AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged.Catalog.Id ||
        recipe.LowerGarment.Catalog.Id == AppearanceComponentCatalog.LowerGarmentE2DyedGoldEdged.Catalog.Id ||
        recipe.Adornments.Any(a =>
            a.Catalog.Id == AppearanceComponentCatalog.AdornmentI4GoldEarrings.Catalog.Id ||
            a.Catalog.Id == AppearanceComponentCatalog.AdornmentI5GoldNecklace.Catalog.Id);

    [Fact]
    public void Prohibition07_NoMotifOrPatternRenderChannelExistsForTattooing()
    {
        // Prohibition 7: motif-level tattoo detail is unsourceable at pawn
        // scale, so no "motif" (or "pattern") render channel may exist at
        // all — the prohibition holds by construction, not by a runtime
        // filter, so this test pins the enum shape directly.
        var channelNames = Enum.GetNames<AppearanceRenderChannel>();
        Assert.Equal(3, channelNames.Length);
        foreach (var name in channelNames)
        {
            Assert.DoesNotContain("motif", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pattern", name, StringComparison.OrdinalIgnoreCase);
        }

        // Both tattoo adornments render exclusively through the tone-shift
        // color-block channel — never silhouette, never a would-be "motif"
        // channel.
        Assert.Equal(
            AppearanceRenderChannel.ColorBlock,
            AppearanceComponentCatalog.AdornmentI1FullBodyTattoos.RenderChannel);
        Assert.Equal(
            AppearanceRenderChannel.ColorBlock,
            AppearanceComponentCatalog.AdornmentI2PartialTattoos.RenderChannel);
    }

    [Fact]
    public void Prohibition08_NoEuropeanElementOrFootwearComponentExistsInTheCatalog()
    {
        var forbiddenTerms = new[] { "crest", "doublet", "boots", "boot", "shoe", "footwear", "sandal" };

        // Deliberately illegal probe label, proving the term filter below can
        // actually detect a violation rather than being vacuously true.
        const string illegalProbeLabel = "European Doublet, Crested Helmet, and Boots";
        Assert.Contains(forbiddenTerms, term => illegalProbeLabel.Contains(term, StringComparison.OrdinalIgnoreCase));

        foreach (var entry in AppearanceComponentCatalog.All)
        {
            foreach (var term in forbiddenTerms)
            {
                Assert.DoesNotContain(term, entry.Catalog.DisplayLabel, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(term, entry.Catalog.Notes, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Prohibition09_NoLaterMoroSpecificKitExistsInTheCatalog()
    {
        var forbiddenTerms = new[] { "moro", "sultanate" };

        // Deliberately illegal probe label, proving the term filter below can
        // actually detect a violation rather than being vacuously true.
        const string illegalProbeLabel = "Later Moro Sultanate Brass Armor";
        Assert.Contains(forbiddenTerms, term => illegalProbeLabel.Contains(term, StringComparison.OrdinalIgnoreCase));

        foreach (var entry in AppearanceComponentCatalog.All)
        {
            foreach (var term in forbiddenTerms)
            {
                Assert.DoesNotContain(term, entry.Catalog.DisplayLabel, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(term, entry.Catalog.Notes, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Prohibition10_EveryPresetCarriesAScopeTagAndBlockAssignmentNeverMixesTwoRegionsInOneMatch()
    {
        // Deliberately illegal recipe: LEV-01 cloned with no scope tag at
        // all (VisualScopeTag.NotApplicable) rather than one of the four
        // shipped regional blocks. Constructible because `with` bypasses the
        // primary-constructor validation that would otherwise reject it —
        // exactly why this mechanical check exists.
        var illegal = AppearancePresets.Lev01 with
        {
            Catalog = AppearancePresets.Lev01.Catalog with { ScopeTag = VisualScopeTag.NotApplicable },
            Block = VisualScopeTag.NotApplicable,
        };
        Assert.DoesNotContain(illegal.Block, ShippedBlocks);

        // Real roster: every shipped preset carries one of the four shipped
        // regional blocks, shown in the inspector as its scope tag, and the
        // two never drift apart (prohibition 10).
        foreach (var preset in AppearancePresets.All)
        {
            Assert.Contains(preset.Block, ShippedBlocks);
            Assert.Equal(preset.Block, preset.Catalog.ScopeTag);
        }

        // Block assignment gives each faction exactly one region per match —
        // no pan-archipelagic pool ever exists at selection time (design,
        // "Deterministic selection" step 1).
        for (var factionId = 0; factionId < 4; factionId++)
        {
            for (ulong seed = 0; seed < 100; seed++)
            {
                Assert.Contains(AppearancePresets.SelectBlock(seed, factionId), ShippedBlocks);
            }
        }
    }

    // --- Minimum differentiation, within each regional block, over the full roster ---

    [Fact]
    public void SatisfiesDifferentiation_HoldsForEveryPairWithinEachRegionalBlockAcrossTheFullRoster()
    {
        // "the suite iterates each regional block's pairs, never cross-block
        // pairs" (task spec). Grouping AppearancePresets.All by Block, rather
        // than reusing each block's own .All list, proves the grouping
        // itself resolves to the same four blocks the per-block files ship.
        var byBlock = AppearancePresets.All.GroupBy(preset => preset.Block).ToList();
        Assert.Equal(4, byBlock.Count);

        foreach (var block in byBlock)
        {
            var presets = block.ToList();
            for (var i = 0; i < presets.Count; i++)
            {
                for (var j = i + 1; j < presets.Count; j++)
                {
                    Assert.True(
                        AppearancePresetValidator.SatisfiesDifferentiation(presets[i], presets[j]),
                        $"{presets[i].Catalog.Id} and {presets[j].Catalog.Id} (block {block.Key}) do not " +
                        "satisfy the minimum differentiation criterion.");
                }
            }
        }
    }

    [Fact]
    public void CrossBlockNearDuplicates_AreAcceptedByDesign()
    {
        // The design's own named example: VIS-01 (Visayan) and LEV-01
        // (Unscoped-generic) share a recipe-identical component set. The
        // criterion is deliberately scoped within each block, so this pair
        // fails SatisfiesDifferentiation if compared directly — and that is
        // by design, not a defect, because block assignment guarantees the
        // two blocks never co-exist inside one faction's army, so the pair
        // never actually competes at selection time.
        var vis01 = AppearancePresetsVisayan.Vis01;
        var lev01 = AppearancePresets.Lev01;

        Assert.NotEqual(vis01.Block, lev01.Block);
        Assert.Equal(vis01.Recipe.Hair.Catalog.Id, lev01.Recipe.Hair.Catalog.Id);
        Assert.Equal(vis01.Recipe.HeadCovering.Catalog.Id, lev01.Recipe.HeadCovering.Catalog.Id);
        Assert.Equal(vis01.Recipe.TorsoGarment.Catalog.Id, lev01.Recipe.TorsoGarment.Catalog.Id);
        Assert.Equal(vis01.Recipe.LowerGarment.Catalog.Id, lev01.Recipe.LowerGarment.Catalog.Id);
        Assert.Equal(vis01.Recipe.SashBelt.Catalog.Id, lev01.Recipe.SashBelt.Catalog.Id);
        Assert.Equal(vis01.Recipe.Condition.Catalog.Id, lev01.Recipe.Condition.Catalog.Id);

        Assert.False(AppearancePresetValidator.SatisfiesDifferentiation(vis01, lev01));
    }

    // --- Loadout-pool totality across all blocks and all loadouts ---

    [Fact]
    public void HasLoadoutPoolTotality_HoldsForTheFullShippedRosterAcrossEveryWeapon()
    {
        Assert.True(AppearancePresetValidator.HasLoadoutPoolTotality(AppearancePresets.All));
    }

    [Fact]
    public void GetCompatiblePresets_EveryShippedBlockAndLoadoutPairResolvesAtLeastOnePreset()
    {
        foreach (var block in Enum.GetValues<VisualScopeTag>())
        {
            foreach (var weapon in Enum.GetValues<PawnWeaponRole>())
            {
                var pool = AppearancePresets.GetCompatiblePresets(block, weapon);

                if (block == VisualScopeTag.NotApplicable)
                {
                    // No shipped preset ever declares NotApplicable as its
                    // own Block (prohibition 10, pinned above) — this pool
                    // must stay empty for every weapon.
                    Assert.Empty(pool);
                }
                else
                {
                    Assert.NotEmpty(pool);
                }
            }
        }
    }

    // --- Exclusion suite: every inspector-only entry is unreachable by any selection stream ---

    [Theory]
    [InlineData("appearance.headCovering.c2")] // C2 — earned red head wrap; excluded entirely, OD-5.
    [InlineData("appearance.accessory.h3")] // H3 — betel pouch; not renderable at pawn scale.
    [InlineData("appearance.adornment.i3")] // I3 — facial tattooing; not renderable at pawn scale.
    [InlineData("appearance.adornment.i6")] // I6 — gold armlets; not renderable at pawn scale.
    [InlineData("appearance.adornment.i7")] // I7 — gold dental work; not renderable at pawn scale.
    [InlineData("appearance.adornment.i8")] // I8 — tooth filing/blackening; not renderable at pawn scale.
    public void InspectorOnlyComponents_HaveNoCatalogEntryAndAreUnreachableByAnySelectionStream(
        string inspectorOnlyId)
    {
        // No AppearanceComponentEntry exists for this identifier at all —
        // the catalog ships strictly fewer entries than the research's full
        // category tally for exactly this reason (AppearanceComponentCatalog
        // class remarks).
        Assert.DoesNotContain(AppearanceComponentCatalog.All, entry => entry.Catalog.Id == inspectorOnlyId);

        // And therefore no shipped preset's recipe can reference it either —
        // every recipe field is populated exclusively from
        // AppearanceComponentCatalog's own static entries, so an id with no
        // catalog entry can never be selected by any recipe field,
        // structurally, not merely by review discipline.
        foreach (var preset in AppearancePresets.All)
        {
            Assert.NotEqual(inspectorOnlyId, preset.Recipe.Hair.Catalog.Id);
            Assert.NotEqual(inspectorOnlyId, preset.Recipe.HeadCovering.Catalog.Id);
            Assert.NotEqual(inspectorOnlyId, preset.Recipe.TorsoGarment.Catalog.Id);
            Assert.NotEqual(inspectorOnlyId, preset.Recipe.LowerGarment.Catalog.Id);
            Assert.NotEqual(inspectorOnlyId, preset.Recipe.SashBelt.Catalog.Id);
            Assert.NotEqual(inspectorOnlyId, preset.Recipe.Condition.Catalog.Id);

            if (preset.Recipe.Armor is { } armor)
            {
                Assert.NotEqual(inspectorOnlyId, armor.Catalog.Id);
            }

            if (preset.Recipe.Accessory is { } accessory)
            {
                Assert.NotEqual(inspectorOnlyId, accessory.Catalog.Id);
            }

            foreach (var adornment in preset.Recipe.Adornments)
            {
                Assert.NotEqual(inspectorOnlyId, adornment.Catalog.Id);
            }
        }
    }
}
