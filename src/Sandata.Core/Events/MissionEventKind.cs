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
}
