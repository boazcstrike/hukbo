using System.Collections.Immutable;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Combat;
using Sandata.Core.Maps;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;

namespace Sandata.Core.Tests;

/// <summary>
/// Smoke row SD-2's core-side half: <see cref="Sandata.Client"/> cannot draw
/// an autonomous group's path because <see cref="SandataSimulation"/> exposed
/// no accessor onto its private <c>_pathService</c> field at all —
/// <see cref="TickPipelineTests"/>'s own
/// <c>RunTick_OutstandingGroupPathRequest_PublishesAtExactlyRequestTickPlusLatencyAndIsNotReissued</c>
/// documents that gap explicitly in its remarks. This file proves the two new
/// read-only accessors that close it,
/// <see cref="SandataSimulation.GetPublishedPath"/> and
/// <see cref="SandataSimulation.GetPublishedPathReasonCode"/>: they return
/// nothing before publication, they return nothing for an unknown group, and
/// once a path is published, the returned polyline's endpoints agree with
/// grid geometry computed independently of <see cref="PathService"/>'s own
/// smoothing pass — not merely with what <see cref="PathService.GetCurrentPath"/>
/// itself already says, which would prove nothing beyond "the delegation
/// compiles."
/// </summary>
public sealed class PublishedPathAccessTests
{
    // ---- Shared fixture builders, matching TickPipelineTests' own shapes. ----

    private static Mission BuildMission(ulong seed = 1UL) => new(
        formatVersion: Mission.CurrentFormatVersion,
        seed: seed,
        mapContentHash: 1UL,
        tickPolicy: new MissionTickPolicy(TickLimit: 10_000, StateHashCadenceTicks: 1),
        factionSetups: ImmutableArray.Create(
            new MissionFactionSetup(FactionId: 0, OperatorCount: 1),
            new MissionFactionSetup(FactionId: 1, OperatorCount: 1)),
        rulesetId: SandataPresetId.ModernTacticalV1);

    private static OperatorState BuildOperator(int entityId, int faction, int positionXWu, int positionYWu) => new(
        EntityId: (ulong)entityId,
        PositionX: FixedPoint.FromWhole(positionXWu),
        PositionY: FixedPoint.FromWhole(positionYWu),
        Facing: Facing16.East,
        AimAngle: Bam16.FromFacing16(Facing16.East),
        Health: 100,
        Faction: faction,
        Intent: 0,
        IsCrouched: false,
        WeaponLowered: false,
        WeaponChainPhase: 0,
        WeaponChainRemainingTicks: 0,
        MagazineRounds: 30,
        CyclicFireAccumulator: 0,
        SuppressionCounter: 0);

    private static MissionState BuildState(ImmutableArray<GroupPathState> groups) => new(
        Tick: 0, Phase: 1, Winner: -1, NextEntityId: 2, NextEventSequence: 0)
    {
        Operators = ImmutableArray.Create(BuildOperator(1, faction: 0, positionXWu: 0, positionYWu: 0)),
        FactionAlerts = ImmutableArray.Create(new FactionAlertState(0, 0), new FactionAlertState(1, 0)),
        Doors = ImmutableArray<DoorState>.Empty,
        Groups = groups,
        RngStreams = ImmutableArray<RngStreamState>.Empty,
    };

    private static NavGrid BuildOpenGrid(int width = 10, int height = 10)
    {
        var grid = new NavGrid(width: width, height: height);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        return grid;
    }

    private static WallBuckets NoWalls(NavGrid grid) => WallBuckets.Build(grid, [], [], [], []);

    private const int PathLatencyTicks = 3;

    private static SandataRuleset BuildRuleset(int pathLatencyTicks = PathLatencyTicks) => new(
        tickRate: 50,
        msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
        pathLatencyTicks: pathLatencyTicks,
        groupCohesionRadiusWu: 96,
        loweredWallDistanceWu: 24,
        aimToleranceBam: 1024);

    // ---- Behaviour before publication and for an unknown group. ----------

    [Fact]
    public void GetPublishedPath_ForAGroupIdThatNeverRequested_ReturnsEmptyRatherThanThrowing()
    {
        var grid = BuildOpenGrid();
        var sim = new SandataSimulation(
            BuildMission(), BuildRuleset(), grid, NoWalls(grid), BuildState(ImmutableArray<GroupPathState>.Empty),
            ImmutableArray<CoverRecord>.Empty);

        Assert.True(sim.GetPublishedPath(groupId: 999).IsEmpty);
        Assert.Equal(PathReasonCode.NoDestinationRequested, sim.GetPublishedPathReasonCode(groupId: 999));
    }

    [Fact]
    public void GetPublishedPath_CalledBeforeItsLatencyElapses_ReturnsEmptyRatherThanTheUnpublishedPath()
    {
        var grid = BuildOpenGrid();
        var startCell = grid.CellIndex(0, 0);
        var goalCell = grid.CellIndex(5, 0);

        var groupState = new GroupPathState(
            GroupId: 1UL, DestinationCellIndex: goalCell, HasOutstandingRequest: true,
            StartCellIndex: startCell, GoalCellIndex: goalCell, RequestTick: 0);

        var sim = new SandataSimulation(
            BuildMission(), BuildRuleset(), grid, NoWalls(grid),
            BuildState(ImmutableArray.Create(groupState)), ImmutableArray<CoverRecord>.Empty);

        for (var tick = 0; tick < PathLatencyTicks; tick++)
        {
            sim.RunTick(tick);

            Assert.True(sim.GetPublishedPath(groupId: 1).IsEmpty, $"tick {tick}: must not publish before RequestTick + PathLatencyTicks");
            Assert.Equal(PathReasonCode.AwaitingLatency, sim.GetPublishedPathReasonCode(groupId: 1));
        }
    }

