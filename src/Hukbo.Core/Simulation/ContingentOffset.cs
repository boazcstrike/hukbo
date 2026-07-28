using Hukbo.Core.Determinism;

namespace Hukbo.Core.Simulation;

/// <summary>
/// Stateless deterministic per-member jitter offset for persistent-contingent
/// cohesion. The same scenario seed and entity ID always resolve to the same
/// offset; no <see cref="System.Random"/>, wall-clock time, or mutable state
/// is used.
/// </summary>
/// <remarks>
/// The key deliberately excludes the tick, for the reason
/// <see cref="RallyOffset"/>'s own remarks
/// (<c>src/Hukbo.Core/Simulation/RallyOffset.cs:11-21</c>) record: a
/// tick-keyed offset would move every member's aim point every tick, which
/// reproduces the jitter/stall failure named in
/// <c>docs/research/FORMATION_AND_COLLISION_MECHANICS.md</c> — a member
/// chasing an aim point that shifts a fraction of a unit on every tick never
/// settles, and the collision resolver spends its slack fighting a moving
/// goalpost instead of letting the contingent converge. Keying only on the
/// seed and the entity ID gives each member one stable offset for the whole
/// battle.
/// </remarks>
internal static class ContingentOffset
{
    private const ulong ContingentTag = 0x484B424F5F435447UL;

    /// <summary>
    /// The half-width of the raw draw taken before scaling into world units,
    /// so a member's offset is resolved to the same precision regardless of
    /// the jitter radius it is scaled against. Mirrors
    /// <see cref="Movement.MovementRuleset.OffsetUnit"/>, whose registered
    /// value is this same 1024.
    /// </summary>
    private const int OffsetUnit = 1024;

    /// <summary>
    /// Computes the deterministic jitter offset, in raw fixed-point units,
    /// that one contingent member adds to its contingent's trail base to
    /// find its own aim point.
    /// </summary>
    /// <param name="seed">The scenario seed.</param>
    /// <param name="entityId">The member entity's ID.</param>
    /// <param name="jitterRaw">
    /// The contingent jitter radius, in raw fixed-point units, that the unit
    /// draw is scaled against. Must be non-negative.
    /// </param>
    /// <returns>
    /// The offset to add to the contingent's trail base, with both axes
    /// independently drawn from the closed span
    /// <c>[-jitterRaw, +jitterRaw]</c>.
    /// </returns>
    internal static (int XRaw, int YRaw) Compute(
        ulong seed,
        ulong entityId,
        int jitterRaw)
    {
        const int Span = 2 * OffsetUnit + 1;

        var hash = Fnv1a.OffsetBasis;
        Fnv1a.Add(ref hash, ContingentTag);
        Fnv1a.Add(ref hash, seed);
        Fnv1a.Add(ref hash, entityId);

        var generator = new SplitMix64(hash);
        var xUnit = generator.NextInt(Span) - OffsetUnit;
        var yUnit = generator.NextInt(Span) - OffsetUnit;

        var xRaw = checked((int)((long)xUnit * jitterRaw / OffsetUnit));
        var yRaw = checked((int)((long)yUnit * jitterRaw / OffsetUnit));

        return (xRaw, yRaw);
    }
}
