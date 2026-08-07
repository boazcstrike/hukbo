namespace Sandata.Core.Geometry;

/// <summary>
/// Point-in-polygon containment by crossing number, with no epsilon and no
/// special-cased degenerate input.
/// </summary>
/// <remarks>
/// The classic crossing-number test walks every edge of the polygon and
/// counts how many times a horizontal ray cast from the test point in the
/// direction of increasing X crosses that edge; an odd count means the
/// point is inside. Written naively with an inclusive test on both edge
/// endpoints, that count is wrong at every vertex the ray happens to pass
/// through, because both edges meeting at that vertex report a crossing and
/// the count comes out even when it should be odd, or the reverse.
///
/// This type instead uses the standard half-open fix: walking an edge from
/// <c>(ax, ay)</c> to <c>(bx, by)</c>, the edge counts as an
/// <b>upward</b> crossing candidate only when <c>ay &lt;= pointY &lt; by</c>,
/// and as a <b>downward</b> crossing candidate only when
/// <c>by &lt;= pointY &lt; ay</c>. The asymmetry — one endpoint tested with
/// <c>&lt;=</c>, the other with a strict <c>&lt;</c> — is deliberate, and it
/// is what removes every degenerate case without a single special-cased
/// branch:
///
/// <list type="bullet">
/// <item><description>
/// A horizontal edge has <c>ay == by</c>, so both the upward test
/// (<c>ay &lt;= pointY &lt; by</c>) and the downward test
/// (<c>by &lt;= pointY &lt; ay</c>) require a value to be strictly less than
/// itself, which is never true. Horizontal edges are silently skipped with
/// no dedicated branch.
/// </description></item>
/// <item><description>
/// A vertex shared by two consecutive edges is, by construction, the
/// endpoint where one edge's <c>&lt;=</c> side meets the other edge's
/// strict <c>&lt;</c> side. At that shared height, exactly one of the two
/// edges can ever satisfy its half-open test, so a ray passing through a
/// vertex is counted by exactly one adjoining edge rather than zero or two.
/// </description></item>
/// <item><description>
/// The same rule handles a ray that exits the polygon exactly through a
/// vertex where the boundary turns back on itself (for example the tip of
/// an inward notch): each adjoining edge is still evaluated independently
/// under its own half-open rule, so the vertex is never double-counted and
/// never silently dropped.
/// </description></item>
/// </list>
///
/// Whether an edge's half-open height test passes only decides whether that
/// edge is a <i>candidate</i> crossing; whether it actually crosses to the
/// correct side of the test point is then decided exactly, with no epsilon,
/// by <see cref="ExactPredicates.Orient"/> — the same sign-of-cross-product
/// test used everywhere else in this module.
/// </remarks>
public static class Polygon
{
    /// <summary>
    /// Determines whether the point <c>(pointX, pointY)</c> lies inside the
    /// polygon whose vertices are <c>(vertexXs[i], vertexYs[i])</c> in
    /// order, with an implicit closing edge from the last vertex back to
    /// the first.
    /// </summary>
    public static bool Contains(
        ReadOnlySpan<long> vertexXs,
        ReadOnlySpan<long> vertexYs,
        long pointX,
        long pointY)
    {
        if (vertexXs.Length != vertexYs.Length)
        {
            throw new ArgumentException(
                "Vertex coordinate spans must have equal length.",
                nameof(vertexYs));
        }

        var vertexCount = vertexXs.Length;
        if (vertexCount < 3)
        {
            return false;
        }

        var inside = false;
        var previous = vertexCount - 1;

        for (var current = 0; current < vertexCount; previous = current, current++)
        {
            var ax = vertexXs[previous];
            var ay = vertexYs[previous];
            var bx = vertexXs[current];
            var by = vertexYs[current];

            var isUpwardCandidate = ay <= pointY && pointY < by;
            var isDownwardCandidate = by <= pointY && pointY < ay;

            if (!isUpwardCandidate && !isDownwardCandidate)
            {
                continue;
            }

            var orientation = ExactPredicates.Orient(ax, ay, bx, by, pointX, pointY);

            if (isUpwardCandidate && orientation > 0)
            {
                inside = !inside;
            }
            else if (isDownwardCandidate && orientation < 0)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
