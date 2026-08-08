using System.Reflection;
using Sandata.Headless;

namespace Sandata.Core.Tests;

/// <summary>
/// The test bar for plan task 50: <c>NavBenchmarkOptions</c> validates each
/// of its five navigation-benchmark matrix parameters against its own named
/// range and refuses to build with any one of them missing, and
/// <c>NavBenchmark</c>'s percentile helper matches
/// <c>Hukbo.Headless.RunReport</c>'s nearest-rank convention exactly,
/// including at the one-sample and two-sample edges. This file also covers
/// the command-line surface plan task 50 adds to <c>Program.cs</c>: every
/// new <c>--nav-*</c> flag parses, <c>--help</c> lists all five matrix
/// flags, an unrecognised argument's exit code is unchanged, and supplying
/// only some of the five matrix flags is refused by name rather than
/// silently defaulting the rest.
/// </summary>
public sealed class NavBenchmarkOptionTests
{
    private const int ValidMapDensityPercent = 10;
    private const int ValidChangedCellCount = 5;
    private const int ValidConcurrentSeekers = 4;
    private const int ValidQueryDistanceWu = 64;
    private const int ValidReplanningRatePercent = 20;

    [Fact]
    public void ValidValuesConstructWithoutThrowing()
    {
        var options = NavBenchmarkOptions.Create(
            ValidMapDensityPercent,
            ValidChangedCellCount,
            ValidConcurrentSeekers,
            ValidQueryDistanceWu,
            ValidReplanningRatePercent);

        Assert.Equal(ValidMapDensityPercent, options.MapDensityPercent);
        Assert.Equal(ValidChangedCellCount, options.ChangedCellCount);
        Assert.Equal(ValidConcurrentSeekers, options.ConcurrentSeekers);
        Assert.Equal(ValidQueryDistanceWu, options.QueryDistanceWu);
        Assert.Equal(ValidReplanningRatePercent, options.ReplanningRatePercent);
    }

