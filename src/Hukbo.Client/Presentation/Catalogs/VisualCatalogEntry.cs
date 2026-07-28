using System.Text.RegularExpressions;

namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// The zoom level at which a catalog entry's silhouette contribution first
/// draws, aligned to the detail-tier switch in
/// <see cref="Hukbo.Client.Rendering.PawnDetailTier"/> (visual-system-
/// integration-design.md section 7). A pure presentation concept: the
/// simulation never asks what tier the camera is at.
/// </summary>
internal enum VisualDetailTier
{
    Low,
    Medium,
    High,
}

/// <summary>
/// The cultural scope a catalog entry claims, or the absence of one
/// (visual-system-integration-design.md section 2, R-X.8 rule 10). Every
/// entry that depicts a specific culture carries one of the three named
/// regions or the shared generic pool; an entry that makes no cultural claim
/// at all — a condition tint, for example — carries
/// <see cref="VisualScopeTag.NotApplicable"/> rather than a false-precision
/// guess.
/// </summary>
internal enum VisualScopeTag
{
    NotApplicable,
    Visayan,
    Tagalog,
    Cagayan,
    UnscopedGeneric,
}

/// <summary>
/// The identifier grammar every <see cref="VisualCatalogEntry.Id"/> must
/// satisfy (visual-system-integration-design.md section 2, RF-05
/// resolution): dotted <c>&lt;domain&gt;.&lt;family&gt;.&lt;variant&gt;</c>
/// segments, camelCase within a segment, with an optional literal
/// <c>tint.</c> sub-segment between family and variant for presentation-only
/// tint variants. Domains are <c>weapon</c>, <c>shield</c>,
/// <c>appearance</c>, and <c>backdrop</c>. Identifiers are machine keys in
/// the <c>LogEvents</c> spirit — do not renumber or reword a shipped one,
/// because it is pinned forever the moment a catalog ships it.
/// </summary>
internal static partial class VisualCatalogGrammar
{
    /// <summary>
    /// True when <paramref name="id"/> satisfies the identifier grammar
    /// above. Examples that match: <c>weapon.kalis.l1</c>,
    /// <c>weapon.kampilan.tint.freshIron</c>,
    /// <c>shield.tallHardwood.mactanThin</c>,
    /// <c>appearance.headcovering.putong</c>, <c>backdrop.grass.cluster</c>.
    /// </summary>
    public static bool IsWellFormedId(string id) =>
        id is not null && IdPattern().IsMatch(id);

    [GeneratedRegex(
        @"^(weapon|shield|appearance|backdrop)\.[a-z][a-zA-Z0-9]*(\.tint)?\.[a-z][a-zA-Z0-9]*$")]
    private static partial Regex IdPattern();
}

/// <summary>
/// The shared, immutable shape every visual catalog entry takes, following
/// the <c>PawnAppearance</c> precedent: presentation metadata lives in the
/// client, never in Core (visual-system-integration-design.md section 2,
/// R-W6.1, R-X.7). Sibling designs add domain fields — tint values, layout
/// offsets, component category — by composition, wrapping or extending this
/// type; none of the seven fields here is ever removed or repurposed.
/// </summary>
/// <param name="Id">
/// The stable string identifier (<see cref="VisualCatalogGrammar"/>), pinned
/// forever once shipped.
/// </param>
/// <param name="Index">
/// The stable ordinal used by modulo selection, pinned forever once shipped.
/// </param>
/// <param name="DisplayLabel">
/// Player-facing text, pair form where a cultural identification is made
/// (R-X.6) — the Filipino name, an em dash, and a plain English descriptor.
/// </param>
/// <param name="EvidenceTier">How far the evidence behind this entry reaches.</param>
/// <param name="ScopeTag">
/// The cultural scope this entry claims, or <see cref="VisualScopeTag.NotApplicable"/>
/// where the entry depicts no culture.
/// </param>
/// <param name="Notes">
/// The evidence note or source anchor surfaced by the inspector (a
/// <c>Mactan — 1521</c> style citation, R-X.10).
/// </param>
/// <param name="MinimumDetailTier">
/// The tier at which this entry's silhouette contribution first draws.
/// </param>
internal sealed record VisualCatalogEntry(
    string Id,
    int Index,
    string DisplayLabel,
    VisualEvidenceTier EvidenceTier,
    VisualScopeTag ScopeTag,
    string Notes,
    VisualDetailTier MinimumDetailTier)
{
    // Positional-record property re-declarations: each initializer runs once,
    // against the primary constructor parameter of the same name, on every
    // `new VisualCatalogEntry(...)` call. A `with` expression uses the
    // compiler-generated copy constructor instead and never re-runs these
    // initializers, which is exactly the behavior VisualCatalogEntryTests.cs
    // pins via its `Create` factory comment above.
    public string Id { get; init; } = ValidateId(Id);

    public int Index { get; init; } = ValidateIndex(Index);

    public string DisplayLabel { get; init; } = ValidateDisplayLabel(DisplayLabel);

    public VisualEvidenceTier EvidenceTier { get; init; } = ValidateEvidenceTier(EvidenceTier);

    public VisualScopeTag ScopeTag { get; init; } = ValidateScopeTag(ScopeTag);

    public string Notes { get; init; } = Notes ?? throw new ArgumentNullException(nameof(Notes));

    public VisualDetailTier MinimumDetailTier { get; init; } = ValidateMinimumDetailTier(MinimumDetailTier);

    private static string ValidateId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!VisualCatalogGrammar.IsWellFormedId(id))
        {
            throw new ArgumentException(
                $"'{id}' does not match the visual catalog identifier " +
                "grammar '<domain>.<family>[.tint].<variant>'.",
                nameof(id));
        }

        return id;
    }

    private static int ValidateIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return index;
    }

    private static string ValidateDisplayLabel(string displayLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayLabel);
        return displayLabel;
    }

    private static VisualEvidenceTier ValidateEvidenceTier(VisualEvidenceTier evidenceTier)
    {
        if (!Enum.IsDefined(evidenceTier))
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidenceTier),
                evidenceTier,
                "Evidence tier must be a defined VisualEvidenceTier member.");
        }

        return evidenceTier;
    }

    private static VisualScopeTag ValidateScopeTag(VisualScopeTag scopeTag)
    {
        if (!Enum.IsDefined(scopeTag))
        {
            throw new ArgumentOutOfRangeException(
                nameof(scopeTag),
                scopeTag,
                "Scope tag must be a defined VisualScopeTag member.");
        }

        return scopeTag;
    }

    private static VisualDetailTier ValidateMinimumDetailTier(VisualDetailTier minimumDetailTier)
    {
        if (!Enum.IsDefined(minimumDetailTier))
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumDetailTier),
                minimumDetailTier,
                "Minimum detail tier must be a defined VisualDetailTier member.");
        }

        return minimumDetailTier;
    }
}
