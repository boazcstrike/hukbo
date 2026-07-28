using Hukbo.Client.Rendering;

namespace Hukbo.Client.Tests;

/// <summary>
/// <see cref="NullRenderMetricsRecorder"/> and
/// <see cref="SpriteBatchRenderMetricsRecorder"/> against the
/// <see cref="IRenderMetricsRecorder"/> contract (VIS-034, amendment A-1):
/// the disabled recorder is a true no-op, the current backend's recorder
/// accumulates Tier 1 counters and Tier 2 counters independently, and every
/// Tier 2 field on the snapshot carries the applicability the amendment
/// specifies for today's <c>SpriteBatch</c> backend.
/// </summary>
public sealed class RenderMetricsTests
{
    [Fact]
    public void NullRecorder_IsDisabled()
    {
        Assert.False(NullRenderMetricsRecorder.Instance.IsEnabled);
    }

    [Fact]
    public void NullRecorder_EverySnapshotFieldStaysZeroRegardlessOfRecordedCalls()
    {
        var recorder = NullRenderMetricsRecorder.Instance;

        recorder.AddQuad();
        recorder.AddQuads(41);
        recorder.AddTriangles(7);
        recorder.AddGeometryBuildMicroseconds(3.5);
        recorder.AddSubmitMicroseconds(2.5);
        recorder.SetManagedBytesAllocated(1_024);
        recorder.AddSubmission();
        recorder.AddBatch();
        recorder.AddTextureBind();
        recorder.AddBufferUploadBytes(512);
        recorder.AddClearMicroseconds(1.5);
        recorder.AddLayoutMicroseconds(2.5);
        recorder.AddHoverSelectionMicroseconds(3.5);
        recorder.AddUiLayerMicroseconds(4.5);
        recorder.AddBaseDrawMicroseconds(5.5);
        recorder.AddArenaGeometryMicroseconds(6.5);
        recorder.AddProbeOverheadMicroseconds(7.5);
        recorder.AddPawnGeometryInvocations(9);

        Assert.Equal(default, recorder.Snapshot());
    }

    /// <summary>
    /// GPU-001. The disabled recorder must stay a no-op on every span added
    /// for the Phase 1 frame breakdown, individually rather than only in
    /// aggregate: a snapshot that is <c>default</c> after all of them are
    /// called together would still pass if one span were wired to a field the
    /// others zeroed. Each call is checked on its own recorder-and-snapshot
    /// pair so a single mis-wired member cannot hide behind the rest.
    /// </summary>
    [Fact]
    public void NullRecorder_EveryNewFrameSpanIsIndividuallyANoOp()
    {
        var recorder = NullRenderMetricsRecorder.Instance;

        recorder.AddClearMicroseconds(11.0);
        Assert.Equal(default, recorder.Snapshot());

        recorder.AddLayoutMicroseconds(12.0);
        Assert.Equal(default, recorder.Snapshot());

        recorder.AddHoverSelectionMicroseconds(13.0);
        Assert.Equal(default, recorder.Snapshot());

        recorder.AddUiLayerMicroseconds(14.0);
        Assert.Equal(default, recorder.Snapshot());

        recorder.AddBaseDrawMicroseconds(15.0);
        Assert.Equal(default, recorder.Snapshot());

        recorder.AddArenaGeometryMicroseconds(16.0);
        Assert.Equal(default, recorder.Snapshot());

        recorder.AddProbeOverheadMicroseconds(17.0);
        Assert.Equal(default, recorder.Snapshot());

        recorder.AddPawnGeometryInvocations(18);
        Assert.Equal(default, recorder.Snapshot());
    }

    [Fact]
    public void NullRecorder_IsTheSameSharedInstanceEveryTime()
    {
        Assert.Same(NullRenderMetricsRecorder.Instance, NullRenderMetricsRecorder.Instance);
    }

    [Fact]
    public void SpriteBatchRecorder_IsEnabled()
    {
        Assert.True(new SpriteBatchRenderMetricsRecorder().IsEnabled);
    }

