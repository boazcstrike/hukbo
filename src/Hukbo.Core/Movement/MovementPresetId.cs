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

    /// <summary>
    /// The narrowed-cohesion-scan preset. Identical to
    /// <see cref="PersistentContingentsV3"/> in every tunable; the one
    /// difference is that movement gate 6, the cross-contingent bias-square
    /// overlap test of design section 3.5, walks only those same-faction
    /// contingents that could actually be granted cohesion this tick, and
    /// skips any contingent whose tick-start
    /// <see cref="Simulation.ContingentState"/> is
    /// <see cref="Simulation.ContingentState.Close"/> or
    /// <see cref="Simulation.ContingentState.Break"/>. Such a contingent parks
    /// no cohesion aim points anywhere, because gate 1 already sends every one
    /// of its members to independent pursuit, so excluding it preserves the
    /// combined-density statement of design section 3.5 exactly while removing
    /// the chain-denial path that made cohesion inert once a faction's leading
    /// contingents reached the enemy. This is the remedy design section 3.5
    /// pre-analysed and declined, and section 13 question 8 reserved for the
    /// user; the user answered it in favour of narrowing after section 10.3's
    /// inertness bar failed. The trade it accepts is recorded in section
    /// 13 question 6: <c>Close</c> and <c>Break</c> members standing inside a
    /// granted square move out of "excluded by gate 6" and into the unbounded
    /// body-occupancy residual.
    /// </summary>
    /// <remarks>
    /// The narrowing does what it claims and does not fix the inertness bar,
    /// and both halves of that sentence are measured. It provably stops a
    /// <c>Close</c> contingent denying its neighbours, which is what
    /// <c>UnderTheNarrowedScanACloseContingentStopsDenyingItsNeighbours</c>
    /// pins. The bar nevertheless still fails, on more faction-seeds than
    /// before rather than fewer, because the clause that fails turns out not
    /// to be measuring chain denial at all. The evidence, and the threshold
    /// question it opens instead, are recorded in
    /// docs/plans/2026-07-28-cohesion-scan-narrowing-design.md. Do not read
    /// this preset as a fix for section 10.3.
    /// </remarks>
    PersistentContingentsV4 = 4,
}
