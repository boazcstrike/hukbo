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
    /// <param name="generation">
    /// Which aim point this follower is on. <c>0</c> is the offset it holds for
    /// the whole battle under normal conditions, and is the default because it
    /// is the normal case; every existing call site means it. A higher
    /// generation is the stall escape: an agent blocked for
    /// <see cref="FormationRules.StallEscapeStreakTicks"/> consecutive ticks
    /// has proved its current aim point unreachable and draws a different one.
    /// </param>
    /// <returns>
    /// The offset to add to the rally agent's position, with both axes
    /// independently drawn from the closed span
    /// <c>[-jitter, +jitter]</c>.
    /// </returns>
    /// <remarks>
    /// The generation is not a back door to the fleeing-goalpost failure the
    /// type-level remarks rule out. That failure needs the aim point to move
    /// every tick; this one moves only after a run of consecutive blocked ticks
    /// well above anything a healthy battle produces, and it then holds the new
    /// point for at least as long again.
    /// </remarks>
    internal static (int XRaw, int YRaw) Compute(
        ulong seed,
        ulong entityId,
        int bodyRadiusRaw,
        int generation = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(generation);

        var jitter = FormationRules.ComputeRallyJitterRaw(bodyRadiusRaw);
        var span = checked(2 * jitter + 1);

        var hash = Fnv1a.OffsetBasis;
        Fnv1a.Add(ref hash, LastStandTag);
        Fnv1a.Add(ref hash, seed);
        Fnv1a.Add(ref hash, entityId);

        // Mixed only when it is non-zero, so that generation 0 reproduces the
        // pre-escape offset bit for bit. Mixing a zero would still perturb the
        // hash and would move every recorded state hash for a feature that has
        // not fired.
        if (generation != 0)
        {
            Fnv1a.Add(ref hash, (ulong)generation);
        }

        var generator = new SplitMix64(hash);
        var xRaw = checked(generator.NextInt(span) - jitter);
        var yRaw = checked(generator.NextInt(span) - jitter);

        return (xRaw, yRaw);
    }
}
