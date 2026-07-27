using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

public sealed class DeterminismTests
{
    [Fact]
    public void IndependentSameSeedRunsProduceIdenticalEventsAndStateHashes()
    {
        var scenario = Scenario.CreateDefault(seed: 0xDEADBEEF, totalAgents: 200);
        var left = BattleSimulation.Create(scenario);
        var right = BattleSimulation.Create(scenario);
        var sawAttackEvent = false;

        for (var tick = 0; tick < 2_000 && left.Outcome == BattleOutcome.Ongoing; tick++)
        {
            left.AdvanceOneTick();
            right.AdvanceOneTick();

            Assert.Equal(left.Tick, right.Tick);
            Assert.Equal(left.Outcome, right.Outcome);
            Assert.Equal(left.LastEvents, right.LastEvents);
            Assert.Equal(left.ComputeStateHash(), right.ComputeStateHash());

            foreach (var battleEvent in left.LastEvents)
            {
                if (battleEvent.Kind != BattleEventKind.Attack)
                {
                    Assert.Null(battleEvent.Weapon);
                    Assert.Null(battleEvent.HitLocation);
                    continue;
                }

                sawAttackEvent = true;
                Assert.NotNull(battleEvent.Weapon);
                Assert.NotNull(battleEvent.HitLocation);
            }
        }

        Assert.NotEqual(0UL, left.ComputeStateHash());
        Assert.NotEqual(BattleOutcome.Ongoing, left.Outcome);
        Assert.True(sawAttackEvent, "Expected at least one Attack event across the run.");
    }

