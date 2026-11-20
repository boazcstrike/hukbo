using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Differential tests for the ring-search nearest-enemy grid. The grid may only
/// ever be a faster way of computing what <see cref="NaiveNearestEnemy.Find"/>
/// already computes, so every test asserts exact equality against that oracle
/// over scenarios built from <see cref="SplitMix64"/>, never against a
/// hand-written expectation, apart from the one handcrafted tie case that
/// exists to pin the strict stop bound. Design:
/// <c>docs/plans/2026-11-21-ring-search-targeting-design.md</c> section 6.
/// </summary>
public sealed class TargetingGridTests
{
    private const int UnitRaw = FixedPoint.Scale;

    private const int MapUnits = 1_280;

    private const int WholeMapPerceptionRaw = 2_048 * UnitRaw;

    private const int SeedCount = 12;

    /// <summary>
    /// Stands in for "this enemy's weapon is melee" so the second minimum the
    /// simulation tracks for ranged actors is exercised too.
    /// </summary>
    private static bool IsThreat(AgentState candidate) => candidate.EntityId % 2 == 1;

    public static TheoryData<int, int> SeedsAndCellSizes()
    {
        var data = new TheoryData<int, int>();
        foreach (var cellSizeRaw in new[]
        {
            UnitRaw,
            8 * UnitRaw,
            TargetingGrid.DefaultCellSizeRaw,
            1_000 * UnitRaw,
        })
        {
            for (var seed = 1; seed <= SeedCount; seed++)
            {
                data.Add(seed, cellSizeRaw);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SeedsAndCellSizes))]
    public void RandomScatter_MatchesTheOracle(int seed, int cellSizeRaw)
    {
        var random = new SplitMix64((ulong)seed);
        var agents = CreateScatter(ref random, 150, 0, MapUnits, WholeMapPerceptionRaw);

        AssertMatchesOracle(agents, cellSizeRaw);
    }

    [Theory]
    [MemberData(nameof(SeedsAndCellSizes))]
    public void LatticeWithManyEqualDistances_MatchesTheOracle(int seed, int cellSizeRaw)
    {
        var random = new SplitMix64((ulong)seed + 100UL);
        var agents = new List<AgentState>();
        for (var index = 0; index < 150; index++)
        {
            // Whole 4-unit steps on a 16 x 16 lattice, so many candidates sit at
            // exactly the same squared distance from an actor.
            agents.Add(CreateAgent(
                (ulong)(index + 1),
                random.NextInt(2),
                random.NextInt(16) * 4 * UnitRaw,
                random.NextInt(16) * 4 * UnitRaw,
                WholeMapPerceptionRaw));
        }

        AssertMatchesOracle(agents.ToArray(), cellSizeRaw);
    }

    [Theory]
    [MemberData(nameof(SeedsAndCellSizes))]
    public void NegativeAndUnclampedCoordinates_MatchTheOracle(int seed, int cellSizeRaw)
    {
        var random = new SplitMix64((ulong)seed + 200UL);
        var agents = CreateScatter(ref random, 150, -600, 600, WholeMapPerceptionRaw);

        AssertMatchesOracle(agents, cellSizeRaw);
    }

    [Theory]
    [MemberData(nameof(SeedsAndCellSizes))]
    public void PerceptionSmallerThanTheMap_MatchesTheOracle(int seed, int cellSizeRaw)
    {
        var random = new SplitMix64((ulong)seed + 300UL);
        var agents = CreateScatter(ref random, 150, 0, MapUnits, 25 * UnitRaw);

        AssertMatchesOracle(agents, cellSizeRaw);
    }

    [Theory]
    [MemberData(nameof(SeedsAndCellSizes))]
    public void DeadAgents_AreNeverSelectedAndNeverStretchTheBounds(int seed, int cellSizeRaw)
    {
        var random = new SplitMix64((ulong)seed + 400UL);
        var agents = CreateScatter(ref random, 150, -200, 900, WholeMapPerceptionRaw);
        foreach (var agent in agents.Where(_ => random.NextInt(10) < 4))
        {
            agent.HitPoints = 0;
        }

        AssertMatchesOracle(agents, cellSizeRaw);
    }

