namespace Hukbo.Client.Rendering;

/// <summary>
/// Renderer-agnostic per-frame measurement seam (VIS-034, integration design
/// section 11, amendment A-1, 2026-07-28, user-approved). An interface
/// rather than a static counter class, so a future GPU-instanced backend can
/// supply its own implementation without editing the presentation layer that
/// records into it.
/// </summary>
/// <remarks>
/// Two tiers, mirrored on <see cref="RenderMetricsSnapshot"/>:
/// <list type="bullet">
/// <item>
/// <description>
/// Tier 1 (<see cref="AddQuad"/>, <see cref="AddQuads"/>,
/// <see cref="AddTriangles"/>, <see cref="AddGeometryBuildMicroseconds"/>,
/// <see cref="AddSubmitMicroseconds"/>, <see cref="SetManagedBytesAllocated"/>,
/// and the frame-span breakdown <see cref="AddClearMicroseconds"/>,
/// <see cref="AddLayoutMicroseconds"/>,
/// <see cref="AddHoverSelectionMicroseconds"/>,
/// <see cref="AddUiLayerMicroseconds"/>, <see cref="AddBaseDrawMicroseconds"/>,
/// <see cref="AddArenaGeometryMicroseconds"/>,
/// <see cref="AddProbeOverheadMicroseconds"/>,
/// <see cref="AddPawnGeometryInvocations"/>, and the appearance-cache
/// counters <see cref="AddAppearanceCacheHits"/>,
/// <see cref="AddAppearanceCacheMisses"/>,
/// <see cref="AddAppearanceCacheFills"/>)
/// is renderer-invariant — identical under an immediate-mode or an
/// instanced backend. It is the ONLY tier a budget constant is ever written
/// against (<see cref="RenderBudgetEstimate"/>).
/// </description>
/// </item>
/// <item>
/// <description>
/// Tier 2 (<see cref="AddSubmission"/>, <see cref="AddBatch"/>,
/// <see cref="AddTextureBind"/>, <see cref="AddBufferUploadBytes"/>) is
/// backend-specific and diagnostic only, never budgeted. A metric that does
/// not apply to the active backend reports zero and its own
/// <c>*Applicable</c> flag <see langword="false"/> on the snapshot, so an
/// absent field stays distinguishable from a genuine zero.
/// </description>
/// </item>
/// </list>
/// Every recording method is an allocation-free integer or floating-point
/// accumulation — no per-quad object, no list, no event — so a live renderer
/// can call these every frame without contributing to the allocation figure
/// it is itself measuring. <see cref="NullRenderMetricsRecorder"/> is the
/// disabled no-op a caller reaches for when measurement is off; its calls
/// allocate nothing and compute no payload, the same discipline
/// <c>Hukbo.Diagnostics.DiagnosticLog</c> requires of a disabled log call.
/// This seam lives in <c>Hukbo.Client</c> and is never referenced from
/// <c>Hukbo.Core</c>.
/// </remarks>
public interface IRenderMetricsRecorder
{
    /// <summary>
    /// <see langword="false"/> for <see cref="NullRenderMetricsRecorder"/>,
    /// <see langword="true"/> for every real implementation. A caller checks
    /// this before doing any work whose only purpose is to produce a
    /// recording payload, exactly as the debug-logging standard requires for
    /// a disabled log call.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>Tier 1. Records one logical quad.</summary>
    void AddQuad();

    /// <summary>Tier 1. Records <paramref name="count"/> logical quads at once.</summary>
    void AddQuads(int count);

    /// <summary>
    /// Tier 1. Records <paramref name="count"/> triangles, independent of
    /// <see cref="AddQuad"/>/<see cref="AddQuads"/> rather than derived from
    /// them, so a future backend emitting non-quad geometry can report
    /// honestly.
    /// </summary>
    void AddTriangles(int count);

