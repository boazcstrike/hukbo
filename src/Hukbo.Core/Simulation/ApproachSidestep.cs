using Hukbo.Core.Determinism;

namespace Hukbo.Core.Simulation;

/// <summary>
/// Stateless deterministic lateral displacement for a pursuing warrior that a
/// comrade's body has refused for long enough to prove its line of approach
/// unwalkable. The same scenario seed, entity ID and stall generation always
/// resolve to the same offset; no <see cref="System.Random"/>, wall-clock time,
/// or mutable state is used.
/// </summary>
/// <remarks>
/// <para>
/// This is the pursuit-path counterpart of <see cref="RallyOffset"/>. The rally
/// escape frees a warrior whose intent is <see cref="AgentIntent.Regrouping"/>
/// and reaches nothing else; a warrior walking at an enemy takes a different
/// branch of the movement-proposal loop entirely and, before this type existed,
/// had no escape at all. Two such warriors were enough to hold a whole battle
/// open to the tick limit. See
/// the 2026-07-29 approach sidestep design section 2 for the
/// measurement.
/// </para>
/// <para>
/// The key deliberately excludes the tick, for the reason
/// <see cref="RallyOffset"/>'s type-level remarks give: an aim point that moves
/// every tick is a fleeing goalpost, and the collision resolver spends its slack
/// chasing it instead of letting the approach converge. The offset changes only
/// when the stall generation does, which is once per unbroken run of
/// <see cref="FormationRules.StallEscapeStreakTicks"/> blocked ticks.
/// </para>
/// </remarks>
internal static class ApproachSidestep
{
    private const ulong ApproachTag = 0x484B424F5F41505FUL;

    /// <summary>
    /// Computes the deterministic lateral offset, in raw fixed-point units,
    /// that a blocked pursuer adds to its target's position to find an aim
    /// point beside that target rather than at its centre.
    /// </summary>
    /// <param name="seed">The scenario seed.</param>
    /// <param name="entityId">The pursuing entity's ID.</param>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units.
    /// </param>
    /// <param name="generation">
    /// Which aim point this pursuer is on. <c>0</c> means it has not been
    /// blocked long enough to have proved anything, and is the overwhelmingly
    /// common case.
    /// </param>
    /// <param name="deltaXRaw">Target X minus pursuer X, in raw units.</param>
    /// <param name="deltaYRaw">Target Y minus pursuer Y, in raw units.</param>
    /// <param name="distanceRaw">
    /// The length of that approach vector, in raw units, as the caller has
    /// already computed it.
    /// </param>
    /// <returns>
    /// The offset to add to the target's position, perpendicular to the
    /// approach vector, with length inside the closed span
    /// <c>[ApproachSidestepMinimumMultiplier * R,
    /// ApproachSidestepMaximumMultiplier * R]</c> up to integer truncation.
    /// <c>(0, 0)</c> whenever the sidestep does not apply.
    /// </returns>
    /// <remarks>
    /// Generation 0 returns before the hash is built rather than hashing to a
    /// value that happens to be zero. That is structural, and it is the whole
    /// reason this feature moves no recorded hash: every battle in which no
    /// agent accumulates a full stall streak computes exactly the aim points it
    /// computed before this type existed.
    /// </remarks>
    internal static (int XRaw, int YRaw) Compute(
        ulong seed,
        ulong entityId,
        int bodyRadiusRaw,
        int generation,
        long deltaXRaw,
        long deltaYRaw,
        long distanceRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(generation);

        if (generation == 0 || distanceRaw <= 0)
        {
            return (0, 0);
        }

        var minimumRaw = checked(
            FormationRules.ApproachSidestepMinimumMultiplier * bodyRadiusRaw);
        var maximumRaw = checked(
            FormationRules.ApproachSidestepMaximumMultiplier * bodyRadiusRaw);
        var span = checked(maximumRaw - minimumRaw + 1);

        var hash = Fnv1a.OffsetBasis;
        Fnv1a.Add(ref hash, ApproachTag);
        Fnv1a.Add(ref hash, seed);
        Fnv1a.Add(ref hash, entityId);
        Fnv1a.Add(ref hash, (ulong)generation);

        var generator = new SplitMix64(hash);
        var magnitudeRaw = checked(minimumRaw + generator.NextInt(span));

        // Which of the two perpendiculars this warrior takes is drawn rather
        // than fixed, so two warriors blocked against each other do not
        // systematically step the same way and stay blocked.
        var sign = generator.NextInt(2) == 0 ? 1 : -1;

        // The perpendicular of (dx, dy) is (-dy, dx). Both products are widened
        // to long before the multiply: on a large map a raw delta is in the
        // millions and the magnitude is in the thousands, so the product
        // overflows int well before the division brings it back into range.
        var offsetXRaw = checked(-deltaYRaw * magnitudeRaw * sign) / distanceRaw;
        var offsetYRaw = checked(deltaXRaw * magnitudeRaw * sign) / distanceRaw;

        return (checked((int)offsetXRaw), checked((int)offsetYRaw));
    }
}