    [Fact]
    public void PhilippinePresetContentHash_IsStableAcrossIndependentRegistryLookups()
    {
        var first = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV1);
        var second = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV1);

        Assert.Equal(0x59FB4CA563D87A49UL, first.ContentHash);
        Assert.Equal(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void StateHash_ChangesWhenAnyAgentWeaponArmorOrShieldChanges()
    {
        var scenario = Scenario.CreateDefault(seed: 5, totalAgents: 2);

        var baseline = ComputeSingleAgentStateHash(
            scenario,
            new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.None));
        var weaponChanged = ComputeSingleAgentStateHash(
            scenario,
            new CombatLoadout(WeaponId.Bolo, ArmorId.LightOrganic, ShieldId.None));
        var armorChanged = ComputeSingleAgentStateHash(
            scenario,
            new CombatLoadout(WeaponId.GreatBlade, (ArmorId)99, ShieldId.None));
        var shieldChanged = ComputeSingleAgentStateHash(
            scenario,
            new CombatLoadout(WeaponId.GreatBlade, ArmorId.LightOrganic, ShieldId.TallHardwood));

        Assert.NotEqual(baseline, weaponChanged);
        Assert.NotEqual(baseline, armorChanged);
        Assert.NotEqual(baseline, shieldChanged);
        Assert.NotEqual(weaponChanged, armorChanged);
        Assert.NotEqual(weaponChanged, shieldChanged);
        Assert.NotEqual(armorChanged, shieldChanged);
    }

    [Fact]
    public void StateHash_ChangesWhenTheScenarioBodyRadiusChanges()
    {
        var loadout = new CombatLoadout(
            WeaponId.GreatBlade,
            ArmorId.LightOrganic,
            ShieldId.None);
        // The step is lowered once up front so that halving the radius still
        // satisfies the tunneling guard, leaving the radius as the only
        // difference between the two hashed scenarios.
        var scenario = Scenario.CreateDefault(seed: 5, totalAgents: 2) with
        {
            MovementSpeedRaw = FixedPoint.Scale,
        };
        var narrowerBodies = scenario with
        {
            BodyRadiusRaw = scenario.BodyRadiusRaw / 2,
        };

        var baseline = ComputeSingleAgentStateHash(scenario, loadout);
        var changed = ComputeSingleAgentStateHash(narrowerBodies, loadout);

        scenario.Validate();
        narrowerBodies.Validate();
        Assert.NotEqual(scenario.BodyRadiusRaw, narrowerBodies.BodyRadiusRaw);
        Assert.NotEqual(baseline, changed);
    }

    [Fact]
    public void StateHash_ChangesWhenTheScenarioCollisionPolicyChanges()
    {
        // The cast value is deliberately outside the approved contract: Solid is
        // the only policy Validate accepts. StateHasher does not validate, so an
        // unapproved value is the only way to prove the field reaches the hash.
        var loadout = new CombatLoadout(
            WeaponId.GreatBlade,
            ArmorId.LightOrganic,
            ShieldId.None);
        var scenario = Scenario.CreateDefault(seed: 5, totalAgents: 2);
        var unapprovedPolicy = scenario with
        {
            CollisionPolicy = (CollisionPolicy)1,
        };

        var baseline = ComputeSingleAgentStateHash(scenario, loadout);
        var changed = ComputeSingleAgentStateHash(unapprovedPolicy, loadout);

        Assert.Equal(CollisionPolicy.Solid, scenario.CollisionPolicy);
        Assert.NotEqual(baseline, changed);
    }

    [Fact]
    public void StateHashChangesWhenTheLastStandThresholdChanges()
    {
        var loadout = new CombatLoadout(
            WeaponId.GreatBlade,
            ArmorId.LightOrganic,
            ShieldId.None);
        var scenario = Scenario.CreateDefault(seed: 5, totalAgents: 2) with
        {
            LastStandThresholdAgents = 0,
        };
        var thresholdChanged = scenario with
        {
            LastStandThresholdAgents = 6,
        };

        var baseline = ComputeSingleAgentStateHash(scenario, loadout);
        var changed = ComputeSingleAgentStateHash(thresholdChanged, loadout);

        Assert.NotEqual(baseline, changed);
    }

    private static ulong ComputeSingleAgentStateHash(
        Scenario scenario,
        CombatLoadout loadout)
    {
        var agent = new AgentState(
            entityId: 1,
            factionId: 0,
            xRaw: 0,
            yRaw: 0,
            maximumHitPoints: scenario.MaximumHitPoints,
            movementSpeedRaw: scenario.MovementSpeedRaw,
            perceptionRangeRaw: scenario.PerceptionRangeRaw,
            attackRangeRaw: scenario.AttackRangeRaw,
            damagePerAttack: scenario.DamagePerAttack,
            attackCooldownTicks: scenario.AttackCooldownTicks,
            loadout: loadout);

        return StateHasher.Compute(
            scenario,
            tick: 1,
            BattleOutcome.Ongoing,
            eventSequence: 0,
            agents: [agent]);
    }

    /// <summary>
    /// Acceptance row <c>Determinism</c>: two independent runs of one seed agree
    /// on the ordered event stream and on the state hash at <em>every</em> tick,
    /// not merely at the end. Comparing only the final state would let a
    /// divergence that cancels itself out pass unnoticed.
    /// </summary>
    [Fact]
    public void TwoIndependentSameSeedRunsAgreeOnOrderedEventsAndStateHashEveryTick()
    {
        var scenario = Scenario.CreateDefault(seed: 11, totalAgents: 60);
        var left = BattleSimulation.Create(scenario);
        var right = BattleSimulation.Create(scenario);

        Assert.Equal(left.ComputeStateHash(), right.ComputeStateHash());

        while (left.Outcome == BattleOutcome.Ongoing)
        {
            left.AdvanceOneTick();
            right.AdvanceOneTick();

            if (!left.LastEvents.SequenceEqual(right.LastEvents))
            {
                Assert.Fail(
                    $"Ordered events first diverged at tick {left.Tick}: the first " +
                    $"run emitted {left.LastEvents.Count} events and the second " +
                    $"emitted {right.LastEvents.Count}.");
            }

            var leftHash = left.ComputeStateHash();
            var rightHash = right.ComputeStateHash();

            if (leftHash != rightHash)
            {
                Assert.Fail(
                    $"State hash first diverged at tick {left.Tick}: " +
                    $"0x{leftHash:X16} against 0x{rightHash:X16}.");
            }
        }

        Assert.Equal(left.Tick, right.Tick);
        Assert.Equal(left.Outcome, right.Outcome);
        Assert.NotEqual(BattleOutcome.Ongoing, left.Outcome);
    }

    /// <summary>
    /// Task 7 coverage: the same lockstep, every-tick comparison as
    /// <see cref="TwoIndependentSameSeedRunsAgreeOnOrderedEventsAndStateHashEveryTick"/>,
    /// but with the last-stand formation explicitly active, so a divergence
    /// introduced by the rally-agent scan, the aim-point movement, or the
    /// new <see cref="AgentIntent.Regrouping"/> hash input would be caught
    /// here even if it cancelled out by the final tick.
    /// </summary>
    [Fact]
    public void TheSameSeedProducesIdenticalHashesAndEventsWithTheLastStandActive()
    {
        var scenario = Scenario.CreateDefault(seed: 3, totalAgents: 40) with
        {
            LastStandThresholdAgents = 6,
        };
        var left = BattleSimulation.Create(scenario);
        var right = BattleSimulation.Create(scenario);

        Assert.Equal(left.ComputeStateHash(), right.ComputeStateHash());

        while (left.Outcome == BattleOutcome.Ongoing)
        {
            left.AdvanceOneTick();
            right.AdvanceOneTick();

            if (!left.LastEvents.SequenceEqual(right.LastEvents))
            {
                Assert.Fail(
                    $"Ordered events first diverged at tick {left.Tick} " +
                    "with the last stand active: the first run emitted " +
                    $"{left.LastEvents.Count} events and the second " +
                    $"emitted {right.LastEvents.Count}.");
            }

            var leftHash = left.ComputeStateHash();
            var rightHash = right.ComputeStateHash();

            if (leftHash != rightHash)
            {
                Assert.Fail(
                    $"State hash first diverged at tick {left.Tick} with " +
                    $"the last stand active: 0x{leftHash:X16} against " +
                    $"0x{rightHash:X16}.");
            }
        }

        Assert.Equal(left.Tick, right.Tick);
        Assert.Equal(left.Outcome, right.Outcome);
        Assert.NotEqual(BattleOutcome.Ongoing, left.Outcome);
    }

    /// <summary>
    /// Acceptance row <c>Permutation</c>: the order the caller happens to store
    /// agents in cannot reach any ordered result. Three storage orders of one
    /// identical roster are advanced in lockstep and compared every tick.
    /// </summary>
    [Fact]
    public void InputArrayOrderCannotChangeOrderedResults()
    {
        var scenario = PermutationScenario();
        var ascending = BattleSimulation.CreateForTesting(
            scenario,
            BuildCrowdedRoster(scenario, AgentOrder.Ascending));
        var descending = BattleSimulation.CreateForTesting(
            scenario,
            BuildCrowdedRoster(scenario, AgentOrder.Descending));
        var interleaved = BattleSimulation.CreateForTesting(
            scenario,
            BuildCrowdedRoster(scenario, AgentOrder.Interleaved));

        for (var tick = 0;
             tick < 60 && ascending.Outcome == BattleOutcome.Ongoing;
             tick++)
        {
            ascending.AdvanceOneTick();
            descending.AdvanceOneTick();
            interleaved.AdvanceOneTick();

            AssertSameOrderedResults(ascending, descending, "descending");
            AssertSameOrderedResults(ascending, interleaved, "interleaved");
        }

        Assert.True(
            ascending.Tick > 0,
            "The permutation comparison never advanced a tick.");
    }

    /// <summary>
    /// Acceptance row <c>ID order</c>: the collision policy decision record,
    /// section 9, gives a contested destination to the lower
    /// <c>EntityId</c>. Renumbering the same two bodies therefore moves the win
    /// to the other body. ID independence is explicitly <em>not</em> the
    /// contract, so this test asserts the documented dependence rather than
    /// asserting it away.
    /// </summary>
    /// <remarks>
    /// Two allies sit one body diameter apart and converge on one enemy. Their
    /// preferred destinations overlap, so exactly one of them can take its
    /// preferred destination and report <see cref="MovementResolution.Moved"/>.
    /// </remarks>
    [Fact]
    public void ContestedGroundGoesToTheLowerEntityIdAndFollowsARenumbering()
    {
        var straight = ResolveContestedGround(lowerRowEntityId: 1, upperRowEntityId: 2);
        var renumbered = ResolveContestedGround(lowerRowEntityId: 2, upperRowEntityId: 1);

        Assert.Equal(MovementResolution.Moved, straight[1].MovementResolution);
        Assert.Equal(MovementResolution.Moved, renumbered[1].MovementResolution);
        Assert.NotEqual(MovementResolution.Moved, straight[2].MovementResolution);
        Assert.NotEqual(MovementResolution.Moved, renumbered[2].MovementResolution);

        // Entity 1 occupies the lower row in the first arrangement and entity 2
        // occupies it in the second, so these two views describe the same body on
        // the same ground under two numberings. They must differ.
        var lowerRowStraight = straight[1];
        var lowerRowRenumbered = renumbered[2];
        Assert.NotEqual(
            lowerRowStraight.MovementResolution,
            lowerRowRenumbered.MovementResolution);
        Assert.NotEqual(lowerRowStraight.XRaw, lowerRowRenumbered.XRaw);
    }

    private static void AssertSameOrderedResults(
        BattleSimulation reference,
        BattleSimulation candidate,
        string orderName)
    {
        if (!reference.Agents.SequenceEqual(candidate.Agents))
        {
            Assert.Fail(
                $"The {orderName} storage order first produced different agent " +
                $"state at tick {reference.Tick}.");
        }

        if (!reference.LastEvents.SequenceEqual(candidate.LastEvents))
        {
            Assert.Fail(
                $"The {orderName} storage order first produced a different event " +
                $"stream at tick {reference.Tick}.");
        }

        var referenceHash = reference.ComputeStateHash();
        var candidateHash = candidate.ComputeStateHash();

        if (referenceHash != candidateHash)
        {
            Assert.Fail(
                $"The {orderName} storage order first produced a different state " +
                $"hash at tick {reference.Tick}: 0x{referenceHash:X16} against " +
                $"0x{candidateHash:X16}.");
        }
    }

    private static Dictionary<ulong, AgentView> ResolveContestedGround(
        ulong lowerRowEntityId,
        ulong upperRowEntityId)
    {
        var scenario = ContestScenario();
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(lowerRowEntityId, 0, 60 * FixedPoint.Scale, 46 * FixedPoint.Scale, scenario),
            CreateAgent(upperRowEntityId, 0, 60 * FixedPoint.Scale, 54 * FixedPoint.Scale, scenario),
            CreateAgent(3, 1, 100 * FixedPoint.Scale, 50 * FixedPoint.Scale, scenario));

        simulation.AdvanceOneTick();

        return simulation.Agents.ToDictionary(agent => agent.EntityId);
    }

    private enum AgentOrder
    {
        Ascending,
        Descending,
        Interleaved,
    }

    /// <summary>
    /// Two opposing lines close enough to crowd into one another within the
    /// compared window, stored in one of three orders. The rosters are identical
    /// in content; only the array order differs.
    /// </summary>
    private static AgentState[] BuildCrowdedRoster(Scenario scenario, AgentOrder order)
    {
        const int rows = 6;
        var agents = new List<AgentState>(rows * 2);

        for (var row = 0; row < rows; row++)
        {
            var yRaw = checked((20 + (row * 8)) * FixedPoint.Scale);
            agents.Add(
                CreateAgent(
                    checked((ulong)row + 1),
                    factionId: 0,
                    40 * FixedPoint.Scale,
                    yRaw,
                    scenario));
            agents.Add(
                CreateAgent(
                    checked((ulong)(rows + row) + 1),
                    factionId: 1,
                    70 * FixedPoint.Scale,
                    yRaw,
                    scenario));
        }

        return order switch
        {
            AgentOrder.Ascending => [.. agents.OrderBy(agent => agent.EntityId)],
            AgentOrder.Descending => [.. agents.OrderByDescending(agent => agent.EntityId)],
            _ => [.. agents],
        };
    }

    private static Scenario PermutationScenario() =>
        new(
            Seed: 3,
            MapWidth: 200,
            MapHeight: 200,
            AgentsPerFaction: 6,
            TickRate: 20,
            TickLimit: 1_000);

    private static Scenario ContestScenario() =>
        new(
            Seed: 3,
            MapWidth: 200,
            MapHeight: 200,
            AgentsPerFaction: 2,
            TickRate: 20,
            TickLimit: 1_000);

    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario) =>
        new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            scenario.AttackRangeRaw,
            scenario.DamagePerAttack,
            scenario.AttackCooldownTicks,
            new CombatLoadout(
                WeaponId.GreatBlade,
                ArmorId.LightOrganic,
                ShieldId.None));

    [Fact]
    public void SnapshotIsAnImmutableCopyOfTheCompletedTick()
    {
        var simulation = BattleSimulation.Create(
            Scenario.CreateDefault(seed: 7, totalAgents: 20));
        simulation.AdvanceOneTick();

        var snapshot = simulation.CreateSnapshot();
        var firstAgent = snapshot.Agents[0];

        simulation.AdvanceOneTick();

        Assert.Equal(1, snapshot.Tick);
        Assert.Equal(firstAgent, snapshot.Agents[0]);
        Assert.NotEqual(snapshot.StateHash, simulation.ComputeStateHash());
    }
}
