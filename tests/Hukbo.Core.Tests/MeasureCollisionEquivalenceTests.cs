using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Proof that the count-only collision metrics equal what the sorted pair list
/// produced. <see cref="CollisionUniformGrid.CountContacts"/> is compared with
/// <see cref="NaiveCollisionPairs"/>, and whole battles are compared tick by tick
/// with a test-only copy of the pair-list logic <c>MeasureCollision</c> used
/// before it counted, fed by the naive oracle rather than by the grid.
/// </summary>
/// <remarks>
/// The oracle here never calls <c>CountContacts</c>, <c>Rebuild</c> or any
/// production collision helper, and takes the deepest penetration from every
/// pair rather than from the closest one, so it does not share the shortcut
/// under test.
/// </remarks>
public sealed class MeasureCollisionEquivalenceTests
{
    private const int BodyRadiusRaw = 4 * FixedPoint.Scale;

    private const int DiameterRaw = 2 * BodyRadiusRaw;

    private const int MapDimensionRaw = 100 * FixedPoint.Scale;

    /// <summary>Bodies are scattered past the map edge to reach the dictionary path.</summary>
    private const int ScatterExtentRaw = 140 * FixedPoint.Scale;

    private const int ClusterExtentRaw = 40 * FixedPoint.Scale;

    private const int ScatterBodies = 300;

    private const int DeadBodyOneIn = 10;

    private const int BattleTicksSmall = 400;

    private const int BattleTicksLarge = 300;

    private const int SmallArmyAgents = 200;

    private const int LargeArmyAgents = 500;

    public static TheoryData<bool> BoundedAndUnbounded() => new() { true, false };

    /// <summary>
    /// Every registered movement preset that can run with the shipped combat
    /// preset. The equipment-relative ones (V6, V7, V9) register loadout
    /// profiles for only some weapons and refuse to build a battle with the
    /// others, so they cannot run here.
    /// </summary>
    public static TheoryData<MovementPresetId, ulong> PresetsAndSeeds()
    {
        var data = new TheoryData<MovementPresetId, ulong>();
        foreach (var preset in Enum.GetValues<MovementPresetId>())
        {
            if (!MovementPresetRegistry.IsRegistered(preset) ||
                MovementPresetRegistry.Get(preset).UsesEquipmentRelativeFootwork)
            {
                continue;
            }

            foreach (var seed in new[] { 1UL, 2UL, 3UL })
            {
                data.Add(preset, seed);
            }
        }

        return data;
    }

    public static TheoryData<MovementPresetId, ulong> LargeArmies() => new()
    {
        { MovementPresetId.EvasiveFootworkV14, 1UL },
        { MovementPresetId.EvasiveFootworkV14, 4UL },
        { MovementPresetId.SeparatingContactV17, 1UL },
        { MovementPresetId.SeparatingContactV17, 4UL },
    };

