namespace Hukbo.Core.Simulation;

/// <summary>
/// Approved constants for the last-stand rally formation. These are
/// game-design inventions, not historical measurements — no source describes
/// a rally radius or a formation headcount.
/// </summary>
/// <remarks>
/// <para>
/// The rally jitter radius is <see cref="RallyJitterRadiusMultiplier"/> times
/// the body radius. Each follower draws its jitter offset independently on
/// both axes from the closed span
/// <c>[-jitter, +jitter]</c>, so the bias square a follower can land in has
/// side <c>2 * jitter = 2 * (RallyJitterRadiusMultiplier * bodyRadius) =
/// 8 * bodyRadius</c>.
/// </para>
/// <para>
/// A living body occupies a square of side <c>2 * bodyRadius</c> (its
/// bounding box). Dividing the bias square's side by the body square's side
/// gives <c>(8 * bodyRadius) / (2 * bodyRadius) = 4</c>, and squaring that
/// ratio for two dimensions gives <c>4 * 4 = 16</c> non-overlapping bodies
/// that fit inside the bias square. That is the derivation of
/// <see cref="MaximumLastStandThresholdAgents"/>: past sixteen agents, the
/// jitter square can no longer plausibly separate every follower, and the
/// collision resolver would spend its slack fighting an over-packed cluster
/// instead of a battle.
/// </para>
/// </remarks>
public static class FormationRules
{
    /// <summary>
    /// The living-agent count, per faction, at or below which the last-stand
    /// rally behaviour engages by default. A game-design choice, not a
    /// measurement.
    /// </summary>
    public const int DefaultLastStandThresholdAgents = 6;

    /// <summary>
    /// The highest last-stand threshold a scenario may configure. See the
    /// type-level remarks for the square-packing derivation of sixteen.
    /// </summary>
    public const int MaximumLastStandThresholdAgents = 16;

    /// <summary>
    /// How many body radii out the rally jitter square extends from the rally
    /// agent's centre, on each axis. A game-design choice, not a measurement.
    /// </summary>
    public const int RallyJitterRadiusMultiplier = 4;

    /// <summary>
    /// Computes the rally jitter radius, in raw fixed-point units, for a body
    /// of the given radius: <c>RallyJitterRadiusMultiplier * bodyRadiusRaw</c>.
    /// </summary>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units. Must be positive.
    /// </param>
    /// <returns>The rally jitter radius, in raw fixed-point units.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="bodyRadiusRaw"/> is not positive, or when
    /// the jitter span <c>2 * jitter + 1 = 8 * bodyRadiusRaw + 1</c> would
    /// overflow <see cref="int"/>. That span is the exclusive upper bound a
    /// caller passes to <c>SplitMix64.NextInt</c>, which takes an
    /// <see cref="int"/>.
    /// </exception>
    public static int ComputeRallyJitterRaw(int bodyRadiusRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bodyRadiusRaw);

        if (8L * bodyRadiusRaw + 1 > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bodyRadiusRaw),
                bodyRadiusRaw,
                "Body radius is too large: the rally jitter span " +
                "(8 * bodyRadiusRaw + 1) would overflow Int32.");
        }

        return checked(RallyJitterRadiusMultiplier * bodyRadiusRaw);
    }
}
