namespace Hukbo.Client.Rendering;

/// <summary>
/// One frame's render-instrumentation sample. Populated only when
/// <see cref="ArenaGame"/>'s render-probe opt-in is active (environment
/// variable <c>HUKBO_RENDER_PROBE=1</c>, checked once at construction);
/// never raised on the default Release render path, so a normal run pays
/// nothing for it (VIS-035, retrofitted onto the two-tier seam by VIS-035R,
/// integration design section 11, amendment A-1, 2026-07-28, user-approved).
/// </summary>
/// <param name="FrameMilliseconds">
/// Wall-clock time spent inside this frame's <c>Draw</c> call, measured with
/// <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/> /
/// <see cref="System.Diagnostics.Stopwatch.GetElapsedTime(long)"/>, the same
/// idiom <c>HeadlessRunner</c> uses for tick timing. Covers the whole frame
/// — arena batch and UI layer both — unlike <see cref="Metrics"/>, which is
/// scoped to the arena batch only, matching the original <c>ArenaSubmissionCount</c>
/// field's own scope.
/// </param>
/// <param name="Metrics">
/// The arena batch's Tier 1 (renderer-invariant, budgeted) and Tier 2
/// (backend-specific, diagnostic-only) counters for this frame, recorded
/// through <see cref="IRenderMetricsRecorder"/> (VIS-034, amendment A-1).
/// <c>ArenaGame</c> records Tier 1 quads/triangles by evaluating the same
/// pure <c>PawnQuadCount</c>/<c>BackdropQuadCount</c> functions the design's
/// quad budgets (<c>RenderBudgetEstimate</c>) are pinned against, over the
/// exact appearance/layout/cull inputs the live renderers use this frame,
/// and times that work separately as
/// <see cref="RenderMetricsSnapshot.GeometryBuildMicroseconds"/>; it times
/// the arena <c>SpriteBatch</c> submission itself as
/// <see cref="RenderMetricsSnapshot.SubmitMicroseconds"/>. Tier 2
/// submissions/batches/texture binds are the current
/// <c>spritebatch-1x1</c> backend's own derived facts — one
/// <c>SpriteBatch.Draw</c> call and two triangles per quad, one arena
/// <c>Begin</c>/<c>End</c> pair, one shared pixel texture — none of them
/// budgeted (Tier 2 is diagnostic only).
/// </param>
/// <param name="Gen0Collections">
/// <see cref="System.GC.CollectionCount(int)"/> for generation 0, sampled at
/// the end of this frame (cumulative process count, not a per-frame delta —
/// callers subtract across a sampling window themselves, as
/// <see cref="RenderProbeStatistics.Summarize"/> does).
/// </param>
/// <param name="Gen1Collections">Generation 1, same sample point.</param>
/// <param name="Gen2Collections">Generation 2, same sample point.</param>
/// <param name="AllocatedBytes">
/// <see cref="System.GC.GetAllocatedBytesForCurrentThread"/>, sampled at the
/// end of this frame (cumulative for the thread, not a per-frame delta).
/// Independent of <see cref="RenderMetricsSnapshot.ManagedBytesAllocated"/>
/// inside <see cref="Metrics"/>, which <c>ArenaGame</c> sets explicitly from
/// this same counter's frame-to-frame delta (R-W4.10) rather than leaving a
/// caller to derive it from a window of samples afterward.
/// </param>
public readonly record struct RenderProbeSample(
    double FrameMilliseconds,
    RenderMetricsSnapshot Metrics,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    long AllocatedBytes);
