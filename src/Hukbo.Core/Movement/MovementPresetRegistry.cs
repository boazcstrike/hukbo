namespace Hukbo.Core.Movement;

/// <summary>
/// Exhaustive registry of authoritative movement rulesets keyed by
/// <see cref="MovementPresetId"/>. New presets require a new enum value and
/// a corresponding switch arm here; unregistered values fail loudly rather
/// than silently falling back to a default ruleset.
/// </summary>
public static class MovementPresetRegistry
{
    /// <summary>
    /// The frozen preset. What is frozen is this preset's simulated
    /// behaviour, proved byte-identically by
    /// tests/Hukbo.Core.Tests/Fixtures/seed-1-200-agents-movement-v1-digest.json,
    /// not its field list. <see cref="MovementRuleset.ContentHash"/> never
    /// reaches the state hash, so a task that adds a field to
    /// <see cref="MovementRuleset"/> moves only the pinned <c>ContentHash</c>
    /// identity literal in <c>MovementPresetRegistryTests</c> — recomputed
    /// from the built code, never guessed — and leaves this preset's actual
    /// behaviour untouched. See
    /// docs/archives/2026-07-28/2026-07-28-contingent-close-latch-design.md section 3.
    /// <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c> are
    /// registered here at <c>(0, 1)</c>, which collapses both the entry and
    /// exit thresholds in <c>MovementRules.ResolveContingentState</c> to
    /// <c>Max(1, ...)</c> — "at least one member in contact" — exactly
    /// today's minimum-distance rule, so this preset's behaviour does not
    /// move.
    /// </summary>
    private static readonly MovementRuleset IndependentPursuitV1Ruleset = new(
        id: MovementPresetId.IndependentPursuitV1,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        closeFractionNumerator: 0,
        closeFractionDenominator: 1,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024,
        narrowsCohesionScanToCohesionCapableContingents: false,
        selectsLeaderByRank: false);

    /// <summary>
    /// The persistent-contingent preset. Every tunable is the same value
    /// <see cref="IndependentPursuitV1Ruleset"/> already carries.
    /// <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c> are
    /// registered here at <c>(0, 1)</c> for the same reason as above: the
    /// floor of <c>Max(1, ...)</c> makes the fraction reproduce today's
    /// minimum-distance rule exactly, so introducing the fields moves no
    /// behaviour under this preset either. See
    /// docs/archives/2026-07-28/2026-07-28-contingent-close-latch-design.md section 3 for
    /// the derivation of each value.
    /// </summary>
    private static readonly MovementRuleset PersistentContingentsV2Ruleset = new(
        id: MovementPresetId.PersistentContingentsV2,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        closeFractionNumerator: 0,
        closeFractionDenominator: 1,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024,
        narrowsCohesionScanToCohesionCapableContingents: false,
        selectsLeaderByRank: false);

    /// <summary>
    /// The contact-fraction preset. Every tunable is the same value
    /// <see cref="PersistentContingentsV2Ruleset"/> already carries except
    /// <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c>,
    /// registered here at <c>(1, 2)</c>: transition rule 3 closes the
    /// contingent once at least half its living members have a selected
    /// target inside the close radius, instead of the single-member minimum
    /// the <c>(0, 1)</c> floor reproduces. Not the shipped default — reachable
    /// only through <c>--movement-preset</c> until
    /// docs/archives/2026-07-28/2026-07-28-contingent-close-latch.md T6 flips it. See
    /// docs/archives/2026-07-28/2026-07-28-contingent-close-latch-design.md section 3 for
    /// the derivation.
    /// </summary>
    /// <remarks>
    /// The pair <c>(1, 2)</c> is a provisional game-design choice, not a
    /// historical measurement. No source describes a unit's contact
    /// threshold, and half-to-close with a quarter-to-re-open is a starting
    /// point chosen for the shape it produces, not a quantity derived from
    /// anything. It is re-measured by <c>Hukbo.Tools.ContingentShape</c> in
    /// plan task T7, and the exit band in particular — half the entry
    /// fraction — is the open question design section 7 records.
    /// </remarks>
    private static readonly MovementRuleset PersistentContingentsV3Ruleset = new(
        id: MovementPresetId.PersistentContingentsV3,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        closeFractionNumerator: 1,
        closeFractionDenominator: 2,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024,
        narrowsCohesionScanToCohesionCapableContingents: false,
        selectsLeaderByRank: false);

