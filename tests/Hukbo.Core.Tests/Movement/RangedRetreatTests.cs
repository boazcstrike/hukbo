using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The ranged-retreat threat-observation scratch of battlefield realism
/// design section 5.3: <c>BattleSimulation</c>'s per-agent
/// <c>_nearestMeleeThreatSquared</c> row, fused into the existing
/// <c>SelectTargetsAndIntents</c> candidate loop rather than a second scan.
/// Every expected distance below is a hand-computed squared distance from a
/// scenario this test builds itself -- never a value read back from the
/// simulation under test. Task 7 only observes; nothing here reads
/// <see cref="AgentIntent.BackingAway"/> or any movement consequence, because
/// the retreat rung that consumes this scratch is task 8's insertion.
/// </summary>
public sealed class RangedRetreatTests
{
    private static readonly CombatLoadout Bangkaw =
        new(WeaponId.Bangkaw, ArmorId.LightOrganic, ShieldId.None);

    private static readonly CombatLoadout Kampilan =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    // ----- The nearest melee enemy, regardless of the selected target -----

    /// <summary>
    /// A ranged shooter with three enemies in perception range: a nearer
    /// ranged enemy (the overall-nearest candidate, and therefore the
    /// selected target), a near melee enemy, and a far melee enemy. The
    /// scratch value is the squared distance to the near melee enemy --
    /// design 5.3's "not the shooter's selected target" claim made concrete
    /// -- and the far melee enemy is proof the reduction takes a minimum
    /// rather than the first or last melee candidate observed.
    /// </summary>
    [Fact]
    public void NearestMeleeThreatIsTheClosestLivingMeleeEnemyRegardlessOfSelectedTarget()
    {
        var scenario = CreateScenario();
        var rules = RangedRules();

        var shooter = CreateAgent(1, factionId: 0, xRaw: 0, yRaw: 0, scenario, rules, Bangkaw);
        var nearestRangedEnemy = CreateAgent(
            2, factionId: 1, xRaw: 5 * FixedPoint.Scale, yRaw: 0, scenario, rules, Bangkaw);
        var nearMeleeEnemy = CreateAgent(
            3, factionId: 1, xRaw: 10 * FixedPoint.Scale, yRaw: 0, scenario, rules, Kampilan);
        var farMeleeEnemy = CreateAgent(
            4, factionId: 1, xRaw: 30 * FixedPoint.Scale, yRaw: 0, scenario, rules, Kampilan);

        var simulation = BattleSimulation.CreateForTesting(
            scenario, rules, shooter, nearestRangedEnemy, nearMeleeEnemy, farMeleeEnemy);

        simulation.AdvanceOneTick();

        // The overall-nearest candidate is the ranged enemy at 5 world
        // units, so target selection -- unrelated to this task's scratch --
        // picked it, not either melee enemy.
        Assert.Equal(nearestRangedEnemy.EntityId, shooter.TargetEntityId);

        var expectedSquared = 10L * FixedPoint.Scale * (10L * FixedPoint.Scale);
        Assert.Equal(
            expectedSquared,
            simulation.NearestMeleeThreatSquaredForTesting(shooter.EntityId));
    }

    // ----- No melee enemy observed -----

    /// <summary>
    /// A ranged shooter whose only perceivable enemy carries a ranged
    /// weapon observes the sentinel, <see cref="long.MaxValue"/> --
    /// <c>RangedRetreatRules.IsThreatened</c>'s documented "none observed"
    /// value, confirmed safe there because it is always greater than any
    /// finite <c>threatRadiusRaw</c> squared.
    /// </summary>
    [Fact]
    public void NearestMeleeThreatIsTheSentinelWhenNoMeleeEnemyIsObserved()
    {
        var scenario = CreateScenario();
        var rules = RangedRules();

        var shooter = CreateAgent(1, factionId: 0, xRaw: 0, yRaw: 0, scenario, rules, Bangkaw);
        var rangedEnemy = CreateAgent(
            2, factionId: 1, xRaw: 10 * FixedPoint.Scale, yRaw: 0, scenario, rules, Bangkaw);

        var simulation = BattleSimulation.CreateForTesting(scenario, rules, shooter, rangedEnemy);

        simulation.AdvanceOneTick();

        Assert.Equal(
            long.MaxValue,
            simulation.NearestMeleeThreatSquaredForTesting(shooter.EntityId));
    }

