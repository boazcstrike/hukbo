namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// One catalog entry's metadata exactly as the startup validator inspects
/// it (visual-system-integration-design.md section 2): every field of the
/// shared <see cref="VisualCatalogEntry"/> shape, but nullable. A real,
/// shipped <see cref="VisualCatalogEntry"/> can never populate this type
/// with a missing field — its own constructor already rejects a blank ID, an
/// undefined enum member, or a null notes string before the object exists —
/// so <see cref="FromCatalogEntry"/> is the only path a production catalog
/// takes, and it always produces a fully populated value. The nullability
/// exists so a deliberately invalid in-memory catalog built by a test can
/// express "this field is simply absent", which is exactly the class of bug
/// this validator exists to catch in a future data-driven catalog even
/// though no catalog shipped today can trigger it
/// (implementation-plan-draft.md VIS-006: "shipped catalogs are code and
/// cannot fail these checks at runtime").
/// </summary>
/// <param name="Id">The candidate identifier, or <see langword="null"/> when absent.</param>
/// <param name="Index">The candidate stable ordinal.</param>
/// <param name="DisplayLabel">The candidate player-facing label, or <see langword="null"/> when absent.</param>
/// <param name="EvidenceTier">The candidate evidence tier, or <see langword="null"/> when absent.</param>
/// <param name="ScopeTag">The candidate scope tag, or <see langword="null"/> when absent.</param>
/// <param name="Notes">The candidate evidence note, or <see langword="null"/> when absent.</param>
/// <param name="MinimumDetailTier">The candidate minimum detail tier, or <see langword="null"/> when absent.</param>
internal readonly record struct VisualCatalogValidationEntry(
    string? Id,
    int Index,
    string? DisplayLabel,
    VisualEvidenceTier? EvidenceTier,
    VisualScopeTag? ScopeTag,
    string? Notes,
    VisualDetailTier? MinimumDetailTier)
{
    /// <summary>
    /// Converts a real, already-validated <see cref="VisualCatalogEntry"/>
    /// losslessly — every field below is populated, because the source
    /// entry's own constructor guaranteed as much.
    /// </summary>
    public static VisualCatalogValidationEntry FromCatalogEntry(VisualCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new VisualCatalogValidationEntry(
            entry.Id,
            entry.Index,
            entry.DisplayLabel,
            entry.EvidenceTier,
            entry.ScopeTag,
            entry.Notes,
            entry.MinimumDetailTier);
    }
}

/// <summary>
/// One combination-rule failure a per-catalog rule delegate reports
/// (visual-system-integration-design.md section 2, "the combination rules
/// that apply to it" — the warrior-appearance validator of R-W3.4 is the
/// worked example this hook exists for). <see cref="Reason"/> is a stable
/// reason code, never prose, matching the discipline
/// <see cref="VisualDiagnostics.ReportCatalogInvalid"/> already enforces for
/// its own <c>reason</c> parameter; free prose belongs in
/// <see cref="Message"/> only.
/// </summary>
internal readonly record struct VisualCatalogCombinationViolation(
    string Reason,
    string? Message = null);

/// <summary>
/// A per-catalog combination rule (visual-system-integration-design.md
/// section 2): domain knowledge <see cref="VisualCatalogValidator"/> itself
/// does not have, supplied by the catalog's own wiring. Returns
/// <see langword="null"/> when <paramref name="entry"/> satisfies the rule,
/// or a <see cref="VisualCatalogCombinationViolation"/> describing how it
/// does not.
/// </summary>
internal delegate VisualCatalogCombinationViolation? VisualCatalogCombinationRule(
    VisualCatalogValidationEntry entry);