    [Theory]
    [MemberData(nameof(SeedsAndCellSizes))]
    public void CoincidentAgents_TieOnTheLowerEntityId(int seed, int cellSizeRaw)
    {
        var random = new SplitMix64((ulong)seed + 500UL);
        var spots = new (int X, int Y)[6];
        for (var spot = 0; spot < spots.Length; spot++)
        {
            spots[spot] = (random.NextInt(MapUnits) * UnitRaw, random.NextInt(MapUnits) * UnitRaw);
        }

        var agents = new List<AgentState>();
        for (var index = 0; index < 120; index++)
        {
            var spot = spots[random.NextInt(spots.Length)];
            agents.Add(CreateAgent(
                (ulong)(index + 1),
                random.NextInt(2),
                spot.X,
                spot.Y,
                WholeMapPerceptionRaw));
        }

        AssertMatchesOracle(agents.ToArray(), cellSizeRaw);
    }

    [Theory]
    [MemberData(nameof(SeedsAndCellSizes))]
    public void ArmiesInSeparateHalves_QueriedFromOutsideTheEnemyBounds_MatchTheOracle(
        int seed,
        int cellSizeRaw)
    {
        var random = new SplitMix64((ulong)seed + 600UL);
        var agents = new List<AgentState>();
        for (var index = 0; index < 150; index++)
        {
            var factionId = random.NextInt(2);
            var halfStart = factionId == 0 ? 0 : MapUnits * 3 / 4;
            agents.Add(CreateAgent(
                (ulong)(index + 1),
                factionId,
                (halfStart + random.NextInt(MapUnits / 4)) * UnitRaw,
                random.NextInt(MapUnits / 2) * UnitRaw,
                WholeMapPerceptionRaw));
        }

        AssertMatchesOracle(agents.ToArray(), cellSizeRaw);
    }

    [Fact]
    public void EmptyEnemyFaction_FindsNothingAndVisitsNothing()
    {
        var agents = new[]
        {
            CreateAgent(1, 0, 10 * UnitRaw, 10 * UnitRaw, WholeMapPerceptionRaw),
            CreateAgent(2, 0, 30 * UnitRaw, 40 * UnitRaw, WholeMapPerceptionRaw),
        };
        var enemyGrid = new TargetingGrid();

        enemyGrid.Rebuild(agents, 1);

        Assert.True(enemyGrid.IsEmpty);
        var visitor = new NearestEnemyVisitor(agents[0], observesThreats: true);
        enemyGrid.Search(agents[0].XRaw, agents[0].YRaw, ref visitor);
        Assert.Null(visitor.Target);
        Assert.Equal(long.MaxValue, visitor.Selected);
    }

    [Fact]
    public void EnemyFactionWhollyDead_IsEmptyLikeAnAbsentOne()
    {
        var enemy = CreateAgent(2, 1, 50 * UnitRaw, 50 * UnitRaw, WholeMapPerceptionRaw);
        enemy.HitPoints = 0;
        var agents = new[]
        {
            CreateAgent(1, 0, 10 * UnitRaw, 10 * UnitRaw, WholeMapPerceptionRaw),
            enemy,
        };
        var enemyGrid = new TargetingGrid();

        enemyGrid.Rebuild(agents, 1);

        Assert.True(enemyGrid.IsEmpty);
    }

    /// <summary>
    /// The case the strict stop bound exists for. With 1-unit cells and the
    /// actor at the corner of its cell, the enemy with the higher entity ID at
    /// (3, 4) sits in ring 4 and the enemy with the lower entity ID at (5, 0)
    /// sits in ring 5, both at squared distance 25. Ring 5's lower bound,
    /// (5 - 1) * 1 unit, squared, is 16, below the best of 25, so the search
    /// must go on into ring 5 and pick the lower entity ID.
    /// </summary>
    [Fact]
    public void EqualDistanceEnemyInAFartherRing_WithALowerEntityId_Wins()
    {
        var actor = CreateAgent(10, 0, 0, 0, WholeMapPerceptionRaw);
        var nearRingHigherId = CreateAgent(9, 1, 3 * UnitRaw, 4 * UnitRaw, WholeMapPerceptionRaw);
        var farRingLowerId = CreateAgent(2, 1, 5 * UnitRaw, 0, WholeMapPerceptionRaw);
        var agents = new[] { farRingLowerId, nearRingHigherId, actor };
        var enemyGrid = new TargetingGrid(UnitRaw);
        enemyGrid.Rebuild(agents, 1);

        var visitor = new NearestEnemyVisitor(actor, observesThreats: false);
        enemyGrid.Search(actor.XRaw, actor.YRaw, ref visitor);

        Assert.Equal(25L * UnitRaw * UnitRaw, visitor.Selected);
        Assert.Equal(2UL, visitor.Target);
    }

