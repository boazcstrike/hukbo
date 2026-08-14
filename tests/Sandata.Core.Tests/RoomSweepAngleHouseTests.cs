using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Maps;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;

namespace Sandata.Core.Tests;

/// <summary>
/// The room sweep, driven against the real <c>angle-house.hkmap</c> fixture
/// with a real <see cref="NavBake"/> instead of a hand-built open grid.
/// <para>
/// Every other Stage 0 test builds its grid with <c>Array.Fill(passability,
/// NavCellFlags.Open)</c> and then bakes <see cref="RoomLayout"/> from wall
/// records that were never rasterised into that passability. On such a grid
/// every cell is walkable, including the top-left interior corner cell a
/// room takes its id from, so the sweep's choice of goal cell could not
/// fail — and it failed on every room of the shipped map, because
/// <see cref="NavBake"/>'s body-radius inflation blocks exactly that corner.
/// These tests exist so that a goal cell no search can reach is caught here
/// rather than by watching a squad stand still in the running game.
/// </para>
/// </summary>
public sealed class RoomSweepAngleHouseTests
{
    /// <summary>
    /// The body radius <c>Sandata.Client</c> bakes the shipped map with. Held
    /// here as its own literal rather than read from the client, which this
    /// project may not reference; a divergence between the two would make
    /// these tests pass against geometry the game never runs.
    /// </summary>
    private const int ClientBodyRadiusWu = 5;

    /// <summary>
    /// The map's <c>OBJECTIVE 0 500 120</c> record as a flat cell index:
    /// (500 / 4, 120 / 4) = (125, 30), so 30 * 160 + 125. This is the cell
    /// <c>Sandata.Client.Simulation.InitialSquadGroups</c> seeds the assaulting
    /// group's one path request with, and it lies in the top-right room.
    /// </summary>
    private const int ObjectiveCellIndex = 4925;

    /// <summary>The top-right objective room's id — its lowest cell index, (106, 16).</summary>
    private const int ObjectiveRoomId = 2666;

    /// <summary>The lower-left closet's id — its lowest cell index, (16, 116).</summary>
    private const int ClosetRoomId = 18576;

    /// <summary>
    /// The assaulting <c>SPAWN 0 296 690</c> record as a flat cell index:
    /// (74, 172), so 172 * 160 + 74. The closet is the nearest uncleared room
    /// to this cell by octile distance (804) and the objective room is more
    /// than twice as far (1688), which is exactly why the bare argmin sends a
    /// squad the wrong way.
    /// </summary>
    private const int SpawnCellIndex = 27594;