    // ---- The independent-oracle test. -------------------------------------

    /// <summary>
    /// The load-bearing assertion in this file. Rather than comparing
    /// <see cref="SandataSimulation.GetPublishedPath"/> against
    /// <see cref="PathService.GetCurrentPath"/> — the very method it
    /// delegates to, which would pass even if that method returned garbage —
    /// this test derives the expected polyline endpoints directly from
    /// <see cref="NavGrid.CellSizeWu"/> arithmetic: a cell's centre sits at
    /// <c>cellCoordinate * CellSizeWu + CellSizeWu / 2</c>, the same formula
    /// <c>TickPipelineTests</c>' own remarks record for cell (0, 0) sitting
    /// at world-unit centre (2, 2). Start (0, 0) and goal (5, 0) are on the
    /// same row of a fully open grid with no walls, so line-of-sight
    /// smoothing has nothing to bend around: the published polyline must
    /// start at the start cell's centre and end at the goal cell's centre,
    /// a fact this test checks by arithmetic no line-of-sight smoothing code
    /// participates in, independent of whatever <see cref="PathService"/>
    /// internally does to get there.
    /// </summary>
    [Fact]
    public void GetPublishedPath_AfterLatencyElapses_StartsAndEndsAtTheRequestedCellCentres_ComputedIndependently()
    {
        var grid = BuildOpenGrid();
        var startCell = grid.CellIndex(0, 0);
        var goalCell = grid.CellIndex(5, 0);

        var groupState = new GroupPathState(
            GroupId: 1UL, DestinationCellIndex: goalCell, HasOutstandingRequest: true,
            StartCellIndex: startCell, GoalCellIndex: goalCell, RequestTick: 0);

        var sim = new SandataSimulation(
            BuildMission(), BuildRuleset(), grid, NoWalls(grid),
            BuildState(ImmutableArray.Create(groupState)), ImmutableArray<CoverRecord>.Empty);

        for (var tick = 0; tick <= PathLatencyTicks; tick++)
        {
            sim.RunTick(tick);
        }

        Assert.Equal(PathReasonCode.PathValid, sim.GetPublishedPathReasonCode(groupId: 1));

        var path = sim.GetPublishedPath(groupId: 1);
        Assert.False(path.IsEmpty);

        // Independently derived from grid geometry, not from PathService.
        const long half = NavGrid.CellSizeWu / 2;
        var expectedStart = new PathPoint((0 * NavGrid.CellSizeWu) + half, (0 * NavGrid.CellSizeWu) + half);
        var expectedGoal = new PathPoint((5 * NavGrid.CellSizeWu) + half, (0 * NavGrid.CellSizeWu) + half);

        Assert.Equal(expectedStart, path[0]);
        Assert.Equal(expectedGoal, path[^1]);
    }

    // ---- Determinism: reading the accessor changes nothing it should not. ----

    /// <summary>
    /// Design section 4's determinism contract binds every read-only surface
    /// exactly as it binds every write: nothing may move the state hash that
    /// is not itself authoritative state. This runs the same tick sequence
    /// twice from equal starting fixtures, calling the two new accessors
    /// after every tick on one run and never on the other, and requires the
    /// final <see cref="SandataSimulation.LastStateHash"/> and
    /// <see cref="SandataSimulation.State"/> to agree — the cheapest
    /// available proof that these accessors are pure reads. No absolute hash
    /// literal is added here: <c>CLAUDE.md</c> reserves the one permitted
    /// literal in this test project for
    /// <c>MissionStateTests.PreTask79cBaselineHash</c>, and comparing two
    /// freshly computed hashes against each other needs no literal at all.
    /// </summary>
    [Fact]
    public void CallingTheNewAccessorsEveryTick_LeavesTheStateHashAndStateIdenticalToNeverCallingThem()
    {
        var grid = BuildOpenGrid();
        var startCell = grid.CellIndex(0, 0);
        var goalCell = grid.CellIndex(5, 0);

        GroupPathState BuildGroup() => new(
            GroupId: 1UL, DestinationCellIndex: goalCell, HasOutstandingRequest: true,
            StartCellIndex: startCell, GoalCellIndex: goalCell, RequestTick: 0);

        var simObserved = new SandataSimulation(
            BuildMission(), BuildRuleset(), grid, NoWalls(grid),
            BuildState(ImmutableArray.Create(BuildGroup())), ImmutableArray<CoverRecord>.Empty);
        var simUnobserved = new SandataSimulation(
            BuildMission(), BuildRuleset(), grid, NoWalls(grid),
            BuildState(ImmutableArray.Create(BuildGroup())), ImmutableArray<CoverRecord>.Empty);

        for (var tick = 0; tick <= PathLatencyTicks + 2; tick++)
        {
            simObserved.RunTick(tick);
            _ = simObserved.GetPublishedPath(groupId: 1);
            _ = simObserved.GetPublishedPathReasonCode(groupId: 1);
            _ = simObserved.GetPublishedPath(groupId: 999); // unknown group, still read every tick
            simUnobserved.RunTick(tick);
        }

        Assert.NotNull(simObserved.LastStateHash);
        Assert.Equal(simUnobserved.LastStateHash, simObserved.LastStateHash);
        Assert.Equal(simUnobserved.State, simObserved.State);
    }
}