    [Fact]
    public void WideBoundingBox_GrowsTheCellSideToStayWithinTheCellCap()
    {
        var agents = new[]
        {
            CreateAgent(1, 1, 0, 0, WholeMapPerceptionRaw),
            CreateAgent(2, 1, MapUnits * UnitRaw, 0, WholeMapPerceptionRaw),
        };
        var grid = new TargetingGrid(UnitRaw);

        grid.Rebuild(agents, 1);

        // 1,280 units at 1-unit cells would need 1,281 cells on one axis. The
        // smallest integer factor that fits 256 is 6: 1,280 / 6 + 1 = 214,
        // where a factor of 5 would still need 257.
        Assert.Equal(6L * UnitRaw, grid.CellSizeRaw);
    }

    [Fact]
    public void RebuildingOneGridAcrossDifferentScenarios_StaysExact()
    {
        var grid = new TargetingGrid(8 * UnitRaw);
        foreach (var (seed, count) in new[] { (1, 200), (2, 7), (3, 90), (4, 1), (5, 300) })
        {
            var random = new SplitMix64((ulong)seed + 700UL);
            var agents = CreateScatter(ref random, count, -300, 700, WholeMapPerceptionRaw);

            AssertMatchesOracleAfterRebuilding(agents, grid);
        }
    }

    [Theory]
    [MemberData(nameof(SeedsAndCellSizes))]
    public void AnUnstoppedSearch_VisitsEveryLivingEnemyExactlyOnce(int seed, int cellSizeRaw)
    {
        var random = new SplitMix64((ulong)seed + 800UL);
        var agents = CreateScatter(ref random, 150, -300, 700, WholeMapPerceptionRaw);
        foreach (var agent in agents.Where(_ => random.NextInt(10) < 3))
        {
            agent.HitPoints = 0;
        }

        var grids = BuildGrids(agents, cellSizeRaw);
        foreach (var actor in agents)
        {
            var visitor = new CountingVisitor(agents.Length);
            grids[1 - actor.FactionId].Search(actor.XRaw, actor.YRaw, ref visitor);

            var expected = agents
                .Where(candidate => candidate.IsAlive && candidate.FactionId != actor.FactionId)
                .Select(candidate => candidate.EntityId)
                .Order()
                .ToArray();
            Assert.Equal(expected, visitor.VisitedEntityIds.Order().ToArray());
        }
    }

    private static void AssertMatchesOracle(AgentState[] agents, int cellSizeRaw) =>
        AssertMatchesOracle(agents, BuildGrids(agents, cellSizeRaw));

    /// <summary>
    /// Rebuilds the one <paramref name="grid"/> for each faction in turn, so
    /// leftover contents from an earlier, larger scenario would show up as a
    /// wrong answer.
    /// </summary>
    private static void AssertMatchesOracleAfterRebuilding(AgentState[] agents, TargetingGrid grid)
    {
        foreach (var enemyFactionId in new[] { 1, 0 })
        {
            grid.Rebuild(agents, enemyFactionId);
            foreach (var actor in agents.Where(
                agent => agent.IsAlive && agent.FactionId != enemyFactionId))
            {
                AssertActorMatchesOracle(agents, actor, grid);
            }
        }
    }

    private static void AssertMatchesOracle(AgentState[] agents, TargetingGrid[] grids)
    {
        foreach (var actor in agents.Where(agent => agent.IsAlive))
        {
            AssertActorMatchesOracle(agents, actor, grids[1 - actor.FactionId]);
        }
    }

