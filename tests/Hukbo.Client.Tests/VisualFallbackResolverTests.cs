using Hukbo.Client.Presentation.Catalogs;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins the four-step fallback chain (visual-system-integration-design.md
/// section 4, R-W6.4): each step is individually reachable under a test
/// double, resolution is total for out-of-range indices and unknown
/// identifiers, and the diagnostic placeholder color is a fixed, opaque,
/// non-theme value.
/// </summary>
public sealed class VisualFallbackResolverTests
{
    private sealed record Entry(string Id);

    private static readonly Entry SpecificEntry = new("weapon.kalis.l1");
    private static readonly Entry FamilyDefaultEntry = new("weapon.kalis.l0");
    private static readonly Entry ModelCategoryDefaultEntry = new("weapon.generic.default");

    private static readonly Func<Entry, bool> AlwaysValid = _ => true;
    private static readonly Func<Entry, bool> AlwaysInvalid = _ => false;

    // --- Each chain step individually reachable ---

    [Fact]
    public void Resolve_ReachesSpecificVariant_WhenTheSelectionRecipesEntryIsValid()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => SpecificEntry,
            () => FamilyDefaultEntry,
            () => ModelCategoryDefaultEntry,
            AlwaysValid);

        Assert.Equal(VisualFallbackStep.SpecificVariant, resolution.Step);
        Assert.Same(SpecificEntry, resolution.Entry);
    }

    [Fact]
    public void Resolve_ReachesFamilyDefault_WhenTheSpecificVariantIsMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (Entry?)null,
            () => FamilyDefaultEntry,
            () => ModelCategoryDefaultEntry,
            AlwaysValid);

        Assert.Equal(VisualFallbackStep.FamilyDefault, resolution.Step);
        Assert.Same(FamilyDefaultEntry, resolution.Entry);
    }

    [Fact]
    public void Resolve_ReachesFamilyDefault_WhenTheSpecificVariantFailsValidation()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => SpecificEntry,
            () => FamilyDefaultEntry,
            () => ModelCategoryDefaultEntry,
            entry => !ReferenceEquals(entry, SpecificEntry));

        Assert.Equal(VisualFallbackStep.FamilyDefault, resolution.Step);
        Assert.Same(FamilyDefaultEntry, resolution.Entry);
    }

    [Fact]
    public void Resolve_ReachesModelCategoryDefault_WhenSpecificAndFamilyDefaultAreBothMissing()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (Entry?)null,
            () => (Entry?)null,
            () => ModelCategoryDefaultEntry,
            AlwaysValid);

        Assert.Equal(VisualFallbackStep.ModelCategoryDefault, resolution.Step);
        Assert.Same(ModelCategoryDefaultEntry, resolution.Entry);
    }

    [Fact]
    public void Resolve_ReachesModelCategoryDefault_WhenSpecificAndFamilyDefaultBothFailValidation()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => SpecificEntry,
            () => FamilyDefaultEntry,
            () => ModelCategoryDefaultEntry,
            entry => ReferenceEquals(entry, ModelCategoryDefaultEntry));

        Assert.Equal(VisualFallbackStep.ModelCategoryDefault, resolution.Step);
        Assert.Same(ModelCategoryDefaultEntry, resolution.Entry);
    }

    [Fact]
    public void Resolve_ReachesDiagnosticPlaceholder_WhenNoStepYieldsAnEntry()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => (Entry?)null,
            () => (Entry?)null,
            () => (Entry?)null,
            AlwaysValid);

        Assert.Equal(VisualFallbackStep.DiagnosticPlaceholder, resolution.Step);
        Assert.Null(resolution.Entry);
    }

    [Fact]
    public void Resolve_ReachesDiagnosticPlaceholder_WhenEveryCandidateFailsValidation()
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => SpecificEntry,
            () => FamilyDefaultEntry,
            () => ModelCategoryDefaultEntry,
            AlwaysInvalid);

        Assert.Equal(VisualFallbackStep.DiagnosticPlaceholder, resolution.Step);
        Assert.Null(resolution.Entry);
    }

    // --- Totality: out-of-range indices and unknown identifiers never throw ---

    private enum TestVariant
    {
        First,
        Second,
        Third,
    }

    private static readonly Entry?[] IndexedFamily =
    [
        new Entry("family.0"),
        new Entry("family.1"),
        new Entry("family.2"),
    ];

    // Simulates a real catalog's array-backed lookup: an in-range index
    // returns the entry, anything else — negative or past the end —
    // returns null rather than throwing, which is the contract the resolver
    // depends on to stay total.
    private static Entry? LookUpByIndex(int index) =>
        index >= 0 && index < IndexedFamily.Length ? IndexedFamily[index] : null;

    [Theory]
    [InlineData((int)TestVariant.First)]
    [InlineData((int)TestVariant.Second)]
    [InlineData((int)TestVariant.Third)]
    [InlineData(-1)]
    [InlineData(-999)]
    [InlineData(3)]
    [InlineData(999)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Resolve_IsTotalOverEveryEnumValueAndOutOfRangeIndices(int requestedIndex)
    {
        var resolution = VisualFallbackResolver.Resolve(
            () => LookUpByIndex(requestedIndex),
            () => LookUpByIndex(0),
            () => ModelCategoryDefaultEntry,
            AlwaysValid);

        // Never throws (implicit: reaching this line at all), and always
        // resolves to a defined step.
        Assert.True(Enum.IsDefined(resolution.Step));
    }

    [Theory]
    [InlineData("weapon.unknown.id")]
    [InlineData("")]
    [InlineData(null)]
    public void Resolve_FallsToModelCategoryDefault_ForAnUnknownIdentifier(string? requestedId)
    {
        static Entry? LookUpById(string? id) =>
            id is "weapon.kalis.l1" ? SpecificEntry : null;

        var resolution = VisualFallbackResolver.Resolve(
            () => LookUpById(requestedId),
            () => (Entry?)null,
            () => ModelCategoryDefaultEntry,
            AlwaysValid);

        Assert.Equal(VisualFallbackStep.ModelCategoryDefault, resolution.Step);
        Assert.Same(ModelCategoryDefaultEntry, resolution.Entry);
    }

    [Fact]
    public void Resolve_FallsToDiagnosticPlaceholder_ForAnUnknownIdentifierWithNoModelCategoryDefault()
    {
        static Entry? LookUpById(string id) =>
            id is "weapon.kalis.l1" ? SpecificEntry : null;

        var resolution = VisualFallbackResolver.Resolve(
            () => LookUpById("weapon.unknown.id"),
            () => (Entry?)null,
            () => (Entry?)null,
            AlwaysValid);

        Assert.Equal(VisualFallbackStep.DiagnosticPlaceholder, resolution.Step);
        Assert.Null(resolution.Entry);
    }

    // --- Argument validation ---

    [Fact]
    public void Resolve_ThrowsOnNullSpecificVariantDelegate()
    {
        Assert.Throws<ArgumentNullException>(() => VisualFallbackResolver.Resolve(
            null!,
            () => FamilyDefaultEntry,
            () => ModelCategoryDefaultEntry,
            AlwaysValid));
    }

    [Fact]
    public void Resolve_ThrowsOnNullFamilyDefaultDelegate()
    {
        Assert.Throws<ArgumentNullException>(() => VisualFallbackResolver.Resolve(
            () => SpecificEntry,
            null!,
            () => ModelCategoryDefaultEntry,
            AlwaysValid));
    }

    [Fact]
    public void Resolve_ThrowsOnNullModelCategoryDefaultDelegate()
    {
        Assert.Throws<ArgumentNullException>(() => VisualFallbackResolver.Resolve(
            () => SpecificEntry,
            () => FamilyDefaultEntry,
            null!,
            AlwaysValid));
    }

    [Fact]
    public void Resolve_ThrowsOnNullIsValidDelegate()
    {
        Assert.Throws<ArgumentNullException>(() => VisualFallbackResolver.Resolve<Entry>(
            () => SpecificEntry,
            () => FamilyDefaultEntry,
            () => ModelCategoryDefaultEntry,
            null!));
    }

    // --- Placeholder color: fixed, conspicuous, opaque, never theme-derived ---

    [Fact]
    public void PlaceholderColor_IsPinnedToFullAlphaMagenta()
    {
        Assert.Equal(new Color(255, 0, 255), VisualFallbackResolver.PlaceholderColor);
    }

    [Fact]
    public void PlaceholderColor_IsFullyOpaque()
    {
        Assert.Equal(255, VisualFallbackResolver.PlaceholderColor.A);
    }

    [Fact]
    public void PlaceholderColor_IsNotBlackOrWhite()
    {
        // Guards against a future edit collapsing the constant into a value
        // indistinguishable from either end of the theme's tone range.
        var color = VisualFallbackResolver.PlaceholderColor;
        Assert.False(color is { R: 0, G: 0, B: 0 });
        Assert.False(color is { R: 255, G: 255, B: 255 });
    }
}
