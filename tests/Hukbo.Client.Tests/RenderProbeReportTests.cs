using System.Text.Json;
using Hukbo.Client.Rendering;

namespace Hukbo.Client.Tests;

public sealed class RenderProbeReportTests
{
    private static RenderMetricsSnapshot CreateMetrics(
        int quads,
        int triangles,
        double geometryBuildMicroseconds,
        double submitMicroseconds,
        long managedBytesAllocated,
        int submissions = 0,
        int batches = 0,
        int textureBinds = 0,
        double clearMicroseconds = 0,
        double layoutMicroseconds = 0,
        double hoverSelectionMicroseconds = 0,
        double uiLayerMicroseconds = 0,
        double baseDrawMicroseconds = 0,
        double arenaGeometryMicroseconds = 0,
        double probeOverheadMicroseconds = 0,
        int pawnGeometryInvocations = 0) =>
        new(
            quads,
            triangles,
            geometryBuildMicroseconds,
            submitMicroseconds,
            managedBytesAllocated,
            submissions,
            SubmissionsApplicable: true,
            batches,
            BatchesApplicable: true,
            textureBinds,
            TextureBindsApplicable: true,
            BufferUploadBytes: 0,
            BufferUploadBytesApplicable: false,
            clearMicroseconds,
            layoutMicroseconds,
            hoverSelectionMicroseconds,
            uiLayerMicroseconds,
            baseDrawMicroseconds,
            arenaGeometryMicroseconds,
            probeOverheadMicroseconds,
            pawnGeometryInvocations);

    [Fact]
    public void Percentile_EmptySequenceReturnsZero()
    {
        Assert.Equal(0, RenderProbeStatistics.Percentile([], 0.50));
    }

    [Fact]
    public void Percentile_NearestRankMatchesKnownValues()
    {
        double[] sorted = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        Assert.Equal(5, RenderProbeStatistics.Percentile(sorted, 0.50));
        Assert.Equal(10, RenderProbeStatistics.Percentile(sorted, 0.99));
        Assert.Equal(1, RenderProbeStatistics.Percentile(sorted, 0.01));
    }

    [Fact]
    public void Summarize_EmptySampleListReturnsZeroedResult()
    {
        var result = RenderProbeStatistics.Summarize(
            "minimum-zoom",
            Array.Empty<RenderProbeSample>());

        Assert.Equal("minimum-zoom", result.StationName);
        Assert.Equal(0, result.FrameCount);
        Assert.Equal(0, result.FrameMillisecondsP50);
        Assert.Equal(0, result.QuadsMaximum);
        Assert.Equal(0, result.TrianglesMaximum);
        Assert.Equal(0, result.PawnGeometryInvocationsMaximum);
        Assert.Equal(0, result.GeometryBuildMicrosecondsP50);
        Assert.Equal(0, result.SubmitMicrosecondsP50);
        Assert.Equal(0, result.ClearMicrosecondsP50);
        Assert.Equal(0, result.ClearMicrosecondsP95);
        Assert.Equal(0, result.ClearMicrosecondsP99);
        Assert.Equal(0, result.LayoutMicrosecondsP50);
        Assert.Equal(0, result.LayoutMicrosecondsP95);
        Assert.Equal(0, result.LayoutMicrosecondsP99);
        Assert.Equal(0, result.HoverSelectionMicrosecondsP50);
        Assert.Equal(0, result.HoverSelectionMicrosecondsP95);
        Assert.Equal(0, result.HoverSelectionMicrosecondsP99);
        Assert.Equal(0, result.UiLayerMicrosecondsP50);
        Assert.Equal(0, result.UiLayerMicrosecondsP95);
        Assert.Equal(0, result.UiLayerMicrosecondsP99);
        Assert.Equal(0, result.BaseDrawMicrosecondsP50);
        Assert.Equal(0, result.BaseDrawMicrosecondsP95);
        Assert.Equal(0, result.BaseDrawMicrosecondsP99);
        Assert.Equal(0, result.ArenaGeometryMicrosecondsP50);
        Assert.Equal(0, result.ArenaGeometryMicrosecondsP95);
        Assert.Equal(0, result.ArenaGeometryMicrosecondsP99);
        Assert.Equal(0, result.ProbeOverheadMicrosecondsP50);
        Assert.Equal(0, result.ProbeOverheadMicrosecondsP95);
        Assert.Equal(0, result.ProbeOverheadMicrosecondsP99);
        Assert.Equal(0L, result.ManagedBytesAllocatedMaximum);
        Assert.False(result.SubmissionsApplicable);
        Assert.False(result.BatchesApplicable);
        Assert.False(result.TextureBindsApplicable);
        Assert.False(result.BufferUploadBytesApplicable);
        Assert.Equal(0, result.Gen0CollectionsDelta);
        Assert.Equal(0L, result.AllocatedBytesDelta);
    }

