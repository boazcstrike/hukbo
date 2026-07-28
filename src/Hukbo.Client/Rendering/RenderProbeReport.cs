using System.Text.Json;

namespace Hukbo.Client.Rendering;

/// <summary>
/// The JSON report <c>tools/Hukbo.Tools.RenderProbe</c> writes under
/// <c>artifacts/</c> (VIS-035, integration design section 11; retrofitted
/// onto the renderer-agnostic Tier 1/Tier 2 seam by VIS-035R, amendment A-1,
/// 2026-07-28, user-approved). Defined here, inside the gate, rather than in
/// the hand-run tool project, so the schema itself is exercised by an xunit
/// round-trip test even though the harness that produces a real one is not
/// (it needs a window and a GPU).
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
/// The hardware, resolution, build, and renderer backend a
/// <see cref="RenderProbeReport"/> was captured under, so a later run — on
/// the same backend or, eventually, a different one — can be judged
/// comparable at a glance.
/// </summary>
/// <param name="Backend">
/// Names the active renderer this report's <see cref="RenderMetricsSnapshot"/>
/// values were recorded against (VIS-034, amendment A-1) —
/// <c>"spritebatch-1x1"</c> for today's immediate-mode <c>SpriteBatch</c>
/// renderer sharing one 1x1 pixel texture. Recorded explicitly so that a
/// future GPU-instanced backend's report is legibly a different backend
/// rather than silently comparable numbers denominated in an incompatible
/// unit — the whole point of amendment A-1's Tier 1/Tier 2 split.
/// </param>
/// <param name="VerticalRetraceSynchronized">
/// GPU-002. Whether the probe ran with the graphics device synchronized to
/// the display's vertical retrace. A synchronized run cannot report a frame
/// time below the display's refresh interval, so every frame-time percentile
/// in this report is a floor rather than a measurement whenever this is
/// <see langword="true"/>. Recorded on the fingerprint rather than per
/// station because it is a property of the whole run.
/// </param>
/// <param name="ProbeDuplicationFactor">
/// GPU-002. How many times the probe build that wrote this report evaluated
/// the pure pawn-geometry helper per pawn the renderer actually drew — the
/// ratio of recorded
/// <see cref="RenderMetricsSnapshot.PawnGeometryInvocations"/> to the
/// invocations the draw path alone needs. A value of 1 means the probe added
/// no duplicate counting pass; a value near 2 means every visible pawn's
/// geometry was built twice, once for the renderer and once for the probe's
/// own counting. A value of 0 means the probe build that wrote this report
/// did not measure duplication at all, which is the honest reading for any
/// report captured before GPU-005 landed the measurement.
/// </param>
public sealed record RenderProbeFingerprint(
    string HardwareName,
    int ResolutionWidth,
    int ResolutionHeight,
    string BuildConfiguration,
    string Backend,
    bool VerticalRetraceSynchronized,
    double ProbeDuplicationFactor,
    DateTime CapturedAtUtc);