    /// <summary>
    /// The narrowed-cohesion-scan preset, and the shipped default. Every
    /// tunable is the same value <see cref="PersistentContingentsV3Ruleset"/>
    /// already carries; the single difference is
    /// <c>NarrowsCohesionScanToCohesionCapableContingents</c>, registered here
    /// at <see langword="true"/>, which restricts movement gate 6 to
    /// contingents that could actually be granted cohesion this tick.
    /// </summary>
    /// <remarks>
    /// The narrowing is the remedy design section 3.5 pre-analysed, declined,
    /// and named as "the first remedy if the inertness bar in section 10.3
    /// fails"; section 13 question 8 reserved the ordering for the user. The
    /// bar did fail — <c>CohesionCoverageIsNotPracticallyInertAcrossSeedsOneThroughTwenty</c>
    /// reported seed 11, faction 1 with no cohering tick in the later half of
    /// a 138-tick pre-<c>Close</c> window — and the user answered question 8
    /// in favour of narrowing. Adopting it did not clear the bar; see
    /// docs/plans/2026-07-28-cohesion-scan-narrowing-design.md for what the
    /// measurement found instead. It lands as a
    /// new preset rather than as an edit to
    /// <see cref="PersistentContingentsV3Ruleset"/> because V3 has already
    /// shipped as a default: CLAUDE.md section 5 requires a new preset version
    /// plus new golden expectations for any change that moves simulated
    /// behaviour, and <c>PersistentContingentsV2</c> and V3 both keep the
    /// behaviour their own recorded expectations pin.
    /// </remarks>
    private static readonly MovementRuleset PersistentContingentsV4Ruleset = new(
        id: MovementPresetId.PersistentContingentsV4,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        closeFractionNumerator: 1,
        closeFractionDenominator: 2,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024,
        narrowsCohesionScanToCohesionCapableContingents: true,
        selectsLeaderByRank: false);

    /// <summary>
    /// The rank-aware leader-scan preset. Every tunable is the same value
    /// <see cref="PersistentContingentsV4Ruleset"/> already carries,
    /// restated verbatim rather than referenced, following the
    /// "restate, do not reference" convention V4 already uses against V3;
    /// the single difference is <see cref="MovementRuleset.SelectsLeaderByRank"/>,
    /// registered here at <see langword="true"/>, which orders the leader
    /// scan's candidates by <c>(RankId ascending, EntityId ascending)</c>
    /// instead of <c>EntityId</c> alone.
    /// </summary>
    /// <remarks>
    /// It lands as a new preset rather than as an edit to
    /// <see cref="PersistentContingentsV4Ruleset"/> because V4 has already
    /// shipped as a default: CLAUDE.md section 5 requires a new preset
    /// version plus new golden expectations for any change that moves
    /// simulated behaviour, and V1 through V4 all keep the behaviour their
    /// own recorded expectations pin. See
    /// docs/plans/2026-07-29-leader-rank-design.md section 2 for why this
    /// comparator swap reaches further into the simulation than the leader
    /// scan itself.
    /// </remarks>
    private static readonly MovementRuleset PersistentContingentsV5Ruleset = new(
        id: MovementPresetId.PersistentContingentsV5,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        closeFractionNumerator: 1,
        closeFractionDenominator: 2,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024,
        narrowsCohesionScanToCohesionCapableContingents: true,
        selectsLeaderByRank: true);

    public static bool IsRegistered(MovementPresetId id) =>
        id switch
        {
            MovementPresetId.IndependentPursuitV1 => true,
            MovementPresetId.PersistentContingentsV2 => true,
            MovementPresetId.PersistentContingentsV3 => true,
            MovementPresetId.PersistentContingentsV4 => true,
            MovementPresetId.PersistentContingentsV5 => true,
            _ => false,
        };

    public static MovementRuleset Get(MovementPresetId id) =>
        id switch
        {
            MovementPresetId.IndependentPursuitV1 => IndependentPursuitV1Ruleset,
            MovementPresetId.PersistentContingentsV2 => PersistentContingentsV2Ruleset,
            MovementPresetId.PersistentContingentsV3 => PersistentContingentsV3Ruleset,
            MovementPresetId.PersistentContingentsV4 => PersistentContingentsV4Ruleset,
            MovementPresetId.PersistentContingentsV5 => PersistentContingentsV5Ruleset,
            _ => throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                $"Movement preset {id} is not registered."),
        };
}
