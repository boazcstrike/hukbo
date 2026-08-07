using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins the category-J dye palette (<see cref="DyePalette"/>, VIS-017) and
/// the full category A-through-K appearance component catalog
/// (<see cref="AppearanceComponentCatalog"/>). The catalog started at
/// fourteen VIS-017 milestone entries — hair B1/B3, head covering C4/C5,
/// torso D1, lower garment E1, sash/belt G2/G3, the side-blade accent H1,
/// and condition K1-K5 — and VIS-019 completed it to thirty-five entries
/// across every research category except A (stature/build, no catalog
/// entries by design) and J (the dye palette tested above)
/// (implementation-plan-draft.md, R-W3.1, R-W3.8, R-W3.11).
/// </summary>
public sealed class AppearanceComponentCatalogTests
{
    // Faction pawn colors are fixed constants (blue, red, gold) per
    // visual-system-integration-design.md section 3 and
    // FactionColorPalette; mirrors ContrastEnvelopeTests' own reference set.
    private static readonly Color FactionBlue = new(64, 164, 255);
    private static readonly Color FactionRed = new(255, 91, 105);
    private static readonly Color FactionGold = new(231, 199, 84);
    private static readonly Color[] AllFactionConstants = [FactionBlue, FactionRed, FactionGold];

    // --- DyePalette: palette pins at the exact research hex values ---

    [Theory]
    [InlineData(231, 216, 183)] // UndyedCream, #E7D8B7
    [InlineData(53, 77, 107)]   // IndigoBlue, #354D6B
    [InlineData(42, 49, 64)]    // DeepBlueBlack, #2A3140
    [InlineData(143, 63, 53)]   // SappanRed, #8F3F35
    [InlineData(201, 162, 63)]  // TurmericYellow, #C9A23F
    [InlineData(122, 90, 58)]   // BarkBrown, #7A5A3A
    [InlineData(208, 166, 74)]  // GoldAccent, #D0A64A
    [InlineData(56, 66, 73)]    // IronBlueBlack, #384249
    public void DyePalette_AllContainsAConstantAtTheExactResearchValue(byte r, byte g, byte b)
    {
        var expected = new Color(r, g, b);

        Assert.Contains(expected, DyePalette.All);
    }

    [Fact]
    public void DyePalette_UndyedCreamPinsTheExactResearchHex()
    {
        Assert.Equal(new Color(231, 216, 183), DyePalette.UndyedCream);
    }

    [Fact]
    public void DyePalette_IndigoBluePinsTheExactResearchHex()
    {
        Assert.Equal(new Color(53, 77, 107), DyePalette.IndigoBlue);
    }

    [Fact]
    public void DyePalette_DeepBlueBlackPinsTheExactResearchHex()
    {
        Assert.Equal(new Color(42, 49, 64), DyePalette.DeepBlueBlack);
    }

    [Fact]
    public void DyePalette_SappanRedPinsTheExactResearchHex()
    {
        Assert.Equal(new Color(143, 63, 53), DyePalette.SappanRed);
    }

    [Fact]
    public void DyePalette_TurmericYellowPinsTheExactResearchHex()
    {
        Assert.Equal(new Color(201, 162, 63), DyePalette.TurmericYellow);
    }

    [Fact]
    public void DyePalette_BarkBrownPinsTheExactResearchHex()
    {
        Assert.Equal(new Color(122, 90, 58), DyePalette.BarkBrown);
    }

    [Fact]
    public void DyePalette_GoldAccentPinsTheExactResearchHex()
    {
        Assert.Equal(new Color(208, 166, 74), DyePalette.GoldAccent);
    }

    [Fact]
    public void DyePalette_IronBlueBlackPinsTheExactResearchHex()
    {
        Assert.Equal(new Color(56, 66, 73), DyePalette.IronBlueBlack);
    }

    [Fact]
    public void DyePalette_AllHasExactlyEightEntries()
    {
        Assert.Equal(8, DyePalette.All.Count);
    }

    [Fact]
    public void DyePalette_EveryConstantIsDistinct()
    {
        Assert.Equal(DyePalette.All.Count, DyePalette.All.Distinct().Count());
    }

    // --- DyePalette: dye-to-faction color distance (VIS-005 envelope, R-W3.8, R-W6.10) ---

    [Theory]
    [MemberData(nameof(NonGoldDyeConstants))]
    public void DyePalette_NonGoldConstantsClearTheFactionDyeEnvelopeAgainstEveryFactionConstant(
        Color dye)
    {
        var result = ContrastEnvelope.IsWithinEnvelope(
            dye,
            AllFactionConstants,
            ContrastEnvelope.MinimumFactionDyeDistance);

        Assert.True(result);
    }

