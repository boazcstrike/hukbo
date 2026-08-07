namespace Sandata.Core.Orders;

/// <summary>
/// Design section 16's four <see cref="OrderKind.MoveAlongPath"/> rejection
/// rules, in the order that section lists them, checked by
/// <see cref="OrderValidation.ValidateMoveAlongPath"/>: "A node lies outside
/// the map bounds. A node lies in a cell that is <c>Blocked</c> in the
/// current nav bake. A segment between consecutive nodes crosses a wall
/// segment... The node count exceeds the cap, or is below two." A door cell
/// is deliberately not one of these rules — "Door cells are deliberately not
/// a rejection condition."
/// </summary>
/// <remarks>
/// <b>Append-only</b>, matching every other Sandata enum's convention
/// (<see cref="OrderKind"/>, <c>PathReasonCode</c>): a member's numeric value
/// never changes once it has shipped, a member is never reordered, and a
/// retired value is never reused for a different reason. This value is not
/// currently folded into the state hash or any snapshot — design section 16's
/// determinism amendment names only the queue and the per-operator
/// assignment for that — but a later task's operator inspector or a recorded
/// rejection-event fixture may still pin a specific member's numeric value,
/// so reordering is avoided regardless.
/// </remarks>
public enum OrderRejectReason
{
    /// <summary>
    /// A node's coordinate falls outside the closed map-bounds interval
    /// <c>[0, WidthWu] x [0, HeightWu]</c> — the same closed interval
    /// <c>MapTokenizer</c> validates a <c>WALL</c>/<c>DOOR</c> record's own
    /// endpoints against.
    /// </summary>
    NodeOutsideMapBounds = 0,

    /// <summary>
    /// A node's coordinate falls inside a cell the current nav bake marks
    /// <c>NavCellFlags.Blocked</c>. A <c>NavCellFlags.Door</c> cell is not
    /// this reason — design section 16: "Door cells are deliberately not a
    /// rejection condition."
    /// </summary>
    NodeInBlockedCell = 1,

    /// <summary>
    /// The segment between two consecutive nodes is classified as anything
    /// other than <c>SegmentRelation.Disjoint</c> against a wall segment in
    /// the wall bucket index — a proper crossing, an endpoint touch, or a
    /// collinear overlap all count, the same rule
    /// <c>LineOfSight.FirstBlockingSegment</c> already applies to a
    /// sightline: "A relation of anything other than
    /// <c>SegmentRelation.Disjoint</c> counts as blocking."
    /// </summary>
    SegmentCrossesWall = 2,

    /// <summary>
    /// The polyline's node count is below two (too short to draw a path at
    /// all) or exceeds <see cref="Order.MaxAuthoredPathNodeCount"/>.
    /// </summary>
    InvalidNodeCount = 3,
}

/// <summary>
/// The observable outcome of a rejected <see cref="OrderKind.MoveAlongPath"/>
/// submission — design section 16: "A rejected order emits an authoritative
/// event carrying the order id and a reason code. It is not silently
/// dropped." <see cref="Orders.OrderQueue.SubmitValidated"/> is the only
/// producer of this type; see that method's remarks for whether a rejected
/// order's <see cref="OrderId"/> was ever assigned to a stored
/// <see cref="Order"/>.
/// </summary>
/// <param name="OrderId">
/// The id the rejected submission was assigned. Carried here specifically so
/// a spectator-facing event or log line can name which submission failed
/// without that submission ever having entered <c>OrderQueue.Orders</c>.
/// </param>
/// <param name="Reason">Which of design section 16's four rules the submission failed.</param>
public sealed record OrderRejection(long OrderId, OrderRejectReason Reason);
