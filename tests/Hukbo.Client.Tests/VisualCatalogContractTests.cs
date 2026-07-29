using System.Reflection;
using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Diagnostics;

namespace Hukbo.Client.Tests;

/// <summary>
/// The shared, milestone-scope contract suite over every visual catalog and
/// piece of shared visual infrastructure shipped so far (implementation-
/// plan-draft.md VIS-037; visual-system-integration-design.md sections 2 and
/// 4): exact identifier-string pins for every shipped entry (R-W6.1) so a
/// reword fails, index contiguity and uniqueness across the weapon, shield,
/// appearance, and backdrop catalogs, mandatory metadata presence, the
/// pair-form rule (R-X.7), salt-registry distinctness (extending
/// <c>PresentationSaltsTests</c>, R-W6.2), fallback totality proven against
/// each domain's own real shipped entry type (R-W6.4), and the three
/// missing-visual <see cref="LogEvents"/> constants (R-W6.5).
/// </summary>
/// <remarks>
/// Each per-catalog test file (<c>WeaponVisualCatalogTests</c>,
/// <c>ShieldVisualCatalogTests</c>, <c>AppearanceComponentCatalogTests</c>,
/// <c>BackdropVisualCatalogTests</c>, <c>PresentationSaltsTests</c>,
/// <c>VisualFallbackResolverTests</c>, <c>LogEventCatalogTests</c>) already
/// covers its own domain in depth; this suite is the cross-catalog contract
/// those files do not individually enforce, and it is green when it passes
/// over the milestone content shipped as of VIS-037 — a later content task
/// extends the pinned tables and tallies below in its own diff exactly as
/// its own new entries ship (the task's own "Automated verification"
/// remit).
/// </remarks>
public sealed class VisualCatalogContractTests
{
    // ================= Domain entry collectors =================
    //
    // One private helper per domain's real selection-index space, reused by
    // every section below. Weapon silhouettes and weapon tints are collected
    // separately from each other — and shield's family-default/model-
    // category-default fallback targets are collected separately from its
    // real skin list — because both pairs share the same
    // VisualCatalogValidator "domain.family" grouping prefix while occupying
    // independent index sequences by design (WeaponVisualCatalog and
    // ShieldVisualCatalog's own remarks); combining them in one
    // VisualCatalogValidator.Validate call would report a false index
    // collision between two entries that were never meant to share a
    // selection-index space.

    private static VisualCatalogEntry[] AllWeaponSilhouetteEntries() =>
        WeaponVisualCatalog.KalisSilhouettes
            .Concat(WeaponVisualCatalog.KampilanSilhouettes)
            .Concat(WeaponVisualCatalog.WasaySilhouettes)
            .Concat(WeaponVisualCatalog.ItakSilhouettes)
            .Select(entry => entry.Catalog)
            .ToArray();

    private static VisualCatalogEntry[] AllWeaponTintEntries() =>
        WeaponVisualCatalog.GetTints(PawnWeaponRole.Kalis)
            .Concat(WeaponVisualCatalog.GetTints(PawnWeaponRole.Kampilan))
            .Concat(WeaponVisualCatalog.GetTints(PawnWeaponRole.Wasay))
            .Concat(WeaponVisualCatalog.GetTints(PawnWeaponRole.Itak))
            .Select(entry => entry.Catalog)
            .Append(WeaponVisualCatalog.ModelCategoryDefaultTint.Catalog)
            .ToArray();

    private static VisualCatalogEntry[] AllShieldSkinEntries() =>
        ShieldVisualCatalog.TallHardwoodSkins
            .Select(entry => entry.Catalog)
            .ToArray();

    private static VisualCatalogEntry[] AllShieldEntries() =>
        AllShieldSkinEntries()
            .Append(ShieldVisualCatalog.Default.Catalog)
            .Append(ShieldVisualCatalog.ModelCategoryDefault.Catalog)
            .ToArray();