    private static void AssertActorMatchesOracle(
        AgentState[] agents,
        AgentState actor,
        TargetingGrid enemyGrid)
    {
        var expected = NaiveNearestEnemy.Find(agents, actor, IsThreat);

        var visitor = new NearestEnemyVisitor(actor, observesThreats: true);
        enemyGrid.Search(actor.XRaw, actor.YRaw, ref visitor);

        var context =
            $"actor {actor.EntityId} at ({actor.XRaw}, {actor.YRaw}), " +
            $"cell {enemyGrid.CellSizeRaw}, {agents.Length} agents";
        Assert.True(expected.TargetEntityId == visitor.Target, context);
        Assert.True(expected.SelectedDistance == visitor.Selected, context);
        Assert.True(expected.NearestThreatDistance == visitor.Threat, context);

        var targetOnly = new NearestEnemyVisitor(actor, observesThreats: false);
        enemyGrid.Search(actor.XRaw, actor.YRaw, ref targetOnly);
        Assert.True(expected.TargetEntityId == targetOnly.Target, context);
        Assert.True(expected.SelectedDistance == targetOnly.Selected, context);
    }

    private static TargetingGrid[] BuildGrids(AgentState[] agents, int cellSizeRaw)
    {
        var grids = new[] { new TargetingGrid(cellSizeRaw), new TargetingGrid(cellSizeRaw) };
        grids[0].Rebuild(agents, 0);
        grids[1].Rebuild(agents, 1);
        return grids;
    }

    private static AgentState[] CreateScatter(
        ref SplitMix64 random,
        int count,
        int minUnits,
        int maxUnits,
        int perceptionRaw)
    {
        var agents = new AgentState[count];
        var span = maxUnits - minUnits;
        for (var index = 0; index < count; index++)
        {
            // A sub-unit fraction keeps positions off any cell boundary.
            agents[index] = CreateAgent(
                (ulong)(index + 1),
                random.NextInt(2),
                (minUnits * UnitRaw) + (random.NextInt(span) * UnitRaw) + random.NextInt(UnitRaw),
                (minUnits * UnitRaw) + (random.NextInt(span) * UnitRaw) + random.NextInt(UnitRaw),
                perceptionRaw);
        }

        return agents;
    }

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        int perceptionRangeRaw) =>
        new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            maximumHitPoints: 100,
            movementSpeedRaw: UnitRaw / 2,
            perceptionRangeRaw: perceptionRangeRaw,
            attackRangeRaw: UnitRaw,
            damagePerAttack: 10,
            attackCooldownTicks: 1,
            new CombatLoadout(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None));

    /// <summary>
    /// The selection rule written out independently of the simulation: nearest
    /// perceivable enemy, ties on the lower entity ID, plus a plain minimum over
    /// the enemies <see cref="IsThreat"/> accepts.
    /// </summary>
    private struct NearestEnemyVisitor : ITargetingVisitor
    {
        private readonly AgentState _actor;
        private readonly long _perceptionSquared;
        private readonly bool _observesThreats;

        internal NearestEnemyVisitor(AgentState actor, bool observesThreats)
        {
            _actor = actor;
            _perceptionSquared = (long)actor.PerceptionRangeRaw * actor.PerceptionRangeRaw;
            _observesThreats = observesThreats;
            Target = null;
            Selected = long.MaxValue;
            Threat = long.MaxValue;
        }

        internal ulong? Target { get; private set; }

        internal long Selected { get; private set; }

        internal long Threat { get; private set; }

        public void Visit(AgentState candidate)
        {
            var deltaX = (long)candidate.XRaw - _actor.XRaw;
            var deltaY = (long)candidate.YRaw - _actor.YRaw;
            var distance = (deltaX * deltaX) + (deltaY * deltaY);
            if (distance > _perceptionSquared)
            {
                return;
            }

            if (_observesThreats && IsThreat(candidate) && distance < Threat)
            {
                Threat = distance;
            }

            if (distance < Selected ||
                (distance == Selected && candidate.EntityId < Target))
            {
                Target = candidate.EntityId;
                Selected = distance;
            }
        }

        public readonly bool CanStopBefore(long lowerBoundSquared) =>
            lowerBoundSquared > _perceptionSquared ||
            (lowerBoundSquared > Selected &&
                (!_observesThreats || lowerBoundSquared > Threat));
    }

    /// <summary>A visitor that never stops the search and records every visit.</summary>
    private readonly struct CountingVisitor : ITargetingVisitor
    {
        internal CountingVisitor(int capacity) =>
            VisitedEntityIds = new List<ulong>(capacity);

        internal List<ulong> VisitedEntityIds { get; }

        public void Visit(AgentState candidate) => VisitedEntityIds.Add(candidate.EntityId);

        public bool CanStopBefore(long lowerBoundSquared) => false;
    }
}
