namespace Hukbo.Core.Movement;

/// <summary>
/// Pure, integer-only footwork arithmetic for the in-fight evasion rungs of the
/// <see cref="MovementPresetId.EvasiveFootworkV14"/> preset (design section
/// 5.1): the duty phase that rate-limits a mechanic without any timer state,
/// the alternation sign that makes consecutive fires cancel each other out, and
/// the perpendicular offset that turns a warrior's aim point off the straight
/// line to its enemy. Every method reads only its own arguments — no agent
/// array, no simulation, no tick pipeline — matching the testability shape of
/// <see cref="RangedRetreatRules"/> and <see cref="MovementRouteRules"/>.
/// Division truncates toward zero everywhere, and nothing here touches floating
/// point.
/// </summary>
/// <remarks>
/// <para>
/// Nothing in this class draws from a random generator. The duty phase and the
/// alternation sign are pure functions of the tick and the entity id, so the
/// <c>SplitMix64</c> stream after the evasion stage is exactly what it was
/// before the stage existed. That is what lets V14 join the preset family
/// without disturbing any earlier preset's recorded hashes.
/// </para>
/// <para>
/// Every constant below is a provisional reconstruction — gameplay tuning under
/// <c>CLAUDE.md</c> section 7, not a historical measurement. Sixteenth-century
/// Philippine sources describe weapons, not footwork intervals, and each
/// constant carries the label on its own member so a value cannot be lifted out
/// of this file and quoted as evidence.
/// </para>
/// </remarks>
internal static class EvasionRules
{
    /// <summary>
    /// The basis-point denominator of the slip-radius model, matching the
    /// basis-point scale <see cref="RangedRetreatRules"/> and
    /// <see cref="MovementRouteRules"/> already use. A radius in basis points
    /// becomes a raw distance as
    /// <c>rawDistance * basisPoints / BasisPointDenominator</c>, truncating
    /// toward zero.
    /// </summary>
    internal const long BasisPointDenominator = 10_000;

    /// <summary>
    /// PROVISIONAL RECONSTRUCTION — gameplay tuning under <c>CLAUDE.md</c>
    /// section 7, not a historical measurement. The multiple of the living body
    /// radius by which a break-off step displaces its aim point sideways
    /// (design section 5.2): three radii, which is 13,056 raw at the default
    /// body radius of 4,352 raw. At a typical contact separation of one body
    /// diameter this turns the heading far enough that the committed step
    /// carries much more lateral motion than forward motion, so the warrior
    /// circles its enemy at contact distance rather than opening the range.
    /// </summary>
    internal const int BreakOffOffsetMultiplier = 3;

    /// <summary>
    /// PROVISIONAL RECONSTRUCTION — gameplay tuning under <c>CLAUDE.md</c>
    /// section 7, not a historical measurement. The multiple of the living body
    /// radius by which a lateral slip displaces its aim point (design section
    /// 5.3): two radii, which is 8,704 raw at the default body radius and
    /// exactly one body diameter. Chosen so that a warrior still well outside
    /// contact weaves visibly without paying more than a few per cent of its
    /// closing speed, which is what keeps the termination bar of design section
    /// 8 reachable.
    /// </summary>
    internal const int SlipOffsetMultiplier = 2;

    /// <summary>
    /// PROVISIONAL RECONSTRUCTION — gameplay tuning under <c>CLAUDE.md</c>
    /// section 7, not a historical measurement. The multiple of the living body
    /// radius by which a missile dodge displaces its aim point off the line of
    /// the incoming shot (design section 5.4): two radii, 8,704 raw at the
    /// default body radius. The behaviour it models is the best-attested
    /// evasive movement in the research corpus — two independent manuscripts
    /// record men leaping about under missile fire at Mactan — but the distance
    /// itself is tuning and nothing more.
    /// </summary>
    internal const int DodgeOffsetMultiplier = 2;

    /// <summary>
    /// PROVISIONAL RECONSTRUCTION — gameplay tuning under <c>CLAUDE.md</c>
    /// section 7, not a historical measurement. The raw distance a warrior
    /// yields directly away from its enemy when it gives ground while pinned in
    /// contact (design section 5.5): 1,024 raw, which is exactly one world unit
    /// at <c>FixedPoint.Scale</c>, one third of a full 3,072-raw step, and
    /// 23.5 per cent of the default body radius. It is deliberately short: a
    /// warrior stops 8,704 raw from its target's centre while its reach is
    /// 12,288 raw, so three consecutive give-ground steps still leave it inside
    /// its own attack range and the movement cannot read as a rout.
    /// </summary>
    internal const int GiveGroundStepRaw = 1_024;

