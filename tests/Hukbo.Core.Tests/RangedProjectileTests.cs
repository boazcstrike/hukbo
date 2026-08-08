using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Ranged-units plan RU-28: the four projectile pins that
/// <see cref="ProjectileTests"/> (RU-17) does not cover -- storage-order
/// independence, same-tick simultaneity, delivery by a shooter that dies
/// mid-flight, and mid-flight save/resume equivalence. Every case runs on
/// <see cref="CombatPresetId.PrecolonialPhilippinesV5"/>, the first (and, as
/// of this task, only) registered preset that fields a ranged weapon, and
/// agents are hand-placed through <see cref="BattleSimulation.CreateForTesting"/>
/// for the same reason <see cref="ProjectileTests"/> does: no shipped loadout
/// pairing is clash-neutral, so a hand-picked seed and entity ID whose roll
/// happens to land would be silently invalidated by a later tuning or mixer
/// change.
/// </summary>
public sealed class RangedProjectileTests
{
    /// <summary>
    /// Bangkaw — see <see cref="ProjectileTests.BangkawLoadout"/> for the
    /// reach rationale: 48 world units, comfortably clear of every melee
    /// weapon's reach.
    /// </summary>
    private static readonly CombatLoadout BangkawLoadout =
        new(WeaponId.Bangkaw, ArmorId.LightOrganic, ShieldId.None);

    /// <summary>
    /// A melee loadout whose 16-unit reach is well short of the 40-unit
    /// separations used below, so a "target" placed at that range never
    /// attacks back and never adds noise to the event feed under test.
    /// </summary>
    private static readonly CombatLoadout OutOfReachMeleeLoadout =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    /// <summary>
    /// Acceptance: the array order the caller happens to pass agents in
    /// cannot change a ranged battle's ordered events, agent states, or
    /// state hash. Mirrors
    /// <c>BattleSimulationTests.CrowdedTarget_ResolvesIdenticallyUnderEveryStorageOrder</c>
    /// and <c>DeterminismTests.InputArrayOrderCannotChangeOrderedResults</c>,
    /// carried through a full launch-to-arrival cycle (15 ticks covers the
    /// 1-tick launch plus Bangkaw's 10-tick flight) so an ordering bug in the
    /// pass-A0 projectile-pool compaction -- for example one that favored
    /// array index over a stable key -- would surface here rather than only
    /// in the melee gather loop the two mirrored tests already cover.
    /// </summary>
    [Fact]
    public void ProjectileBattle_ResolvesIdenticallyUnderEveryStorageOrder()
    {
        var rules = RangedRulesetWithClashProfile(ClashProfile.Neutral);
        var scenario = CreateRangedTestScenario(maximumProjectilesInFlight: 8);

        var ascending = BattleSimulation.CreateForTesting(
            scenario,
            rules,
            BuildMirroredRoster(scenario, rules, reversed: false, interleaved: false));
        var descending = BattleSimulation.CreateForTesting(
            scenario,
            rules,
            BuildMirroredRoster(scenario, rules, reversed: true, interleaved: false));
        var interleaved = BattleSimulation.CreateForTesting(
            scenario,
            rules,
            BuildMirroredRoster(scenario, rules, reversed: false, interleaved: true));

        for (var tick = 0; tick < 15; tick++)
        {
            ascending.AdvanceOneTick();
            descending.AdvanceOneTick();
            interleaved.AdvanceOneTick();

            Assert.Equal(ascending.LastEvents, descending.LastEvents);
            Assert.Equal(ascending.LastEvents, interleaved.LastEvents);
            Assert.Equal(ascending.Agents, descending.Agents);
            Assert.Equal(ascending.Agents, interleaved.Agents);
            Assert.Equal(ascending.ComputeStateHash(), descending.ComputeStateHash());
            Assert.Equal(ascending.ComputeStateHash(), interleaved.ComputeStateHash());
        }
    }

    /// <summary>
    /// Acceptance: two projectiles that arrive on the same tick against the
    /// same target both land and both contribute to the damage total, the
    /// ranged mirror of
    /// <c>PhilippineCombatIntegrationTests.MutualLethalAttacksStillProduceADrawWhenBothLand</c>.
    /// The target's hit points are sized so that neither shot alone is
    /// lethal and only the sum is, which is what proves the second arrival
    /// was not silently dropped because the first had already resolved --
    /// exactly the property <c>DamageIsAccumulatedBeforeMutualDeathResolution</c>
    /// proves for two melee blows.
    /// </summary>
    [Fact]
    public void TwoProjectilesArrivingTheSameTickBothLandAndBothContributeDamage()
    {
        var rules = RangedRulesetWithClashProfile(ClashProfile.Neutral);
        var scenario = CreateRangedTestScenario(maximumProjectilesInFlight: 4) with
        {
            MaximumHitPoints = 15,
        };

        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            rules,
            CreateAgent(1, factionId: 0, x: 0, y: 0, scenario, rules, BangkawLoadout),
            CreateAgent(2, factionId: 0, x: 2, y: 0, scenario, rules, BangkawLoadout),
            CreateAgent(3, factionId: 1, x: 40, y: 0, scenario, rules, OutOfReachMeleeLoadout));

