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
/// <see cref="AddSubmitMicroseconds"/>, <see cref="SetManagedBytesAllocated"/>)
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
    bool BufferUploadBytesApplicable);

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
            BufferUploadBytesApplicable: false);

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
    }
}
