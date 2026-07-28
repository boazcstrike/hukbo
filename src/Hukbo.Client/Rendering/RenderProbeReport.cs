using System.Text.Json;

namespace Hukbo.Client.Rendering;

/// <summary>
/// The JSON report <c>tools/Hukbo.Tools.RenderProbe</c> writes under
/// <c>artifacts/</c> (VIS-035, integration design section 11). Defined here,
/// inside the gate, rather than in the hand-run tool project, so the schema
/// itself is exercised by an xunit round-trip test even though the harness
/// that produces a real one is not (it needs a window and a GPU).
/// </summary>
/// <param name="Fingerprint">The configuration this report was captured under.</param>
/// <param name="AgentCount">Total agents in the scripted scenario (both factions).</param>
/// <param name="Seed">The scenario seed the probe ran, for reproducing a station.</param>
/// <param name="Stations">
/// One aggregated result per camera station the probe drove — minimum zoom,
/// default fit, and maximum zoom, per the measurement matrix.
/// </param>
public sealed record RenderProbeReport(
    RenderProbeFingerprint Fingerprint,
    int AgentCount,
    ulong Seed,
    IReadOnlyList<RenderProbeStationResult> Stations)
{
    /// <summary>
    /// Shared between the probe's writer and this report's own round-trip
    /// test, so both sides of the schema agree by construction rather than
    /// by convention.
    /// </summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}

/// <summary>
/// The hardware, resolution, and build a <see cref="RenderProbeReport"/> was
/// captured under, so a later run can be judged comparable at a glance.
/// </summary>
public sealed record RenderProbeFingerprint(
    string HardwareName,
    int ResolutionWidth,
    int ResolutionHeight,
    string BuildConfiguration,
    DateTime CapturedAtUtc);

/// <summary>
/// One camera station's aggregated samples: frame-time percentiles, the peak
/// arena submission count observed, and the GC/allocation delta across the
/// sampled frames (not cumulative process totals — see
/// <see cref="RenderProbeStatistics.Summarize"/>).
/// </summary>
public sealed record RenderProbeStationResult(
    string StationName,
    int FrameCount,
    double FrameMillisecondsP50,
    double FrameMillisecondsP95,
    double FrameMillisecondsP99,
    int ArenaSubmissionCountMaximum,
    int Gen0CollectionsDelta,
    int Gen1CollectionsDelta,
    int Gen2CollectionsDelta,
    long AllocatedBytesDelta);

/// <summary>
/// Pure percentile and delta arithmetic over a station's captured
/// <see cref="RenderProbeSample"/> frames. Kept separate from
/// <c>Hukbo.Tools.RenderProbe</c> so the math is unit-testable in the gate
/// even though the harness that calls it is not (VIS-035).
/// </summary>
public static class RenderProbeStatistics
{
    /// <summary>
    /// Nearest-rank percentile over an already-sorted, ascending sequence.
    /// Mirrors the percentile helper in
    /// <c>tools/Hukbo.Tools.CueDemand/Program.cs</c> so the two harnesses
    /// report numbers the same way.
    /// </summary>
    public static double Percentile(
        IReadOnlyList<double> sortedAscendingValues,
        double percentile)
    {
        ArgumentNullException.ThrowIfNull(sortedAscendingValues);
        if (sortedAscendingValues.Count == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percentile * sortedAscendingValues.Count) - 1;
        return sortedAscendingValues[
            Math.Clamp(rank, 0, sortedAscendingValues.Count - 1)];
    }

    /// <summary>
    /// Reduces one station's captured frames to a
    /// <see cref="RenderProbeStationResult"/>. GC and allocation figures are
    /// the delta between the first and last sample in the window, so they
    /// read as "steady-state cost of this station" rather than "everything
    /// since process start".
    /// </summary>
    public static RenderProbeStationResult Summarize(
        string stationName,
        IReadOnlyList<RenderProbeSample> samples)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stationName);
        ArgumentNullException.ThrowIfNull(samples);

        if (samples.Count == 0)
        {
            return new RenderProbeStationResult(
                stationName, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var sortedFrameMilliseconds = samples
            .Select(sample => sample.FrameMilliseconds)
            .Order()
            .ToArray();
        var first = samples[0];
        var last = samples[^1];
        var maximumSubmissions = 0;
        foreach (var sample in samples)
        {
            if (sample.ArenaSubmissionCount > maximumSubmissions)
            {
                maximumSubmissions = sample.ArenaSubmissionCount;
            }
        }

        return new RenderProbeStationResult(
            stationName,
            samples.Count,
            Percentile(sortedFrameMilliseconds, 0.50),
            Percentile(sortedFrameMilliseconds, 0.95),
            Percentile(sortedFrameMilliseconds, 0.99),
            maximumSubmissions,
            last.Gen0Collections - first.Gen0Collections,
            last.Gen1Collections - first.Gen1Collections,
            last.Gen2Collections - first.Gen2Collections,
            last.AllocatedBytes - first.AllocatedBytes);
    }
}
