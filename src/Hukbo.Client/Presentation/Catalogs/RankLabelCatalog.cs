using Hukbo.Core.Combat;

namespace Hukbo.Client.Presentation.Catalogs;

/// <summary>
/// One rank label catalog entry: the player-facing pair-form label (CLAUDE.md
/// section 7 — a cultural identification never appears without its plain
/// English descriptor), the region(s) the term is attested in, the evidence
/// tier behind it, and a source note. Mirrors the shape and validation
/// conventions <see cref="VisualCatalogEntry"/> already establishes for the
/// weapon, shield, and appearance catalogs — pair-form label,
/// <see cref="VisualEvidenceTier"/>, a notes/citation field — but is its own
/// type rather than a literal reuse of that record: <see cref="VisualCatalogGrammar"/>
/// restricts <see cref="VisualCatalogEntry.Id"/> to the
/// <c>weapon</c>/<c>shield</c>/<c>appearance</c>/<c>backdrop</c> domains, and
/// <see cref="VisualScopeTag"/> has no member for a rank whose region spans
/// two named peoples (<c>Datu</c> — "Tagalog and Visayan"), so <see cref="Region"/>
/// is a free-form string here instead.
/// </summary>
/// <param name="Rank">The <see cref="RankId"/> this entry describes.</param>
/// <param name="Label">
/// Player-facing pair-form text — the Filipino name, an em dash, and a plain
/// English descriptor.
/// </param>
/// <param name="Region">
/// The historical region(s) this term is attested in, as free text (for
/// example "Tagalog and Visayan", "Tagalog", or "Visayan").
/// </param>
/// <param name="EvidenceTier">How far the evidence behind this entry reaches.</param>
/// <param name="Notes">The evidence note or source citation shown by the inspector.</param>
/// <param name="ReconstructionNote">
/// <see langword="null"/> for every rank except
/// <see cref="RankId.AlipingNamamahay"/>. That roster entry fields a
/// household-dependent class Plasencia records with maritime and
/// agricultural obligations, not an armed one; whether a householder was
/// ever put in a battle line is an inference either way, not an attested
/// fact, and <c>docs/research/HISTORICAL_1500s_RANKS.md</c> requires this
/// said wherever the class is shown. This field is that statement, carried
/// on the catalog entry so every surface that shows the rank shows the same
/// wording rather than each hand-writing its own.
/// </param>
internal sealed record RankLabelEntry(
    RankId Rank,
    string Label,
    string Region,
    VisualEvidenceTier EvidenceTier,
    string Notes,
    string? ReconstructionNote = null)
{
    // Positional-record property re-declarations, mirroring VisualCatalogEntry's
    // own pattern: each initializer runs once, against the primary constructor
    // parameter of the same name, and never re-runs on a `with` expression.
    public RankId Rank { get; init; } = ValidateRank(Rank);

    public string Label { get; init; } = ValidateNonEmpty(Label, nameof(Label));

    public string Region { get; init; } = ValidateNonEmpty(Region, nameof(Region));

    public VisualEvidenceTier EvidenceTier { get; init; } =
        ValidateEvidenceTier(EvidenceTier);

    public string Notes { get; init; } =
        Notes ?? throw new ArgumentNullException(nameof(Notes));

    private static RankId ValidateRank(RankId rank)
    {
        if (!Enum.IsDefined(rank))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rank),
                rank,
                "Rank must be a defined RankId member.");
        }

        return rank;
    }

    private static string ValidateNonEmpty(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    private static VisualEvidenceTier ValidateEvidenceTier(VisualEvidenceTier tier)
    {
        if (!Enum.IsDefined(tier))
        {
            throw new ArgumentOutOfRangeException(
                nameof(tier),
                tier,
                "Evidence tier must be a defined VisualEvidenceTier member.");
        }

        return tier;
    }
}

/// <summary>
/// The rank label catalog: one <see cref="RankLabelEntry"/> per
/// <see cref="RankId"/> value, carrying the player-facing pair-form label,
/// region, evidence tier, and source note for
/// <c>docs/research/HISTORICAL_1500s_RANKS.md</c>'s five cleared terms.
/// <see cref="RankId"/> is never a delegated military office (see the
/// enum's own remarks) — this catalog labels social and legal standing
/// only. <see cref="Ayuey"/> is declared, matching
/// the enum, even though no registered <c>CombatPresetRegistry</c> preset
/// rosters it, on the same "declared but unreachable" principle
/// <c>PhilippineCombatPresetV3</c> already uses for its unrostered paired
/// weapon profiles.
/// </summary>
internal static class RankLabelCatalog
{
    /// <summary>
    /// Datu — Chief. Documented. Plasencia, 1589: the best-attested term,
    /// recorded verbatim including the war obligation owed to a chief by
    /// those below him. The descriptor deliberately avoids "noble" (which
    /// carries Plasencia's Spanish gloss rather than its content) and the
    /// modern "royalty" misreading entirely.
    /// </summary>
    public static readonly RankLabelEntry Datu = new(
        RankId.Datu,
        "Datu — Chief",
        "Tagalog and Visayan",
        VisualEvidenceTier.Documented,
        "Plasencia, 1589. The best-attested rank term, recorded verbatim " +
        "including the war obligation owed to a chief by those below him. " +
        "\"Chief\" avoids Plasencia's Spanish gloss \"noble\" and the " +
        "modern \"royalty\" misreading.");

