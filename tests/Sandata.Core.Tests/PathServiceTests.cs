using Sandata.Core.Navigation;

namespace Sandata.Core.Tests;

/// <summary>
/// Plan task 27's "Done when": a path becomes valid on exactly
/// <c>requestTick + PathLatencyTicks</c> whether one or eight searches ran
/// that tick; a group with an outstanding request does not enqueue a second;
/// a group with no path holds and reports a reason code; and recomputing
/// every published path from its stored request reproduces the identical
/// polyline.
/// </summary>
public sealed class PathServiceTests
{
    [Fact]
    public void PublishedPath_BecomesValidAtExactlyRequestTickPlusLatency_RegardlessOfHowManySearchesShareTheTick()
    {
        // A single group's own timing, in its own service instance.
        var (soloGrid, soloBlocked, start, goal) = BuildGrid(
        [
            "S....",
            ".....",
            ".....",
            ".....",
            "....G",
        ]);
        const long requestTick = 10;
        const int latency = 3;
        var soloWallBuckets = WallBuckets.Build(soloGrid, [], [], [], []);

        var soloService = new PathService(latency);
        soloService.RequestPath(groupId: 1, start, goal, requestTick);

        for (var tick = requestTick; tick < requestTick + latency; tick++)
        {
            soloService.Advance(tick, soloGrid, soloBlocked, soloWallBuckets);
            Assert.True(soloService.GetCurrentPath(1).IsEmpty, $"tick {tick} must not publish yet");
            Assert.Equal(PathReasonCode.AwaitingLatency, soloService.GetReasonCode(1));
        }

        soloService.Advance(requestTick + latency, soloGrid, soloBlocked, soloWallBuckets);
        var soloPath = soloService.GetCurrentPath(1);
        Assert.False(soloPath.IsEmpty);
        Assert.Equal(PathReasonCode.PathValid, soloService.GetReasonCode(1));

        // Eight groups sharing one service and one tick: every one of them
        // gets searched the same tick, and every one of them must still
        // publish at exactly the same tick as the solo case above — never
        // earlier because a search happened to finish fast, never later
        // because seven other groups also wanted a search that tick. There
        // is no per-tick search budget to throttle them apart.
        var (crowdedGrid, crowdedBlocked, _, _) = BuildGrid(
        [
            "S....",
            ".....",
            ".....",
            ".....",
            "....G",
        ]);
        var crowdedWallBuckets = WallBuckets.Build(crowdedGrid, [], [], [], []);
        var crowdedService = new PathService(latency);
        for (ulong groupId = 1; groupId <= 8; groupId++)
        {
            crowdedService.RequestPath(groupId, start, goal, requestTick);
        }

        for (var tick = requestTick; tick < requestTick + latency; tick++)
        {
            crowdedService.Advance(tick, crowdedGrid, crowdedBlocked, crowdedWallBuckets);
            for (ulong groupId = 1; groupId <= 8; groupId++)
            {
                Assert.True(crowdedService.GetCurrentPath(groupId).IsEmpty, $"group {groupId} at tick {tick} must not publish yet");
            }
        }

        crowdedService.Advance(requestTick + latency, crowdedGrid, crowdedBlocked, crowdedWallBuckets);
        for (ulong groupId = 1; groupId <= 8; groupId++)
        {
            var path = crowdedService.GetCurrentPath(groupId);
            Assert.False(path.IsEmpty);
            Assert.Equal(PathReasonCode.PathValid, crowdedService.GetReasonCode(groupId));
            Assert.Equal(soloPath.AsSpan().ToArray(), path.AsSpan().ToArray());
        }
    }

    [Fact]
    public void RequestPath_WithAnOutstandingRequest_DoesNotEnqueueASecond()
    {
        var (grid, blocked, start, firstGoal) = BuildGrid(
        [
            "S....",
            ".....",
            ".....",
            ".....",
            "....G",
        ]);
        var secondGoal = grid.CellIndex(4, 0);
        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);

        var service = new PathService(pathLatencyTicks: 5);
        service.RequestPath(groupId: 1, start, goalCellIndex: firstGoal, requestTick: 0);
        service.RequestPath(groupId: 1, start, goalCellIndex: secondGoal, requestTick: 1);

        Assert.True(service.TryGetRequest(1, out var storedRequest));
        Assert.Equal(new PathRequest(1, start, firstGoal, 0), storedRequest);