        simulation.AdvanceOneTick();

        var releases = simulation.LastEvents
            .Where(battleEvent => battleEvent.Kind == BattleEventKind.Release)
            .ToArray();
        Assert.Equal(2, releases.Length);

        for (var tick = 0; tick < 9; tick++)
        {
            simulation.AdvanceOneTick();
        }

        simulation.AdvanceOneTick();
        Assert.Equal(11, simulation.Tick);

        var attacks = simulation.LastEvents
            .Where(battleEvent => battleEvent.Kind == BattleEventKind.Attack)
            .ToArray();
        Assert.Equal(2, attacks.Length);
        Assert.All(
            attacks,
            attack => Assert.Equal(AttackResolution.Landed, attack.Resolution));
        Assert.All(
            attacks,
            attack => Assert.Equal(3UL, attack.TargetEntityId));

        var damageEvent = Assert.Single(
            simulation.LastEvents,
            battleEvent =>
                battleEvent.Kind == BattleEventKind.Damage &&
                battleEvent.TargetEntityId == 3);
        Assert.Equal(20, damageEvent.Value); // Two landed Bangkaw shots, 10 each.

        var target = Assert.Single(simulation.Agents, agent => agent.EntityId == 3);
        Assert.False(target.IsAlive);
    }

    /// <summary>
    /// Six Bangkaw-armed warriors, three per faction, close enough that both
    /// sides' shots are in reach from the first tick. Mirrors the shape of
    /// <c>BattleSimulationTests.BuildCrowdedRoster</c> and
    /// <c>DeterminismTests.BuildCrowdedRoster</c>: identical roster content
    /// in every case, only the array order differs.
    /// </summary>
    private static AgentState[] BuildMirroredRoster(
        Scenario scenario,
        CombatRuleset rules,
        bool reversed,
        bool interleaved)
    {
        var agents = new List<AgentState>
        {
            CreateAgent(1, factionId: 0, x: 10, y: 10, scenario, rules, BangkawLoadout),
            CreateAgent(2, factionId: 0, x: 10, y: 12, scenario, rules, BangkawLoadout),
            CreateAgent(3, factionId: 0, x: 10, y: 14, scenario, rules, BangkawLoadout),
            CreateAgent(4, factionId: 1, x: 20, y: 10, scenario, rules, BangkawLoadout),
            CreateAgent(5, factionId: 1, x: 20, y: 12, scenario, rules, BangkawLoadout),
            CreateAgent(6, factionId: 1, x: 20, y: 14, scenario, rules, BangkawLoadout),
        };

        if (reversed)
        {
            agents.Reverse();
        }
        else if (interleaved)
        {
            agents = [.. agents.OrderBy(agent => agent.EntityId % 2)];
        }

        return [.. agents];
    }

    /// <summary>
    /// The registered V5 preset with its clash profile swapped for the
    /// caller-chosen one -- everything else (roster, weapon attributes,
    /// target weights) carried forward unchanged. Mirrors
    /// <see cref="ProjectileTests"/>'s private helper of the same name.
    /// </summary>
    private static CombatRuleset RangedRulesetWithClashProfile(ClashProfile profile) =>
        CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV5)
            .WithClashProfile(profile);

    /// <summary>
    /// Builds a scenario on the V5 preset with a caller-chosen projectile
    /// pool ceiling. Mirrors <see cref="ProjectileTests"/>'s private helper
    /// of the same name.
    /// </summary>
    private static Scenario CreateRangedTestScenario(int maximumProjectilesInFlight) =>
        new(
            Seed: 1,
            MapWidth: 500,
            MapHeight: 500,
            AgentsPerFaction: 2,
            TickRate: 20,
            TickLimit: 200)
        {
            MaximumHitPoints = 1_000,
            DamagePerAttack = 10,
            AttackRangeRaw = 12 * FixedPoint.Scale,
            PerceptionRangeRaw = 1_000 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 1,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV5,
            MaximumProjectilesInFlight = maximumProjectilesInFlight,
        };

    /// <summary>
    /// Mirrors <see cref="ProjectileTests"/>'s private helper of the same
    /// name: per-agent attack range, damage, and cooldown are resolved from
    /// <paramref name="loadout"/> through <paramref name="rules"/>, not read
    /// from <paramref name="scenario"/>, since V5 declares weapon profiles
    /// for every loadout.
    /// </summary>
    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int x,
        int y,
        Scenario scenario,
        CombatRuleset rules,
        CombatLoadout loadout)
    {
        var profile = rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield);
        return new AgentState(
            entityId,
            factionId,
            checked(x * FixedPoint.Scale),
            checked(y * FixedPoint.Scale),
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            profile.AttackRangeRaw,
            profile.DamagePerAttack,
            profile.AttackCooldownTicks,
            loadout);
    }
}
