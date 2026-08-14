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
using Sandata.Core.Weapons;

namespace Sandata.Core.Tests;

/// <summary>
/// Decision D6: an operator engaging a hostile it has identified this tick is
/// never forced lowered by <c>WeaponLoweredRules.IsForcedLowered</c>, even
/// while it stands within <see cref="SandataRuleset.LoweredWallDistanceWu"/>
/// of a wall or inside a door cell. Before this change a rifle-armed operator
/// inside angle-house's roughly 32 world-unit corridors — narrower than
/// twice the ruleset's 24 world-unit threshold — was forced lowered for the
/// whole time it stood there, including while actively engaging an
/// identified hostile, and so could never fire indoors. These tests exercise
/// <see cref="SandataSimulation.RunTick"/> end to end, the same way
/// <c>OrderFollowingAndFireModeTests</c> proves the weapon-lowered flag and
/// the fire-mode band rule, so the fix is proven at the level a spectator
/// would actually observe it: a fired shot and the stored
/// <see cref="OperatorState.WeaponLowered"/> flag, not the isolated predicate.
/// </summary>
public sealed class WeaponLoweredEngagementTests
{
    // ---- Shared fixture builders, mirroring OrderFollowingAndFireModeTests ----

    private static Mission BuildMission(ulong seed = 1UL) => new(
        formatVersion: Mission.CurrentFormatVersion,
        seed: seed,
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
        FirearmId firearm = FirearmId.Ak47) => new(
            EntityId: (ulong)entityId,
            PositionX: FixedPoint.FromWhole(positionXWu),
            PositionY: FixedPoint.FromWhole(positionYWu),
            Facing: Facing16.East,
            AimAngle: Bam16.FromFacing16(Facing16.East),
            Health: health,
            Faction: faction,
            Intent: 0,
            IsCrouched: false,
            WeaponLowered: false,
            WeaponChainPhase: 0,
            WeaponChainRemainingTicks: 0,
            MagazineRounds: 30,
            CyclicFireAccumulator: 0,
            SuppressionCounter: 0)
        {
            Firearm = firearm,
        };

    private static MissionState BuildState(ImmutableArray<OperatorState> operators) => new(
        Tick: 0, Phase: 1, Winner: -1, NextEntityId: (ulong)(operators.Length + 1), NextEventSequence: 0)
    {
        Operators = operators,
        FactionAlerts = ImmutableArray.Create(new FactionAlertState(0, 0), new FactionAlertState(1, 0)),
        Doors = ImmutableArray<DoorState>.Empty,
        Groups = ImmutableArray<GroupPathState>.Empty,
        RngStreams = ImmutableArray<RngStreamState>.Empty,
    };

    private static NavGrid BuildGrid(int width = 64, int height = 64)
    {
        var grid = new NavGrid(width: width, height: height);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        return grid;
    }

    private static WallBuckets NoWalls(NavGrid grid) => WallBuckets.Build(grid, [], [], [], []);

    /// <summary>
    /// A wall along Y = 0 spanning X in [0, 400] — the same fixture shape
    /// <c>OrderFollowingAndFireModeTests.RunTick_OperatorStandingAgainstAWall_...</c>
    /// uses — so a shooter placed a few world units north of it sits well
    /// inside <see cref="SandataRuleset.LoweredWallDistanceWu"/> (24 wu).
    /// </summary>
    private static WallBuckets WallAlongY0(NavGrid grid) =>
        WallBuckets.Build(grid, [0L], [0L], [400L], [0L]);

    // ---- 1. Wall proximity: firing indoors becomes possible ---------------