    /// <summary>
    /// Tier 1. Accumulates CPU time spent inside the pure geometry helpers
    /// this frame.
    /// </summary>
    void AddGeometryBuildMicroseconds(double microseconds);

    /// <summary>
    /// Tier 1. Accumulates CPU time from first submission call to end of
    /// frame submission.
    /// </summary>
    void AddSubmitMicroseconds(double microseconds);

    /// <summary>
    /// Tier 1. Sets the managed allocation attributable to one steady-state
    /// frame (R-W4.10). A set rather than an accumulation: the caller
    /// measures this once per frame from a GC allocation delta, not by
    /// summing per-draw contributions.
    /// </summary>
    void SetManagedBytesAllocated(long bytes);

    /// <summary>
    /// Tier 1 (GPU-001, GPU-003). Accumulates CPU time spent inside the
    /// frame's <c>GraphicsDevice.Clear</c> call.
    /// </summary>
    void AddClearMicroseconds(double microseconds);

    /// <summary>
    /// Tier 1 (GPU-001, GPU-003). Accumulates CPU time spent resolving this
    /// frame's screen layout, before anything is drawn.
    /// </summary>
    void AddLayoutMicroseconds(double microseconds);

    /// <summary>
    /// Tier 1 (GPU-001, GPU-003). Accumulates CPU time spent resolving the
    /// pointer's hovered agent and the resulting selection state.
    /// </summary>
    void AddHoverSelectionMicroseconds(double microseconds);

    /// <summary>
    /// Tier 1 (GPU-001, GPU-003). Accumulates CPU time spent drawing the user
    /// interface layer, which is separate from the arena layer the budget is
    /// written against.
    /// </summary>
    void AddUiLayerMicroseconds(double microseconds);

    /// <summary>
    /// Tier 1 (GPU-001, GPU-003). Accumulates CPU time spent inside the base
    /// draw call, so the portion of the frame this seam does not otherwise
    /// name stays attributable rather than becoming residual.
    /// </summary>
    void AddBaseDrawMicroseconds(double microseconds);

    /// <summary>
    /// Tier 1 (GPU-001, GPU-004). Accumulates CPU time spent constructing the
    /// arena's real per-pawn geometry — the geometry the renderer actually
    /// draws from — so it stays separate from
    /// <see cref="AddSubmitMicroseconds"/>, which after GPU-004 narrows to
    /// submission work alone. The two spans are recorded independently rather
    /// than one being derived from the other.
    /// </summary>
    void AddArenaGeometryMicroseconds(double microseconds);

    /// <summary>
    /// Tier 1 (GPU-001, GPU-005). Accumulates CPU time the measurement probe
    /// spends on its own duplicate counting pass. Reported separately so the
    /// probe's overhead is never silently folded into a figure a budget is
    /// written against.
    /// </summary>
    void AddProbeOverheadMicroseconds(double microseconds);

    /// <summary>
    /// Tier 1 (GPU-001, GPU-005). Records <paramref name="count"/> calls into
    /// the pure pawn-geometry helper this frame, so the probe's duplication
    /// factor is derived from a recorded invocation count rather than assumed.
    /// </summary>
    void AddPawnGeometryInvocations(int count);

    /// <summary>
    /// Tier 1 (GPU-017). Records <paramref name="count"/> appearance-cache
    /// reads this frame that were answered from a slot whose stored key
    /// matched, without the appearance factory being called at all.
    /// </summary>
    void AddAppearanceCacheHits(int count);

    /// <summary>
    /// Tier 1 (GPU-017). Records <paramref name="count"/> appearance-cache
    /// reads this frame that had to call
    /// <c>PawnAppearanceFactory.Create</c> because no slot held the key. The
    /// first frame of a battle is all misses and every frame after it should
    /// be all hits, so a non-zero figure in a steady-state frame is the
    /// signal that the cache's key or its lifetime assumption is wrong.
    /// </summary>
    void AddAppearanceCacheMisses(int count);

