namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// The minimal <c>backdrop.*</c> catalog declaration (battlefield-
/// environment-design.md; implementation-plan-draft.md VIS-025, giving the
/// RF-10 resolution of R-W4.9's labelling obligation a real artifact). The
/// battlefield ground — grid shading and the grass clusters
/// <see cref="Hukbo.Client.Rendering.GrassGeometry"/> places — depicts
/// generic open ground with no historical claim about a specific vegetation,
/// region, or land use. This single entry is that claim's home: one
/// <see cref="VisualCatalogEntry"/> in the <c>backdrop.*</c> identifier
/// domain, evidence tier <see cref="VisualEvidenceTier.ProvisionalReconstruction"/>,
/// reusing the shared catalog entry shape sibling designs use for weapons,
/// shields, and appearance so the agent inspector can surface it the same
/// way. No player-facing text anywhere in this declaration names a
/// vegetation, a region, or a land use.
/// </summary>
internal static class BackdropVisualCatalog
{
    /// <summary>
    /// The sole backdrop catalog entry. <c>backdrop.grass.cluster</c> is
    /// pinned — the package's catalog-grammar tests already exercise this
    /// exact identifier as a grammar-acceptance case.
    /// </summary>
    public static readonly VisualCatalogEntry GrassCluster = new(
        Id: "backdrop.grass.cluster",
        Index: 0,
        DisplayLabel: "Open ground",
        EvidenceTier: VisualEvidenceTier.ProvisionalReconstruction,
        ScopeTag: VisualScopeTag.NotApplicable,
        Notes: "Ground cover technique only; no historical claim is made " +
            "about the depicted vegetation, region, or land use.",
        MinimumDetailTier: VisualDetailTier.Low);

    /// <summary>
    /// Every declared backdrop entry. A one-element list today; sibling
    /// backdrop treatments (if any ship later) register here alongside
    /// <see cref="GrassCluster"/>, never replacing it.
    /// </summary>
    public static IReadOnlyList<VisualCatalogEntry> All { get; } =
    [
        GrassCluster,
    ];
}
