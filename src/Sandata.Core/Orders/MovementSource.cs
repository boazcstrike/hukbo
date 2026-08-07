using System.Collections.Immutable;
using Sandata.Core.Navigation;

namespace Sandata.Core.Orders;

/// <summary>
/// Which of Sandata's two movement sources one operator follows on one tick.
/// Design section 16, "The two path sources, and the rule that keeps them
/// from fighting": "Every operator's movement, on every tick, comes from
/// exactly one of two sources... There is no third case and no blend of the
/// two."
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a closed hierarchy, not an enum, and that is the structural
/// enforcement.</b> The base record's constructor is <see langword="private"/>,
/// so only the two nested record types declared inside this file —
/// <see cref="AutonomousSlot"/> and <see cref="AuthoredOrder"/> — can ever
/// derive from it; no third subtype can be added anywhere else in the
/// codebase without editing this file. An <see langword="enum"/> would not
/// give the same guarantee: a caller can cast an arbitrary
/// <see langword="int"/> to an enum type and get a defined-looking value that
/// names none of its declared members, so "exactly one of two, never a
/// third" would rest on every caller's good behaviour rather than on the
/// compiler. <see cref="For"/> is the only place either subtype is ever
/// constructed for a real operator, and it can only ever produce one of the
/// two.
/// </para>
/// </remarks>
public abstract record MovementSource
{
    private MovementSource()
    {
    }

    /// <summary>
    /// The operator has no <see cref="OrderAssignment"/> for this tick and
    /// follows its squad slot target along its group's autonomous polyline —
    /// design section 16: "An operator with no assignment follows its squad
    /// slot target... exactly as section 8 describes."
    /// </summary>
    public sealed record AutonomousSlot : MovementSource
    {
        /// <summary>The single shared instance — this case carries no data of its own.</summary>
        public static readonly AutonomousSlot Instance = new();
    }

    /// <summary>
    /// The operator has an <see cref="OrderAssignment"/> for this tick and
    /// follows the authored polyline it names — design section 16: "An
    /// operator whose <c>OrderAssignment</c> is present follows the authored
    /// polyline that assignment names."
    /// </summary>
    /// <param name="Assignment">The assignment being followed.</param>
    public sealed record AuthoredOrder(OrderAssignment Assignment) : MovementSource;

    /// <summary>
    /// Selects the movement source for one operator from the one
    /// authoritative fact design section 16 names: whether an
    /// <see cref="OrderAssignment"/> is present. This is the entire rule —
    /// no other input ever changes the answer.
    /// </summary>
    /// <param name="assignment">
    /// The operator's current assignment, or <see langword="null"/> when it
    /// has none.
    /// </param>
    public static MovementSource For(OrderAssignment? assignment) =>
        assignment is null ? AutonomousSlot.Instance : new AuthoredOrder(assignment);

    /// <summary>
    /// Every reason an <see cref="OrderAssignment"/> can be cleared. Design
    /// section 16 lists exactly these four, in this order, and states that
    /// each is "inspectable" — the operator inspector's third row (design
    /// section 16, "What a spectator sees") shows this value directly.
    /// </summary>
    /// <remarks>
    /// Append-only, matching every other Sandata enum's convention
    /// (<see cref="OrderKind"/>, <see cref="Navigation.PathReasonCode"/>): a
    /// member's numeric value never changes once shipped, and a later task's
    /// inspector or golden fixture may pin one.
    /// </remarks>
    public enum AssignmentClearReason
    {
        /// <summary>Design section 16, condition 1: "The operator reached the polyline's final node."</summary>
        ReachedFinalNode = 0,

        /// <summary>Design section 16, condition 2: "A <c>Cancel</c> order addressed to that operator was applied."</summary>
        Cancelled = 1,

        /// <summary>Design section 16, condition 3: "The operator died."</summary>
        OperatorDied = 2,

        /// <summary>
        /// Design section 16, condition 4: "The polyline became untraversable
        /// — a door closed across it, or a nav rebake blocked a cell the
        /// polyline crosses." Design states this as one condition with two
        /// possible underlying causes, not two conditions, so it gets one
        /// reason code covering both: <see cref="IsPolylineTraversable"/>
        /// tests exactly the same fact — "is every cell this polyline crosses
        /// currently <see cref="NavCellFlags.Open"/>?" — regardless of
        /// whether a closed door or a rebake is what made a cell stop being
        /// <see cref="NavCellFlags.Open"/>.
        /// </summary>
        PolylineUntraversable = 3,
    }