    private static VisualCatalogEntry[] AllAppearanceEntries() =>
        AppearanceComponentCatalog.All.Select(entry => entry.Catalog).ToArray();

    private static VisualCatalogEntry[] AllShippedCatalogEntries() =>
        AllWeaponSilhouetteEntries()
            .Concat(AllWeaponTintEntries())
            .Concat(AllShieldEntries())
            .Concat(BackdropVisualCatalog.All)
            .Concat(AllAppearanceEntries())
            .ToArray();

    private static string DescribeFailures(VisualCatalogValidationResult result) =>
        string.Join(
            "; ",
            result.Failures.Select(failure =>
                $"{failure.EntryId}: {failure.Reason}" +
                (failure.Message is null ? string.Empty : $" ({failure.Message})")));

    // ================= R-W6.1: exact identifier-string pins =================
    //
    // A reword or a renumber of any shipped identifier below fails its own
    // pinned case; a new shipped entry with no matching pin is caught by the
    // coverage-count guards further down instead of silently passing.

    public static TheoryData<string, string> PinnedWeaponIds()
    {
        var data = new TheoryData<string, string>();
        data.Add(WeaponVisualCatalog.KalisL1.Catalog.Id, "weapon.kalis.l1");
        data.Add(WeaponVisualCatalog.KalisL2.Catalog.Id, "weapon.kalis.l2");
        data.Add(WeaponVisualCatalog.KalisL3.Catalog.Id, "weapon.kalis.l3");
        data.Add(WeaponVisualCatalog.KalisTintFreshIron.Catalog.Id, "weapon.kalis.tint.freshIron");
        data.Add(WeaponVisualCatalog.KalisTintDarkHilt.Catalog.Id, "weapon.kalis.tint.darkHilt");
        data.Add(WeaponVisualCatalog.KampilanK1.Catalog.Id, "weapon.kampilan.k1");
        data.Add(WeaponVisualCatalog.KampilanK2.Catalog.Id, "weapon.kampilan.k2");
        data.Add(WeaponVisualCatalog.KampilanTintFreshIron.Catalog.Id, "weapon.kampilan.tint.freshIron");
        data.Add(WeaponVisualCatalog.KampilanTintWornIron.Catalog.Id, "weapon.kampilan.tint.wornIron");
        data.Add(WeaponVisualCatalog.KampilanTintOchreHilt.Catalog.Id, "weapon.kampilan.tint.ochreHilt");
        data.Add(WeaponVisualCatalog.WasayW1.Catalog.Id, "weapon.wasay.w1");
        data.Add(WeaponVisualCatalog.WasayTintOchreHaft.Catalog.Id, "weapon.wasay.tint.ochreHaft");
        data.Add(WeaponVisualCatalog.WasayTintCharredHaft.Catalog.Id, "weapon.wasay.tint.charredHaft");
        data.Add(WeaponVisualCatalog.WasayTintLashedWorn.Catalog.Id, "weapon.wasay.tint.lashedWorn");
        data.Add(WeaponVisualCatalog.ItakI1.Catalog.Id, "weapon.itak.i1");
        data.Add(WeaponVisualCatalog.ItakTintPlainOchre.Catalog.Id, "weapon.itak.tint.plainOchre");
        data.Add(WeaponVisualCatalog.ItakTintWornField.Catalog.Id, "weapon.itak.tint.wornField");
        data.Add(WeaponVisualCatalog.ModelCategoryDefaultTint.Catalog.Id, "weapon.category.bladedDefault");
        return data;
    }

    [Theory]
    [MemberData(nameof(PinnedWeaponIds))]
    public void Weapon_ShippedIdentifierIsPinned(string actual, string expected) =>
        Assert.Equal(expected, actual);

    [Fact]
    public void Weapon_PinnedIdentifierTableCoversEveryShippedSilhouetteAndTint()
    {
        var liveCount = AllWeaponSilhouetteEntries().Length + AllWeaponTintEntries().Length;
        Assert.Equal(liveCount, PinnedWeaponIds().Count());
    }