        // Advancing past both the ignored request's and the accepted
        // request's would-be publish tick must still resolve toward the
        // first goal, never the second. Asserted against the raw corridor,
        // not GetCurrentPath: task 65 changed GetCurrentPath's element type
        // from a cell index to a smoothed PathPoint, so "does this path end
        // at the first goal's cell" is now a GetCurrentCorridor question.
        service.Advance(5, grid, blocked, wallBuckets);
        Assert.Equal(PathReasonCode.PathValid, service.GetReasonCode(1));
        var corridor = service.GetCurrentCorridor(1);
        Assert.Equal(firstGoal, corridor[^1]);
    }

    [Fact]
    public void GroupWithNoReachablePath_HoldsWithAnUnreachableReasonCode_AndNeverPublishesAPath()
    {
        var (grid, blocked, start, goal) = BuildGrid(
        [
            "S....",
            ".....",
            "..###",
            "..#G#",
            "..###",
        ]);

        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);
        var service = new PathService(pathLatencyTicks: 2);

        // Before any request: nothing to hold a reason against at all.
        Assert.Equal(PathReasonCode.NoDestinationRequested, service.GetReasonCode(42));

        service.RequestPath(groupId: 1, start, goal, requestTick: 0);
        service.Advance(0, grid, blocked, wallBuckets);
        Assert.Equal(PathReasonCode.AwaitingLatency, service.GetReasonCode(1));
        Assert.True(service.GetCurrentPath(1).IsEmpty);

        service.Advance(1, grid, blocked, wallBuckets);
        service.Advance(2, grid, blocked, wallBuckets);

        Assert.True(service.GetCurrentPath(1).IsEmpty);
        Assert.Equal(PathReasonCode.Unreachable, service.GetReasonCode(1));
        Assert.False(service.HasOutstandingRequest(1));
    }

    [Fact]
    public void ResumingFromAStoredRequestAlone_ReproducesTheIdenticalPublishedPolyline()
    {
        var (grid, blocked, start, goal) = BuildGrid(
        [
            "S.....",
            ".####.",
            ".#..#.",
            ".#.##.",
            ".#....",
            "....#G",
        ]);

        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);
        var original = new PathService(pathLatencyTicks: 4);
        original.RequestPath(groupId: 7, start, goal, requestTick: 100);
        for (var tick = 100L; tick <= 104; tick++)
        {
            original.Advance(tick, grid, blocked, wallBuckets);
        }

        var originalPath = original.GetCurrentPath(7);
        Assert.False(originalPath.IsEmpty);
        Assert.True(original.TryGetRequest(7, out var storedRequest));
        Assert.Equal(new PathRequest(7, start, goal, 100), storedRequest);

        // A resume knows only the stored request tuple — never the polyline
        // that was published from it, and never the position the group has
        // since moved to. A brand new service, fed only that tuple, must
        // reconstruct the same path once its latency has elapsed.
        var resumed = new PathService(pathLatencyTicks: 4);
        resumed.RequestPath(storedRequest.GroupId, storedRequest.StartCellIndex, storedRequest.GoalCellIndex, storedRequest.RequestTick);
        resumed.Advance(storedRequest.RequestTick + 4, grid, blocked, wallBuckets);

        // The core task 65 assertion: recomputing from the stored request
        // alone reproduces the identical smoothed polyline, not merely an
        // equivalent corridor.
        Assert.Equal(originalPath.AsSpan().ToArray(), resumed.GetCurrentPath(7).AsSpan().ToArray());

        // And an entirely independent, direct call to the search this
        // service is built on, driven from nothing but the same stored
        // request tuple, must agree with the raw corridor PathService
        // smoothed — GetCurrentPath itself no longer holds cell indices to
        // compare a direct NavSearch call against, so this half moves to
        // GetCurrentCorridor.
        var directPath = new List<int>();
        var directOutcome = new NavSearch().TryFindPath(
            grid, storedRequest.StartCellIndex, storedRequest.GoalCellIndex, blocked, directPath, []);

        Assert.Equal(NavSearchOutcome.PathFound, directOutcome);
        Assert.Equal(original.GetCurrentCorridor(7).AsSpan().ToArray(), directPath);
    }

    /// <summary>
    /// Plan task 65's restated acceptance criterion (Sandata's scaffold
    /// plan, "What task 26 proved about the funnel, and what that means for task
    /// 65"): a single-file, one-cell-wide corridor cannot generally collapse
    /// to a straight segment, so this fixture is a fully open 10-by-4-cell
    /// region with no walls at all — every one of its 40 cells is passable —
    /// crossing a start-to-goal offset of (9, 3) cells, the same 3:1,
    /// 18.4-degree slope <c>FunnelTests</c> pins. A* is free to use diagonal
    /// steps everywhere in this region, and still returns a corridor whose
    /// tail is a run of due-east, axis-aligned single-cell steps (grid
    /// movement is discrete; a 3:1 slope is not a pure diagonal). The
    /// published path must be materially straighter than that corridor: it
    /// asserts a vertex count strictly lower than the corridor's cell count,
    /// and it asserts that none of the published polyline's own segments is
    /// axis-aligned, even though the corridor it came from holds several.
    /// </summary>
    [Fact]
    public void PublishedPath_AcrossAnOpenRegionWiderThanOneCell_IsMaterallyStraighterThanItsCorridor()
    {
        var (grid, blocked, start, goal) = BuildGrid(
        [
            "S.........",
            "..........",
            "..........",
            ".........G",
        ]);

        var wallBuckets = WallBuckets.Build(grid, [], [], [], []);
        var service = new PathService(pathLatencyTicks: 1);
        service.RequestPath(groupId: 1, start, goal, requestTick: 0);
        service.Advance(0, grid, blocked, wallBuckets);
        service.Advance(1, grid, blocked, wallBuckets);

        var corridor = service.GetCurrentCorridor(1);
        var path = service.GetCurrentPath(1);

        Assert.False(corridor.IsEmpty);
        Assert.False(path.IsEmpty);

        // The corridor itself is a real staircase: it holds at least one
        // axis-aligned step, confirming the fixture actually exercises the
        // defect the funnel exists to fix rather than starting from an
        // already-straight input.
        var corridorHasAnAxisAlignedStep = false;
        for (var i = 1; i < corridor.Length; i++)
        {
            var deltaX = grid.CellX(corridor[i]) - grid.CellX(corridor[i - 1]);
            var deltaY = grid.CellY(corridor[i]) - grid.CellY(corridor[i - 1]);
            if (deltaX == 0 || deltaY == 0)
            {
                corridorHasAnAxisAlignedStep = true;
                break;
            }
        }

        Assert.True(corridorHasAnAxisAlignedStep, "Fixture must produce a corridor with at least one axis-aligned step.");

        // The published polyline is materially straighter: fewer vertices
        // than the corridor had cells, ...
        Assert.True(
            path.Length < corridor.Length,
            $"Published path ({path.Length} vertices) must hold fewer points than its corridor ({corridor.Length} cells).");

        // ... and none of its own segments is the axis-aligned step the
        // corridor had — the exact defect design section 7 introduced the
        // funnel to prevent.
        for (var i = 1; i < path.Length; i++)
        {
            var deltaX = path[i].X - path[i - 1].X;
            var deltaY = path[i].Y - path[i - 1].Y;
            Assert.False(
                deltaX == 0 || deltaY == 0,
                $"Published path segment {i - 1}->{i} ({path[i - 1]} -> {path[i]}) must not be axis-aligned.");
        }
    }

    [Fact]
    public void Constructor_RejectsANegativeLatency()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PathService(pathLatencyTicks: -1));
    }

    [Fact]
    public void RequestPath_RejectsANegativeRequestTick()
    {
        var service = new PathService(pathLatencyTicks: 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.RequestPath(1, 0, 0, requestTick: -1));
    }

    private static (NavGrid Grid, bool[] Blocked, int Start, int Goal) BuildGrid(string[] rows)
    {
        var height = rows.Length;
        var width = rows[0].Length;
        var grid = new NavGrid(width, height);
        var blocked = new bool[grid.CellCount];
        var start = -1;
        var goal = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var symbol = rows[y][x];
                var cellIndex = grid.CellIndex(x, y);
                blocked[cellIndex] = symbol == '#';

                if (symbol == 'S')
                {
                    start = cellIndex;
                }

                if (symbol == 'G')
                {
                    goal = cellIndex;
                }
            }
        }

        if (start < 0 || goal < 0)
        {
            throw new ArgumentException("Every fixture row set must mark exactly one 'S' and one 'G'.");
        }

        return (grid, blocked, start, goal);
    }
}
