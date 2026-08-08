using Sandata.Core.Navigation;

namespace Sandata.Headless;

/// <summary>
/// The five matrix parameters that
/// SIMULATION-GAME-STANDARDS.md section 11's future pathfinding
/// acceptance gate names for the navigation benchmark workload: map
/// density, changed-cell count, concurrent seekers, query distance, and
/// replanning rate. Every one is validated here, and every one is a
/// required constructor argument with no default — a benchmark run's
/// numbers are only comparable to another run's numbers when both runs
/// supplied the same five values, so this type is built to make silently
/// omitting one impossible rather than merely discouraged.
/// </summary>
/// <remarks>
/// <see cref="Create"/> is the only way to obtain an instance. It takes
/// all five values positionally, with no optional parameter and no
/// default, so a caller that forgets one gets a compiler error rather
/// than a benchmark that quietly ran with an unstated value. Range
/// validation happens here, once, so both the command-line entry point in
/// <c>Program.cs</c> and any direct test of this type share one source of
/// truth for what "in range" means.
/// </remarks>
public sealed class NavBenchmarkOptions
{
    /// <summary>
    /// Lower bound for <see cref="MapDensityPercent"/>: zero means the
    /// fixture's authored passability is used unmodified.
    /// </summary>
    public const int MinMapDensityPercent = 0;

    /// <summary>
    /// PROVISIONAL upper bound for <see cref="MapDensityPercent"/>. Not a
    /// measured limit — chosen so the densest requested map still keeps a
    /// large majority of the fixture's cells open, which keeps the seeker
    /// placement search in <see cref="NavBenchmark"/> able to find pairs of
    /// open cells at the requested query distance in a bounded number of
    /// attempts. A future task may re-derive this from an actual
    /// connectivity measurement instead of this placeholder.
    /// </summary>
    public const int MaxMapDensityPercent = 80;

    /// <summary>
    /// Lower bound for <see cref="ChangedCellCount"/>: zero means no
    /// per-tick passability churn.
    /// </summary>
    public const int MinChangedCellCount = 0;

    /// <summary>
    /// PROVISIONAL upper bound for <see cref="ChangedCellCount"/>. Not a
    /// measured limit — chosen well under the angle-house fixture's 28,800
    /// cells (160 by 180 at <see cref="NavGrid.CellSizeWu"/>) so a single
    /// tick's churn can never approach rewriting the whole map, which would
    /// make "changed-cell count" indistinguishable from "re-baked map".
    /// </summary>
    public const int MaxChangedCellCount = 2_000;

    /// <summary>
    /// Lower bound for <see cref="ConcurrentSeekers"/>: at least one seeker
    /// is required for the benchmark to produce any A* query samples at
    /// all.
    /// </summary>
    public const int MinConcurrentSeekers = 1;

    /// <summary>
    /// PROVISIONAL upper bound for <see cref="ConcurrentSeekers"/>. Not a
    /// measured limit — chosen as a round number well above any squad count
    /// design section 8 describes for v0.1, so the benchmark can still
    /// exercise a "many seekers" scenario without an unbounded run time.
    /// </summary>
    public const int MaxConcurrentSeekers = 256;

    /// <summary>
    /// Lower bound for <see cref="QueryDistanceWu"/>: one grid cell, the
    /// smallest distance a start and goal cell can meaningfully differ by.
    /// </summary>
    public const int MinQueryDistanceWu = NavGrid.CellSizeWu;

    /// <summary>
    /// PROVISIONAL upper bound for <see cref="QueryDistanceWu"/>. Not a
    /// measured limit — chosen larger than the diagonal of the largest grid
    /// <see cref="NavGrid"/> supports (512 by 512 cells at
    /// <see cref="NavGrid.CellSizeWu"/> world units per cell is a 2048 by
    /// 2048 world-unit square, whose diagonal is about 2,896 world units),
    /// so a legitimate distance for any supported map is never rejected by
    /// this range check. The seeker placement search still only
    /// approximates the requested distance on the fixture actually loaded.
    /// </summary>
    public const int MaxQueryDistanceWu = 4_096;

    /// <summary>
    /// Lower bound for <see cref="ReplanningRatePercent"/>: zero means a
    /// seeker never requests a new path once its first one is issued.
    /// </summary>
    public const int MinReplanningRatePercent = 0;

    /// <summary>
    /// Upper bound for <see cref="ReplanningRatePercent"/>: one hundred
    /// means every seeker requests a new path on every tick. This is a true
    /// percentage, not a provisional constant — 0 to 100 is exact by
    /// definition and needs no re-derivation.
    /// </summary>
    public const int MaxReplanningRatePercent = 100;