    [Fact]
    public void SpriteBatchRecorder_AccumulatesTier1CountersAcrossMultipleCalls()
    {
        var recorder = new SpriteBatchRenderMetricsRecorder();

        recorder.AddQuad();
        recorder.AddQuads(4);
        recorder.AddTriangles(3);
        recorder.AddTriangles(2);
        recorder.AddGeometryBuildMicroseconds(10.5);
        recorder.AddGeometryBuildMicroseconds(1.5);
        recorder.AddSubmitMicroseconds(6.0);
        recorder.SetManagedBytesAllocated(2_048);

        var snapshot = recorder.Snapshot();

        Assert.Equal(5, snapshot.Quads);
        Assert.Equal(5, snapshot.Triangles);
        Assert.Equal(12.0, snapshot.GeometryBuildMicroseconds);
        Assert.Equal(6.0, snapshot.SubmitMicroseconds);
        Assert.Equal(2_048, snapshot.ManagedBytesAllocated);
    }

    /// <summary>
    /// GPU-001. Every span added for the Phase 1 frame breakdown accumulates
    /// across calls and lands on its own snapshot field. Each span is given a
    /// distinct value and each is called twice, so a member wired to the wrong
    /// field, or one that assigns instead of accumulating, fails here rather
    /// than surviving into a recorded baseline.
    /// </summary>
    [Fact]
    public void SpriteBatchRecorder_AccumulatesEveryNewFrameSpanOntoItsOwnField()
    {
        var recorder = new SpriteBatchRenderMetricsRecorder();

        recorder.AddClearMicroseconds(1.0);
        recorder.AddClearMicroseconds(0.5);
        recorder.AddLayoutMicroseconds(2.0);
        recorder.AddLayoutMicroseconds(0.5);
        recorder.AddHoverSelectionMicroseconds(3.0);
        recorder.AddHoverSelectionMicroseconds(0.5);
        recorder.AddUiLayerMicroseconds(4.0);
        recorder.AddUiLayerMicroseconds(0.5);
        recorder.AddBaseDrawMicroseconds(5.0);
        recorder.AddBaseDrawMicroseconds(0.5);
        recorder.AddArenaGeometryMicroseconds(6.0);
        recorder.AddArenaGeometryMicroseconds(0.5);
        recorder.AddProbeOverheadMicroseconds(7.0);
        recorder.AddProbeOverheadMicroseconds(0.5);
        recorder.AddPawnGeometryInvocations(8);
        recorder.AddPawnGeometryInvocations(4);

        var snapshot = recorder.Snapshot();

        Assert.Equal(1.5, snapshot.ClearMicroseconds);
        Assert.Equal(2.5, snapshot.LayoutMicroseconds);
        Assert.Equal(3.5, snapshot.HoverSelectionMicroseconds);
        Assert.Equal(4.5, snapshot.UiLayerMicroseconds);
        Assert.Equal(5.5, snapshot.BaseDrawMicroseconds);
        Assert.Equal(6.5, snapshot.ArenaGeometryMicroseconds);
        Assert.Equal(7.5, snapshot.ProbeOverheadMicroseconds);
        Assert.Equal(12, snapshot.PawnGeometryInvocations);
    }

    /// <summary>
    /// GPU-004 splits today's single Submit span into arena geometry
    /// construction and submission proper. The two are independent
    /// accumulations on this seam — recording one must leave the other
    /// untouched — because the go/no-go trigger in the plan's section 4 turns
    /// on being able to tell them apart.
    /// </summary>
    [Fact]
    public void SpriteBatchRecorder_ArenaGeometryAndSubmitSpansAccumulateIndependently()
    {
        var recorder = new SpriteBatchRenderMetricsRecorder();

        recorder.AddArenaGeometryMicroseconds(30.0);
        recorder.AddSubmitMicroseconds(70.0);

        var snapshot = recorder.Snapshot();

        Assert.Equal(30.0, snapshot.ArenaGeometryMicroseconds);
        Assert.Equal(70.0, snapshot.SubmitMicroseconds);
        Assert.Equal(0, snapshot.GeometryBuildMicroseconds);
    }

    /// <summary>
    /// GPU-005 moves the probe's own duplicate counting pass out of
    /// <c>geometryBuildMicroseconds</c>. The probe-overhead span is therefore
    /// its own accumulation and must never leak into the renderer's geometry
    /// figure, which a budget is written against.
    /// </summary>
    [Fact]
    public void SpriteBatchRecorder_ProbeOverheadDoesNotContributeToGeometryBuild()
    {
        var recorder = new SpriteBatchRenderMetricsRecorder();

        recorder.AddProbeOverheadMicroseconds(25.0);
        recorder.AddGeometryBuildMicroseconds(5.0);

        var snapshot = recorder.Snapshot();

        Assert.Equal(25.0, snapshot.ProbeOverheadMicroseconds);
        Assert.Equal(5.0, snapshot.GeometryBuildMicroseconds);
    }

