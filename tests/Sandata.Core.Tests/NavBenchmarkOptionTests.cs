using System.Reflection;
using Sandata.Headless;

namespace Sandata.Core.Tests;

/// <summary>
/// A tiny <c>.hkmap</c> fixture, distinct from <c>angle-house</c>, built for
/// task 82's outcome-breakdown coverage: two 160-by-160-world-unit rooms
/// side by side inside one sealed perimeter, split by a solid interior
/// <c>WALL</c> with no <c>DOOR</c> anywhere along it, so the two rooms share
/// no passable cell at all in the baked nav grid — a real, deliberately
/// built disconnection, not a hoped-for side effect of a map-density
/// percentage. Carries no <c>OBJECTIVE</c> record, so <c>MapValidator</c>'s
/// faction-0-reachability rule (which only runs when at least one objective
/// exists) never applies to it.
/// </summary>
file static class DisconnectedRoomsFixture
{
    public const string Text =
        "HKMAP 1\n" +
        "NAME nav-unreachable-fixture\n" +
        "GRID 320 160 4\n" +
        "WALL 0 0 0 160 1\n" +
        "WALL 0 0 320 0 1\n" +
        "WALL 0 160 320 160 1\n" +
        "WALL 320 0 320 160 1\n" +
        "WALL 160 0 160 160 1\n" +
        "SPAWN 0 40 80 0\n" +
        "SPAWN 1 280 80 0\n" +
        "END\n";

    /// <summary>
    /// Writes <see cref="Text"/> to a fresh temporary file and returns its
    /// path. The caller owns deletion; <see cref="NavBenchmark.Run"/> only
    /// reads the path it is given.
    /// </summary>
    public static string WriteToTempFile()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, Text);
        return path;
    }
}

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

    /// <summary>
    /// Task 82's reproduction of the defect the wave-12 matrix run exposed:
    /// a benchmark configuration whose seekers can never reach their goal
    /// must report that plainly, rather than a fast p50 that looks like a
    /// pass. <see cref="DisconnectedRoomsFixture"/> guarantees this by
    /// construction — two rooms with no shared passable cell — rather than
    /// hoping a map-density percentage happens to disconnect
    /// <c>angle-house</c>. <see cref="NavBenchmarkOptions.MaxQueryDistanceWu"/>
    /// is used as the target distance so <c>PlaceSeekerPair</c>'s
    /// closest-to-target search always prefers whichever of its candidate
    /// draws is farthest from the start cell — a candidate in the opposite,
    /// unreachable room every time, for every one of the six seekers, at
    /// this fixture and seed.
    /// </summary>
    [Fact]
    public void AllGoalsUnreachableReportsZeroSuccessfulSearches()
    {
        var fixturePath = DisconnectedRoomsFixture.WriteToTempFile();
        try
        {
            var options = NavBenchmarkOptions.Create(
                mapDensityPercent: 0,
                changedCellCount: 0,
                concurrentSeekers: 6,
                queryDistanceWu: NavBenchmarkOptions.MaxQueryDistanceWu,
                replanningRatePercent: 0);

            var report = NavBenchmark.Run(options, fixturePath, seed: 1, tickCount: 1);

            Assert.Equal(6, report.AStarQuerySampleCount);
            Assert.Equal(0, report.ProbeOutcomeBreakdown.PathFoundQueryCount);
            Assert.Equal(6, report.ProbeOutcomeBreakdown.UnreachableQueryCount);
            Assert.Equal(0, report.SuccessfulAStarQuerySampleCount);
            Assert.Equal(0, report.SuccessfulAStarQueryPercentiles.P50Milliseconds);
            Assert.Equal(0, report.SuccessfulAStarQueryPercentiles.P95Milliseconds);
            Assert.Equal(0, report.SuccessfulAStarQueryPercentiles.P99Milliseconds);
        }
        finally
        {
            File.Delete(fixturePath);
        }
    }

    /// <summary>
    /// No probe query may go unaccounted for: the successful-search sample
    /// count plus every failure count in <see cref="NavBenchmarkReport.ProbeOutcomeBreakdown"/>
    /// must equal the report's total probe count. Checked against both the
    /// degenerate, all-unreachable fixture and the ordinary, mostly
    /// reachable <c>angle-house</c> fixture, so the accounting invariant is
    /// proven independent of how many queries actually found a path.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProbeOutcomeBreakdownAccountsForEveryProbeQuery(bool useDisconnectedFixture)
    {
        var fixturePath = useDisconnectedFixture
            ? DisconnectedRoomsFixture.WriteToTempFile()
            : NavBenchmark.ResolveFixturePath(null);
        try
        {
            var options = NavBenchmarkOptions.Create(
                mapDensityPercent: 0,
                changedCellCount: 0,
                concurrentSeekers: useDisconnectedFixture ? 6 : 3,
                queryDistanceWu: useDisconnectedFixture ? NavBenchmarkOptions.MaxQueryDistanceWu : 64,
                replanningRatePercent: 0);

            var report = NavBenchmark.Run(options, fixturePath, seed: 1, tickCount: 1);

            var accountedFor =
                report.SuccessfulAStarQuerySampleCount +
                report.ProbeOutcomeBreakdown.UnreachableQueryCount;

            Assert.Equal(report.AStarQuerySampleCount, accountedFor);
            Assert.Equal(
                report.ProbeOutcomeBreakdown.PathFoundQueryCount,
                report.SuccessfulAStarQuerySampleCount);
        }
        finally
        {
            if (useDisconnectedFixture)
            {
                File.Delete(fixturePath);
            }
        }
    }

    /// <summary>
    /// The floor task 83's brief requires named: the fraction of probe
    /// queries that find a path, at the "stress-connected" matrix row from
    /// <c>docs/plans/2026-08-07-sandata-scaffold.md</c>'s VERIFY section
    /// (density 10, changed cells 50, 32 seekers, 2,048-wu queries, 25
    /// percent replanning). Before task 83's fix, that exact row measured
    /// 8.1 percent found (1,293 of 16,011) because <c>ApplyChangedCells</c>
    /// redrew fresh random indices across the whole grid every tick and
    /// random-walked the map toward disconnection. After the fix this row
    /// measures roughly 93 to 94 percent found, stable across tick counts
    /// (verified by hand: 100% at 1 tick, 93.2% at 200, 93.6% at 1,000,
    /// 93.7% at 1,500 and 2,000). 90 percent sits comfortably above the old
    /// defect's 8.1 percent and comfortably below every measured post-fix
    /// value, so a regression back toward the random-walk defect trips this
    /// floor while ordinary run-to-run variance does not.
    /// </summary>
    private const double SuccessfulSearchFloorFraction = 0.90;

    /// <summary>
    /// Task 83's fixed changed-cell set: <c>ChooseChangedCells</c> draws
    /// <see cref="NavBenchmarkOptions.ChangedCellCount"/> indices once, from
    /// the same open-cell collection <c>PlaceSeekerPair</c> already draws
    /// seekers from, and <c>ApplyChangedCells</c> toggles that same fixed
    /// array on every tick. Toggling a fixed set an even number of times
    /// nets to zero change for every cell it contains (each cell's per-tick
    /// contribution is constant, so two applications of it cancel), so a
    /// run of even length must return <see cref="NavBenchmarkReport.FinalBlockedCellCount"/>
    /// to exactly the pre-loop, changed-cell-free baseline — the opposite of
    /// the old per-tick-fresh-random-draw defect, which drifted further from
    /// that baseline as tick count grew. A run of odd length differs from
    /// the baseline by whichever cells were drawn an odd number of times,
    /// proving the map actually moved to its second configuration rather
    /// than never changing at all.
    /// </summary>
    [Fact]
    public void ChangedCellRunOscillatesBetweenExactlyTwoConfigurationsAcross2000Ticks()
    {
        var fixturePath = NavBenchmark.ResolveFixturePath(null);
        var baselineOptions = NavBenchmarkOptions.Create(
            mapDensityPercent: 10,
            changedCellCount: 0,
            concurrentSeekers: 1,
            queryDistanceWu: 64,
            replanningRatePercent: 0);
        var churnOptions = NavBenchmarkOptions.Create(
            mapDensityPercent: 10,
            changedCellCount: 50,
            concurrentSeekers: 1,
            queryDistanceWu: 64,
            replanningRatePercent: 0);

        // changedCellCount is 0 here, so the tick loop never mutates
        // passability; tickCount is otherwise irrelevant to the resulting
        // blocked-cell count and is kept small only for speed.
        var baselineReport = NavBenchmark.Run(baselineOptions, fixturePath, seed: 1, tickCount: 1);
        var evenReport = NavBenchmark.Run(churnOptions, fixturePath, seed: 1, tickCount: 2000);
        var oddReport = NavBenchmark.Run(churnOptions, fixturePath, seed: 1, tickCount: 2001);
        var secondEvenReport = NavBenchmark.Run(churnOptions, fixturePath, seed: 1, tickCount: 2002);

        Assert.Equal(baselineReport.FinalBlockedCellCount, evenReport.FinalBlockedCellCount);
        Assert.Equal(baselineReport.FinalBlockedCellCount, secondEvenReport.FinalBlockedCellCount);
        Assert.NotEqual(baselineReport.FinalBlockedCellCount, oddReport.FinalBlockedCellCount);
    }

    /// <summary>
    /// Task 83's acceptance criterion on connectivity: the successful-search
    /// fraction of a changed-cell run inside the usable density range (0 to
    /// 20 percent per the density sweep in
    /// <c>docs/plans/2026-08-07-sandata-scaffold.md</c>) must stay above
    /// <see cref="SuccessfulSearchFloorFraction"/> for the whole run, not
    /// only at its end. Each sampled tick count below is a fresh, independent
    /// <see cref="NavBenchmark.Run"/> call with identical options and seed,
    /// standing in for "how connected is the map at this point in the run" —
    /// under the old per-tick-fresh-random-draw defect this fraction would
    /// fall as tick count grows, since every extra tick pushed the map
    /// further toward the roughly-half-blocked noise field the density sweep
    /// found disconnects the fixture. Under the fix the map only ever
    /// occupies the same two configurations, so the fraction should stay
    /// essentially flat across all three sampled points, including the full
    /// 2,000-tick run the brief requires.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    [InlineData(2000)]
    public void ChangedCellRunStaysAboveTheSuccessfulSearchFloorThroughoutTheRun(int tickCount)
    {
        var fixturePath = NavBenchmark.ResolveFixturePath(null);
        var options = NavBenchmarkOptions.Create(
            mapDensityPercent: 10,
            changedCellCount: 50,
            concurrentSeekers: 32,
            queryDistanceWu: 2048,
            replanningRatePercent: 25);

        var report = NavBenchmark.Run(options, fixturePath, seed: 1, tickCount);

        Assert.True(report.AStarQuerySampleCount > 0);
        var foundFraction =
            (double)report.ProbeOutcomeBreakdown.PathFoundQueryCount / report.AStarQuerySampleCount;

        Assert.True(
            foundFraction >= SuccessfulSearchFloorFraction,
            $"Found fraction {foundFraction:P1} at tickCount={tickCount} fell below the " +
            $"{SuccessfulSearchFloorFraction:P0} floor.");
    }
}