    public static TheoryData<string, string> PinnedShieldIds()
    {
        var data = new TheoryData<string, string>();
        data.Add(ShieldVisualCatalog.MactanThin.Catalog.Id, "shield.tallHardwood.mactanThin");
        data.Add(ShieldVisualCatalog.MorgaFullBody.Catalog.Id, "shield.tallHardwood.morgaFullBody");
        data.Add(ShieldVisualCatalog.BoxerCagayan.Catalog.Id, "shield.tallHardwood.boxerCagayan");
        data.Add(ShieldVisualCatalog.VisayanKalasag.Catalog.Id, "shield.tallHardwood.visayanKalasag");
        data.Add(ShieldVisualCatalog.Default.Catalog.Id, "shield.tallHardwood.default");
        data.Add(ShieldVisualCatalog.ModelCategoryDefault.Catalog.Id, "shield.category.tallDefault");
        return data;
    }

    [Theory]
    [MemberData(nameof(PinnedShieldIds))]
    public void Shield_ShippedIdentifierIsPinned(string actual, string expected) =>
        Assert.Equal(expected, actual);

    [Fact]
    public void Shield_PinnedIdentifierTableCoversEveryShippedSkinAndFallbackTarget()
    {
        Assert.Equal(AllShieldEntries().Length, PinnedShieldIds().Count());
    }

    [Fact]
    public void Backdrop_ShippedIdentifierIsPinned() =>
        Assert.Equal("backdrop.grass.cluster", BackdropVisualCatalog.GrassCluster.Id);

