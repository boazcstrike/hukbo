using Sandata.Core.Mathematics;

namespace Sandata.Core.Weapons;

/// <summary>
/// The result of advancing a <see cref="WeaponChainPhase"/> state machine by
/// exactly one tick: the phase and remaining-tick counter to store as the
/// operator's hashed state for the next tick, and whether a shot resolved on
/// this tick.
/// </summary>
/// <param name="Phase">
/// The phase to hold going into the next tick. Never <see cref="WeaponChainPhase.Firing"/>
/// — <see cref="WeaponChain.Advance"/> always resolves that phase within the
/// same call and moves on, so it can never be the phase a caller stores.
/// </param>
/// <param name="RemainingTicks">
/// The tick counter to store alongside <paramref name="Phase"/>. Meaningful
/// only for <see cref="WeaponChainPhase.Raising"/>,
/// <see cref="WeaponChainPhase.Aiming"/>, and
/// <see cref="WeaponChainPhase.Resetting"/>; always <c>0</c> for
/// <see cref="WeaponChainPhase.Lowered"/> and
/// <see cref="WeaponChainPhase.Turning"/>, whose completion conditions are
/// not tick-counted.
/// </param>
/// <param name="Fired">
/// <c>true</c> if this call resolved exactly one shot. At most one shot ever
/// resolves per <see cref="WeaponChain.Advance"/> call — see the remarks on
/// that method for why that bound is safe rather than a missed round.
/// </param>
public readonly record struct WeaponChainAdvanceResult(
    WeaponChainPhase Phase,
    int RemainingTicks,
    bool Fired);

