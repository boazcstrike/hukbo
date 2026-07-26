using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// The reference oracle for collision pair generation. It is a deliberately
/// naive O(n^2) double loop whose only job is to be obviously correct, so that
/// any optimised production broad phase can be compared against it.
/// </summary>
/// <remarks>
/// This type must never call <c>CollisionGeometry</c>, <c>CollisionRules</c>,
/// or any other production helper. Its independence from the code under test
/// is the entire reason it exists, so the squared-distance arithmetic is
/// written out inline here even though the simulation has its own helper.
/// Do not optimise this class; it is a correctness oracle, not a hot path.
/// </remarks>
internal static class NaiveCollisionPairs
{
    /// <summary>
    /// Enumerates every unordered pair of living bodies in contact, sorted by
    /// <see cref="CollisionPair.CompareTo"/>.
    /// </summary>
    /// <remarks>
    /// The contact predicate is
    /// <c>squaredDistance &lt;= (2 * bodyRadiusRaw)^2</c>, so exact tangency
    /// counts as contact and is reported as a pair. This is intentionally
    /// wider than the post-tick overlap invariant of the collision policy
    /// decision record, which forbids only the strict inequality
    /// <c>squaredDistance &lt; (2R)^2</c>. Pair generation is allowed to
    /// surface the tangent case; the resolver is what decides that tangency is
    /// a legal resting position.
    /// </remarks>
    /// <param name="bodies">The bodies to test. Dead bodies are skipped.</param>
    /// <param name="bodyRadiusRaw">The common body radius in raw units.</param>
    internal static List<CollisionPair> Enumerate(
        IReadOnlyList<CollisionBody> bodies,
        int bodyRadiusRaw)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        var diameterRaw = 2L * bodyRadiusRaw;
        var contactThreshold = checked(diameterRaw * diameterRaw);
        var pairs = new List<CollisionPair>();

        for (var leftIndex = 0; leftIndex < bodies.Count; leftIndex++)
        {
            var left = bodies[leftIndex];

            if (!left.IsAlive)
            {
                continue;
            }

            for (var rightIndex = leftIndex + 1; rightIndex < bodies.Count; rightIndex++)
            {
                var right = bodies[rightIndex];

                if (!right.IsAlive)
                {
                    continue;
                }

                var deltaX = (long)right.XRaw - left.XRaw;
                var deltaY = (long)right.YRaw - left.YRaw;
                var squaredDistance = checked((deltaX * deltaX) + (deltaY * deltaY));

                if (squaredDistance <= contactThreshold)
                {
                    pairs.Add(CollisionPair.Create(left.EntityId, right.EntityId));
                }
            }
        }

        pairs.Sort();
        return pairs;
    }
}