    public static TheoryData<string, string> PinnedAppearanceIds()
    {
        var data = new TheoryData<string, string>();
        data.Add(AppearanceComponentCatalog.HairB1LongHairKnotted.Catalog.Id, "appearance.hair.b1");
        data.Add(AppearanceComponentCatalog.HairB2LongLooseHair.Catalog.Id, "appearance.hair.b2");
        data.Add(AppearanceComponentCatalog.HairB3Cropped.Catalog.Id, "appearance.hair.b3");
        data.Add(AppearanceComponentCatalog.HairB4TuckedUnderWrap.Catalog.Id, "appearance.hair.b4");
        data.Add(AppearanceComponentCatalog.HeadCoveringC1PutongPlain.Catalog.Id, "appearance.headCovering.c1");
        data.Add(AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged.Catalog.Id, "appearance.headCovering.c3");
        data.Add(AppearanceComponentCatalog.HeadCoveringC4WovenSunHat.Catalog.Id, "appearance.headCovering.c4");
        data.Add(AppearanceComponentCatalog.HeadCoveringC5BareHead.Catalog.Id, "appearance.headCovering.c5");
        data.Add(AppearanceComponentCatalog.HeadCoveringC6FeatheredHeaddress.Catalog.Id, "appearance.headCovering.c6");
        data.Add(AppearanceComponentCatalog.TorsoD1BareChested.Catalog.Id, "appearance.torso.d1");
        data.Add(AppearanceComponentCatalog.TorsoD2ChininaIndigoOrBlack.Catalog.Id, "appearance.torso.d2");
        data.Add(AppearanceComponentCatalog.TorsoD3ChininaRedChiefly.Catalog.Id, "appearance.torso.d3");
        data.Add(AppearanceComponentCatalog.TorsoD4AbacaJacket.Catalog.Id, "appearance.torso.d4");
        data.Add(AppearanceComponentCatalog.LowerGarmentE1Bahag.Catalog.Id, "appearance.lowerGarment.e1");
        data.Add(AppearanceComponentCatalog.LowerGarmentE2DyedGoldEdged.Catalog.Id, "appearance.lowerGarment.e2");
        data.Add(AppearanceComponentCatalog.LowerGarmentE3WaistCloth.Catalog.Id, "appearance.lowerGarment.e3");
        data.Add(AppearanceComponentCatalog.ArmorF1Unarmored.Catalog.Id, "appearance.armor.f1");
        data.Add(AppearanceComponentCatalog.ArmorF2CordedFiberArmor.Catalog.Id, "appearance.armor.f2");
        data.Add(AppearanceComponentCatalog.ArmorF3HideCorselet.Catalog.Id, "appearance.armor.f3");
        data.Add(AppearanceComponentCatalog.ArmorF4WoodenBreastplate.Catalog.Id, "appearance.armor.f4");
        data.Add(AppearanceComponentCatalog.ArmorF5ShellSetHelmet.Catalog.Id, "appearance.armor.f5");
        data.Add(AppearanceComponentCatalog.SashBeltG1RedWaistSash.Catalog.Id, "appearance.sashBelt.g1");
        data.Add(AppearanceComponentCatalog.SashBeltG2ClothBelt.Catalog.Id, "appearance.sashBelt.g2");
        data.Add(AppearanceComponentCatalog.SashBeltG3CordBelt.Catalog.Id, "appearance.sashBelt.g3");
        data.Add(AppearanceComponentCatalog.AccessoryH1SheathedSideBlade.Catalog.Id, "appearance.accessory.h1");
        data.Add(AppearanceComponentCatalog.AccessoryH2DrapedShoulderCloth.Catalog.Id, "appearance.accessory.h2");
        data.Add(AppearanceComponentCatalog.AdornmentI1FullBodyTattoos.Catalog.Id, "appearance.adornment.i1");
        data.Add(AppearanceComponentCatalog.AdornmentI2PartialTattoos.Catalog.Id, "appearance.adornment.i2");
        data.Add(AppearanceComponentCatalog.AdornmentI4GoldEarrings.Catalog.Id, "appearance.adornment.i4");
        data.Add(AppearanceComponentCatalog.AdornmentI5GoldNecklace.Catalog.Id, "appearance.adornment.i5");
        data.Add(AppearanceComponentCatalog.ConditionK1Clean.Catalog.Id, "appearance.condition.k1");
        data.Add(AppearanceComponentCatalog.ConditionK2DustyMuddy.Catalog.Id, "appearance.condition.k2");
        data.Add(AppearanceComponentCatalog.ConditionK3SweatSheen.Catalog.Id, "appearance.condition.k3");
        data.Add(AppearanceComponentCatalog.ConditionK4FadedDye.Catalog.Id, "appearance.condition.k4");
        data.Add(AppearanceComponentCatalog.ConditionK5BattleWorn.Catalog.Id, "appearance.condition.k5");
        return data;
    }

    [Theory]
    [MemberData(nameof(PinnedAppearanceIds))]
    public void Appearance_ShippedIdentifierIsPinned(string actual, string expected) =>
        Assert.Equal(expected, actual);

    [Fact]
    public void Appearance_PinnedIdentifierTableCoversTheEntireShippedCatalog() =>
        Assert.Equal(AppearanceComponentCatalog.All.Count, PinnedAppearanceIds().Count());

    // ================= Index contiguity and uniqueness across catalogs =================

    [Fact]
    public void Weapon_SilhouetteIndexSpaceHasNoDuplicateIdOrCollidingIndexWithinAnyWeaponFamily()
    {
        var result = VisualCatalogValidator.Validate("weapon.silhouette", AllWeaponSilhouetteEntries());
        Assert.True(result.IsValid, DescribeFailures(result));
    }

    [Fact]
    public void Weapon_TintIndexSpaceHasNoDuplicateIdOrCollidingIndexWithinAnyWeaponFamily()
    {
        var result = VisualCatalogValidator.Validate("weapon.tint", AllWeaponTintEntries());
        Assert.True(result.IsValid, DescribeFailures(result));
    }

    [Fact]
    public void Shield_SkinIndexSpaceHasNoDuplicateIdOrCollidingIndex()
    {
        var result = VisualCatalogValidator.Validate("shield.skin", AllShieldSkinEntries());
        Assert.True(result.IsValid, DescribeFailures(result));
    }

