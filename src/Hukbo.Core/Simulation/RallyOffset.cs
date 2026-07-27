using Hukbo.Core.Determinism;

namespace Hukbo.Core.Simulation;

/// <summary>
/// Stateless deterministic per-follower jitter offset for the last-stand
/// rally formation. The same scenario seed and entity ID always resolve to
/// the same offset; no <see cref="System.Random"/>, wall-clock time, or
/// mutable state is used.
/// </summary>
/// <remarks>
/// The key deliberately excludes the tick. A tick-keyed offset would move
/// every warrior's aim point every tick, which produces exactly the
/// jitter/stall failure named in
/// <c>docs/research/FORMATION_AND_COLLISION_MECHANICS.md</c>: a follower
/// chasing a target that flees a fraction of a unit on every tick never
/// settles, and the collision resolver spends its slack fighting a moving
/// goalpost instead of letting the formation converge. Keying only on the
/// seed and the entity ID gives each follower one stable offset for the
/// whole battle.
/// </remarks>
internal static class RallyOffset
{
    private const ulong LastStandTag = 0x484B424F5F4C5354UL;

    /// <summary>
    /// Computes the deterministic jitter offset, in raw fixed-point units,
    /// that one follower adds to its rally agent's position to find its own
    /// aim point.
    /// </summary>
    /// <param name="seed">The scenario seed.</param>
    /// <param name="entityId">The following entity's ID.</param>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units. Must be positive,
    /// and small enough that <see cref="FormationRules.ComputeRallyJitterRaw"/>
    /// accepts it.
    /// </param>
    /// <returns>
    /// The offset to add to the rally agent's position, with both axes
    /// independently drawn from the closed span
    /// <c>[-jitter, +jitter]</c>.
    /// </returns>
    internal static (int XRaw, int YRaw) Compute(
        ulong seed,
        ulong entityId,
        int bodyRadiusRaw)
    {
        var jitter = FormationRules.ComputeRallyJitterRaw(bodyRadiusRaw);
        var span = checked(2 * jitter + 1);

        var hash = Fnv1a.OffsetBasis;
        Fnv1a.Add(ref hash, LastStandTag);
        Fnv1a.Add(ref hash, seed);
        Fnv1a.Add(ref hash, entityId);

        var generator = new SplitMix64(hash);
        var xRaw = checked(generator.NextInt(span) - jitter);
        var yRaw = checked(generator.NextInt(span) - jitter);

        return (xRaw, yRaw);
    }
}
