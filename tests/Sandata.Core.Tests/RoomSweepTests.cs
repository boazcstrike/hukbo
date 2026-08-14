using System.Collections.Immutable;
using System.Linq;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Events;
using Sandata.Core.Maps;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;

namespace Sandata.Core.Tests;

/// <summary>
/// Stage 0's <see cref="SandataSimulation"/>-level test bar (design decisions
/// 3, 4, and 5 of <c>docs/plans/2026-08-14-sandata-clear-the-map-design.md</c>,
/// staged for Stage 0 only): the presence-based <c>Cleared</c> predicate, one
/// <see cref="MissionEventKind.RoomCleared"/> event per transition, a group
/// retargeting to the next not-yet-cleared room, and the Engage freeze
/// holding a group's current selection. Every test drives the real fourteen-
/// stage pipeline through <see cref="SandataSimulation.RunTick"/> — none of
/// them calls a private room-sweep method directly — so each one exercises
/// the same mechanism a live mission actually runs.
/// </summary>
public sealed class RoomSweepTests
{
    // ---- Shared fixture builders ----------------------------------------

    private static Mission BuildMission() => new(
        formatVersion: Mission.CurrentFormatVersion,
        seed: 1UL,
        mapContentHash: 1UL,
        tickPolicy: new MissionTickPolicy(TickLimit: 10_000, StateHashCadenceTicks: 1),
        factionSetups: ImmutableArray.Create(
            new MissionFactionSetup(FactionId: 0, OperatorCount: 4),
            new MissionFactionSetup(FactionId: 1, OperatorCount: 4)),
        rulesetId: SandataPresetId.ModernTacticalV1);

    private static OperatorState BuildOperator(
        int entityId,
        int faction,
        int positionXWu,
        int positionYWu,
        int health = 100,
        Facing16? facing = null) => new(
            EntityId: (ulong)entityId,
            PositionX: FixedPoint.FromWhole(positionXWu),
            PositionY: FixedPoint.FromWhole(positionYWu),
            Facing: facing ?? Facing16.East,
            AimAngle: Bam16.FromFacing16(facing ?? Facing16.East),
            Health: health,
            Faction: faction,
            Intent: 0,
            IsCrouched: false,
            WeaponLowered: false,
            WeaponChainPhase: 0,
            WeaponChainRemainingTicks: 0,
            MagazineRounds: 30,
            CyclicFireAccumulator: 0,
            SuppressionCounter: 0);

    private static MissionState BuildState(
        ImmutableArray<OperatorState> operators,
        ImmutableArray<GroupPathState> groups) => new(
            Tick: 0, Phase: 1, Winner: -1, NextEntityId: (ulong)(operators.Length + 1), NextEventSequence: 0)
        {
            Operators = operators,
            FactionAlerts = ImmutableArray.Create(new FactionAlertState(0, 0), new FactionAlertState(1, 0)),
            Doors = ImmutableArray<DoorState>.Empty,
            Groups = groups,
            RngStreams = ImmutableArray<RngStreamState>.Empty,
        };

    private static NavGrid BuildOpenGrid(int width, int height)
    {
        var grid = new NavGrid(width: width, height: height);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        return grid;
    }

    private static WallBuckets NoWalls(NavGrid grid) => WallBuckets.Build(grid, [], [], [], []);

    /// <summary>
    /// A single-cell-wide, full-height wall-plus-door divider at world-unit
    /// x = <paramref name="dividerXWu"/>, spanning a 4-row-tall grid (0-16 wu)
    /// with three <see cref="WallRecord"/> segments and one
    /// <see cref="DoorRecord"/> segment. Every door counts as boundary
    /// geometry regardless of its open/closed state (<see cref="RoomLayout"/>'s
    /// own summary), so the door's own <c>State</c> value here is irrelevant
    /// to room derivation and is left closed (0) arbitrarily. Mirrors
    /// <c>NavBakeTests</c>'s hand-computed-fixture convention: each segment's
    /// supercover walk is worked by hand from <c>NavBake.SupercoverCells</c>'s
    /// own rule (a vertical segment at world x rasterizes to cell column
    /// x / 4 only, never the column to its left), confirmed against the real
    /// axis-parallel-wall fixture in <c>NavBakeTests</c>.
    /// </summary>
    private static (WallRecord[] Walls, DoorRecord[] Doors) BuildFullHeightDivider(int dividerXWu) => (
        new[]
        {
            new WallRecord(LineNumber: 1, X1: dividerXWu, Y1: 0, X2: dividerXWu, Y2: 4, Material: 1),
            new WallRecord(LineNumber: 2, X1: dividerXWu, Y1: 8, X2: dividerXWu, Y2: 12, Material: 1),
            new WallRecord(LineNumber: 3, X1: dividerXWu, Y1: 12, X2: dividerXWu, Y2: 16, Material: 1),
        },
        new[]
        {
            new DoorRecord(LineNumber: 4, X1: dividerXWu, Y1: 4, X2: dividerXWu, Y2: 8, Hinge: 0, State: 0),
        });

