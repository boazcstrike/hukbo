namespace Hukbo.Diagnostics;

/// <summary>
/// How often a per-tick observation is emitted.
/// </summary>
/// <remarks>
/// Declared once and shared by the client and the headless runner so a log from
/// either has the same cadence and the same meaning. Sampling is what keeps
/// <c>sim.tick</c> affordable at <see cref="LogLevel.Debug"/>: forty lines per
/// ten thousand ticks is enough to bisect a divergence to a narrow window,
/// while <see cref="LogLevel.Trace"/> keeps every tick for when that window has
/// to be read in full.
/// </remarks>
public static class LogSampling
{
    /// <summary>
    /// Ticks between sampled <c>sim.tick</c> lines at
    /// <see cref="LogLevel.Debug"/>. A power of two so the test is a mask.
    /// </summary>
    public const int SimulationTickInterval = 256;

    /// <summary>
    /// Whether this tick is one the debug tier samples.
    /// </summary>
    /// <remarks>
    /// Callers observe a tick after advancing it, so the first sampled row of a
    /// run is tick 256 rather than tick 0. The opening baseline comes from
    /// <c>sim.scenario.built</c> instead, which carries the seed, the agent
    /// count, and the map size.
    /// </remarks>
    public static bool IsSampledTick(long tick) =>
        (tick % SimulationTickInterval) == 0;
}