    [Fact]
    public void Summarize_ReportsFrameTimePercentilesAndPeakQuadCount()
    {
        RenderProbeSample[] samples =
        [
            new RenderProbeSample(
                4.0,
                CreateMetrics(10, 20, 100, 200, 1_000, submissions: 10, batches: 1, textureBinds: 1),
                1, 0, 0, 1_000),
            new RenderProbeSample(
                8.0,
                CreateMetrics(30, 60, 300, 600, 1_500, submissions: 30, batches: 1, textureBinds: 1),
                1, 0, 0, 1_500),
            new RenderProbeSample(
                2.0,
                CreateMetrics(20, 40, 200, 400, 2_000, submissions: 20, batches: 1, textureBinds: 1),
                2, 1, 0, 2_000),
        ];

        var result = RenderProbeStatistics.Summarize("maximum-zoom", samples);

        Assert.Equal("maximum-zoom", result.StationName);
        Assert.Equal(3, result.FrameCount);
        Assert.Equal(4.0, result.FrameMillisecondsP50);
        Assert.Equal(8.0, result.FrameMillisecondsP95);
        Assert.Equal(8.0, result.FrameMillisecondsP99);
        Assert.Equal(30, result.QuadsMaximum);
        Assert.Equal(60, result.TrianglesMaximum);
        Assert.Equal(200, result.GeometryBuildMicrosecondsP50);
        Assert.Equal(400, result.SubmitMicrosecondsP50);
        Assert.Equal(2_000L, result.ManagedBytesAllocatedMaximum);
        Assert.Equal(30, result.SubmissionsMaximum);
        Assert.True(result.SubmissionsApplicable);
        Assert.Equal(1, result.BatchesMaximum);
        Assert.True(result.BatchesApplicable);
        Assert.Equal(1, result.TextureBindsMaximum);
        Assert.True(result.TextureBindsApplicable);
        Assert.Equal(0L, result.BufferUploadBytesMaximum);
        Assert.False(result.BufferUploadBytesApplicable);
    }

