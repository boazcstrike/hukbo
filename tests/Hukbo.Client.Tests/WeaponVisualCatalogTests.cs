using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins the <c>weapon.*</c> catalog (<see cref="WeaponVisualCatalog"/>):
/// Kalis (VIS-010) — the L1 pawn silhouette, the inspector-only L2/L3 later
/// forms, its two presentation-only tints — plus Kampilan, Wasay, and Itak
/// (VIS-011) — their own pawn silhouettes, Kampilan's inspector-only K2,
/// their tint sets (Wasay's <c>lashedWorn</c> carrying the rattan lashing
/// band accent), the deterministic tint-selection stream, the fallback
/// chain, and the contrast envelope, for all four weapons (R-W1.1 through
/// R-W1.9).
/// </summary>
public sealed class WeaponVisualCatalogTests
{
    // --- Reference colors mirroring production sources, for the envelope
    // checks below. Kept local rather than importing the production types
    // that own them, matching AppearanceComponentCatalogTests' own
    // "mirrors ContrastEnvelopeTests' own reference set" convention. ---

    // The six shipped themes' ArenaSurface/ArenaBorder pair, lerped to
    // PlainsBackdropGeometry.MaximumBackdropInterpolation (0.22) — the
    // worst-case ground shade VIS-005's own text names ("minimum distance
    // of any equipment tone from all ground shades at the 0.22 ceiling").
    // Hex values mirror src/Hukbo.Client/Content/Themes/ui-theme-
    // standards.json (command, field-manual, signal, broadcast,
    // high-contrast, datu-court).
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
    // same way AppearanceComponentCatalogTests mirrors FactionColorPalette.
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

    // --- Structure: identifiers, indices, weapon tagging ---

    [Fact]
    public void KalisSilhouettes_HasExactlyThreeEntries()
    {
        Assert.Equal(3, WeaponVisualCatalog.KalisSilhouettes.Count);
    }

    [Fact]
    public void AllKalisEntries_HaveWellFormedDistinctIds()
    {
        string[] ids =
        [
            WeaponVisualCatalog.KalisL1.Catalog.Id,
            WeaponVisualCatalog.KalisL2.Catalog.Id,
            WeaponVisualCatalog.KalisL3.Catalog.Id,
            WeaponVisualCatalog.KalisTintFreshIron.Catalog.Id,
            WeaponVisualCatalog.KalisTintDarkHilt.Catalog.Id,
            WeaponVisualCatalog.ModelCategoryDefaultTint.Catalog.Id,
        ];

        foreach (var id in ids)
        {
            Assert.True(
                VisualCatalogGrammar.IsWellFormedId(id),
                $"'{id}' is not a well-formed catalog identifier.");
        }

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void AllKalisEntries_UseTheWeaponDomain()
    {
        foreach (var entry in WeaponVisualCatalog.KalisSilhouettes)
        {
            Assert.StartsWith("weapon.", entry.Catalog.Id);
        }

        Assert.StartsWith("weapon.", WeaponVisualCatalog.KalisTintFreshIron.Catalog.Id);
        Assert.StartsWith("weapon.", WeaponVisualCatalog.KalisTintDarkHilt.Catalog.Id);
    }

    [Fact]
    public void KalisTintIds_UseTheTintSubSegment()
    {
        Assert.Contains(".tint.", WeaponVisualCatalog.KalisTintFreshIron.Catalog.Id);
        Assert.Contains(".tint.", WeaponVisualCatalog.KalisTintDarkHilt.Catalog.Id);
    }

    [Fact]
    public void KalisSilhouetteIndices_AreZeroOneTwoInDeclaredOrder()
    {
        Assert.Equal(0, WeaponVisualCatalog.KalisL1.Catalog.Index);
        Assert.Equal(1, WeaponVisualCatalog.KalisL2.Catalog.Index);
        Assert.Equal(2, WeaponVisualCatalog.KalisL3.Catalog.Index);
    }

    [Fact]
    public void KalisTintIndices_AreZeroAndOneInDeclaredOrder()
    {
        Assert.Equal(0, WeaponVisualCatalog.KalisTintFreshIron.Catalog.Index);
        Assert.Equal(1, WeaponVisualCatalog.KalisTintDarkHilt.Catalog.Index);
    }

    [Fact]
    public void EveryKalisEntry_IsTaggedWithTheKalisWeaponRole()
    {
        Assert.Equal(PawnWeaponRole.Kalis, WeaponVisualCatalog.KalisL1.Weapon);
        Assert.Equal(PawnWeaponRole.Kalis, WeaponVisualCatalog.KalisL2.Weapon);
        Assert.Equal(PawnWeaponRole.Kalis, WeaponVisualCatalog.KalisL3.Weapon);
        Assert.Equal(PawnWeaponRole.Kalis, WeaponVisualCatalog.KalisTintFreshIron.Weapon);
        Assert.Equal(PawnWeaponRole.Kalis, WeaponVisualCatalog.KalisTintDarkHilt.Weapon);
    }

    // --- R-W1.6: every entry carries a tier and a non-empty note ---

    [Fact]
    public void EveryKalisEntry_CarriesADefinedEvidenceTierAndANonEmptyNote()
    {
        var silhouettes = WeaponVisualCatalog.KalisSilhouettes.Select(entry => entry.Catalog);
        var tints = new[]
        {
            WeaponVisualCatalog.KalisTintFreshIron.Catalog,
            WeaponVisualCatalog.KalisTintDarkHilt.Catalog,
            WeaponVisualCatalog.ModelCategoryDefaultTint.Catalog,
        };

        foreach (var entry in silhouettes.Concat(tints))
        {
            Assert.True(Enum.IsDefined(entry.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(entry.Notes));
        }
    }

    [Fact]
    public void KalisTints_CarryThePresentationOnlyEvidenceTier()
    {
        // Tints assert no historical claim (weapon-visuals-design.md,
        // "Historically meaningful versus presentation-only").
        Assert.Equal(VisualEvidenceTier.PresentationOnly, WeaponVisualCatalog.KalisTintFreshIron.Catalog.EvidenceTier);
        Assert.Equal(VisualEvidenceTier.PresentationOnly, WeaponVisualCatalog.KalisTintDarkHilt.Catalog.EvidenceTier);
    }

    [Fact]
    public void KalisL1_CarriesTheDocumentedEvidenceTier()
    {
        // The strongest attestation of the four weapons: Pigafetta records
        // calis by name at Cebu in 1521.
        Assert.Equal(VisualEvidenceTier.Documented, WeaponVisualCatalog.KalisL1.Catalog.EvidenceTier);
    }

    [Fact]
    public void KalisL2AndL3_CarryTheProvisionalReconstructionEvidenceTier()
    {
        Assert.Equal(VisualEvidenceTier.ProvisionalReconstruction, WeaponVisualCatalog.KalisL2.Catalog.EvidenceTier);
        Assert.Equal(VisualEvidenceTier.ProvisionalReconstruction, WeaponVisualCatalog.KalisL3.Catalog.EvidenceTier);
    }

    // --- R-X.6: every variant keeps the unchanged pair-form weapon label ---

    [Fact]
    public void EveryKalisEntry_UsesTheUnchangedPairFormLabel()
    {
        const string expectedLabel = "Kalis — Thrusting Blade";

        foreach (var entry in WeaponVisualCatalog.KalisSilhouettes)
        {
            Assert.Equal(expectedLabel, entry.Catalog.DisplayLabel);
        }

        Assert.Equal(expectedLabel, WeaponVisualCatalog.KalisTintFreshIron.Catalog.DisplayLabel);
        Assert.Equal(expectedLabel, WeaponVisualCatalog.KalisTintDarkHilt.Catalog.DisplayLabel);
        Assert.Equal(expectedLabel, WeaponVisualCatalog.ModelCategoryDefaultTint.Catalog.DisplayLabel);
    }

    // --- R-W1.4: L2/L3 unreachable by any pawn-scale selection ---

    [Fact]
    public void OnlyKalisL1_IsPawnSelectable()
    {
        Assert.True(WeaponVisualCatalog.KalisL1.PawnSelectable);
        Assert.False(WeaponVisualCatalog.KalisL2.PawnSelectable);
        Assert.False(WeaponVisualCatalog.KalisL3.PawnSelectable);
    }

    [Fact]
    public void PawnSilhouette_AlwaysReturnsL1ForKalis()
    {
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            // The silhouette stream is degenerate this pass (one selectable
            // entry); PawnSilhouette is not a function of entityId at all,
            // but the loop documents that fact rather than assuming it.
            Assert.Equal(
                WeaponVisualCatalog.KalisL1,
                WeaponVisualCatalog.PawnSilhouette(PawnWeaponRole.Kalis));
        }
    }

    [Fact]
    public void PawnSilhouette_ReturnsEveryWeaponsOwnSilhouetteAsOfVIS011()
    {
        Assert.Equal(WeaponVisualCatalog.KampilanK1, WeaponVisualCatalog.PawnSilhouette(PawnWeaponRole.Kampilan));
        Assert.Equal(WeaponVisualCatalog.WasayW1, WeaponVisualCatalog.PawnSilhouette(PawnWeaponRole.Wasay));
        Assert.Equal(WeaponVisualCatalog.ItakI1, WeaponVisualCatalog.PawnSilhouette(PawnWeaponRole.Itak));
    }

    [Fact]
    public void PawnSilhouette_ReturnsEveryRangedWeaponsOwnSilhouetteAsOfRU10()
    {
        Assert.Equal(WeaponVisualCatalog.BangkawB1, WeaponVisualCatalog.PawnSilhouette(PawnWeaponRole.Bangkaw));
        Assert.Equal(WeaponVisualCatalog.BusogB1, WeaponVisualCatalog.PawnSilhouette(PawnWeaponRole.Busog));
        Assert.Equal(WeaponVisualCatalog.ArquebusA1, WeaponVisualCatalog.PawnSilhouette(PawnWeaponRole.Arquebus));
    }

    // --- R-W1.3: the tint-selection stream is pure, stable, and total ---

    [Fact]
    public void SelectTint_IsStableAcrossRepeatedCallsForTheSameEntityId()
    {
        var first = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Kalis);
        var second = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Kalis);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SelectTint_ForKalisAlwaysReturnsOneOfTheTwoCatalogedTints()
    {
        WeaponTintEntry[] allowed =
        [
            WeaponVisualCatalog.KalisTintFreshIron,
            WeaponVisualCatalog.KalisTintDarkHilt,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Contains(WeaponVisualCatalog.SelectTint(entityId, PawnWeaponRole.Kalis), allowed);
        }
    }