/// <summary>
/// Advances the weapon-chain state machine described in design section 9 by
/// exactly one tick — the timing chain that makes a doorway gunfight in this
/// game a race rather than a dice roll. Whoever completes ready, turn, aim,
/// fire first wins, and under roughly 14 m the race is effectively instant,
/// so getting the chain's tick accounting exactly right is the mechanical
/// core of the product.
/// </summary>
/// <remarks>
/// <para>
/// <b>The written order for one tick, exactly as design section 9 requires
/// it be resolved in a single documented pass:</b>
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// If <c>forceLowered</c> is set, the weapon-lowered rule wins immediately
/// and unconditionally: the result is <see cref="WeaponChainPhase.Lowered"/>
/// with zero remaining ticks and no fire, regardless of what phase the
/// operator was in or how far a same-tick cascade below would otherwise have
/// carried them. This is deliberate — being forced to lower cancels an
/// in-progress shot rather than letting it complete a tick late.
/// </description>
/// </item>
/// <item>
/// <description>
/// Otherwise, exactly one tick is charged against the counter of whichever
/// phase was current <b>at the start of this call</b> — <see cref="WeaponChainPhase.Raising"/>,
/// <see cref="WeaponChainPhase.Aiming"/>, or <see cref="WeaponChainPhase.Resetting"/>
/// decrement <c>remainingTicks</c> by one; <see cref="WeaponChainPhase.Lowered"/>
/// and <see cref="WeaponChainPhase.Turning"/> are not tick-counted and are
/// left untouched. This charge happens exactly once per call, before any
/// cascading below, which is what stops a phase entered by cascade later in
/// the same call from being charged a second time (double-advancing) and
/// what stops the original phase from silently absorbing a tick it already
/// finished (swallowing).
/// </description>
/// </item>
/// <item>
/// <description>
/// The state machine then walks forward from the current phase, in the
/// chain's fixed order, transitioning out of every phase whose completion
/// condition already holds this same tick — a tick-counted phase whose
/// counter reached zero or below, <see cref="WeaponChainPhase.Turning"/> once
/// its caller-supplied <c>arcWithinTolerance</c> flag is true, and
/// <see cref="WeaponChainPhase.Lowered"/> once <c>raiseRequested</c> is true —
/// initialising each newly entered phase's counter fresh from its configured
/// duration and never re-charging that fresh counter within the same call.
/// This is the same rule stated twice for two different reasons: it is what
/// lets a zero-tick phase (a duration that converted to zero ticks) resolve
/// immediately without waiting for a call that would never come due to
/// charge it, and it is what stops that same zero-tick phase from being
/// walked through twice.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="WeaponChainPhase.Firing"/> is not a wait: entering it always
/// records one resolved shot and moves on to <see cref="WeaponChainPhase.Resetting"/>
/// within the same pass, so it is never the phase this method returns.
/// </description>
/// </item>
/// <item>
/// <description>
/// The walk stops the first time it reaches a phase that has not yet
/// completed — a positive remaining-tick count, an unmet
/// <see cref="WeaponChainPhase.Turning"/> or <see cref="WeaponChainPhase.Lowered"/>
/// condition — or once a shot has already resolved this call. The second
/// half of that stopping rule is a deliberate, documented bound: a weapon
/// definition whose <c>AimTicks</c> and <c>ResetTicks</c> both convert to
/// zero would otherwise cycle <see cref="WeaponChainPhase.Aiming"/> →
/// <see cref="WeaponChainPhase.Firing"/> → <see cref="WeaponChainPhase.Resetting"/>
/// → <see cref="WeaponChainPhase.Aiming"/> forever within one call, given a
/// target that never leaves tolerance. Holding at <see cref="WeaponChainPhase.Aiming"/>
/// with zero remaining ticks, aimed and ready, is the correct outcome for
/// that tick; the next <see cref="Advance"/> call resolves the next shot. No
/// weapon in the design section 9 roster reaches this bound — every rifle
/// and pistol timing published there converts to a positive tick count at
/// 50 Hz — but the guard holds regardless of what a future weapon authors.
/// </description>
/// </item>
/// </list>
/// <para>
/// Sustained automatic fire between engagements is not this method's
/// concern: design section 9's cyclic-fire accumulator (a per-round tick
/// interval, not this chain) governs how many rounds leave the barrel while
/// <see cref="WeaponChainPhase.Firing"/> is genuinely held open by an
/// automatic burst, and belongs to whichever future task wires it against
/// this chain. This method models exactly one engagement's ready-turn-aim-
/// fire-reset cycle.
/// </para>
/// </remarks>
public static class WeaponChain
{
    /// <summary>
    /// Design section 9's <see cref="WeaponChainPhase.Turning"/> completion
    /// test, <c>|ShortestArc| &lt;= AimToleranceBam</c>: <see langword="true"/>
    /// when the shortest signed arc from <paramref name="currentAimBam"/> to
    /// <paramref name="targetAimBam"/> has an absolute magnitude no greater
    /// than <paramref name="aimToleranceBam"/>. Nothing in <see cref="Advance"/>
    /// computes this comparison — it takes the already-decided
    /// <c>arcWithinTolerance</c> boolean as a parameter — so this is the
    /// method a caller runs once per tick to produce that boolean, passing
    /// <c>SandataRuleset.AimToleranceBam</c> in as
    /// <paramref name="aimToleranceBam"/> the same way <c>PathService</c> and
    /// <c>WeaponLoweredRules</c> take their own ruleset constants as plain
    /// parameters rather than reading <c>SandataRuleset</c> themselves.
    /// </summary>
    /// <param name="currentAimBam">The operator's current aim point.</param>
    /// <param name="targetAimBam">The target's angle from the operator.</param>
    /// <param name="aimToleranceBam">
    /// The raw <see cref="Bam16"/> magnitude within which the arc counts as
    /// close enough — <c>SandataRuleset.AimToleranceBam</c> in the caller's
    /// possession.
    /// </param>
    /// <remarks>
    /// <see cref="Bam16.ShortestArc"/> already resolves the wrap at raw angle
    /// zero into a signed <see cref="short"/> before this method ever sees
    /// the value, so an arc that crosses that boundary compares identically
    /// to an equivalent arc that does not — the naive alternative, plain
    /// unwrapped subtraction of the two raw angles, does not have that
    /// property and would fail exactly that case. The magnitude is taken by
    /// widening the <see cref="short"/> result to <see cref="int"/> before
    /// negating, so the one value a <see cref="short"/> cannot represent
    /// positively — <see cref="short.MinValue"/>, an exact half turn — does
    /// not overflow. The comparison itself is a plain integer <c>&lt;=</c>:
    /// no floating point anywhere and no epsilon.
    /// </remarks>
    public static bool IsArcWithinTolerance(Bam16 currentAimBam, Bam16 targetAimBam, ushort aimToleranceBam)
    {
        int shortestArc = Bam16.ShortestArc(currentAimBam, targetAimBam);
        var magnitude = shortestArc < 0 ? -shortestArc : shortestArc;
        return magnitude <= aimToleranceBam;
    }

