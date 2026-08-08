namespace Sandata.Core.Geometry;

/// <summary>
/// Ray-versus-axis-aligned-box intersection by the slab method, implemented
/// without ever dividing a parametric value.
/// </summary>
/// <remarks>
/// The classic slab method computes, for each axis, the two values of the
/// ray parameter <c>t</c> at which the ray crosses that axis's pair of
/// bounding planes: <c>t = (bound - origin) / direction</c>. That division
/// is exactly the operation this type avoids. Instead, every such bound is
/// kept as a rational pair <c>(numerator, denominator)</c> with
/// <c>denominator</c> equal to the ray's direction component, and two bounds
/// are ordered by cross-multiplying their numerators against the other
/// bound's denominator. Cross-multiplication only preserves the ordering of
/// two fractions when both denominators share a sign, so every bound is
/// normalised on construction to carry a strictly positive denominator
/// (flipping both numerator and denominator when the direction component is
/// negative). Once every bound in play has a positive denominator, ordinary
/// integer cross-multiplication never needs a sign-flip correction.
///
/// A direction component of exactly zero is not a degenerate input to be
/// rejected; it means the ray is parallel to that axis, so that axis places
/// no bound on <c>t</c> at all. In that case the method falls back to a
/// direct inside-slab test on the origin's coordinate for that axis: if the
/// origin already lies outside the slab, a ray parallel to it can never
/// enter the box on that axis, and if it lies inside, that axis contributes
/// no constraint on <c>t</c> whatsoever. When both direction components are
/// zero the "ray" is a stationary point, and both inside-slab tests already
/// decide the answer directly.
/// </remarks>
public static class RayBox
{
    /// <summary>
    /// Determines whether the ray starting at <c>(originX, originY)</c> and
    /// travelling in direction <c>(directionX, directionY)</c> intersects the
    /// axis-aligned box <c>[boxMinX, boxMaxX] x [boxMinY, boxMaxY]</c> at some
    /// parameter <c>t &gt;= 0</c>.
    /// </summary>
    /// <remarks>
    /// The box bounds are inclusive, so a ray that only grazes a corner or
    /// an edge of the box is reported as intersecting.
    /// </remarks>
    public static bool Intersects(
        long originX,
        long originY,
        long directionX,
        long directionY,
        long boxMinX,
        long boxMinY,
        long boxMaxX,
        long boxMaxY)
    {
        var xConstrained = directionX != 0;
        var yConstrained = directionY != 0;

        if (!xConstrained && (originX < boxMinX || originX > boxMaxX))
        {
            // Parallel to the X axis and already outside the X slab: no
            // value of t can ever bring the ray into the box.
            return false;
        }

        if (!yConstrained && (originY < boxMinY || originY > boxMaxY))
        {
            // Parallel to the Y axis and already outside the Y slab: same
            // reasoning on the other axis.
            return false;
        }

        if (!xConstrained && !yConstrained)
        {
            // A zero-length direction is a stationary point. Both inside-slab
            // checks above already passed, so the origin sits inside the box.
            return true;
        }

        // Exactly one of the three remaining shapes applies: only X is
        // constrained, only Y is constrained, or both are. Branching on it
        // explicitly (rather than accumulating into a nullable running
        // bound) means the compiler can see that lowerBound and upperBound
        // are always assigned, with no unproven "the nullable is populated
        // by now" step.
        Bound lowerBound;
        Bound upperBound;

        if (xConstrained && yConstrained)
        {
            var (xNear, xFar) = AxisBounds(boxMinX - originX, boxMaxX - originX, directionX);
            var (yNear, yFar) = AxisBounds(boxMinY - originY, boxMaxY - originY, directionY);
            lowerBound = Max(xNear, yNear);
            upperBound = Min(xFar, yFar);
        }
        else if (xConstrained)
        {
            (lowerBound, upperBound) = AxisBounds(boxMinX - originX, boxMaxX - originX, directionX);
        }
        else
        {
            (lowerBound, upperBound) = AxisBounds(boxMinY - originY, boxMaxY - originY, directionY);
        }

        // The ray intersects the box when the entry parameter does not come
        // after the exit parameter, and the exit parameter is not behind the
        // ray's origin. Denominator is always positive by construction, so
        // "upperBound.Numerator >= 0" alone decides "upperBound >= 0" with no
        // division.
        return Compare(lowerBound, upperBound) <= 0 && upperBound.Numerator >= 0;
    }

    /// <summary>
    /// Turns one axis's raw numerators into an ordered <c>(near, far)</c>
    /// bound pair. The direction component decides which of the two
    /// candidate raw numerators is the near bound and which is the far
    /// bound: a positive direction keeps the box's minimum-coordinate
    /// numerator as the near bound, and a negative direction swaps the two,
    /// because dividing by a negative number reverses which raw fraction is
    /// the smaller value.
    /// </summary>
    private static (Bound Near, Bound Far) AxisBounds(long numeratorAtMin, long numeratorAtMax, long direction) =>
        direction > 0
            ? (new Bound(numeratorAtMin, direction), new Bound(numeratorAtMax, direction))
            : (new Bound(numeratorAtMax, direction), new Bound(numeratorAtMin, direction));

    /// <summary>
    /// Orders two bounds by cross-multiplying their numerators against the
    /// other bound's denominator. Both denominators are guaranteed strictly
    /// positive by <see cref="Bound"/>'s constructor, so this comparison
    /// never needs a sign-flip correction.
    /// </summary>
    private static int Compare(Bound left, Bound right)
    {
        var crossLeft = left.Numerator * right.Denominator;
        var crossRight = right.Numerator * left.Denominator;
        return crossLeft.CompareTo(crossRight);
    }

    private static Bound Max(Bound left, Bound right) => Compare(left, right) >= 0 ? left : right;

    private static Bound Min(Bound left, Bound right) => Compare(left, right) <= 0 ? left : right;

    /// <summary>
    /// A parametric bound <c>t = Numerator / Denominator</c>, held as a
    /// rational pair and never actually divided. The constructor normalises
    /// the sign so <see cref="Denominator"/> is always strictly positive,
    /// which is what lets every later comparison use plain cross-
    /// multiplication with no sign-flip case.
    /// </summary>
    private readonly struct Bound
    {
        public Bound(long numerator, long denominator)
        {
            if (denominator < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            Numerator = numerator;
            Denominator = denominator;
        }

        public long Numerator { get; }

        public long Denominator { get; }
    }
}