    /// <summary>
    /// An 8-by-4-cell grid (32 by 16 wu) split by a divider at world-unit
    /// x = 16 (cell column 4) into Room A (columns 0-3, <c>RoomId</c> 0 — the
    /// flood fill's own seed cell, index 0) and Room B (columns 5-7,
    /// <c>RoomId</c> 5 — the first unclaimed cell after the boundary column
    /// in the ascending row-major scan, at flat index <c>0 * 8 + 5</c>).
    /// </summary>
    private static (NavGrid Grid, RoomLayout Layout) BuildTwoRoomFixture()
    {
        var grid = BuildOpenGrid(width: 8, height: 4);
        var (walls, doors) = BuildFullHeightDivider(dividerXWu: 16);
        var layout = RoomLayout.Bake(grid, walls, doors);
        return (grid, layout);
    }

    /// <summary>
    /// A 32-by-4-cell grid (128 by 16 wu) split by a divider at world-unit
    /// x = 104 (cell column 26) into Room A (columns 0-25, <c>RoomId</c> 0)
    /// and Room B (columns 27-31, <c>RoomId</c> 27 — flat index
    /// <c>0 * 32 + 27</c>). Room A is wide enough to hold an operator at
    /// world x = 0 and another at world x = 90 simultaneously — the exact
    /// 90 wu separation <c>TickPipelineTests</c>'s own
    /// <c>ContactMemory.IdentifyRangeWu</c> (96) fixture uses to force a
    /// fresh <see cref="Sensing.ContactTier.Identified"/> sighting — so the
    /// Engage-freeze test below needs a room large enough to host that exact,
    /// already-proven-good fixture rather than inventing a new range fact.
    /// </summary>
    private static (NavGrid Grid, RoomLayout Layout) BuildWideTwoRoomFixture()
    {
        var grid = BuildOpenGrid(width: 32, height: 4);
        var (walls, doors) = BuildFullHeightDivider(dividerXWu: 104);
        var layout = RoomLayout.Bake(grid, walls, doors);
        return (grid, layout);
    }

    // ---- Clear-state predicate -------------------------------------------

    /// <summary>
    /// Design section 12's Stage-0 predicate: a living assaulting-faction
    /// operator occupies Room A alongside a living hostile, so Room A must
    /// not clear on this tick, and no <see cref="MissionEventKind.RoomCleared"/>
    /// event is emitted for it.
    /// </summary>
    [Fact]
    public void RoomWithALivingHostile_DoesNotClear()
    {
        var (grid, layout) = BuildTwoRoomFixture();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var operators = ImmutableArray.Create(
            BuildOperator(entityId: 1, faction: 0, positionXWu: 0, positionYWu: 0),
            BuildOperator(entityId: 2, faction: 1, positionXWu: 8, positionYWu: 0));
        var state = BuildState(operators, ImmutableArray<GroupPathState>.Empty);

        var sim = new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty, layout);

        sim.RunTick(0);