    /// <summary>
    /// Tier 1 (GPU-017). Records <paramref name="count"/> cache slots that
    /// went from empty to occupied this frame. Counted apart from
    /// <see cref="AddAppearanceCacheMisses"/> because a miss that overwrites
    /// an already-occupied slot is an ordinal reused by a different agent,
    /// which is a different fault from a slot simply not being warm yet.
    /// </summary>
    void AddAppearanceCacheFills(int count);

    /// <summary>
    /// Tier 2, diagnostic only. One <c>SpriteBatch.Draw</c> call under the
    /// current backend; zero and not applicable under a future instanced
    /// backend.
    /// </summary>
    void AddSubmission();

    /// <summary>
    /// Tier 2, diagnostic only. One <c>Begin</c>/<c>End</c> pair under the
    /// current backend; one instance batch under a future instanced backend
    /// — this metric stays applicable either way, only its meaning shifts.
    /// </summary>
    void AddBatch();

    /// <summary>Tier 2, diagnostic only. One texture bind.</summary>
    void AddTextureBind();

    /// <summary>
    /// Tier 2, diagnostic only. Instance-buffer upload bytes; not applicable
    /// under the current <c>SpriteBatch</c> backend, which uploads none.
    /// </summary>
    void AddBufferUploadBytes(long bytes);

    /// <summary>Reads every counter recorded so far without resetting them.</summary>
    RenderMetricsSnapshot Snapshot();

    /// <summary>Zeroes every counter, ready for the next frame.</summary>
    void Reset();
}

