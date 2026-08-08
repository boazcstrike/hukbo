using Sandata.Core.Mathematics;

namespace Sandata.Core.Geometry;

/// <summary>
/// A per-unit vision cone tested by two half-plane cross products against a
/// pinned boundary-vector table, never by a cosine comparison.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why never a cosine comparison.</b> The obvious cone test — take the
/// dot product of the look direction and the candidate direction, and
/// compare it against <c>cos(halfWidth)</c> scaled by both vectors'
/// magnitudes — is exactly the test this type refuses to write, because the
/// direction vectors already sitting in this codebase are not safe to use
/// that way. <c>Hukbo.Core</c>'s <c>FacingRules</c> sector vectors are
/// scaled to 1024 but are not unit length: 946² + 392² is 1,048,580 against
/// 1024²'s 1,048,576 (off by 4), while 724² doubled is 1,048,352 (off by
/// 224). That error is a different size for every sector, so a cosine
/// comparison that assumes a constant magnitude produces a cone that is
/// subtly wider or narrower depending on which way the unit happens to be
/// facing — a bug that changes shape with facing and would not show up in a
/// test that only ever checks one direction. Two half-plane cross products
/// have no such assumption: each test only asks which side of a boundary
/// ray a point falls on, a question a cross product's sign answers exactly
/// regardless of how long the boundary vector happens to be. See
/// <see cref="ConeBoundaryTable"/> for the vectors this method reads.
/// </para>
/// <para>
/// <b>Deviation from the terse table row.</b> Design section 6 of
/// docs/plans/2026-08-07-sandata-scaffold-design.md writes this method's
/// signature as <c>Contains(Bam16 centre, ushort halfWidth, long dx, long
/// dy)</c> with no range parameter, but a vision cone with no distance limit
/// is not the shape a sensing system needs, and the task instruction that
/// commissioned this file requires a range check ahead of the two
/// half-plane tests. <c>rangeSquared</c> is added as this method's third
/// parameter to make that check possible; every other detail of the design
/// row is followed exactly. This is the same kind of correction design
/// section 6 already needed once before — the same section originally wrote
/// <c>RayBox.Intersects</c> and <c>Polygon.Contains</c> against a
/// <c>Point</c> type that does not exist in this repository, corrected to
/// flat <see cref="long"/> coordinates once tasks 4 and 6 actually
/// implemented them — and is recorded here for the same reason: so a later
/// pass reconciling the design document finds one more row to update rather
/// than a silent divergence.
/// </para>
/// </remarks>
public static class VisionCone
{
    /// <summary>
    /// Half of a full turn in <see cref="Bam16"/> raw units (65,536 per
    /// turn), used to decide whether a cone's total angular width — twice
    /// its <c>halfWidth</c> — is narrower than a half turn or not. A cone
    /// exactly as wide as a half turn is <b>not</b> treated as narrower; see
    /// the boundary-combination rule on <see cref="Contains"/>.
    /// </summary>
    private const int QuarterTurn = Bam16.UnitsPerTurn / 4;

    /// <summary>
    /// Whether the point <paramref name="dx"/>, <paramref name="dy"/> away
    /// from the cone's apex lies inside the vision cone facing
    /// <paramref name="centre"/>, spanning <paramref name="halfWidth"/> raw
    /// units to either side of centre, out to a maximum range of
    /// <c>sqrt(rangeSquared)</c> world units.
    /// </summary>
    /// <param name="centre">The angle the cone faces.</param>
    /// <param name="halfWidth">
    /// Half the cone's total angular width, in <see cref="Bam16"/> raw
    /// units. The cone's two edges sit at <c>centre - halfWidth</c> and
    /// <c>centre + halfWidth</c>. A value of 16,384 (a quarter turn) makes
    /// the cone exactly a half turn wide in total.
    /// </param>
    /// <param name="rangeSquared">
    /// The square of the cone's maximum range, in the same squared world
    /// units as <c>dx * dx + dy * dy</c>. A point farther than this is
    /// excluded before either angular test runs, regardless of direction.
    /// </param>
    /// <param name="dx">The candidate point's offset from the apex, X axis.</param>
    /// <param name="dy">The candidate point's offset from the apex, Y axis.</param>
    /// <remarks>
    /// <b>The boundary decision.</b> A point lying exactly on either edge
    /// ray produces a cross product of exactly zero against that edge's
    /// boundary vector, and this method treats zero as inside for both
    /// edges — <c>crossLeft &gt;= 0</c> and <c>crossRight &lt;= 0</c> both
    /// admit zero. This is a deliberate, one-time choice rather than an
    /// accident of the comparison operators: a target standing exactly on
    /// the geometric edge of a unit's field of view is visible to it, on
    /// both edges, symmetrically. No caller may assume the opposite.
    /// </remarks>
    public static bool Contains(Bam16 centre, ushort halfWidth, long rangeSquared, long dx, long dy)
    {
        checked
        {
            if (dx * dx + dy * dy > rangeSquared)
            {
                return false;
            }

            var leftAngle = new Bam16(unchecked((ushort)(centre.Raw - halfWidth)));
            var rightAngle = new Bam16(unchecked((ushort)(centre.Raw + halfWidth)));

            var (leftX, leftY) = ConeBoundaryTable.BoundaryVector(leftAngle);
            var (rightX, rightY) = ConeBoundaryTable.BoundaryVector(rightAngle);

            long crossLeft = leftX * dy - leftY * dx;
            long crossRight = rightX * dy - rightY * dx;

            // A cone narrower than a half turn is the intersection of the
            // two half-planes: a point must be on the inner side of both
            // edges at once. A cone at least a half turn wide (including the
            // reflex case, wider than a half turn) is instead their union:
            // once the two edges are 180 degrees or more apart, "between
            // them the short way" is no longer a bounded wedge, and a point
            // only needs to clear one edge or the other.
            var narrowerThanHalfTurn = halfWidth < QuarterTurn;
            return narrowerThanHalfTurn
                ? crossLeft >= 0 && crossRight <= 0
                : crossLeft >= 0 || crossRight <= 0;
        }
    }
}