    [Theory]
    [InlineData(PawnWeaponRole.Kampilan)]
    [InlineData(PawnWeaponRole.Wasay)]
    [InlineData(PawnWeaponRole.Kalis)]
    [InlineData(PawnWeaponRole.Itak)]
    [InlineData(PawnWeaponRole.Bangkaw)]
    [InlineData(PawnWeaponRole.Busog)]
    [InlineData(PawnWeaponRole.Arquebus)]
    public void SelectTint_NeverFallsThroughToTheModelCategoryDefaultForAnyDefinedWeapon(
        PawnWeaponRole weapon)
    {
        // As of VIS-011/RU-10 every defined PawnWeaponRole ships its own
        // tints, so ModelCategoryDefaultTint (fallback step 3) is
        // unreachable through SelectTint for any of them — it stays alive
        // only as a distinct, testable chain step under the fallback
        // tests' delegate doubles below, exactly like
        // ShieldVisualCatalog.Default.
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.NotEqual(
                WeaponVisualCatalog.ModelCategoryDefaultTint,
                WeaponVisualCatalog.SelectTint(entityId, weapon));
        }
    }

    [Fact]
    public void GetTints_ForKalisReturnsBothTintsInIndexOrder()
    {
        var tints = WeaponVisualCatalog.GetTints(PawnWeaponRole.Kalis);

        Assert.Equal(2, tints.Count);
        Assert.Equal(WeaponVisualCatalog.KalisTintFreshIron, tints[0]);
        Assert.Equal(WeaponVisualCatalog.KalisTintDarkHilt, tints[1]);
    }

    [Fact]
    public void GetTints_IsNonEmptyForEveryDefinedWeaponAsOfVIS011()
    {
        Assert.NotEmpty(WeaponVisualCatalog.GetTints(PawnWeaponRole.Kampilan));
        Assert.NotEmpty(WeaponVisualCatalog.GetTints(PawnWeaponRole.Wasay));
        Assert.NotEmpty(WeaponVisualCatalog.GetTints(PawnWeaponRole.Kalis));
        Assert.NotEmpty(WeaponVisualCatalog.GetTints(PawnWeaponRole.Itak));
    }

    [Fact]
    public void GetTints_IsNonEmptyForEveryRangedWeaponAsOfRU10()
    {
        Assert.NotEmpty(WeaponVisualCatalog.GetTints(PawnWeaponRole.Bangkaw));
        Assert.NotEmpty(WeaponVisualCatalog.GetTints(PawnWeaponRole.Busog));
        Assert.NotEmpty(WeaponVisualCatalog.GetTints(PawnWeaponRole.Arquebus));
    }

    // --- R-W1.8: at most three tints per weapon ---

    [Fact]
    public void KalisTintCount_IsAtMostThree()
    {
        Assert.True(WeaponVisualCatalog.GetTints(PawnWeaponRole.Kalis).Count <= 3);
    }

    // --- R-X.3: blade geometry/reach identity is unaffected by tint ---

    [Fact]
    public void BothKalisTints_ShareTheExactSameBladeColor()
    {
        // Only the grip differs between the two tints — the blade itself
        // never varies, which is exactly what keeps the tint from reading
        // as a mechanical difference (R-X.3) and what "drawn blade geometry
        // identical to today for both tints (only color differs)" pins.
        Assert.Equal(
            WeaponVisualCatalog.KalisTintFreshIron.BladeColor,
            WeaponVisualCatalog.KalisTintDarkHilt.BladeColor);
    }

    [Fact]
    public void BothKalisTints_MatchDyePaletteIronBlueBlackForTheBladeColor()
    {
        // The exact hex the existing, unconditional weapon blade color
        // already draws today (PawnRenderer's private Iron constant,
        // (56, 66, 73)) — the tint introduces no new blade tone at all.
        Assert.Equal(DyePalette.IronBlueBlack, WeaponVisualCatalog.KalisTintFreshIron.BladeColor);
        Assert.Equal(DyePalette.IronBlueBlack, WeaponVisualCatalog.KalisTintDarkHilt.BladeColor);
    }

    [Fact]
    public void TheTwoKalisTints_HaveDistinctGripColors()
    {
        Assert.NotEqual(
            WeaponVisualCatalog.KalisTintFreshIron.GripColor,
            WeaponVisualCatalog.KalisTintDarkHilt.GripColor);
    }

    // --- Fallback totality (VIS-003/VIS-008 pattern): every step reachable ---

    [Fact]
    public void FallbackChain_Step1SpecificVariantResolvesToTheSelectedKalisTint()
    {
        var selected = WeaponVisualCatalog.SelectTint(5, PawnWeaponRole.Kalis);

        var resolution = VisualFallbackResolver.Resolve(
            () => selected,
            () => WeaponVisualCatalog.KalisTintFreshIron,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.SpecificVariant, resolution.Step);
        Assert.Equal(selected, resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step2FamilyDefaultResolvesWhenTheSpecificVariantIsMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (WeaponTintEntry?)null,
            () => WeaponVisualCatalog.KalisTintFreshIron,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.FamilyDefault, resolution.Step);
        Assert.Equal(WeaponVisualCatalog.KalisTintFreshIron, resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step3ModelCategoryDefaultResolvesWhenTheFirstTwoStepsAreMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (WeaponTintEntry?)null,
            () => (WeaponTintEntry?)null,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.ModelCategoryDefault, resolution.Step);
        Assert.Equal(WeaponVisualCatalog.ModelCategoryDefaultTint, resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step4DiagnosticPlaceholderResolvesWhenEveryStepIsMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (WeaponTintEntry?)null,
            () => (WeaponTintEntry?)null,
            () => (WeaponTintEntry?)null,
            _ => true);

        Assert.Equal(VisualFallbackStep.DiagnosticPlaceholder, resolution.Step);
        Assert.Null(resolution.Entry);
    }

    // --- R-W1.7 / VIS-005: contrast envelope (GripWarmOchre — this task's
    // freely chosen tint color, tuned to clear both bounds cleanly) ---

    [Theory]
    [MemberData(nameof(AllGroundShadeData))]
    public void GripWarmOchre_ClearsTheGroundEnvelopeAgainstEveryTheme(Color groundShade)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.GripWarmOchre,
            [groundShade],
            ContrastEnvelope.MinimumGroundDistance));
    }

    [Theory]
    [MemberData(nameof(AllClothingColorData))]
    public void GripWarmOchre_ClearsTheClothingEnvelopeAgainstEveryClothingColor(Color clothingColor)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.GripWarmOchre,
            [clothingColor],
            ContrastEnvelope.MinimumClothingDistance));
    }

    // --- R-W1.7 / VIS-005: contrast envelope (CharredWoodBrown — the
    // existing, already-shipped charred-wood tone, reused verbatim per the
    // weapon design's own instruction, not freely tunable here). Clothing
    // clears cleanly; ground has a real structural shortfall against the
    // three darkest theme grounds, the same situation DyePalette's
    // GoldAccent/TurmericYellow record against the third faction constant.
    // These tests record the true relationship rather than asserting past
    // it; VIS-033 (theme contrast continuity) owns reconciling it. ---

    [Theory]
    [MemberData(nameof(AllClothingColorData))]
    public void CharredWoodBrown_ClearsTheClothingEnvelopeAgainstEveryClothingColor(Color clothingColor)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.CharredWoodBrown,
            [clothingColor],
            ContrastEnvelope.MinimumClothingDistance));
    }

    [Fact]
    public void CharredWoodBrown_ClearsTheGroundEnvelopeAgainstItsSafeThemes()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.CharredWoodBrown,
            [
                GroundShadeFieldManual,
                GroundShadeBroadcast,
                GroundShadeDatuCourt,
            ],
            ContrastEnvelope.MinimumGroundDistance));
    }

    [Fact]
    public void CharredWoodBrown_DoesNotClearTheGroundEnvelopeAgainstTheThreeDarkestThemes()
    {
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.CharredWoodBrown,
            [GroundShadeCommand, GroundShadeSignal, GroundShadeHighContrast],
            ContrastEnvelope.MinimumGroundDistance));
    }

    // --- R-W1.7 / VIS-005: contrast envelope (DyePalette.IronBlueBlack —
    // the existing, already-shipped, unconditional blade color; not
    // introduced by this task, tested here only because R-W1.7 names it and
    // this catalog now reuses it as both Kalis tints' BladeColor. Same
    // "record the true relationship" treatment as CharredWoodBrown above.
    // ---

    [Fact]
    public void IronBlueBlack_ClearsTheClothingEnvelopeAgainstTheNonAdjacentClothingColors()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            DyePalette.IronBlueBlack,
            [ClothingCream, ClothingTextileRed],
            ContrastEnvelope.MinimumClothingDistance));
    }

    [Fact]
    public void IronBlueBlack_DoesNotClearTheClothingEnvelopeAgainstIndigoOrPatinaGreen()
    {
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            DyePalette.IronBlueBlack,
            [ClothingIndigo, ClothingPatinaGreen],
            ContrastEnvelope.MinimumClothingDistance));
    }

    [Fact]
    public void IronBlueBlack_ClearsTheGroundEnvelopeAgainstTheLighterThemes()
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            DyePalette.IronBlueBlack,
            [GroundShadeFieldManual, GroundShadeBroadcast],
            ContrastEnvelope.MinimumGroundDistance));
    }

    [Fact]
    public void IronBlueBlack_DoesNotClearTheGroundEnvelopeAgainstFourThemes()
    {
        Assert.False(ContrastEnvelope.IsWithinEnvelope(
            DyePalette.IronBlueBlack,
            [
                GroundShadeCommand,
                GroundShadeSignal,
                GroundShadeHighContrast,
                GroundShadeDatuCourt,
            ],
            ContrastEnvelope.MinimumGroundDistance));
    }

    // =====================================================================
    // VIS-011: Kampilan, Wasay, and Itak — structure, evidence, selection,
    // fallback, and contrast-envelope coverage mirroring the Kalis suite
    // above, plus the Wasay lashing band and the W2/K2 exclusion tests.
    // =====================================================================

    // --- Kampilan: structure, evidence, labels, exclusion ---

    [Fact]
    public void KampilanSilhouettes_HasExactlyTwoEntries()
    {
        Assert.Equal(2, WeaponVisualCatalog.KampilanSilhouettes.Count);
    }

    [Fact]
    public void AllKampilanEntries_HaveWellFormedDistinctIds()
    {
        string[] ids =
        [
            WeaponVisualCatalog.KampilanK1.Catalog.Id,
            WeaponVisualCatalog.KampilanK2.Catalog.Id,
            WeaponVisualCatalog.KampilanTintFreshIron.Catalog.Id,
            WeaponVisualCatalog.KampilanTintWornIron.Catalog.Id,
            WeaponVisualCatalog.KampilanTintOchreHilt.Catalog.Id,
        ];

        foreach (var id in ids)
        {
            Assert.True(
                VisualCatalogGrammar.IsWellFormedId(id),
                $"'{id}' is not a well-formed catalog identifier.");
        }

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void AllKampilanTintIds_UseTheTintSubSegment()
    {
        Assert.Contains(".tint.", WeaponVisualCatalog.KampilanTintFreshIron.Catalog.Id);
        Assert.Contains(".tint.", WeaponVisualCatalog.KampilanTintWornIron.Catalog.Id);
        Assert.Contains(".tint.", WeaponVisualCatalog.KampilanTintOchreHilt.Catalog.Id);
    }

    [Fact]
    public void KampilanSilhouetteIndices_AreZeroAndOneInDeclaredOrder()
    {
        Assert.Equal(0, WeaponVisualCatalog.KampilanK1.Catalog.Index);
        Assert.Equal(1, WeaponVisualCatalog.KampilanK2.Catalog.Index);
    }

    [Fact]
    public void KampilanTintIndices_AreZeroOneTwoInDeclaredOrder()
    {
        Assert.Equal(0, WeaponVisualCatalog.KampilanTintFreshIron.Catalog.Index);
        Assert.Equal(1, WeaponVisualCatalog.KampilanTintWornIron.Catalog.Index);
        Assert.Equal(2, WeaponVisualCatalog.KampilanTintOchreHilt.Catalog.Index);
    }

    [Fact]
    public void EveryKampilanEntry_IsTaggedWithTheKampilanWeaponRole()
    {
        Assert.Equal(PawnWeaponRole.Kampilan, WeaponVisualCatalog.KampilanK1.Weapon);
        Assert.Equal(PawnWeaponRole.Kampilan, WeaponVisualCatalog.KampilanK2.Weapon);
        Assert.Equal(PawnWeaponRole.Kampilan, WeaponVisualCatalog.KampilanTintFreshIron.Weapon);
        Assert.Equal(PawnWeaponRole.Kampilan, WeaponVisualCatalog.KampilanTintWornIron.Weapon);
        Assert.Equal(PawnWeaponRole.Kampilan, WeaponVisualCatalog.KampilanTintOchreHilt.Weapon);
    }

    [Fact]
    public void EveryKampilanEntry_CarriesADefinedEvidenceTierAndANonEmptyNote()
    {
        var entries = WeaponVisualCatalog.KampilanSilhouettes
            .Select(entry => entry.Catalog)
            .Concat(WeaponVisualCatalog.GetTints(PawnWeaponRole.Kampilan).Select(entry => entry.Catalog));

        foreach (var entry in entries)
        {
            Assert.True(Enum.IsDefined(entry.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(entry.Notes));
        }
    }

    [Fact]
    public void KampilanK1_CarriesTheDocumentedFormUncertainEvidenceTier()
    {
        Assert.Equal(VisualEvidenceTier.DocumentedFormUncertain, WeaponVisualCatalog.KampilanK1.Catalog.EvidenceTier);
    }

    [Fact]
    public void KampilanK2_CarriesTheProvisionalReconstructionEvidenceTier()
    {
        Assert.Equal(VisualEvidenceTier.ProvisionalReconstruction, WeaponVisualCatalog.KampilanK2.Catalog.EvidenceTier);
    }

    [Fact]
    public void KampilanTints_CarryThePresentationOnlyEvidenceTier()
    {
        Assert.Equal(VisualEvidenceTier.PresentationOnly, WeaponVisualCatalog.KampilanTintFreshIron.Catalog.EvidenceTier);
        Assert.Equal(VisualEvidenceTier.PresentationOnly, WeaponVisualCatalog.KampilanTintWornIron.Catalog.EvidenceTier);
        Assert.Equal(VisualEvidenceTier.PresentationOnly, WeaponVisualCatalog.KampilanTintOchreHilt.Catalog.EvidenceTier);
    }

    [Fact]
    public void EveryKampilanEntry_UsesTheUnchangedPairFormLabel()
    {
        const string expectedLabel = "Kampilan — Great Blade";

        foreach (var entry in WeaponVisualCatalog.KampilanSilhouettes)
        {
            Assert.Equal(expectedLabel, entry.Catalog.DisplayLabel);
        }

        foreach (var tint in WeaponVisualCatalog.GetTints(PawnWeaponRole.Kampilan))
        {
            Assert.Equal(expectedLabel, tint.Catalog.DisplayLabel);
        }
    }

    [Fact]
    public void OnlyKampilanK1_IsPawnSelectable()
    {
        Assert.True(WeaponVisualCatalog.KampilanK1.PawnSelectable);
        Assert.False(WeaponVisualCatalog.KampilanK2.PawnSelectable);
    }

    [Fact]
    public void PawnSilhouette_AlwaysReturnsK1ForKampilan()
    {
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Equal(
                WeaponVisualCatalog.KampilanK1,
                WeaponVisualCatalog.PawnSilhouette(PawnWeaponRole.Kampilan));
        }
    }

    // --- Kampilan: tint-selection stream ---

    [Fact]
    public void SelectTint_IsStableAcrossRepeatedCallsForTheSameEntityId_Kampilan()
    {
        var first = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Kampilan);
        var second = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Kampilan);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SelectTint_ForKampilanAlwaysReturnsOneOfTheThreeCatalogedTints()
    {
        WeaponTintEntry[] allowed =
        [
            WeaponVisualCatalog.KampilanTintFreshIron,
            WeaponVisualCatalog.KampilanTintWornIron,
            WeaponVisualCatalog.KampilanTintOchreHilt,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Contains(WeaponVisualCatalog.SelectTint(entityId, PawnWeaponRole.Kampilan), allowed);
        }
    }

    [Fact]
    public void SelectTint_ForKampilanNeverResolvesToTheInspectorOnlyK2()
    {
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.NotEqual(
                WeaponVisualCatalog.KampilanK2.Catalog.Id,
                WeaponVisualCatalog.SelectTint(entityId, PawnWeaponRole.Kampilan).Catalog.Id);
        }
    }

    [Fact]
    public void GetTints_ForKampilanReturnsAllThreeTintsInIndexOrder()
    {
        var tints = WeaponVisualCatalog.GetTints(PawnWeaponRole.Kampilan);

        Assert.Equal(3, tints.Count);
        Assert.Equal(WeaponVisualCatalog.KampilanTintFreshIron, tints[0]);
        Assert.Equal(WeaponVisualCatalog.KampilanTintWornIron, tints[1]);
        Assert.Equal(WeaponVisualCatalog.KampilanTintOchreHilt, tints[2]);
    }

    [Fact]
    public void KampilanTintCount_IsAtMostThree()
    {
        Assert.True(WeaponVisualCatalog.GetTints(PawnWeaponRole.Kampilan).Count <= 3);
    }

    [Fact]
    public void KampilanWornIronTint_DiffersFromFreshIronOnlyInBladeColor()
    {
        // R-W1.2: tints vary color and tone only. Both share the exact same
        // hilt (grip) color; only the blade color differs.
        Assert.Equal(
            WeaponVisualCatalog.KampilanTintFreshIron.GripColor,
            WeaponVisualCatalog.KampilanTintWornIron.GripColor);
        Assert.NotEqual(
            WeaponVisualCatalog.KampilanTintFreshIron.BladeColor,
            WeaponVisualCatalog.KampilanTintWornIron.BladeColor);
    }

    // --- Kampilan: fallback totality ---

    [Fact]
    public void FallbackChain_Step1SpecificVariantResolvesToTheSelectedKampilanTint()
    {
        var selected = WeaponVisualCatalog.SelectTint(5, PawnWeaponRole.Kampilan);

        var resolution = VisualFallbackResolver.Resolve(
            () => selected,
            () => WeaponVisualCatalog.KampilanTintFreshIron,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.SpecificVariant, resolution.Step);
        Assert.Equal(selected, resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step2FamilyDefaultResolvesWhenTheSpecificKampilanVariantIsMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (WeaponTintEntry?)null,
            () => WeaponVisualCatalog.KampilanTintFreshIron,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.FamilyDefault, resolution.Step);
        Assert.Equal(WeaponVisualCatalog.KampilanTintFreshIron, resolution.Entry);
    }

    // --- Kampilan: contrast envelope (IronWornGrey, PalmRattanOchre — new
    // this task, tuned to clear both bounds cleanly) ---

    [Theory]
    [MemberData(nameof(AllGroundShadeData))]
    public void IronWornGrey_ClearsTheGroundEnvelopeAgainstEveryTheme(Color groundShade)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.IronWornGrey,
            [groundShade],
            ContrastEnvelope.MinimumGroundDistance));
    }

    [Theory]
    [MemberData(nameof(AllClothingColorData))]
    public void IronWornGrey_ClearsTheClothingEnvelopeAgainstEveryClothingColor(Color clothingColor)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.IronWornGrey,
            [clothingColor],
            ContrastEnvelope.MinimumClothingDistance));
    }

    [Theory]
    [MemberData(nameof(AllGroundShadeData))]
    public void PalmRattanOchre_ClearsTheGroundEnvelopeAgainstEveryTheme(Color groundShade)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.PalmRattanOchre,
            [groundShade],
            ContrastEnvelope.MinimumGroundDistance));
    }

    [Theory]
    [MemberData(nameof(AllClothingColorData))]
    public void PalmRattanOchre_ClearsTheClothingEnvelopeAgainstEveryClothingColor(Color clothingColor)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.PalmRattanOchre,
            [clothingColor],
            ContrastEnvelope.MinimumClothingDistance));
    }

    [Fact]
    public void PalmRattanOchre_IsDarkerThanGripWarmOchre()
    {
        // Palette table ordering: "GripWarmOchre — plain grips — warm
        // ochre, lighter than PalmRattanOchre".
        Assert.True(WeaponVisualCatalog.PalmRattanOchre.R < WeaponVisualCatalog.GripWarmOchre.R);
        Assert.True(WeaponVisualCatalog.PalmRattanOchre.G < WeaponVisualCatalog.GripWarmOchre.G);
    }

    // --- Wasay: structure, evidence, labels ---

    [Fact]
    public void WasaySilhouettes_HasExactlyOneEntry()
    {
        Assert.Single(WeaponVisualCatalog.WasaySilhouettes);
        Assert.Equal(WeaponVisualCatalog.WasayW1, WeaponVisualCatalog.WasaySilhouettes[0]);
    }

    [Fact]
    public void AllWasayEntries_HaveWellFormedDistinctIds()
    {
        string[] ids =
        [
            WeaponVisualCatalog.WasayW1.Catalog.Id,
            WeaponVisualCatalog.WasayTintOchreHaft.Catalog.Id,
            WeaponVisualCatalog.WasayTintCharredHaft.Catalog.Id,
            WeaponVisualCatalog.WasayTintLashedWorn.Catalog.Id,
        ];

        foreach (var id in ids)
        {
            Assert.True(
                VisualCatalogGrammar.IsWellFormedId(id),
                $"'{id}' is not a well-formed catalog identifier.");
        }

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void WasayTintIndices_AreZeroOneTwoInDeclaredOrder()
    {
        Assert.Equal(0, WeaponVisualCatalog.WasayTintOchreHaft.Catalog.Index);
        Assert.Equal(1, WeaponVisualCatalog.WasayTintCharredHaft.Catalog.Index);
        Assert.Equal(2, WeaponVisualCatalog.WasayTintLashedWorn.Catalog.Index);
    }

    [Fact]
    public void EveryWasayEntry_IsTaggedWithTheWasayWeaponRole()
    {
        Assert.Equal(PawnWeaponRole.Wasay, WeaponVisualCatalog.WasayW1.Weapon);
        Assert.Equal(PawnWeaponRole.Wasay, WeaponVisualCatalog.WasayTintOchreHaft.Weapon);
        Assert.Equal(PawnWeaponRole.Wasay, WeaponVisualCatalog.WasayTintCharredHaft.Weapon);
        Assert.Equal(PawnWeaponRole.Wasay, WeaponVisualCatalog.WasayTintLashedWorn.Weapon);
    }

    [Fact]
    public void EveryWasayEntry_CarriesADefinedEvidenceTierAndANonEmptyNote()
    {
        var entries = WeaponVisualCatalog.WasaySilhouettes
            .Select(entry => entry.Catalog)
            .Concat(WeaponVisualCatalog.GetTints(PawnWeaponRole.Wasay).Select(entry => entry.Catalog));

        foreach (var entry in entries)
        {
            Assert.True(Enum.IsDefined(entry.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(entry.Notes));
        }
    }

    [Fact]
    public void WasayW1_CarriesTheDocumentedFormUncertainEvidenceTier()
    {
        Assert.Equal(VisualEvidenceTier.DocumentedFormUncertain, WeaponVisualCatalog.WasayW1.Catalog.EvidenceTier);
    }

    [Fact]
    public void EveryWasayEntry_UsesTheUnchangedPairFormLabel()
    {
        const string expectedLabel = "Wasay — War Axe";

        Assert.Equal(expectedLabel, WeaponVisualCatalog.WasayW1.Catalog.DisplayLabel);

        foreach (var tint in WeaponVisualCatalog.GetTints(PawnWeaponRole.Wasay))
        {
            Assert.Equal(expectedLabel, tint.Catalog.DisplayLabel);
        }
    }

    [Fact]
    public void PawnSilhouette_AlwaysReturnsW1ForWasay()
    {
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Equal(
                WeaponVisualCatalog.WasayW1,
                WeaponVisualCatalog.PawnSilhouette(PawnWeaponRole.Wasay));
        }
    }

    // --- R-W1.4: the Cordilleran head axe (research W2) has no catalog
    // identifier anywhere, not merely no pawn-scale reachability ---

    [Fact]
    public void NoWasayW2Identifier_ExistsAnywhereInTheCatalog()
    {
        var allWasayIds = WeaponVisualCatalog.WasaySilhouettes
            .Select(entry => entry.Catalog.Id)
            .Concat(WeaponVisualCatalog.GetTints(PawnWeaponRole.Wasay).Select(entry => entry.Catalog.Id))
            .ToList();

        Assert.DoesNotContain("weapon.wasay.w2", allWasayIds);
        Assert.Single(allWasayIds, id => !id.Contains(".tint.", StringComparison.Ordinal));
    }

    // --- Wasay: tint-selection stream ---

    [Fact]
    public void SelectTint_IsStableAcrossRepeatedCallsForTheSameEntityId_Wasay()
    {
        var first = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Wasay);
        var second = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Wasay);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SelectTint_ForWasayAlwaysReturnsOneOfTheThreeCatalogedTints()
    {
        WeaponTintEntry[] allowed =
        [
            WeaponVisualCatalog.WasayTintOchreHaft,
            WeaponVisualCatalog.WasayTintCharredHaft,
            WeaponVisualCatalog.WasayTintLashedWorn,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Contains(WeaponVisualCatalog.SelectTint(entityId, PawnWeaponRole.Wasay), allowed);
        }
    }

    [Fact]
    public void GetTints_ForWasayReturnsAllThreeTintsInIndexOrder()
    {
        var tints = WeaponVisualCatalog.GetTints(PawnWeaponRole.Wasay);

        Assert.Equal(3, tints.Count);
        Assert.Equal(WeaponVisualCatalog.WasayTintOchreHaft, tints[0]);
        Assert.Equal(WeaponVisualCatalog.WasayTintCharredHaft, tints[1]);
        Assert.Equal(WeaponVisualCatalog.WasayTintLashedWorn, tints[2]);
    }

    [Fact]
    public void WasayTintCount_IsAtMostThree()
    {
        Assert.True(WeaponVisualCatalog.GetTints(PawnWeaponRole.Wasay).Count <= 3);
    }

    // --- R-W1.5: only lashedWorn carries the lashing band accent ---

    [Fact]
    public void OnlyWasayLashedWornTint_CarriesTheLashingBandColor()
    {
        Assert.Null(WeaponVisualCatalog.WasayTintOchreHaft.LashingBandColor);
        Assert.Null(WeaponVisualCatalog.WasayTintCharredHaft.LashingBandColor);
        Assert.Equal(
            WeaponVisualCatalog.RattanLashingTone,
            WeaponVisualCatalog.WasayTintLashedWorn.LashingBandColor);
    }

    [Fact]
    public void WasayLashedWornTint_CarriesTheMediumMinimumDetailTier()
    {
        // The design's own table: "the accent draws at Medium/High detail
        // tier only (R-W1.5)".
        Assert.Equal(VisualDetailTier.Medium, WeaponVisualCatalog.WasayTintLashedWorn.Catalog.MinimumDetailTier);
    }

    // --- Wasay: fallback totality ---

    [Fact]
    public void FallbackChain_Step1SpecificVariantResolvesToTheSelectedWasayTint()
    {
        var selected = WeaponVisualCatalog.SelectTint(5, PawnWeaponRole.Wasay);

        var resolution = VisualFallbackResolver.Resolve(
            () => selected,
            () => WeaponVisualCatalog.WasayTintOchreHaft,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.SpecificVariant, resolution.Step);
        Assert.Equal(selected, resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step2FamilyDefaultResolvesWhenTheSpecificWasayVariantIsMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (WeaponTintEntry?)null,
            () => WeaponVisualCatalog.WasayTintOchreHaft,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.FamilyDefault, resolution.Step);
        Assert.Equal(WeaponVisualCatalog.WasayTintOchreHaft, resolution.Entry);
    }

    // --- Wasay: contrast envelope (RattanLashingTone — new this task) ---

    [Theory]
    [MemberData(nameof(AllGroundShadeData))]
    public void RattanLashingTone_ClearsTheGroundEnvelopeAgainstEveryTheme(Color groundShade)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.RattanLashingTone,
            [groundShade],
            ContrastEnvelope.MinimumGroundDistance));
    }

    [Theory]
    [MemberData(nameof(AllClothingColorData))]
    public void RattanLashingTone_ClearsTheClothingEnvelopeAgainstEveryClothingColor(Color clothingColor)
    {
        Assert.True(ContrastEnvelope.IsWithinEnvelope(
            WeaponVisualCatalog.RattanLashingTone,
            [clothingColor],
            ContrastEnvelope.MinimumClothingDistance));
    }

    [Fact]
    public void RattanLashingTone_IsDistinctFromBothWasayHaftTones()
    {
        Assert.NotEqual(WeaponVisualCatalog.RattanLashingTone, WeaponVisualCatalog.PalmRattanOchre);
        Assert.NotEqual(WeaponVisualCatalog.RattanLashingTone, WeaponVisualCatalog.CharredWoodBrown);
    }

    // --- Itak: structure, evidence, labels ---

    [Fact]
    public void ItakSilhouettes_HasExactlyOneEntry()
    {
        Assert.Single(WeaponVisualCatalog.ItakSilhouettes);
        Assert.Equal(WeaponVisualCatalog.ItakI1, WeaponVisualCatalog.ItakSilhouettes[0]);
    }

    [Fact]
    public void AllItakEntries_HaveWellFormedDistinctIds()
    {
        string[] ids =
        [
            WeaponVisualCatalog.ItakI1.Catalog.Id,
            WeaponVisualCatalog.ItakTintPlainOchre.Catalog.Id,
            WeaponVisualCatalog.ItakTintWornField.Catalog.Id,
        ];

        foreach (var id in ids)
        {
            Assert.True(
                VisualCatalogGrammar.IsWellFormedId(id),
                $"'{id}' is not a well-formed catalog identifier.");
        }

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void EveryItakEntry_IsTaggedWithTheItakWeaponRole()
    {
        Assert.Equal(PawnWeaponRole.Itak, WeaponVisualCatalog.ItakI1.Weapon);
        Assert.Equal(PawnWeaponRole.Itak, WeaponVisualCatalog.ItakTintPlainOchre.Weapon);
        Assert.Equal(PawnWeaponRole.Itak, WeaponVisualCatalog.ItakTintWornField.Weapon);
    }

    [Fact]
    public void EveryItakEntry_CarriesADefinedEvidenceTierAndANonEmptyNote()
    {
        var entries = WeaponVisualCatalog.ItakSilhouettes
            .Select(entry => entry.Catalog)
            .Concat(WeaponVisualCatalog.GetTints(PawnWeaponRole.Itak).Select(entry => entry.Catalog));

        foreach (var entry in entries)
        {
            Assert.True(Enum.IsDefined(entry.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(entry.Notes));
        }
    }

    [Fact]
    public void ItakI1_CarriesTheProvisionalReconstructionEvidenceTier()
    {
        Assert.Equal(VisualEvidenceTier.ProvisionalReconstruction, WeaponVisualCatalog.ItakI1.Catalog.EvidenceTier);
    }

    [Fact]
    public void EveryItakEntry_UsesTheUnchangedPairFormLabel()
    {
        const string expectedLabel = "Itak — Work Blade";

        Assert.Equal(expectedLabel, WeaponVisualCatalog.ItakI1.Catalog.DisplayLabel);

        foreach (var tint in WeaponVisualCatalog.GetTints(PawnWeaponRole.Itak))
        {
            Assert.Equal(expectedLabel, tint.Catalog.DisplayLabel);
        }
    }

    [Fact]
    public void PawnSilhouette_AlwaysReturnsI1ForItak()
    {
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Equal(
                WeaponVisualCatalog.ItakI1,
                WeaponVisualCatalog.PawnSilhouette(PawnWeaponRole.Itak));
        }
    }

    // --- Itak: tint-selection stream (R-W1.8: two tints is the honest
    // ceiling for the plainest weapon in the roster — not required to hit
    // the three-tint maximum) ---

    [Fact]
    public void SelectTint_IsStableAcrossRepeatedCallsForTheSameEntityId_Itak()
    {
        var first = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Itak);
        var second = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Itak);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SelectTint_ForItakAlwaysReturnsOneOfTheTwoCatalogedTints()
    {
        WeaponTintEntry[] allowed =
        [
            WeaponVisualCatalog.ItakTintPlainOchre,
            WeaponVisualCatalog.ItakTintWornField,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Contains(WeaponVisualCatalog.SelectTint(entityId, PawnWeaponRole.Itak), allowed);
        }
    }

    [Fact]
    public void GetTints_ForItakReturnsBothTintsInIndexOrder()
    {
        var tints = WeaponVisualCatalog.GetTints(PawnWeaponRole.Itak);

        Assert.Equal(2, tints.Count);
        Assert.Equal(WeaponVisualCatalog.ItakTintPlainOchre, tints[0]);
        Assert.Equal(WeaponVisualCatalog.ItakTintWornField, tints[1]);
    }

    [Fact]
    public void ItakTintCount_IsAtMostThree()
    {
        Assert.True(WeaponVisualCatalog.GetTints(PawnWeaponRole.Itak).Count <= 3);
    }

    // --- Itak: fallback totality ---

    [Fact]
    public void FallbackChain_Step1SpecificVariantResolvesToTheSelectedItakTint()
    {
        var selected = WeaponVisualCatalog.SelectTint(5, PawnWeaponRole.Itak);

        var resolution = VisualFallbackResolver.Resolve(
            () => selected,
            () => WeaponVisualCatalog.ItakTintPlainOchre,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.SpecificVariant, resolution.Step);
        Assert.Equal(selected, resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step2FamilyDefaultResolvesWhenTheSpecificItakVariantIsMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (WeaponTintEntry?)null,
            () => WeaponVisualCatalog.ItakTintPlainOchre,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.FamilyDefault, resolution.Step);
        Assert.Equal(WeaponVisualCatalog.ItakTintPlainOchre, resolution.Entry);
    }

    // =====================================================================
    // RU-10: Bangkaw, Busog, and Arquebus — structure, evidence, selection,
    // and fallback coverage mirroring the melee weapon suites above. No
    // geometry: RU-10 owns the catalog only (ranged-units.md row RU-10).
    // =====================================================================

    // --- Bangkaw: structure, evidence, labels ---

    [Fact]
    public void BangkawSilhouettes_HasExactlyOneEntry()
    {
        Assert.Single(WeaponVisualCatalog.BangkawSilhouettes);
        Assert.Equal(WeaponVisualCatalog.BangkawB1, WeaponVisualCatalog.BangkawSilhouettes[0]);
    }

    [Fact]
    public void AllBangkawEntries_HaveWellFormedDistinctIds()
    {
        string[] ids =
        [
            WeaponVisualCatalog.BangkawB1.Catalog.Id,
            WeaponVisualCatalog.BangkawTintDarkShaft.Catalog.Id,
            WeaponVisualCatalog.BangkawTintOchreShaft.Catalog.Id,
        ];

        foreach (var id in ids)
        {
            Assert.True(
                VisualCatalogGrammar.IsWellFormedId(id),
                $"'{id}' is not a well-formed catalog identifier.");
        }

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void EveryBangkawEntry_IsTaggedWithTheBangkawWeaponRole()
    {
        Assert.Equal(PawnWeaponRole.Bangkaw, WeaponVisualCatalog.BangkawB1.Weapon);
        Assert.Equal(PawnWeaponRole.Bangkaw, WeaponVisualCatalog.BangkawTintDarkShaft.Weapon);
        Assert.Equal(PawnWeaponRole.Bangkaw, WeaponVisualCatalog.BangkawTintOchreShaft.Weapon);
    }

    [Fact]
    public void EveryBangkawEntry_CarriesADefinedEvidenceTierAndANonEmptyNote()
    {
        var entries = WeaponVisualCatalog.BangkawSilhouettes
            .Select(entry => entry.Catalog)
            .Concat(WeaponVisualCatalog.GetTints(PawnWeaponRole.Bangkaw).Select(entry => entry.Catalog));

        foreach (var entry in entries)
        {
            Assert.True(Enum.IsDefined(entry.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(entry.Notes));
        }
    }

    [Fact]
    public void BangkawB1_CarriesTheDocumentedEvidenceTier()
    {
        // Pigafetta records bamboo spears — some iron-tipped — thrown and
        // reused at Mactan in 1521, and his own vocabulary names the
        // weapon (bancan/bangcao) with a zero-year gap.
        Assert.Equal(VisualEvidenceTier.Documented, WeaponVisualCatalog.BangkawB1.Catalog.EvidenceTier);
    }

    [Fact]
    public void BangkawTints_CarryThePresentationOnlyEvidenceTier()
    {
        Assert.Equal(VisualEvidenceTier.PresentationOnly, WeaponVisualCatalog.BangkawTintDarkShaft.Catalog.EvidenceTier);
        Assert.Equal(VisualEvidenceTier.PresentationOnly, WeaponVisualCatalog.BangkawTintOchreShaft.Catalog.EvidenceTier);
    }

    [Fact]
    public void EveryBangkawEntry_UsesTheUnchangedPairFormLabel()
    {
        const string expectedLabel = "Bangkaw — Long Spear";

        foreach (var entry in WeaponVisualCatalog.BangkawSilhouettes)
        {
            Assert.Equal(expectedLabel, entry.Catalog.DisplayLabel);
        }

        foreach (var tint in WeaponVisualCatalog.GetTints(PawnWeaponRole.Bangkaw))
        {
            Assert.Equal(expectedLabel, tint.Catalog.DisplayLabel);
        }
    }

    [Fact]
    public void OnlyBangkawB1_IsPawnSelectable()
    {
        Assert.True(WeaponVisualCatalog.BangkawB1.PawnSelectable);
    }

    // --- Bangkaw: tint-selection stream ---

    [Fact]
    public void SelectTint_IsStableAcrossRepeatedCallsForTheSameEntityId_Bangkaw()
    {
        var first = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Bangkaw);
        var second = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Bangkaw);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SelectTint_ForBangkawAlwaysReturnsOneOfTheTwoCatalogedTints()
    {
        WeaponTintEntry[] allowed =
        [
            WeaponVisualCatalog.BangkawTintDarkShaft,
            WeaponVisualCatalog.BangkawTintOchreShaft,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Contains(WeaponVisualCatalog.SelectTint(entityId, PawnWeaponRole.Bangkaw), allowed);
        }
    }

    [Fact]
    public void GetTints_ForBangkawReturnsBothTintsInIndexOrder()
    {
        var tints = WeaponVisualCatalog.GetTints(PawnWeaponRole.Bangkaw);

        Assert.Equal(2, tints.Count);
        Assert.Equal(WeaponVisualCatalog.BangkawTintDarkShaft, tints[0]);
        Assert.Equal(WeaponVisualCatalog.BangkawTintOchreShaft, tints[1]);
    }

    // --- Bangkaw: fallback totality ---

    [Fact]
    public void FallbackChain_Step1SpecificVariantResolvesToTheSelectedBangkawTint()
    {
        var selected = WeaponVisualCatalog.SelectTint(5, PawnWeaponRole.Bangkaw);

        var resolution = VisualFallbackResolver.Resolve(
            () => selected,
            () => WeaponVisualCatalog.BangkawTintDarkShaft,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.SpecificVariant, resolution.Step);
        Assert.Equal(selected, resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step2FamilyDefaultResolvesWhenTheSpecificBangkawVariantIsMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (WeaponTintEntry?)null,
            () => WeaponVisualCatalog.BangkawTintDarkShaft,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.FamilyDefault, resolution.Step);
        Assert.Equal(WeaponVisualCatalog.BangkawTintDarkShaft, resolution.Entry);
    }

    // --- Busog: structure, evidence, labels ---

    [Fact]
    public void BusogSilhouettes_HasExactlyOneEntry()
    {
        Assert.Single(WeaponVisualCatalog.BusogSilhouettes);
        Assert.Equal(WeaponVisualCatalog.BusogB1, WeaponVisualCatalog.BusogSilhouettes[0]);
    }

    [Fact]
    public void AllBusogEntries_HaveWellFormedDistinctIds()
    {
        string[] ids =
        [
            WeaponVisualCatalog.BusogB1.Catalog.Id,
            WeaponVisualCatalog.BusogTintPaleStave.Catalog.Id,
            WeaponVisualCatalog.BusogTintDarkStave.Catalog.Id,
        ];

        foreach (var id in ids)
        {
            Assert.True(
                VisualCatalogGrammar.IsWellFormedId(id),
                $"'{id}' is not a well-formed catalog identifier.");
        }

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void EveryBusogEntry_IsTaggedWithTheBusogWeaponRole()
    {
        Assert.Equal(PawnWeaponRole.Busog, WeaponVisualCatalog.BusogB1.Weapon);
        Assert.Equal(PawnWeaponRole.Busog, WeaponVisualCatalog.BusogTintPaleStave.Weapon);
        Assert.Equal(PawnWeaponRole.Busog, WeaponVisualCatalog.BusogTintDarkStave.Weapon);
    }

    [Fact]
    public void EveryBusogEntry_CarriesADefinedEvidenceTierAndANonEmptyNote()
    {
        var entries = WeaponVisualCatalog.BusogSilhouettes
            .Select(entry => entry.Catalog)
            .Concat(WeaponVisualCatalog.GetTints(PawnWeaponRole.Busog).Select(entry => entry.Catalog));

        foreach (var entry in entries)
        {
            Assert.True(Enum.IsDefined(entry.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(entry.Notes));
        }
    }

    [Fact]
    public void BusogB1_CarriesTheDocumentedEvidenceTier()
    {
        // Pigafetta's own 1521 Visayan vocabulary records bossugh (bosog),
        // inherited from Proto-Austronesian busuʀ — a zero-year gap to the
        // depicted period, the strongest name attestation in the package.
        Assert.Equal(VisualEvidenceTier.Documented, WeaponVisualCatalog.BusogB1.Catalog.EvidenceTier);
    }

    [Fact]
    public void BusogTints_CarryThePresentationOnlyEvidenceTier()
    {
        Assert.Equal(VisualEvidenceTier.PresentationOnly, WeaponVisualCatalog.BusogTintPaleStave.Catalog.EvidenceTier);
        Assert.Equal(VisualEvidenceTier.PresentationOnly, WeaponVisualCatalog.BusogTintDarkStave.Catalog.EvidenceTier);
    }

    [Fact]
    public void EveryBusogEntry_UsesTheUnchangedPairFormLabel()
    {
        const string expectedLabel = "Busog — War Bow";

        foreach (var entry in WeaponVisualCatalog.BusogSilhouettes)
        {
            Assert.Equal(expectedLabel, entry.Catalog.DisplayLabel);
        }

        foreach (var tint in WeaponVisualCatalog.GetTints(PawnWeaponRole.Busog))
        {
            Assert.Equal(expectedLabel, tint.Catalog.DisplayLabel);
        }
    }

    [Fact]
    public void OnlyBusogB1_IsPawnSelectable()
    {
        Assert.True(WeaponVisualCatalog.BusogB1.PawnSelectable);
    }

    // --- Busog: tint-selection stream ---

    [Fact]
    public void SelectTint_IsStableAcrossRepeatedCallsForTheSameEntityId_Busog()
    {
        var first = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Busog);
        var second = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Busog);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SelectTint_ForBusogAlwaysReturnsOneOfTheTwoCatalogedTints()
    {
        WeaponTintEntry[] allowed =
        [
            WeaponVisualCatalog.BusogTintPaleStave,
            WeaponVisualCatalog.BusogTintDarkStave,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Contains(WeaponVisualCatalog.SelectTint(entityId, PawnWeaponRole.Busog), allowed);
        }
    }

    [Fact]
    public void GetTints_ForBusogReturnsBothTintsInIndexOrder()
    {
        var tints = WeaponVisualCatalog.GetTints(PawnWeaponRole.Busog);

        Assert.Equal(2, tints.Count);
        Assert.Equal(WeaponVisualCatalog.BusogTintPaleStave, tints[0]);
        Assert.Equal(WeaponVisualCatalog.BusogTintDarkStave, tints[1]);
    }

    [Fact]
    public void BothBusogTints_ShareTheExactSameArrowPointAccentColor()
    {
        // Arrows are documented as hardwood-tipped, never iron (Artieda);
        // only the stave tone varies between the two tints, matching R-X.3
        // for the melee weapons above.
        Assert.Equal(
            WeaponVisualCatalog.BusogTintPaleStave.BladeColor,
            WeaponVisualCatalog.BusogTintDarkStave.BladeColor);
    }

    // --- Busog: fallback totality ---

    [Fact]
    public void FallbackChain_Step1SpecificVariantResolvesToTheSelectedBusogTint()
    {
        var selected = WeaponVisualCatalog.SelectTint(5, PawnWeaponRole.Busog);

        var resolution = VisualFallbackResolver.Resolve(
            () => selected,
            () => WeaponVisualCatalog.BusogTintPaleStave,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.SpecificVariant, resolution.Step);
        Assert.Equal(selected, resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step2FamilyDefaultResolvesWhenTheSpecificBusogVariantIsMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (WeaponTintEntry?)null,
            () => WeaponVisualCatalog.BusogTintPaleStave,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.FamilyDefault, resolution.Step);
        Assert.Equal(WeaponVisualCatalog.BusogTintPaleStave, resolution.Entry);
    }

    // --- Arquebus: structure, evidence, labels ---

    [Fact]
    public void ArquebusSilhouettes_HasExactlyOneEntry()
    {
        Assert.Single(WeaponVisualCatalog.ArquebusSilhouettes);
        Assert.Equal(WeaponVisualCatalog.ArquebusA1, WeaponVisualCatalog.ArquebusSilhouettes[0]);
    }

    [Fact]
    public void AllArquebusEntries_HaveWellFormedDistinctIds()
    {
        string[] ids =
        [
            WeaponVisualCatalog.ArquebusA1.Catalog.Id,
            WeaponVisualCatalog.ArquebusTintFreshBarrel.Catalog.Id,
            WeaponVisualCatalog.ArquebusTintWornBarrel.Catalog.Id,
        ];

        foreach (var id in ids)
        {
            Assert.True(
                VisualCatalogGrammar.IsWellFormedId(id),
                $"'{id}' is not a well-formed catalog identifier.");
        }

        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void EveryArquebusEntry_IsTaggedWithTheArquebusWeaponRole()
    {
        Assert.Equal(PawnWeaponRole.Arquebus, WeaponVisualCatalog.ArquebusA1.Weapon);
        Assert.Equal(PawnWeaponRole.Arquebus, WeaponVisualCatalog.ArquebusTintFreshBarrel.Weapon);
        Assert.Equal(PawnWeaponRole.Arquebus, WeaponVisualCatalog.ArquebusTintWornBarrel.Weapon);
    }

    [Fact]
    public void EveryArquebusEntry_CarriesADefinedEvidenceTierAndANonEmptyNote()
    {
        var entries = WeaponVisualCatalog.ArquebusSilhouettes
            .Select(entry => entry.Catalog)
            .Concat(WeaponVisualCatalog.GetTints(PawnWeaponRole.Arquebus).Select(entry => entry.Catalog));

        foreach (var entry in entries)
        {
            Assert.True(Enum.IsDefined(entry.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(entry.Notes));
        }
    }

    [Fact]
    public void ArquebusA1_CarriesTheDocumentedFormUncertainEvidenceTier()
    {
        // Escalante Alvarado (c. 1543-45) and Legazpi's 1567 specimen
        // establish local possession; the exact form and how common the
        // weapon was remain uncertain.
        Assert.Equal(VisualEvidenceTier.DocumentedFormUncertain, WeaponVisualCatalog.ArquebusA1.Catalog.EvidenceTier);
    }

    [Fact]
    public void ArquebusTints_CarryThePresentationOnlyEvidenceTier()
    {
        Assert.Equal(VisualEvidenceTier.PresentationOnly, WeaponVisualCatalog.ArquebusTintFreshBarrel.Catalog.EvidenceTier);
        Assert.Equal(VisualEvidenceTier.PresentationOnly, WeaponVisualCatalog.ArquebusTintWornBarrel.Catalog.EvidenceTier);
    }

    // --- CLAUDE.md section 7: "Arquebus" is not a cultural identification,
    // so it never carries an em-dash pair or a Filipino name — this is the
    // deliberate exception the naming policy documents, not an omission. ---

    [Fact]
    public void EveryArquebusEntry_UsesTheUnchangedUnpairedImportedLabel()
    {
        const string expectedLabel = "Imported Arquebus";

        foreach (var entry in WeaponVisualCatalog.ArquebusSilhouettes)
        {
            Assert.Equal(expectedLabel, entry.Catalog.DisplayLabel);
            Assert.DoesNotContain("—", entry.Catalog.DisplayLabel, StringComparison.Ordinal);
        }

        foreach (var tint in WeaponVisualCatalog.GetTints(PawnWeaponRole.Arquebus))
        {
            Assert.Equal(expectedLabel, tint.Catalog.DisplayLabel);
        }
    }

    [Fact]
    public void OnlyArquebusA1_IsPawnSelectable()
    {
        Assert.True(WeaponVisualCatalog.ArquebusA1.PawnSelectable);
    }

    // --- Arquebus: tint-selection stream ---

    [Fact]
    public void SelectTint_IsStableAcrossRepeatedCallsForTheSameEntityId_Arquebus()
    {
        var first = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Arquebus);
        var second = WeaponVisualCatalog.SelectTint(19, PawnWeaponRole.Arquebus);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SelectTint_ForArquebusAlwaysReturnsOneOfTheTwoCatalogedTints()
    {
        WeaponTintEntry[] allowed =
        [
            WeaponVisualCatalog.ArquebusTintFreshBarrel,
            WeaponVisualCatalog.ArquebusTintWornBarrel,
        ];

        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Contains(WeaponVisualCatalog.SelectTint(entityId, PawnWeaponRole.Arquebus), allowed);
        }
    }

    [Fact]
    public void GetTints_ForArquebusReturnsBothTintsInIndexOrder()
    {
        var tints = WeaponVisualCatalog.GetTints(PawnWeaponRole.Arquebus);

        Assert.Equal(2, tints.Count);
        Assert.Equal(WeaponVisualCatalog.ArquebusTintFreshBarrel, tints[0]);
        Assert.Equal(WeaponVisualCatalog.ArquebusTintWornBarrel, tints[1]);
    }

    [Fact]
    public void ArquebusWornBarrelTint_DiffersFromFreshBarrelOnlyInBladeColor()
    {
        // R-W1.2: tints vary color and tone only. Both share the exact
        // same stock (grip) color; only the barrel color differs.
        Assert.Equal(
            WeaponVisualCatalog.ArquebusTintFreshBarrel.GripColor,
            WeaponVisualCatalog.ArquebusTintWornBarrel.GripColor);
        Assert.NotEqual(
            WeaponVisualCatalog.ArquebusTintFreshBarrel.BladeColor,
            WeaponVisualCatalog.ArquebusTintWornBarrel.BladeColor);
    }

    // --- Arquebus: fallback totality ---

    [Fact]
    public void FallbackChain_Step1SpecificVariantResolvesToTheSelectedArquebusTint()
    {
        var selected = WeaponVisualCatalog.SelectTint(5, PawnWeaponRole.Arquebus);

        var resolution = VisualFallbackResolver.Resolve(
            () => selected,
            () => WeaponVisualCatalog.ArquebusTintFreshBarrel,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.SpecificVariant, resolution.Step);
        Assert.Equal(selected, resolution.Entry);
    }

    [Fact]
    public void FallbackChain_Step2FamilyDefaultResolvesWhenTheSpecificArquebusVariantIsMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (WeaponTintEntry?)null,
            () => WeaponVisualCatalog.ArquebusTintFreshBarrel,
            () => WeaponVisualCatalog.ModelCategoryDefaultTint,
            _ => true);

        Assert.Equal(VisualFallbackStep.FamilyDefault, resolution.Step);
        Assert.Equal(WeaponVisualCatalog.ArquebusTintFreshBarrel, resolution.Entry);
    }

    // --- R-X.3: all seven weapons stay mutually distinguishable by
    // silhouette at every tier — a proxy for that here is that no two
    // weapons' pawn silhouettes share a catalog identifier or a display
    // label collision that would blur their identity. ---

    [Fact]
    public void AllFourPawnSilhouettes_HaveDistinctCatalogIdentifiers()
    {
        string[] ids =
        [
            WeaponVisualCatalog.KampilanK1.Catalog.Id,
            WeaponVisualCatalog.WasayW1.Catalog.Id,
            WeaponVisualCatalog.KalisL1.Catalog.Id,
            WeaponVisualCatalog.ItakI1.Catalog.Id,
            WeaponVisualCatalog.BangkawB1.Catalog.Id,
            WeaponVisualCatalog.BusogB1.Catalog.Id,
            WeaponVisualCatalog.ArquebusA1.Catalog.Id,
        ];

        Assert.Equal(ids.Length, ids.Distinct().Count());
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

    public static TheoryData<Color> AllClothingColorData()
    {
        var data = new TheoryData<Color>();
        foreach (var color in AllClothingColors)
        {
            data.Add(color);
        }

        return data;
    }
}
