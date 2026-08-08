using System.Diagnostics;
using Hukbo.Core.Determinism;
using Hukbo.Diagnostics;
using Sandata.Core.Maps;
using Sandata.Core.Navigation;
using Sandata.Core.Rules;

namespace Sandata.Headless;

/// <summary>
/// Three <c>p50</c>/<c>p95</c>/<c>p99</c> millisecond values drawn from one
/// sorted sample set, computed the same way
/// <c>Hukbo.Headless.RunReport.TickPercentiles</c> computes its own: the
/// nearest-rank method, <c>rank = ceiling(percentile * count) - 1</c>,
/// clamped into range.
/// </summary>
public readonly record struct NavBenchmarkPercentiles(
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds);

/// <summary>
/// The full result of one navigation benchmark run: the matrix parameters
/// and operational settings the run used, and the six percentiles plan
/// task 50 requires — <c>p50</c>/<c>p95</c>/<c>p99</c> for a standalone
/// <see cref="NavSearch.TryFindPath"/> query and for one
/// <see cref="PathService.Advance"/> tick-stage call, six numbers in total
/// across the two <see cref="NavBenchmarkPercentiles"/> fields below.
/// </summary>
public sealed record NavBenchmarkReport(
    int MapDensityPercent,
    int ChangedCellCount,
    int ConcurrentSeekers,
    int QueryDistanceWu,
    int ReplanningRatePercent,
    ulong Seed,
    int TickCount,
    string FixturePath,
    int GridWidthCells,
    int GridHeightCells,
    long AStarQuerySampleCount,
    NavBenchmarkPercentiles AStarQueryPercentiles,
    long TickStageSampleCount,
    NavBenchmarkPercentiles TickStagePercentiles);

/// <summary>
/// Runs the navigation benchmark workload SIMULATION-GAME-STANDARDS.md
/// section 11 requires: a fixed matrix of map density, changed-cell count,
/// concurrent seekers, query distance, and replanning rate, driven against
/// the <c>angle-house</c> fixture, timing a standalone A* query and one
/// <see cref="PathService.Advance"/> tick-stage call separately.
/// </summary>
/// <remarks>
/// <b>The wall clock is used here and only here.</b> <see cref="Run"/>
/// measures elapsed time around each timed call with
/// <see cref="Stopwatch"/>, but the measured elapsed time never feeds back
/// into a search, a seed, a hash, or any decision the benchmark makes — it
/// is recorded into the sample lists and nothing else reads it until the
/// run is over and percentiles are computed. Every decision the benchmark
/// itself makes — which cells to perturb, where to place a seeker, whether
/// a seeker re-plans on a given tick — is driven by <see cref="SplitMix64"/>
/// seeded from the caller-supplied <paramref name="seed"/> parameter of
/// <see cref="Run"/>, never by <see cref="System.Random"/> and never by the
/// clock.
/// </remarks>
public static class NavBenchmark
{
    /// <summary>
    /// PROVISIONAL: the mover body radius, in whole world units, baked into
    /// the fixture's nav grid before the benchmark runs. Not a measured or
    /// historical value — matches the constant
    /// <c>tests/Sandata.Core.Tests/NavBakeTests.cs</c> already uses for the
    /// same fixture, so the baked passability this benchmark searches over
    /// matches what that test suite already exercises.
    /// </summary>
    internal const int BenchmarkBodyRadiusWu = 5;

    private static readonly string[] FixtureRelativePathSegments =
    [
        "tests", "Sandata.Core.Tests", "Fixtures", "angle-house.hkmap",
    ];

    /// <summary>
    /// Locates the <c>angle-house.hkmap</c> fixture. When
    /// <paramref name="explicitPath"/> is supplied it is used verbatim
    /// (validated to exist); otherwise the fixture is located relative to
    /// the repository root discovered from <see cref="AppContext.BaseDirectory"/>
    /// by <see cref="LogPaths.FindRepositoryRoot"/> — the same mechanism
    /// <c>Sandata.Core.Tests.SandataSourceHygieneTests</c> already uses to
    /// find its own repository root. This only succeeds for a local build
    /// whose output directory still lives under a source checkout; a
    /// packaged, published build has no <c>Hukbo.slnx</c> ancestor to find
    /// and must supply <paramref name="explicitPath"/>.
    /// </summary>
    /// <exception cref="FileNotFoundException">
    /// No fixture could be located, either because
    /// <paramref name="explicitPath"/> does not exist or because no
    /// repository root was found and none was supplied.
    /// </exception>
    public static string ResolveFixturePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (!File.Exists(explicitPath))
            {
                throw new FileNotFoundException(
                    $"The navigation benchmark fixture was not found at '{explicitPath}'.",
                    explicitPath);
            }

