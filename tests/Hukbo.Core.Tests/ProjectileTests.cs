using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// Ranged-units plan RU-17: the pooled, hitscan-with-flight-time projectile.
/// Every case here runs on <see cref="CombatPresetId.PrecolonialPhilippinesV5"/>,
/// the first (and, at this task, only) registered preset that fields a ranged
/// weapon. Agents are hand-placed through
/// <see cref="BattleSimulation.CreateForTesting"/> rather than spawned through
/// <see cref="BattleSimulation.Create"/>, for the same reason every other
/// clash-sensitive case in this test project does that: no shipped loadout
/// pairing is clash-neutral, so a hand-picked seed or entity ID whose roll
/// happens to land would be silently invalidated by any later tuning or mixer
/// change.
/// </summary>
public sealed class ProjectileTests
{
    /// <summary>
    /// Bangkaw — the fastest-cycling of the three ranged rows — reused across
    /// every case below. Its reach (48 world units) comfortably exceeds every
    /// melee weapon's reach, which is what lets a shooter and an
    /// intentionally out-of-reach melee "target" sit in one shooter's attack
    /// range without the target ever attacking back and adding noise to the
    /// event feed under test.
    /// </summary>
    private static readonly CombatLoadout BangkawLoadout =
        new(WeaponId.Bangkaw, ArmorId.LightOrganic, ShieldId.None);

    /// <summary>
    /// A melee loadout whose 16-unit reach is well short of the 40-unit
    /// separation every case below places it at from its Bangkaw-armed
    /// counterpart, so it never attacks back.
    /// </summary>
    private static readonly CombatLoadout OutOfReachMeleeLoadout =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    [Fact]
    public void LaunchedProjectileResolvesExactlyAtLaunchTickPlusFlightTicks()
    {
        var rules = RangedRulesetWithClashProfile(ClashProfile.Neutral);
        var scenario = CreateRangedTestScenario(maximumProjectilesInFlight: 4);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            rules,
            CreateAgent(1, factionId: 0, x: 0, y: 0, scenario, rules, BangkawLoadout),
            CreateAgent(2, factionId: 1, x: 40, y: 0, scenario, rules, OutOfReachMeleeLoadout));

        // Tick 1: launch. Bangkaw's cooldown (25 ticks) and flight ceiling
        // (10 ticks) both start counting from here.
        simulation.AdvanceOneTick();
        Assert.Equal(1, simulation.Tick);

        var release = Assert.Single(
            simulation.LastEvents,
            e => e.Kind == BattleEventKind.Release);
        Assert.Equal(1UL, release.SourceEntityId);
        Assert.Equal(2UL, release.TargetEntityId);
        Assert.Equal(10, release.Value); // Bangkaw's FlightTickCeiling.
        Assert.DoesNotContain(
            simulation.LastEvents,
            e => e.Kind == BattleEventKind.Attack);

        var target = Assert.Single(simulation.Agents, a => a.EntityId == 2);
        Assert.Equal(1_000, target.HitPoints);
        Assert.Equal(0, simulation.ProjectileLaunchRefusals);
        Assert.Single(simulation.CreateSnapshot().Projectiles);

        // Ticks 2 through 10: the shot is in flight and must not resolve on
        // any of them.
        for (var tick = 0; tick < 9; tick++)
        {
            simulation.AdvanceOneTick();
            Assert.DoesNotContain(
                simulation.LastEvents,
                e => e.Kind is BattleEventKind.Attack or BattleEventKind.Miss);
        }

        Assert.Equal(10, simulation.Tick);

        // Tick 11 = launch tick (1) + flight ticks (10): arrival, on this
        // tick and no other.
        simulation.AdvanceOneTick();
        Assert.Equal(11, simulation.Tick);

        var attack = Assert.Single(
            simulation.LastEvents,
            e => e.Kind == BattleEventKind.Attack);
        Assert.Equal(1UL, attack.SourceEntityId);
        Assert.Equal(2UL, attack.TargetEntityId);
        Assert.Equal(AttackResolution.Landed, attack.Resolution);