    /// <summary>
    /// The bug as measured on the shipped <c>angle-house</c> map: a rifle
    /// operator standing well within <c>LoweredWallDistanceWu</c> of a wall,
    /// with an identified hostile in range and inside the AK-47's auto band,
    /// now fires — where before this change it never could.
    /// </summary>
    [Fact]
    public void RunTick_RifleNearAWall_WithIdentifiedHostile_FiresAndEndsUpNotLowered()
    {
        var grid = BuildGrid();
        var wallBuckets = WallAlongY0(grid);

        // Shooter sits 20 wu off the wall — inside the 24 wu threshold — so
        // WeaponLoweredRules.IsForcedLowered would return true for it absent
        // the engagement exemption. The enemy sits 90 wu east, inside both
        // ContactMemory.IdentifyRangeWu (96) and the AK-47's auto band
        // (RifleAutoBandMaxWu = 240), and far outside any damage this fixture
        // measures the cadence of, so the burst is never cut short by death.
        var shooter = BuildOperator(entityId: 1, faction: 0, positionXWu: 40, positionYWu: 20);
        var target = BuildOperator(
            entityId: 2, faction: 1, positionXWu: 130, positionYWu: 20, health: 100_000);

        var sim = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            wallBuckets,
            BuildState(ImmutableArray.Create(shooter, target)),
            ImmutableArray<CoverRecord>.Empty);

        for (var tick = 0; tick < 60; tick++)
        {
            sim.RunTick(tick);
        }

        var shots = sim.State.EventFeed.Events.Count(e => e.Kind == MissionEventKind.ShotFired);
        Assert.True(shots > 0, "a rifle engaging an identified hostile near a wall must be able to fire");

        var shooterState = sim.State.Operators.Single(o => o.EntityId == 1);
        Assert.False(shooterState.WeaponLowered);
    }

    /// <summary>
    /// The control for the test above, and decision D6's other half: the
    /// identical wall-proximity geometry with no enemy on the roster at all
    /// produces no identified hostile, so the operator is still forced
    /// lowered and fires nothing — proving the fix is conditioned on the
    /// engagement, not a side effect of removing the wall rule altogether.
    /// </summary>
    [Fact]
    public void RunTick_RifleNearAWall_WithNoIdentifiedHostile_StaysLoweredAndDoesNotFire()
    {
        var grid = BuildGrid();
        var wallBuckets = WallAlongY0(grid);

        var shooter = BuildOperator(entityId: 1, faction: 0, positionXWu: 40, positionYWu: 20);

        var sim = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            wallBuckets,
            BuildState(ImmutableArray.Create(shooter)),
            ImmutableArray<CoverRecord>.Empty);

        for (var tick = 0; tick < 60; tick++)
        {
            sim.RunTick(tick);
        }

        var shots = sim.State.EventFeed.Events.Count(e => e.Kind == MissionEventKind.ShotFired);
        Assert.Equal(0, shots);

        var shooterState = sim.State.Operators.Single(o => o.EntityId == 1);
        Assert.True(shooterState.WeaponLowered);
    }

    // ---- 2. Doorway regression: smoke row SD-4's behaviour is unchanged ---

    /// <summary>
    /// Regression guard for smoke row SD-4: an operator with no identified
    /// hostile at all — the doorway-transiting case that row asks a person to
    /// watch — still lowers its weapon inside a closed door's cell exactly as
    /// it did before decision D6. The engagement exemption must never widen
    /// past "a target was actually acquired this tick".
    /// </summary>
    [Fact]
    public void RunTick_OperatorInsideADoorCell_WithNoIdentifiedHostile_StillLowers()
    {
        var grid = BuildGrid();

        // Cell (5, 5) — world units [20, 24) on each axis — tagged Door,
        // exactly as NavBake.Bake would tag a closed door's rasterised
        // footprint. No walls registered, isolating the door-cell branch.
        var doorCellIndex = grid.CellIndex(5, 5);
        grid.Passability[doorCellIndex] = NavCellFlags.Door;
        var wallBuckets = NoWalls(grid);

        // (21, 21) falls inside cell (5, 5): 21 >> 2 == 5.
        var solo = BuildOperator(entityId: 1, faction: 0, positionXWu: 21, positionYWu: 21);

        var sim = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            wallBuckets,
            BuildState(ImmutableArray.Create(solo)),
            ImmutableArray<CoverRecord>.Empty);

        sim.RunTick(0);

        Assert.True(Assert.Single(sim.State.Operators).WeaponLowered);
        Assert.Single(sim.State.EventFeed.Events, e => e.Kind == MissionEventKind.WeaponLowered);
    }
}
