using Sandata.Core.Mathematics;

namespace Sandata.Core.Movement;

/// <summary>
/// The one deterministic sidestep a blocked unit is allowed to try, per design
/// section 8's "Local avoidance: propose, prioritise, commit": "A blocked unit
/// first tries a single 22.5-degree sidestep, choosing the side by a rule
/// pinned on <c>entityId</c> parity so it is total; if that is also blocked,
/// it waits a tick." This type is the pure geometry half of that sentence —
/// it rotates one already-proposed direction vector by exactly one
/// <see cref="Facing16"/> sector and nothing else. It never inspects another
/// entity's position, never accumulates a second vector into the one it
/// rotates, and never repeats: <see cref="LocalAvoidance"/> calls it at most
/// once per blocked entity per tick, which is what makes "one sidestep, then
/// wait" a fact about the caller rather than a state this type has to track.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why rotation, not another vector.</b> A force-based avoidance scheme —
/// the "boids" family <c>CLAUDE.md</c> section 9 forbids as rigid-body physics
/// under another name — would answer "which way do I step" by summing a
/// repulsion term from every nearby obstacle into an accumulator and moving
/// along whatever direction the sum points. This type answers a narrower
/// question with no accumulator at all: given the one direction vector the
/// entity already proposed for itself
/// (<paramref name="deltaXRaw"/>/<paramref name="deltaYRaw"/> in
/// <see cref="Sidestep"/>), turn that single vector by a fixed angle and
/// nothing else. The turned vector's length is bounded by the input vector's
/// own length (rotation preserves magnitude up to the bounded rounding this
/// codebase's integer trigonometry always carries — see <see cref="Trig"/>'s
/// remarks) regardless of how many other bodies happen to be nearby, which is
/// the observable difference from a summed force: a crowd never makes a
/// single rotated step grow.
/// </para>
/// <para>
/// <b>The 22.5-degree magnitude.</b> Bam16 confirms the mapping this file
/// relies on: <see cref="Bam16.UnitsPerFacingSector"/> is
/// <see cref="Bam16.UnitsPerTurn"/> divided by the sixteen <see cref="Facing16"/>
/// sectors, 4,096 raw units, and a full turn is 65,536 raw units by that same
/// type's documented contract — so 4,096 raw units is exactly one sixteenth of
/// a turn, 22.5 degrees, with no rounding. <see cref="TurnMagnitudeBam"/> is
/// that constant, read from <see cref="Bam16"/> rather than restated as a
/// literal, so a change to the sector count would move this file's angle with
/// it instead of silently disagreeing.
/// </para>
/// </remarks>
internal static class SidestepRules
{
    /// <summary>
    /// The fixed sidestep turn magnitude: one <see cref="Facing16"/> sector,
    /// 4,096 raw <see cref="Bam16"/> units, 22.5 degrees. See this type's
    /// remarks for the derivation.
    /// </summary>
    internal const int TurnMagnitudeBam = Bam16.UnitsPerFacingSector;

    /// <summary>
    /// The scale <see cref="Trig.Sin"/> and <see cref="Trig.Cos"/> return
    /// their results at — 65,536 — reused here as the divisor that turns a
    /// scaled sine or cosine back into a plain raw-unit component after a
    /// rotation. It is exactly <see cref="Bam16.UnitsPerTurn"/>, not a
    /// coincidence: both were fixed at the same power of two so a Bam value
    /// and a trig scale factor stay interchangeable without a conversion
    /// constant of their own.
    /// </summary>
    private const int TrigScale = Bam16.UnitsPerTurn;

    /// <summary>
    /// The total, pinned rule that decides which of the two sides a given
    /// entity sidesteps toward: an even <paramref name="entityId"/> turns
    /// toward the positive (clockwise, per <see cref="Bam16"/>'s documented
    /// winding) side; an odd one turns toward the negative
    /// (counter-clockwise) side. Entity IDs are unique, so this rule is total
    /// over every entity that can ever propose a move, and it depends on
    /// nothing but the one entity's own ID — never on which side a neighbour
    /// chose, and never on the tick number — so the same entity always steps
    /// the same way whenever the shape of its own blocked proposal repeats.
    /// </summary>
    internal static bool TurnsTowardThePositiveSide(ulong entityId) => (entityId & 1UL) == 0UL;

    /// <summary>
    /// Rotates the direction vector
    /// (<paramref name="deltaXRaw"/>, <paramref name="deltaYRaw"/>) — a
    /// blocked unit's own already-proposed displacement, start subtracted
    /// from desired, never another entity's vector and never a sum of more
    /// than one vector — by exactly <see cref="TurnMagnitudeBam"/>, to the
    /// side <see cref="TurnsTowardThePositiveSide"/> picks for
    /// <paramref name="entityId"/>.
    /// </summary>
    /// <returns>
    /// The rotated vector, at the same integer scale as the input. When the
    /// input is the zero vector the result is the zero vector: there is no
    /// direction to turn away from, so a stationary proposal's sidestep
    /// candidate is its own start position, which is exactly the fallback
    /// <see cref="LocalAvoidance"/> would otherwise commit to.
    /// </returns>
    /// <remarks>
    /// Standard integer rotation: each output component is a sum of two
    /// products of the input components with a scaled sine or cosine, divided
    /// back down by <see cref="TrigScale"/>. The division truncates toward
    /// zero, the same rounding rule <c>SlotTargets.ComputeTarget</c> already
    /// documents for the identical situation — an integer component
    /// recovered from a scaled multiply-then-divide — so the two components
    /// of the rotated vector can round independently by at most one raw unit,
    /// a bounded and deterministic behaviour rather than an approximation
    /// that could disagree between two evaluations of the same input.
    /// </remarks>
    internal static (int DeltaXRaw, int DeltaYRaw) Sidestep(int deltaXRaw, int deltaYRaw, ulong entityId)
    {
        var turnBam = TurnsTowardThePositiveSide(entityId)
            ? unchecked((ushort)TurnMagnitudeBam)
            : unchecked((ushort)(-TurnMagnitudeBam));

        var cosRaw = Trig.Cos(turnBam);
        var sinRaw = Trig.Sin(turnBam);

        var rotatedXRaw = checked(((long)deltaXRaw * cosRaw) - ((long)deltaYRaw * sinRaw)) / TrigScale;
        var rotatedYRaw = checked(((long)deltaXRaw * sinRaw) + ((long)deltaYRaw * cosRaw)) / TrigScale;

        return (checked((int)rotatedXRaw), checked((int)rotatedYRaw));
    }
}
