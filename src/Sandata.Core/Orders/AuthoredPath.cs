using System.Collections.Immutable;

namespace Sandata.Core.Orders;

/// <summary>
/// The candidate authored polyline <see cref="OrderValidation"/> validates at
/// submission — design section 16, "Validation happens at submission, and
/// rejection is observable": "A <c>MoveAlongPath</c> order is rejected when
/// any of the following holds..." This type wraps the same
/// <see cref="OrderPathNode"/> sequence <see cref="Order.PathNodes"/> stores,
/// giving the validator a named type to work against instead of a bare
/// <see cref="ImmutableArray{T}"/>, and one place to enumerate the
/// consecutive-node segments the wall-crossing rule walks.
/// </summary>
/// <param name="Nodes">
/// The polyline's nodes, drawing order first to last, exactly as they would
/// be stored on <see cref="Order.PathNodes"/> if the order is accepted. Not
/// re-smoothed and not reordered — design section 16, "An authored polyline
/// is authoritative, not derived."
/// </param>
/// <remarks>
/// This type performs no validation of its own; it is a plain data carrier.
/// <see cref="OrderValidation.ValidateMoveAlongPath"/> is the sole place
/// design section 16's four rejection rules are checked.
/// </remarks>
public readonly record struct AuthoredPath(ImmutableArray<OrderPathNode> Nodes)
{
    /// <summary>
    /// The number of nodes this path carries. <c>0</c> for a
    /// default-initialized (never-assigned) <see cref="Nodes"/> array, which
    /// <see cref="ImmutableArray{T}.IsDefault"/> distinguishes from a
    /// genuinely empty one — both count as zero nodes here, since neither
    /// carries a usable polyline.
    /// </summary>
    public int NodeCount => Nodes.IsDefault ? 0 : Nodes.Length;

    /// <summary>
    /// Yields each consecutive pair of nodes as one segment, in drawing
    /// order — the segments design section 16's wall-crossing rule tests one
    /// at a time against <c>ExactPredicates.ClassifySegments</c>. Empty when
    /// fewer than two nodes are present; a single node has no segment to
    /// draw.
    /// </summary>
    public IEnumerable<(OrderPathNode From, OrderPathNode To)> Segments()
    {
        if (Nodes.IsDefault)
        {
            yield break;
        }

        for (var index = 1; index < Nodes.Length; index++)
        {
            yield return (Nodes[index - 1], Nodes[index]);
        }
    }
}
