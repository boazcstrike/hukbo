namespace Hukbo.Client.Rendering;

/// <summary>
/// One frame's render-instrumentation sample. Populated only when
/// <see cref="ArenaGame"/>'s render-probe opt-in is active (environment
/// variable <c>HUKBO_RENDER_PROBE=1</c>, checked once at construction);
/// never raised on the default Release render path, so a normal run pays
/// nothing for it (VIS-035, integration design section 11).
/// </summary>
/// <param name="FrameMilliseconds">
/// Wall-clock time spent inside this frame's <c>Draw</c> call, measured with
/// <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/> /
/// <see cref="System.Diagnostics.Stopwatch.GetElapsedTime(long)"/>, the same
/// idiom <c>HeadlessRunner</c> uses for tick timing.
/// </param>
/// <param name="ArenaSubmissionCount">
/// Sprite submissions issued inside the single arena
/// <c>Begin</c>/<c>End</c> pair this frame. Always <c>0</c> today: VIS-034's
/// counting seam (<c>Rendering/SubmissionCount.cs</c>) is a pure,
/// GPU-independent function over already-built layout values and is not
/// wired into the live render loop. Making this field meaningful means
/// calling that counting function, once it exists, from inside
/// <c>ArenaGame.DrawArena</c> with the same layout values the renderer
/// already computes, and passing the total through this field — the exact
/// hookup point this task leaves for whichever task lands after VIS-034.
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
/// </param>
public readonly record struct RenderProbeSample(
    double FrameMilliseconds,
    int ArenaSubmissionCount,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    long AllocatedBytes);