    private static (NavGrid Grid, WallBuckets Buckets, RoomLayout Layout) BakeAngleHouse()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "angle-house.hkmap");

        Assert.True(
            File.Exists(path),
            $"The angle-house fixture is missing at '{path}'. It is committed under " +
            "tests/Sandata.Core.Tests/Fixtures and copied to the output directory by " +
            "the project's Fixtures item.");

        var canonical = MapCanonicalizer.Canonicalize(MapTokenizer.Tokenize(File.ReadAllText(path)));
        var gridRecord = canonical.OfType<GridRecord>().Single();
        var grid = new NavGrid(gridRecord.WidthWu / gridRecord.CellWu, gridRecord.HeightWu / gridRecord.CellWu);
        var walls = canonical.OfType<WallRecord>().ToArray();
        var doors = canonical.OfType<DoorRecord>().ToArray();

        NavBake.Bake(grid, walls, doors, ClientBodyRadiusWu);

        var segmentAX = new long[walls.Length];
        var segmentAY = new long[walls.Length];
        var segmentBX = new long[walls.Length];
        var segmentBY = new long[walls.Length];
        for (var index = 0; index < walls.Length; index++)
        {
            segmentAX[index] = walls[index].X1;
            segmentAY[index] = walls[index].Y1;
            segmentBX[index] = walls[index].X2;
            segmentBY[index] = walls[index].Y2;
        }

        var buckets = WallBuckets.Build(grid, segmentAX, segmentAY, segmentBX, segmentBY);

        return (grid, buckets, RoomLayout.Bake(grid, walls, doors));
    }

    private static Mission BuildMission() => new(
        formatVersion: Mission.CurrentFormatVersion,
        seed: 1UL,
        mapContentHash: 1UL,
        tickPolicy: new MissionTickPolicy(TickLimit: 10_000, StateHashCadenceTicks: 1),
        factionSetups: ImmutableArray.Create(
            new MissionFactionSetup(FactionId: 0, OperatorCount: 2),
            new MissionFactionSetup(FactionId: 1, OperatorCount: 2)),
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

    /// <summary>
    /// The map's own <c>SPAWN</c> records, verbatim: the assaulting pair in
    /// the bottom band and the two defenders, one in the lower-left closet
    /// and one in the top-right objective room.
    /// </summary>
    private static ImmutableArray<OperatorState> BuildAngleHouseRoster() => ImmutableArray.Create(
        BuildOperator(entityId: 1, faction: 0, positionXWu: 296, positionYWu: 690),
        BuildOperator(entityId: 2, faction: 0, positionXWu: 320, positionYWu: 690),
        BuildOperator(entityId: 3, faction: 1, positionXWu: 120, positionYWu: 520),
        BuildOperator(entityId: 4, faction: 1, positionXWu: 500, positionYWu: 120));

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

    /// <summary>
    /// Every room of the shipped map takes its id from a cell the body-radius
    /// inflation blocks, so a sweep that pathed to a room's id cell could
    /// never reach any room on this map. This pins the property that made
    /// that possible, so nobody reintroduces the id cell as a goal by
    /// assuming a room id names walkable floor.
    /// </summary>
    [Fact]
    public void EveryAngleHouseRoomIdCell_IsBlockedByBodyRadiusInflation()
    {
        var (grid, _, layout) = BakeAngleHouse();

        Assert.NotEmpty(layout.RoomIds);

        foreach (var roomId in layout.RoomIds)
        {
            Assert.Equal(NavCellFlags.Blocked, grid.Passability[roomId]);
        }
    }

    /// <summary>
    /// The regression itself, at simulation level: a squad spawned where the
    /// map spawns it, given no target, must be handed a goal cell a search
    /// can actually accept — never a blocked one — and must still hold one
    /// after enough ticks for the first path to publish and the first
    /// retarget to run.
    /// </summary>
    [Fact]
    public void SweepingTheRealMap_NeverTargetsABlockedCell()
    {
        var (grid, buckets, layout) = BakeAngleHouse();
        var blocked = SandataSimulation.BuildPathBlockedCells(grid);

        var groups = ImmutableArray.Create(new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: -1,
            HasOutstandingRequest: false,
            StartCellIndex: 0,
            GoalCellIndex: 0,
            RequestTick: 0));

        var sim = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            buckets,
            BuildState(BuildAngleHouseRoster(), groups),
            ImmutableArray<CoverRecord>.Empty,
            layout);

        var observedTargets = 0;
        for (var tick = 0L; tick < 600; tick++)
        {
            sim.RunTick(tick);

            var group = sim.State.Groups.Single();
            if (group.TargetRoomId < 0)
            {
                continue;
            }

            observedTargets++;
            Assert.False(
                blocked[group.GoalCellIndex],
                $"tick {tick}: the sweep targeted room {group.TargetRoomId} at blocked cell " +
                $"{group.GoalCellIndex} ({grid.CellX(group.GoalCellIndex)},{grid.CellY(group.GoalCellIndex)}).");
        }

        Assert.True(observedTargets > 0, "the sweep never selected a room at all over 600 ticks.");
    }

    /// <summary>
    /// The symptom a person watching the game sees. A squad that has been
    /// given a reachable destination moves toward it; the branch's earlier
    /// shape left both operators standing on their spawn cells for the whole
    /// run, which is what a blocked goal looks like from outside.
    /// </summary>
    [Fact]
    public void SweepingTheRealMap_MovesTheSquadOffItsSpawn()
    {
        var (grid, buckets, layout) = BakeAngleHouse();

        var groups = ImmutableArray.Create(new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: -1,
            HasOutstandingRequest: false,
            StartCellIndex: 0,
            GoalCellIndex: 0,
            RequestTick: 0));

        var roster = BuildAngleHouseRoster();
        var sim = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            buckets,
            BuildState(roster, groups),
            ImmutableArray<CoverRecord>.Empty,
            layout);

        for (var tick = 0L; tick < 600; tick++)
        {
            sim.RunTick(tick);
        }

        var leader = sim.State.Operators.Single(o => o.EntityId == 1UL);
        var spawn = roster.Single(o => o.EntityId == 1UL);

        Assert.True(
            leader.PositionX != spawn.PositionX || leader.PositionY != spawn.PositionY,
            $"the squad leader never left its spawn cell over 600 ticks " +
            $"(target room {sim.State.Groups.Single().TargetRoomId}, " +
            $"goal cell {sim.State.Groups.Single().GoalCellIndex}).");
    }

    /// <summary>
    /// Assault first, sweep after. A squad seeded with the map's own
    /// <c>OBJECTIVE 0</c> cell must keep that destination on its opening
    /// move — target room 2666, the top-right objective room — instead of
    /// retargeting to room 18576, the lower-left closet, which is the nearest
    /// uncleared room from the spawn by octile distance and which the sweep's
    /// argmin therefore picks whenever it is allowed to run first.
    /// </summary>
    [Fact]
    public void SquadSeededWithTheObjective_KeepsItAsItsOpeningTarget()
    {
        var (grid, buckets, layout) = BakeAngleHouse();

        var groups = ImmutableArray.Create(new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: ObjectiveCellIndex,
            HasOutstandingRequest: true,
            StartCellIndex: SpawnCellIndex,
            GoalCellIndex: ObjectiveCellIndex,
            RequestTick: 0));

        var sim = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            buckets,
            BuildState(BuildAngleHouseRoster(), groups),
            ImmutableArray<CoverRecord>.Empty,
            layout);

        sim.RunTick(0);

        var group = sim.State.Groups.Single();

        Assert.Equal(ObjectiveRoomId, group.TargetRoomId);
        Assert.Equal(ObjectiveCellIndex, group.GoalCellIndex);
    }

    /// <summary>
    /// The other half of the same rule: adoption is an opening move only.
    /// Once the seeded room is cleared, the sweep's own argmin takes over and
    /// the group moves on to another room rather than holding a finished one
    /// forever.
    /// </summary>
    [Fact]
    public void OnceTheSeededRoomIsCleared_TheSweepTakesOver()
    {
        var (grid, buckets, layout) = BakeAngleHouse();

        var groups = ImmutableArray.Create(new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: ObjectiveCellIndex,
            HasOutstandingRequest: true,
            StartCellIndex: SpawnCellIndex,
            GoalCellIndex: ObjectiveCellIndex,
            RequestTick: 0));

        // The squad already stands inside the objective room and its defender
        // is already dead, so the Stage-0 presence predicate clears that room
        // on the very first tick and adoption has nothing left to adopt.
        var roster = ImmutableArray.Create(
            BuildOperator(entityId: 1, faction: 0, positionXWu: 500, positionYWu: 120),
            BuildOperator(entityId: 2, faction: 0, positionXWu: 508, positionYWu: 120),
            BuildOperator(entityId: 3, faction: 1, positionXWu: 120, positionYWu: 520));

        var sim = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            buckets,
            BuildState(roster, groups),
            ImmutableArray<CoverRecord>.Empty,
            layout);

        sim.RunTick(0);

        var group = sim.State.Groups.Single();

        Assert.True(
            sim.State.RoomClearStates.Single(r => r.RoomId == ObjectiveRoomId).Cleared,
            "the objective room did not clear, so this test is not exercising the handover it names.");
        Assert.NotEqual(ObjectiveRoomId, group.TargetRoomId);
        Assert.True(group.TargetRoomId >= 0, "the sweep did not take over after the seeded room cleared.");
    }

    /// <summary>
    /// The behaviour a spectator would judge the rule by, driven long enough
    /// for the squad to cross the map: the objective room's defender is the
    /// one the squad reaches, and it is that room that clears — not the
    /// closet the bare argmin sends them to.
    /// </summary>
    [Fact]
    public void SquadSeededWithTheObjective_AssaultsTheObjectiveRoomFirst()
    {
        var (grid, buckets, layout) = BakeAngleHouse();

        var groups = ImmutableArray.Create(new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: ObjectiveCellIndex,
            HasOutstandingRequest: true,
            StartCellIndex: SpawnCellIndex,
            GoalCellIndex: ObjectiveCellIndex,
            RequestTick: 0));

        var sim = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            buckets,
            BuildState(BuildAngleHouseRoster(), groups),
            ImmutableArray<CoverRecord>.Empty,
            layout);

        var closetRoomTargetedFirst = false;
        for (var tick = 0L; tick < 4_000; tick++)
        {
            sim.RunTick(tick);

            if (sim.State.Groups.Single().TargetRoomId == ClosetRoomId
                && !sim.State.RoomClearStates.Single(r => r.RoomId == ObjectiveRoomId).Cleared)
            {
                closetRoomTargetedFirst = true;
                break;
            }
        }

        Assert.False(
            closetRoomTargetedFirst,
            "the squad turned toward the closet before the objective room was cleared.");

        var objectiveDefender = sim.State.Operators.Single(o => o.EntityId == 4UL);
        Assert.True(
            objectiveDefender.Health < 100,
            $"the objective room's defender was never engaged (health {objectiveDefender.Health}).");
    }

    /// <summary>
    /// The squad has to be able to finish a fight. An operator holds
    /// <see cref="OperatorIntent.Engage"/> off an identified contact, and
    /// nothing decays a contact, so before a dead subject's contact was
    /// forgotten the leader stayed in <c>Engage</c> for the rest of the
    /// mission — measured at tick 35,999 against a target that died at 672 —
    /// and the sweep, frozen by design decision 5 for any group holding an
    /// identified contact, could never retarget.
    /// </summary>
    [Fact]
    public void OnceItsTargetIsDead_TheLeaderLeavesEngageAndTheSweepMovesOn()
    {
        var (grid, buckets, layout) = BakeAngleHouse();

        var groups = ImmutableArray.Create(new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: ObjectiveCellIndex,
            HasOutstandingRequest: true,
            StartCellIndex: SpawnCellIndex,
            GoalCellIndex: ObjectiveCellIndex,
            RequestTick: 0));

        var sim = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            buckets,
            BuildState(BuildAngleHouseRoster(), groups),
            ImmutableArray<CoverRecord>.Empty,
            layout);

        var everEngaged = false;
        for (var tick = 0L; tick < 4_000; tick++)
        {
            sim.RunTick(tick);

            if (sim.State.Operators.Single(o => o.EntityId == 1UL).Intent == (int)OperatorIntent.Engage)
            {
                everEngaged = true;
            }
        }

        Assert.True(everEngaged, "the leader never engaged at all, so this test proves nothing about leaving it.");

        var leader = sim.State.Operators.Single(o => o.EntityId == 1UL);
        var objectiveDefender = sim.State.Operators.Single(o => o.EntityId == 4UL);
        var group = sim.State.Groups.Single();

        Assert.Equal(0, objectiveDefender.Health);
        Assert.NotEqual((int)OperatorIntent.Engage, leader.Intent);
        Assert.Equal(ClosetRoomId, group.TargetRoomId);
    }
}