    public static TheoryData<Color> NonGoldDyeConstants()
    {
        var data = new TheoryData<Color>();
        data.Add(DyePalette.UndyedCream);
        data.Add(DyePalette.IndigoBlue);
        data.Add(DyePalette.DeepBlueBlack);
        data.Add(DyePalette.SappanRed);
        data.Add(DyePalette.BarkBrown);
        data.Add(DyePalette.IronBlueBlack);
        return data;
    }

    // GoldAccent and TurmericYellow are both historically pinned gold/yellow
    // tones (R-W3.8), and FactionColorPalette's third ("other faction") pawn
    // color is itself gold (231, 199, 84) — the collision is structural, not
    // a bug in either value. This task pins the exact research hex per
    // R-W3.8; the traceability table assigns the faction-distance
    // enforcement decision to VIS-033 (implementation-plan-draft.md). These
    // tests record the real relationship rather than asserting past it: both
    // constants clear the envelope against the two battle-faction colors,
    // and neither clears it against the third.
    [Fact]
    public void DyePalette_GoldAccentClearsTheEnvelopeAgainstTheTwoBattleFactionConstants()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            DyePalette.GoldAccent, [FactionBlue, FactionRed], ContrastEnvelope.MinimumFactionDyeDistance));
    }

    [Fact]
    public void DyePalette_GoldAccentDoesNotClearTheEnvelopeAgainstTheThirdFactionConstant()
    {
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            DyePalette.GoldAccent, [FactionGold], ContrastEnvelope.MinimumFactionDyeDistance));
    }

    [Fact]
    public void DyePalette_TurmericYellowClearsTheEnvelopeAgainstTheTwoBattleFactionConstants()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            DyePalette.TurmericYellow, [FactionBlue, FactionRed], ContrastEnvelope.MinimumFactionDyeDistance));
    }

    [Fact]
    public void DyePalette_TurmericYellowDoesNotClearTheEnvelopeAgainstTheThirdFactionConstant()
    {
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            DyePalette.TurmericYellow, [FactionGold], ContrastEnvelope.MinimumFactionDyeDistance));
    }

    // --- AppearanceComponentCatalog: structure ---

    [Fact]
    public void AppearanceComponentCatalog_AllHasExactlyThirtyFiveEntries()
    {
        Assert.Equal(35, AppearanceComponentCatalog.All.Count);
    }

    [Fact]
    public void AppearanceComponentCatalog_EveryEntryHasAUniqueId()
    {
        var ids = AppearanceComponentCatalog.All.Select(entry => entry.Catalog.Id);

        Assert.Equal(AppearanceComponentCatalog.All.Count, ids.Distinct().Count());
    }

    [Fact]
    public void AppearanceComponentCatalog_EveryEntryIdIsWellFormed()
    {
        foreach (var entry in AppearanceComponentCatalog.All)
        {
            Assert.True(
                VisualCatalogGrammar.IsWellFormedId(entry.Catalog.Id),
                $"'{entry.Catalog.Id}' is not a well-formed catalog identifier.");
        }
    }

    [Fact]
    public void AppearanceComponentCatalog_EveryEntryIdUsesTheAppearanceDomain()
    {
        foreach (var entry in AppearanceComponentCatalog.All)
        {
            Assert.StartsWith("appearance.", entry.Catalog.Id);
        }
    }

    // --- AppearanceComponentCatalog: every option carries tier and channel (R-W3.1, R-W3.6) ---

    [Theory]
    [MemberData(nameof(AllEntryIndices))]
    public void AppearanceComponentCatalog_EveryEntryCarriesADefinedMinimumDetailTier(int index)
    {
        var entry = AppearanceComponentCatalog.All[index];
        Assert.True(Enum.IsDefined(entry.Catalog.MinimumDetailTier));
    }

    [Theory]
    [MemberData(nameof(AllEntryIndices))]
    public void AppearanceComponentCatalog_EveryEntryCarriesADefinedRenderChannel(int index)
    {
        var entry = AppearanceComponentCatalog.All[index];
        Assert.True(Enum.IsDefined(entry.RenderChannel));
    }

    [Theory]
    [MemberData(nameof(AllEntryIndices))]
    public void AppearanceComponentCatalog_EveryEntryCarriesADefinedCategory(int index)
    {
        var entry = AppearanceComponentCatalog.All[index];
        Assert.True(Enum.IsDefined(entry.Category));
    }

    public static TheoryData<int> AllEntryIndices()
    {
        var data = new TheoryData<int>();
        for (var index = 0; index < AppearanceComponentCatalog.All.Count; index++)
        {
            data.Add(index);
        }

        return data;
    }

    [Fact]
    public void AppearanceComponentCatalog_HairEntriesCarryTheHairCategory()
    {
        Assert.Equal(AppearanceComponentCategory.Hair, AppearanceComponentCatalog.HairB1LongHairKnotted.Category);
        Assert.Equal(AppearanceComponentCategory.Hair, AppearanceComponentCatalog.HairB3Cropped.Category);
    }

    [Fact]
    public void AppearanceComponentCatalog_HeadCoveringEntriesCarryTheHeadCoveringCategory()
    {
        Assert.Equal(
            AppearanceComponentCategory.HeadCovering,
            AppearanceComponentCatalog.HeadCoveringC4WovenSunHat.Category);
        Assert.Equal(
            AppearanceComponentCategory.HeadCovering,
            AppearanceComponentCatalog.HeadCoveringC5BareHead.Category);
    }

    [Fact]
    public void AppearanceComponentCatalog_SashBeltEntriesShareTheSashBeltCategoryAndSilhouetteChannel()
    {
        Assert.Equal(AppearanceComponentCategory.SashBelt, AppearanceComponentCatalog.SashBeltG2ClothBelt.Category);
        Assert.Equal(AppearanceComponentCategory.SashBelt, AppearanceComponentCatalog.SashBeltG3CordBelt.Category);
        Assert.Equal(AppearanceRenderChannel.Silhouette, AppearanceComponentCatalog.SashBeltG2ClothBelt.RenderChannel);
        Assert.Equal(AppearanceRenderChannel.Silhouette, AppearanceComponentCatalog.SashBeltG3CordBelt.RenderChannel);
    }

    [Fact]
    public void AppearanceComponentCatalog_TorsoD1RendersAsAColorBlockAtLowTier()
    {
        // D1 has no distinct silhouette; it is the base skin-tone torso
        // fill, so it draws starting at Low tier
        // (warrior-appearance-design.md zoom table).
        Assert.Equal(AppearanceRenderChannel.ColorBlock, AppearanceComponentCatalog.TorsoD1BareChested.RenderChannel);
        Assert.Equal(VisualDetailTier.Low, AppearanceComponentCatalog.TorsoD1BareChested.Catalog.MinimumDetailTier);
    }

    [Fact]
    public void AppearanceComponentCatalog_AccessoryH1RendersAsSilhouette()
    {
        // warrior-appearance-design.md's Silhouette channel list explicitly
        // names "waist side-blade accent (H1)".
        Assert.Equal(
            AppearanceRenderChannel.Silhouette,
            AppearanceComponentCatalog.AccessoryH1SheathedSideBlade.RenderChannel);
    }

    [Fact]
    public void AppearanceComponentCatalog_IndicesFollowResearchNumberMinusOneWithGapsForUnshippedOptions()
    {
        Assert.Equal(0, AppearanceComponentCatalog.HairB1LongHairKnotted.Catalog.Index); // B1
        Assert.Equal(1, AppearanceComponentCatalog.HairB2LongLooseHair.Catalog.Index);   // B2
        Assert.Equal(2, AppearanceComponentCatalog.HairB3Cropped.Catalog.Index);         // B3
        Assert.Equal(3, AppearanceComponentCatalog.HairB4TuckedUnderWrap.Catalog.Index); // B4
        Assert.Equal(0, AppearanceComponentCatalog.HeadCoveringC1PutongPlain.Catalog.Index);      // C1
        Assert.Equal(2, AppearanceComponentCatalog.HeadCoveringC3PutongGoldEdged.Catalog.Index);  // C3 (C2 gap at 1)
        Assert.Equal(3, AppearanceComponentCatalog.HeadCoveringC4WovenSunHat.Catalog.Index); // C4
        Assert.Equal(4, AppearanceComponentCatalog.HeadCoveringC5BareHead.Catalog.Index);    // C5
        Assert.Equal(5, AppearanceComponentCatalog.HeadCoveringC6FeatheredHeaddress.Catalog.Index); // C6
        Assert.Equal(0, AppearanceComponentCatalog.TorsoD1BareChested.Catalog.Index);        // D1
        Assert.Equal(1, AppearanceComponentCatalog.TorsoD2ChininaIndigoOrBlack.Catalog.Index); // D2
        Assert.Equal(2, AppearanceComponentCatalog.TorsoD3ChininaRedChiefly.Catalog.Index);    // D3
        Assert.Equal(3, AppearanceComponentCatalog.TorsoD4AbacaJacket.Catalog.Index);          // D4
        Assert.Equal(0, AppearanceComponentCatalog.LowerGarmentE1Bahag.Catalog.Index);       // E1
        Assert.Equal(1, AppearanceComponentCatalog.LowerGarmentE2DyedGoldEdged.Catalog.Index); // E2
        Assert.Equal(2, AppearanceComponentCatalog.LowerGarmentE3WaistCloth.Catalog.Index);    // E3
        Assert.Equal(0, AppearanceComponentCatalog.ArmorF1Unarmored.Catalog.Index);          // F1
        Assert.Equal(1, AppearanceComponentCatalog.ArmorF2CordedFiberArmor.Catalog.Index);   // F2
        Assert.Equal(2, AppearanceComponentCatalog.ArmorF3HideCorselet.Catalog.Index);       // F3
        Assert.Equal(3, AppearanceComponentCatalog.ArmorF4WoodenBreastplate.Catalog.Index);  // F4
        Assert.Equal(4, AppearanceComponentCatalog.ArmorF5ShellSetHelmet.Catalog.Index);     // F5
        Assert.Equal(0, AppearanceComponentCatalog.SashBeltG1RedWaistSash.Catalog.Index);    // G1
        Assert.Equal(1, AppearanceComponentCatalog.SashBeltG2ClothBelt.Catalog.Index);       // G2
        Assert.Equal(2, AppearanceComponentCatalog.SashBeltG3CordBelt.Catalog.Index);        // G3
        Assert.Equal(0, AppearanceComponentCatalog.AccessoryH1SheathedSideBlade.Catalog.Index); // H1
        Assert.Equal(1, AppearanceComponentCatalog.AccessoryH2DrapedShoulderCloth.Catalog.Index); // H2
        Assert.Equal(0, AppearanceComponentCatalog.AdornmentI1FullBodyTattoos.Catalog.Index); // I1
        Assert.Equal(1, AppearanceComponentCatalog.AdornmentI2PartialTattoos.Catalog.Index);  // I2
        Assert.Equal(3, AppearanceComponentCatalog.AdornmentI4GoldEarrings.Catalog.Index);    // I4 (I3 gap at 2)
        Assert.Equal(4, AppearanceComponentCatalog.AdornmentI5GoldNecklace.Catalog.Index);    // I5
    }

    // --- Condition options (K1-K5) carry the no-historical-claim marker (R-W3.11) ---

    private static AppearanceComponentEntry[] ConditionEntryValues() =>
    [
        AppearanceComponentCatalog.ConditionK1Clean,
        AppearanceComponentCatalog.ConditionK2DustyMuddy,
        AppearanceComponentCatalog.ConditionK3SweatSheen,
        AppearanceComponentCatalog.ConditionK4FadedDye,
        AppearanceComponentCatalog.ConditionK5BattleWorn,
    ];

    public static TheoryData<int> ConditionEntryIndices()
    {
        var data = new TheoryData<int>();
        for (var index = 0; index < ConditionEntryValues().Length; index++)
        {
            data.Add(index);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ConditionEntryIndices))]
    public void AppearanceComponentCatalog_EveryConditionEntryCarriesThePresentationOnlyEvidenceTier(int index)
    {
        var entry = ConditionEntryValues()[index];
        Assert.Equal(VisualEvidenceTier.PresentationOnly, entry.Catalog.EvidenceTier);
    }

    [Theory]
    [MemberData(nameof(ConditionEntryIndices))]
    public void AppearanceComponentCatalog_EveryConditionEntryCarriesTheNotApplicableScopeTag(int index)
    {
        var entry = ConditionEntryValues()[index];
        Assert.Equal(VisualScopeTag.NotApplicable, entry.Catalog.ScopeTag);
    }

    [Fact]
    public void AppearanceComponentCatalog_ExactlyFiveEntriesAreConditionEntries()
    {
        var conditionCount = AppearanceComponentCatalog.All
            .Count(entry => entry.Category == AppearanceComponentCategory.Condition);

        Assert.Equal(5, conditionCount);
    }

    [Fact]
    public void AppearanceComponentCatalog_NoNonConditionEntryCarriesThePresentationOnlyEvidenceTier()
    {
        foreach (var entry in AppearanceComponentCatalog.All)
        {
            if (entry.Category != AppearanceComponentCategory.Condition)
            {
                Assert.NotEqual(VisualEvidenceTier.PresentationOnly, entry.Catalog.EvidenceTier);
            }
        }
    }

    // --- AppearanceComponentEntry: construction-time validation ---

    private static AppearanceComponentEntry CreateEntry(
        VisualCatalogEntry? catalog = null,
        AppearanceComponentCategory category = AppearanceComponentCategory.Hair,
        AppearanceRenderChannel renderChannel = AppearanceRenderChannel.Silhouette) =>
        new(
            catalog ?? new VisualCatalogEntry(
                "appearance.hair.b1",
                0,
                "Long Hair, Knotted",
                VisualEvidenceTier.Documented,
                VisualScopeTag.UnscopedGeneric,
                "Test fixture entry.",
                VisualDetailTier.Medium),
            category,
            renderChannel);

    [Fact]
    public void AppearanceComponentEntry_ConstructorAcceptsAWellFormedEntry()
    {
        var entry = CreateEntry();

        Assert.Equal(AppearanceComponentCategory.Hair, entry.Category);
        Assert.Equal(AppearanceRenderChannel.Silhouette, entry.RenderChannel);
    }

    [Fact]
    public void AppearanceComponentEntry_ConstructorRejectsANullCatalogEntry()
    {
        Assert.Throws<ArgumentNullException>(() => new AppearanceComponentEntry(
            null!,
            AppearanceComponentCategory.Hair,
            AppearanceRenderChannel.Silhouette));
    }

    [Fact]
    public void AppearanceComponentEntry_ConstructorRejectsAnUndefinedCategory()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateEntry(category: (AppearanceComponentCategory)99));
    }

    [Fact]
    public void AppearanceComponentEntry_ConstructorRejectsAnUndefinedRenderChannel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateEntry(renderChannel: (AppearanceRenderChannel)99));
    }

    // ================= VIS-019: category option counts match the research =================

    [Fact]
    public void AppearanceComponentCatalog_HairCategoryHasAllFourResearchOptions()
    {
        Assert.Equal(
            4,
            AppearanceComponentCatalog.All.Count(entry => entry.Category == AppearanceComponentCategory.Hair));
    }

    [Fact]
    public void AppearanceComponentCatalog_HeadCoveringCategoryHasFiveOfSixResearchOptionsC2Excluded()
    {
        // Six research options (C1-C6); C2 (earned red putong) is excluded
        // per OD-5 (implementation-plan-draft.md), so five ship here.
        Assert.Equal(
            5,
            AppearanceComponentCatalog.All.Count(entry => entry.Category == AppearanceComponentCategory.HeadCovering));
    }

    [Fact]
    public void AppearanceComponentCatalog_TorsoGarmentCategoryHasAllFourResearchOptions()
    {
        Assert.Equal(
            4,
            AppearanceComponentCatalog.All.Count(entry => entry.Category == AppearanceComponentCategory.TorsoGarment));
    }

    [Fact]
    public void AppearanceComponentCatalog_LowerGarmentCategoryHasAllThreeResearchOptions()
    {
        Assert.Equal(
            3,
            AppearanceComponentCatalog.All.Count(entry => entry.Category == AppearanceComponentCategory.LowerGarment));
    }

    [Fact]
    public void AppearanceComponentCatalog_ArmorCategoryHasAllFiveResearchOptions()
    {
        // "Five includable armor options plus a documented exclusion list"
        // (warrior-appearance-historical-research.md, category F).
        Assert.Equal(
            5,
            AppearanceComponentCatalog.All.Count(entry => entry.Category == AppearanceComponentCategory.Armor));
    }

    [Fact]
    public void AppearanceComponentCatalog_SashBeltCategoryHasAllThreeResearchOptions()
    {
        Assert.Equal(
            3,
            AppearanceComponentCatalog.All.Count(entry => entry.Category == AppearanceComponentCategory.SashBelt));
    }

    [Fact]
    public void AppearanceComponentCatalog_AccessoryCategoryHasExactlyTheTwoRenderableResearchOptions()
    {
        // "Four accessory options (two renderable)" (research, category H);
        // H3 and H4 are inspector-text-only / weapons-catalog-owned and have
        // no catalog entry.
        Assert.Equal(
            2,
            AppearanceComponentCatalog.All.Count(entry => entry.Category == AppearanceComponentCategory.Accessory));
    }

    [Fact]
    public void AppearanceComponentCatalog_AdornmentCategoryHasExactlyTheFourRenderableResearchOptions()
    {
        // Planner inconsistency 4's resolution: "eight adornment options...
        // four are renderable at pawn scale (I1, I2, I4, and I5)" (research,
        // category I closing paragraph).
        Assert.Equal(
            4,
            AppearanceComponentCatalog.All.Count(entry => entry.Category == AppearanceComponentCategory.Adornment));
    }

    // ================= VIS-019: tattoo tone-shift channel (R-W3.7) =================

    [Fact]
    public void AppearanceComponentCatalog_TattooEntriesRenderAsColorBlockToneShiftOnlyWithNoMotifChannel()
    {
        Assert.Equal(
            AppearanceRenderChannel.ColorBlock,
            AppearanceComponentCatalog.AdornmentI1FullBodyTattoos.RenderChannel);
        Assert.Equal(
            AppearanceRenderChannel.ColorBlock,
            AppearanceComponentCatalog.AdornmentI2PartialTattoos.RenderChannel);

        // AppearanceRenderChannel has exactly three members (Silhouette,
        // ColorBlock, Accent); there is no fourth "motif" channel for
        // tattooing or anything else to draw through.
        Assert.Equal(3, Enum.GetValues<AppearanceRenderChannel>().Length);
    }

    [Fact]
    public void AppearanceComponentCatalog_TattooEntriesShareTheExactSamePinnedToneShiftDelta()
    {
        // Planner inconsistency 4: I1 and I2 share a single skin-tone-shift
        // channel rather than each carrying its own. There is only one
        // TattooToneShift value in the whole catalog for both to share.
        var delta = AppearanceComponentCatalog.TattooToneShift;

        Assert.Equal(-36, delta.Red);
        Assert.Equal(-30, delta.Green);
        Assert.Equal(-18, delta.Blue);
    }

    [Fact]
    public void AppearanceComponentCatalog_TattooToneShiftIsDarkerAndCoolerNotJustDarker()
    {
        // "Darker and cooler" (research, category J) requires every channel
        // to move down (darker) and Red to move down more than Blue
        // (cooler — the remaining tone reads relatively more blue).
        var delta = AppearanceComponentCatalog.TattooToneShift;

        Assert.True(delta.Red < 0);
        Assert.True(delta.Green < 0);
        Assert.True(delta.Blue < 0);
        Assert.True(Math.Abs(delta.Red) > Math.Abs(delta.Blue));
    }

    [Fact]
    public void AppearanceComponentCatalog_TattooEntriesAreScopedToVisayanOnly()
    {
        Assert.Equal(VisualScopeTag.Visayan, AppearanceComponentCatalog.AdornmentI1FullBodyTattoos.Catalog.ScopeTag);
        Assert.Equal(VisualScopeTag.Visayan, AppearanceComponentCatalog.AdornmentI2PartialTattoos.Catalog.ScopeTag);
    }

    // ================= VIS-019: armor widening bound (R-W3.6) =================

    [Fact]
    public void AppearanceComponentCatalog_MaxArmorWidthFactorIsPinnedToTheDesignCeiling()
    {
        Assert.Equal(1.18f, AppearanceComponentCatalog.MaxArmorWidthFactor);
    }

    // Identifiers, not the internal AppearanceComponentEntry record itself,
    // cross the [Theory]/[MemberData] boundary: AppearanceComponentEntry is
    // internal, and a public test method's parameter (or a public
    // TheoryData<T>'s T) may never be less accessible than the method itself
    // (CS0051/CS0050) while xunit's own analyzer (xUnit1000) requires the
    // test class, and therefore its test methods, to stay public.
    public static TheoryData<string> ArmorEntriesWithAWidthFactor()
    {
        var data = new TheoryData<string>();
        foreach (var entry in AppearanceComponentCatalog.All)
        {
            if (entry.ArmorWidthFactor is not null)
            {
                data.Add(entry.Catalog.Id);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ArmorEntriesWithAWidthFactor))]
    public void AppearanceComponentCatalog_EveryArmorWidthFactorStaysWithinTheDesignEnvelope(
        string entryId)
    {
        var entry = Assert.Single(AppearanceComponentCatalog.All, e => e.Catalog.Id == entryId);

        Assert.True(entry.ArmorWidthFactor >= 1f);
        Assert.True(entry.ArmorWidthFactor <= AppearanceComponentCatalog.MaxArmorWidthFactor);
    }

    [Fact]
    public void AppearanceComponentCatalog_OnlyArmorCategoryEntriesEverCarryAWidthFactor()
    {
        foreach (var entry in AppearanceComponentCatalog.All)
        {
            if (entry.Category != AppearanceComponentCategory.Armor)
            {
                Assert.Null(entry.ArmorWidthFactor);
            }
        }
    }

    [Fact]
    public void AppearanceComponentCatalog_ArmorF4WoodenBreastplateSitsExactlyAtTheDesignCeiling()
    {
        Assert.Equal(
            (float?)AppearanceComponentCatalog.MaxArmorWidthFactor,
            AppearanceComponentCatalog.ArmorF4WoodenBreastplate.ArmorWidthFactor);
    }

    [Fact]
    public void AppearanceComponentCatalog_ArmorF1UnarmoredCarriesTheBaselineWidthFactorOfOne()
    {
        Assert.Equal((float?)1f, AppearanceComponentCatalog.ArmorF1Unarmored.ArmorWidthFactor);
    }

    [Fact]
    public void AppearanceComponentCatalog_ArmorF5ShellSetHelmetDoesNotWidenTheTorso()
    {
        // F5 is a head-disk element, not a torso-capsule widener.
        Assert.Null(AppearanceComponentCatalog.ArmorF5ShellSetHelmet.ArmorWidthFactor);
    }

    [Fact]
    public void AppearanceComponentEntry_ConstructorRejectsAnArmorWidthFactorOnANonArmorCategory()
    {
        Assert.Throws<ArgumentException>(() => new AppearanceComponentEntry(
            new VisualCatalogEntry(
                "appearance.hair.b1",
                0,
                "Long Hair, Knotted",
                VisualEvidenceTier.Documented,
                VisualScopeTag.UnscopedGeneric,
                "Test fixture entry.",
                VisualDetailTier.Medium),
            AppearanceComponentCategory.Hair,
            AppearanceRenderChannel.Silhouette,
            ArmorWidthFactor: 1.1f));
    }

    [Theory]
    [InlineData(0.99f)]
    [InlineData(1.19f)]
    public void AppearanceComponentEntry_ConstructorRejectsAnArmorWidthFactorOutsideTheEnvelope(float badFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AppearanceComponentEntry(
            new VisualCatalogEntry(
                "appearance.armor.f2",
                1,
                "Corded Fiber Armor",
                VisualEvidenceTier.DocumentedFormUncertain,
                VisualScopeTag.Visayan,
                "Test fixture entry.",
                VisualDetailTier.Medium),
            AppearanceComponentCategory.Armor,
            AppearanceRenderChannel.Silhouette,
            ArmorWidthFactor: badFactor));
    }

    [Fact]
    public void AppearanceComponentEntry_ConstructorAcceptsANullArmorWidthFactorOnAnArmorCategoryEntry()
    {
        var entry = new AppearanceComponentEntry(
            new VisualCatalogEntry(
                "appearance.armor.f5",
                4,
                "Shell-Set Helmet",
                VisualEvidenceTier.DocumentedFormUncertain,
                VisualScopeTag.Tagalog,
                "Test fixture entry.",
                VisualDetailTier.Medium),
            AppearanceComponentCategory.Armor,
            AppearanceRenderChannel.Silhouette);

        Assert.Null(entry.ArmorWidthFactor);
    }

    // ================= VIS-019: accent caps (R-W3.6, "Area cap") =================

    [Fact]
    public void AppearanceComponentCatalog_MaxAccentMarksPerPawnIsPinnedToTwo()
    {
        Assert.Equal(2, AppearanceComponentCatalog.MaxAccentMarksPerPawn);
    }

    [Fact]
    public void AppearanceComponentCatalog_MaxAccentPixelSizeAtApparentScale1IsPinnedToTwo()
    {
        Assert.Equal(2, AppearanceComponentCatalog.MaxAccentPixelSizeAtApparentScale1);
    }

    [Fact]
    public void AppearanceComponentCatalog_ExactlyFourEntriesUseTheAccentChannel()
    {
        // C3 (gold-edged putong), E2 (dyed bahag gold pixel), I4 (gold
        // earrings), I5 (gold necklace).
        var accentCount = AppearanceComponentCatalog.All
            .Count(entry => entry.RenderChannel == AppearanceRenderChannel.Accent);

        Assert.Equal(4, accentCount);
    }

    [Theory]
    [MemberData(nameof(AccentEntries))]
    public void AppearanceComponentCatalog_EveryAccentEntryRendersAtHighTierOnly(string entryId)
    {
        var entry = Assert.Single(AppearanceComponentCatalog.All, e => e.Catalog.Id == entryId);

        Assert.Equal(VisualDetailTier.High, entry.Catalog.MinimumDetailTier);
    }

    // See the identifiers-not-the-record note on ArmorEntriesWithAWidthFactor
    // above: AppearanceComponentEntry is internal and cannot cross a public
    // [Theory]/[MemberData] boundary.
    public static TheoryData<string> AccentEntries()
    {
        var data = new TheoryData<string>();
        foreach (var entry in AppearanceComponentCatalog.All)
        {
            if (entry.RenderChannel == AppearanceRenderChannel.Accent)
            {
                data.Add(entry.Catalog.Id);
            }
        }

        return data;
    }

    // ================= VIS-019: label negative tests (R-X.6, salakot exclusion) =================

    [Fact]
    public void AppearanceComponentCatalog_NoEntryUsesTheSalakotTermAnywhere()
    {
        foreach (var entry in AppearanceComponentCatalog.All)
        {
            Assert.False(
                entry.Catalog.DisplayLabel.Contains("salakot", StringComparison.OrdinalIgnoreCase),
                $"'{entry.Catalog.Id}' display label contains the forbidden 'salakot' term.");
            Assert.False(
                entry.Catalog.Notes.Contains("salakot", StringComparison.OrdinalIgnoreCase),
                $"'{entry.Catalog.Id}' notes contain the forbidden 'salakot' term.");
        }
    }

    [Theory]
    [InlineData("Kandit")]
    [InlineData("Barote")]
    [InlineData("Panika")]
    [InlineData("Kamagi")]
    [InlineData("Kolombiga")]
    [InlineData("Pusad")]
    public void AppearanceComponentCatalog_NoDisplayLabelUsesABareUnreviewedFilipinoTerm(string pendingTerm)
    {
        foreach (var entry in AppearanceComponentCatalog.All)
        {
            Assert.False(
                entry.Catalog.DisplayLabel.Contains(pendingTerm, StringComparison.OrdinalIgnoreCase),
                $"'{entry.Catalog.Id}' display label contains the pending term '{pendingTerm}'.");
        }
    }

    [Theory]
    [InlineData("Putong — Head Wrap")]
    [InlineData("Bahag — Loincloth")]
    [InlineData("Chinina — Collarless Jacket")]
    public void AppearanceComponentCatalog_EveryPairFormLabelUsesOnlyOneOfTheThreeClearedBases(
        string clearedBase)
    {
        var pairFormEntries = AppearanceComponentCatalog.All
            .Where(entry => entry.Catalog.DisplayLabel.Contains('—', StringComparison.Ordinal));

        Assert.Contains(
            pairFormEntries,
            entry => entry.Catalog.DisplayLabel.StartsWith(clearedBase, StringComparison.Ordinal));
    }

    [Fact]
    public void AppearanceComponentCatalog_EveryPairFormLabelStartsWithOneOfTheThreeClearedBases()
    {
        string[] clearedBases =
        [
            "Putong — Head Wrap",
            "Bahag — Loincloth",
            "Chinina — Collarless Jacket",
        ];

        foreach (var entry in AppearanceComponentCatalog.All)
        {
            if (entry.Catalog.DisplayLabel.Contains('—', StringComparison.Ordinal))
            {
                Assert.Contains(
                    clearedBases,
                    clearedBase => entry.Catalog.DisplayLabel.StartsWith(clearedBase, StringComparison.Ordinal));
            }
        }
    }

    // ================= VIS-019: full-catalog structural sanity =================

    [Fact]
    public void AppearanceComponentCatalog_NoEntryHasTheC2ResearchCode()
    {
        Assert.DoesNotContain(AppearanceComponentCatalog.All, entry => entry.Catalog.Id.EndsWith(".c2", StringComparison.Ordinal));
    }

    [Fact]
    public void AppearanceComponentCatalog_ArmorAndAdornmentCategoriesAreDefinedEnumMembers()
    {
        Assert.True(Enum.IsDefined(AppearanceComponentCategory.Armor));
        Assert.True(Enum.IsDefined(AppearanceComponentCategory.Adornment));
    }

    [Fact]
    public void AppearanceComponentCatalog_EveryNewCategoryHasAtLeastOneEntry()
    {
        foreach (var category in Enum.GetValues<AppearanceComponentCategory>())
        {
            Assert.Contains(AppearanceComponentCatalog.All, entry => entry.Category == category);
        }
    }
}