    /// <summary>
    /// One evaluation's outcome: either the assignment survives unchanged, or
    /// it is cleared and the reason is named. <see cref="Assignment"/> and
    /// <see cref="ClearedReason"/> are never both non-<see langword="null"/>
    /// and never both <see langword="null"/> — see <see cref="Evaluate"/>'s
    /// own remarks for why that is guaranteed by construction rather than by
    /// convention.
    /// </summary>
    public readonly record struct EvaluationResult(OrderAssignment? Assignment, AssignmentClearReason? ClearedReason);

    /// <summary>
    /// Evaluates one operator's <see cref="OrderAssignment"/> for this tick
    /// against design section 16's four clearing conditions, and returns
    /// either the assignment unchanged or a cleared result naming which
    /// condition fired.
    /// </summary>
    /// <param name="assignment">The assignment being evaluated. Never mutated; this method reads it and either returns it unchanged or discards it entirely.</param>
    /// <param name="operatorIsAlive">
    /// Whether the operator is alive at this tick's start. Design section
    /// 16's condition 3 ("The operator died") is a fact about the operator,
    /// not about the polyline, so it is supplied here rather than derived —
    /// the death system that decides it is not this file's to own.
    /// </param>
    /// <param name="cancelOrderApplied">
    /// Whether a <see cref="OrderKind.Cancel"/> order addressed to this
    /// operator was applied this tick — design section 16's condition 2. Stage
    /// 1 (order application, task 49's tick pipeline, not built yet) is the
    /// caller that would compute this; this method only consumes the fact.
    /// </param>
    /// <param name="reachedFinalNode">
    /// Whether the operator arrived at <see cref="OrderAssignment.PathNodes"/>'s
    /// last node this tick — design section 16's condition 1. Arrival is a
    /// question about the operator's world-space position against the
    /// polyline's last node, which only a mover (task 49's tick pipeline)
    /// can answer; this method only consumes the fact, the same way it
    /// consumes <paramref name="cancelOrderApplied"/> and
    /// <paramref name="operatorIsAlive"/>.
    /// </param>
    /// <param name="navGrid">
    /// The current nav grid, used only to test design section 16's condition
    /// 4 — see <see cref="IsPolylineTraversable"/>. This is the one condition
    /// this method computes for itself rather than accepting as a
    /// caller-supplied fact, because "is every cell along this polyline
    /// currently open" is a pure function of the polyline and the current
    /// bake, with no need for a mover, a death system, or an order-queue
    /// applier to answer it.
    /// </param>
    /// <returns>
    /// <see cref="EvaluationResult.Assignment"/> is the same
    /// <paramref name="assignment"/> reference and
    /// <see cref="EvaluationResult.ClearedReason"/> is <see langword="null"/>
    /// when none of the four conditions fired.
    /// <see cref="EvaluationResult.Assignment"/> is <see langword="null"/> and
    /// <see cref="EvaluationResult.ClearedReason"/> names the condition when
    /// one did. There is no third outcome and no case where the assignment
    /// comes back altered rather than either kept whole or discarded whole —
    /// design section 16: "The polyline is never silently repaired,
    /// re-routed, or re-smoothed."
    /// </returns>
    /// <remarks>
    /// <b>Precedence when more than one condition holds at once.</b> Design
    /// section 16 does not say what happens when, for example, the operator
    /// both died and reached the final node on the same tick's frozen facts —
    /// only that each condition, exercised alone, clears the assignment with
    /// its own reason. This method checks the four conditions in the exact
    /// order design section 16 lists them (final node, cancel, death,
    /// untraversable), and the first one that holds is the reason reported.
    /// <b>PROVISIONAL:</b> that ordering is this implementation's own
    /// invented tie-break, not a value named anywhere in the design or the
    /// plan; a later task is free to revise it once a concrete tick pipeline
    /// makes a real simultaneous case observable. What is not provisional is
    /// that exactly one reason is always reported when any condition holds —
    /// the four <see langword="if"/> branches below are mutually exclusive in
    /// their return, never falling through to a second check once the first
    /// has already returned.
    /// </remarks>
    public static EvaluationResult Evaluate(
        OrderAssignment assignment,
        bool operatorIsAlive,
        bool cancelOrderApplied,
        bool reachedFinalNode,
        NavGrid navGrid)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(navGrid);

