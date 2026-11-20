namespace Hukbo.Client.Presentation;

/// <summary>
/// Slows the play rate to the speed the machine can sustain, so a heavy battle
/// plays smoothly at a lower speed instead of stuttering and skipping (play-rate
/// governor design, sections 3.1 and 3.2).
/// </summary>
/// <remarks>
/// <para>
/// Pure: it holds no clock. The caller passes the frame's elapsed seconds, the
/// seconds it measured spending on simulation work, and the tick count, and
/// reads the effective speed back. It changes how many ticks run per wall-clock
/// second and never what a tick computes, so it cannot move a hash.
/// </para>
/// <para>
/// <b>Intended frame order.</b> Accumulate the frame with
/// <see cref="EffectiveSpeed"/> as it stood at the start of the frame, run the
/// ticks, then call <see cref="Update"/> with what the frame cost. The new speed
/// applies from the next frame.
/// </para>
/// <para>
/// <b>The governed speed is held independently of the requested speed</b>, up to
/// <see cref="MaximumSpeed"/>, and <see cref="EffectiveSpeed"/> is the lesser of
/// the two. Holding it per request instead would make the governor climb from
/// the old request to the new one at the slow rising rate whenever the spectator
/// presses a faster speed key, and would report that climb as governing.
/// </para>
/// </remarks>
internal sealed class PlayRateGovernor
{
    /// <summary>
    /// The share of each frame that simulation work may use (design section
    /// 3.2), the same 50% share the thousand-unit performance plan uses for
    /// submit time.
    /// </summary>
    internal const double FrameBudgetShare = 0.5;

    /// <summary>
    /// The lowest speed the governor will hold, so a battle keeps moving on any
    /// machine (design section 3.2).
    /// </summary>
    internal const double MinimumSpeed = 0.25;

    /// <summary>
    /// The highest speed the spectator can request, and so the highest speed the
    /// governed state tracks. A sustainable rate above it carries no information
    /// the spectator could use, and tracking it would make a later fall from
    /// 50x to 2.6x take seconds.
    /// </summary>
    internal const double MaximumSpeed = 4d;

    /// <summary>
    /// Governed-state time constant while the target is below the current speed,
    /// in seconds. Provisional: a starting point to tune during the smoke rows
    /// (design section 3.2).
    /// </summary>
    internal const double FallingTimeConstantSeconds = 0.4d;

    /// <summary>
    /// Governed-state time constant while the target is above the current speed,
    /// in seconds. Provisional, as <see cref="FallingTimeConstantSeconds"/>.
    /// </summary>
    internal const double RisingTimeConstantSeconds = 2.5d;

    /// <summary>
    /// Time constant of the cost-per-tick average, in seconds. Provisional: this
    /// only keeps one slow tick from deciding the speed, since the governed
    /// state is smoothed again on top.
    /// </summary>
    internal const double CostTimeConstantSeconds = 0.25d;

    /// <summary>
    /// <see cref="IsGoverned"/> turns on when the effective speed is more than 5%
    /// below the requested speed (design section 3.5).
    /// </summary>
    internal const double GovernedThreshold = 0.95d;

    private double _smoothedCostSecondsPerTick;
    private bool _hasCost;
    private double _governedSpeed = MaximumSpeed;

    /// <summary>
    /// The speed to play at: the lesser of the requested speed as of the last
    /// <see cref="Update"/> and the governed speed. Reads 1 before the first
    /// update, the speed a battle opens at.
    /// </summary>
    public double EffectiveSpeed { get; private set; } = 1d;

    /// <summary>
    /// Whether the effective speed is more than 5% below the requested speed, as
    /// of the last <see cref="Update"/>.
    /// </summary>
    public bool IsGoverned { get; private set; }

    /// <summary>
    /// The smoothed cost of one tick, in seconds; zero before any frame that ran
    /// a tick. For the log's <c>costPerTickMicroseconds</c> field.
    /// </summary>
    public double SmoothedCostSecondsPerTick => _smoothedCostSecondsPerTick;

    /// <summary>
    /// Folds one frame into the governor and returns the effective speed.
    /// </summary>
    /// <param name="frameSeconds">Wall time since the previous frame. Not negative.</param>
    /// <param name="simulationWorkSeconds">
    /// Wall time this frame spent on simulation work. Not negative.
    /// </param>
    /// <param name="ticksRun">Ticks this frame ran. Not negative.</param>
    /// <param name="requestedSpeed">The speed the spectator asked for. Strictly positive.</param>
    /// <param name="tickRate">Simulation ticks per second. Strictly positive.</param>
    /// <returns>The effective speed, also available as <see cref="EffectiveSpeed"/>.</returns>
    /// <remarks>
    /// A frame that ran no ticks measures nothing, so it leaves the smoothed
    /// cost and the governed speed unchanged; only the clamp to the requested
    /// speed is reapplied. A paused battle does not drift the governor.
    /// </remarks>
    public double Update(
        double frameSeconds,
        double simulationWorkSeconds,
        int ticksRun,
        int requestedSpeed,
        int tickRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(simulationWorkSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(ticksRun);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedSpeed);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tickRate);

        if (ticksRun > 0 && frameSeconds > 0d)
        {
            FoldFrame(frameSeconds, simulationWorkSeconds / ticksRun, tickRate);
        }

        EffectiveSpeed = Math.Min(requestedSpeed, _governedSpeed);
        IsGoverned = EffectiveSpeed < requestedSpeed * GovernedThreshold;
        return EffectiveSpeed;
    }

    /// <summary>
    /// Forgets all history, as after a battle reset: no cost, ungoverned.
    /// </summary>
    public void Reset()
    {
        _smoothedCostSecondsPerTick = 0d;
        _hasCost = false;
        _governedSpeed = MaximumSpeed;
        EffectiveSpeed = 1d;
        IsGoverned = false;
    }

    private void FoldFrame(
        double frameSeconds,
        double costSecondsPerTick,
        int tickRate)
    {
        _smoothedCostSecondsPerTick = _hasCost
            ? Blend(
                _smoothedCostSecondsPerTick,
                costSecondsPerTick,
                frameSeconds,
                CostTimeConstantSeconds)
            : costSecondsPerTick;
        _hasCost = true;

        var target = _smoothedCostSecondsPerTick > 0d
            ? Math.Clamp(
                FrameBudgetShare / _smoothedCostSecondsPerTick / tickRate,
                MinimumSpeed,
                MaximumSpeed)
            : MaximumSpeed;
        var timeConstant = target < _governedSpeed
            ? FallingTimeConstantSeconds
            : RisingTimeConstantSeconds;
        _governedSpeed = Blend(_governedSpeed, target, frameSeconds, timeConstant);
    }

    private static double Blend(
        double current,
        double target,
        double frameSeconds,
        double timeConstantSeconds) =>
        current + ((target - current) * (1d - Math.Exp(-frameSeconds / timeConstantSeconds)));
}
