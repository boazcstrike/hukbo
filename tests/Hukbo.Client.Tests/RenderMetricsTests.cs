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
            BufferUploadBytesApplicable: false);
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