    /// <summary>
    /// Pins the GPU-002 span percentiles against arithmetic worked out by hand
    /// rather than against whatever the implementation happens to return.
    ///
    /// Twenty samples carry the ordinals 1 through 20 exactly once each, fed in
    /// a deliberately shuffled order, so a summarizer that forgot to sort would
    /// fail. Each span multiplies the ordinal by its own distinct factor, so a
    /// percentile wired to the wrong span reports the wrong multiple and fails
    /// rather than coincidentally matching.
    ///
    /// The percentile helper is nearest-rank, rank = ceil(p * n) - 1, indexing
    /// the ascending sequence. With n = 20:
    ///   p50 -> ceil(0.50 * 20) - 1 = ceil(10.0) - 1 = 10 - 1 = 9  -> ordinal 10
    ///   p95 -> ceil(0.95 * 20) - 1 = ceil(19.0) - 1 = 19 - 1 = 18 -> ordinal 19
    ///   p99 -> ceil(0.99 * 20) - 1 = ceil(19.8) - 1 = 20 - 1 = 19 -> ordinal 20
    /// So a span valued at factor * ordinal has the expected triple
    /// (factor * 10, factor * 19, factor * 20):
    ///   clear          factor 1 -> ( 10,  19,  20)
    ///   layout         factor 2 -> ( 20,  38,  40)
    ///   hoverSelection factor 3 -> ( 30,  57,  60)
    ///   uiLayer        factor 4 -> ( 40,  76,  80)
    ///   baseDraw       factor 5 -> ( 50,  95, 100)
    ///   arenaGeometry  factor 6 -> ( 60, 114, 120)
    ///   probeOverhead  factor 7 -> ( 70, 133, 140)
    /// The invocation counter is a peak, not a percentile: 10 * 20 = 200. Its
    /// peak sample sits second in the shuffled input, so a summarizer that read
    /// the last sample instead of the maximum would report 150 and fail.
    /// </summary>
    [Fact]
    public void Summarize_NewTier1SpanPercentilesMatchHandComputedNearestRank()
    {
        int[] shuffledOrdinals =
        [
            7, 20, 3, 14, 1, 18, 11, 5, 9, 16,
            2, 13, 6, 19, 4, 12, 8, 17, 10, 15,
        ];

        var samples = new RenderProbeSample[shuffledOrdinals.Length];
        for (var index = 0; index < shuffledOrdinals.Length; index++)
        {
            var ordinal = shuffledOrdinals[index];
            samples[index] = new RenderProbeSample(
                FrameMilliseconds: ordinal,
                Metrics: CreateMetrics(
                    quads: 0,
                    triangles: 0,
                    geometryBuildMicroseconds: 0,
                    submitMicroseconds: 0,
                    managedBytesAllocated: 0,
                    clearMicroseconds: 1 * ordinal,
                    layoutMicroseconds: 2 * ordinal,
                    hoverSelectionMicroseconds: 3 * ordinal,
                    uiLayerMicroseconds: 4 * ordinal,
                    baseDrawMicroseconds: 5 * ordinal,
                    arenaGeometryMicroseconds: 6 * ordinal,
                    probeOverheadMicroseconds: 7 * ordinal,
                    pawnGeometryInvocations: 10 * ordinal),
                Gen0Collections: 0,
                Gen1Collections: 0,
                Gen2Collections: 0,
                AllocatedBytes: 0);
        }

        var result = RenderProbeStatistics.Summarize("default-fit", samples);

        Assert.Equal(20, result.FrameCount);

        // Frame time carries the bare ordinal, so it pins the same three ranks
        // every span assertion below relies on.
        Assert.Equal(10.0, result.FrameMillisecondsP50);
        Assert.Equal(19.0, result.FrameMillisecondsP95);
        Assert.Equal(20.0, result.FrameMillisecondsP99);

        Assert.Equal(10.0, result.ClearMicrosecondsP50);
        Assert.Equal(19.0, result.ClearMicrosecondsP95);
        Assert.Equal(20.0, result.ClearMicrosecondsP99);

        Assert.Equal(20.0, result.LayoutMicrosecondsP50);
        Assert.Equal(38.0, result.LayoutMicrosecondsP95);
        Assert.Equal(40.0, result.LayoutMicrosecondsP99);

        Assert.Equal(30.0, result.HoverSelectionMicrosecondsP50);
        Assert.Equal(57.0, result.HoverSelectionMicrosecondsP95);
        Assert.Equal(60.0, result.HoverSelectionMicrosecondsP99);

        Assert.Equal(40.0, result.UiLayerMicrosecondsP50);
        Assert.Equal(76.0, result.UiLayerMicrosecondsP95);
        Assert.Equal(80.0, result.UiLayerMicrosecondsP99);

        Assert.Equal(50.0, result.BaseDrawMicrosecondsP50);
        Assert.Equal(95.0, result.BaseDrawMicrosecondsP95);
        Assert.Equal(100.0, result.BaseDrawMicrosecondsP99);

        Assert.Equal(60.0, result.ArenaGeometryMicrosecondsP50);
        Assert.Equal(114.0, result.ArenaGeometryMicrosecondsP95);
        Assert.Equal(120.0, result.ArenaGeometryMicrosecondsP99);

        Assert.Equal(70.0, result.ProbeOverheadMicrosecondsP50);
        Assert.Equal(133.0, result.ProbeOverheadMicrosecondsP95);
        Assert.Equal(140.0, result.ProbeOverheadMicrosecondsP99);

        // A peak, not a percentile, and not the last sample's value of 150.
        Assert.Equal(200, result.PawnGeometryInvocationsMaximum);
    }