        var roomA = sim.State.RoomClearStates.Single(r => r.RoomId == 0);
        Assert.False(roomA.Cleared);
        Assert.Empty(sim.State.EventFeed.Events.Where(e => e.Kind == MissionEventKind.RoomCleared));
    }

    /// <summary>
    /// The same room, the same assaulting operator, but the hostile is not
    /// alive (<see cref="OperatorState.Health"/> 0): design section 12's
    /// predicate reads only <see cref="Simulation.TickStartView.IsAlive"/>
    /// occupants, so a dead body left behind does not block a room's clear
    /// state. Room A must clear on this exact tick, and exactly one
    /// <see cref="MissionEventKind.RoomCleared"/> event names it — never zero,
    /// never two.
    /// </summary>
    [Fact]
    public void RoomClears_OnceItsLivingHostileDies()
    {
        var (grid, layout) = BuildTwoRoomFixture();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var operators = ImmutableArray.Create(
            BuildOperator(entityId: 1, faction: 0, positionXWu: 0, positionYWu: 0),
            BuildOperator(entityId: 2, faction: 1, positionXWu: 8, positionYWu: 0, health: 0));
        var state = BuildState(operators, ImmutableArray<GroupPathState>.Empty);

        var sim = new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty, layout);

        sim.RunTick(0);

        var roomA = sim.State.RoomClearStates.Single(r => r.RoomId == 0);
        Assert.True(roomA.Cleared);

        var roomB = sim.State.RoomClearStates.Single(r => r.RoomId == 5);
        Assert.False(roomB.Cleared);

        var roomClearedEvents = sim.State.EventFeed.Events.Where(e => e.Kind == MissionEventKind.RoomCleared).ToArray();
        var roomClearedEvent = Assert.Single(roomClearedEvents);
        Assert.Equal(0, roomClearedEvent.SubjectId);
    }

    // ---- Retargeting -------------------------------------------------------

    /// <summary>
    /// Design decision 4's Phase A: a lone Faction-0 group's
    /// <see cref="GroupPathState.TargetRoomId"/> names Room A, which clears
    /// on this exact tick (the operator is alone in it), so the same tick's
    /// retargeting pass must move the group on to Room B — the only
    /// remaining not-yet-cleared room — rather than leaving it idle the way
    /// today's shipped <c>InitialSquadGroups</c> behaviour does.
    /// </summary>
    [Fact]
    public void GroupRetargets_ToTheNextUnclearedRoom_OnceItsCurrentTargetClears()
    {
        var (grid, layout) = BuildTwoRoomFixture();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var operators = ImmutableArray.Create(
            BuildOperator(entityId: 1, faction: 0, positionXWu: 0, positionYWu: 0));
        var groups = ImmutableArray.Create(
            new GroupPathState(
                GroupId: 1UL,
                DestinationCellIndex: 0,
                HasOutstandingRequest: false,
                StartCellIndex: 0,
                GoalCellIndex: 0,
                RequestTick: 0,
                TargetRoomId: 0));
        var state = BuildState(operators, groups);

        var sim = new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty, layout);

        sim.RunTick(0);

        Assert.True(sim.State.RoomClearStates.Single(r => r.RoomId == 0).Cleared);

        var group = Assert.Single(sim.State.Groups);
        Assert.Equal(5, group.TargetRoomId);
        Assert.Equal(0, group.StartCellIndex);
        Assert.Equal(5, group.GoalCellIndex);
        Assert.Equal(0, group.RequestTick);
        Assert.True(group.HasOutstandingRequest);
    }

    // ---- The Engage freeze --------------------------------------------------

    /// <summary>
    /// Design decision 5's freeze: a lone Faction-0 group's target is still
    /// unassigned (<see cref="GroupPathState.TargetRoomId"/> -1, which would
    /// ordinarily select a room this same tick, exactly as
    /// <see cref="GroupRetargets_ToTheNextUnclearedRoom_OnceItsCurrentTargetClears"/>
    /// proves it does), but the group's only living member sights a hostile
    /// 90 world units due east — the exact separation
    /// <c>TickPipelineTests.RunTick_AimToleranceBamThreshold_CompletesTurningOnlyWhenResidualArcFitsInside</c>
    /// already proves resolves to a fresh <see cref="Sensing.ContactTier.Identified"/>
    /// sighting and an <see cref="OperatorIntent.Engage"/> selection this same
    /// tick. The room sweep must not touch the group's target at all this
    /// tick, even though Room B is real, reachable, and not cleared.
    /// </summary>
    [Fact]
    public void GroupDoesNotRetarget_WhileALivingMemberHasIdentifiedAContact()
    {
        var (grid, layout) = BuildWideTwoRoomFixture();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var operators = ImmutableArray.Create(
            BuildOperator(entityId: 1, faction: 0, positionXWu: 0, positionYWu: 0),
            BuildOperator(entityId: 2, faction: 1, positionXWu: 90, positionYWu: 0));
        var groups = ImmutableArray.Create(
            new GroupPathState(
                GroupId: 1UL,
                DestinationCellIndex: 0,
                HasOutstandingRequest: false,
                StartCellIndex: 0,
                GoalCellIndex: 0,
                RequestTick: 0,
                TargetRoomId: -1));
        var state = BuildState(operators, groups);

        var sim = new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty, layout);

        sim.RunTick(0);

        // Confirms the freeze actually engaged, not merely that retargeting
        // happened not to fire: the assaulting operator's own contact memory
        // must carry an Identified entry for the hostile this tick.
        var assaultOperator = sim.State.Operators.Single(o => o.EntityId == 1UL);
        var contact = Assert.Single(assaultOperator.ContactMemory);
        Assert.Equal(2UL, contact.EnemyEntityId);
        Assert.Equal((int)Sensing.ContactTier.Identified, contact.ContactTier);

        var group = Assert.Single(sim.State.Groups);
        Assert.Equal(-1, group.TargetRoomId);
        Assert.False(group.HasOutstandingRequest);
    }
}