/// <summary>
/// One frame's recorded metrics (VIS-034, amendment A-1). Tier 1 fields are
/// renderer-invariant and are what a budget constant is ever compared
/// against; Tier 2 fields are backend-specific and diagnostic only, each
/// paired with an <c>*Applicable</c> flag so a metric the active backend does
/// not produce reports as absent rather than as a false zero.
/// </summary>
/// <param name="Quads">Tier 1. Logical quads drawn this frame.</param>
/// <param name="Triangles">Tier 1. Triangles drawn this frame.</param>
/// <param name="GeometryBuildMicroseconds">
/// Tier 1. CPU time inside the pure geometry helpers this frame.
/// </param>
/// <param name="SubmitMicroseconds">
/// Tier 1. CPU time from first submission call to end of frame submission.
/// </param>
/// <param name="ManagedBytesAllocated">
/// Tier 1. Managed allocation attributable to this frame (R-W4.10).
/// </param>
/// <param name="Submissions">
/// Tier 2, diagnostic only. <c>SpriteBatch.Draw</c> calls under the current
/// backend.
/// </param>
/// <param name="SubmissionsApplicable">
/// Whether <see cref="Submissions"/> means anything under the active
/// backend.
/// </param>
/// <param name="Batches">
/// Tier 2, diagnostic only. <c>Begin</c>/<c>End</c> pairs under the current
/// backend (R-W4.5, "one batch, one texture" — retained as a Tier 2
/// assertion scoped to this backend).
/// </param>
/// <param name="BatchesApplicable">
/// Whether <see cref="Batches"/> means anything under the active backend.
/// </param>
/// <param name="TextureBinds">
/// Tier 2, diagnostic only. Texture binds — always 1 under the current
/// backend.
/// </param>
/// <param name="TextureBindsApplicable">
/// Whether <see cref="TextureBinds"/> means anything under the active
/// backend.
/// </param>
/// <param name="BufferUploadBytes">
/// Tier 2, diagnostic only. Instance-buffer upload bytes — always 0 and not
/// applicable under the current <c>SpriteBatch</c> backend.
/// </param>
/// <param name="BufferUploadBytesApplicable">
/// Whether <see cref="BufferUploadBytes"/> means anything under the active
/// backend.
/// </param>
/// <param name="ClearMicroseconds">
/// Tier 1. CPU time inside this frame's <c>GraphicsDevice.Clear</c> call.
/// </param>
/// <param name="LayoutMicroseconds">
/// Tier 1. CPU time spent resolving this frame's screen layout.
/// </param>
/// <param name="HoverSelectionMicroseconds">
/// Tier 1. CPU time spent resolving the hovered agent and selection state.
/// </param>
/// <param name="UiLayerMicroseconds">
/// Tier 1. CPU time spent drawing the user interface layer.
/// </param>
/// <param name="BaseDrawMicroseconds">
/// Tier 1. CPU time inside the base draw call.
/// </param>
/// <param name="ArenaGeometryMicroseconds">
/// Tier 1. CPU time spent constructing the arena's real per-pawn geometry,
/// held separate from <see cref="SubmitMicroseconds"/> (GPU-004).
/// </param>
/// <param name="ProbeOverheadMicroseconds">
/// Tier 1. CPU time the measurement probe spends on its own duplicate
/// counting pass, reported separately from renderer cost (GPU-005).
/// </param>
/// <param name="PawnGeometryInvocations">
/// Tier 1. Calls into the pure pawn-geometry helper this frame, from which
/// the probe's duplication factor is derived rather than assumed (GPU-005).
/// </param>
/// <param name="AppearanceCacheHits">
/// Tier 1. Appearance-cache reads this frame answered from a slot whose
/// stored key matched, without calling the appearance factory (GPU-017).
/// </param>
/// <param name="AppearanceCacheMisses">
/// Tier 1. Appearance-cache reads this frame that had to call
/// <c>PawnAppearanceFactory.Create</c> because no slot held the key
/// (GPU-017).
/// </param>
/// <param name="AppearanceCacheFills">
/// Tier 1. Cache slots that went from empty to occupied this frame
/// (GPU-017).
/// </param>
/// <remarks>
/// The eight fields added by GPU-001, and the three appearance-cache fields
/// added by GPU-017, are declared last but carry no default, so every
/// construction site names them explicitly (GPU-002). That matters because,
/// unlike Tier 2, none of them carries an <c>*Applicable</c> flag — a CPU
/// span and a cache counter both apply under every backend — so a zero
/// arriving from an omitted argument would be indistinguishable from a
/// genuine measured zero. Making them required means a zero is always
/// something a caller chose to record.
/// </remarks>
public readonly record struct RenderMetricsSnapshot(
    int Quads,
    int Triangles,
    double GeometryBuildMicroseconds,
    double SubmitMicroseconds,
    long ManagedBytesAllocated,
    int Submissions,
    bool SubmissionsApplicable,
    int Batches,
    bool BatchesApplicable,
    int TextureBinds,
    bool TextureBindsApplicable,
    long BufferUploadBytes,
    bool BufferUploadBytesApplicable,
    double ClearMicroseconds,
    double LayoutMicroseconds,
    double HoverSelectionMicroseconds,
    double UiLayerMicroseconds,
    double BaseDrawMicroseconds,
    double ArenaGeometryMicroseconds,
    double ProbeOverheadMicroseconds,
    int PawnGeometryInvocations,
    int AppearanceCacheHits,
    int AppearanceCacheMisses,
    int AppearanceCacheFills);

/// <summary>
/// The disabled, allocation-free no-op <see cref="IRenderMetricsRecorder"/>.
/// Every call is a JIT-elidable no-op, exactly as
/// <c>Hukbo.Diagnostics.DiagnosticLog</c> requires of a disabled log call.
/// One shared instance rather than a new one per caller — there is no state
/// to keep separate.
/// </summary>
public sealed class NullRenderMetricsRecorder : IRenderMetricsRecorder
{
    /// <summary>The single shared disabled recorder.</summary>
    public static readonly NullRenderMetricsRecorder Instance = new();

    private NullRenderMetricsRecorder()
    {
    }

    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public void AddQuad()
    {
    }

    /// <inheritdoc />
    public void AddQuads(int count)
    {
    }

    /// <inheritdoc />
    public void AddTriangles(int count)
    {
    }

