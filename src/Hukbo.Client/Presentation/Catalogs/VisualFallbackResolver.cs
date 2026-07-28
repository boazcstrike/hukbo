using Microsoft.Xna.Framework;

namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// The four-step fallback chain a visual lookup resolves through
/// (visual-system-integration-design.md section 4, R-W6.4), in resolution
/// order. Numeric values are stable and may be logged as
/// <c>resolvedStep</c> payload data (R-W6.5) — never renumber a shipped
/// value.
/// </summary>
internal enum VisualFallbackStep
{
    /// <summary>The selection recipe's chosen entry, unmodified.</summary>
    SpecificVariant = 1,

    /// <summary>The family's designated default entry (index 0 by convention).</summary>
    FamilyDefault = 2,

    /// <summary>The generic drawable for the model category.</summary>
    ModelCategoryDefault = 3,

    /// <summary>
    /// The conspicuous diagnostic primitive
    /// (<see cref="VisualFallbackResolver.PlaceholderColor"/>); no catalog
    /// entry backs this step.
    /// </summary>
    DiagnosticPlaceholder = 4,
}

/// <summary>
/// The outcome of one <see cref="VisualFallbackResolver.Resolve{T}"/> call:
/// the step the chain reached, and the resolved entry for every step except
/// <see cref="VisualFallbackStep.DiagnosticPlaceholder"/>, which carries no
/// entry (visual-system-integration-design.md section 4). Callers emit a
/// diagnostic (R-W6.5) whenever <see cref="Step"/> is past
/// <see cref="VisualFallbackStep.SpecificVariant"/>.
/// </summary>
/// <typeparam name="T">The domain's catalog entry type.</typeparam>
/// <param name="Step">The chain step this resolution reached.</param>
/// <param name="Entry">
/// The resolved entry, or <see langword="null"/> when
/// <paramref name="Step"/> is <see cref="VisualFallbackStep.DiagnosticPlaceholder"/>.
/// </param>
internal readonly record struct VisualResolution<T>(VisualFallbackStep Step, T? Entry)
    where T : class;

/// <summary>
/// One pure, total resolution function per visual domain, implementing the
/// four-step fallback chain (visual-system-integration-design.md section 4,
/// R-W6.4): specific variant, then family default, then model-category
/// default, then the diagnostic placeholder. Every caller supplies its own
/// domain-specific lookup delegates (index-based, ID-based, or otherwise);
/// this type only sequences the chain and never inspects how a lookup
/// failed. A delegate signals "this step has no candidate" by returning
/// <see langword="null"/> — never by throwing — which is what keeps
/// resolution total for out-of-range indices and unknown identifiers alike.
/// </summary>
internal static class VisualFallbackResolver
{
    // Conspicuous, never theme-derived, never invisible (R-W6.4). Full-alpha
    // magenta — a color no theme or historical palette in this codebase uses
    // for a legitimate drawable, so its appearance on screen is unambiguous.
    // PROVISIONAL: the exact RGB triple is a free implementer pick within
    // those constraints; only its conspicuousness is load-bearing.
    private const int PlaceholderRed = 255;
    private const int PlaceholderGreen = 0;
    private const int PlaceholderBlue = 255;

    /// <summary>
    /// The fixed, conspicuous color drawn for
    /// <see cref="VisualFallbackStep.DiagnosticPlaceholder"/>. Never derived
    /// from a theme, never invisible.
    /// </summary>
    internal static readonly Color PlaceholderColor =
        new(PlaceholderRed, PlaceholderGreen, PlaceholderBlue);

    /// <summary>
    /// Walks the four-step chain and returns the first step that yields a
    /// non-null, valid entry, falling through to the diagnostic placeholder
    /// when none does. Total: no combination of delegate results causes this
    /// method to throw.
    /// </summary>
    /// <typeparam name="T">The domain's catalog entry type.</typeparam>
    /// <param name="resolveSpecificVariant">
    /// Step 1: the selection recipe's chosen entry, or <see langword="null"/>
    /// when the requested index or identifier has no entry.
    /// </param>
    /// <param name="resolveFamilyDefault">
    /// Step 2: the family's index-0 default entry, or <see langword="null"/>
    /// when the family itself is empty or undefined.
    /// </param>
    /// <param name="resolveModelCategoryDefault">
    /// Step 3: the generic drawable for the model category, or
    /// <see langword="null"/> when even that is unavailable.
    /// </param>
    /// <param name="isValid">
    /// Additional per-entry validity check applied at every step (for
    /// example, catalog startup validation failure); a non-null entry that
    /// fails this check is treated the same as a null one.
    /// </param>
    internal static VisualResolution<T> Resolve<T>(
        Func<T?> resolveSpecificVariant,
        Func<T?> resolveFamilyDefault,
        Func<T?> resolveModelCategoryDefault,
        Func<T, bool> isValid)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(resolveSpecificVariant);
        ArgumentNullException.ThrowIfNull(resolveFamilyDefault);
        ArgumentNullException.ThrowIfNull(resolveModelCategoryDefault);
        ArgumentNullException.ThrowIfNull(isValid);

        var specificVariant = resolveSpecificVariant();
        if (specificVariant is not null && isValid(specificVariant))
        {
            return new VisualResolution<T>(VisualFallbackStep.SpecificVariant, specificVariant);
        }

        var familyDefault = resolveFamilyDefault();
        if (familyDefault is not null && isValid(familyDefault))
        {
            return new VisualResolution<T>(VisualFallbackStep.FamilyDefault, familyDefault);
        }

        var modelCategoryDefault = resolveModelCategoryDefault();
        if (modelCategoryDefault is not null && isValid(modelCategoryDefault))
        {
            return new VisualResolution<T>(VisualFallbackStep.ModelCategoryDefault, modelCategoryDefault);
        }

        return new VisualResolution<T>(VisualFallbackStep.DiagnosticPlaceholder, null);
    }
}