/// <summary>
/// One camera station's aggregated samples: frame-time percentiles; Tier 1
/// (renderer-invariant, budgeted) quad/triangle peaks and geometry-build/
/// submit CPU-time percentiles; Tier 2 (backend-specific, diagnostic-only)
/// submission/batch/texture-bind/buffer-upload peaks, each paired with an
/// <c>*Applicable</c> flag so a metric the active backend does not produce
/// stays distinguishable from a genuine zero; the GC/allocation delta
/// across the sampled frames (not cumulative process totals — see
/// <see cref="RenderProbeStatistics.Summarize"/>); and the Tier 1
/// appearance-cache hit/miss/fill counters, which are the only counters here
/// reported as both a peak and a percentile triple (GPU-018a, see their own
/// parameter documentation for why).
/// </summary>
/// <param name="QuadsMaximum">
/// Tier 1, budgeted. The peak <see cref="RenderMetricsSnapshot.Quads"/>
/// observed across this station's sampled frames — the number every
/// <c>RenderBudgetEstimate</c> ceiling is compared against.
/// </param>
/// <param name="TrianglesMaximum">
/// Tier 1. The peak <see cref="RenderMetricsSnapshot.Triangles"/> observed,
/// recorded independently of <see cref="QuadsMaximum"/> rather than derived
/// from it (a future non-quad backend could report the two independently).
/// </param>
/// <param name="PawnGeometryInvocationsMaximum">
/// Tier 1 (GPU-002, GPU-005). The peak
/// <see cref="RenderMetricsSnapshot.PawnGeometryInvocations"/> observed
/// across this station's sampled frames. Reported as a peak rather than a
/// percentile because it is a count, matching <see cref="QuadsMaximum"/>'s
/// own treatment, and it is what
/// <see cref="RenderProbeFingerprint.ProbeDuplicationFactor"/> is checked
/// against for self-consistency.
/// </param>
/// <param name="GeometryBuildMicrosecondsP50">
/// Tier 1. Median CPU time inside the pure geometry helpers this station's
/// frames spent, mirroring <see cref="FrameMillisecondsP50"/>'s percentile
/// treatment rather than a peak, since this is a duration, not a count.
/// </param>
/// <param name="GeometryBuildMicrosecondsP95">The 95th percentile of the same distribution.</param>
/// <param name="GeometryBuildMicrosecondsP99">The 99th percentile of the same distribution.</param>
/// <param name="SubmitMicrosecondsP50">
/// Tier 1. Median CPU time from first arena submission call to end of arena
/// frame submission.
/// </param>
/// <param name="SubmitMicrosecondsP95">The 95th percentile of the same distribution.</param>
/// <param name="SubmitMicrosecondsP99">The 99th percentile of the same distribution.</param>
/// <param name="ClearMicrosecondsP50">
/// Tier 1 (GPU-002, GPU-003). Median CPU time this station's frames spent
/// inside <c>GraphicsDevice.Clear</c>. Reported as percentiles rather than a
/// peak for the same reason <see cref="GeometryBuildMicrosecondsP50"/> is:
/// this is a duration, not a count.
/// </param>
/// <param name="ClearMicrosecondsP95">The 95th percentile of the same distribution.</param>
/// <param name="ClearMicrosecondsP99">The 99th percentile of the same distribution.</param>
/// <param name="LayoutMicrosecondsP50">
/// Tier 1 (GPU-002, GPU-003). Median CPU time this station's frames spent
/// resolving the screen layout before anything was drawn.
/// </param>
/// <param name="LayoutMicrosecondsP95">The 95th percentile of the same distribution.</param>
/// <param name="LayoutMicrosecondsP99">The 99th percentile of the same distribution.</param>
/// <param name="HoverSelectionMicrosecondsP50">
/// Tier 1 (GPU-002, GPU-003). Median CPU time this station's frames spent
/// resolving the pointer's hovered agent and the resulting selection state.
/// </param>
/// <param name="HoverSelectionMicrosecondsP95">The 95th percentile of the same distribution.</param>
/// <param name="HoverSelectionMicrosecondsP99">The 99th percentile of the same distribution.</param>
/// <param name="UiLayerMicrosecondsP50">
/// Tier 1 (GPU-002, GPU-003). Median CPU time this station's frames spent
/// drawing the user interface layer, which is separate from the arena layer
/// the budget is written against.
/// </param>
/// <param name="UiLayerMicrosecondsP95">The 95th percentile of the same distribution.</param>
/// <param name="UiLayerMicrosecondsP99">The 99th percentile of the same distribution.</param>
/// <param name="BaseDrawMicrosecondsP50">
/// Tier 1 (GPU-002, GPU-003). Median CPU time this station's frames spent
/// inside the base draw call, so the portion of the frame this schema does
/// not otherwise name stays attributable rather than becoming residual.
/// </param>
/// <param name="BaseDrawMicrosecondsP95">The 95th percentile of the same distribution.</param>
/// <param name="BaseDrawMicrosecondsP99">The 99th percentile of the same distribution.</param>
/// <param name="ArenaGeometryMicrosecondsP50">
/// Tier 1 (GPU-002, GPU-004). Median CPU time this station's frames spent
/// constructing the arena's real per-pawn geometry — the geometry the
/// renderer actually draws from — held separate from
/// <see cref="SubmitMicrosecondsP50"/>, which after GPU-004 narrows to
/// submission work alone.
/// </param>
/// <param name="ArenaGeometryMicrosecondsP95">The 95th percentile of the same distribution.</param>
/// <param name="ArenaGeometryMicrosecondsP99">The 99th percentile of the same distribution.</param>
/// <param name="ProbeOverheadMicrosecondsP50">
/// Tier 1 (GPU-002, GPU-005). Median CPU time this station's frames spent on
/// the probe's own duplicate counting pass. Reported separately so probe
/// overhead is never silently folded into a figure a budget is written
/// against.
/// </param>
/// <param name="ProbeOverheadMicrosecondsP95">The 95th percentile of the same distribution.</param>
/// <param name="ProbeOverheadMicrosecondsP99">The 99th percentile of the same distribution.</param>
/// <param name="ManagedBytesAllocatedMaximum">
/// Tier 1 (R-W4.10). The peak single-frame
/// <see cref="RenderMetricsSnapshot.ManagedBytesAllocated"/> observed —
/// distinct from <see cref="AllocatedBytesDelta"/>, which is the whole
/// station's cumulative allocation window rather than one frame's peak.
/// </param>
/// <param name="SubmissionsMaximum">
/// Tier 2, diagnostic only. Peak <c>SpriteBatch.Draw</c> calls under the
/// current backend.
/// </param>
/// <param name="SubmissionsApplicable">
/// Whether <see cref="SubmissionsMaximum"/> means anything under the active
/// backend (taken from the station's last sample — Tier 2 applicability is a
/// backend constant, not something that varies frame to frame).
/// </param>
/// <param name="BatchesMaximum">
/// Tier 2, diagnostic only. Peak <c>Begin</c>/<c>End</c> pairs under the
/// current backend (R-W4.5, "one batch, one texture" — retained as a Tier 2
/// assertion scoped to this backend).
/// </param>
/// <param name="BatchesApplicable">Whether <see cref="BatchesMaximum"/> means anything under the active backend.</param>
/// <param name="TextureBindsMaximum">Tier 2, diagnostic only. Peak texture binds.</param>
/// <param name="TextureBindsApplicable">Whether <see cref="TextureBindsMaximum"/> means anything under the active backend.</param>
/// <param name="BufferUploadBytesMaximum">
/// Tier 2, diagnostic only. Peak instance-buffer upload bytes — always 0 and
/// not applicable under the current <c>SpriteBatch</c> backend, which
/// uploads none.
/// </param>
/// <param name="BufferUploadBytesApplicable">Whether <see cref="BufferUploadBytesMaximum"/> means anything under the active backend.</param>
/// <param name="AppearanceCacheHitsMaximum">
/// Tier 1 (GPU-017, carried through by GPU-018a). The peak
/// <see cref="RenderMetricsSnapshot.AppearanceCacheHits"/> observed across
/// this station's sampled frames.
///
/// The three appearance-cache counters are the only counters in this schema
/// reported as a peak AND a percentile triple, and the reason is that
/// GPU-018's completion criterion is a ratio — "a probe run reports a hit
/// rate approaching 1 after the first frame" — and a ratio needs both of its
/// terms drawn from comparable frames. Peaks alone cannot supply that: the
/// peak hit count comes from a steady-state frame while the peak miss count
/// comes from the one cold frame, so dividing one by the other mixes two
/// different frames and yields a number that means nothing. The percentile
/// triple supplies the ratio, because at any rank at or below P99 both terms
/// describe a warm frame. The peak is kept alongside it because the converse
/// is also true: in a station of a few hundred frames the single cold frame
/// is an outlier that every percentile up to P99 discards, so without a peak
/// the report would carry no evidence at all that the cache was ever cold,
/// ever filled, or how large the warm-up was — and "approaching 1 after the
/// first frame" cannot be judged without being able to see the first frame.
/// </param>
/// <param name="AppearanceCacheHitsP50">
/// Tier 1 (GPU-018a). Median per-frame appearance-cache hit count. Paired
/// with <see cref="AppearanceCacheMissesP50"/> this is the median frame's hit
/// rate, which is the figure GPU-018's criterion is read from.
/// </param>
/// <param name="AppearanceCacheHitsP95">The 95th percentile of the same distribution.</param>
/// <param name="AppearanceCacheHitsP99">The 99th percentile of the same distribution.</param>
/// <param name="AppearanceCacheMissesMaximum">
/// Tier 1 (GPU-018a). The peak
/// <see cref="RenderMetricsSnapshot.AppearanceCacheMisses"/> observed. In a
/// healthy run this is the cold frame's count and is roughly the agent count;
/// it says the cache started empty, not that it is faulty.
/// </param>
/// <param name="AppearanceCacheMissesP50">
/// Tier 1 (GPU-018a). Median per-frame appearance-cache miss count. Expected
/// to be 0: a non-zero median means the cache is missing in steady state, which
/// is the key-or-lifetime fault
/// <see cref="IRenderMetricsRecorder.AddAppearanceCacheMisses"/> exists to
/// expose.
/// </param>
/// <param name="AppearanceCacheMissesP95">
/// The 95th percentile of the same distribution. Also expected to be 0 — a
/// non-zero figure here means more than one frame in twenty missed, which the
/// median alone would hide.
/// </param>
/// <param name="AppearanceCacheMissesP99">The 99th percentile of the same distribution.</param>
/// <param name="AppearanceCacheFillsMaximum">
/// Tier 1 (GPU-018a). The peak
/// <see cref="RenderMetricsSnapshot.AppearanceCacheFills"/> observed — the
/// size of the warm-up. A 0 here in a station that recorded hits or misses
/// means no slot went from empty to occupied during the station, which for the
/// first station of a run would itself be a finding.
/// </param>
/// <param name="AppearanceCacheFillsP50">
/// Tier 1 (GPU-018a). Median per-frame fill count, expected to be 0 once the
/// cache is warm. A non-zero median means slots are being refilled every
/// frame, which is an ordinal being reused rather than a cold start.
/// </param>
/// <param name="AppearanceCacheFillsP95">The 95th percentile of the same distribution.</param>
/// <param name="AppearanceCacheFillsP99">The 99th percentile of the same distribution.</param>
public sealed record RenderProbeStationResult(
    string StationName,
    int FrameCount,
    double FrameMillisecondsP50,
    double FrameMillisecondsP95,
    double FrameMillisecondsP99,
    int QuadsMaximum,
    int TrianglesMaximum,
    int PawnGeometryInvocationsMaximum,
    double GeometryBuildMicrosecondsP50,
    double GeometryBuildMicrosecondsP95,
    double GeometryBuildMicrosecondsP99,
    double SubmitMicrosecondsP50,
    double SubmitMicrosecondsP95,
    double SubmitMicrosecondsP99,
    double ClearMicrosecondsP50,
    double ClearMicrosecondsP95,
    double ClearMicrosecondsP99,
    double LayoutMicrosecondsP50,
    double LayoutMicrosecondsP95,
    double LayoutMicrosecondsP99,
    double HoverSelectionMicrosecondsP50,
    double HoverSelectionMicrosecondsP95,
    double HoverSelectionMicrosecondsP99,
    double UiLayerMicrosecondsP50,
    double UiLayerMicrosecondsP95,
    double UiLayerMicrosecondsP99,
    double BaseDrawMicrosecondsP50,
    double BaseDrawMicrosecondsP95,
    double BaseDrawMicrosecondsP99,
    double ArenaGeometryMicrosecondsP50,
    double ArenaGeometryMicrosecondsP95,
    double ArenaGeometryMicrosecondsP99,
    double ProbeOverheadMicrosecondsP50,
    double ProbeOverheadMicrosecondsP95,
    double ProbeOverheadMicrosecondsP99,
    long ManagedBytesAllocatedMaximum,
    int SubmissionsMaximum,
    bool SubmissionsApplicable,
    int BatchesMaximum,
    bool BatchesApplicable,
    int TextureBindsMaximum,
    bool TextureBindsApplicable,
    long BufferUploadBytesMaximum,
    bool BufferUploadBytesApplicable,
    int Gen0CollectionsDelta,
    int Gen1CollectionsDelta,
    int Gen2CollectionsDelta,
    long AllocatedBytesDelta,
    int AppearanceCacheHitsMaximum,
    double AppearanceCacheHitsP50,
    double AppearanceCacheHitsP95,
    double AppearanceCacheHitsP99,
    int AppearanceCacheMissesMaximum,
    double AppearanceCacheMissesP50,
    double AppearanceCacheMissesP95,
    double AppearanceCacheMissesP99,
    int AppearanceCacheFillsMaximum,
    double AppearanceCacheFillsP50,
    double AppearanceCacheFillsP95,
    double AppearanceCacheFillsP99);

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
    /// <see cref="RenderProbeStationResult"/>. Tier 1/Tier 2 counts and
    /// <see cref="RenderMetricsSnapshot.ManagedBytesAllocated"/> are reported
    /// as their peak observed value (the number a budget ceiling is compared
    /// against); every Tier 1 CPU-time span is reported as percentiles,
    /// matching <see cref="RenderProbeStationResult.FrameMillisecondsP50"/>'s
    /// own treatment. GC and allocation figures are the delta between the
    /// first and last sample in the window, so they read as "steady-state
    /// cost of this station" rather than "everything since process start".
    /// The three appearance-cache counters are the one exception to the
    /// count-means-peak rule: they are reported both ways (GPU-018a), because
    /// the hit rate they exist to establish is a ratio the peaks cannot form
    /// and the warm-up they exist to explain is an outlier the percentiles
    /// discard.
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
                stationName, 0,
                0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                0, 0, 0,
                0,
                0, false,
                0, false,
                0, false,
                0, false,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0);
        }

        var sortedFrameMilliseconds = samples
            .Select(sample => sample.FrameMilliseconds)
            .Order()
            .ToArray();
        var sortedGeometryBuildMicroseconds = samples
            .Select(sample => sample.Metrics.GeometryBuildMicroseconds)
            .Order()
            .ToArray();
        var sortedSubmitMicroseconds = samples
            .Select(sample => sample.Metrics.SubmitMicroseconds)
            .Order()
            .ToArray();
        var sortedClearMicroseconds = samples
            .Select(sample => sample.Metrics.ClearMicroseconds)
            .Order()
            .ToArray();
        var sortedLayoutMicroseconds = samples
            .Select(sample => sample.Metrics.LayoutMicroseconds)
            .Order()
            .ToArray();
        var sortedHoverSelectionMicroseconds = samples
            .Select(sample => sample.Metrics.HoverSelectionMicroseconds)
            .Order()
            .ToArray();
        var sortedUiLayerMicroseconds = samples
            .Select(sample => sample.Metrics.UiLayerMicroseconds)
            .Order()
            .ToArray();
        var sortedBaseDrawMicroseconds = samples
            .Select(sample => sample.Metrics.BaseDrawMicroseconds)
            .Order()
            .ToArray();
        var sortedArenaGeometryMicroseconds = samples
            .Select(sample => sample.Metrics.ArenaGeometryMicroseconds)
            .Order()
            .ToArray();
        var sortedProbeOverheadMicroseconds = samples
            .Select(sample => sample.Metrics.ProbeOverheadMicroseconds)
            .Order()
            .ToArray();

        // GPU-018a. Widened to double on the way in rather than percentiled as
        // integers, because Percentile is nearest-rank over an ascending
        // sequence and returns an element of it: every value it can return is
        // one of these counts exactly, so the widening is lossless and no
        // interpolated half-a-hit can appear in the report.
        var sortedAppearanceCacheHits = samples
            .Select(sample => (double)sample.Metrics.AppearanceCacheHits)
            .Order()
            .ToArray();
        var sortedAppearanceCacheMisses = samples
            .Select(sample => (double)sample.Metrics.AppearanceCacheMisses)
            .Order()
            .ToArray();
        var sortedAppearanceCacheFills = samples
            .Select(sample => (double)sample.Metrics.AppearanceCacheFills)
            .Order()
            .ToArray();

        var first = samples[0];
        var last = samples[^1];
        var lastMetrics = last.Metrics;

        var quadsMaximum = 0;
        var trianglesMaximum = 0;
        var pawnGeometryInvocationsMaximum = 0;
        var managedBytesAllocatedMaximum = 0L;
        var submissionsMaximum = 0;
        var batchesMaximum = 0;
        var textureBindsMaximum = 0;
        var bufferUploadBytesMaximum = 0L;
        var appearanceCacheHitsMaximum = 0;
        var appearanceCacheMissesMaximum = 0;
        var appearanceCacheFillsMaximum = 0;

        foreach (var sample in samples)
        {
            var metrics = sample.Metrics;
            if (metrics.Quads > quadsMaximum)
            {
                quadsMaximum = metrics.Quads;
            }

            if (metrics.Triangles > trianglesMaximum)
            {
                trianglesMaximum = metrics.Triangles;
            }

            if (metrics.PawnGeometryInvocations > pawnGeometryInvocationsMaximum)
            {
                pawnGeometryInvocationsMaximum = metrics.PawnGeometryInvocations;
            }

            if (metrics.ManagedBytesAllocated > managedBytesAllocatedMaximum)
            {
                managedBytesAllocatedMaximum = metrics.ManagedBytesAllocated;
            }

            if (metrics.Submissions > submissionsMaximum)
            {
                submissionsMaximum = metrics.Submissions;
            }

            if (metrics.Batches > batchesMaximum)
            {
                batchesMaximum = metrics.Batches;
            }

            if (metrics.TextureBinds > textureBindsMaximum)
            {
                textureBindsMaximum = metrics.TextureBinds;
            }

            if (metrics.BufferUploadBytes > bufferUploadBytesMaximum)
            {
                bufferUploadBytesMaximum = metrics.BufferUploadBytes;
            }

            if (metrics.AppearanceCacheHits > appearanceCacheHitsMaximum)
            {
                appearanceCacheHitsMaximum = metrics.AppearanceCacheHits;
            }

            if (metrics.AppearanceCacheMisses > appearanceCacheMissesMaximum)
            {
                appearanceCacheMissesMaximum = metrics.AppearanceCacheMisses;
            }

            if (metrics.AppearanceCacheFills > appearanceCacheFillsMaximum)
            {
                appearanceCacheFillsMaximum = metrics.AppearanceCacheFills;
            }
        }

        return new RenderProbeStationResult(
            stationName,
            samples.Count,
            Percentile(sortedFrameMilliseconds, 0.50),
            Percentile(sortedFrameMilliseconds, 0.95),
            Percentile(sortedFrameMilliseconds, 0.99),
            quadsMaximum,
            trianglesMaximum,
            pawnGeometryInvocationsMaximum,
            Percentile(sortedGeometryBuildMicroseconds, 0.50),
            Percentile(sortedGeometryBuildMicroseconds, 0.95),
            Percentile(sortedGeometryBuildMicroseconds, 0.99),
            Percentile(sortedSubmitMicroseconds, 0.50),
            Percentile(sortedSubmitMicroseconds, 0.95),
            Percentile(sortedSubmitMicroseconds, 0.99),
            Percentile(sortedClearMicroseconds, 0.50),
            Percentile(sortedClearMicroseconds, 0.95),
            Percentile(sortedClearMicroseconds, 0.99),
            Percentile(sortedLayoutMicroseconds, 0.50),
            Percentile(sortedLayoutMicroseconds, 0.95),
            Percentile(sortedLayoutMicroseconds, 0.99),
            Percentile(sortedHoverSelectionMicroseconds, 0.50),
            Percentile(sortedHoverSelectionMicroseconds, 0.95),
            Percentile(sortedHoverSelectionMicroseconds, 0.99),
            Percentile(sortedUiLayerMicroseconds, 0.50),
            Percentile(sortedUiLayerMicroseconds, 0.95),
            Percentile(sortedUiLayerMicroseconds, 0.99),
            Percentile(sortedBaseDrawMicroseconds, 0.50),
            Percentile(sortedBaseDrawMicroseconds, 0.95),
            Percentile(sortedBaseDrawMicroseconds, 0.99),
            Percentile(sortedArenaGeometryMicroseconds, 0.50),
            Percentile(sortedArenaGeometryMicroseconds, 0.95),
            Percentile(sortedArenaGeometryMicroseconds, 0.99),
            Percentile(sortedProbeOverheadMicroseconds, 0.50),
            Percentile(sortedProbeOverheadMicroseconds, 0.95),
            Percentile(sortedProbeOverheadMicroseconds, 0.99),
            managedBytesAllocatedMaximum,
            submissionsMaximum,
            lastMetrics.SubmissionsApplicable,
            batchesMaximum,
            lastMetrics.BatchesApplicable,
            textureBindsMaximum,
            lastMetrics.TextureBindsApplicable,
            bufferUploadBytesMaximum,
            lastMetrics.BufferUploadBytesApplicable,
            last.Gen0Collections - first.Gen0Collections,
            last.Gen1Collections - first.Gen1Collections,
            last.Gen2Collections - first.Gen2Collections,
            last.AllocatedBytes - first.AllocatedBytes,
            appearanceCacheHitsMaximum,
            Percentile(sortedAppearanceCacheHits, 0.50),
            Percentile(sortedAppearanceCacheHits, 0.95),
            Percentile(sortedAppearanceCacheHits, 0.99),
            appearanceCacheMissesMaximum,
            Percentile(sortedAppearanceCacheMisses, 0.50),
            Percentile(sortedAppearanceCacheMisses, 0.95),
            Percentile(sortedAppearanceCacheMisses, 0.99),
            appearanceCacheFillsMaximum,
            Percentile(sortedAppearanceCacheFills, 0.50),
            Percentile(sortedAppearanceCacheFills, 0.95),
            Percentile(sortedAppearanceCacheFills, 0.99));
    }
}