    [Fact]
    public void SpriteBatchRecorder_SetManagedBytesAllocatedReplacesRatherThanAccumulates()
    {
        var recorder = new SpriteBatchRenderMetricsRecorder();

        recorder.SetManagedBytesAllocated(1_000);
        recorder.SetManagedBytesAllocated(200);

        Assert.Equal(200, recorder.Snapshot().ManagedBytesAllocated);
    }

    [Fact]
    public void SpriteBatchRecorder_AccumulatesTier2CountersAndReportsThemApplicable()
    {
        var recorder = new SpriteBatchRenderMetricsRecorder();

        recorder.AddSubmission();
        recorder.AddSubmission();
        recorder.AddBatch();
        recorder.AddTextureBind();

        var snapshot = recorder.Snapshot();

        Assert.Equal(2, snapshot.Submissions);
        Assert.True(snapshot.SubmissionsApplicable);
        Assert.Equal(1, snapshot.Batches);
        Assert.True(snapshot.BatchesApplicable);
        Assert.Equal(1, snapshot.TextureBinds);
        Assert.True(snapshot.TextureBindsApplicable);
    }

    /// <summary>
    /// Amendment A-1: "BufferUploadBytes (0 and not-applicable now; instance
    /// buffer bytes later)" — today's <c>SpriteBatch</c> backend never
    /// uploads an instance buffer, so the snapshot reports zero and
    /// not-applicable regardless of what a caller passes in.
    /// </summary>
    [Fact]
    public void SpriteBatchRecorder_BufferUploadBytesIsAlwaysZeroAndNotApplicable()
    {
        var recorder = new SpriteBatchRenderMetricsRecorder();

        recorder.AddBufferUploadBytes(4_096);

        var snapshot = recorder.Snapshot();

        Assert.Equal(0, snapshot.BufferUploadBytes);
        Assert.False(snapshot.BufferUploadBytesApplicable);
    }

    [Fact]
    public void SpriteBatchRecorder_ResetZeroesEveryCounter()
    {
        var recorder = new SpriteBatchRenderMetricsRecorder();
        recorder.AddQuads(10);
        recorder.AddTriangles(6);
        recorder.AddGeometryBuildMicroseconds(9.0);
        recorder.AddSubmitMicroseconds(4.0);
        recorder.SetManagedBytesAllocated(512);
        recorder.AddSubmission();
        recorder.AddBatch();
        recorder.AddTextureBind();
        recorder.AddClearMicroseconds(1.0);
        recorder.AddLayoutMicroseconds(2.0);
        recorder.AddHoverSelectionMicroseconds(3.0);
        recorder.AddUiLayerMicroseconds(4.0);
        recorder.AddBaseDrawMicroseconds(5.0);
        recorder.AddArenaGeometryMicroseconds(6.0);
        recorder.AddProbeOverheadMicroseconds(7.0);
        recorder.AddPawnGeometryInvocations(8);

        recorder.Reset();

        // Not `default` — SubmissionsApplicable/BatchesApplicable/
        // TextureBindsApplicable are a backend fact for this SpriteBatch
        // recorder, always true regardless of the zeroed counters (matching
        // the always-true assertions this same file already pins on a fresh
        // snapshot above); only BufferUploadBytesApplicable stays false, per
        // amendment A-1.
        var expected = new RenderMetricsSnapshot(
            Quads: 0,
            Triangles: 0,
            GeometryBuildMicroseconds: 0,
            SubmitMicroseconds: 0,
            ManagedBytesAllocated: 0,
            Submissions: 0,
            SubmissionsApplicable: true,
            Batches: 0,
            BatchesApplicable: true,
            TextureBinds: 0,
            TextureBindsApplicable: true,
            BufferUploadBytes: 0,
            BufferUploadBytesApplicable: false,
            ClearMicroseconds: 0,
            LayoutMicroseconds: 0,
            HoverSelectionMicroseconds: 0,
            UiLayerMicroseconds: 0,
            BaseDrawMicroseconds: 0,
            ArenaGeometryMicroseconds: 0,
            ProbeOverheadMicroseconds: 0,
            PawnGeometryInvocations: 0);
        Assert.Equal(expected, recorder.Snapshot());
    }

    [Fact]
    public void SpriteBatchRecorder_SnapshotDoesNotResetTheRecorder()
    {
        var recorder = new SpriteBatchRenderMetricsRecorder();
        recorder.AddQuad();

        _ = recorder.Snapshot();

        Assert.Equal(1, recorder.Snapshot().Quads);
    }
}
