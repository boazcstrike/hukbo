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
    internal const double GovernedOnThreshold = 0.95d;

    /// <summary>
    /// <see cref="IsGoverned"/> turns back off only once the effective speed is
    /// within 2% of the requested speed. The gap between the two thresholds keeps
    /// a sustainable speed hovering near 5% below the request from flipping the
    /// status text and the governed log line every few frames. Provisional.
    /// </summary>
    internal const double GovernedOffThreshold = 0.98d;

    /// <summary>
    /// The longest wall-time span a single fold may blend over, in seconds, so
    /// one hitch frame cannot replace the smoothed state: at the cost time
    /// constant it caps one fold's weight near one third. Provisional.
    /// </summary>
    internal const double MaximumBlendSeconds = 0.1d;

    private double _smoothedCostSecondsPerTick;
    private bool _hasCost;
    private double _governedSpeed = MaximumSpeed;
    private double _unfoldedSeconds;

    /// <summary>
    /// The speed to play at: the lesser of the requested speed as of the last
    /// <see cref="Update"/> and the governed speed. Reads 1 before the first
    /// update, the speed a battle opens at.
    /// </summary>
    public double EffectiveSpeed { get; private set; } = 1d;

    /// <summary>
    /// Whether the effective speed is more than 5% below the requested speed, as
    /// of the last <see cref="Update"/>. Latched: it stays on until the effective
    /// speed is back within 2% of the request.
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
    /// speed is reapplied. A paused battle does not drift the governor. Its wall
    /// time is not lost: it is held and credited to the next frame that ran a
    /// tick, so the time constants mean wall-clock seconds even when ticks land
    /// on one frame in three. <c>Update(0, 0, 0, requested, tickRate)</c> is the
    /// way to re-clamp to a changed request without a sample.
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

        var elapsedSeconds = _unfoldedSeconds + frameSeconds;
        if (ticksRun > 0 && elapsedSeconds > 0d)
        {
            FoldFrame(
                Math.Min(elapsedSeconds, MaximumBlendSeconds),
                simulationWorkSeconds / ticksRun,
                tickRate);
            _unfoldedSeconds = 0d;
        }
        else
        {
            _unfoldedSeconds = Math.Min(elapsedSeconds, MaximumBlendSeconds);
        }

        EffectiveSpeed = Math.Min(requestedSpeed, _governedSpeed);
        IsGoverned = IsGoverned
            ? EffectiveSpeed < requestedSpeed * GovernedOffThreshold
            : EffectiveSpeed < requestedSpeed * GovernedOnThreshold;
        return EffectiveSpeed;
    }

    /// <summary>
    /// Forgets all history, as after a battle reset: no cost, ungoverned. Not for
    /// a changed request, which <see cref="Update"/> re-clamps without a sample.
    /// </summary>
    public void Reset()
    {
        _smoothedCostSecondsPerTick = 0d;
        _hasCost = false;
        _governedSpeed = MaximumSpeed;
        _unfoldedSeconds = 0d;
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
