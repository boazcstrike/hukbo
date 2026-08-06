using Hukbo.Core.Determinism;

namespace Hukbo.Core.Simulation;

/// <summary>
/// The order movers are considered in during the collision stage, recomputed
/// every tick.
/// </summary>
/// <remarks>
/// <para>
/// Contested ground goes to whichever mover is committed first, so whatever
/// decides that order decides who advances. Ascending <c>EntityId</c> used to
/// decide it, and because faction 0 holds the low IDs, faction 0 won every
/// cross-faction contest of the entire battle. That is a standing advantage,
/// and once the mirrored deployment removed the spawn noise that had masked
/// it, it decided 19 of 20 seeds. See
/// <c>docs/archives/2026-07-27/2026-07-27-collision-priority-fairness-design.md</c>.
/// </para>
/// <para>
/// The key below reshuffles priority every tick, so no agent and therefore no
/// faction holds a standing advantage. It consumes no random stream: like
/// hit-location selection, it is a pure hash of the scenario seed, the tick, and
/// the agent, so a resumed save reproduces it exactly and no other consumer's
/// draws shift.
/// </para>
/// </remarks>
internal static class CollisionPriority
{
    /// <summary>
    /// Domain separator, the ASCII bytes of <c>HKBO_PRI</c>. Keeps this mixer's
    /// output independent of the hit-location mixer even for an identical
    /// seed-and-tick pair.
    /// </summary>
    private const ulong CollisionPriorityTag = 0x484B424F5F505249UL;

    /// <summary>
    /// The sort key for one mover on one tick. Lower sorts first and therefore
    /// wins contested ground.
    /// </summary>
    /// <remarks>
    /// The high half is the pseudorandom mix; the low half is the entity ID, so
    /// two agents whose mixes collide in their top 32 bits still compare in a
    /// strict, stable order. That keeps the resolver's ordering total and
    /// algorithm-independent, which is what <c>CLAUDE.md</c> §5 requires, and it
    /// preserves the documented "ties break on stable EntityId" rule underneath
    /// the shuffle.
    /// </remarks>
    /// <param name="seed">The scenario seed.</param>
    /// <param name="tick">The tick being resolved. Must be nonnegative.</param>
    /// <param name="entityId">
    /// The mover. Must be positive and below 2^32; entity IDs are bounded by
    /// twice <see cref="Scenario.MaximumAgentsPerFaction"/>, so a value that
    /// large is a caller defect rather than a supported population.
    /// </param>
    internal static ulong Resolve(ulong seed, long tick, ulong entityId)
    {
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick),
                tick,
                "Tick must be nonnegative.");
        }

        if (entityId == 0 || entityId > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entityId),
                entityId,
                "Entity ID must be positive and fit in 32 bits.");
        }

        return (Mix(seed, tick, entityId) & 0xFFFF_FFFF_0000_0000UL) | entityId;
    }

    /// <summary>
    /// The deterministic FNV-1a mix for one priority tuple. Internal rather than
    /// private so golden vectors can be checked without going through the
    /// entity-ID composition.
    /// </summary>
    internal static ulong Mix(ulong seed, long tick, ulong entityId)
    {
        var hash = Fnv1a.OffsetBasis;
        Fnv1a.Add(ref hash, CollisionPriorityTag);
        Fnv1a.Add(ref hash, seed);
        Fnv1a.Add(ref hash, unchecked((ulong)tick));
        Fnv1a.Add(ref hash, entityId);
        return hash;
    }
}