    [Theory]
    [InlineData(NavBenchmarkOptions.MinMapDensityPercent - 1)]
    [InlineData(NavBenchmarkOptions.MaxMapDensityPercent + 1)]
    public void MapDensityPercentOutOfRangeIsRefusedByName(int invalidValue)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            NavBenchmarkOptions.Create(
                invalidValue,
                ValidChangedCellCount,
                ValidConcurrentSeekers,
                ValidQueryDistanceWu,
                ValidReplanningRatePercent));

        Assert.Equal("mapDensityPercent", exception.ParamName);
    }

    [Theory]
    [InlineData(NavBenchmarkOptions.MinChangedCellCount - 1)]
    [InlineData(NavBenchmarkOptions.MaxChangedCellCount + 1)]
    public void ChangedCellCountOutOfRangeIsRefusedByName(int invalidValue)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            NavBenchmarkOptions.Create(
                ValidMapDensityPercent,
                invalidValue,
                ValidConcurrentSeekers,
                ValidQueryDistanceWu,
                ValidReplanningRatePercent));

        Assert.Equal("changedCellCount", exception.ParamName);
    }

    [Theory]
    [InlineData(NavBenchmarkOptions.MinConcurrentSeekers - 1)]
    [InlineData(NavBenchmarkOptions.MaxConcurrentSeekers + 1)]
    public void ConcurrentSeekersOutOfRangeIsRefusedByName(int invalidValue)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            NavBenchmarkOptions.Create(
                ValidMapDensityPercent,
                ValidChangedCellCount,
                invalidValue,
                ValidQueryDistanceWu,
                ValidReplanningRatePercent));

        Assert.Equal("concurrentSeekers", exception.ParamName);
    }

    [Theory]
    [InlineData(NavBenchmarkOptions.MinQueryDistanceWu - 1)]
    [InlineData(NavBenchmarkOptions.MaxQueryDistanceWu + 1)]
    public void QueryDistanceWuOutOfRangeIsRefusedByName(int invalidValue)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            NavBenchmarkOptions.Create(
                ValidMapDensityPercent,
                ValidChangedCellCount,
                ValidConcurrentSeekers,
                invalidValue,
                ValidReplanningRatePercent));

        Assert.Equal("queryDistanceWu", exception.ParamName);
    }

    [Theory]
    [InlineData(NavBenchmarkOptions.MinReplanningRatePercent - 1)]
    [InlineData(NavBenchmarkOptions.MaxReplanningRatePercent + 1)]
    public void ReplanningRatePercentOutOfRangeIsRefusedByName(int invalidValue)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            NavBenchmarkOptions.Create(
                ValidMapDensityPercent,
                ValidChangedCellCount,
                ValidConcurrentSeekers,
                ValidQueryDistanceWu,
                invalidValue));

        Assert.Equal("replanningRatePercent", exception.ParamName);
    }

    /// <summary>
    /// The property test over the whole option set the plan brief asks for:
    /// no parameter of <see cref="NavBenchmarkOptions.Create"/> may carry a
    /// default value. A convenient default on even one parameter would let a
    /// caller omit it and get a benchmark run whose numbers cannot be
    /// compared to another run — exactly the failure mode a
    /// wrong-but-plausible implementation would introduce, and exactly what
    /// per-parameter range tests alone would not catch.
    /// </summary>
    [Fact]
    public void CreateRequiresAllFiveParametersWithNoDefaultValue()
    {
        var method = typeof(NavBenchmarkOptions).GetMethod(
            nameof(NavBenchmarkOptions.Create),
            BindingFlags.Public | BindingFlags.Static)!;
        var parameters = method.GetParameters();

        Assert.Equal(5, parameters.Length);
        Assert.All(parameters, parameter => Assert.False(parameter.HasDefaultValue));
    }

    [Fact]
    public void PercentileOfAnEmptySampleSetIsZero()
    {
        Assert.Equal(0, NavBenchmark.Percentile([], 0.50));
        Assert.Equal(0, NavBenchmark.Percentile([], 0.95));
        Assert.Equal(0, NavBenchmark.Percentile([], 0.99));
    }

    /// <summary>
    /// A single-sample set: every percentile's rank clamps to the one
    /// available index regardless of the requested percentile.
    /// </summary>
    [Fact]
    public void PercentileOfASingleSampleReturnsThatSample()
    {
        double[] samples = [5.0];

        Assert.Equal(5.0, NavBenchmark.Percentile(samples, 0.50));
        Assert.Equal(5.0, NavBenchmark.Percentile(samples, 0.95));
        Assert.Equal(5.0, NavBenchmark.Percentile(samples, 0.99));
    }

    /// <summary>
    /// A two-sample set, hand-computed against the nearest-rank formula
    /// <c>rank = ceiling(percentile * count) - 1</c>: p50 lands on index 0
    /// (<c>ceiling(1.0) - 1 = 0</c>) and both p95 and p99 land on index 1
    /// (<c>ceiling(1.9) - 1 = 1</c>, <c>ceiling(1.98) - 1 = 1</c>) — the
    /// edge naive index arithmetic (for example truncating rather than
    /// taking a ceiling, or not subtracting one) most often gets wrong.
    /// </summary>
    [Fact]
    public void PercentileOfTwoSamplesMatchesTheHandComputedNearestRank()
    {
        double[] samples = [1.0, 2.0];

        Assert.Equal(1.0, NavBenchmark.Percentile(samples, 0.50));
        Assert.Equal(2.0, NavBenchmark.Percentile(samples, 0.95));
        Assert.Equal(2.0, NavBenchmark.Percentile(samples, 0.99));
    }

    /// <summary>
    /// A four-sample set as a broader sanity check on top of the required
    /// one- and two-sample edges: <c>ceiling(2.0) - 1 = 1</c> for p50,
    /// <c>ceiling(3.8) - 1 = 3</c> for both p95 and p99.
    /// </summary>
    [Fact]
    public void PercentileOfFourSamplesMatchesTheHandComputedNearestRank()
    {
        double[] samples = [10.0, 20.0, 30.0, 40.0];

        Assert.Equal(20.0, NavBenchmark.Percentile(samples, 0.50));
        Assert.Equal(40.0, NavBenchmark.Percentile(samples, 0.95));
        Assert.Equal(40.0, NavBenchmark.Percentile(samples, 0.99));
    }

    [Fact]
    public void HelpListsAllFiveNavigationBenchmarkMatrixFlagsAndReturnsSuccess()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(["--help"], output, error);

        Assert.Equal(Program.ExitSuccess, exitCode);
        var usage = output.ToString();
        Assert.Contains("--nav-map-density", usage, StringComparison.Ordinal);
        Assert.Contains("--nav-changed-cells", usage, StringComparison.Ordinal);
        Assert.Contains("--nav-seekers", usage, StringComparison.Ordinal);
        Assert.Contains("--nav-query-distance", usage, StringComparison.Ordinal);
        Assert.Contains("--nav-replan-rate", usage, StringComparison.Ordinal);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void UnrecognisedArgumentExitCodeIsUnchangedByTheNavigationBenchmarkFlags()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(["--not-a-real-flag", "value"], output, error);

        Assert.Equal(Program.ExitArgumentError, exitCode);
    }

    [Fact]
    public void TryParseArgumentsAcceptsEveryNavigationBenchmarkFlag()
    {
        var parsed = Program.TryParseArguments(
            [
                "--nav-map-density", "10",
                "--nav-changed-cells", "5",
                "--nav-seekers", "4",
                "--nav-query-distance", "64",
                "--nav-replan-rate", "20",
                "--nav-seed", "42",
                "--nav-ticks", "7",
                "--nav-fixture-path", "somewhere.hkmap",
            ],
            out var options,
            out var error);

        Assert.True(parsed);
        Assert.Equal(string.Empty, error);
        Assert.Equal(10, options.NavMapDensityPercent);
        Assert.Equal(5, options.NavChangedCellCount);
        Assert.Equal(4, options.NavConcurrentSeekers);
        Assert.Equal(64, options.NavQueryDistanceWu);
        Assert.Equal(20, options.NavReplanningRatePercent);
        Assert.Equal(42UL, options.NavSeed);
        Assert.Equal(7, options.NavTickCount);
        Assert.Equal("somewhere.hkmap", options.NavFixturePath);
    }

    /// <summary>
    /// No <c>--nav-*</c> flag at all leaves the navigation benchmark
    /// untriggered, so the run falls through to the existing boot-log
    /// behaviour rather than failing — supplying none of the five is
    /// "benchmark not requested", not "benchmark requested with missing
    /// values".
    /// </summary>
    [Fact]
    public void NoNavigationFlagsLeavesTheOrdinaryBootFlowUntouched()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(["--log-level", "off"], output, error);

        Assert.Equal(Program.ExitSuccess, exitCode);
        Assert.Empty(error.ToString());
    }

    /// <summary>
    /// Supplying some but not all five matrix flags is refused, naming every
    /// flag still missing — the CLI-layer mirror of
    /// <see cref="CreateRequiresAllFiveParametersWithNoDefaultValue"/>: no
    /// convenient default may let a partially-specified run start anyway.
    /// </summary>
    [Fact]
    public void PartialNavigationFlagsAreRejectedNamingEveryMissingFlag()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = Program.Run(
            ["--nav-map-density", "10", "--nav-seekers", "4"], output, error);

        Assert.Equal(Program.ExitArgumentError, exitCode);
        var message = error.ToString();
        Assert.Contains("--nav-changed-cells", message, StringComparison.Ordinal);
        Assert.Contains("--nav-query-distance", message, StringComparison.Ordinal);
        Assert.Contains("--nav-replan-rate", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An end-to-end structural check against the real angle-house fixture,
    /// resolved through <see cref="NavBenchmark.ResolveFixturePath"/>'s
    /// repository-root discovery rather than a hardcoded path. Deliberately
    /// asserts shape — sample counts and percentile ordering — never a
    /// timing value: this project's rule is that a benchmark's actual
    /// numbers are verified by a human running it and reading the output,
    /// not by a unit test asserting on wall-clock duration.
    /// </summary>
    [Fact]
    public void RunProducesExpectedSampleCountsAgainstTheRealFixture()
    {
        var fixturePath = NavBenchmark.ResolveFixturePath(null);
        var options = NavBenchmarkOptions.Create(
            mapDensityPercent: 0,
            changedCellCount: 0,
            concurrentSeekers: 3,
            queryDistanceWu: 64,
            replanningRatePercent: 0);

        var report = NavBenchmark.Run(options, fixturePath, seed: 1, tickCount: 5);

        Assert.Equal(3, report.AStarQuerySampleCount);
        Assert.Equal(5, report.TickStageSampleCount);
        Assert.True(report.AStarQueryPercentiles.P50Milliseconds <= report.AStarQueryPercentiles.P95Milliseconds);
        Assert.True(report.AStarQueryPercentiles.P95Milliseconds <= report.AStarQueryPercentiles.P99Milliseconds);
        Assert.True(report.TickStagePercentiles.P50Milliseconds <= report.TickStagePercentiles.P95Milliseconds);
        Assert.True(report.TickStagePercentiles.P95Milliseconds <= report.TickStagePercentiles.P99Milliseconds);
    }
}
