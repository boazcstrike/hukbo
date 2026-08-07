using System.Collections.Immutable;

namespace Sandata.Core.Orders;

/// <summary>
/// One operator's currently active order assignment — the authoritative
/// field design section 16's per-tick movement-source rule is derived from:
/// "An operator whose <c>OrderAssignment</c> is present follows the authored
/// polyline that assignment names. An operator with no assignment follows its
/// squad slot target along its group's autonomous polyline... There is no
/// third case and no blend of the two." <see cref="MovementSource"/> is the
/// type that reads this presence/absence to make that selection, and that
/// evaluates the four conditions design section 16 lists for clearing it.
/// </summary>
/// <param name="EntityId">The operator this assignment belongs to.</param>
/// <param name="OrderId">
/// The <see cref="Order.OrderId"/> of the <see cref="OrderKind.MoveAlongPath"/>
/// order that created this assignment. Design section 16, "What a spectator
/// sees": "The operator inspector gains three rows: the active order id, the
/// node index currently being walked, and the reason code that cleared the
/// last assignment." <see cref="OrderId"/> is the first of those three; this
/// type stores it so a later inspector task never has to re-derive "which
/// order produced this assignment" by searching the order queue.
/// </param>
/// <param name="CurrentNodeIndex">
/// The zero-based index, into <see cref="PathNodes"/>, of the node the
/// operator is currently walking toward — design section 16's "the node index
/// currently being walked." A future mover (task 49's tick pipeline, which
/// does not exist yet in this worktree) is the type that advances this value
/// as the operator makes progress along the polyline; this record only
/// stores whatever value that caller supplies. It is not itself validated
/// against <see cref="PathNodes"/>'s length here — a value at or past the
/// last index is exactly the state a caller sets to signal "the operator has
/// arrived", the fact <see cref="MovementSource.Evaluate"/> asks for through
/// its own <c>reachedFinalNode</c> parameter rather than re-deriving from this
/// field, since only the mover — not this record and not the evaluator — knows
/// whether "at the last node's cell" instead means "still walking toward it".
/// </param>
public sealed record OrderAssignment(
    ulong EntityId,
    long OrderId,
    int CurrentNodeIndex)
{
    /// <summary>
    /// The authored polyline this assignment follows, stored verbatim from
    /// the <see cref="OrderKind.MoveAlongPath"/> order that created it.
    /// Design section 16, "An authored polyline is authoritative, not
    /// derived": "An authored polyline is player input. It is stored verbatim
    /// in the snapshot and folds into the state hash. It is never recomputed,
    /// never re-smoothed, and never replaced by a search result." This
    /// property carries that same array end to end — nothing in this file or
    /// in <see cref="MovementSource"/> ever mutates an element of it,
    /// reorders it, or replaces it with a re-routed value; the only thing
    /// <see cref="MovementSource.Evaluate"/> ever does with an untraversable
    /// polyline is discard the whole assignment and let autonomy resume, per
    /// that same section's "The polyline is never silently repaired,
    /// re-routed, or re-smoothed."
    /// </summary>
    public ImmutableArray<OrderPathNode> PathNodes { get; init; } = ImmutableArray<OrderPathNode>.Empty;

    public bool Equals(OrderAssignment? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return EntityId == other.EntityId &&
            OrderId == other.OrderId &&
            CurrentNodeIndex == other.CurrentNodeIndex &&
            PathNodesSpan.SequenceEqual(other.PathNodesSpan);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EntityId);
        hash.Add(OrderId);
        hash.Add(CurrentNodeIndex);
        foreach (var node in PathNodesSpan)
        {
            hash.Add(node);
        }

        return hash.ToHashCode();
    }

    private ReadOnlySpan<OrderPathNode> PathNodesSpan =>
        PathNodes.IsDefault ? ReadOnlySpan<OrderPathNode>.Empty : PathNodes.AsSpan();
}
