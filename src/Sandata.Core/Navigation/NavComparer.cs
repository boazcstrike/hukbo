namespace Sandata.Core.Navigation;

/// <summary>
/// Grid A*'s open-set ordering rule: the total key <c>(f, h, nodeIndex)</c>,
/// compared in that order. Design section 7, "The comparator": "Because
/// <c>nodeIndex</c> is unique, the key is total, so any correct heap
/// implementation produces the same expansion order. That is the point: the
/// comparator carries the determinism, not the container."
///
/// <para>
/// This is the whole reason the risk register's "A* open set ordered by
/// something less than a total key" row closes: <c>nodeIndex</c> is unique
/// per cell, so no two distinct nodes can ever compare equal, which makes
/// this a strict total order rather than a preorder with ties. Two entries
/// that share the same <c>nodeIndex</c> necessarily share the same
/// <c>f</c> and <c>h</c> too (both are looked up from that same node), so the
/// only way <see cref="Compare"/> returns zero is when both sides describe
/// the same node.
/// </para>
///
/// <para>
/// Deliberately implemented with three-way comparisons (<c>&lt;</c> /
/// <c>&gt;</c>) rather than subtraction: <c>f[a] - f[b]</c> can overflow
/// <see cref="int"/> for inputs that are individually well within range, and
/// an overflowed subtraction can silently invert a comparison. Comparing
/// directly has no such failure mode for any pair of <see cref="int"/>
/// values.
/// </para>
/// </summary>
public static class NavComparer
{
    /// <summary>
    /// Compares two open-set keys, each given as its own <c>(f, h, nodeIndex)</c>
    /// triple. Returns a negative number when the first key sorts before the
    /// second, a positive number when it sorts after, and zero only when
    /// every one of the three fields is equal on both sides — which, because
    /// <c>nodeIndex</c> is unique per node, only happens when both triples
    /// describe the same node.
    /// </summary>
    public static int Compare(int fA, int hA, int nodeIndexA, int fB, int hB, int nodeIndexB)
    {
        if (fA != fB)
        {
            return fA < fB ? -1 : 1;
        }

        if (hA != hB)
        {
            return hA < hB ? -1 : 1;
        }

        if (nodeIndexA != nodeIndexB)
        {
            return nodeIndexA < nodeIndexB ? -1 : 1;
        }

        return 0;
    }
}
