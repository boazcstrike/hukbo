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

    // ----- Task 8: the retreat rung itself -----

    /// <summary>
    /// A stationary melee enemy placed well inside the threat radius, far
    /// from every map edge: the shooter ends the tick strictly farther from
    /// it than it started -- design 5.2 rung 1 made concrete. The enemy is
    /// held at zero speed so the only thing that can change the separation
    /// is the shooter's own retreat step.
    /// </summary>
    [Fact]
    public void AThreatenedShooterEndsTheTickFartherFromTheThreatThanItBegan()
    {
        var scenario = CreateScenario();
        var rules = RangedRules();
        var standoffRaw = rules
            .ResolveWeaponProfile(Bangkaw.Weapon, Bangkaw.Shield)
            .StandoffDistanceRaw;
        var threatRadiusRaw = RangedRetreatRules.ThreatRadiusRaw(standoffRaw);

        const int centerRaw = 250 * FixedPoint.Scale;
        var shooter = CreateAgent(1, factionId: 0, xRaw: centerRaw, yRaw: centerRaw, scenario, rules, Bangkaw);
        var threat = CreateAgent(
            2,
            factionId: 1,
            xRaw: centerRaw + (threatRadiusRaw / 2),
            yRaw: centerRaw,
            scenario,
            rules,
            Kampilan,
            movementSpeedRawOverride: 0);

        var simulation = BattleSimulation.CreateForTesting(scenario, rules, shooter, threat);

        var startingSquared = SquaredDistanceRaw(shooter.XRaw, shooter.YRaw, threat.XRaw, threat.YRaw);

        simulation.AdvanceOneTick();

        var endingSquared = SquaredDistanceRaw(shooter.XRaw, shooter.YRaw, threat.XRaw, threat.YRaw);

        Assert.True(shooter.XRaw < centerRaw, $"Expected the shooter to step away from the threat, got XRaw={shooter.XRaw}.");
        Assert.True(
            endingSquared > startingSquared,
            $"Expected the separation to grow: started at {startingSquared}, ended at {endingSquared}.");
    }

    /// <summary>
    /// A shooter placed at the map's low-X edge -- exactly
    /// <c>CollisionGeometry.ClampCenterToBounds</c>'s own low bound,
    /// <c>BodyRadiusRaw</c> -- with a melee enemy inside the threat radius on
    /// its +X side. The reflected retreat destination is strictly further
    /// -X, which the bounds clamp pulls straight back to where the shooter
    /// already stands (design 5.5, hazard one), so the cornered warrior
    /// reads <see cref="AgentIntent.Holding"/> and never
    /// <see cref="AgentIntent.BackingAway"/>. Checked on the second tick, not
    /// the first, for the same reason
    /// <c>RangedStandoffTests.RangedWarriorInsideStandoffDistanceHoldsAndDoesNotMove</c>
    /// checks its own hold on the second tick: the first tick's launch can
    /// re-mark <c>Intent</c> to <see cref="AgentIntent.Attacking"/>, and the
    /// weapon's cooldown then keeps the second tick from firing again, so
    /// whatever the movement stage decided is what survives.
    /// </summary>
    [Fact]
    public void ACorneredShooterReadsHoldingNeverBackingAway()
    {
        var scenario = CreateScenario();
        var rules = RangedRules();
        var standoffRaw = rules
            .ResolveWeaponProfile(Bangkaw.Weapon, Bangkaw.Shield)
            .StandoffDistanceRaw;
        var threatRadiusRaw = RangedRetreatRules.ThreatRadiusRaw(standoffRaw);

        const int centerYRaw = 250 * FixedPoint.Scale;
        var shooter = CreateAgent(
            1, factionId: 0, xRaw: scenario.BodyRadiusRaw, yRaw: centerYRaw, scenario, rules, Bangkaw);
        var threat = CreateAgent(
            2,
            factionId: 1,
            xRaw: scenario.BodyRadiusRaw + (threatRadiusRaw / 2),
            yRaw: centerYRaw,
            scenario,
            rules,
            Kampilan,
            movementSpeedRawOverride: 0);

        var simulation = BattleSimulation.CreateForTesting(scenario, rules, shooter, threat);

        simulation.AdvanceOneTick(); // Launch tick: Intent may be re-marked Attacking.
        simulation.AdvanceOneTick(); // Cooldown active: the movement stage's verdict stands.

        Assert.Equal(AgentIntent.Holding, shooter.Intent);
        Assert.Equal(scenario.BodyRadiusRaw, shooter.XRaw);
        Assert.Equal(centerYRaw, shooter.YRaw);
    }

    /// <summary>
    /// A shooter far from every map edge, threatened by a stationary melee
    /// enemy for long enough to cross <see cref="FormationRules.StallEscapeStreakTicks"/>
    /// twice over: design 5.5's hazard two, made observable. An open retreat
    /// is never obstructed, so <c>LongestBlockedStreakTicks</c> never leaves
    /// zero, and on every tick the shooter reads
    /// <see cref="AgentIntent.BackingAway"/> its separation from the threat
    /// only grows -- proof the retreat rung never reads
    /// <c>CollisionScratch.StallGeneration</c> and never builds the
    /// sidestepping-pursuit proposal that would close the distance instead.
    /// </summary>
    [Fact]
    public void RetreatingShooterNeverAccumulatesABlockedStreakAndNeverClosesOnTheThreat()
    {
        const int ticksToRun = FormationRules.StallEscapeStreakTicks * 2;
        var scenario = CreateScenario() with
        {
            MapWidth = 4_000,
            MapHeight = 4_000,
            TickLimit = ticksToRun + 10,
        };
        var rules = RangedRules();
        var standoffRaw = rules
            .ResolveWeaponProfile(Bangkaw.Weapon, Bangkaw.Shield)
            .StandoffDistanceRaw;
        var threatRadiusRaw = RangedRetreatRules.ThreatRadiusRaw(standoffRaw);

        const int centerRaw = 2_000 * FixedPoint.Scale;
        var shooter = CreateAgent(1, factionId: 0, xRaw: centerRaw, yRaw: centerRaw, scenario, rules, Bangkaw);
        var threat = CreateAgent(
            2,
            factionId: 1,
            xRaw: centerRaw + (threatRadiusRaw / 2),
            yRaw: centerRaw,
            scenario,
            rules,
            Kampilan,
            movementSpeedRawOverride: 0);

        var simulation = BattleSimulation.CreateForTesting(scenario, rules, shooter, threat);

        var sawBackingAway = false;
        var previousBackingAwaySquared = -1L;
        for (var tick = 0; tick < ticksToRun; tick++)
        {
            simulation.AdvanceOneTick();

            if (shooter.Intent == AgentIntent.BackingAway)
            {
                sawBackingAway = true;
                var currentSquared =
                    SquaredDistanceRaw(shooter.XRaw, shooter.YRaw, threat.XRaw, threat.YRaw);
                if (previousBackingAwaySquared >= 0)
                {
                    Assert.True(
                        currentSquared >= previousBackingAwaySquared,
                        "A BackingAway tick must never close on the threat -- " +
                        $"was {previousBackingAwaySquared}, now {currentSquared}.");
                }

                previousBackingAwaySquared = currentSquared;
            }
        }

        Assert.True(sawBackingAway, "Expected at least one BackingAway tick.");
        Assert.Equal(0L, simulation.LongestBlockedStreakTicks);
    }

    /// <summary>
    /// The same geometry as <see cref="ACorneredShooterReadsHoldingNeverBackingAway"/>'s
    /// threat placement -- a melee enemy inside what would be V10's threat
    /// radius -- run under <see cref="MovementPresetId.RangedStandoffV8"/>
    /// instead: the widened preset equality test in
    /// <c>GatherMovementProposals</c> must never let V8 reach the retreat
    /// rung, so the shooter follows exactly the V8 two-way ladder it always
    /// has -- holds, because the enemy also sits inside its own standoff
    /// distance -- and never once reads <see cref="AgentIntent.BackingAway"/>.
    /// This is the regression proof that the predicate widening in this task
    /// was inert for V8, alongside the nine frozen digests.
    /// </summary>
    [Fact]
    public void V8NeverReadsBackingAwayEvenWithAMeleeEnemyInsideWhatWouldBeV10sThreatRadius()
    {
        var scenario = CreateScenario() with
        {
            MovementPreset = MovementPresetId.RangedStandoffV8,
        };
        var rules = RangedRules();
        var standoffRaw = rules
            .ResolveWeaponProfile(Bangkaw.Weapon, Bangkaw.Shield)
            .StandoffDistanceRaw;
        var threatRadiusRaw = RangedRetreatRules.ThreatRadiusRaw(standoffRaw);

        const int centerRaw = 250 * FixedPoint.Scale;
        var shooter = CreateAgent(1, factionId: 0, xRaw: centerRaw, yRaw: centerRaw, scenario, rules, Bangkaw);
        var meleeEnemy = CreateAgent(
            2,
            factionId: 1,
            xRaw: centerRaw + (threatRadiusRaw / 2),
            yRaw: centerRaw,
            scenario,
            rules,
            Kampilan,
            movementSpeedRawOverride: 0);

        var simulation = BattleSimulation.CreateForTesting(scenario, rules, shooter, meleeEnemy);

        for (var tick = 0; tick < 10; tick++)
        {
            simulation.AdvanceOneTick();
            Assert.NotEqual(AgentIntent.BackingAway, shooter.Intent);
        }

        Assert.Equal(AgentIntent.Holding, shooter.Intent);
        Assert.Equal(centerRaw, shooter.XRaw);
        Assert.Equal(centerRaw, shooter.YRaw);
    }

    // ----- Helpers -----

    private static long SquaredDistanceRaw(int x1, int y1, int x2, int y2)
    {
        var deltaX = (long)x2 - x1;
        var deltaY = (long)y2 - y1;
        return checked((deltaX * deltaX) + (deltaY * deltaY));
    }

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
    /// <paramref name="movementSpeedRawOverride"/> mirrors the same helper's
    /// own parameter, letting task 8's tests hold a threat stationary so the
    /// only thing that can change a separation is the shooter's own retreat
    /// step.
    /// </summary>
    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatRuleset rules,
        CombatLoadout loadout,
        int? movementSpeedRawOverride = null)
    {
        var profile = rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield);
        return new(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            movementSpeedRawOverride ?? scenario.MovementSpeedRaw,
            scenario.PerceptionRangeRaw,
            profile.AttackRangeRaw,
            profile.DamagePerAttack,
            profile.AttackCooldownTicks,
            loadout);
    }
}
