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
    /// How many consecutive blocked ticks prove a follower's rally aim point
    /// unreachable, after which it draws a different one. **Provisional tuning
    /// value, not a measurement of anything in the world.**
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sized to sit above every blocked run a healthy battle produces, so that
    /// the escape cannot fire merely because a front is crowded. The longest
    /// runs recorded in <c>docs/development/testing.md</c> are 88 at 200
    /// agents, 87 at 500, 108 at 2 000, and 111 at 1 000; the last-stand
    /// regression test asserts a provisional bound of 125 across seeds 1 to 20.
    /// This value is 1.73 times the largest observed run and 1.54 times that
    /// asserted bound.
    /// </para>
    /// <para>
    /// A net-pressure form of this trigger was tried and reverted: a leaky
    /// bucket that rose on a blocked tick and drained on a moving one detected
    /// no additional stall over 200 seeds, and it fired in healthy battles at
    /// 500 and 1 000 agents, moving two recorded hashes and flipping the
    /// 1 000-agent outcome. The margin argument above is derived from
    /// consecutive runs and does not transfer to a net measure. If this trigger
    /// is ever replaced by one, the threshold has to be re-derived rather than
    /// carried across.
    /// </para>
    /// <para>
    /// Against a stall that otherwise runs to the 10 000-tick limit, waiting
    /// this long costs nothing. The margin is what matters: too low and the
    /// escape perturbs battles that were going to resolve on their own.
    /// </para>
    /// </remarks>
    public const int StallEscapeStreakTicks = 192;

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

    /// <summary>
    /// Computes a contingent's jitter radius, in raw fixed-point units, for a
    /// body of the given radius and a living headcount:
    /// <c>bodyRadiusRaw * (IntegerSquareRoot(4 * livingCount) + 1)</c>. This
    /// is the persistent-contingent analogue of
    /// <see cref="ComputeRallyJitterRaw"/>, generalised from a fixed
    /// multiplier to one solved from the contingent's own size — see
    /// docs/plans/2026-07-28-formation-movement-realism-design.md section 3.5,
    /// "The personal offset".
    /// </summary>
    /// <remarks>
    /// The derivation is the same fourfold packing margin the type-level
    /// remarks above establish for the rally jitter square, solved for a
    /// variable headcount instead of a fixed one. A bias square of half-side
    /// <c>J = m * R</c> holds <c>m^2</c> non-overlapping bodies at capacity,
    /// and capacity is not a safe headcount because offsets drawn at random
    /// do not pack perfectly — the safe headcount is <c>capacity / 4</c>.
    /// Solving <c>m^2 &gt;= 4 * livingCount</c> for the smallest integer
    /// <c>m</c> gives <c>m = IntegerSquareRoot(4 * livingCount) + 1</c>,
    /// where the <c>+ 1</c> absorbs the integer square root's floor and
    /// makes the inequality strict:
    /// <c>ContingentJitterMultiplierSquaredStrictlyExceedsFourTimesLivingCount</c>
    /// in <c>FormationRulesTests</c> pins that for every living count from 1
    /// to 2000.
    /// </remarks>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units. Must be positive.
    /// </param>
    /// <param name="livingCount">
    /// The contingent's living headcount. Must be positive.
    /// </param>
    /// <returns>The contingent jitter radius, in raw fixed-point units.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="bodyRadiusRaw"/> or
    /// <paramref name="livingCount"/> is not positive, or when
    /// <see cref="IsBodyRadiusWithinContingentJitterRange"/> reports the
    /// result would overflow <see cref="int"/>.
    /// </exception>
    public static int ComputeContingentJitterRaw(int bodyRadiusRaw, int livingCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bodyRadiusRaw);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(livingCount);

        if (!IsBodyRadiusWithinContingentJitterRange(bodyRadiusRaw, livingCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bodyRadiusRaw),
                bodyRadiusRaw,
                "Body radius is too large: the contingent jitter " +
                "(bodyRadiusRaw * (IntegerSquareRoot(4 * livingCount) + 1)) " +
                "would overflow Int32.");
        }

        var multiplier = checked(IntegerSquareRoot(checked(4L * livingCount)) + 1);
        return checked((int)(bodyRadiusRaw * multiplier));
    }

    /// <summary>
    /// Reports whether the contingent jitter for the given body radius and
    /// living headcount fits in an <see cref="int"/>.
    /// </summary>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units.
    /// </param>
    /// <param name="livingCount">The contingent's living headcount.</param>
    /// <returns>
    /// <see langword="true"/> when
    /// <c>bodyRadiusRaw * (IntegerSquareRoot(4 * livingCount) + 1)</c> is
    /// representable as an <see cref="int"/>.
    /// </returns>
    public static bool IsBodyRadiusWithinContingentJitterRange(
        int bodyRadiusRaw,
        int livingCount) =>
        bodyRadiusRaw > 0 &&
        livingCount > 0 &&
        ((long)bodyRadiusRaw *
            (IntegerSquareRoot(checked(4L * livingCount)) + 1)) <= int.MaxValue;

    /// <summary>
    /// Computes a contingent's trail distance, in raw fixed-point units, for
    /// a body of the given radius and jitter radius:
    /// <c>((3 * jitterRaw + 1) / 2) + (3 * bodyRadiusRaw)</c>. This is the
    /// persistent-contingent analogue of <see cref="ComputeRallyTrailRaw"/>,
    /// how far behind a contingent's leader, opposite the leader's own
    /// direction of travel, a member's unjittered aim point sits — see
    /// docs/plans/2026-07-28-formation-movement-realism-design.md section 3.5,
    /// "The trail".
    /// </summary>
    /// <remarks>
    /// The trail must clear the worst-case forward encroachment the jitter
    /// offset alone could produce, the same Chebyshev bound
    /// <see cref="ComputeRallyTrailRaw"/>'s type-level remarks derive for the
    /// rally case: an offset drawn independently per axis from
    /// <c>[-jitterRaw, +jitterRaw]</c> has a worst-case projection of
    /// <c>jitterRaw * sqrt(2)</c> onto any one direction. The trail must
    /// therefore strictly exceed <c>jitterRaw * sqrt(2) + 2 *
    /// bodyRadiusRaw</c> — the jitter diagonal plus the two body radii of
    /// contact distance — which
    /// <c>ContingentTrailRawStrictlyExceedsTheJitterDiagonalPlusTwoBodyRadii</c>
    /// in <c>FormationRulesTests</c> pins with exact squared-integer
    /// arithmetic, never a floating-point square root, across a sweep of
    /// body radii and living counts.
    /// </remarks>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units. Must be positive.
    /// </param>
    /// <param name="jitterRaw">
    /// The contingent jitter radius, in raw fixed-point units, as returned
    /// by <see cref="ComputeContingentJitterRaw"/>. Must be positive.
    /// </param>
    /// <returns>The contingent trail distance, in raw fixed-point units.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="bodyRadiusRaw"/> or
    /// <paramref name="jitterRaw"/> is not positive, or when
    /// <see cref="IsBodyRadiusWithinContingentTrailRange"/> reports the
    /// result would overflow <see cref="int"/>.
    /// </exception>
    public static int ComputeContingentTrailRaw(int bodyRadiusRaw, int jitterRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bodyRadiusRaw);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(jitterRaw);

        if (!IsBodyRadiusWithinContingentTrailRange(bodyRadiusRaw, jitterRaw))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bodyRadiusRaw),
                bodyRadiusRaw,
                "Body radius is too large: the contingent trail distance " +
                "(((3 * jitterRaw + 1) / 2) + (3 * bodyRadiusRaw)) would " +
                "overflow Int32.");
        }

        var halfJitterTermRaw = checked(((3L * jitterRaw) + 1) / 2);
        return checked((int)(halfJitterTermRaw + (3L * bodyRadiusRaw)));
    }

    /// <summary>
    /// Reports whether the contingent trail distance for the given body
    /// radius and jitter radius fits in an <see cref="int"/>.
    /// </summary>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units.
    /// </param>
    /// <param name="jitterRaw">
    /// The contingent jitter radius, in raw fixed-point units.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when
    /// <c>((3 * jitterRaw + 1) / 2) + (3 * bodyRadiusRaw)</c> is
    /// representable as an <see cref="int"/>.
    /// </returns>
    public static bool IsBodyRadiusWithinContingentTrailRange(
        int bodyRadiusRaw,
        int jitterRaw) =>
        bodyRadiusRaw > 0 &&
        jitterRaw > 0 &&
        ((((3L * jitterRaw) + 1) / 2) + (3L * bodyRadiusRaw)) <= int.MaxValue;

    /// <summary>
    /// The map-edge open-ground test. Reports whether a contingent's entire
    /// bias square — centred on its unclamped trail base, half-side
    /// <c>jitterRaw + bodyRadiusRaw</c> — fits inside the legal interval
    /// <see cref="CollisionGeometry.ClampCenterToBounds"/> enforces on both
    /// axes. See
    /// docs/plans/2026-07-28-formation-movement-realism-design.md section 3.5,
    /// "The map-edge open-ground test, and why the packing proof needs it".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four exact integer comparisons in <see cref="long"/>. No tolerance, no
    /// epsilon, no floating point.
    /// </para>
    /// <para>
    /// The comparisons are <b>non-strict</b>: a square that fits exactly —
    /// touching the legal interval's endpoint on an axis — counts as fitting.
    /// At exact equality no aim point falls outside the clamp interval, so
    /// <see cref="CollisionGeometry.ClampCenterToBounds"/> returns its
    /// argument unchanged and no collapse occurs. That is the safe side to
    /// round toward, because it only ever grants cohesion when the packing
    /// proof's open-ground hypothesis genuinely still holds.
    /// </para>
    /// <para>
    /// When the map is smaller than <c>2 * (jitterRaw + bodyRadiusRaw)</c> on
    /// either axis, no trail base — however placed — can satisfy both
    /// comparisons on that axis, so this always reports
    /// <see langword="false"/> regardless of <paramref name="trailBaseXRaw"/>
    /// or <paramref name="trailBaseYRaw"/>.
    /// </para>
    /// </remarks>
    /// <param name="trailBaseXRaw">
    /// The contingent's unclamped trail base X, in raw fixed-point units.
    /// </param>
    /// <param name="trailBaseYRaw">
    /// The contingent's unclamped trail base Y, in raw fixed-point units.
    /// </param>
    /// <param name="jitterRaw">
    /// The contingent jitter radius, in raw fixed-point units.
    /// </param>
    /// <param name="bodyRadiusRaw">
    /// The living body radius, in raw fixed-point units.
    /// </param>
    /// <param name="mapWidthRaw">The map width, in raw fixed-point units.</param>
    /// <param name="mapHeightRaw">The map height, in raw fixed-point units.</param>
    /// <returns>
    /// <see langword="true"/> when the contingent's bias square fits inside
    /// the map on both axes.
    /// </returns>
    public static bool IsCohesionSquareWithinBounds(
        int trailBaseXRaw,
        int trailBaseYRaw,
        int jitterRaw,
        int bodyRadiusRaw,
        int mapWidthRaw,
        int mapHeightRaw)
    {
        var marginRaw = (long)jitterRaw + bodyRadiusRaw;

        return (long)trailBaseXRaw - marginRaw >= bodyRadiusRaw &&
            (long)trailBaseXRaw + marginRaw <= (long)mapWidthRaw - bodyRadiusRaw &&
            (long)trailBaseYRaw - marginRaw >= bodyRadiusRaw &&
            (long)trailBaseYRaw + marginRaw <= (long)mapHeightRaw - bodyRadiusRaw;
    }

    /// <summary>
    /// The cross-contingent test. Reports whether two same-faction
    /// contingents' bias squares — each centred on its own unclamped trail
    /// base, half-side its own margin — overlap. See
    /// docs/plans/2026-07-28-formation-movement-realism-design.md section 3.5,
    /// "The cross-contingent test, and the combined-density argument".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Exact integer arithmetic on axis-aligned squares: two absolute
    /// differences and two comparisons, all <see cref="long"/>. No
    /// tolerance, no epsilon, no floating point, no square root, and no
    /// distance.
    /// </para>
    /// <para>
    /// The comparisons are <b>non-strict</b>: two squares that merely touch
    /// along an edge count as overlapping. That is the opposite convention
    /// from <see cref="IsCohesionSquareWithinBounds"/>'s, and deliberately
    /// so — the safe side is the opposite side. Exact contact means the two
    /// squares share a boundary line, on which two aim points can land at
    /// the same coordinate, so contact is already the first separation at
    /// which the combined-density argument stops being strictly true.
    /// Choosing "overlapping" at equality can only ever remove a cohesion
    /// destination, never grant one.
    /// </para>
    /// <para>
    /// This predicate is <b>symmetric</b> in its two contingents by
    /// construction: both <see cref="Math.Abs(long)"/> of a difference and a
    /// sum of margins are symmetric, so exchanging the two contingents'
    /// arguments can never change the answer. No ordering rule and no
    /// tie-break is needed, and both contingents yield cohesion together.
    /// </para>
    /// <para>
    /// This takes margins, not jitters, so a caller cannot pass a half-side
    /// that disagrees with the one <see cref="IsCohesionSquareWithinBounds"/>
    /// uses for the same contingent.
    /// </para>
    /// </remarks>
    /// <param name="aTrailBaseXRaw">
    /// The first contingent's unclamped trail base X, in raw fixed-point
    /// units.
    /// </param>
    /// <param name="aTrailBaseYRaw">
    /// The first contingent's unclamped trail base Y, in raw fixed-point
    /// units.
    /// </param>
    /// <param name="aMarginRaw">
    /// The first contingent's bias-square half-side (<c>jitterRaw +
    /// bodyRadiusRaw</c>), in raw fixed-point units.
    /// </param>
    /// <param name="bTrailBaseXRaw">
    /// The second contingent's unclamped trail base X, in raw fixed-point
    /// units.
    /// </param>
    /// <param name="bTrailBaseYRaw">
    /// The second contingent's unclamped trail base Y, in raw fixed-point
    /// units.
    /// </param>
    /// <param name="bMarginRaw">
    /// The second contingent's bias-square half-side, in raw fixed-point
    /// units.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the two bias squares overlap or touch on
    /// both axes.
    /// </returns>
    public static bool DoCohesionSquaresOverlap(
        int aTrailBaseXRaw,
        int aTrailBaseYRaw,
        int aMarginRaw,
        int bTrailBaseXRaw,
        int bTrailBaseYRaw,
        int bMarginRaw)
    {
        var marginSumRaw = (long)aMarginRaw + bMarginRaw;

        return Math.Abs((long)aTrailBaseXRaw - bTrailBaseXRaw) <= marginSumRaw &&
            Math.Abs((long)aTrailBaseYRaw - bTrailBaseYRaw) <= marginSumRaw;
    }

    /// <summary>
    /// The same integer square root <see cref="Simulation.BattleSimulation"/>
    /// and <see cref="Simulation.CollisionResolver"/> each carry their own
    /// copy of: a binary digit-by-digit extraction, exact for every
    /// non-negative <see cref="long"/> and requiring no floating point.
    /// </summary>
    private static long IntegerSquareRoot(long value)
    {
        var remainder = checked((ulong)value);
        ulong root = 0;
        var bit = 1UL << 62;

        while (bit > remainder)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (remainder >= root + bit)
            {
                remainder -= root + bit;
                root = (root >> 1) + bit;
            }
            else
            {
                root >>= 1;
            }

            bit >>= 2;
        }

        return checked((long)root);
    }
}
