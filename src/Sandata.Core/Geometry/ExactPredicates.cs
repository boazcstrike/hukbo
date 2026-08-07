namespace Sandata.Core.Geometry;

/// <summary>
/// Exact integer geometric predicates. Every result is either a sign
/// (<see cref="Orient"/>) or a named classification (<see cref="ClassifySegments"/>)
/// computed from exact integer arithmetic. Nothing in this type compares a
/// magnitude to a tolerance; every branch resolves against exactly zero.
///
/// Coordinates are accepted as <see cref="long"/> because the intermediate
/// cross product can reach roughly 7 * 10^13 at this game's maximum map
/// coordinate of 8,388,608 raw units — comfortably inside <see cref="long"/>
/// range but far outside <see cref="int"/> range, which is why every
/// multiplication here is widened before it happens rather than after.
/// </summary>
public static class ExactPredicates
{
    /// <summary>
    /// Returns the sign of the cross product <c>(B - A) x (C - A)</c>:
    /// <c>-1</c> when C is to the right of the directed line A→B (a
    /// clockwise turn), <c>0</c> when A, B, and C are exactly collinear, and
    /// <c>+1</c> when C is to the left of A→B (a counter-clockwise turn).
    ///
    /// Only the sign is a stable contract. The raw magnitude of the cross
    /// product is an implementation detail of how the sign is computed, and
    /// callers must never depend on it.
    /// </summary>
    public static int Orient(long ax, long ay, long bx, long by, long cx, long cy)
    {
        checked
        {
            long cross = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            return Math.Sign(cross);
        }
    }

    /// <summary>
    /// Classifies segment A-B against segment C-D using exact integer
    /// orientation tests only. See <see cref="SegmentRelation"/> for the
    /// written rule behind each named result. Both segments must be
    /// non-degenerate; a segment whose two endpoints are identical has no
    /// direction to orient against and is rejected rather than silently
    /// treated as a point.
    /// </summary>
    public static SegmentRelation ClassifySegments(
        long ax, long ay, long bx, long by,
        long cx, long cy, long dx, long dy)
    {
        if (ax == bx && ay == by)
        {
            throw new ArgumentException("Segment A-B is degenerate: A and B must not be the same point.", nameof(ax));
        }

        if (cx == dx && cy == dy)
        {
            throw new ArgumentException("Segment C-D is degenerate: C and D must not be the same point.", nameof(cx));
        }

        int orientCD_toC = Orient(ax, ay, bx, by, cx, cy);
        int orientCD_toD = Orient(ax, ay, bx, by, dx, dy);
        int orientAB_toA = Orient(cx, cy, dx, dy, ax, ay);
        int orientAB_toB = Orient(cx, cy, dx, dy, bx, by);

        bool properlyStraddlesAB = orientCD_toC != 0 && orientCD_toD != 0 && orientCD_toC != orientCD_toD;
        bool properlyStraddlesCD = orientAB_toA != 0 && orientAB_toB != 0 && orientAB_toA != orientAB_toB;

        if (properlyStraddlesAB && properlyStraddlesCD)
        {
            return SegmentRelation.Crossing;
        }

        bool allCollinear = orientCD_toC == 0 && orientCD_toD == 0 && orientAB_toA == 0 && orientAB_toB == 0;
        if (allCollinear)
        {
            return ClassifyCollinear(ax, ay, bx, by, cx, cy, dx, dy);
        }

        bool touches =
            (orientCD_toC == 0 && IsWithinClosedBoundingBox(ax, ay, bx, by, cx, cy)) ||
            (orientCD_toD == 0 && IsWithinClosedBoundingBox(ax, ay, bx, by, dx, dy)) ||
            (orientAB_toA == 0 && IsWithinClosedBoundingBox(cx, cy, dx, dy, ax, ay)) ||
            (orientAB_toB == 0 && IsWithinClosedBoundingBox(cx, cy, dx, dy, bx, by));

        return touches ? SegmentRelation.Touching : SegmentRelation.Disjoint;
    }

    /// <summary>
    /// Both segments lie on the same infinite line (every orientation test
    /// against it returned zero). The rule for the collinear case is to
    /// parametrize every point along whichever axis segment A-B actually
    /// spans — A-B is guaranteed non-degenerate, so it spans at least one of
    /// the two axes — and compare the two segments' closed intervals along
    /// that axis. An interval intersection wider than one point is
    /// <see cref="SegmentRelation.CollinearOverlap"/>; an intersection of
    /// exactly one point is <see cref="SegmentRelation.Touching"/>; no
    /// intersection at all is <see cref="SegmentRelation.Disjoint"/>.
    /// </summary>
    private static SegmentRelation ClassifyCollinear(
        long ax, long ay, long bx, long by,
        long cx, long cy, long dx, long dy)
    {
        bool useXAxis = ax != bx;

        long abLow = useXAxis ? Math.Min(ax, bx) : Math.Min(ay, by);
        long abHigh = useXAxis ? Math.Max(ax, bx) : Math.Max(ay, by);
        long cdLow = useXAxis ? Math.Min(cx, dx) : Math.Min(cy, dy);
        long cdHigh = useXAxis ? Math.Max(cx, dx) : Math.Max(cy, dy);

        long overlapLow = Math.Max(abLow, cdLow);
        long overlapHigh = Math.Min(abHigh, cdHigh);

        if (overlapLow > overlapHigh)
        {
            return SegmentRelation.Disjoint;
        }

        return overlapLow == overlapHigh ? SegmentRelation.Touching : SegmentRelation.CollinearOverlap;
    }

    /// <summary>
    /// Returns whether point (rx, ry) — already known to be collinear with
    /// the line through (px, py) and (qx, qy) — falls within the closed
    /// axis-aligned bounding box those two points define. This is the
    /// bounded companion to <see cref="Orient"/>'s unbounded line test: an
    /// orientation of zero only means "on the infinite line", and this
    /// method is what narrows that down to "on the segment".
    /// </summary>
    private static bool IsWithinClosedBoundingBox(long px, long py, long qx, long qy, long rx, long ry)
    {
        long minX = Math.Min(px, qx);
        long maxX = Math.Max(px, qx);
        long minY = Math.Min(py, qy);
        long maxY = Math.Max(py, qy);
        return rx >= minX && rx <= maxX && ry >= minY && ry <= maxY;
    }
}
