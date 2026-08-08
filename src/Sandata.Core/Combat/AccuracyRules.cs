using Hukbo.Core.Determinism;
using Sandata.Core.Determinism;

namespace Sandata.Core.Combat;

/// <summary>
/// Design section 9's "accuracy interpolation": a firearm's angular
/// dispersion grows linearly with range up to a clamp, and the shot's actual
/// angular error is a deterministic draw from that dispersion, taken from the
/// named <c>Accuracy</c> RNG stream.
/// </summary>
public static class AccuracyRules
{
    /// <summary>
    /// Design section 9's linear integer dispersion interpolation:
    /// <c>DispersionAtZeroWu + (DispersionAtMaxWu - DispersionAtZeroWu) *
    /// min(range, MaxEffectiveWu) / MaxEffectiveWu</c>, using C#'s
    /// truncating integer division. The clamp on <paramref name="rangeWu"/>
    /// happens before the interpolation, not after, so a range beyond
    /// <paramref name="maxEffectiveWu"/> yields exactly
    /// <paramref name="dispersionAtMaxWu"/> — the division by
    /// <paramref name="maxEffectiveWu"/> then cancels exactly rather than
    /// truncating away from it.
    /// </summary>
    /// <param name="rangeWu">The current engagement range, in world units. Never negative.</param>
    /// <param name="dispersionAtZeroWu"><see cref="Weapons.FirearmDefinition.DispersionAtZeroWu"/>. Never negative.</param>
    /// <param name="dispersionAtMaxWu"><see cref="Weapons.FirearmDefinition.DispersionAtMaxWu"/>. Never negative.</param>
    /// <param name="maxEffectiveWu"><see cref="Weapons.FirearmDefinition.MaxEffectiveWu"/>. Must be positive.</param>
    /// <returns>
    /// The interpolated dispersion, in Bam, at <paramref name="rangeWu"/>,
    /// clamped so no value beyond <paramref name="maxEffectiveWu"/> ever
    /// exceeds <paramref name="dispersionAtMaxWu"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="rangeWu"/>, <paramref name="dispersionAtZeroWu"/>, or
    /// <paramref name="dispersionAtMaxWu"/> is negative, or
    /// <paramref name="maxEffectiveWu"/> is not positive.
    /// </exception>
    public static int Dispersion(
        int rangeWu,
        int dispersionAtZeroWu,
        int dispersionAtMaxWu,
        int maxEffectiveWu)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rangeWu);
        ArgumentOutOfRangeException.ThrowIfNegative(dispersionAtZeroWu);
        ArgumentOutOfRangeException.ThrowIfNegative(dispersionAtMaxWu);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxEffectiveWu, 0);

        var clampedRangeWu = Math.Min(rangeWu, maxEffectiveWu);

        // Widened to long before the multiply: the delta and the clamped
        // range are each bounded by today's 38-weapon roster, but nothing
        // here should silently overflow a 32-bit product for a future,
        // longer-ranged weapon.
        var delta = (long)dispersionAtMaxWu - dispersionAtZeroWu;
        var interpolated = (delta * clampedRangeWu) / maxEffectiveWu;

        return (int)(dispersionAtZeroWu + interpolated);
    }

    /// <summary>
    /// Draws this shot's angular error, in Bam, from the named
    /// <c>Accuracy</c> RNG stream — <see cref="SplitMix64"/> only, never
    /// <see cref="System.Random"/> — keyed on <c>(missionSeed, Accuracy,
    /// entityId)</c> exactly as design section 4's "ordering and randomness"
    /// rule requires: a stream derived from the mission seed, a fixed system
    /// tag, and the entity drawing from it, so that adding a draw in one
    /// system can never shift an unrelated outcome.
    /// </summary>
    /// <remarks>
    /// The stream key folds <paramref name="missionSeed"/>, then
    /// <see cref="SandataSystemTag.Accuracy"/>, then <paramref name="entityId"/>,
    /// in that order — the same order the design line states the key's three
    /// components — through <see cref="SandataHash"/>, Sandata's own folding
    /// wrapper over the shared <see cref="Fnv1a"/> primitive. The folded
    /// value seeds a fresh <see cref="SplitMix64"/> generator, and exactly
    /// one draw is taken from it, so the same three inputs always reproduce
    /// the same angular error. This method is stateless and does not persist
    /// or advance a stream across calls; a future task that wires this draw
    /// into a live <see cref="Simulation.RngStreamState"/> owns deciding
    /// whether successive shots by the same entity draw from a persisted,
    /// advancing generator or re-derive a fresh one each time — this method
    /// only implements the single-draw formula design section 9 specifies.
    /// <paramref name="entityId"/> folds through the same
    /// <c>unchecked((long)value)</c> bit-reinterpretation
    /// <paramref name="missionSeed"/> and <c>PathService</c>'s
    /// <c>GroupId</c> already use elsewhere in this fold, so every entity id
    /// that also fits in a non-negative <see langword="int"/> — every id
    /// task 64's widening pass actually produces — folds to the identical
    /// <see langword="long"/> value, and therefore draws the identical
    /// angular error, this method produced before its parameter widened from
    /// <see langword="int"/> to <see langword="ulong"/>
    /// (<c>AccuracyRulesTests.DrawAngularErrorBam_PinnedDraws</c> proves this
    /// for three concrete triples).
    /// </remarks>
    /// <param name="missionSeed">The mission's root seed.</param>
    /// <param name="entityId">
    /// The firing entity's ID. <see cref="Simulation.OperatorState.EntityId"/>
    /// is <see langword="ulong"/>; this parameter matches it exactly, so a
    /// caller never narrows the entity id to reach this method.
    /// </param>
    /// <param name="dispersionBam">
    /// The dispersion computed by <see cref="Dispersion"/> for this shot's
    /// range. Never negative. The draw is uniform over the
    /// <c>2 * dispersionBam + 1</c> integers in the closed span
    /// <c>[-dispersionBam, +dispersionBam]</c>.
    /// </param>
    /// <returns>
    /// The signed angular error, in Bam, in the closed span
    /// <c>[-dispersionBam, +dispersionBam]</c>. Always <c>0</c> when
    /// <paramref name="dispersionBam"/> is <c>0</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dispersionBam"/> is negative.</exception>
    public static int DrawAngularErrorBam(ulong missionSeed, ulong entityId, int dispersionBam)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dispersionBam);

        var hash = SandataHash.Begin();
        SandataHash.Fold(ref hash, unchecked((long)missionSeed));
        SandataHash.Fold(ref hash, (long)SandataSystemTag.Accuracy);
        SandataHash.Fold(ref hash, unchecked((long)entityId));

        var generator = new SplitMix64(hash);

        // 2 * dispersionBam + 1 is always at least 1 (when dispersionBam is
        // 0), so SplitMix64.NextInt's positive-bound requirement always
        // holds without a separate zero-dispersion branch.
        var span = checked((2 * dispersionBam) + 1);
        var draw = generator.NextInt(span);

        return draw - dispersionBam;
    }
}