/// <summary>
/// One structural or combination-rule failure <see cref="VisualCatalogValidator.Validate"/>
/// found. <see cref="Reason"/> is one of the <c>Reason*</c> constants on
/// <see cref="VisualCatalogValidator"/> or a reason code a
/// <see cref="VisualCatalogCombinationRule"/> supplied — always a stable
/// code, never a sentence, matching the <c>reason</c> parameter
/// <see cref="VisualDiagnostics.ReportCatalogInvalid"/> logs.
/// </summary>
/// <param name="EntryId">
/// The offending entry's identifier, or a fixed placeholder
/// (<see cref="VisualCatalogValidator.UnknownEntryId"/>) when the entry has
/// no usable identifier at all.
/// </param>
/// <param name="Reason">The stable reason code.</param>
/// <param name="Message">Optional free-text detail for a human reader.</param>
internal readonly record struct VisualCatalogFailure(
    string EntryId,
    string Reason,
    string? Message = null);

/// <summary>
/// The outcome of one <see cref="VisualCatalogValidator.Validate"/> call:
/// every failure found, plus which specific entry identifiers are now
/// "known invalid" so a fallback lookup can skip straight past them
/// (visual-system-integration-design.md section 4 — a failure here routes
/// resolution to <see cref="VisualFallbackStep.FamilyDefault"/>, the same
/// way any other invalid specific-variant candidate does, via
/// <see cref="VisualFallbackResolver.Resolve{T}"/>'s own <c>isValid</c>
/// parameter). <see cref="IsEntryValid"/> is exactly the predicate a caller
/// wires into that parameter.
/// </summary>
internal sealed class VisualCatalogValidationResult
{
    private readonly HashSet<string> _invalidEntryIds;

    internal VisualCatalogValidationResult(
        string catalogId,
        IReadOnlyList<VisualCatalogFailure> failures)
    {
        CatalogId = catalogId;
        Failures = failures;
        _invalidEntryIds = new HashSet<string>(
            failures.Select(failure => failure.EntryId),
            StringComparer.Ordinal);
    }

    /// <summary>Which catalog this result belongs to.</summary>
    public string CatalogId { get; }

    /// <summary>
    /// Every failure found, in a stable order (entry identifier, then reason
    /// code, both ordinal) so a test asserting against this list never
    /// depends on dictionary or set iteration order.
    /// </summary>
    public IReadOnlyList<VisualCatalogFailure> Failures { get; }

    /// <summary>True when <see cref="Failures"/> is empty.</summary>
    public bool IsValid => Failures.Count == 0;

    /// <summary>
    /// False when <paramref name="entryId"/> is the subject of at least one
    /// recorded failure — the predicate a fallback resolver's <c>isValid</c>
    /// delegate wires in directly so a failed entry is treated exactly like
    /// a missing one.
    /// </summary>
    public bool IsEntryValid(string entryId) =>
        entryId is not null && !_invalidEntryIds.Contains(entryId);
}

/// <summary>
/// The once-at-load Client validation pass in the <c>UiThemeCatalog</c>
/// style (implementation-plan-draft.md VIS-006; visual-system-integration-
/// design.md section 2): identifier uniqueness, per-family index
/// uniqueness, mandatory metadata presence, and whatever additional
/// per-catalog combination rule the caller supplies. Pure and total — never
/// throws for malformed input data, only reports it as a
/// <see cref="VisualCatalogFailure"/> — because a validator that can itself
/// crash the game defeats the point of validating before the game trusts the
/// data (section 4: "never crash, never silently drop an entry"). The
/// caller (<c>ArenaGame.LoadContent</c>) owns turning a non-empty result
/// into a logged <see cref="VisualDiagnostics.ReportCatalogInvalid"/> line;
/// this type never touches <see cref="Hukbo.Diagnostics.DiagnosticLog"/>
/// itself, keeping it constructible and testable without one.
/// </summary>
internal static class VisualCatalogValidator
{
    /// <summary>
    /// The placeholder <see cref="VisualCatalogFailure.EntryId"/> for an
    /// entry that has no usable identifier of its own to report.
    /// </summary>
    internal const string UnknownEntryId = "<unknown>";

