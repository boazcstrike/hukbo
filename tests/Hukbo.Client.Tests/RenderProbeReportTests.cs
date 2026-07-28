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
        int textureBinds = 0) =>
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
            BufferUploadBytesApplicable: false);

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
        Assert.Equal(0, result.GeometryBuildMicrosecondsP50);
        Assert.Equal(0, result.SubmitMicrosecondsP50);
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
                new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc)),
            AgentCount: 200,
            Seed: 1UL,
            Stations:
            [
                new RenderProbeStationResult(
                    "minimum-zoom", 300,
                    4.1, 6.2, 8.3,
                    11_500, 23_000,
                    120, 180, 220,
                    340, 410, 470,
                    3_200,
                    11_500, true,
                    1, true,
                    1, true,
                    0, false,
                    2, 1, 0, 3_200),
                new RenderProbeStationResult(
                    "default-fit", 300,
                    3.9, 5.8, 7.9,
                    10_900, 21_800,
                    115, 175, 210,
                    330, 400, 460,
                    3_050,
                    10_900, true,
                    1, true,
                    1, true,
                    0, false,
                    2, 1, 0, 3_050),
                new RenderProbeStationResult(
                    "maximum-zoom", 300,
                    5.0, 7.4, 9.1,
                    12_800, 25_600,
                    130, 190, 230,
                    350, 420, 480,
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
        Assert.Equal(
            report.Fingerprint.CapturedAtUtc,
            roundTripped.Fingerprint.CapturedAtUtc);
        Assert.Equal(report.Stations.Count, roundTripped.Stations.Count);
        for (var index = 0; index < report.Stations.Count; index++)
        {
            Assert.Equal(report.Stations[index], roundTripped.Stations[index]);
        }
    }
}