            return explicitPath;
        }

        var repositoryRoot = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        if (repositoryRoot is null)
        {
            throw new FileNotFoundException(
                "The navigation benchmark could not find a repository root above " +
                $"'{AppContext.BaseDirectory}' to locate the angle-house fixture. " +
                "This is expected for a packaged build; pass --nav-fixture-path explicitly.");
        }

        var candidate = Path.Combine([repositoryRoot, .. FixtureRelativePathSegments]);
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException(
                $"The navigation benchmark fixture was not found at '{candidate}', " +
                $"discovered from repository root '{repositoryRoot}'.",
                candidate);
        }

        return candidate;
    }

    /// <summary>
    /// Runs the full navigation benchmark workload against the map loaded
    /// from <paramref name="fixturePath"/> and returns the resulting
    /// report. See this type's remarks for the determinism and
    /// wall-clock-isolation contract every call obeys.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tickCount"/> is not strictly positive.</exception>
    /// <exception cref="InvalidOperationException">
    /// The loaded map's <c>GRID</c> cell size does not match
    /// <see cref="NavGrid.CellSizeWu"/>, or it has fewer than two open
    /// cells once map density is applied.
    /// </exception>
    public static NavBenchmarkReport Run(
        NavBenchmarkOptions options,
        string fixturePath,
        ulong seed,
        int tickCount)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixturePath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tickCount, 0);

        var mapText = File.ReadAllText(fixturePath);
        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(mapText));
        MapValidator.Validate(canonical);

        var gridRecord = canonical.OfType<GridRecord>().Single();
        if (gridRecord.CellWu != NavGrid.CellSizeWu)
        {
            throw new InvalidOperationException(
                $"The fixture's GRID cell size ({gridRecord.CellWu} wu) does not match " +
                $"NavGrid.CellSizeWu ({NavGrid.CellSizeWu} wu); this benchmark cannot bake it.");
        }

        var widthCells = gridRecord.WidthWu / gridRecord.CellWu;
        var heightCells = gridRecord.HeightWu / gridRecord.CellWu;
        var grid = new NavGrid(widthCells, heightCells);

        var walls = canonical.OfType<WallRecord>().ToArray();
        var doors = canonical.OfType<DoorRecord>().ToArray();
        NavBake.Bake(grid, walls, doors, BenchmarkBodyRadiusWu);

        var rng = new SplitMix64(seed);

        ApplyMapDensity(grid, options.MapDensityPercent, ref rng);

        var wallBuckets = BuildWallBuckets(grid, walls);

        var blocked = new bool[grid.CellCount];
        RefreshBlocked(grid, blocked);

        var openCells = CollectOpenCells(grid);
        if (openCells.Count < 2)
        {
            throw new InvalidOperationException(
                "Fewer than two open cells remain after applying map density; " +
                "the navigation benchmark cannot place any seeker.");
        }

        var seekers = new (ulong GroupId, int Start, int Goal)[options.ConcurrentSeekers];
        for (var index = 0; index < seekers.Length; index++)
        {
            var (start, goal) = PlaceSeekerPair(grid, openCells, options.QueryDistanceWu, ref rng);
            seekers[index] = ((ulong)(index + 1), start, goal);
        }

        var pathService = new PathService(SandataRuleset.ModernTacticalV1.PathLatencyTicks);
        var probeSearch = new NavSearch();
        var scratchPath = new List<int>();
        var scratchExpanded = new List<int>();

        var aStarSamples = new List<double>();
        var tickStageSamples = new List<double>(tickCount);

        foreach (var seeker in seekers)
        {
            pathService.RequestPath(seeker.GroupId, seeker.Start, seeker.Goal, requestTick: 0);
            aStarSamples.Add(TimeProbeQuery(probeSearch, grid, seeker.Start, seeker.Goal, blocked, scratchPath, scratchExpanded));
        }

        for (var tick = 0; tick < tickCount; tick++)
        {
            if (options.ChangedCellCount > 0)
            {
                ApplyChangedCells(grid, options.ChangedCellCount, ref rng);
                RefreshBlocked(grid, blocked);
            }

            foreach (var seeker in seekers)
            {
                if (rng.NextInt(100) >= options.ReplanningRatePercent)
                {
                    continue;
                }

                pathService.RequestPath(seeker.GroupId, seeker.Start, seeker.Goal, tick);
                aStarSamples.Add(TimeProbeQuery(probeSearch, grid, seeker.Start, seeker.Goal, blocked, scratchPath, scratchExpanded));
            }

            var stageStart = Stopwatch.GetTimestamp();
            pathService.Advance(tick, grid, blocked, wallBuckets);
            tickStageSamples.Add(Stopwatch.GetElapsedTime(stageStart).TotalMilliseconds);
        }

        var sortedAStar = aStarSamples.Order().ToArray();
        var sortedTickStage = tickStageSamples.Order().ToArray();

        return new NavBenchmarkReport(
            options.MapDensityPercent,
            options.ChangedCellCount,
            options.ConcurrentSeekers,
            options.QueryDistanceWu,
            options.ReplanningRatePercent,
            seed,
            tickCount,
            fixturePath,
            grid.Width,
            grid.Height,
            sortedAStar.Length,
            new NavBenchmarkPercentiles(
                Percentile(sortedAStar, 0.50),
                Percentile(sortedAStar, 0.95),
                Percentile(sortedAStar, 0.99)),
            sortedTickStage.Length,
            new NavBenchmarkPercentiles(
                Percentile(sortedTickStage, 0.50),
                Percentile(sortedTickStage, 0.95),
                Percentile(sortedTickStage, 0.99)));
    }

    private static double TimeProbeQuery(
        NavSearch search,
        NavGrid grid,
        int startCellIndex,
        int goalCellIndex,
        bool[] blocked,
        List<int> scratchPath,
        List<int> scratchExpanded)
    {
        var start = Stopwatch.GetTimestamp();
        search.TryFindPath(grid, startCellIndex, goalCellIndex, blocked, scratchPath, scratchExpanded);
        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    /// <summary>
    /// Forces <paramref name="mapDensityPercent"/> percent of
    /// <paramref name="grid"/>'s currently open cells to
    /// <see cref="NavCellFlags.Blocked"/>, chosen independently per cell by
    /// <paramref name="rng"/>, simulating a denser map than the fixture was
    /// authored with. Runs once, before the tick loop, on top of the
    /// fixture's own baked passability.
    /// </summary>
    private static void ApplyMapDensity(NavGrid grid, int mapDensityPercent, ref SplitMix64 rng)
    {
        if (mapDensityPercent <= 0)
        {
            return;
        }

        var passability = grid.Passability;
        for (var cellIndex = 0; cellIndex < passability.Length; cellIndex++)
        {
            if (passability[cellIndex] != NavCellFlags.Open)
            {
                continue;
            }

            if (rng.NextInt(100) < mapDensityPercent)
            {
                passability[cellIndex] = NavCellFlags.Blocked;
            }
        }
    }

    /// <summary>
    /// Toggles <paramref name="changedCellCount"/> randomly chosen cells
    /// between <see cref="NavCellFlags.Open"/> and
    /// <see cref="NavCellFlags.Blocked"/>, run once per benchmark tick to
    /// simulate ongoing map change. A door cell is left untouched — its
    /// passability is governed by door state, not by this synthetic churn.
    /// The same cell may be chosen, and toggled back, more than once within
    /// one call; that is an accepted property of independent per-pick
    /// sampling, not a bug to work around.
    /// </summary>
    private static void ApplyChangedCells(NavGrid grid, int changedCellCount, ref SplitMix64 rng)
    {
        var passability = grid.Passability;
        for (var i = 0; i < changedCellCount; i++)
        {
            var cellIndex = rng.NextInt(grid.CellCount);
            var current = passability[cellIndex];
            if (current == NavCellFlags.Door)
            {
                continue;
            }

            passability[cellIndex] = current == NavCellFlags.Open
                ? NavCellFlags.Blocked
                : NavCellFlags.Open;
        }
    }

    /// <summary>
    /// Rewrites <paramref name="blocked"/> from <paramref name="grid"/>'s
    /// current passability: <see langword="true"/> for
    /// <see cref="NavCellFlags.Blocked"/>, <see langword="false"/> for
    /// <see cref="NavCellFlags.Open"/> and for <see cref="NavCellFlags.Door"/>
    /// — a closed door is passable to the planner, per
    /// <see cref="NavCellFlags.Door"/>'s own contract.
    /// </summary>
    private static void RefreshBlocked(NavGrid grid, bool[] blocked)
    {
        var passability = grid.Passability;
        for (var i = 0; i < passability.Length; i++)
        {
            blocked[i] = passability[i] == NavCellFlags.Blocked;
        }
    }

    private static List<int> CollectOpenCells(NavGrid grid)
    {
        var open = new List<int>();
        var passability = grid.Passability;
        for (var i = 0; i < passability.Length; i++)
        {
            if (passability[i] == NavCellFlags.Open)
            {
                open.Add(i);
            }
        }

        return open;
    }

    /// <summary>
    /// Picks a start cell uniformly at random from <paramref name="openCells"/>,
    /// then picks the goal cell — from a bounded number of further random
    /// draws from the same list — whose straight-line world-unit distance
    /// from the start cell comes closest to <paramref name="queryDistanceWu"/>.
    /// This is a best-effort approximation, not an exact placement: on a
    /// small or sparse open-cell set the closest available pair may sit far
    /// from the requested distance, which is why <see cref="NavBenchmarkOptions.QueryDistanceWu"/>'s
    /// own documentation calls it a target rather than a guarantee.
    /// </summary>
    private static (int Start, int Goal) PlaceSeekerPair(
        NavGrid grid, List<int> openCells, int queryDistanceWu, ref SplitMix64 rng)
    {
        const int candidateAttempts = 64;

        var startCellIndex = openCells[rng.NextInt(openCells.Count)];
        var startX = grid.CellX(startCellIndex);
        var startY = grid.CellY(startCellIndex);

        var bestGoalCellIndex = startCellIndex;
        var bestDistanceError = double.MaxValue;

        for (var attempt = 0; attempt < candidateAttempts; attempt++)
        {
            var candidateCellIndex = openCells[rng.NextInt(openCells.Count)];
            if (candidateCellIndex == startCellIndex)
            {
                continue;
            }

            var deltaXWu = (grid.CellX(candidateCellIndex) - startX) * (double)NavGrid.CellSizeWu;
            var deltaYWu = (grid.CellY(candidateCellIndex) - startY) * (double)NavGrid.CellSizeWu;
            var distanceWu = Math.Sqrt((deltaXWu * deltaXWu) + (deltaYWu * deltaYWu));
            var distanceError = Math.Abs(distanceWu - queryDistanceWu);

            if (distanceError < bestDistanceError)
            {
                bestDistanceError = distanceError;
                bestGoalCellIndex = candidateCellIndex;
            }
        }

        return (startCellIndex, bestGoalCellIndex);
    }

    /// <summary>
    /// Marshals <paramref name="walls"/>' four coordinate fields into the flat
    /// <see langword="long"/> arrays <see cref="WallBuckets.Build"/> requires,
    /// verbatim — a perimeter wall's endpoint sitting exactly on the map's
    /// far edge (for example angle-house's <c>WALL 0 720 640 720 1</c>, whose
    /// Y coordinate equals the fixture's own <c>HeightWu</c>) no longer needs
    /// any local adjustment here: <see cref="WallBuckets.Build"/> itself now
    /// clamps only the coordinates it feeds its broad-phase traversal, and
    /// stores every segment's true, unclamped endpoint for the narrow phase.
    /// Matches <c>Sandata.Client.SandataGame.BuildWallBuckets</c>'s own
    /// unadorned marshaling for the identical call.
    /// </summary>
    private static WallBuckets BuildWallBuckets(NavGrid grid, IReadOnlyList<WallRecord> walls)
    {
        var segmentAX = new long[walls.Count];
        var segmentAY = new long[walls.Count];
        var segmentBX = new long[walls.Count];
        var segmentBY = new long[walls.Count];

        for (var i = 0; i < walls.Count; i++)
        {
            segmentAX[i] = walls[i].X1;
            segmentAY[i] = walls[i].Y1;
            segmentBX[i] = walls[i].X2;
            segmentBY[i] = walls[i].Y2;
        }

        return WallBuckets.Build(grid, segmentAX, segmentAY, segmentBX, segmentBY);
    }

    /// <summary>
    /// The nearest-rank percentile of <paramref name="sortedValues"/> at
    /// <paramref name="percentile"/> (0 to 1 exclusive-of-neither), matching
    /// <c>Hukbo.Headless.RunReport</c>'s own percentile convention:
    /// <c>rank = ceiling(percentile * count) - 1</c>, clamped to
    /// <c>[0, count - 1]</c>. Returns <c>0</c> for an empty sample set.
    /// <see langword="internal"/> rather than <see langword="private"/> so
    /// <c>Sandata.Core.Tests</c> — granted access by this project's
    /// <c>InternalsVisibleTo</c> — can test its edge cases directly.
    /// </summary>
    internal static double Percentile(double[] sortedValues, double percentile)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        rank = Math.Clamp(rank, 0, sortedValues.Length - 1);
        return sortedValues[rank];
    }
}