    /// <summary>
    /// PROVISIONAL RECONSTRUCTION — gameplay tuning under <c>CLAUDE.md</c>
    /// section 7, not a historical measurement. The duty period of the lateral
    /// slip, in ticks (design section 5.3): one slip every eight ticks per
    /// warrior, which at the 20 Hz tick rate is one every 0.4 seconds. Because
    /// <see cref="FiresThisTick"/> phases the duty on the entity id, a rank of
    /// eight neighbours slips on eight consecutive ticks rather than in unison.
    /// </summary>
    internal const int SlipPeriodTicks = 8;

    /// <summary>
    /// PROVISIONAL RECONSTRUCTION — gameplay tuning under <c>CLAUDE.md</c>
    /// section 7, not a historical measurement. The duty period of the
    /// give-ground step, in ticks (design section 5.5): at most once every
    /// twelve ticks per warrior, 0.6 seconds at the 20 Hz tick rate. This is
    /// the longest period of the four mechanics because it is the only one that
    /// moves a warrior backwards, and the rate is half of what bounds the
    /// backward drift.
    /// </summary>
    internal const int GiveGroundPeriodTicks = 12;

    /// <summary>
    /// PROVISIONAL RECONSTRUCTION — gameplay tuning under <c>CLAUDE.md</c>
    /// section 7, not a historical measurement. How many ticks of remaining
    /// flight time make an inbound missile imminent enough to dodge (design
    /// section 5.4). A projectile holding one tick of flight arrives at the end
    /// of the current tick and one holding two arrives on the next, so a
    /// threshold of two is the widest window in which a step aside is still a
    /// reaction to a shot already in the air rather than a prediction of one.
    /// </summary>
    internal const int DodgeImminenceTicks = 2;

    /// <summary>
    /// PROVISIONAL RECONSTRUCTION — gameplay tuning under <c>CLAUDE.md</c>
    /// section 7, not a historical measurement. The outer bound of the lateral
    /// slip, in basis points of the warrior's own attack range (design section
    /// 5.3): 20,000 basis points is twice its reach, which is 24,576 raw at the
    /// default attack range of 12,288 raw. Beyond that the warrior is still
    /// crossing open ground and weaving costs closing speed for no legibility;
    /// inside contact a different rung owns the tick.
    /// </summary>
    internal const int SlipRadiusBasisPoints = 20_000;

    /// <summary>
    /// PROVISIONAL RECONSTRUCTION — gameplay tuning under <c>CLAUDE.md</c>
    /// section 7, not a historical measurement. The floor every evasive
    /// displacement must clear to be worth animating, in raw units per tick.
    /// </summary>
    /// <remarks>
    /// The renderer's gait system treats any per-tick displacement below 60 raw
    /// as a stance rather than a step and animates no legs for it; that
    /// threshold is the private <c>CrawlThresholdRawPerTick</c> constant of
    /// <c>Hukbo.Client</c>'s <c>GaitGeometry</c>. <c>Hukbo.Core</c> cannot
    /// reference the client and must not learn what a renderer is, so the link
    /// is documented here rather than compiled: 384 raw is 6.4 times that
    /// threshold, which leaves a wide margin for the collision resolver to
    /// shorten a step and for integer truncation to shave it further while the
    /// movement still reads as a movement on screen. A mechanic whose
    /// displacement falls below this floor is invisible, and an invisible
    /// mechanic fails the spectator-discoverability question of
    /// <c>SIMULATION-GAME-STANDARDS.md</c> section 10.
    /// </remarks>
    internal const int MinimumLegibleStepRaw = 384;

    /// <summary>
    /// Whether a mechanic with a duty period of <paramref name="periodTicks"/>
    /// fires for the warrior identified by <paramref name="entityId"/> on tick
    /// <paramref name="tick"/> — true exactly when
    /// <c>tick % periodTicks == entityId % periodTicks</c>.
    /// </summary>
    /// <param name="tick">
    /// The authoritative simulation tick, which is never negative.
    /// </param>
    /// <param name="entityId">The warrior's stable entity id.</param>
    /// <param name="periodTicks">
    /// The duty period in ticks, which must be strictly positive. A period of
    /// one fires on every tick.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on exactly one tick out of every
    /// <paramref name="periodTicks"/> consecutive ticks, for any fixed
    /// <paramref name="entityId"/>.
    /// </returns>
    /// <remarks>
    /// This rate-limits a mechanic with no timer state at all: nothing is
    /// stored on the agent, nothing is decremented, and nothing enters the
    /// state hash. Phasing the duty on the entity id also staggers neighbours,
    /// so a rank of warriors never steps in unison and the effect reads as
    /// individual footwork rather than as a drill.
    /// </remarks>
    internal static bool FiresThisTick(long tick, ulong entityId, int periodTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(periodTicks);

        return tick % periodTicks == (long)(entityId % (ulong)periodTicks);
    }