        target = Assert.Single(simulation.Agents, a => a.EntityId == 2);
        Assert.Equal(990, target.HitPoints); // 1,000 - Bangkaw's 10 damage.
        Assert.Equal(0, simulation.ProjectileLaunchRefusals);
        Assert.Empty(simulation.CreateSnapshot().Projectiles);
    }

    /// <summary>
    /// The real, unforced V5 clash profile, precisely so the roll at
    /// impact depends on which tick it folds. If the arrival pass folded the
    /// impact tick instead of the launch tick, this comparison against a
    /// direct call at the launch tick would have no reason to hold.
    /// </summary>
    [Fact]
    public void ClashRollAtImpactFoldsTheLaunchTickNotTheArrivalTick()
    {
        var rules = CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV5);
        var scenario = CreateRangedTestScenario(maximumProjectilesInFlight: 4);
        const ulong shooterId = 1;
        const ulong targetId = 2;

        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            rules,
            CreateAgent(shooterId, factionId: 0, x: 0, y: 0, scenario, rules, BangkawLoadout),
            CreateAgent(targetId, factionId: 1, x: 40, y: 0, scenario, rules, OutOfReachMeleeLoadout));

        simulation.AdvanceOneTick();
        var launchTick = simulation.Tick;
        Assert.Equal(1, launchTick);

        for (var tick = 0; tick < 9; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var expectedHitLocation = HitLocationResolver.Resolve(
            rules,
            BangkawLoadout,
            OutOfReachMeleeLoadout,
            scenario.Seed,
            launchTick,
            shooterId,
            targetId);
        var expectedResolution = ClashResolver.Resolve(
            rules.ClashProfile,
            scenario.Seed,
            launchTick,
            shooterId,
            targetId,
            BangkawLoadout.Weapon,
            OutOfReachMeleeLoadout.Weapon,
            OutOfReachMeleeLoadout.Shield);

        simulation.AdvanceOneTick();
        Assert.Equal(11, simulation.Tick);

        var attack = Assert.Single(
            simulation.LastEvents,
            e => e.Kind == BattleEventKind.Attack);
        Assert.Equal(expectedHitLocation, attack.HitLocation);
        Assert.Equal(expectedResolution, attack.Resolution);

        var target = Assert.Single(simulation.Agents, a => a.EntityId == targetId);
        var expectedDamage = expectedResolution == AttackResolution.Landed ? 10 : 0;
        Assert.Equal(scenario.MaximumHitPoints - expectedDamage, target.HitPoints);
    }

    /// <summary>
    /// Two shooters, one ceiling of one: the first launch fills the pool and
    /// the second is refused outright. The refused shooter's cooldown is
    /// proven never charged by having it attempt again, and be refused
    /// again, on the very next tick -- a charged cooldown would instead have
    /// gone silent for 25 ticks.
    /// </summary>
    [Fact]
    public void LaunchAtThePoolCeilingIsRefusedWithoutChargingCooldown()
    {
        var rules = RangedRulesetWithClashProfile(ClashProfile.Neutral);
        var scenario = CreateRangedTestScenario(maximumProjectilesInFlight: 1);

        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            rules,
            CreateAgent(1, factionId: 0, x: 0, y: 0, scenario, rules, BangkawLoadout),
            CreateAgent(2, factionId: 1, x: 40, y: 0, scenario, rules, OutOfReachMeleeLoadout),
            CreateAgent(3, factionId: 0, x: 2, y: 0, scenario, rules, BangkawLoadout));

        simulation.AdvanceOneTick();

        var release = Assert.Single(
            simulation.LastEvents,
            e => e.Kind == BattleEventKind.Release);
        Assert.Equal(1UL, release.SourceEntityId);
        Assert.DoesNotContain(
            simulation.LastEvents,
            e => e.Kind == BattleEventKind.Release && e.SourceEntityId == 3UL);
        Assert.Equal(1, simulation.ProjectileLaunchRefusals);
        Assert.Single(simulation.CreateSnapshot().Projectiles);

        simulation.AdvanceOneTick();

        // Entity 1's shot has neither resolved (10-tick flight) nor freed
        // its slot, so the pool is still at its ceiling of one and entity 3
        // is refused again -- proof its cooldown was never charged by the
        // first refusal.
        Assert.DoesNotContain(
            simulation.LastEvents,
            e => e.Kind == BattleEventKind.Release && e.SourceEntityId == 3UL);
        Assert.Equal(2, simulation.ProjectileLaunchRefusals);
        var shot = Assert.Single(simulation.CreateSnapshot().Projectiles);
        Assert.Equal(1UL, shot.SourceEntityId);
    }

    /// <summary>
    /// Zero-allocation warm-tick budget (design section 8.3): a duel between
    /// two Bangkaw-armed shooters at their standoff-clear reach, run long
    /// enough to cycle several full launch-cooldown-arrival rounds on both
    /// sides, must not allocate once the pool and its scratch state have
    /// warmed up. Hit points are large enough that neither side dies inside
    /// the measured window, which keeps the duel -- and the launch, pass-A0
    /// decrement/compaction, and arrival-resolution code paths under test --
    /// running for the whole window.
    /// </summary>
    [Fact]
    public void RepeatedRangedCombatTicksHaveBoundedAllocations()
    {
        const int measuredTicks = 1_000;
        const long maximumAllocatedBytes = 16_384;

        var rules = RangedRulesetWithClashProfile(ClashProfile.Neutral);
        var scenario = CreateRangedTestScenario(maximumProjectilesInFlight: 8) with
        {
            MaximumHitPoints = 1_000_000,
            TickLimit = (measuredTicks * 2) + 100,
        };

        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            rules,
            CreateAgent(1, factionId: 0, x: 0, y: 0, scenario, rules, BangkawLoadout),
            CreateAgent(2, factionId: 1, x: 40, y: 0, scenario, rules, BangkawLoadout));

        for (var tick = 0; tick < 32; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var firstWindowStart = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < measuredTicks; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var secondWindowStart = GC.GetAllocatedBytesForCurrentThread();
        for (var tick = 0; tick < measuredTicks; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var windowEnd = GC.GetAllocatedBytesForCurrentThread();
        var firstWindowBytes = secondWindowStart - firstWindowStart;
        var secondWindowBytes = windowEnd - secondWindowStart;

        Assert.Equal(BattleOutcome.Ongoing, simulation.Outcome);
        Assert.Equal(0, simulation.ProjectileLaunchRefusals);

        Assert.True(
            firstWindowBytes <= maximumAllocatedBytes,
            $"Ranged-combat ticks allocated {firstWindowBytes:N0} bytes; " +
            $"expected at most {maximumAllocatedBytes:N0}.");

        // The warm-window guard: the projectile pool, its write-index
        // scratch, and the event buffers must all be reused, so a second
        // identical window allocates no more than the first.
        Assert.True(
            secondWindowBytes <= maximumAllocatedBytes,
            $"A warm ranged-combat window allocated {secondWindowBytes:N0} " +
            $"bytes after a first window of {firstWindowBytes:N0}; expected " +
            $"at most {maximumAllocatedBytes:N0}. The projectile pool must " +
            "be reused, growing only when capacity is insufficient.");
    }

    /// <summary>
    /// The registered V5 preset with its clash profile swapped for the
    /// supplied one and everything else -- roster, weapon attributes, target
    /// weights -- carried forward unchanged, matching the shape
    /// <c>BattleSimulationTests.PresetWith</c> uses for V1. Preserves the
    /// registered roster so <c>BattleSimulation.CreateForTesting</c>'s
    /// roster-agreement check passes.
    /// </summary>
    private static CombatRuleset RangedRulesetWithClashProfile(ClashProfile profile) =>
        CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV5)
            .WithClashProfile(profile);

    /// <summary>
    /// Builds a scenario on the V5 preset with a caller-chosen projectile
    /// pool ceiling. The scenario-level combat fields
    /// (<see cref="Scenario.AttackRangeRaw"/>,
    /// <see cref="Scenario.DamagePerAttack"/>,
    /// <see cref="Scenario.AttackCooldownTicks"/>) are never read by any
    /// agent constructed through <see cref="CreateAgent"/> below, because
    /// V5 declares weapon profiles for every loadout; they are set only to
    /// satisfy <see cref="Scenario.Validate"/>.
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
    /// Mirrors <c>BattleSimulation.CreateAgent</c>'s own resolution of
    /// per-agent attack range, damage, and cooldown from the ruleset's
    /// <see cref="WeaponProfile"/> rather than from the scenario's global
    /// fallback fields, which is load-bearing here: every case in this file
    /// runs on V5, which declares weapon profiles for every loadout, so a
    /// hand-built agent that instead carried the scenario's global
    /// <see cref="Scenario.AttackRangeRaw"/> would not reach -- or would
    /// unrealistically over-reach -- at the distances these cases place it.
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
        return new(
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