    /// <summary>
    /// The percentage of the fixture's originally open cells that are
    /// forced <see cref="Sandata.Core.Navigation.NavCellFlags.Blocked"/>
    /// before the benchmark's tick loop starts, simulating a denser map
    /// than the fixture was authored with.
    /// </summary>
    public int MapDensityPercent { get; }

    /// <summary>
    /// How many cells <see cref="NavBenchmark"/> toggles between
    /// <see cref="Sandata.Core.Navigation.NavCellFlags.Open"/> and
    /// <see cref="Sandata.Core.Navigation.NavCellFlags.Blocked"/> on every
    /// benchmark tick, simulating ongoing map change a live mission would
    /// see from doors, destructible cover, or similar.
    /// </summary>
    public int ChangedCellCount { get; }

    /// <summary>
    /// How many independent seeker groups the benchmark drives at once,
    /// each with its own start cell, goal cell, and <c>PathService</c>
    /// group identity.
    /// </summary>
    public int ConcurrentSeekers { get; }

    /// <summary>
    /// The straight-line world-unit distance a seeker's start and goal
    /// cells are placed to approximate. The seeker placement search picks
    /// the closest match available among the fixture's currently open
    /// cells; it is a target, not a guarantee.
    /// </summary>
    public int QueryDistanceWu { get; }

    /// <summary>
    /// The percentage chance, rolled independently per seeker per tick,
    /// that a seeker requests a fresh path to its existing goal — modelling
    /// how often a live mission's units would re-plan in response to
    /// changed terrain.
    /// </summary>
    public int ReplanningRatePercent { get; }

    private NavBenchmarkOptions(
        int mapDensityPercent,
        int changedCellCount,
        int concurrentSeekers,
        int queryDistanceWu,
        int replanningRatePercent)
    {
        MapDensityPercent = mapDensityPercent;
        ChangedCellCount = changedCellCount;
        ConcurrentSeekers = concurrentSeekers;
        QueryDistanceWu = queryDistanceWu;
        ReplanningRatePercent = replanningRatePercent;
    }

    /// <summary>
    /// Validates and builds one immutable set of navigation benchmark
    /// matrix parameters. Every parameter is required and is checked
    /// against its own named range; an out-of-range value throws
    /// <see cref="ArgumentOutOfRangeException"/> naming that parameter,
    /// rather than being clamped or silently accepted.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any one parameter falls outside its documented range.
    /// </exception>
    public static NavBenchmarkOptions Create(
        int mapDensityPercent,
        int changedCellCount,
        int concurrentSeekers,
        int queryDistanceWu,
        int replanningRatePercent)
    {
        if (mapDensityPercent < MinMapDensityPercent || mapDensityPercent > MaxMapDensityPercent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mapDensityPercent),
                mapDensityPercent,
                $"Map density must be between {MinMapDensityPercent} and {MaxMapDensityPercent} percent.");
        }

        if (changedCellCount < MinChangedCellCount || changedCellCount > MaxChangedCellCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(changedCellCount),
                changedCellCount,
                $"Changed cell count must be between {MinChangedCellCount} and {MaxChangedCellCount}.");
        }

        if (concurrentSeekers < MinConcurrentSeekers || concurrentSeekers > MaxConcurrentSeekers)
        {
            throw new ArgumentOutOfRangeException(
                nameof(concurrentSeekers),
                concurrentSeekers,
                $"Concurrent seekers must be between {MinConcurrentSeekers} and {MaxConcurrentSeekers}.");
        }

        if (queryDistanceWu < MinQueryDistanceWu || queryDistanceWu > MaxQueryDistanceWu)
        {
            throw new ArgumentOutOfRangeException(
                nameof(queryDistanceWu),
                queryDistanceWu,
                $"Query distance must be between {MinQueryDistanceWu} and {MaxQueryDistanceWu} world units.");
        }

        if (replanningRatePercent < MinReplanningRatePercent || replanningRatePercent > MaxReplanningRatePercent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(replanningRatePercent),
                replanningRatePercent,
                $"Replanning rate must be between {MinReplanningRatePercent} and {MaxReplanningRatePercent} percent.");
        }

        return new NavBenchmarkOptions(
            mapDensityPercent,
            changedCellCount,
            concurrentSeekers,
            queryDistanceWu,
            replanningRatePercent);
    }
}
