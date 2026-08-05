using System.Text.Json;
using Hukbo.Core.Simulation;
using Hukbo.Diagnostics;
using Hukbo.Headless;

namespace Hukbo.Core.Tests;

/// <summary>
/// The rules that keep the debug log from becoming a way to change the
/// simulation. Both are enforced here rather than in prose, because a
/// convention that only lives in a document gets broken by the third agent to
/// touch the file.
/// </summary>
public sealed class DiagnosticLoggingBoundaryTests
{
    /// <summary>
    /// <c>Hukbo.Core</c> is forbidden the filesystem and the wall clock, and
    /// the logger needs both. If this fails, someone added a log call inside the
    /// simulation and the determinism contract is no longer sound.
    /// </summary>
    [Fact]
    public void CoreDoesNotReferenceTheDiagnosticsAssembly()
    {
        var referenced = typeof(Scenario).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("Hukbo.Diagnostics", referenced);
    }

    /// <summary>
    /// The positive control for the assertion above. Without it, deleting the
    /// diagnostics assembly entirely would leave that test passing and prove
    /// nothing.
    /// </summary>
    [Fact]
    public void TheHeadlessRunnerDoesReferenceTheDiagnosticsAssembly()
    {
        var referenced = typeof(HeadlessRunner).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.Contains("Hukbo.Diagnostics", referenced);
    }

    /// <summary>
    /// The load-bearing claim of the whole facility: turning the log all the
    /// way up cannot move a hash, change an outcome, or reorder an event.
    /// </summary>
    [Fact]
    public void FullTraceLoggingDoesNotChangeTheSimulationResult()
    {
        var logDirectory = Path.Combine(
            Path.GetTempPath(),
            "hukbo-log-determinism-" + Guid.NewGuid().ToString("N"));

        try
        {
            var silent = RunHeadless(
                ["--agents", "200", "--ticks", "2000", "--seed", "1",
                 "--log-level", "off"]);
            var traced = RunHeadless(
                ["--agents", "200", "--ticks", "2000", "--seed", "1",
                 "--log-level", "trc", "--log-channels", "all",
                 "--log-dir", logDirectory]);

            Assert.Equal(silent.StateHash, traced.StateHash);
            Assert.Equal(silent.EventHash, traced.EventHash);
            Assert.Equal(silent.Outcome, traced.Outcome);
            Assert.Equal(silent.MeasuredTicks, traced.MeasuredTicks);
            Assert.Equal(silent.Faction0Survivors, traced.Faction0Survivors);
            Assert.Equal(silent.Faction1Survivors, traced.Faction1Survivors);
            Assert.True(silent.Deterministic);
            Assert.True(traced.Deterministic);

            // The traced run must actually have written something, or this
            // test would pass just as happily with the logger unplugged.
            var written = Directory.GetFiles(
                logDirectory,
                LogPaths.FileNamePrefix + "*" + LogPaths.FileNameExtension);
            var singleFile = Assert.Single(written);
            Assert.NotEmpty(File.ReadAllLines(singleFile));
        }
        finally
        {
            if (Directory.Exists(logDirectory))
            {
                Directory.Delete(logDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Task F0 of docs/archives/2026-08-06/movement/2026-07-31-movement-v7-pressure-interrupt.md. The
    /// same claim as
    /// <see cref="FullTraceLoggingDoesNotChangeTheSimulationResult"/>, made
    /// again under <c>EquipmentRelativeFootworkV7</c>. The preset above it runs
    /// is whatever <c>Scenario</c> defaults to, which is
    /// <c>PersistentContingentsV4</c> and never reaches the footwork stage at
    /// all, so nothing in this file exercised the pressure interrupt before
    /// this Fact existed.
    /// </summary>
    [Fact]
    public void FullTraceLoggingDoesNotChangeTheSimulationResultUnderV7()
    {
        var logDirectory = Path.Combine(
            Path.GetTempPath(),
            "hukbo-log-determinism-v7-" + Guid.NewGuid().ToString("N"));

        try
        {
            var silent = RunHeadless(
                ["--agents", "200", "--ticks", "2000", "--seed", "1",
                 "--movement-preset", "EquipmentRelativeFootworkV7",
                 "--log-level", "off"]);
            var traced = RunHeadless(
                ["--agents", "200", "--ticks", "2000", "--seed", "1",
                 "--movement-preset", "EquipmentRelativeFootworkV7",
                 "--log-level", "trc", "--log-channels", "all",
                 "--log-dir", logDirectory]);

            Assert.Equal(silent.StateHash, traced.StateHash);
            Assert.Equal(silent.EventHash, traced.EventHash);
            Assert.Equal(silent.Outcome, traced.Outcome);
            Assert.Equal(silent.MeasuredTicks, traced.MeasuredTicks);
            Assert.Equal(silent.Faction0Survivors, traced.Faction0Survivors);
            Assert.Equal(silent.Faction1Survivors, traced.Faction1Survivors);
            Assert.True(silent.Deterministic);
            Assert.True(traced.Deterministic);

            var written = Directory.GetFiles(
                logDirectory,
                LogPaths.FileNamePrefix + "*" + LogPaths.FileNameExtension);
            var singleFile = Assert.Single(written);
            Assert.NotEmpty(File.ReadAllLines(singleFile));
        }
        finally
        {
            if (Directory.Exists(logDirectory))
            {
                Directory.Delete(logDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Every line the runner writes must be valid JSON, and the six leading
    /// fields must be present on all of them.
    /// </summary>
    [Fact]
    public void EveryEmittedLineIsAJsonObjectCarryingTheLeadingFields()
    {
        var logDirectory = Path.Combine(
            Path.GetTempPath(),
            "hukbo-log-shape-" + Guid.NewGuid().ToString("N"));

        try
        {
            RunHeadless(
                ["--agents", "20", "--ticks", "300", "--seed", "1",
                 "--log-level", "dbg", "--log-dir", logDirectory]);

            var file = Assert.Single(Directory.GetFiles(
                logDirectory,
                LogPaths.FileNamePrefix + "*" + LogPaths.FileNameExtension));
            var lines = File.ReadAllLines(file);
            Assert.NotEmpty(lines);

            foreach (var line in lines)
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                Assert.Equal(JsonValueKind.Object, root.ValueKind);
                foreach (var field in new[] { "seq", "t", "ms", "lvl", "ch", "ev" })
                {
                    Assert.True(
                        root.TryGetProperty(field, out _),
                        $"Line is missing the '{field}' field: {line}");
                }
            }
        }
        finally
        {
            if (Directory.Exists(logDirectory))
            {
                Directory.Delete(logDirectory, recursive: true);
            }
        }
    }

    private static RunReport RunHeadless(string[] arguments)
    {
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();
        var exitCode = HeadlessRunner.Run(
            arguments,
            standardOutput,
            standardError);

        Assert.Equal(0, exitCode);
        var report = JsonSerializer.Deserialize<RunReport>(
            standardOutput.ToString(),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
        Assert.NotNull(report);
        return report;
    }
}
