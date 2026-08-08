using System.Collections.Immutable;
using Hukbo.Diagnostics;
using Sandata.Core.Orders;
using Sandata.Headless;

namespace Sandata.Core.Tests;

/// <summary>
/// The seeded determinism workload added by task 51 of
/// docs/plans/2026-08-07-sandata-scaffold.md: <see cref="HeadlessRunner.Execute"/>
/// and its dispatch through <c>Program</c>.
/// </summary>
/// <remarks>
/// <b>No expected-hash literal anywhere in this file.</b> The wave-11 audit
/// forbids pinning a golden Sandata hash this wave, because task 77 (landing
/// in the same batch) moves <c>SandataRuleset.ContentHash</c>, which moves
/// every mission hash with it. Every assertion below is a self-consistency
/// check — two runs of one seed agreeing with each other, a corrupted run
/// disagreeing with itself — never a comparison against a literal typed
/// here.
/// </remarks>
public sealed class HeadlessRunnerTests
{
    // Small enough to keep this suite fast, even across many facts; large
    // enough that the tick pipeline's stages (sensing, squad grouping,
    // collision, fire resolution) actually run rather than mostly idling.
    private const int OperatorCount = 20;
    private const int TickCount = 30;
    private const ulong Seed = 1;

    [Fact]
    public void ExecuteProducesAFullyPopulatedRunReport()
    {
        var report = HeadlessRunner.Execute(OperatorCount, TickCount, Seed, DiagnosticLog.Disabled);

        Assert.NotNull(report.Environment);
        Assert.False(string.IsNullOrWhiteSpace(report.Environment.OperatingSystem));
        Assert.False(string.IsNullOrWhiteSpace(report.Environment.Framework));
        Assert.False(string.IsNullOrWhiteSpace(report.Environment.ProcessArchitecture));
        Assert.True(report.Environment.ProcessorCount > 0);

        Assert.Equal(Seed, report.Seed);
        Assert.Equal(OperatorCount / 2, report.OperatorsPerFaction);
        Assert.Equal(TickCount, report.RequestedTicks);
        Assert.Equal(TickCount, report.MeasuredTicks);
        Assert.True(report.DurationMilliseconds > 0);

        Assert.NotNull(report.TickPercentiles);
        Assert.True(report.TickPercentiles.P50Milliseconds >= 0);
        Assert.True(report.TickPercentiles.P95Milliseconds >= report.TickPercentiles.P50Milliseconds);
        Assert.True(report.TickPercentiles.P99Milliseconds >= report.TickPercentiles.P95Milliseconds);
        Assert.True(report.TickPercentiles.MaximumMilliseconds >= report.TickPercentiles.P99Milliseconds);
        Assert.True(report.TickPercentiles.MaximumMilliseconds > 0);

        Assert.True(report.AllocatedBytes > 0);
        Assert.False(string.IsNullOrWhiteSpace(report.Outcome));
        Assert.True(report.Faction0Survivors >= 0);
        Assert.True(report.Faction1Survivors >= 0);
        Assert.False(string.IsNullOrWhiteSpace(report.EventHash));
        Assert.False(string.IsNullOrWhiteSpace(report.StateHash));
        Assert.True(report.Deterministic);
        Assert.Null(report.FirstMismatchTick);
    }

