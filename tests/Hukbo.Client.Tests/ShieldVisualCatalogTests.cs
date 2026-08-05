using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins the <c>shield.tallHardwood.*</c> catalog
/// (<see cref="ShieldVisualCatalog"/>, VIS-013 + VIS-014): all four cleared
/// skins (S1 <c>mactanThin</c>, S2 <c>morgaFullBody</c>, S3
/// <c>boxerCagayan</c>, S5 <c>visayanKalasag</c>), the unrolled
/// <c>default</c> fallback target, the deterministic skin-selection stream
/// over the modulus of four, the fallback chain, and the contrast envelope
/// (R-W2.1 through R-W2.8, OD-10).
/// </summary>
public sealed class ShieldVisualCatalogTests
{
    // --- Reference colors mirroring production sources, for the envelope
    // checks below. Kept local rather than importing the production types
    // that own them, matching WeaponVisualCatalogTests' own convention. ---

    // The six shipped themes' ArenaSurface/ArenaBorder pair, lerped to
    // PlainsBackdropGeometry.MaximumBackdropInterpolation (0.22) — the
    // worst-case ground shade VIS-005's own text names. Hex values mirror
    // src/Hukbo.Client/Content/Themes/ui-theme-standards.json (command,
    // field-manual, signal, broadcast, high-contrast, datu-court).
    private static readonly Color GroundShadeCommand = new(40, 54, 73);
    private static readonly Color GroundShadeFieldManual = new(201, 185, 146);
    private static readonly Color GroundShadeSignal = new(26, 42, 46);
    private static readonly Color GroundShadeBroadcast = new(212, 217, 222);
    private static readonly Color GroundShadeHighContrast = new(56, 56, 56);
    private static readonly Color GroundShadeDatuCourt = new(101, 99, 63);

    private static readonly Color[] AllGroundShades =
    [
        GroundShadeCommand,
        GroundShadeFieldManual,
        GroundShadeSignal,
        GroundShadeBroadcast,
        GroundShadeHighContrast,
        GroundShadeDatuCourt,
    ];

    // PawnAppearanceFactory's private clothing-color set, mirrored here the
    // same way WeaponVisualCatalogTests mirrors it.
    private static readonly Color ClothingCream = new(231, 216, 183);
    private static readonly Color ClothingIndigo = new(53, 77, 107);
    private static readonly Color ClothingTextileRed = new(143, 63, 53);
    private static readonly Color ClothingPatinaGreen = new(81, 112, 100);

    private static readonly Color[] AllClothingColors =
    [
        ClothingCream,
        ClothingIndigo,
        ClothingTextileRed,
        ClothingPatinaGreen,
    ];

    // --- Every declared shield entry, for the structural sweeps below ---

    private static ShieldSkinEntry[] AllEntries() =>
    [
        ShieldVisualCatalog.MactanThin,
        ShieldVisualCatalog.MorgaFullBody,
        ShieldVisualCatalog.BoxerCagayan,
        ShieldVisualCatalog.VisayanKalasag,
        ShieldVisualCatalog.Default,
        ShieldVisualCatalog.ModelCategoryDefault,
    ];

    // --- Structure: identifiers, indices, shield tagging ---

    [Fact]
    public void TallHardwoodSkins_HasExactlyFourEntries()
    {
        // R-W2.1 pin (acceptance criteria): the shield design's evidence
        // clears exactly S1/S2/S3/S5 — four skins, no fifth.
        Assert.Equal(4, ShieldVisualCatalog.TallHardwoodSkins.Count);
    }

