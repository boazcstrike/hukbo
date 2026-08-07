using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins the once-at-load startup validation pass (implementation-plan-draft.md
/// VIS-006; visual-system-integration-design.md section 2): identifier
/// uniqueness, per-family index uniqueness ("index contiguity" — a
/// documented, reserved gap is not a failure, a collision is), mandatory
/// metadata presence, the per-catalog combination-rule hook, and that a
/// failure here routes a <see cref="VisualFallbackResolver.Resolve{T}"/>
/// call past the specific variant to the family default (section 4). Also
/// pins the acceptance criterion that every catalog shipped today validates
/// clean.
/// </summary>
public sealed class VisualCatalogValidatorTests
{
    private static VisualCatalogEntry MakeValidEntry(string id, int index) =>
        new(
            id,
            index,
            "Display Label",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "Evidence note.",
            VisualDetailTier.Medium);

    private static VisualCatalogValidationEntry MakeValidValidationEntry(
        string id = "appearance.hair.b1",
        int index = 0) =>
        new(
            id,
            index,
            "Display Label",
            VisualEvidenceTier.Documented,
            VisualScopeTag.UnscopedGeneric,
            "Evidence note.",
            VisualDetailTier.Medium);

    // --- A structurally valid catalog passes clean ---

    [Fact]
    public void Validate_ReportsNoFailures_ForAStructurallyValidCatalog()
    {
        VisualCatalogEntry[] entries =
        [
            MakeValidEntry("appearance.hair.b1", 0),
            MakeValidEntry("appearance.hair.b2", 1),
            MakeValidEntry("appearance.headCovering.c1", 0),
        ];

        var result = VisualCatalogValidator.Validate("appearance", entries);

        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
        Assert.Equal("appearance", result.CatalogId);
    }

    [Fact]
    public void Validate_ToleratesAReservedIndexGapWithinAFamily()
    {
        // Mirrors AppearanceComponentCatalog's own documented convention:
        // a research option excluded on purpose (C2, I3) leaves its index
        // slot skipped, never reused. A skip with no collision is not a
        // failure.
        VisualCatalogEntry[] entries =
        [
            MakeValidEntry("appearance.headCovering.c1", 0),
            MakeValidEntry("appearance.headCovering.c3", 2),
            MakeValidEntry("appearance.headCovering.c4", 3),
        ];

        var result = VisualCatalogValidator.Validate("appearance", entries);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ToleratesTheSameIndexReusedAcrossDifferentFamilies()
    {
        // Every AppearanceComponentCategory restarts its own index sequence
        // at 0; index uniqueness is checked within a family, never globally.
        VisualCatalogEntry[] entries =
        [
            MakeValidEntry("appearance.hair.b1", 0),
            MakeValidEntry("appearance.headCovering.c1", 0),
            MakeValidEntry("appearance.torso.d1", 0),
        ];

        var result = VisualCatalogValidator.Validate("appearance", entries);

        Assert.True(result.IsValid);
    }

    // --- Duplicate ID ---

    [Fact]
    public void Validate_ReportsDuplicateId_WhenTwoEntriesShareAnIdentifier()
    {
        VisualCatalogEntry[] entries =
        [
            MakeValidEntry("appearance.hair.b1", 0),
            MakeValidEntry("appearance.hair.b1", 1),
        ];

        var result = VisualCatalogValidator.Validate("appearance", entries);

        Assert.False(result.IsValid);
        var failure = Assert.Single(
            result.Failures,
            f => f.Reason == VisualCatalogValidator.ReasonDuplicateId);
        Assert.Equal("appearance.hair.b1", failure.EntryId);
        Assert.False(result.IsEntryValid("appearance.hair.b1"));
    }

    // --- Index gap (collision within a family) ---

    [Fact]
    public void Validate_ReportsIndexGap_WhenTwoEntriesInTheSameFamilyShareAnIndex()
    {
        VisualCatalogEntry[] entries =
        [
            MakeValidEntry("appearance.hair.b1", 0),
            MakeValidEntry("appearance.hair.b2", 0),
        ];

        var result = VisualCatalogValidator.Validate("appearance", entries);

        Assert.False(result.IsValid);
        Assert.Equal(
            2,
            result.Failures.Count(f => f.Reason == VisualCatalogValidator.ReasonIndexGap));
        Assert.Contains(
            result.Failures,
            f => f.Reason == VisualCatalogValidator.ReasonIndexGap &&
                 f.EntryId == "appearance.hair.b1");
        Assert.Contains(
            result.Failures,
            f => f.Reason == VisualCatalogValidator.ReasonIndexGap &&
                 f.EntryId == "appearance.hair.b2");
    }

    [Fact]
    public void Validate_DoesNotReportIndexGap_WhenTheCollidingEntriesAreInDifferentFamilies()
    {
        VisualCatalogEntry[] entries =
        [
            MakeValidEntry("appearance.hair.b1", 0),
            MakeValidEntry("appearance.headCovering.c1", 0),
        ];

        var result = VisualCatalogValidator.Validate("appearance", entries);

        Assert.True(result.IsValid);
    }

    // --- Missing tier (only reachable via a raw VisualCatalogValidationEntry;
    // VisualCatalogEntry's own constructor forbids this) ---

    [Fact]
    public void Validate_ReportsMissingEvidenceTier_WhenTheFieldIsNull()
    {
        var entry = MakeValidValidationEntry() with { EvidenceTier = null };

        var result = VisualCatalogValidator.Validate("test", [entry]);

        var failure = Assert.Single(result.Failures);
        Assert.Equal(VisualCatalogValidator.ReasonMissingEvidenceTier, failure.Reason);
        Assert.Equal("appearance.hair.b1", failure.EntryId);
    }

    [Fact]
    public void Validate_ReportsMissingEvidenceTier_WhenTheValueIsUndefined()
    {
        var entry = MakeValidValidationEntry() with { EvidenceTier = (VisualEvidenceTier)999 };

        var result = VisualCatalogValidator.Validate("test", [entry]);

        Assert.Contains(
            result.Failures,
            f => f.Reason == VisualCatalogValidator.ReasonMissingEvidenceTier);
    }

    // --- Missing scope tag (structural presence; the "cultural entry"
    // obligation itself is a combination-rule concern, exercised below) ---

    [Fact]
    public void Validate_ReportsMissingScopeTag_WhenTheFieldIsNull()
    {
        var entry = MakeValidValidationEntry() with { ScopeTag = null };

        var result = VisualCatalogValidator.Validate("test", [entry]);

        var failure = Assert.Single(result.Failures);
        Assert.Equal(VisualCatalogValidator.ReasonMissingScopeTag, failure.Reason);
    }

    [Fact]
    public void Validate_ReportsMissingDisplayLabel_WhenTheFieldIsBlank()
    {
        var entry = MakeValidValidationEntry() with { DisplayLabel = "   " };

        var result = VisualCatalogValidator.Validate("test", [entry]);

        Assert.Contains(
            result.Failures,
            f => f.Reason == VisualCatalogValidator.ReasonMissingDisplayLabel);
    }

    [Fact]
    public void Validate_ReportsMissingNotes_WhenTheFieldIsNull()
    {
        var entry = MakeValidValidationEntry() with { Notes = null };

        var result = VisualCatalogValidator.Validate("test", [entry]);

        Assert.Contains(
            result.Failures,
            f => f.Reason == VisualCatalogValidator.ReasonMissingNotes);
    }

    [Fact]
    public void Validate_ReportsMissingDetailTier_WhenTheFieldIsNull()
    {
        var entry = MakeValidValidationEntry() with { MinimumDetailTier = null };

        var result = VisualCatalogValidator.Validate("test", [entry]);

        Assert.Contains(
            result.Failures,
            f => f.Reason == VisualCatalogValidator.ReasonMissingDetailTier);
    }

    [Fact]
    public void Validate_ReportsMissingId_WhenTheFieldIsNull()
    {
        var entry = MakeValidValidationEntry() with { Id = null };

        var result = VisualCatalogValidator.Validate("test", [entry]);

        var failure = Assert.Single(
            result.Failures,
            f => f.Reason == VisualCatalogValidator.ReasonMissingId);
        Assert.Equal(VisualCatalogValidator.UnknownEntryId, failure.EntryId);
    }

    // --- Per-catalog combination rule hook ---

    [Fact]
    public void Validate_ReportsACombinationRuleViolation_WhenTheRuleReturnsOne()
    {
        var entry = MakeValidValidationEntry();
        VisualCatalogCombinationRule rule =
            candidate => new VisualCatalogCombinationViolation(
                "missingScopeTagForCulturalEntry",
                $"entry '{candidate.Id}' needs review.");

        var result = VisualCatalogValidator.Validate("test", [entry], rule);

        var failure = Assert.Single(result.Failures);
        Assert.Equal("missingScopeTagForCulturalEntry", failure.Reason);
        Assert.Equal("entry 'appearance.hair.b1' needs review.", failure.Message);
    }

    [Fact]
    public void Validate_ReportsNoFailure_WhenTheCombinationRuleReturnsNull()
    {
        var entry = MakeValidValidationEntry();
        VisualCatalogCombinationRule rule = _ => null;

        var result = VisualCatalogValidator.Validate("test", [entry], rule);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ExercisesTheMissingScopeTagOnACulturalEntryScenario_ViaACombinationRule()
    {
        // The generic validator has no domain knowledge of which entries are
        // "cultural" — that is exactly what a per-catalog combination rule
        // supplies (visual-system-integration-design.md section 2, "the
        // combination rules that apply to it"). Here: any entry whose
        // evidence tier makes a historical claim needs a scope tag other
        // than NotApplicable.
        VisualCatalogCombinationRule requiresScopeForHistoricalClaims = candidate =>
            candidate.EvidenceTier is { } tier &&
            tier != VisualEvidenceTier.PresentationOnly &&
            candidate.ScopeTag == VisualScopeTag.NotApplicable
                ? new VisualCatalogCombinationViolation("missingScopeTagForCulturalEntry")
                : null;

        var culturalEntryMissingScope = MakeValidValidationEntry() with
        {
            ScopeTag = VisualScopeTag.NotApplicable,
        };

        var result = VisualCatalogValidator.Validate(
            "appearance",
            [culturalEntryMissingScope],
            requiresScopeForHistoricalClaims);

        Assert.Contains(
            result.Failures,
            f => f.Reason == "missingScopeTagForCulturalEntry");
    }

    // --- Failure marks route resolution to the family default (step 2) ---

    [Fact]
    public void FailedEntry_RoutesFallbackResolutionToTheFamilyDefault()
    {
        var specific = MakeValidEntry("appearance.hair.b1", 0);
        var duplicate = MakeValidEntry("appearance.hair.b1", 1);
        var familyDefault = MakeValidEntry("appearance.hair.b0", 0);

        var result = VisualCatalogValidator.Validate(
            "appearance",
            [specific, duplicate]);

        Assert.False(result.IsEntryValid(specific.Id));

        var resolution = VisualFallbackResolver.Resolve(
            () => specific,
            () => familyDefault,
            () => (VisualCatalogEntry?)null,
            candidate => result.IsEntryValid(candidate.Id));

        Assert.Equal(VisualFallbackStep.FamilyDefault, resolution.Step);
        Assert.Same(familyDefault, resolution.Entry);
    }

    [Fact]
    public void AValidEntry_StillResolvesAsTheSpecificVariant()
    {
        var familyDefault = MakeValidEntry("appearance.hair.b0", 0);
        var specific = MakeValidEntry("appearance.hair.b1", 1);

        var result = VisualCatalogValidator.Validate("appearance", [specific, familyDefault]);

        var resolution = VisualFallbackResolver.Resolve(
            () => specific,
            () => familyDefault,
            () => (VisualCatalogEntry?)null,
            candidate => result.IsEntryValid(candidate.Id));

        Assert.Equal(VisualFallbackStep.SpecificVariant, resolution.Step);
        Assert.Same(specific, resolution.Entry);
    }

    // --- Argument validation ---

    [Fact]
    public void Validate_ThrowsOnNullEntries()
    {
        Assert.Throws<ArgumentNullException>(
            () => VisualCatalogValidator.Validate("appearance", (VisualCatalogEntry[])null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ThrowsOnBlankCatalogId(string? catalogId)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => VisualCatalogValidator.Validate(
                catalogId!,
                Array.Empty<VisualCatalogEntry>()));
    }

    // --- Stable, non-dictionary-order failure sequence ---

    [Fact]
    public void Validate_OrdersFailuresByEntryIdThenReason()
    {
        VisualCatalogEntry[] entries =
        [
            MakeValidEntry("appearance.hair.b2", 5),
            MakeValidEntry("appearance.hair.b2", 6),
            MakeValidEntry("appearance.hair.b1", 5),
            MakeValidEntry("appearance.hair.b1", 6),
        ];

        var result = VisualCatalogValidator.Validate("appearance", entries);

        var ids = result.Failures.Select(f => f.EntryId).ToArray();
        Assert.Equal(ids.OrderBy(id => id, StringComparer.Ordinal).ToArray(), ids);
    }

    // --- Shipped catalogs pass at load with zero failures (acceptance criterion) ---

    [Fact]
    public void Validate_ReportsNoFailures_ForTheShippedAppearanceComponentCatalog()
    {
        var result = VisualCatalogValidator.Validate(
            "appearance",
            AppearanceComponentCatalog.All.Select(entry => entry.Catalog).ToArray());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReportsNoFailures_ForTheShippedBackdropVisualCatalog()
    {
        var result = VisualCatalogValidator.Validate("backdrop", BackdropVisualCatalog.All);

        Assert.True(result.IsValid);
    }
}