    [Fact]
    public void Summarize_GcAndAllocationDeltasAreLastMinusFirstSample()
    {
        var metrics = CreateMetrics(0, 0, 0, 0, 0);
        RenderProbeSample[] samples =
        [
            new RenderProbeSample(1.0, metrics, 5, 3, 1, 10_000),
            new RenderProbeSample(1.0, metrics, 5, 3, 1, 12_500),
            new RenderProbeSample(1.0, metrics, 6, 4, 1, 14_000),
        ];

        var result = RenderProbeStatistics.Summarize("default-fit", samples);

        Assert.Equal(1, result.Gen0CollectionsDelta);
        Assert.Equal(1, result.Gen1CollectionsDelta);
        Assert.Equal(0, result.Gen2CollectionsDelta);
        Assert.Equal(4_000L, result.AllocatedBytesDelta);
    }

    [Fact]
    public void Report_SerializedThenDeserializedRoundTripsEveryField()
    {
        var report = new RenderProbeReport(
            new RenderProbeFingerprint(
                "TEST-BENCH",
                1920,
                1080,
                "Release",
                "spritebatch-1x1",
                // Deliberately not the shipped client's value, so a field that
                // silently failed to round-trip would read back as true.
                VerticalRetraceSynchronized: false,
                // Exactly representable in binary floating point (31/16), so
                // the assertion below is an equality, not a tolerance.
                ProbeDuplicationFactor: 1.9375,
                new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc)),
            AgentCount: 200,
            Seed: 1UL,
            Stations:
            [
                new RenderProbeStationResult(
                    "minimum-zoom", 300,
                    4.1, 6.2, 8.3,
                    11_500, 23_000, 2_100,
                    120, 180, 220,
                    340, 410, 470,
                    11, 14, 17,
                    21, 24, 27,
                    31, 34, 37,
                    41, 44, 47,
                    51, 54, 57,
                    61, 64, 67,
                    71, 74, 77,
                    3_200,
                    11_500, true,
                    1, true,
                    1, true,
                    0, false,
                    2, 1, 0, 3_200),
                new RenderProbeStationResult(
                    "default-fit", 300,
                    3.9, 5.8, 7.9,
                    10_900, 21_800, 1_950,
                    115, 175, 210,
                    330, 400, 460,
                    12, 15, 18,
                    22, 25, 28,
                    32, 35, 38,
                    42, 45, 48,
                    52, 55, 58,
                    62, 65, 68,
                    72, 75, 78,
                    3_050,
                    10_900, true,
                    1, true,
                    1, true,
                    0, false,
                    2, 1, 0, 3_050),
                new RenderProbeStationResult(
                    "maximum-zoom", 300,
                    5.0, 7.4, 9.1,
                    12_800, 25_600, 2_400,
                    130, 190, 230,
                    350, 420, 480,
                    13, 16, 19,
                    23, 26, 29,
                    33, 36, 39,
                    43, 46, 49,
                    53, 56, 59,
                    63, 66, 69,
                    73, 76, 79,
                    3_400,
                    12_800, true,
                    1, true,
                    1, true,
                    0, false,
                    2, 1, 0, 3_400),
            ]);

