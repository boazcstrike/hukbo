namespace Sandata.Core.Events;

/// <summary>
/// Every kind of authoritative Sandata mission event. Task 76 of
/// docs/plans/2026-08-07-sandata-scaffold.md adds the one member this wave's
/// own criterion requires — design section 16's rejected-order event. A
/// later task appends more members as other stages grow real event
/// emission (design section 5, stage 14: "Emit ordered events").
/// </summary>
/// <remarks>
/// <b>Append-only</b>, matching every other Sandata enum's convention
/// (<see cref="Orders.OrderKind"/>, <see cref="Orders.OrderRejectReason"/>):
/// a member's numeric value never changes once it has shipped, a member is
/// never reordered, and a retired value is never reused for a different
/// meaning. This value folds into the event hash (see
/// <see cref="MissionEventFeed"/>), so reordering would be a silent
/// determinism break, not merely a readability one.
/// </remarks>
public enum MissionEventKind
{
    /// <summary>
    /// Design section 16, "Validation happens at submission, and rejection
    /// is observable": "A rejected order emits an authoritative event
    /// carrying the order id and a reason code. It is not silently
    /// dropped." Emitted by <see cref="Simulation.SandataSimulation.SubmitOrder"/>
    /// at the moment <see cref="Orders.OrderQueue.SubmitValidated"/> reports
    /// a rejection, not deferred to any tick stage.
    /// </summary>
    OrderRejected = 0,

    /// <summary>
    /// Task 79d-1 of docs/plans/2026-08-07-sandata-scaffold.md (the wave-12
    /// audit's corrected obligation): stage 12's weapon chain completed a
    /// shot this tick, before hit resolution decides whether it connects.
    /// Emitted by <see cref="Simulation.SandataSimulation.ProposeFire"/> for
    /// every shot stage 11 recorded as fired, whether it goes on to be a hit
    /// or a miss.
    /// </summary>
    ShotFired = 1,

    /// <summary>
    /// Task 79d-1: the angular error drawn from the <c>Accuracy</c> RNG
    /// stream fell within the target's subtended half-angle at the measured
    /// range, so the shot connects. Emitted alongside <see cref="ShotFired"/>
    /// by <see cref="Simulation.SandataSimulation.ProposeFire"/>, immediately
    /// after it, for the same shot.
    /// </summary>
    ShotHit = 2,

    /// <summary>
    /// Task 79d-1: the angular error drawn from the <c>Accuracy</c> RNG
    /// stream exceeded the target's subtended half-angle at the measured
    /// range, so the shot goes wide. Emitted alongside <see cref="ShotFired"/>
    /// by <see cref="Simulation.SandataSimulation.ProposeFire"/>, immediately
    /// after it, for the same shot.
    /// </summary>
    ShotMissed = 3,
}