    /// <inheritdoc />
    public void AddGeometryBuildMicroseconds(double microseconds)
    {
    }

    /// <inheritdoc />
    public void AddSubmitMicroseconds(double microseconds)
    {
    }

    /// <inheritdoc />
    public void SetManagedBytesAllocated(long bytes)
    {
    }

    /// <inheritdoc />
    public void AddClearMicroseconds(double microseconds)
    {
    }

    /// <inheritdoc />
    public void AddLayoutMicroseconds(double microseconds)
    {
    }

    /// <inheritdoc />
    public void AddHoverSelectionMicroseconds(double microseconds)
    {
    }

    /// <inheritdoc />
    public void AddUiLayerMicroseconds(double microseconds)
    {
    }

    /// <inheritdoc />
    public void AddBaseDrawMicroseconds(double microseconds)
    {
    }

    /// <inheritdoc />
    public void AddArenaGeometryMicroseconds(double microseconds)
    {
    }

    /// <inheritdoc />
    public void AddProbeOverheadMicroseconds(double microseconds)
    {
    }

    /// <inheritdoc />
    public void AddPawnGeometryInvocations(int count)
    {
    }

    /// <inheritdoc />
    public void AddAppearanceCacheHits(int count)
    {
    }

    /// <inheritdoc />
    public void AddAppearanceCacheMisses(int count)
    {
    }

    /// <inheritdoc />
    public void AddAppearanceCacheFills(int count)
    {
    }

    /// <inheritdoc />
    public void AddSubmission()
    {
    }

    /// <inheritdoc />
    public void AddBatch()
    {
    }

    /// <inheritdoc />
    public void AddTextureBind()
    {
    }

    /// <inheritdoc />
    public void AddBufferUploadBytes(long bytes)
    {
    }

    /// <inheritdoc />
    public RenderMetricsSnapshot Snapshot() => default;

    /// <inheritdoc />
    public void Reset()
    {
    }
}

/// <summary>
/// The current backend's concrete <see cref="IRenderMetricsRecorder"/>:
/// today's <c>SpriteBatch</c>-based renderer, one shared 1x1 pixel texture,
/// one arena <c>Begin</c>/<c>End</c> pair (integration design section 8).
/// Every counter is a plain mutable field incremented in place — no per-quad
/// object, no list, no event — so recording itself never shows up in the
/// <see cref="RenderMetricsSnapshot.ManagedBytesAllocated"/> measurement. Not thread-safe, matching
/// every other per-frame render-side type in this namespace: it is read and
/// written from the single render thread only.
/// </summary>
public sealed class SpriteBatchRenderMetricsRecorder : IRenderMetricsRecorder
{
    private int _quads;
    private int _triangles;
    private double _geometryBuildMicroseconds;
    private double _submitMicroseconds;
    private long _managedBytesAllocated;
    private int _submissions;
    private int _batches;
    private int _textureBinds;
    private double _clearMicroseconds;
    private double _layoutMicroseconds;
    private double _hoverSelectionMicroseconds;
    private double _uiLayerMicroseconds;
    private double _baseDrawMicroseconds;
    private double _arenaGeometryMicroseconds;
    private double _probeOverheadMicroseconds;
    private int _pawnGeometryInvocations;
    private int _appearanceCacheHits;
    private int _appearanceCacheMisses;
    private int _appearanceCacheFills;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public void AddQuad() => _quads++;

    /// <inheritdoc />
    public void AddQuads(int count) => _quads += count;

    /// <inheritdoc />
    public void AddTriangles(int count) => _triangles += count;

    /// <inheritdoc />
    public void AddGeometryBuildMicroseconds(double microseconds) =>
        _geometryBuildMicroseconds += microseconds;

    /// <inheritdoc />
    public void AddSubmitMicroseconds(double microseconds) =>
        _submitMicroseconds += microseconds;

    /// <inheritdoc />
    public void SetManagedBytesAllocated(long bytes) => _managedBytesAllocated = bytes;