    /// <summary>Two or more entries in the catalog share the same <see cref="VisualCatalogValidationEntry.Id"/>.</summary>
    public const string ReasonDuplicateId = "duplicateId";

    /// <summary>
    /// Two or more entries in the same identifier family (the <c>domain.family</c>
    /// prefix of <see cref="VisualCatalogValidationEntry.Id"/>) share the same
    /// <see cref="VisualCatalogValidationEntry.Index"/>. Named for the design's
    /// "index contiguity" check: a collision here is exactly what breaks a
    /// family's index sequence, leaving some other value in that sequence
    /// unclaimed. A merely <em>skipped</em> value with no collision is not a
    /// failure — this catalog's own convention deliberately reserves index
    /// slots for research options a design excludes on purpose (for example
    /// <c>AppearanceComponentCatalog</c>'s C2 and I3), and reusing that
    /// convention costs nothing at runtime because selection is by the
    /// catalog's pinned entry <em>count</em>, never by indexing an array with
    /// the raw <see cref="VisualCatalogValidationEntry.Index"/> value
    /// (visual-system-integration-design.md section 3).
    /// </summary>
    public const string ReasonIndexGap = "indexGap";

    /// <summary><see cref="VisualCatalogValidationEntry.Id"/> is null, empty, or whitespace.</summary>
    public const string ReasonMissingId = "missingId";

    /// <summary><see cref="VisualCatalogValidationEntry.DisplayLabel"/> is null, empty, or whitespace.</summary>
    public const string ReasonMissingDisplayLabel = "missingDisplayLabel";

    /// <summary><see cref="VisualCatalogValidationEntry.Notes"/> is null.</summary>
    public const string ReasonMissingNotes = "missingNotes";

    /// <summary>
    /// <see cref="VisualCatalogValidationEntry.EvidenceTier"/> is null or not
    /// a defined <see cref="VisualEvidenceTier"/> member.
    /// </summary>
    public const string ReasonMissingEvidenceTier = "missingEvidenceTier";

    /// <summary>
    /// <see cref="VisualCatalogValidationEntry.ScopeTag"/> is null or not a
    /// defined <see cref="VisualScopeTag"/> member. Structural presence only
    /// — whether a specific entry's evidence tier obligates a scope tag
    /// <em>other than</em> <see cref="VisualScopeTag.NotApplicable"/> ("scope
    /// tag on every cultural entry") is domain knowledge this validator does
    /// not have; that rule is exactly what a
    /// <see cref="VisualCatalogCombinationRule"/> is for.
    /// </summary>
    public const string ReasonMissingScopeTag = "missingScopeTag";

    /// <summary>
    /// <see cref="VisualCatalogValidationEntry.MinimumDetailTier"/> is null
    /// or not a defined <see cref="VisualDetailTier"/> member.
    /// </summary>
    public const string ReasonMissingDetailTier = "missingDetailTier";

    /// <summary>
    /// Validates a catalog already expressed as the shared
    /// <see cref="VisualCatalogEntry"/> shape — the common case for every
    /// catalog shipped today.
    /// </summary>
    public static VisualCatalogValidationResult Validate(
        string catalogId,
        IReadOnlyList<VisualCatalogEntry> entries,
        VisualCatalogCombinationRule? combinationRule = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var mapped = new VisualCatalogValidationEntry[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            mapped[i] = VisualCatalogValidationEntry.FromCatalogEntry(entries[i]);
        }

        return Validate(catalogId, mapped, combinationRule);
    }

    /// <summary>
    /// Validates any catalog implementing the shared entry shape, expressed
    /// as <see cref="VisualCatalogValidationEntry"/> so a deliberately
    /// invalid in-memory catalog can exercise every failure class,
    /// including ones a real <see cref="VisualCatalogEntry"/> can never
    /// reach on its own.
    /// </summary>
    public static VisualCatalogValidationResult Validate(
        string catalogId,
        IReadOnlyList<VisualCatalogValidationEntry> entries,
        VisualCatalogCombinationRule? combinationRule = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogId);
        ArgumentNullException.ThrowIfNull(entries);

