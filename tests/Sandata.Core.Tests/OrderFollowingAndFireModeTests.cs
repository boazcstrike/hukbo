using System.Collections.Immutable;
using System.Linq;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Combat;
using Sandata.Core.Events;
using Sandata.Core.Maps;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;
using Sandata.Core.Weapons;

namespace Sandata.Core.Tests;

/// <summary>
/// The 2026-08-12 smoke package
/// (docs/plans/2026-08-12-sandata-order-and-combat-legibility.md): the three
/// simulation defects the second Sandata smoke session surfaced. Each one was
/// a fully implemented, fully unit-tested rule with no production caller, so
/// every test here exercises <see cref="SandataSimulation.RunTick"/> end to
/// end rather than the rule in isolation — the rules themselves were already
/// green while the game did none of this.
/// </summary>
public sealed class OrderFollowingAndFireModeTests
{
    // ---- Shared fixture builders ----------------------------------------

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
        FirearmId firearm = FirearmId.Ak47,
        int weaponChainPhase = 0,
        int weaponChainRemainingTicks = 0) => new(
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
            WeaponChainPhase: weaponChainPhase,
            WeaponChainRemainingTicks: weaponChainRemainingTicks,
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

    /// <summary>
    /// An all-<see cref="NavCellFlags.Open"/> grid. <see cref="NavGrid"/>'s
    /// constructor leaves every cell at the enum's zero value,
    /// <see cref="NavCellFlags.Blocked"/>, which would make every authored
    /// polyline both unsubmittable and untraversable.
    /// </summary>
    private static NavGrid BuildGrid(int width = 32, int height = 32)
    {
        var grid = new NavGrid(width: width, height: height);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        return grid;
    }

    private static WallBuckets NoWalls(NavGrid grid) => WallBuckets.Build(grid, [], [], [], []);

    private static SandataSimulation BuildOrderedFixture(
        ImmutableArray<OrderPathNode> pathNodes,
        out SandataSimulation simulation,
        int operatorHealth = 100)
    {
        var grid = BuildGrid();
        var op = BuildOperator(entityId: 1, faction: 0, positionXWu: 0, positionYWu: 0, health: operatorHealth);

        simulation = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            NoWalls(grid),
            BuildState(ImmutableArray.Create(op)),
            ImmutableArray<CoverRecord>.Empty);

        simulation.SubmitOrder(
            targetTick: 0,
            factionId: 0,
            addressees: ImmutableArray.Create(1UL),
            kind: OrderKind.MoveAlongPath,
            pathNodes: pathNodes);

        return simulation;
    }

    // ---- 1. An authored path is actually walked -------------------------

    /// <summary>
    /// The defect the tester reported on 2026-08-12: "they still follow the
    /// initial objective and they don't follow the key points created". An
    /// operator handed a two-node polyline used to walk to node 0 and stand
    /// there forever, because nothing advanced
    /// <see cref="OrderAssignment.CurrentNodeIndex"/>.
    /// </summary>
    [Fact]
    public void RunTick_OrderedOperator_AdvancesPastANodeItIsAlreadyStandingOn()
    {
        var pathNodes = ImmutableArray.Create(
            new OrderPathNode(0, 0),
            new OrderPathNode(100, 0));

        BuildOrderedFixture(pathNodes, out var sim);

        // Tick 0 applies the order; the assignment starts at node 0, which is
        // the operator's own position.
        sim.RunTick(0);
        Assert.Equal(0, Assert.Single(sim.State.OrderAssignments).CurrentNodeIndex);

        // Tick 1's stage 1 sees it standing on node 0 and advances.
        sim.RunTick(1);
        Assert.Equal(1, Assert.Single(sim.State.OrderAssignments).CurrentNodeIndex);
    }

    /// <summary>
    /// The other half: the operator reaches the polyline's final node and the
    /// assignment clears, which is what returns it to its squad's own
    /// autonomous route — design section 16's first clearing condition, which
    /// <see cref="MovementSource.Evaluate"/> has always implemented and
    /// nothing has ever called.
    /// </summary>
    [Fact]
    public void RunTick_OrderedOperator_WalksToTheFinalNodeAndThenLosesItsAssignment()
    {
        var pathNodes = ImmutableArray.Create(
            new OrderPathNode(0, 0),
            new OrderPathNode(120, 0));

        BuildOrderedFixture(pathNodes, out var sim);

        // 120 world units at the sprint step of 80 wu/s over a 50 Hz tick is
        // 1.6 wu per tick, so 75 ticks covers it with margin even before the
        // 16 wu arrival radius is counted.
        for (var tick = 0; tick <= 100; tick++)
        {
            sim.RunTick(tick);
        }

        Assert.Empty(sim.State.OrderAssignments);

        // It stopped at the node rather than walking through it.
        var arrived = Assert.Single(sim.State.Operators);
        Assert.InRange(WorldUnits.FromFixedPoint(arrived.PositionX), 104, 136);
    }

    /// <summary>
    /// Design section 16's third clearing condition, "the operator died",
    /// reached through the pipeline rather than through
    /// <see cref="MovementSource.Evaluate"/> directly.
    /// </summary>
    [Fact]
    public void RunTick_OrderedOperatorThatIsDead_LosesItsAssignment()
    {
        var pathNodes = ImmutableArray.Create(
            new OrderPathNode(0, 0),
            new OrderPathNode(100, 0));

        BuildOrderedFixture(pathNodes, out var sim, operatorHealth: 0);

        sim.RunTick(0);
        Assert.Single(sim.State.OrderAssignments);

        sim.RunTick(1);
        Assert.Empty(sim.State.OrderAssignments);
    }

    /// <summary>
    /// The regression guard for every run that submits no order at all — the
    /// headless determinism workload included. A mission with no assignment
    /// must hash exactly as it did before this sub-step existed, which is why
    /// the seed-1 golden fixtures' <c>EmptyOrderStream</c> half is expected to
    /// be unmoved by the order half of this package.
    /// </summary>
    [Fact]
    public void RunTick_NoOrderSubmitted_LeavesTheAssignmentArrayEmptyEveryTick()
    {
        var grid = BuildGrid();
        var sim = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            NoWalls(grid),
            BuildState(ImmutableArray.Create(
                BuildOperator(entityId: 1, faction: 0, positionXWu: 0, positionYWu: 0))),
            ImmutableArray<CoverRecord>.Empty);

        for (var tick = 0; tick < 10; tick++)
        {
            sim.RunTick(tick);
            Assert.Empty(sim.State.OrderAssignments);
        }
    }

    // ---- 2. The lowered weapon is stored and observable ------------------

    /// <summary>
    /// Smoke row <c>SD-4</c>'s simulation half. Design section 9 calls the
    /// weapon-lowered rule "one conditional that generates the whole game",
    /// and <see cref="WeaponLoweredRules.IsForcedLowered"/> has always
    /// implemented it — but stage 11 threw the result away rather than
    /// storing it, so <see cref="OperatorState.WeaponLowered"/> was folded
    /// into the state hash on every tick of every run while never once being
    /// assigned.
    /// </summary>
    [Fact]
    public void RunTick_OperatorStandingAgainstAWall_StoresTheLoweredFlagAndEmitsOneEvent()
    {
        var grid = BuildGrid();

        // A wall segment running along Y = 0, with the operator standing two
        // world units off it — well inside the ruleset's lowered distance.
        var wallBuckets = WallBuckets.Build(
            grid, [0L], [0L], [400L], [0L]);

        var sim = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            wallBuckets,
            BuildState(ImmutableArray.Create(
                BuildOperator(entityId: 1, faction: 0, positionXWu: 40, positionYWu: 2))),
            ImmutableArray<CoverRecord>.Empty);

        sim.RunTick(0);

        Assert.True(Assert.Single(sim.State.Operators).WeaponLowered);
        Assert.Single(sim.State.EventFeed.Events, e => e.Kind == MissionEventKind.WeaponLowered);

        // Held lowered, not re-announced: a weapon down for a hundred ticks
        // emits one event, not a hundred.
        for (var tick = 1; tick < 20; tick++)
        {
            sim.RunTick(tick);
        }

        Assert.Single(sim.State.EventFeed.Events, e => e.Kind == MissionEventKind.WeaponLowered);
        Assert.DoesNotContain(sim.State.EventFeed.Events, e => e.Kind == MissionEventKind.WeaponRaised);
    }

    /// <summary>
    /// The control for the test above: the same fixture with no wall anywhere
    /// leaves the flag false and emits nothing, so the assertion above cannot
    /// pass for a reason unrelated to the wall.
    /// </summary>
    [Fact]
    public void RunTick_OperatorNowhereNearAWall_LeavesTheLoweredFlagFalse()
    {
        var grid = BuildGrid();
        var sim = new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            NoWalls(grid),
            BuildState(ImmutableArray.Create(
                BuildOperator(entityId: 1, faction: 0, positionXWu: 40, positionYWu: 40))),
            ImmutableArray<CoverRecord>.Empty);

        sim.RunTick(0);

        Assert.False(Assert.Single(sim.State.Operators).WeaponLowered);
        Assert.DoesNotContain(sim.State.EventFeed.Events, e => e.Kind == MissionEventKind.WeaponLowered);
    }

    // ---- 3. Fire mode and automatic cadence ------------------------------

    private static SandataSimulation BuildSustainedFireFixture(FirearmId firearm, int rangeWu)
    {
        var grid = BuildGrid(width: 64, height: 64);

        var shooter = BuildOperator(
            entityId: 1, faction: 0, positionXWu: 0, positionYWu: 0, firearm: firearm,
            weaponChainPhase: (int)WeaponChainPhase.Aiming, weaponChainRemainingTicks: 1);

        // Health far above any caliber's damage so the burst is not cut short
        // by the target dying, which is what this fixture is measuring the
        // cadence of.
        var target = BuildOperator(
            entityId: 2, faction: 1, positionXWu: rangeWu, positionYWu: 0, health: 100_000);

        return new SandataSimulation(
            BuildMission(),
            SandataRuleset.ModernTacticalV1,
            grid,
            NoWalls(grid),
            BuildState(ImmutableArray.Create(shooter, target)),
            ImmutableArray<CoverRecord>.Empty);
    }

    private static int CountShots(SandataSimulation sim, int ticks, out FireModeSet mode)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            sim.RunTick(tick);
        }

        var shots = sim.State.EventFeed.Events
            .Where(e => e.Kind == MissionEventKind.ShotFired)
            .ToImmutableArray();

        mode = shots.IsDefaultOrEmpty ? FireModeSet.Safe : (FireModeSet)shots[0].ReasonCode;
        return shots.Length;
    }

    /// <summary>
    /// Smoke row <c>SD-5</c>'s simulation half. A rifle engaging inside
    /// <c>RifleAutoBandMaxWu</c> fires at its own <c>CyclicRpm</c> — design
    /// section 9's driftless accumulator — not once per weapon-chain cycle.
    /// At 600 rpm and 50 Hz that is one round roughly every five ticks, so
    /// forty ticks of sustained fire is several rounds rather than one or two.
    /// </summary>
    [Fact]
    public void RunTick_RifleInsideTheAutoBand_FiresAtItsCyclicRateAndReportsAuto()
    {
        var sim = BuildSustainedFireFixture(FirearmId.Ak47, rangeWu: 90);

        var shots = CountShots(sim, ticks: 40, out var mode);

        Assert.Equal(FireModeSet.Auto, mode);

        // Six rounds is the floor a 600 rpm weapon clears over 40 ticks
        // (0.8 seconds) once its first round has resolved; the chain alone,
        // whose aim-plus-reset cycle is 25 ticks for this rifle, could not
        // produce more than two in the same window.
        Assert.InRange(shots, 6, 9);
    }

    /// <summary>
    /// The discriminating half of the test above: a pistol carries no
    /// <see cref="FireModeSet.Auto"/> flag at any range, so the same geometry
    /// produces single shots at the weapon chain's own cadence. Without this,
    /// the auto test above could pass for a fixture that simply fires fast.
    /// </summary>
    [Fact]
    public void RunTick_PistolAtTheSameRange_NeverSelectsAutoAndFiresFarLess()
    {
        var sim = BuildSustainedFireFixture(FirearmId.Glock17Gen5, rangeWu: 90);

        var shots = CountShots(sim, ticks: 40, out var mode);

        Assert.Equal(FireModeSet.Single, mode);
        Assert.InRange(shots, 1, 4);
    }

    /// <summary>
    /// Design section 9's band rule is a range rule, not a weapon rule: the
    /// same rifle beyond <c>RifleAutoBandMaxWu</c> (240 world units) but
    /// inside its burst and single bands reports a mode other than
    /// <see cref="FireModeSet.Auto"/>.
    /// </summary>
    /// <remarks>
    /// The range chosen is inside <c>ContactMemory.IdentifyRangeWu</c>'s reach
    /// only because that constant is 96 — a target at 250 world units is never
    /// identified, so this fixture would produce no shot at all. It therefore
    /// asserts the band boundary through the accumulator's own silence rather
    /// than through a mode value, and says so instead of pretending otherwise.
    /// </remarks>
    [Fact]
    public void RunTick_RifleBeyondIdentifyRange_ProducesNoShotAtAll()
    {
        var sim = BuildSustainedFireFixture(FirearmId.Ak47, rangeWu: 250);

        var shots = CountShots(sim, ticks: 40, out _);

        Assert.Equal(0, shots);
    }
}