    /// <summary>
    /// The direction sign a duty-phased mechanic takes on tick
    /// <paramref name="tick"/> — <c>+1</c> when the duty window index
    /// <c>tick / periodTicks</c> is even and <c>-1</c> when it is odd.
    /// </summary>
    /// <param name="tick">
    /// The authoritative simulation tick, which is never negative.
    /// </param>
    /// <param name="periodTicks">
    /// The same duty period passed to <see cref="FiresThisTick"/>, which must
    /// be strictly positive.
    /// </param>
    /// <returns>Either <c>+1</c> or <c>-1</c>, and never any other value.</returns>
    /// <remarks>
    /// The sign is constant across a whole duty window and flips between one
    /// window and the next, so two consecutive fires by the same warrior step
    /// to opposite sides. Lateral displacement over any two consecutive fires
    /// therefore sums to zero up to integer truncation and collision refusal.
    /// This is what makes the anti-drift bar of design section 8 structural
    /// rather than hopeful: no warrior can accumulate lateral travel in one
    /// direction by repeating a mechanic, because the mechanic itself reverses.
    /// </remarks>
    internal static int DutySign(long tick, int periodTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(periodTicks);

        return (tick / periodTicks & 1) == 0 ? 1 : -1;
    }

    /// <summary>
    /// The vector of length <paramref name="offsetRaw"/> at right angles to
    /// <c>(deltaXRaw, deltaYRaw)</c>, on the side chosen by
    /// <paramref name="sign"/> — the displacement a rung adds to an aim point to
    /// take a warrior off the straight line between itself and its enemy.
    /// </summary>
    /// <param name="deltaXRaw">
    /// The X component of the vector to be stepped off, in raw units.
    /// </param>
    /// <param name="deltaYRaw">
    /// The Y component of the vector to be stepped off, in raw units.
    /// </param>
    /// <param name="distanceRaw">
    /// The length of that vector in raw units, as the caller has already
    /// computed it. A caller that passes a length inconsistent with the two
    /// components gets a proportionally inconsistent result; the callers in
    /// <c>BattleSimulation</c> pass the distance they measured from the same
    /// two components.
    /// </param>
    /// <param name="offsetRaw">
    /// The requested length of the returned vector, in raw units, which must
    /// not be negative.
    /// </param>
    /// <param name="sign">
    /// Which of the two perpendiculars to take, either <c>+1</c> or <c>-1</c>.
    /// The rungs pass <see cref="DutySign"/> so that consecutive fires
    /// alternate.
    /// </param>
    /// <returns>
    /// The perpendicular offset, whose length is <paramref name="offsetRaw"/>
    /// up to integer truncation of each component. <c>(0, 0)</c> whenever
    /// <paramref name="distanceRaw"/> is zero or negative, which every caller
    /// treats as "the rung yields and the pre-existing proposal stands".
    /// </returns>
    /// <remarks>
    /// The perpendicular of <c>(dx, dy)</c> is <c>(-dy, dx)</c>. Both products
    /// are widened to <see langword="long"/> before the multiply, for the same
    /// reason <c>ApproachSidestep.Compute</c> gives: on a large map a raw delta
    /// is in the millions and the offset is in the thousands, so the product
    /// overflows <see langword="int"/> well before the division brings it back
    /// into range. Returning early on a non-positive distance is what supplies
    /// the design's <c>max(1, distanceRaw)</c> divisor — by the time the
    /// division runs, the divisor is at least one. Each component is truncated
    /// toward zero exactly once, so the returned length is at most
    /// <paramref name="offsetRaw"/> and never exceeds it.
    /// </remarks>
    internal static (int XRaw, int YRaw) PerpendicularOffset(
        long deltaXRaw,
        long deltaYRaw,
        long distanceRaw,
        int offsetRaw,
        int sign)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offsetRaw);

        if (sign is not (1 or -1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sign),
                sign,
                "The perpendicular side must be either +1 or -1.");
        }

        if (distanceRaw <= 0)
        {
            return (0, 0);
        }

        var offsetXRaw = checked(-deltaYRaw * offsetRaw * sign) / distanceRaw;
        var offsetYRaw = checked(deltaXRaw * offsetRaw * sign) / distanceRaw;

        return (checked((int)offsetXRaw), checked((int)offsetYRaw));
    }
}