        var failures = new List<VisualCatalogFailure>();
        var idOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var indexOccurrencesByFamily =
            new Dictionary<string, Dictionary<int, List<string>>>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            var entryId = string.IsNullOrWhiteSpace(entry.Id) ? UnknownEntryId : entry.Id;

            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                failures.Add(new VisualCatalogFailure(entryId, ReasonMissingId));
            }
            else
            {
                idOccurrences[entry.Id] = idOccurrences.GetValueOrDefault(entry.Id) + 1;

                var family = ExtractFamily(entry.Id);
                var indexesInFamily = indexOccurrencesByFamily.TryGetValue(family, out var existing)
                    ? existing
                    : indexOccurrencesByFamily[family] = new Dictionary<int, List<string>>();
                var idsAtIndex = indexesInFamily.TryGetValue(entry.Index, out var list)
                    ? list
                    : indexesInFamily[entry.Index] = new List<string>();
                idsAtIndex.Add(entry.Id);
            }

            if (string.IsNullOrWhiteSpace(entry.DisplayLabel))
            {
                failures.Add(new VisualCatalogFailure(entryId, ReasonMissingDisplayLabel));
            }

            if (entry.Notes is null)
            {
                failures.Add(new VisualCatalogFailure(entryId, ReasonMissingNotes));
            }

            if (entry.EvidenceTier is not { } evidenceTier || !Enum.IsDefined(evidenceTier))
            {
                failures.Add(new VisualCatalogFailure(entryId, ReasonMissingEvidenceTier));
            }

            if (entry.ScopeTag is not { } scopeTag || !Enum.IsDefined(scopeTag))
            {
                failures.Add(new VisualCatalogFailure(entryId, ReasonMissingScopeTag));
            }

            if (entry.MinimumDetailTier is not { } minimumDetailTier || !Enum.IsDefined(minimumDetailTier))
            {
                failures.Add(new VisualCatalogFailure(entryId, ReasonMissingDetailTier));
            }

            if (combinationRule is not null &&
                combinationRule(entry) is { } violation)
            {
                failures.Add(new VisualCatalogFailure(entryId, violation.Reason, violation.Message));
            }
        }

        foreach (var (id, count) in idOccurrences)
        {
            if (count > 1)
            {
                failures.Add(new VisualCatalogFailure(id, ReasonDuplicateId));
            }
        }

        foreach (var indexesInFamily in indexOccurrencesByFamily.Values)
        {
            foreach (var idsAtIndex in indexesInFamily.Values)
            {
                if (idsAtIndex.Count <= 1)
                {
                    continue;
                }

                foreach (var id in idsAtIndex)
                {
                    failures.Add(new VisualCatalogFailure(
                        id,
                        ReasonIndexGap,
                        $"index shared with {idsAtIndex.Count - 1} other " +
                        (idsAtIndex.Count - 1 == 1 ? "entry" : "entries") +
                        " in the same identifier family"));
                }
            }
        }

        // Stable order — never dictionary or set iteration order — so a test
        // (and a log reader) can rely on a fixed sequence.
        var ordered = failures
            .OrderBy(failure => failure.EntryId, StringComparer.Ordinal)
            .ThenBy(failure => failure.Reason, StringComparer.Ordinal)
            .ToArray();

        return new VisualCatalogValidationResult(catalogId, ordered);
    }

    /// <summary>
    /// The <c>domain.family</c> prefix of a well-formed catalog identifier
    /// (its first two dotted segments), the grouping key index uniqueness is
    /// checked within. Falls back to the whole identifier for a malformed
    /// one with fewer than two segments — that shape is already reported
    /// separately by <see cref="VisualCatalogGrammar.IsWellFormedId"/>-driven
    /// checks elsewhere; this method only needs a stable, non-throwing key.
    /// </summary>
    private static string ExtractFamily(string id)
    {
        var parts = id.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : id;
    }
}
