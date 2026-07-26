using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
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