    [Theory]
    [MemberData(nameof(BoundedAndUnbounded))]
    public void CountContacts_MatchesTheNaiveOracleOnScatteredBodies(bool isBounded)
    {
        foreach (var seed in new[] { 1UL, 2UL, 3UL, 4UL, 5UL })
        {
            foreach (var extentRaw in new[] { ClusterExtentRaw, ScatterExtentRaw })
            {
                var bodies = ScatterBodiesFor(seed, extentRaw);
                var factions = FactionsFor(seed, bodies.Count);
                var grid = NewGrid(isBounded);

                grid.Clear();
                foreach (var body in bodies)
                {
                    grid.Insert(body);
                }

                var counts = grid.CountContacts(BodyRadiusRaw, factions);

                AssertMatchesOracle(counts, bodies, factions, $"seed {seed}, extent {extentRaw}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(BoundedAndUnbounded))]
    public void CountContacts_ReportsNoDistanceWhenThereAreNoPairs(bool isBounded)
    {
        var grid = NewGrid(isBounded);
        grid.Insert(new CollisionBody(1, 10 * FixedPoint.Scale, 10 * FixedPoint.Scale, true));
        grid.Insert(new CollisionBody(2, 60 * FixedPoint.Scale, 60 * FixedPoint.Scale, true));

        var counts = grid.CountContacts(BodyRadiusRaw, [0, 0, 1]);

        Assert.Equal(new CollisionContactCounts(0, 0, null), counts);
    }

    [Theory]
    [MemberData(nameof(BoundedAndUnbounded))]
    public void CountContacts_VisitsACellOnceAfterARemoveAndReinsert(bool isBounded)
    {
        var grid = NewGrid(isBounded);
        var first = new CollisionBody(1, 10 * FixedPoint.Scale, 10 * FixedPoint.Scale, true);
        var second = new CollisionBody(2, (10 * FixedPoint.Scale) + BodyRadiusRaw, 10 * FixedPoint.Scale, true);
        var third = new CollisionBody(3, 10 * FixedPoint.Scale, (10 * FixedPoint.Scale) + BodyRadiusRaw, true);

        // Emptying the cell leaves its dense head unset, so the refill records
        // the cell's key a second time.
        foreach (var body in new[] { first, second, third })
        {
            grid.Insert(body);
        }

        foreach (var body in new[] { first, second, third })
        {
            grid.Remove(body);
        }

        foreach (var body in new[] { first, second, third })
        {
            grid.Insert(body);
        }

        var counts = grid.CountContacts(BodyRadiusRaw, [0, 0, 1, 0]);

        Assert.Equal(3, counts.PairCount);
        Assert.Equal(2, counts.CrossFactionPairCount);
        Assert.Equal((long)BodyRadiusRaw * BodyRadiusRaw, counts.MinimumSquaredDistance);
    }

    [Theory]
    [MemberData(nameof(BoundedAndUnbounded))]
    public void CountContacts_OnAGridThatLostBodiesMatchesTheOracleOnTheSurvivors(bool isBounded)
    {
        var bodies = ScatterBodiesFor(seed: 9, ClusterExtentRaw);
        var factions = FactionsFor(9, bodies.Count);
        var grid = NewGrid(isBounded);
        var survivors = new List<CollisionBody>();

        foreach (var body in bodies)
        {
            grid.Insert(body);
        }

        for (var index = 0; index < bodies.Count; index++)
        {
            if (index % 3 == 0)
            {
                grid.Remove(bodies[index]);
                grid.Insert(bodies[index]);
            }

            if (index % 4 == 0)
            {
                grid.Remove(bodies[index]);
                continue;
            }

            survivors.Add(bodies[index]);
        }

        AssertMatchesOracle(
            grid.CountContacts(BodyRadiusRaw, factions),
            survivors,
            factions,
            "after removals");
    }

    [Theory]
    [MemberData(nameof(PresetsAndSeeds))]
    public void LastTickCollision_MatchesThePairListOracleOnEveryTick(
        MovementPresetId movementPreset,
        ulong seed)
    {
        var scenario = ShippedCombatScenario(seed, SmallArmyAgents, BattleTicksSmall) with
        {
            MovementPreset = movementPreset,
        };

        var run = AssertEveryTickMatchesTheOracle(scenario, BattleTicksSmall);

        Assert.True(run.TicksWithCandidatePairs > 0, $"{movementPreset} never had a candidate pair.");
    }

    [Theory]
    [MemberData(nameof(LargeArmies))]
    public void LastTickCollision_MatchesThePairListOracleForLargeArmies(
        MovementPresetId movementPreset,
        ulong seed)
    {
        var scenario = ShippedCombatScenario(seed, LargeArmyAgents, BattleTicksLarge) with
        {
            MovementPreset = movementPreset,
        };

        var run = AssertEveryTickMatchesTheOracle(scenario, BattleTicksLarge);

        Assert.True(run.TicksWithContactPairs > 0, $"{movementPreset} never had a contact pair.");
    }

    [Fact]
    public void LastTickCollision_ExercisesNonzeroPenetrationUnderSeparatingContact()
    {
        var scenario = ShippedCombatScenario(1, SmallArmyAgents, BattleTicksSmall) with
        {
            MovementPreset = MovementPresetId.SeparatingContactV17,
        };

        var run = AssertEveryTickMatchesTheOracle(scenario, BattleTicksSmall);

        Assert.True(run.TicksWithPenetration > 0, "No tick ever measured a penetration.");
    }

    private static Scenario ShippedCombatScenario(ulong seed, int totalAgents, int ticks) =>
        Scenario.CreateDefault(seed, totalAgents) with
        {
            TickLimit = ticks,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV8,
        };

    private static CollisionUniformGrid NewGrid(bool isBounded) =>
        isBounded
            ? new CollisionUniformGrid(DiameterRaw, MapDimensionRaw, MapDimensionRaw)
            : new CollisionUniformGrid(DiameterRaw);

    private static List<CollisionBody> ScatterBodiesFor(ulong seed, int extentRaw)
    {
        var random = new SplitMix64(seed);
        var bodies = new List<CollisionBody>(ScatterBodies);

        for (var index = 0; index < ScatterBodies; index++)
        {
            bodies.Add(
                new CollisionBody(
                    (ulong)(index + 1),
                    random.NextInt(extentRaw),
                    random.NextInt(extentRaw),
                    random.NextInt(DeadBodyOneIn) != 0));
        }

        return bodies;
    }

    private static int[] FactionsFor(ulong seed, int bodyCount)
    {
        var random = new SplitMix64(seed ^ 0xFAC7UL);
        var factions = new int[bodyCount + 1];

        for (var index = 1; index <= bodyCount; index++)
        {
            factions[index] = random.NextInt(2);
        }

        return factions;
    }

    private static void AssertMatchesOracle(
        CollisionContactCounts counts,
        IReadOnlyList<CollisionBody> bodies,
        int[] factions,
        string label)
    {
        var pairs = NaiveCollisionPairs.Enumerate(bodies, BodyRadiusRaw);
        var byId = bodies.ToDictionary(body => body.EntityId);
        var crossFaction = pairs.Count(
            pair => factions[(int)pair.LowEntityId] != factions[(int)pair.HighEntityId]);
        long? minimum = pairs.Count == 0
            ? null
            : pairs.Min(pair => SquaredDistance(byId[pair.LowEntityId], byId[pair.HighEntityId]));

        Assert.True(pairs.Count > 0, $"{label}: the scatter produced no pair, so it proves nothing.");
        Assert.Equal(pairs.Count, counts.PairCount);
        Assert.Equal(crossFaction, counts.CrossFactionPairCount);
        Assert.Equal(minimum, counts.MinimumSquaredDistance);
    }

    private static long SquaredDistance(CollisionBody left, CollisionBody right)
    {
        var deltaX = (long)right.XRaw - left.XRaw;
        var deltaY = (long)right.YRaw - left.YRaw;

        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    /// <summary>Exact floor square root, written out so no production helper is shared.</summary>
    private static long FloorSquareRoot(long value)
    {
        var root = (long)Math.Sqrt(value);

        while (root * root > value)
        {
            root--;
        }

        while ((root + 1) * (root + 1) <= value)
        {
            root++;
        }

        return root;
    }

    /// <summary>
    /// The pair-list <c>MeasureCollision</c> logic as it stood before it counted:
    /// every pair within the band, the cross-faction subset, and the deepest
    /// <c>2R - floor(sqrt(d^2))</c> over all pairs from a floor of zero.
    /// </summary>
    private static (int CandidatePairs, int ContactPairs, int PenetrationRaw) PairListOracle(
        IReadOnlyList<AgentView> positions,
        IReadOnlyList<bool> wasAlive,
        Scenario scenario)
    {
        var bandRadiusRaw = scenario.BodyRadiusRaw + (scenario.MovementSpeedRaw / 2);
        var bodies = new List<CollisionBody>(positions.Count);
        var byId = new Dictionary<ulong, AgentView>();

        for (var index = 0; index < positions.Count; index++)
        {
            var agent = positions[index];

            byId[agent.EntityId] = agent;
            bodies.Add(new CollisionBody(agent.EntityId, agent.XRaw, agent.YRaw, wasAlive[index]));
        }

        var contactPairs = 0;
        var penetrationRaw = 0;
        var pairs = NaiveCollisionPairs.Enumerate(bodies, bandRadiusRaw);

        foreach (var pair in pairs)
        {
            var left = byId[pair.LowEntityId];
            var right = byId[pair.HighEntityId];
            var squared = (((long)right.XRaw - left.XRaw) * ((long)right.XRaw - left.XRaw)) +
                (((long)right.YRaw - left.YRaw) * ((long)right.YRaw - left.YRaw));

            penetrationRaw = Math.Max(
                penetrationRaw,
                (2 * scenario.BodyRadiusRaw) - (int)FloorSquareRoot(squared));

            if (left.FactionId != right.FactionId)
            {
                contactPairs++;
            }
        }

        return (pairs.Count, contactPairs, penetrationRaw);
    }

    /// <summary>
    /// Runs the battle and compares the measured pairs, contacts and penetration
    /// with the oracle after every tick. <c>MeasureCollision</c> runs before the
    /// attack stage, so the bodies it saw are the ones alive at the end of the
    /// previous tick, at this tick's committed positions.
    /// </summary>
    private static RunSummary AssertEveryTickMatchesTheOracle(Scenario scenario, int ticks)
    {
        scenario.Validate();
        var simulation = BattleSimulation.Create(scenario);
        var wasAlive = simulation.Agents.Select(agent => agent.IsAlive).ToArray();
        var withCandidates = 0;
        var withContacts = 0;
        var withPenetration = 0;

        for (var tick = 1; tick <= ticks && simulation.Outcome == BattleOutcome.Ongoing; tick++)
        {
            simulation.AdvanceOneTick();

            var expected = PairListOracle(simulation.Agents, wasAlive, scenario);
            var actual = simulation.LastTickCollision;

            Assert.True(
                expected == (actual.CandidatePairs, actual.ContactPairs, actual.PenetrationRaw),
                $"{scenario.MovementPreset} seed {scenario.Seed} tick {tick}: " +
                $"oracle {expected}, measured " +
                $"({actual.CandidatePairs}, {actual.ContactPairs}, {actual.PenetrationRaw}).");

            withCandidates += expected.CandidatePairs > 0 ? 1 : 0;
            withContacts += expected.ContactPairs > 0 ? 1 : 0;
            withPenetration += expected.PenetrationRaw > 0 ? 1 : 0;
            wasAlive = simulation.Agents.Select(agent => agent.IsAlive).ToArray();
        }

        return new RunSummary(withCandidates, withContacts, withPenetration);
    }

    private readonly record struct RunSummary(
        int TicksWithCandidatePairs,
        int TicksWithContactPairs,
        int TicksWithPenetration);
}