    /// <summary>
    /// Maharlika — Sworn Freeman. Documented. Plasencia, 1589: free-born,
    /// exempt from tribute, but obligated to accompany the datu in war at
    /// their own expense, in exchange for a feast beforehand and a share of
    /// the spoils afterward. The modern popularized reading of "maharlika"
    /// as nobility or royalty is a twentieth-century development Plasencia's
    /// own text does not support and this catalog does not use.
    /// </summary>
    public static readonly RankLabelEntry Maharlika = new(
        RankId.Maharlika,
        "Maharlika — Sworn Freeman",
        "Tagalog",
        VisualEvidenceTier.Documented,
        "Plasencia, 1589. Free-born and exempt from tribute, but obligated " +
        "to accompany the datu in war at their own expense, for a feast " +
        "beforehand and a share of the spoils after. Not nobility or " +
        "royalty — a twentieth-century reading Plasencia's own text does " +
        "not support.");

    /// <summary>
    /// Timawa — Bound Freeman. Documented. Loarca, 1582, spelled
    /// <c>timagua</c>: a freeman sworn to a chosen chief, obligated to carry
    /// weapons and accompany him, in exchange for the chief's reciprocal
    /// obligation to defend him; the bond was transferable. Must not be used
    /// for the Tagalog region — Morga's <c>timagua</c> means plebeian
    /// commoner, a different sense from Loarca's sworn fighting client.
    /// </summary>
    public static readonly RankLabelEntry Timawa = new(
        RankId.Timawa,
        "Timawa — Bound Freeman",
        "Visayan",
        VisualEvidenceTier.Documented,
        "Loarca, 1582 (\"timagua\"). A freeman sworn to a chosen chief, " +
        "obligated to carry weapons and accompany him, in exchange for the " +
        "chief's reciprocal obligation to defend him; the bond was " +
        "transferable. Not interchangeable with Morga's Tagalog " +
        "\"timagua\", which means plebeian commoner.");

    /// <summary>
    /// Aliping Namamahay — Householder. Documented. Plasencia, 1589: married
    /// dependents who held their own houses, land, and gold, and could not
    /// be sold or moved from their village. The player-facing label uses
    /// Plasencia's full attested term rather than the shortened
    /// "Namamahay" that <c>docs/research/HISTORICAL_1500s_RANKS.md</c>'s
    /// cleared-terms table lists. That was a deliberate decision taken on
    /// 2026-07-29 and recorded in
    /// <c>docs/archives/2026-08-07/2026-07-29-warrior-standing-design.md</c>: the fuller
    /// form is what the source actually says, and shortening it drops the
    /// <em>alipin</em> element that places the class in the dependent
    /// family at all. The descriptor still avoids the Spanish "slave"
    /// gloss, which is a translation rather than Plasencia's content.
    /// Carries <see cref="RankLabelEntry.ReconstructionNote"/> because the
    /// roster fields this class in a battle line and Plasencia records
    /// maritime and agricultural service for it, not an armed one.
    /// </summary>
    public static readonly RankLabelEntry AlipingNamamahay = new(
        RankId.AlipingNamamahay,
        "Aliping Namamahay — Householder",
        "Tagalog",
        VisualEvidenceTier.Documented,
        "Plasencia, 1589. Married dependents who held their own houses, " +
        "land, and gold, and could not be sold or moved from their " +
        "village.",
        ReconstructionNote:
            "Reconstruction: Plasencia records maritime and agricultural " +
            "service for this class, not an armed one. Whether a " +
            "householder was ever put in a battle line is an inference " +
            "either way, not an attested fact.");

    /// <summary>
    /// Ayuey — Household Dependent. Documented, form uncertain. Loarca,
    /// 1582: the most dependent of his three recorded grades — fed and
    /// clothed by the master, working three days in four for him. The
    /// institution is attested; the spelling is Loarca's own transcription
    /// and its modern orthography is unsettled. Declared here, matching
    /// <see cref="RankId.Ayuey"/>, but not rostered by any preset.
    /// </summary>
    public static readonly RankLabelEntry Ayuey = new(
        RankId.Ayuey,
        "Ayuey — Household Dependent",
        "Visayan",
        VisualEvidenceTier.DocumentedFormUncertain,
        "Loarca, 1582. The most dependent of his three recorded grades — " +
        "fed and clothed by the master, working three days in four for " +
        "him. The institution is attested; the spelling is Loarca's own " +
        "transcription and its modern orthography is unsettled.");

    private static readonly IReadOnlyList<RankLabelEntry> All =
    [
        Datu,
        Maharlika,
        Timawa,
        AlipingNamamahay,
        Ayuey,
    ];

    /// <summary>
    /// Every declared rank entry, in <see cref="RankId"/> value order.
    /// </summary>
    public static IReadOnlyList<RankLabelEntry> Entries => All;

    /// <summary>
    /// The catalog entry for <paramref name="rank"/>. A direct lookup, not a
    /// salted or modulo pick — rank is authoritative identity resolved by
    /// Core at spawn, never a presentation-layer choice.
    /// </summary>
    public static RankLabelEntry Get(RankId rank) =>
        rank switch
        {
            RankId.Datu => Datu,
            RankId.Maharlika => Maharlika,
            RankId.Timawa => Timawa,
            RankId.AlipingNamamahay => AlipingNamamahay,
            RankId.Ayuey => Ayuey,
            _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, null),
        };
}
