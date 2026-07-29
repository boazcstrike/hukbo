namespace Hukbo.Client.Diagnostics;

/// <summary>
/// One closed measurement window: what the frame loop cost over roughly a
/// second of wall time.
/// </summary>
/// <param name="Frames">Frames the window observed.</param>
/// <param name="ElapsedMilliseconds">
/// Wall time the window covers, which is the sum of its frame times rather
/// than a reading of a clock, so the figures inside it always agree with each
/// other.
/// </param>
/// <param name="MeanMilliseconds">Mean frame time over the window.</param>
/// <param name="WorstMilliseconds">The single worst frame in the window.</param>
/// <param name="WorstUpdateMilliseconds">
/// The worst <c>Update</c> span in the window. Not necessarily from the same
/// frame as <see cref="WorstMilliseconds"/>: a window that reports a bad frame
/// and a healthy update spent the time in draw, in present, or in a stall
/// outside the game's own code.
/// </param>
/// <param name="WorstDrawMilliseconds">The worst <c>Draw</c> span in the window.</param>
/// <param name="SimulationTicks">Simulation ticks advanced during the window.</param>
/// <param name="StarvedFrames">
/// Frames whose accumulated simulation time hit the catch-up clamp, meaning
/// the frame arrived so late that whole ticks were dropped rather than run.
/// Anything above zero is the simulation visibly falling behind the wall
/// clock, which is what a spectator reads as warriors jumping instead of
/// walking.
/// </param>
internal readonly record struct FrameTimingWindow(
    int Frames,
    double ElapsedMilliseconds,
    double MeanMilliseconds,
    double WorstMilliseconds,
    double WorstUpdateMilliseconds,
    double WorstDrawMilliseconds,
    int SimulationTicks,
    int StarvedFrames);

/// <summary>
/// Collects per-frame timings and hands back a summary once a window's worth
/// of wall time has passed.
/// </summary>
/// <remarks>
/// <para>
/// A per-frame log line is the honest record but an unreadable one: a healthy
/// minute is thousands of lines that all say the same thing. This reduces the
/// stream to one line per second, which is the cadence
/// <c>CLAUDE.md</c> puts at <c>dbg</c>, while the per-frame line stays
/// available at <c>trc</c> for the case where a single stall has to be located
/// exactly.
/// </para>
/// <para>
/// Deliberately holds no clock and no log. It is fed elapsed seconds by the
/// frame loop and returns a value, which is what lets every assertion about it
/// run without a window, a graphics device, or a wall clock.
/// </para>
/// <para>
/// Fixed-size state: eight fields and no collection. A window that never
/// closes — a game paused at a breakpoint for an hour — accumulates counters,
/// never entries.
/// </para>
/// </remarks>
internal sealed class FrameTimingAggregator
{
    /// <summary>
    /// Wall time a window covers before it closes. One second, so a line's
    /// frame count reads directly as frames per second.
    /// </summary>
    internal const double WindowMilliseconds = 1_000d;

    private double _elapsedMilliseconds;
    private double _worstMilliseconds;
    private double _worstUpdateMilliseconds;
    private double _worstDrawMilliseconds;
    private int _frames;
    private int _simulationTicks;
    private int _starvedFrames;

    /// <summary>Frames observed since the last window closed.</summary>
    internal int PendingFrames => _frames;

    /// <summary>
    /// Records one frame. A non-finite or negative duration is treated as
    /// zero rather than rejected: a diagnostic must not be able to throw
    /// inside the frame loop it is measuring.
    /// </summary>
    public void Add(
        double frameMilliseconds,
        double updateMilliseconds,
        double drawMilliseconds,
        int simulationTicks,
        bool simulationStarved)
    {
        var frame = Sanitize(frameMilliseconds);
        var update = Sanitize(updateMilliseconds);
        var draw = Sanitize(drawMilliseconds);

        _frames++;
        _elapsedMilliseconds += frame;
        _simulationTicks += Math.Max(0, simulationTicks);

        if (frame > _worstMilliseconds)
        {
            _worstMilliseconds = frame;
        }

        if (update > _worstUpdateMilliseconds)
        {
            _worstUpdateMilliseconds = update;
        }

        if (draw > _worstDrawMilliseconds)
        {
            _worstDrawMilliseconds = draw;
        }

        if (simulationStarved)
        {
            _starvedFrames++;
        }
    }

    /// <summary>
    /// Closes and returns the window when a window's worth of wall time has
    /// accumulated, and resets for the next one. Returns false otherwise,
    /// having changed nothing.
    /// </summary>
    public bool TryTakeWindow(out FrameTimingWindow window)
    {
        if (_frames == 0 || _elapsedMilliseconds < WindowMilliseconds)
        {
            window = default;
            return false;
        }

        window = new FrameTimingWindow(
            _frames,
            _elapsedMilliseconds,
            _elapsedMilliseconds / _frames,
            _worstMilliseconds,
            _worstUpdateMilliseconds,
            _worstDrawMilliseconds,
            _simulationTicks,
            _starvedFrames);

        Reset();
        return true;
    }

    /// <summary>
    /// Drops the open window. Called when the thing being measured stops being
    /// one continuous run — a round reset — so the first window of a new round
    /// does not average in the frame that built its scenario.
    /// </summary>
    public void Reset()
    {
        _elapsedMilliseconds = 0d;
        _worstMilliseconds = 0d;
        _worstUpdateMilliseconds = 0d;
        _worstDrawMilliseconds = 0d;
        _frames = 0;
        _simulationTicks = 0;
        _starvedFrames = 0;
    }

    private static double Sanitize(double milliseconds) =>
        double.IsFinite(milliseconds) && milliseconds > 0d ? milliseconds : 0d;
}
