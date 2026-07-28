namespace Hukbo.Core.Movement;

/// <summary>
/// Stable movement preset identity. Numeric values are part of the
/// deterministic replay and content-hash contract; do not renumber or
/// reorder. A new ruleset requires a new value plus a new
/// <see cref="MovementPresetRegistry"/> entry.
/// </summary>
public enum MovementPresetId
{
    /// <summary>
    /// Today's behaviour, frozen. Every warrior pursues its nearest enemy
    /// independently; contingents exist only at deployment and are never
    /// consulted again; no cohesion, no unit state, no arrival taper. See
    /// docs/plans/2026-07-28-formation-movement-realism-design.md section 6.2
    /// for what "frozen" binds here.
    /// </summary>
    IndependentPursuitV1 = 1,

    /// <summary>
    /// Contingent membership survives past deployment. Every living member
    /// carries a <see cref="Simulation.ContingentState"/>, resolved every
    /// tick by the six priority-ordered transition rules of design section
    /// 3.4; a member that has fallen behind its contingent, or the whole
    /// contingent while gathering, may be given a cohesion destination
    /// instead of independent pursuit, subject to the six movement gates and
    /// the cohesion duty cycle of design section 3.5. See
    /// docs/plans/2026-07-28-formation-movement-realism-design.md sections
    /// 3.4 through 3.6.
    /// </summary>
    PersistentContingentsV2 = 2,

    /// <summary>
    /// Contingent membership survives past deployment. Every living member
    /// carries <see cref="Simulation.ContingentState"/>, resolved
    /// tick by six priority-ordered transition rules design section
    /// 3.4; member fallen behind contingent, whole
    /// contingent while gathering, may be given cohesion destination
    /// instead independent pursuit, subject six movement gates
    /// cohesion duty cycle design section 3.5. Identical to
    /// <see cref="PersistentContingentsV2"/> in every respect except transition
    /// rule 3, which now closes the contingent once at least half its living
    /// members have a selected target inside the close radius, and re-opens it
    /// once that fraction drops below a quarter, instead of the single-member
    /// minimum <see cref="PersistentContingentsV2"/> uses. See
    /// docs/archives/2026-07-28/2026-07-28-contingent-close-latch-design.md section 3 for
    /// the derivation.
    /// </summary>
    PersistentContingentsV3 = 3,
}