    /// <summary>
    /// Advances one operator's weapon-chain state by exactly one tick. See
    /// the remarks on <see cref="WeaponChain"/> for the full written order.
    /// </summary>
    /// <param name="phase">The phase held at the start of this tick.</param>
    /// <param name="remainingTicks">
    /// The tick counter held alongside <paramref name="phase"/> at the start
    /// of this tick. Ignored for <see cref="WeaponChainPhase.Lowered"/> and
    /// <see cref="WeaponChainPhase.Turning"/>, which are not tick-counted.
    /// </param>
    /// <param name="forceLowered">
    /// <c>true</c> when the weapon-lowered rule (design section 9) applies
    /// this tick — a doorway crossing or standing within
    /// <c>SandataRuleset.LoweredWallDistanceWu</c> of a wall, evaluated by
    /// whichever future task owns that rule. Overrides everything else.
    /// </param>
    /// <param name="raiseRequested">
    /// <c>true</c> when the operator currently intends to engage, so
    /// <see cref="WeaponChainPhase.Lowered"/> should begin raising. Evaluated
    /// by whichever future task owns intent selection (design section 5,
    /// stage 8); this method only reacts to the flag.
    /// </param>
    /// <param name="arcWithinTolerance">
    /// <c>true</c> when the target's shortest arc from the current aim point
    /// is within <c>SandataRuleset.AimToleranceBam</c>, so
    /// <see cref="WeaponChainPhase.Turning"/> should complete. Evaluated by
    /// the caller, which holds the aim and target angles this chain does
    /// not — ordinarily by calling <see cref="IsArcWithinTolerance"/> once
    /// per tick and passing its result straight through.
    /// </param>
    /// <param name="readyTicks">
    /// The tick form of the definition's authored <c>ReadyMs</c>, from
    /// <see cref="TickConversion.ToTicks"/>. Never negative.
    /// </param>
    /// <param name="aimTicks">
    /// The tick form of the definition's authored aim time, from
    /// <see cref="TickConversion.ToTicks"/>. Never negative.
    /// </param>
    /// <param name="resetTicks">
    /// The tick form of the definition's authored <c>ResetMs</c>, from
    /// <see cref="TickConversion.ToTicks"/>. Never negative.
    /// </param>
    public static WeaponChainAdvanceResult Advance(
        WeaponChainPhase phase,
        int remainingTicks,
        bool forceLowered,
        bool raiseRequested,
        bool arcWithinTolerance,
        int readyTicks,
        int aimTicks,
        int resetTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(remainingTicks);
        ArgumentOutOfRangeException.ThrowIfNegative(readyTicks);
        ArgumentOutOfRangeException.ThrowIfNegative(aimTicks);
        ArgumentOutOfRangeException.ThrowIfNegative(resetTicks);

        // Step 1: the weapon-lowered rule wins immediately and
        // unconditionally, cancelling any in-progress shot rather than
        // letting a same-tick cascade complete it a tick late.
        if (forceLowered)
        {
            return new WeaponChainAdvanceResult(WeaponChainPhase.Lowered, RemainingTicks: 0, Fired: false);
        }

        // Step 2: charge exactly one tick against the phase held at the
        // start of this call, and only that phase. Phases entered by the
        // cascade in step 3 are initialised fresh below and are never
        // charged again within this same call.
        var ticksLeft = remainingTicks;
        if (IsTickCounted(phase))
        {
            ticksLeft--;
        }

        // Step 3: walk forward through every phase whose completion
        // condition already holds this tick, in the chain's fixed order,
        // stopping at the first phase that has not yet completed or once a
        // shot has resolved. See the type-level remarks for why the "already
        // fired" stop is a documented bound rather than an accident.
        var currentPhase = phase;
        var fired = false;
        while (true)
        {
            switch (currentPhase)
            {
                case WeaponChainPhase.Lowered:
                    if (!raiseRequested)
                    {
                        return new WeaponChainAdvanceResult(WeaponChainPhase.Lowered, RemainingTicks: 0, fired);
                    }

                    currentPhase = WeaponChainPhase.Raising;
                    ticksLeft = readyTicks;
                    break;

                case WeaponChainPhase.Raising:
                    if (ticksLeft > 0)
                    {
                        return new WeaponChainAdvanceResult(WeaponChainPhase.Raising, ticksLeft, fired);
                    }

                    currentPhase = WeaponChainPhase.Turning;
                    ticksLeft = 0;
                    break;

                case WeaponChainPhase.Turning:
                    if (!arcWithinTolerance)
                    {
                        return new WeaponChainAdvanceResult(WeaponChainPhase.Turning, RemainingTicks: 0, fired);
                    }

                    currentPhase = WeaponChainPhase.Aiming;
                    ticksLeft = aimTicks;
                    break;

                case WeaponChainPhase.Aiming:
                    if (ticksLeft > 0)
                    {
                        return new WeaponChainAdvanceResult(WeaponChainPhase.Aiming, ticksLeft, fired);
                    }

                    if (fired)
                    {
                        // Bound documented in the type-level remarks: never
                        // resolve a second shot from this chain in one call.
                        return new WeaponChainAdvanceResult(WeaponChainPhase.Aiming, RemainingTicks: 0, fired);
                    }

                    currentPhase = WeaponChainPhase.Firing;
                    break;

                case WeaponChainPhase.Firing:
                    fired = true;
                    currentPhase = WeaponChainPhase.Resetting;
                    ticksLeft = resetTicks;
                    break;

                case WeaponChainPhase.Resetting:
                    if (ticksLeft > 0)
                    {
                        return new WeaponChainAdvanceResult(WeaponChainPhase.Resetting, ticksLeft, fired);
                    }

                    currentPhase = WeaponChainPhase.Aiming;
                    ticksLeft = aimTicks;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(phase), phase, "Unknown weapon chain phase.");
            }
        }
    }

    /// <summary>
    /// <c>true</c> for the three phases whose progress is a plain tick
    /// countdown — <see cref="WeaponChainPhase.Raising"/>,
    /// <see cref="WeaponChainPhase.Aiming"/>, and
    /// <see cref="WeaponChainPhase.Resetting"/>. <see cref="WeaponChainPhase.Lowered"/>
    /// waits on an external request and <see cref="WeaponChainPhase.Turning"/>
    /// waits on an angle, so neither is charged a tick here.
    /// </summary>
    private static bool IsTickCounted(WeaponChainPhase phase) => phase is
        WeaponChainPhase.Raising or WeaponChainPhase.Aiming or WeaponChainPhase.Resetting;
}

