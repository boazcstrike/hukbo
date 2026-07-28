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
    /// The frozen preset. Every field is the value docs/plans/
    /// 2026-07-28-formation-movement-realism-design.md section 3 derives for
    /// the eventual persistent-contingent behaviour; nothing under this
    /// preset reads any of them, but the constant set is closed as of this
    /// task and must not gain a field later, or <see cref="MovementRuleset.ContentHash"/>
    /// would move for this preset too. See
    /// docs/plans/2026-07-28-formation-movement-realism-design.md section 6.2.
    /// </summary>
    private static readonly MovementRuleset IndependentPursuitV1Ruleset = new(
        id: MovementPresetId.IndependentPursuitV1,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
        minimumCohesiveMembers: 3,
        cohesionCycleTicks: 240,
        cohesionDutyTicks: 180,
        arrivalTaperMultiplier: 4,
        offsetUnit: 1024);

    /// <summary>
    /// The persistent-contingent preset. Every tunable is the same value
    /// <see cref="IndependentPursuitV1Ruleset"/> already carries — T2 closed
    /// the constant set at the frozen preset's introduction precisely so
    /// this task could construct a second ruleset from it without adding a
    /// field, which would have moved <c>IndependentPursuitV1</c>'s pinned
    /// <see cref="MovementRuleset.ContentHash"/>. See
    /// docs/plans/2026-07-28-formation-movement-realism-design.md section 3
    /// for the derivation of each value.
    /// </summary>
    private static readonly MovementRuleset PersistentContingentsV2Ruleset = new(
        id: MovementPresetId.PersistentContingentsV2,
        version: 1,
        cohesionRadiusMultiplier: 24,
        closeRadiusMultiplier: 16,
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