    // ----- A melee-weapon actor observes no threat at all -----

    /// <summary>
    /// An agent whose own weapon is melee never has the scratch populated,
    /// even standing beside a living melee enemy well within any plausible
    /// threat radius -- design 5.3's "observed only for actors whose own
    /// weapon is ranged" clause.
    /// </summary>
    [Fact]
    public void AMeleeWeaponActorObservesTheSentinelRegardlessOfNearbyMeleeEnemies()
    {
        var scenario = CreateScenario();
        var rules = RangedRules();

        var meleeActor = CreateAgent(1, factionId: 0, xRaw: 0, yRaw: 0, scenario, rules, Kampilan);
        var adjacentMeleeEnemy = CreateAgent(
            2, factionId: 1, xRaw: 2 * FixedPoint.Scale, yRaw: 0, scenario, rules, Kampilan);

        var simulation = BattleSimulation.CreateForTesting(
            scenario, rules, meleeActor, adjacentMeleeEnemy);

        simulation.AdvanceOneTick();

        Assert.Equal(
            long.MaxValue,
            simulation.NearestMeleeThreatSquaredForTesting(meleeActor.EntityId));
    }

    // ----- Sized zero outside V10 -----

    /// <summary>
    /// The accessor throws under any preset other than
    /// <see cref="MovementPresetId.BattlefieldRealismV10"/>, matching the
    /// constructor sizing the scratch array to zero length for every other
    /// registered preset -- there is no row to read.
    /// </summary>
    [Fact]
    public void TheScratchIsNotDerivedUnderAnyOtherRegisteredPreset()
    {
        var scenario = CreateScenario() with
        {
            MovementPreset = MovementPresetId.RangedStandoffV8,
        };
        var rules = RangedRules();

        var shooter = CreateAgent(1, factionId: 0, xRaw: 0, yRaw: 0, scenario, rules, Bangkaw);
        var meleeEnemy = CreateAgent(
            2, factionId: 1, xRaw: 10 * FixedPoint.Scale, yRaw: 0, scenario, rules, Kampilan);

        var simulation = BattleSimulation.CreateForTesting(scenario, rules, shooter, meleeEnemy);

        simulation.AdvanceOneTick();

        Assert.Throws<InvalidOperationException>(
            () => simulation.NearestMeleeThreatSquaredForTesting(shooter.EntityId));
    }

    // ----- Helpers -----

    private static CombatRuleset RangedRules() =>
        CombatPresetRegistry.Get(CombatPresetId.PrecolonialPhilippinesV5);

    /// <summary>
    /// A V10 scenario on the V5 ranged combat preset, mirroring
    /// <c>RangedStandoffTests.CreateRangedScenario</c>'s combat-field shape
    /// with a wide enough perception range that every hand-placed agent
    /// below is perceivable by every other.
    /// </summary>
    private static Scenario CreateScenario() =>
        new(
            Seed: 1,
            MapWidth: 500,
            MapHeight: 500,
            AgentsPerFaction: 2,
            TickRate: 20,
            TickLimit: 500)
        {
            MaximumHitPoints = 1_000,
            DamagePerAttack = 10,
            AttackRangeRaw = 12 * FixedPoint.Scale,
            PerceptionRangeRaw = 1_000 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 1,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV5,
            MovementPreset = MovementPresetId.BattlefieldRealismV10,
            MaximumProjectilesInFlight = 4,
        };

    /// <summary>
    /// Mirrors <c>RangedStandoffTests.CreateAgent</c>: resolves per-agent
    /// attack range, damage, and cooldown from the ruleset's
    /// <see cref="WeaponProfile"/> so a hand-placed agent carries a real
    /// weapon's real fields rather than the scenario-wide defaults.
    /// </summary>
    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatRuleset rules,
        CombatLoadout loadout)
    {
        var profile = rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield);
        return new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            profile.AttackRangeRaw,
            profile.DamagePerAttack,
            profile.AttackCooldownTicks,
            loadout);
    }
}