    [Fact]
    public void Shield_FallbackTargetIdentifiersAreDistinctFromEveryRealSkinAndFromEachOther()
    {
        // Default and ModelCategoryDefault share the "shield.tallHardwood"
        // family prefix with the real skin index space (Default's own Index
        // is 0, matching MactanThin's), so they are checked directly rather
        // than through VisualCatalogValidator's family-index grouping — see
        // the domain-entry-collector remarks above.
        var skinIds = AllShieldSkinEntries().Select(entry => entry.Id).ToArray();

        Assert.DoesNotContain(ShieldVisualCatalog.Default.Catalog.Id, skinIds);
        Assert.DoesNotContain(ShieldVisualCatalog.ModelCategoryDefault.Catalog.Id, skinIds);
        Assert.NotEqual(
            ShieldVisualCatalog.Default.Catalog.Id,
            ShieldVisualCatalog.ModelCategoryDefault.Catalog.Id);
    }

    [Fact]
    public void Appearance_FullCatalogHasNoDuplicateIdOrCollidingIndexWithinAnyCategoryFamily()
    {
        var result = VisualCatalogValidator.Validate("appearance", AllAppearanceEntries());
        Assert.True(result.IsValid, DescribeFailures(result));
    }

    [Fact]
    public void Backdrop_CatalogHasNoDuplicateIdOrCollidingIndex()
    {
        var result = VisualCatalogValidator.Validate("backdrop", BackdropVisualCatalog.All);
        Assert.True(result.IsValid, DescribeFailures(result));
    }