        if (reachedFinalNode)
        {
            return new EvaluationResult(null, AssignmentClearReason.ReachedFinalNode);
        }

        if (cancelOrderApplied)
        {
            return new EvaluationResult(null, AssignmentClearReason.Cancelled);
        }

        if (!operatorIsAlive)
        {
            return new EvaluationResult(null, AssignmentClearReason.OperatorDied);
        }

        if (!IsPolylineTraversable(assignment.PathNodes, navGrid))
        {
            return new EvaluationResult(null, AssignmentClearReason.PolylineUntraversable);
        }

        return new EvaluationResult(assignment, null);
    }

    /// <summary>
    /// The largest number of cells one polyline segment's traversal can ever
    /// need to visit: the grid's two dimensions, at
    /// <see cref="NavGrid.MaxDimensionCells"/> cells each, plus a small
    /// margin. A segment's two endpoints are always inside the grid (an
    /// authored polyline node outside map bounds is rejected at submission —
    /// design section 16, "Validation happens at submission" — a rule task
    /// 58 enforces, not this file), so no straight segment inside a grid can
    /// ever cross more cells than its two dimensions combined.
    /// </summary>
    /// <remarks>
    /// <b>PROVISIONAL.</b> The <c>+ 4</c> margin is this file's own invented
    /// safety pad against an off-by-one at the walk's inclusive endpoints; it
    /// carries no design citation. <see cref="IsPolylineTraversable"/>
    /// documents what happens if this bound is ever wrong for some future,
    /// larger grid.
    /// </remarks>
    private const int MaxCellsPerSegment = (NavGrid.MaxDimensionCells * 2) + 4;

    /// <summary>
    /// Tests design section 16's condition 4 directly: whether every cell
    /// <paramref name="pathNodes"/> crosses — at each node and along every
    /// segment between consecutive nodes — is currently
    /// <see cref="NavCellFlags.Open"/> in <paramref name="navGrid"/>'s current
    /// bake. A cell that is <see cref="NavCellFlags.Blocked"/> (a nav rebake
    /// blocked it) or <see cref="NavCellFlags.Door"/> (a door across it is
    /// currently closed) makes the whole polyline untraversable — design
    /// section 16 names both as the same one condition, and this method tests
    /// both with the same one check, since a closed door's cells are exactly
    /// the cells <see cref="Navigation.NavBake.Bake"/> tags
    /// <see cref="NavCellFlags.Door"/> and a rebake-blocked cell is exactly a
    /// cell tagged <see cref="NavCellFlags.Blocked"/>.
    /// </summary>
    /// <param name="pathNodes">
    /// The authored polyline, drawing order first to last. An empty or
    /// single-node polyline has nothing to cross and is trivially
    /// traversable; task 58's submission validation already rejects an order
    /// below two nodes, so this method never actually sees that case for a
    /// real assignment, but it does not throw for it either.
    /// </param>
    /// <param name="navGrid">The current nav grid.</param>
    /// <returns>
    /// <see langword="true"/> when every node's own cell and every cell each
    /// consecutive pair's segment crosses is <see cref="NavCellFlags.Open"/>;
    /// <see langword="false"/> otherwise, including when a node's cell no
    /// longer lies inside <paramref name="navGrid"/> at all.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="navGrid"/> is <see langword="null"/>.</exception>
    internal static bool IsPolylineTraversable(ImmutableArray<OrderPathNode> pathNodes, NavGrid navGrid)
    {
        ArgumentNullException.ThrowIfNull(navGrid);

        if (pathNodes.IsDefaultOrEmpty)
        {
            return true;
        }

        for (var index = 0; index < pathNodes.Length; index++)
        {
            var node = pathNodes[index];
            var cellX = NavGrid.WorldToCellCoordinate(node.X);
            var cellY = NavGrid.WorldToCellCoordinate(node.Y);

            if (!navGrid.TryGetCellIndex(cellX, cellY, out var nodeCellIndex))
            {
                return false;
            }

            if (navGrid.Passability[nodeCellIndex] != NavCellFlags.Open)
            {
                return false;
            }
        }

        if (pathNodes.Length < 2)
        {
            return true;
        }

        Span<int> cellBuffer = stackalloc int[MaxCellsPerSegment];

        for (var index = 0; index < pathNodes.Length - 1; index++)
        {
            var from = pathNodes[index];
            var to = pathNodes[index + 1];

            var written = GridRay.Traverse(from.X, from.Y, to.X, to.Y, cellBuffer, navGrid);

            for (var cellIndexInBuffer = 0; cellIndexInBuffer < written; cellIndexInBuffer++)
            {
                if (navGrid.Passability[cellBuffer[cellIndexInBuffer]] != NavCellFlags.Open)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Design section 16's other narrow, exact consequence of an assignment's
    /// presence: "an operator under orders is excluded from slot targeting
    /// for that tick, and the path service does not enqueue a request on its
    /// behalf." This method is that exclusion's concrete representation — the
    /// filtered roster a slot-targeting stage would consume, rather than a
    /// flag consulted by convention at every call site that touches slot
    /// targets.
    /// </summary>
    /// <param name="operatorEntityIds">
    /// Every operator's entity ID for this tick. Order does not matter — the
    /// result is always ascending regardless of the order this span arrives
    /// in, so permuting <paramref name="operatorEntityIds"/> permutes nothing
    /// in the return value.
    /// </param>
    /// <param name="assignedEntityIds">
    /// The <see cref="OrderAssignment.EntityId"/> of every operator that has
    /// an assignment this tick, in strictly ascending order with no repeats.
    /// An operator whose entity ID does not appear here has no assignment.
    /// This is a flat sorted array rather than a dictionary keyed by entity
    /// ID on purpose — design section 7's "flat arrays, no dictionaries" and
    /// the determinism contract's ban on <c>Dictionary&lt;</c> inside
    /// <c>Sandata.Core</c> (this file's own hygiene test enforces the ban by
    /// scanning for the token) both rule out the more obvious map-keyed-by-id
    /// shape. Membership is tested by binary search, which the ascending
    /// order this parameter requires makes correct; passing an unsorted or
    /// duplicate-containing span produces an unspecified result rather than
    /// throwing, since nothing here can afford an O(n) validation pass over a
    /// per-tick input without reintroducing the same complexity a dictionary
    /// would have hidden.
    /// </param>
    /// <returns>
    /// Every entity ID from <paramref name="operatorEntityIds"/> that does not
    /// appear in <paramref name="assignedEntityIds"/>, ascending. Design
    /// section 8's squad-grouping derivation is unaffected by this exclusion —
    /// it keeps consuming the full, unfiltered roster, exactly as design
    /// section 16 states ("Squad grouping itself is unaffected... a group id
    /// still exists for every operator whether or not it is under orders").
    /// This method's return value is for slot targeting alone, not for
    /// <c>SquadGrouping.Compute</c>.
    /// </returns>
    public static ImmutableArray<ulong> SlotTargetingRoster(
        ReadOnlySpan<ulong> operatorEntityIds,
        ReadOnlySpan<ulong> assignedEntityIds)
    {
        var builder = ImmutableArray.CreateBuilder<ulong>(operatorEntityIds.Length);

        foreach (var entityId in operatorEntityIds)
        {
            if (!IsAssigned(entityId, assignedEntityIds))
            {
                builder.Add(entityId);
            }
        }

        builder.Sort();

        return builder.ToImmutable();
    }

    /// <summary>
    /// Binary search for <paramref name="entityId"/> within
    /// <paramref name="assignedEntityIds"/>, which <see cref="SlotTargetingRoster"/>'s
    /// own parameter documentation requires to already be ascending. A flat
    /// sorted-array search rather than a dictionary lookup, for the same
    /// reason <see cref="SlotTargetingRoster"/> takes a span rather than a
    /// map — see that method's <c>assignedEntityIds</c> parameter remarks.
    /// </summary>
    private static bool IsAssigned(ulong entityId, ReadOnlySpan<ulong> assignedEntityIds)
    {
        var low = 0;
        var high = assignedEntityIds.Length - 1;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var candidate = assignedEntityIds[mid];

            if (candidate == entityId)
            {
                return true;
            }

            if (candidate < entityId)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return false;
    }
}
