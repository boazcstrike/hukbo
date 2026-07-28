using Hukbo.Client.Presentation;

namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// The evidence tier a visual catalog entry carries. Extends
/// <see cref="WeaponEvidenceTier"/> with the explicit "no historical claim at
/// all" marker that a condition tint, a generic silhouette recolor, or any
/// other purely cosmetic choice needs (R-X.7). The first three members mirror
/// <see cref="WeaponEvidenceTier"/> name-for-name and value-for-value and
/// must stay in that order; <see cref="WeaponEvidenceTier"/> itself is not
/// touched by this file — see visual-system-integration-design.md section 2.
/// </summary>
public enum VisualEvidenceTier
{
    /// <summary>
    /// A contemporary source records the term, or the depicted subject,
    /// itself. Mirrors <see cref="WeaponEvidenceTier.Documented"/>.
    /// </summary>
    Documented,

    /// <summary>
    /// The subject is attested in the period; the exact form, or the
    /// attachment of this particular name to it, is not. Mirrors
    /// <see cref="WeaponEvidenceTier.DocumentedFormUncertain"/>.
    /// </summary>
    DocumentedFormUncertain,

    /// <summary>
    /// A reasoned reconstruction that no consulted source confirms for the
    /// period. Mirrors <see cref="WeaponEvidenceTier.ProvisionalReconstruction"/>.
    /// </summary>
    ProvisionalReconstruction,

    /// <summary>
    /// No historical claim is made at all — a condition tint, a generic
    /// recolor, or any other entry that exists purely for visual variety.
    /// This member has no counterpart in <see cref="WeaponEvidenceTier"/>;
    /// it is the marker that lets a catalog entry say so honestly instead of
    /// borrowing one of the three historical tiers for a claim it is not
    /// making.
    /// </summary>
    PresentationOnly,
}