    [Fact]
    public void AllFourDomains_EveryShippedIdentifierIsGloballyUnique()
    {
        var ids = AllShippedCatalogEntries().Select(entry => entry.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AllFourDomains_EveryShippedIdentifierUsesTheWellFormedCatalogGrammar()
    {
        foreach (var entry in AllShippedCatalogEntries())
        {
            Assert.True(
                VisualCatalogGrammar.IsWellFormedId(entry.Id),
                $"'{entry.Id}' is not a well-formed catalog identifier.");
        }
    }

    [Fact]
    public void AllFourDomains_ShippedEntryTotalMatchesTheMilestoneTally()
    {
        // 7 weapon silhouettes + 11 weapon tints (including the model-
        // category default) + 6 shield entries (4 skins, the family
        // default, the model-category default) + 1 backdrop entry + 35
        // appearance component entries. A future content task that ships a
        // new catalog entry updates this tally, and the pinned table it
        // belongs to, in its own diff (VIS-037's goal statement).
        Assert.Equal(60, AllShippedCatalogEntries().Length);
    }

    // ================= Metadata presence (evidence tier everywhere; scope tag) =================

    [Fact]
    public void AllShippedEntries_CarryADefinedEvidenceTier()
    {
        foreach (var entry in AllShippedCatalogEntries())
        {
            Assert.True(Enum.IsDefined(entry.EvidenceTier), $"'{entry.Id}' has an undefined evidence tier.");
        }
    }

    [Fact]
    public void AllShippedEntries_CarryADefinedScopeTag()
    {
        foreach (var entry in AllShippedCatalogEntries())
        {
            Assert.True(Enum.IsDefined(entry.ScopeTag), $"'{entry.Id}' has an undefined scope tag.");
        }
    }

    [Fact]
    public void AllShippedEntries_CarryADefinedMinimumDetailTier()
    {
        foreach (var entry in AllShippedCatalogEntries())
        {
            Assert.True(Enum.IsDefined(entry.MinimumDetailTier), $"'{entry.Id}' has an undefined minimum detail tier.");
        }
    }

    [Fact]
    public void AllShippedEntries_CarryANonNullNotesField()
    {
        foreach (var entry in AllShippedCatalogEntries())
        {
            Assert.NotNull(entry.Notes);
        }
    }

    [Fact]
    public void AllShippedEntries_CarryANonBlankDisplayLabel()
    {
        foreach (var entry in AllShippedCatalogEntries())
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.DisplayLabel), $"'{entry.Id}' has a blank display label.");
        }
    }

    // ================= R-X.7: the pair-form rule =================

    [Fact]
    public void PairFormLabels_NeverPresentACulturalIdentificationAsABareUnqualifiedName()
    {
        // R-X.6/R-X.7: a cultural identification appears only in pair form —
        // the Filipino name, an em dash, and a plain English descriptor —
        // never as a bare label. Every em-dash label across the whole
        // milestone must carry non-blank text on both sides of the dash.
        foreach (var entry in AllShippedCatalogEntries())
        {
            var label = entry.DisplayLabel;
            var dashIndex = label.IndexOf('—');
            if (dashIndex < 0)
            {
                continue;
            }

            var name = label[..dashIndex].Trim();
            var descriptor = label[(dashIndex + 1)..].Trim();

            Assert.False(string.IsNullOrWhiteSpace(name), $"'{entry.Id}' pair-form label has no name segment.");
            Assert.False(
                string.IsNullOrWhiteSpace(descriptor),
                $"'{entry.Id}' pair-form label has no descriptor segment.");
        }
    }

    [Fact]
    public void Shield_NoEntryUsesPairFormOrNamesKalasagInAPlayerFacingLabel()
    {
        // OD-1: the shield ships as plain "Tall Hardwood Shield" this pass;
        // the pair form ("Kalasag — Tall Hardwood Shield") is not used, and
        // the kalasag name stays out of every player-facing label.
        foreach (var entry in AllShieldEntries())
        {
            Assert.False(
                entry.DisplayLabel.Contains('—', StringComparison.Ordinal),
                $"'{entry.Id}' shield label uses pair form (OD-1).");
            Assert.False(
                entry.DisplayLabel.Contains("kalasag", StringComparison.OrdinalIgnoreCase),
                $"'{entry.Id}' display label names kalasag (OD-1).");
        }
    }

    [Fact]
    public void AllShippedEntries_NeverNameThePalisayInACatalogLabelOrNote()
    {
        // OD-2: "palisay" may appear only as inspector research-note
        // metadata (AgentInspectorContent.PalisayResearchNote), explicitly
        // flagged attestation-pending, never inside a catalog entry's own
        // DisplayLabel or Notes.
        foreach (var entry in AllShippedCatalogEntries())
        {
            Assert.False(
                entry.DisplayLabel.Contains("palisay", StringComparison.OrdinalIgnoreCase),
                $"'{entry.Id}' display label names palisay (OD-2).");
            Assert.False(
                entry.Notes.Contains("palisay", StringComparison.OrdinalIgnoreCase),
                $"'{entry.Id}' notes name palisay (OD-2).");
        }
    }

    [Fact]
    public void AllShippedEntries_NeverUseTheSalakotTermAnywhere()
    {
        foreach (var entry in AllShippedCatalogEntries())
        {
            Assert.False(
                entry.DisplayLabel.Contains("salakot", StringComparison.OrdinalIgnoreCase),
                $"'{entry.Id}' display label contains the forbidden 'salakot' term.");
            Assert.False(
                entry.Notes.Contains("salakot", StringComparison.OrdinalIgnoreCase),
                $"'{entry.Id}' notes contain the forbidden 'salakot' term.");
        }
    }

    [Fact]
    public void Appearance_NoEntryShipsTheExcludedC2ResearchCode()
    {
        // OD-5: component C2 (earned red putong) ships in no roster.
        Assert.DoesNotContain(
            AllShippedCatalogEntries(),
            entry => entry.Id.EndsWith(".c2", StringComparison.Ordinal));
    }

    // ================= Salt-registry distinctness (extends VIS-001, R-W6.2) =================

    [Fact]
    public void PresentationSalts_AllValuesArePairwiseDistinct()
    {
        var values = PresentationSalts.All.Select(entry => entry.Value).ToArray();
        Assert.Equal(values.Length, values.Distinct().Count());
    }

    [Fact]
    public void PresentationSalts_AllNamesArePairwiseDistinct()
    {
        var names = PresentationSalts.All.Select(entry => entry.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    // Eleven from the visual improvement package, plus the two warrior
    // personal-name streams (region assignment and name selection).
    [Fact]
    public void PresentationSalts_RegistryHasThirteenEntries() =>
        Assert.Equal(13, PresentationSalts.All.Count);

    [Fact]
    public void PresentationSalts_RegistryIncludesEveryNewVisualImprovementPackageSalt()
    {
        string[] expectedNewSaltNames =
        [
            nameof(PresentationSalts.WeaponTintSalt),
            nameof(PresentationSalts.WeaponSilhouetteSalt),
            nameof(PresentationSalts.ShieldSkinSalt),
            nameof(PresentationSalts.AppearanceBlockAssignmentSalt),
            nameof(PresentationSalts.AppearancePresetSelectionSalt),
            nameof(PresentationSalts.GrassGenerationSalt),
            nameof(PresentationSalts.GroundCornerLatticeSalt),
        ];

        var registeredNames = PresentationSalts.All
            .Select(entry => entry.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in expectedNewSaltNames)
        {
            Assert.Contains(name, registeredNames);
        }
    }

    // ================= R-W6.5: the three new LogEvents constants =================

    [Fact]
    public void LogEvents_AssetsVisualCatalogInvalidIsPinned() =>
        Assert.Equal("assets.visual.catalogInvalid", LogEvents.AssetsVisualCatalogInvalid);

    [Fact]
    public void LogEvents_AssetsVisualFallbackIsPinned() =>
        Assert.Equal("assets.visual.fallback", LogEvents.AssetsVisualFallback);

    [Fact]
    public void LogEvents_AssetsVisualVariantMissingIsPinned() =>
        Assert.Equal("assets.visual.variantMissing", LogEvents.AssetsVisualVariantMissing);

    [Theory]
    [InlineData("assets.visual.catalogInvalid")]
    [InlineData("assets.visual.fallback")]
    [InlineData("assets.visual.variantMissing")]
    public void LogEvents_EveryNewVisualDiagnosticConstantIsDeclaredOnTheSharedCatalog(string expectedValue)
    {
        // The general LogEventCatalogTests reflective sweep (uniqueness,
        // lowercase-dotted shape, channel-prefix agreement) already covers
        // every LogEvents constant, including these three, by construction;
        // this pin is the milestone-scope guarantee that the three specific
        // identifiers VisualDiagnostics depends on exist and read exactly as
        // designed (R-W6.5).
        var declared = typeof(LogEvents)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.Contains(expectedValue, declared);
    }

    [Fact]
    public void LogEvents_EveryNewVisualDiagnosticConstantUsesTheAssetsChannelPrefix()
    {
        Assert.StartsWith("assets.", LogEvents.AssetsVisualCatalogInvalid);
        Assert.StartsWith("assets.", LogEvents.AssetsVisualFallback);
        Assert.StartsWith("assets.", LogEvents.AssetsVisualVariantMissing);
    }

    // ================= R-W6.4: fallback totality, per domain's real entry type =================
    //
    // VisualFallbackResolverTests.cs already proves Resolve<T> total against
    // a synthetic fixture type; this section proves the same generic chain
    // against each domain's own real, shipped record type and real,
    // shipped fallback-target instances — the "fallback totality per
    // domain" leg of the milestone contract.

    [Fact]
    public void FallbackResolver_ReachesSpecificVariant_ForTheRealWeaponTintEntryType()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => WeaponVisualCatalog.KalisTintFreshIron,
            () => (WeaponTintEntry?)null,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.SpecificVariant, resolution.Step);
        Assert.Same(WeaponVisualCatalog.KalisTintFreshIron, resolution.Entry);
    }

    [Fact]
    public void FallbackResolver_ReachesTheRealWeaponModelCategoryDefault_WhenNoOtherStepResolves()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (WeaponTintEntry?)null,
            () => (WeaponTintEntry?)null,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.ModelCategoryDefault, resolution.Step);
        Assert.Same(WeaponVisualCatalog.ModelCategoryDefaultTint, resolution.Entry);
    }

    [Fact]
    public void FallbackResolver_ReachesDiagnosticPlaceholder_ForTheRealWeaponTintEntryType_WhenNothingResolves()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (WeaponTintEntry?)null,
            () => (WeaponTintEntry?)null,
            () => (WeaponTintEntry?)null,
            _ => true);

        Assert.Equal(VisualFallbackStep.DiagnosticPlaceholder, resolution.Step);
        Assert.Null(resolution.Entry);
    }

    [Fact]
    public void FallbackResolver_ReachesTheRealShieldFamilyDefault_WhenTheSpecificSkinIsMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (ShieldSkinEntry?)null,
            () => ShieldVisualCatalog.Default,
            () => ShieldVisualCatalog.ModelCategoryDefault,
            _ => true);

        Assert.Equal(VisualFallbackStep.FamilyDefault, resolution.Step);
        Assert.Same(ShieldVisualCatalog.Default, resolution.Entry);
    }

    [Fact]
    public void FallbackResolver_ReachesTheRealShieldModelCategoryDefault_WhenNoOtherStepResolves()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (ShieldSkinEntry?)null,
            () => (ShieldSkinEntry?)null,
            () => ShieldVisualCatalog.ModelCategoryDefault,
            _ => true);

        Assert.Equal(VisualFallbackStep.ModelCategoryDefault, resolution.Step);
        Assert.Same(ShieldVisualCatalog.ModelCategoryDefault, resolution.Entry);
    }

    [Fact]
    public void FallbackResolver_ReachesSpecificVariant_ForTheRealAppearanceComponentEntryType()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => AppearanceComponentCatalog.HairB1LongHairKnotted,
            () => (AppearanceComponentEntry?)null,
            () => (AppearanceComponentEntry?)null,
            _ => true);

        Assert.Equal(VisualFallbackStep.SpecificVariant, resolution.Step);
        Assert.Same(AppearanceComponentCatalog.HairB1LongHairKnotted, resolution.Entry);
    }

    [Fact]
    public void FallbackResolver_ReachesDiagnosticPlaceholder_ForTheRealAppearanceComponentEntryType_WhenNothingResolves()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (AppearanceComponentEntry?)null,
            () => (AppearanceComponentEntry?)null,
            () => (AppearanceComponentEntry?)null,
            _ => true);

        Assert.Equal(VisualFallbackStep.DiagnosticPlaceholder, resolution.Step);
        Assert.Null(resolution.Entry);
    }

    [Fact]
    public void FallbackResolver_ReachesTheBackdropCatalogsSoleEntry_AsTheModelCategoryDefault()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (VisualCatalogEntry?)null,
            () => (VisualCatalogEntry?)null,
            () => BackdropVisualCatalog.GrassCluster,
            _ => true);

        Assert.Equal(VisualFallbackStep.ModelCategoryDefault, resolution.Step);
        Assert.Same(BackdropVisualCatalog.GrassCluster, resolution.Entry);
    }

    [Fact]
    public void FallbackResolver_ReachesDiagnosticPlaceholder_ForTheSharedVisualCatalogEntryType_WhenNothingResolves()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (VisualCatalogEntry?)null,
            () => (VisualCatalogEntry?)null,
            () => (VisualCatalogEntry?)null,
            _ => true);

        Assert.Equal(VisualFallbackStep.DiagnosticPlaceholder, resolution.Step);
        Assert.Null(resolution.Entry);
    }
}
