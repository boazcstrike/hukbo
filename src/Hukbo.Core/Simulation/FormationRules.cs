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
/// gives <c>RallyJitterRadiusMultiplier</c>, and squaring that ratio for two
/// dimensions gives <c>RallyJitterRadiusMultiplier ^ 2</c> non-overlapping
/// bodies that fit inside the bias square. That is the square's total
/// <i>capacity</i>.
/// </para>
/// <para>
/// Capacity is emphatically <b>not</b> a safe headcount, and an earlier
/// revision of this design made exactly that mistake. Filling the bias square
/// to capacity demands perfect packing from offsets that are drawn at random,
/// so in practice every follower overlaps someone, the collision resolver
/// blocks the whole cluster, and — because a rally agent is surrounded by its
/// own followers — even the exempt leader cannot move. Two such factions never
/// touch, and the battle runs to the tick limit with no casualties at all.
/// That failure was observed directly: at a threshold equal to capacity, a
/// sixteen-versus-sixteen battle ended in a forced draw at tick 10,000 with
/// both factions still at full strength and a longest blocked streak of 9,975
/// ticks.
/// </para>
/// <para>
/// <see cref="MaximumLastStandThresholdAgents"/> therefore leaves a fourfold
/// area margin: the bias square must be able to hold four times the permitted
/// headcount, so the permitted headcount is <c>capacity / 4</c>, which is
/// <c>RallyJitterRadiusMultiplier ^ 2 / 4</c>. Bodies then cover at most a
/// quarter of the bias square and the resolver always has room to separate
/// them. The multiplier is set to six so that this ceiling lands at nine,
/// which comfortably admits the default threshold of six.
/// </para>
/// <para>
/// The packing margin above prevents followers from blocking <i>each
/// other</i>, but it does nothing to stop a follower from parking in front of
/// its own rally agent's line of travel: a jitter offset drawn uniformly from
/// the bias square can point straight down the rally agent's forward arc,
/// and a follower that lands there blocks the very agent it is following —
/// forever, since the rally agent is exempt from regrouping and never
/// reroutes around its own formation. Two factions doing this simultaneously
/// deadlock the whole battle at the tick limit with zero casualties. The fix
/// is to aim followers <see cref="RallyTrailRadiusMultiplier"/> body radii
/// <b>behind</b> the rally agent, opposite its direction of travel, before
/// applying the jitter offset, so the rally agent's forward arc is always
/// clear.
/// </para>
/// <para>
/// The trail distance must clear the worst-case forward encroachment the
/// jitter offset alone could produce. Jitter is independently drawn per axis
/// from <c>[-J, +J]</c> where <c>J = RallyJitterRadiusMultiplier * R</c>, so
/// it is Chebyshev-bounded: the offset's projection onto any direction
/// (including the rally agent's direction of travel) is at most
/// <c>J * sqrt(2) (~= 8.49 * R)</c>, reached when both axes hit their extreme
/// simultaneously and the direction is the diagonal of the bias square. With
/// <c>RallyTrailRadiusMultiplier</c> set to 12, the trail places the
/// follower's unjittered aim point <c>12 * R</c> behind the rally agent, so
/// even the worst-case forward jitter leaves
/// <c>12 * R - 8.49 * R = 3.51 * R</c> of clearance beyond the rally agent's
/// position — comfortably past the <c>2 * R</c> contact distance at which two
/// bodies would actually touch. In general
/// <c>RallyTrailRadiusMultiplier</c> must always exceed
/// <c>RallyJitterRadiusMultiplier * sqrt(2) + 2</c> (the <c>2</c> being the
/// two body radii of contact distance in units of <c>R</c>); changing either
/// multiplier requires rechecking this inequality.
/// </para>
/// <para>
/// The trail alone does not fully solve the blocking problem: a follower can
/// still be jittered ahead of its rally agent's own tick-start position, in
/// which case reaching a trail point behind the leader means walking
/// backward straight through the leader's body. Straight-line movement plus
/// solid collision then produces a head-on mutual block that never clears —
/// the rally agent is blocked going forward by the follower, and the
/// follower is blocked going backward by the rally agent. The give-way rule
/// detects a follower standing in its own leader's forward travel corridor
/// (<see cref="RallyCorridorHalfWidthMultiplier"/>) and steps it sideways out
/// of the corridor instead, leaving its forward position unchanged. This
/// mirrors the one give-way behaviour the project's historical research
/// records as plausible for this period and region — avoiding blocking a
/// companion's movement or weapon; see
/// <c>docs/research/battles/03-deep-past-formations-and-tactics.md</c>.
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
    /// How many body radii out the rally jitter square extends from the rally
    /// agent's centre, on each axis. A game-design choice, not a measurement.
    /// </summary>
    public const int RallyJitterRadiusMultiplier = 6;

    /// <summary>
    /// The area margin the bias square keeps over the permitted headcount. The
    /// square must be able to hold this many times the agents it is allowed to
    /// gather, so bodies never cover more than <c>1 / RallyPackingMargin</c> of
    /// it and the collision resolver always has room to separate them.
    /// </summary>
    public const int RallyPackingMargin = 4;

    /// <summary>
    /// How many body radii behind the rally agent, opposite its direction of
    /// travel, a follower's unjittered aim point sits. See the type-level
    /// remarks for the clearance derivation:
    /// <c>RallyTrailRadiusMultiplier</c> must always exceed
    /// <c>RallyJitterRadiusMultiplier * sqrt(2) + 2</c>, and changing either
    /// multiplier requires rechecking that inequality.
    /// </summary>
    public const int RallyTrailRadiusMultiplier = 12;

    /// <summary>
    /// How many body radii wide, on each side of the rally agent's direction
    /// of travel, the forward give-way corridor extends. A follower whose
    /// tick-start position falls inside this corridor and ahead of the rally
    /// agent steps sideways clear of it rather than trying to reach a trail
    /// point behind the leader by walking through it. Set to contact distance
    /// (two body radii), the same span two solid bodies already use to decide
    /// they are touching.
    /// </summary>
    public const int RallyCorridorHalfWidthMultiplier = 2;

    /// <summary>
    /// The highest last-stand threshold a scenario may configure: the bias
    /// square's capacity divided by <see cref="RallyPackingMargin"/>. See the
    /// type-level remarks for why capacity itself is not a safe headcount.
    /// </summary>
    public const int MaximumLastStandThresholdAgents =
        RallyJitterRadiusMultiplier * RallyJitterRadiusMultiplier /
        RallyPackingMargin;

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
    /// the jitter span
    /// <c>2 * jitter + 1 = 2 * RallyJitterRadiusMultiplier * bodyRadiusRaw + 1</c>
    /// would overflow <see cref="int"/>. That span is the exclusive upper
    /// bound a caller passes to <c>SplitMix64.NextInt</c>, which takes an
    /// <see cref="int"/>.
    /// </exception>
    public static int ComputeRallyJitterRaw(int bodyRadiusRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bodyRadiusRaw);

        if (!IsBodyRadiusWithinJitterSpanRange(bodyRadiusRaw))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bodyRadiusRaw),
                bodyRadiusRaw,
                "Body radius is too large: the rally jitter span " +
                "(2 * RallyJitterRadiusMultiplier * bodyRadiusRaw + 1) would " +
                "overflow Int32.");
        }

        return checked(RallyJitterRadiusMultiplier * bodyRadiusRaw);
    }

    /// <summary>
    /// Reports whether the jitter span for the given body radius fits in an
    /// <see cref="int"/>. <c>Scenario.Validate</c> uses this so a scenario that
    /// enables the last stand is rejected up front rather than throwing later
    /// from inside a tick.
    /// </summary>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when
    /// <c>2 * RallyJitterRadiusMultiplier * bodyRadiusRaw + 1</c> is
    /// representable as an <see cref="int"/>.
    /// </returns>
    public static bool IsBodyRadiusWithinJitterSpanRange(int bodyRadiusRaw) =>
        bodyRadiusRaw > 0 &&
        ((2L * RallyJitterRadiusMultiplier * bodyRadiusRaw) + 1) <= int.MaxValue;

    /// <summary>
    /// Computes the rally trail distance, in raw fixed-point units, for a
    /// body of the given radius: <c>RallyTrailRadiusMultiplier *
    /// bodyRadiusRaw</c>. This is how far behind the rally agent, opposite
    /// its direction of travel, a follower's unjittered aim point sits.
    /// </summary>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units. Must be positive.
    /// </param>
    /// <returns>The rally trail distance, in raw fixed-point units.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="bodyRadiusRaw"/> is not positive, or when
    /// <c>RallyTrailRadiusMultiplier * bodyRadiusRaw</c> would overflow
    /// <see cref="int"/>.
    /// </exception>
    public static int ComputeRallyTrailRaw(int bodyRadiusRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bodyRadiusRaw);

        if (!IsBodyRadiusWithinTrailRange(bodyRadiusRaw))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bodyRadiusRaw),
                bodyRadiusRaw,
                "Body radius is too large: the rally trail distance " +
                "(RallyTrailRadiusMultiplier * bodyRadiusRaw) would " +
                "overflow Int32.");
        }

        return checked(RallyTrailRadiusMultiplier * bodyRadiusRaw);
    }

    /// <summary>
    /// Reports whether the trail distance for the given body radius fits in
    /// an <see cref="int"/>. <c>Scenario.Validate</c> uses this so a scenario
    /// that enables the last stand is rejected up front rather than throwing
    /// later from inside a tick.
    /// </summary>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when
    /// <c>RallyTrailRadiusMultiplier * bodyRadiusRaw</c> is representable as
    /// an <see cref="int"/>.
    /// </returns>
    public static bool IsBodyRadiusWithinTrailRange(int bodyRadiusRaw) =>
        bodyRadiusRaw > 0 &&
        ((long)RallyTrailRadiusMultiplier * bodyRadiusRaw) <= int.MaxValue;

    /// <summary>
    /// Computes the give-way corridor half-width, in raw fixed-point units,
    /// for a body of the given radius:
    /// <c>RallyCorridorHalfWidthMultiplier * bodyRadiusRaw</c>. A regrouping
    /// follower whose tick-start position falls inside this distance of its
    /// rally agent's line of travel, and ahead of the rally agent along that
    /// line, gives way sideways instead of trailing behind.
    /// </summary>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units. Must be positive.
    /// </param>
    /// <returns>The corridor half-width, in raw fixed-point units.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="bodyRadiusRaw"/> is not positive, or when
    /// <c>RallyCorridorHalfWidthMultiplier * bodyRadiusRaw</c> would overflow
    /// <see cref="int"/>.
    /// </exception>
    public static int ComputeRallyCorridorHalfWidthRaw(int bodyRadiusRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bodyRadiusRaw);

        if (!IsBodyRadiusWithinCorridorRange(bodyRadiusRaw))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bodyRadiusRaw),
                bodyRadiusRaw,
                "Body radius is too large: the give-way corridor half-width " +
                "(RallyCorridorHalfWidthMultiplier * bodyRadiusRaw) would " +
                "overflow Int32.");
        }

        return checked(RallyCorridorHalfWidthMultiplier * bodyRadiusRaw);
    }

    /// <summary>
    /// Reports whether the give-way corridor half-width for the given body
    /// radius fits in an <see cref="int"/>.
    /// </summary>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when
    /// <c>RallyCorridorHalfWidthMultiplier * bodyRadiusRaw</c> is
    /// representable as an <see cref="int"/>.
    /// </returns>
    public static bool IsBodyRadiusWithinCorridorRange(int bodyRadiusRaw) =>
        bodyRadiusRaw > 0 &&
        ((long)RallyCorridorHalfWidthMultiplier * bodyRadiusRaw) <= int.MaxValue;
}
