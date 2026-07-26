namespace Hukbo.Core.Simulation;

/// <summary>
/// Deterministic integer geometry for solid-disc contact between agent bodies.
/// Every living agent is a disc of one common radius, so a pair test reduces to
/// comparing the squared centre distance against the squared body diameter.
/// </summary>
/// <remarks>
/// <para>
/// All arithmetic is checked <c>long</c> arithmetic over raw fixed-point
/// coordinates. No floating-point value, vector type, or renderer geometry
/// participates, so every result is bit-identical across runs and machines.
/// </para>
/// <para>
/// These helpers describe static discs only. Swept geometry is deliberately
/// absent: scenario validation enforces
/// <c>MovementSpeedRaw &lt;= BodyRadiusRaw</c>, so two agents close at most one
/// diameter minus one raw unit per tick and can never tunnel through or swap
/// past each other between committed positions.
/// </para>
/// </remarks>
internal static class CollisionGeometry
{
    /// <summary>
    /// Squared distance between two body centres, in squared raw units.
    /// </summary>
    /// <remarks>
    /// At the largest validated coordinate, <c>1,000,000 * FixedPoint.Scale</c>
    /// = <c>1,024,000,000</c> raw units on both axes, the result is
    /// <c>2,097,152,000,000,000,000</c>, which is roughly a quarter of
    /// <c>long.MaxValue</c>. The widening to <c>long</c> therefore removes every
    /// overflow case reachable from a valid scenario.
    /// </remarks>
    internal static long SquaredDistance(
        int aXRaw,
        int aYRaw,
        int bXRaw,
        int bYRaw)
    {
        var deltaX = (long)bXRaw - aXRaw;
        var deltaY = (long)bYRaw - aYRaw;
        return checked((deltaX * deltaX) + (deltaY * deltaY));
    }

    /// <summary>
    /// The squared centre distance at which two bodies of
    /// <paramref name="bodyRadiusRaw"/> exactly touch, that is
    /// <c>(2 * bodyRadiusRaw)^2</c>.
    /// </summary>
    internal static long ContactSquaredDistance(int bodyRadiusRaw)
    {
        var diameterRaw = checked(2L * bodyRadiusRaw);
        return checked(diameterRaw * diameterRaw);
    }

    /// <summary>
    /// True when two bodies strictly penetrate. Exact tangency is clearance, not
    /// collision, so the comparison is strict:
    /// <c>squaredDistance &lt; (2 * bodyRadiusRaw)^2</c>. This is the invariant a
    /// committed position must never violate.
    /// </summary>
    internal static bool Overlaps(
        int aXRaw,
        int aYRaw,
        int bXRaw,
        int bYRaw,
        int bodyRadiusRaw) =>
        SquaredDistance(aXRaw, aYRaw, bXRaw, bYRaw) <
        ContactSquaredDistance(bodyRadiusRaw);

    /// <summary>
    /// True when two bodies touch or penetrate, that is
    /// <c>squaredDistance &lt;= (2 * bodyRadiusRaw)^2</c>. This is the
    /// pair-candidate predicate: it admits the tangent resting position that
    /// <see cref="Overlaps"/> deliberately rejects.
    /// </summary>
    internal static bool IsContact(
        int aXRaw,
        int aYRaw,
        int bXRaw,
        int bYRaw,
        int bodyRadiusRaw) =>
        SquaredDistance(aXRaw, aYRaw, bXRaw, bYRaw) <=
        ContactSquaredDistance(bodyRadiusRaw);

    /// <summary>
    /// True when two body centres are exactly equal. Coincident centres are an
    /// invalid input state that the resolver repairs rather than a position
    /// normal ticking can produce.
    /// </summary>
    internal static bool IsCoincident(
        int aXRaw,
        int aYRaw,
        int bXRaw,
        int bYRaw) =>
        aXRaw == bXRaw && aYRaw == bYRaw;

    /// <summary>
    /// Clamps one axis of a body centre into
    /// <c>[bodyRadiusRaw, dimensionRaw - bodyRadiusRaw]</c> so that the body
    /// never crosses the map edge on that axis. Axes are clamped independently;
    /// corner contact is simply both axes clamping in the same tick.
    /// </summary>
    /// <remarks>
    /// Degenerate branch: when <paramref name="dimensionRaw"/> is smaller than
    /// <c>2 * bodyRadiusRaw</c> the target interval is empty, and when it is
    /// exactly <c>2 * bodyRadiusRaw</c> the interval collapses to a single point.
    /// Neither case can arise from a validated scenario, because
    /// <c>Scenario.Validate</c> rejects a map narrower than one body. Rather than
    /// throw from a per-tick hot path, both cases degrade deterministically to the
    /// low bound, <paramref name="bodyRadiusRaw"/>, which is the collapsed point
    /// in the exact case and a stable, input-independent choice in the empty one.
    /// </remarks>
    internal static int ClampCenterToBounds(
        int valueRaw,
        int dimensionRaw,
        int bodyRadiusRaw)
    {
        var lowBoundRaw = (long)bodyRadiusRaw;
        var highBoundRaw = (long)dimensionRaw - bodyRadiusRaw;

        if (highBoundRaw <= lowBoundRaw)
        {
            return bodyRadiusRaw;
        }

        if (valueRaw < lowBoundRaw)
        {
            return bodyRadiusRaw;
        }

        if (valueRaw > highBoundRaw)
        {
            return checked((int)highBoundRaw);
        }

        return valueRaw;
    }
}