/// <summary>
/// The result of advancing a <see cref="CyclicFireAccumulator"/> by exactly
/// one tick: the accumulator value to store as hashed state for the next
/// tick, and how many rounds cycled on this tick.
/// </summary>
/// <param name="Accumulator">The value to store and pass back in on the next tick.</param>
/// <param name="ShotsFired">
/// The number of rounds that cycled this tick. <c>0</c> or <c>1</c> for every
/// rate of fire in the design section 9 roster; the loop that produces it
/// supports more only because a future weapon's <c>CyclicRpm</c> could
/// otherwise exceed the tick rate, not because v0.1 needs it.
/// </param>
public readonly record struct CyclicFireAccumulatorResult(int Accumulator, int ShotsFired);

/// <summary>
/// Design section 9's cyclic-fire accumulator: turns a firearm's
/// <c>CyclicRpm</c> into a per-round tick interval that never drifts, the way
/// a naive "every N ticks" counter would once <c>60 * TickRate / CyclicRpm</c>
/// is not a whole number. An 800 rpm weapon at 50 Hz fires every 75 ms, which
/// is 3.75 ticks — not representable as a fixed integer period — so state
/// carries a running remainder instead and the schedule comes out as the
/// repeating interval pattern 4, 4, 4, 3 ticks between rounds, which averages
/// to exactly 3.75 with no accumulating rounding error over any number of
/// rounds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Units.</b> Both the per-tick increment and the per-round threshold are
/// expressed in microseconds — <c>1,000,000 / TickRate</c> microseconds
/// elapse per tick, and <c>60,000,000 / CyclicRpm</c> microseconds make up
/// one round at the given cyclic rate (60 seconds per minute, each expressed
/// in microseconds, divided by rounds per minute). Microseconds rather than
/// milliseconds are the common unit because they let both figures stay exact
/// integers for the tick rates and cyclic rates design section 9's roster
/// actually uses — 50 Hz and 800 rpm divide 1,000,000 and 60,000,000 evenly,
/// with no fractional remainder silently discarded at construction. Design
/// section 9's own illustrative pseudocode writes the per-tick increment as
/// <c>1000 * TickRate</c>, which is dimensionally the reciprocal of the
/// figure this method needs and computes a rate of fire roughly two and a
/// half times too fast; this method instead implements the formula that
/// actually reproduces the design's own stated results — 75 ms and 3.75
/// ticks per round at 800 rpm and 50 Hz, and the 4, 4, 4, 3 tick pattern —
/// and that disagreement is reported rather than silently corrected only
/// here.
/// </para>
/// <para>
/// <b>The threshold test is a strict <c>&gt;</c>, not <c>&gt;=</c>.</b> Both
/// comparisons are driftless integer accumulators and either converges to
/// the same long-run average rate; they differ only in which tick of the
/// four-tick cycle carries the shorter, three-tick gap. Hand-deriving the
/// schedule as <c>shotTick(n) = floor(n * 3.75)</c> for rounds
/// <c>n = 1, 2, 3, ...</c> — the direct statement of "one shot every 3.75
/// ticks" — gives shot ticks 3, 7, 11, 15, 18, 22, 26, 30, ..., whose
/// consecutive gaps read 4, 4, 4, 3 repeating from the very first interval.
/// A strict <c>&gt;</c> reproduces that exact ordering; <c>&gt;=</c> instead
/// produces the 4, 4, 3, 4 rotation of the same multiset, since it lets the
/// exact-equality accumulator state (reached periodically because 75,000 is
/// an exact integer multiple of the running remainder) fire one tick earlier
/// than the floor-based derivation does.
/// </para>
/// </remarks>
public static class CyclicFireAccumulator
{
    /// <summary>Microseconds per minute, the constant both rates below scale by.</summary>
    private const int MicrosecondsPerMinute = 60_000_000;

