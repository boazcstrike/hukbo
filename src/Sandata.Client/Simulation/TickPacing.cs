using System;

namespace Sandata.Client.Simulation;

/// <summary>
/// The fixed-timestep bridge between a MonoGame frame, whose length is
/// whatever the display and the machine produced, and a Sandata simulation
/// tick, whose length is exactly 20 milliseconds because
/// <c>SandataRuleset.ModernTacticalV1</c> pins <c>TickRate</c> at 50 (design
/// section 4: "Time is an integer tick at a <c>TickRate</c> of 50, so one tick
/// is exactly 20 milliseconds").
/// </summary>
/// <remarks>
/// <para>
/// This type is pure: it holds no state, touches no clock, and returns the
/// caller's next accumulator alongside the number of ticks that frame earned.
/// The caller owns the accumulator and the tick counter. That is what lets the
/// whole pacing rule be tested without a window, a graphics device, or a
/// stopwatch — the pattern <c>hukbo-client-ui</c> calls the pure-helper
/// testability pattern.
/// </para>
/// <para>
/// <b>Wall-clock elapsed time reaches this type and never reaches the
/// simulation.</b> Nothing here decides an outcome; it decides only how many
/// times <c>SandataSimulation.RunTick</c> is called this frame, and
/// <c>RunTick</c> takes its tick number as an argument. Two machines running
/// at different frame rates therefore reach identical state at identical tick
/// numbers, which is the determinism contract's actual requirement — the
/// authoritative clock stays the integer tick.
/// </para>
/// </remarks>
internal static class TickPacing
{
    /// <summary>
    /// One simulation tick, in microseconds. 20,000 µs is 20 ms, which is
    /// <c>1_000_000 / SandataRuleset.ModernTacticalV1.TickRate</c> at the
    /// pinned tick rate of 50. Microseconds rather than milliseconds because
    /// a 60 Hz frame is 16,666.67 µs — truncating that to 16 ms would lose
    /// about four percent of elapsed time on every single frame, and the
    /// simulation would run measurably slow.
    /// </summary>
    internal const long MicrosecondsPerTick = 20_000;

    /// <summary>
    /// The default ceiling on ticks executed in one frame. Eight ticks is
    /// 160 ms of simulation, comfortably more than any healthy frame needs and
    /// short enough that a machine which cannot keep up falls behind visibly
    /// rather than freezing while it tries to catch up.
    /// </summary>
    internal const int DefaultMaxTicksPerFrame = 8;

    /// <summary>
    /// Adds <paramref name="elapsedMicroseconds"/>, scaled by the speed
    /// fraction, to <paramref name="accumulatedMicroseconds"/> and reports how
    /// many whole ticks the result has earned along with the accumulator to
    /// carry into the next frame.
    /// </summary>
    /// <param name="accumulatedMicroseconds">
    /// The unspent remainder returned by the previous call, or zero on the
    /// first frame. Never negative.
    /// </param>
    /// <param name="elapsedMicroseconds">
    /// Real time since the previous frame. Never negative; zero is legal and
    /// earns nothing.
    /// </param>
    /// <param name="speedNumerator">
    /// The numerator of the speed fraction — 1 with a denominator of 2 is half
    /// speed, 4 with a denominator of 1 is quadruple speed. Strictly positive.
    /// </param>
    /// <param name="speedDenominator">The denominator of the speed fraction. Strictly positive.</param>
    /// <param name="maxTicksPerFrame">
    /// The ceiling on the returned tick count. Strictly positive.
    /// </param>
    /// <returns>
    /// The accumulator to carry forward, and the number of times the caller
    /// should call <c>RunTick</c> this frame.
    /// </returns>
    /// <remarks>
    /// <b>Time above the ceiling is discarded rather than banked.</b> When a
    /// frame earns more ticks than <paramref name="maxTicksPerFrame"/> allows
    /// — a breakpoint, a window drag, a machine too slow for the workload —
    /// the surplus is dropped and the accumulator comes back below one tick.
    /// Banking it instead is the classic spiral of death: the next frame
    /// inherits the debt, runs even longer, and earns still more ticks. The
    /// visible consequence of dropping it is that a stalled client resumes at
    /// the tick it stalled on rather than fast-forwarding, which for a
    /// spectator is the better of the two failures.
    /// </remarks>
    internal static (long RemainingMicroseconds, int TicksToRun) Advance(
        long accumulatedMicroseconds,
        long elapsedMicroseconds,
        int speedNumerator,
        int speedDenominator,
        int maxTicksPerFrame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(accumulatedMicroseconds);
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMicroseconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(speedNumerator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(speedDenominator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTicksPerFrame);

        var scaledElapsed = elapsedMicroseconds * speedNumerator / speedDenominator;
        var available = accumulatedMicroseconds + scaledElapsed;

        var earned = available / MicrosecondsPerTick;
        var ticksToRun = earned > maxTicksPerFrame ? maxTicksPerFrame : (int)earned;

        var remaining = available - ((long)ticksToRun * MicrosecondsPerTick);
        if (remaining >= MicrosecondsPerTick)
        {
            remaining = MicrosecondsPerTick - 1;
        }

        return (remaining, ticksToRun);
    }
}