        var json = JsonSerializer.Serialize(
            report,
            RenderProbeReport.SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<RenderProbeReport>(
            json,
            RenderProbeReport.SerializerOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(report.AgentCount, roundTripped.AgentCount);
        Assert.Equal(report.Seed, roundTripped.Seed);
        Assert.Equal(
            report.Fingerprint.HardwareName,
            roundTripped.Fingerprint.HardwareName);
        Assert.Equal(
            report.Fingerprint.ResolutionWidth,
            roundTripped.Fingerprint.ResolutionWidth);
        Assert.Equal(
            report.Fingerprint.ResolutionHeight,
            roundTripped.Fingerprint.ResolutionHeight);
        Assert.Equal(
            report.Fingerprint.BuildConfiguration,
            roundTripped.Fingerprint.BuildConfiguration);
        Assert.Equal(
            report.Fingerprint.Backend,
            roundTripped.Fingerprint.Backend);
        Assert.False(roundTripped.Fingerprint.VerticalRetraceSynchronized);
        Assert.Equal(1.9375, roundTripped.Fingerprint.ProbeDuplicationFactor);
        Assert.Equal(
            report.Fingerprint.CapturedAtUtc,
            roundTripped.Fingerprint.CapturedAtUtc);
        Assert.Equal(report.Stations.Count, roundTripped.Stations.Count);
        for (var index = 0; index < report.Stations.Count; index++)
        {
            Assert.Equal(report.Stations[index], roundTripped.Stations[index]);
        }
    }

    [Fact]
    public void Report_SerializedJsonNamesEveryNewFieldInCamelCase()
    {
        var station = RenderProbeStatistics.Summarize(
            "default-fit",
            [
                new RenderProbeSample(
                    FrameMilliseconds: 1.0,
                    Metrics: CreateMetrics(
                        quads: 1,
                        triangles: 2,
                        geometryBuildMicroseconds: 3,
                        submitMicroseconds: 4,
                        managedBytesAllocated: 5,
                        clearMicroseconds: 6,
                        layoutMicroseconds: 7,
                        hoverSelectionMicroseconds: 8,
                        uiLayerMicroseconds: 9,
                        baseDrawMicroseconds: 10,
                        arenaGeometryMicroseconds: 11,
                        probeOverheadMicroseconds: 12,
                        pawnGeometryInvocations: 13),
                    Gen0Collections: 0,
                    Gen1Collections: 0,
                    Gen2Collections: 0,
                    AllocatedBytes: 0),
            ]);

        var report = new RenderProbeReport(
            new RenderProbeFingerprint(
                "TEST-BENCH",
                1920,
                1080,
                "Release",
                "spritebatch-1x1",
                VerticalRetraceSynchronized: false,
                ProbeDuplicationFactor: 2.0,
                new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc)),
            AgentCount: 200,
            Seed: 1UL,
            Stations: [station]);

        var json = JsonSerializer.Serialize(
            report,
            RenderProbeReport.SerializerOptions);

        Assert.Contains("\"verticalRetraceSynchronized\"", json, StringComparison.Ordinal);
        Assert.Contains("\"probeDuplicationFactor\"", json, StringComparison.Ordinal);
        Assert.Contains("\"pawnGeometryInvocationsMaximum\"", json, StringComparison.Ordinal);
        Assert.Contains("\"clearMicrosecondsP50\"", json, StringComparison.Ordinal);
        Assert.Contains("\"layoutMicrosecondsP95\"", json, StringComparison.Ordinal);
        Assert.Contains("\"hoverSelectionMicrosecondsP99\"", json, StringComparison.Ordinal);
        Assert.Contains("\"uiLayerMicrosecondsP50\"", json, StringComparison.Ordinal);
        Assert.Contains("\"baseDrawMicrosecondsP95\"", json, StringComparison.Ordinal);
        Assert.Contains("\"arenaGeometryMicrosecondsP99\"", json, StringComparison.Ordinal);
        Assert.Contains("\"probeOverheadMicrosecondsP50\"", json, StringComparison.Ordinal);
    }
}