    /// <inheritdoc />
    public void AddClearMicroseconds(double microseconds) =>
        _clearMicroseconds += microseconds;

    /// <inheritdoc />
    public void AddLayoutMicroseconds(double microseconds) =>
        _layoutMicroseconds += microseconds;

    /// <inheritdoc />
    public void AddHoverSelectionMicroseconds(double microseconds) =>
        _hoverSelectionMicroseconds += microseconds;

    /// <inheritdoc />
    public void AddUiLayerMicroseconds(double microseconds) =>
        _uiLayerMicroseconds += microseconds;

    /// <inheritdoc />
    public void AddBaseDrawMicroseconds(double microseconds) =>
        _baseDrawMicroseconds += microseconds;

    /// <inheritdoc />
    public void AddArenaGeometryMicroseconds(double microseconds) =>
        _arenaGeometryMicroseconds += microseconds;

    /// <inheritdoc />
    public void AddProbeOverheadMicroseconds(double microseconds) =>
        _probeOverheadMicroseconds += microseconds;

    /// <inheritdoc />
    public void AddPawnGeometryInvocations(int count) => _pawnGeometryInvocations += count;

    /// <inheritdoc />
    public void AddAppearanceCacheHits(int count) => _appearanceCacheHits += count;

    /// <inheritdoc />
    public void AddAppearanceCacheMisses(int count) => _appearanceCacheMisses += count;

    /// <inheritdoc />
    public void AddAppearanceCacheFills(int count) => _appearanceCacheFills += count;

    /// <inheritdoc />
    public void AddSubmission() => _submissions++;

    /// <inheritdoc />
    public void AddBatch() => _batches++;

    /// <inheritdoc />
    public void AddTextureBind() => _textureBinds++;

    /// <summary>
    /// A no-op: the current <c>SpriteBatch</c> backend never uploads an
    /// instance buffer, so <see cref="RenderMetricsSnapshot.BufferUploadBytes"/>
    /// always reports 0 and not applicable regardless of what a caller passes
    /// here (amendment A-1's "0 and not-applicable now; instance buffer bytes
    /// later").
    /// </summary>
    public void AddBufferUploadBytes(long bytes)
    {
    }

    /// <inheritdoc />
    public RenderMetricsSnapshot Snapshot() =>
        new(
            _quads,
            _triangles,
            _geometryBuildMicroseconds,
            _submitMicroseconds,
            _managedBytesAllocated,
            _submissions,
            SubmissionsApplicable: true,
            _batches,
            BatchesApplicable: true,
            _textureBinds,
            TextureBindsApplicable: true,
            BufferUploadBytes: 0,
            BufferUploadBytesApplicable: false,
            _clearMicroseconds,
            _layoutMicroseconds,
            _hoverSelectionMicroseconds,
            _uiLayerMicroseconds,
            _baseDrawMicroseconds,
            _arenaGeometryMicroseconds,
            _probeOverheadMicroseconds,
            _pawnGeometryInvocations,
            _appearanceCacheHits,
            _appearanceCacheMisses,
            _appearanceCacheFills);

    /// <inheritdoc />
    public void Reset()
    {
        _quads = 0;
        _triangles = 0;
        _geometryBuildMicroseconds = 0;
        _submitMicroseconds = 0;
        _managedBytesAllocated = 0;
        _submissions = 0;
        _batches = 0;
        _textureBinds = 0;
        _clearMicroseconds = 0;
        _layoutMicroseconds = 0;
        _hoverSelectionMicroseconds = 0;
        _uiLayerMicroseconds = 0;
        _baseDrawMicroseconds = 0;
        _arenaGeometryMicroseconds = 0;
        _probeOverheadMicroseconds = 0;
        _pawnGeometryInvocations = 0;
        _appearanceCacheHits = 0;
        _appearanceCacheMisses = 0;
        _appearanceCacheFills = 0;
    }
}