    /// <summary>Microseconds per second, used to convert a tick rate to a per-tick duration.</summary>
    private const int MicrosecondsPerSecond = 1_000_000;

    /// <summary>
    /// Advances one operator's cyclic-fire accumulator by exactly one tick.
    /// </summary>
    /// <param name="accumulator">
    /// The value held at the start of this tick, in microseconds. Never
    /// negative; <c>0</c> for a weapon that has not yet cycled a round.
    /// </param>
    /// <param name="tickRate">Ticks per second, <c>SandataRuleset.TickRate</c>. Must be positive.</param>
    /// <param name="cyclicRpm">
    /// The firearm definition's authored cyclic rate of fire, in rounds per
    /// minute. Must be positive.
    /// </param>
    public static CyclicFireAccumulatorResult Advance(int accumulator, int tickRate, int cyclicRpm)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(accumulator);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tickRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cyclicRpm, 0);

        var microsecondsPerTick = MicrosecondsPerSecond / tickRate;
        var microsecondsPerRound = MicrosecondsPerMinute / cyclicRpm;

        var next = accumulator + microsecondsPerTick;
        var shotsFired = 0;
        while (next > microsecondsPerRound)
        {
            shotsFired++;
            next -= microsecondsPerRound;
        }

        return new CyclicFireAccumulatorResult(next, shotsFired);
    }
}