    [Fact]
    public void AllShieldEntries_HaveWellFormedDistinctIds()
    {
        var ids = AllEntries().Select(entry => entry.Catalog.Id).ToArray();

        foreach (var id in ids)
        {
            Assert.True(
                VisualCatalogGrammar.IsWellFormedId(id),
                $"'{id}' is not a well-formed catalog identifier.");
        }

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void AllShieldEntries_UseTheShieldDomain()
    {
        foreach (var entry in AllEntries())
        {
            Assert.StartsWith("shield.", entry.Catalog.Id);
        }
    }

    [Fact]
    public void EveryShieldEntry_IsTaggedWithTheTallHardwoodShieldRole()
    {
        foreach (var entry in AllEntries())
        {
            Assert.Equal(PawnShieldRole.TallHardwood, entry.Shield);
        }
    }

    [Fact]
    public void TallHardwoodSkins_CarryTheirDesignTableIndicesInOrder()
    {
        var skins = ShieldVisualCatalog.TallHardwoodSkins;

        Assert.Equal(0, skins[0].Catalog.Index);
        Assert.Equal(ShieldVisualCatalog.MactanThin, skins[0]);
        Assert.Equal(1, skins[1].Catalog.Index);
        Assert.Equal(ShieldVisualCatalog.MorgaFullBody, skins[1]);
        Assert.Equal(2, skins[2].Catalog.Index);
        Assert.Equal(ShieldVisualCatalog.BoxerCagayan, skins[2]);
        Assert.Equal(3, skins[3].Catalog.Index);
        Assert.Equal(ShieldVisualCatalog.VisayanKalasag, skins[3]);
    }

    // --- R-W2.7: every entry carries a tier and a non-empty note ---

    [Fact]
    public void EveryShieldEntry_CarriesADefinedEvidenceTierAndANonEmptyNote()
    {
        foreach (var entry in AllEntries())
        {
            Assert.True(Enum.IsDefined(entry.Catalog.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(entry.Catalog.Notes));
        }
    }

    [Fact]
    public void MactanThin_CarriesTheDocumentedFormUncertainEvidenceTier()
    {
        // S1's existence, thinness, and active use are Documented; its exact
        // shape is Documented, form uncertain — the single tier this visual
        // entry (which represents the shape) carries.
        Assert.Equal(
            VisualEvidenceTier.DocumentedFormUncertain,
            ShieldVisualCatalog.MactanThin.Catalog.EvidenceTier);
    }

    [Fact]
    public void MactanThin_NoteDisclosesTheThinWoodVersusHardwoodNamingGap()
    {
        // R-W2.7: the enum name "hardwood" slightly overstates Pigafetta's
        // "thin" wood, and the inspector may honestly say so.
        Assert.Contains("thin", ShieldVisualCatalog.MactanThin.Catalog.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hardwood", ShieldVisualCatalog.MactanThin.Catalog.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MorgaFullBody_CarriesTheDocumentedFormUncertainEvidenceTierAndDoesNotQuoteTopToToe()
    {
        Assert.Equal(
            VisualEvidenceTier.DocumentedFormUncertain,
            ShieldVisualCatalog.MorgaFullBody.Catalog.EvidenceTier);

        // The "top to toe" quotation reached the research through secondary
        // transmission and must be verified against Blair & Robertson before
        // any player-facing quotation (shield-visuals-design.md).
        Assert.DoesNotContain(
            "top to toe",
            ShieldVisualCatalog.MorgaFullBody.Catalog.Notes,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BoxerCagayan_CarriesTheDocumentedFormUncertainEvidenceTier()
    {
        Assert.Equal(
            VisualEvidenceTier.DocumentedFormUncertain,
            ShieldVisualCatalog.BoxerCagayan.Catalog.EvidenceTier);
    }

    [Fact]
    public void BoxerCagayan_FaceColorMatchesTheExistingCharredWoodTone()
    {
        // shield-visuals-design.md: S3 reuses "the existing charred-wood
        // tone", the same tone Default/ModelCategoryDefault already draw —
        // not a freely tunable value.
        Assert.Equal(
            WeaponVisualCatalog.CharredWoodBrown,
            ShieldVisualCatalog.BoxerCagayan.FaceColor);
    }

    [Fact]
    public void VisayanKalasag_CarriesTheDocumentedFormUncertainEvidenceTierAndDisclosesThePendingName()
    {
        Assert.Equal(
            VisualEvidenceTier.DocumentedFormUncertain,
            ShieldVisualCatalog.VisayanKalasag.Catalog.EvidenceTier);

        // R-W2.7 / OD-1: the note may disclose the pending attestation, as
        // long as the player-facing label itself never carries the name.
        Assert.Contains(
            "kalasag",
            ShieldVisualCatalog.VisayanKalasag.Catalog.Notes,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "pending",
            ShieldVisualCatalog.VisayanKalasag.Catalog.Notes,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultAndModelCategoryDefault_CarryThePresentationOnlyEvidenceTier()
    {
        Assert.Equal(VisualEvidenceTier.PresentationOnly, ShieldVisualCatalog.Default.Catalog.EvidenceTier);
        Assert.Equal(VisualEvidenceTier.PresentationOnly, ShieldVisualCatalog.ModelCategoryDefault.Catalog.EvidenceTier);
    }

    // --- OD-1 / R-W2.6 / R-X.6: the plain descriptor ships, never the
    // unverified pair-form kalasag label ---

    [Fact]
    public void EveryShieldEntry_UsesThePlainDescriptorLabel()
    {
        const string expectedLabel = "Tall Hardwood Shield";

        foreach (var entry in AllEntries())
        {
            Assert.Equal(expectedLabel, entry.Catalog.DisplayLabel);
        }
    }

    [Fact]
    public void NoShieldEntryLabel_ContainsTheUnverifiedKalasagName()
    {
        // OD-1, resolved 2026-07-28: the plain descriptor ships this pass;
        // the pair-form "Kalasag — Tall Hardwood Shield" waits on attestation
        // verification that remains unscheduled. This is the assertion that
        // catches a label regressing to the unverified pair form early —
        // including VisayanKalasag itself, whose *note* may disclose the
        // pending name but whose *label* never may.
        foreach (var entry in AllEntries())
        {
            Assert.DoesNotContain(
                "kalasag",
                entry.Catalog.DisplayLabel,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    // --- R-W2.3: the skin-selection stream is pure, stable, and total ---

    [Fact]
    public void SelectSkin_IsStableAcrossRepeatedCallsForTheSameEntityId()
    {
        var first = ShieldVisualCatalog.SelectSkin(23, PawnShieldRole.TallHardwood);
        var second = ShieldVisualCatalog.SelectSkin(23, PawnShieldRole.TallHardwood);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SelectSkin_ForTallHardwoodAlwaysReturnsOneOfTheFourCatalogedSkins()
    {
        // The stream's modulus grew to 4 in VIS-014 (OD-10) — a real stream,
        // no longer degenerate, exactly like WeaponVisualCatalog's
        // silhouette/tint streams once their own families grew past one
        // entry.
        ShieldSkinEntry[] allowed =
        [
            ShieldVisualCatalog.MactanThin,
            ShieldVisualCatalog.MorgaFullBody,
            ShieldVisualCatalog.BoxerCagayan,
            ShieldVisualCatalog.VisayanKalasag,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Contains(
                ShieldVisualCatalog.SelectSkin(entityId, PawnShieldRole.TallHardwood),
                allowed);
        }
    }

    [Fact]
    public void SelectSkin_ReachesMoreThanOneSkinAcrossTheSampledEntityIdRange()
    {
        // A regression guard against the modulus silently collapsing back to
        // 1: over 200 entity IDs the stream must actually vary.
        var distinctSkinIds = Enumerable.Range(0, 200)
            .Select(entityId => ShieldVisualCatalog
                .SelectSkin((ulong)entityId, PawnShieldRole.TallHardwood)
                .Catalog.Id)
            .Distinct()
            .Count();

        Assert.True(
            distinctSkinIds > 1,
            "Expected the shield skin stream to reach more than one skin " +
            "over 200 sampled entity IDs.");
    }

    [Fact]
    public void SelectSkin_NeverResolvesToTheUnrolledDefault()
    {
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.NotEqual(
                ShieldVisualCatalog.Default.Catalog.Id,
                ShieldVisualCatalog.SelectSkin(entityId, PawnShieldRole.TallHardwood).Catalog.Id);
        }
    }

    [Fact]
    public void SelectSkin_FallsThroughToTheModelCategoryDefaultForNoneShieldRole()
    {
        // PawnShieldRole.None never legitimately reaches this call in
        // production (PawnRenderer.DrawShield never draws it), but the
        // resolution must stay total rather than crash — fallback chain
        // step 3.
        Assert.Equal(
            ShieldVisualCatalog.ModelCategoryDefault,
            ShieldVisualCatalog.SelectSkin(11, PawnShieldRole.None));
    }

    [Fact]
    public void GetSkins_ForTallHardwoodReturnsAllFourSkinsInIndexOrder()
    {
        var skins = ShieldVisualCatalog.GetSkins(PawnShieldRole.TallHardwood);

        Assert.Equal(4, skins.Count);
        Assert.Equal(ShieldVisualCatalog.MactanThin, skins[0]);
        Assert.Equal(ShieldVisualCatalog.MorgaFullBody, skins[1]);
        Assert.Equal(ShieldVisualCatalog.BoxerCagayan, skins[2]);
        Assert.Equal(ShieldVisualCatalog.VisayanKalasag, skins[3]);
    }

    [Fact]
    public void GetSkins_IsEmptyForNoneShieldRole()
    {
        Assert.Empty(ShieldVisualCatalog.GetSkins(PawnShieldRole.None));
    }

    // --- R-W2.2 / R-X.12: the per-skin proportion deltas OD-10 authorizes
    // live in PawnGeometry, keyed off the catalog's own stable Id strings —
    // never on ShieldSkinEntry itself, which still carries face tone only.
    // Reflected here so a future edit that "simplifies" the delta lookup by
    // adding a width/height/offset field directly to ShieldSkinEntry — the
    // exact false-cause hazard R-X.12 and the design's OD-10 amendment guard
    // against — fails this test instead of shipping silently ---

    [Fact]
    public void ShieldSkinEntry_CarriesNoGeometryField()
    {
        // The type itself is the guard: face tone only.
        var properties = typeof(ShieldSkinEntry)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet();

        Assert.Equal(
            new HashSet<string> { "Catalog", "Shield", "FaceColor" },
            properties);
    }

    // --- Fallback totality (VIS-003/VIS-010 pattern): every step reachable,
    // steps 2 and 3 distinct even though they coincide in effect ---

    [Fact]
    public void FallbackChain_Step1SpecificVariantResolvesToTheSelectedSkin()
    {
        var selected = ShieldVisualCatalog.SelectSkin(5, PawnShieldRole.TallHardwood);

        var resolution = VisualFallbackResolver.Resolve(
            () => selected,
            () => ShieldVisualCatalog.Default,
            () => ShieldVisualCatalog.ModelCategoryDefault,
            _ => true);

        Assert.Equal(VisualFallbackStep.SpecificVariant, resolution.Step);
        Assert.Equal(selected, resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step2FamilyDefaultResolvesWhenTheSpecificVariantIsMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (ShieldSkinEntry?)null,
            () => ShieldVisualCatalog.Default,
            () => ShieldVisualCatalog.ModelCategoryDefault,
            _ => true);

        Assert.Equal(VisualFallbackStep.FamilyDefault, resolution.Step);
        Assert.Equal(ShieldVisualCatalog.Default, resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step3ModelCategoryDefaultResolvesWhenTheFirstTwoStepsAreMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (ShieldSkinEntry?)null,
            () => (ShieldSkinEntry?)null,
            () => ShieldVisualCatalog.ModelCategoryDefault,
            _ => true);

        Assert.Equal(VisualFallbackStep.ModelCategoryDefault, resolution.Step);
        Assert.Equal(ShieldVisualCatalog.ModelCategoryDefault, resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step4DiagnosticPlaceholderResolvesWhenEveryStepIsMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (ShieldSkinEntry?)null,
            () => (ShieldSkinEntry?)null,
            () => (ShieldSkinEntry?)null,
            _ => true);

        Assert.Equal(VisualFallbackStep.DiagnosticPlaceholder, resolution.Step);
        Assert.Null(resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step2And3AreDistinctEntriesEvenThoughTheyCoincideInEffect()
    {
        // shield-visuals-design.md: "for this single-shield roster this
        // coincides with step 2 in effect but remains a distinct, testable
        // chain step so the chain shape matches the weapon and component
        // chains." Same face tone, different catalog identifiers.
        Assert.NotEqual(
            ShieldVisualCatalog.Default.Catalog.Id,
            ShieldVisualCatalog.ModelCategoryDefault.Catalog.Id);
        Assert.Equal(
            ShieldVisualCatalog.Default.FaceColor,
            ShieldVisualCatalog.ModelCategoryDefault.FaceColor);
    }

    // --- R-W2.8 / VIS-005: contrast envelope (PalmWoodPale — this task's
    // freely chosen S1 tone, OD-W2-a). Clears clothing and every ground
    // shade except the Field Manual theme, whose own parchment-tan palette
    // is structurally close to any pale wood tone — the same honest-
    // recording situation WeaponVisualCatalog.CharredWoodBrown and
    // DyePalette's GoldAccent/TurmericYellow already record; VIS-033 owns
    // reconciling it, not this task. ---

    [Theory]
    [MemberData(nameof(AllClothingColorData))]
    public void PalmWoodPale_ClearsTheClothingEnvelopeAgainstEveryClothingColor(Color clothingColor)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.PalmWoodPale,
            [clothingColor],
            ContrastEnvelope.MinimumClothingDistance));
    }

    [Fact]
    public void PalmWoodPale_ClearsTheGroundEnvelopeAgainstFiveOfTheSixThemes()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.PalmWoodPale,
            [
                GroundShadeCommand,
                GroundShadeSignal,
                GroundShadeBroadcast,
                GroundShadeHighContrast,
                GroundShadeDatuCourt,
            ],
            ContrastEnvelope.MinimumGroundDistance));
    }

    [Fact]
    public void PalmWoodPale_DoesNotClearTheGroundEnvelopeAgainstTheFieldManualTheme()
    {
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.PalmWoodPale,
            [GroundShadeFieldManual],
            ContrastEnvelope.MinimumGroundDistance));
    }

    // --- R-W2.8 / VIS-005: contrast envelope (LightHardwoodTan — S2's
    // freely chosen tone, OD-W2-a companion pick, tuned to clear both
    // bounds cleanly against every reference color) ---

    [Theory]
    [MemberData(nameof(AllGroundShadeData))]
    public void LightHardwoodTan_ClearsTheGroundEnvelopeAgainstEveryTheme(Color groundShade)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.LightHardwoodTan,
            [groundShade],
            ContrastEnvelope.MinimumGroundDistance));
    }

    [Theory]
    [MemberData(nameof(AllClothingColorData))]
    public void LightHardwoodTan_ClearsTheClothingEnvelopeAgainstEveryClothingColor(Color clothingColor)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.LightHardwoodTan,
            [clothingColor],
            ContrastEnvelope.MinimumClothingDistance));
    }

    // --- R-W2.8 / VIS-005: contrast envelope (ResinBrownTone — S5's freely
    // chosen tone, tuned to clear both bounds cleanly against every
    // reference color) ---

    [Theory]
    [MemberData(nameof(AllGroundShadeData))]
    public void ResinBrownTone_ClearsTheGroundEnvelopeAgainstEveryTheme(Color groundShade)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.ResinBrownTone,
            [groundShade],
            ContrastEnvelope.MinimumGroundDistance));
    }

    [Theory]
    [MemberData(nameof(AllClothingColorData))]
    public void ResinBrownTone_ClearsTheClothingEnvelopeAgainstEveryClothingColor(Color clothingColor)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.ResinBrownTone,
            [clothingColor],
            ContrastEnvelope.MinimumClothingDistance));
    }

    // --- R-W2.8 / VIS-005: contrast envelope (BoxerCagayan's reused
    // CharredWoodBrown — the existing, already-shipped charred-wood tone,
    // not freely tunable here). Clothing clears cleanly; ground has the same
    // real structural shortfall against the three darkest theme grounds that
    // WeaponVisualCatalogTests already records for this shared tone. These
    // tests record the true relationship rather than asserting past it;
    // VIS-033 owns reconciling it. ---

    [Theory]
    [MemberData(nameof(AllClothingColorData))]
    public void BoxerCagayanFaceColor_ClearsTheClothingEnvelopeAgainstEveryClothingColor(Color clothingColor)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.BoxerCagayan.FaceColor,
            [clothingColor],
            ContrastEnvelope.MinimumClothingDistance));
    }

    [Fact]
    public void BoxerCagayanFaceColor_ClearsTheGroundEnvelopeAgainstItsSafeThemes()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.BoxerCagayan.FaceColor,
            [
                GroundShadeFieldManual,
                GroundShadeBroadcast,
                GroundShadeDatuCourt,
            ],
            ContrastEnvelope.MinimumGroundDistance));
    }

    [Fact]
    public void BoxerCagayanFaceColor_DoesNotClearTheGroundEnvelopeAgainstTheThreeDarkestThemes()
    {
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            ShieldVisualCatalog.BoxerCagayan.FaceColor,
            [GroundShadeCommand, GroundShadeSignal, GroundShadeHighContrast],
            ContrastEnvelope.MinimumGroundDistance));
    }

    public static TheoryData<Color> AllClothingColorData()
    {
        var data = new TheoryData<Color>();
        foreach (var color in AllClothingColors)
        {
            data.Add(color);
        }

        return data;
    }

    public static TheoryData<Color> AllGroundShadeData()
    {
        var data = new TheoryData<Color>();
        foreach (var color in AllGroundShades)
        {
            data.Add(color);
        }

        return data;
    }
}
