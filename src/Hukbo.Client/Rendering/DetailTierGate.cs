using Hukbo.Client.Presentation.Catalogs;

namespace Hukbo.Client.Rendering;

/// <summary>
/// The single tier-gating decision every visual catalog consumer calls
/// instead of re-deriving it: whether an entry whose silhouette contribution
/// starts at <see cref="VisualDetailTier"/> should draw at a given apparent
/// scale (visual-system-integration-design.md section 7, R-X.4).
/// </summary>
/// <remarks>
/// The thresholds mirror the existing pawn tier switch in
/// <see cref="PawnGeometry"/> exactly — Low below 0.95, Medium below 1.80,
/// High at or above 1.80 — so a new catalog entry classifies at precisely
/// the same apparent-scale boundary an existing pawn already does. Those
/// thresholds are read-only ground truth for this helper: this type never
/// changes them, and <see cref="PawnGeometry"/> is never edited to expose
/// them, so the two stay independently pinned by test rather than sharing
/// mutable state.
/// </remarks>
internal static class DetailTierGate
{
    private const float MediumDetailScale = 0.95f;
    private const float HighDetailScale = 1.80f;

    /// <summary>
    /// True when <paramref name="apparentScale"/> classifies at a tier equal
    /// to or more detailed than <paramref name="minimumDetailTier"/>, so the
    /// caller should draw the entry's contribution this frame.
    /// </summary>
    public static bool ShouldDraw(float apparentScale, VisualDetailTier minimumDetailTier) =>
        ClassifyTier(apparentScale) >= minimumDetailTier;

    private static VisualDetailTier ClassifyTier(float apparentScale) =>
        apparentScale switch
        {
            < MediumDetailScale => VisualDetailTier.Low,
            < HighDetailScale => VisualDetailTier.Medium,
            _ => VisualDetailTier.High,
        };
}