    [Fact]
    public void TwoRunsOfTheSameSeedAgreeOnBothHashesAndOutcome()
    {
        var first = HeadlessRunner.Execute(OperatorCount, TickCount, Seed, DiagnosticLog.Disabled);
        var second = HeadlessRunner.Execute(OperatorCount, TickCount, Seed, DiagnosticLog.Disabled);

        Assert.True(first.Deterministic);
        Assert.True(second.Deterministic);
        Assert.Equal(first.StateHash, second.StateHash);
        // The event hash is a rolling FNV-1a fold over every event ever
        // appended to the feed (RunReport's own remarks), so two runs
        // agreeing here is itself evidence the two ordered event streams
        // agreed, not merely their final tick's snapshot.
        Assert.Equal(first.EventHash, second.EventHash);
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.Faction0Survivors, second.Faction0Survivors);
        Assert.Equal(first.Faction1Survivors, second.Faction1Survivors);
    }

    [Fact]
    public void ACorruptedSecondSimulationReturnsTheFirstMismatchTick()
    {
        // Every MoveAlongPath order submitted with fewer than two path
        // nodes fails OrderQueue's node-count check before its addressees
        // are even inspected (confirmed by reading OrderQueue.SubmitValidated),
        // making this the cheapest deterministic corruption available
        // through SandataSimulation's own public SubmitOrder door — no edit
        // to src/Sandata.Core required. The corruption fires on tick 0, so
        // that is the tick the two simulations must first disagree on.
        const long expectedFirstMismatchTick = 0;

        var report = HeadlessRunner.Execute(
            OperatorCount,
            TickCount,
            Seed,
            DiagnosticLog.Disabled,
            corruptRightSimulation: (rightSimulation, tick) => rightSimulation.SubmitOrder(
                tick,
                factionId: 0,
                addressees: ImmutableArray<ulong>.Empty,
                kind: OrderKind.MoveAlongPath));

        Assert.False(report.Deterministic);
        Assert.Equal(expectedFirstMismatchTick, report.FirstMismatchTick);
        Assert.Equal(
            Program.ExitDeterminismMismatch, Program.DetermineDeterminismExitCode(report));
    }

    [Fact]
    public void ADeterministicReportMapsToTheSuccessExitCode()
    {
        var report = HeadlessRunner.Execute(OperatorCount, TickCount, Seed, DiagnosticLog.Disabled);

        Assert.Equal(Program.ExitSuccess, Program.DetermineDeterminismExitCode(report));
    }

    [Fact]
    public void HelpReturnsSuccessBeforeAnyDeterminismFlagIsConsidered()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(["--agents", "not-a-number", "--help"], output, error);

        Assert.Equal(Program.ExitSuccess, exitCode);
    }

    [Fact]
    public void SupplyingOnlySomeDeterminismFlagsReturnsTheArgumentErrorExitCode()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(["--agents", "4"], output, error);

        Assert.Equal(Program.ExitArgumentError, exitCode);
        Assert.Contains("--ticks", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("--seed", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ANonOddAgentsValueIsRejectedWithTheArgumentErrorExitCode()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(
            ["--agents", "3", "--ticks", "5", "--seed", "1"], output, error);

        Assert.Equal(Program.ExitArgumentError, exitCode);
        Assert.Contains("positive even integer", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SupplyingAllThreeDeterminismFlagsRunsTheWorkloadAndPrintsTheReport()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(
            ["--agents", "4", "--ticks", "3", "--seed", "1", "--log-level", "off"],
            output,
            error);

        Assert.Equal(Program.ExitSuccess, exitCode);
        Assert.Contains("\"seed\"", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"deterministic\"", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnwritableOutputPathReturnsTheUnhandledExceptionExitCode()
    {
        // The --output path is only validated for being a nonempty string
        // at parse time; failing to write it is a filesystem condition
        // Program deliberately lets its outer catch (Exception) handle,
        // exactly like any other unanticipated failure. A path that names
        // an existing directory, rather than a file, reproduces that
        // failure deterministically on every platform: writing a file over
        // a directory always fails.
        var directoryOutputPath = Directory.CreateTempSubdirectory("sandata-headless-test-").FullName;
        try
        {
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = Program.Run(
                [
                    "--agents", "2", "--ticks", "1", "--seed", "1",
                    "--log-level", "off", "--output", directoryOutputPath,
                ],
                output,
                error);

            Assert.Equal(Program.ExitUnhandledException, exitCode);
            Assert.Contains("Sandata headless run failed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directoryOutputPath, recursive: true);
        }
    }
}
