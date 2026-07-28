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
    /// docs/plans/2026-07-28-contingent-close-latch-design.md section 3.
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
        offsetUnit: 1024);

    /// <summary>
    /// The persistent-contingent preset. Every tunable is the same value
    /// <see cref="IndependentPursuitV1Ruleset"/> already carries.
    /// <c>CloseFractionNumerator</c> and <c>CloseFractionDenominator</c> are
    /// registered here at <c>(0, 1)</c> for the same reason as above: the
    /// floor of <c>Max(1, ...)</c> makes the fraction reproduce today's
    /// minimum-distance rule exactly, so introducing the fields moves no
    /// behaviour under this preset either. See
    /// docs/plans/2026-07-28-contingent-close-latch-design.md section 3 for
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
        offsetUnit: 1024);

    public static bool IsRegistered(MovementPresetId id) =>
        id switch
        {
            MovementPresetId.IndependentPursuitV1 => true,
            MovementPresetId.PersistentContingentsV2 => true,
            _ => false,
        };

    public static MovementRuleset Get(MovementPresetId id) =>
        id switch
        {
            MovementPresetId.IndependentPursuitV1 => IndependentPursuitV1Ruleset,
            MovementPresetId.PersistentContingentsV2 => PersistentContingentsV2Ruleset,
            _ => throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                $"Movement preset {id} is not registered."),
        };
}
