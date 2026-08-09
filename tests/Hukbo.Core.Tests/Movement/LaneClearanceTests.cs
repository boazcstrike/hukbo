using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Plan task RU-30 (F-B): pins the absolute lane-clearance rule
/// <c>IsLaneClearOfAllies</c> has always applied under every preset through
/// <see cref="MovementPresetId.EquipmentRelativeFootworkV7"/>, and the
/// narrower monotonicity rule it applies under
/// <see cref="MovementPresetId.MonotoneAllyClearanceV9"/> alone: a candidate
/// already inside an ally's clearance radius is refused only when it also
/// moves the actor strictly closer to that ally than the actor's own
/// tick-start position already was. <c>IsLaneClearOfAllies</c> is private,
/// so every assertion here observes it through <see cref="BattleSimulation"/>
/// behaviour -- route refusals, <see cref="AgentView.FootworkPhase"/>, and
/// final positions -- the same way <see cref="RangedStandoffTests"/> and
/// <c>MovementBehaviorMetricsTests.BattleSimulationV6_RouteRefusalReasonsSumToTheObservedRefuseAgentTicks</c>
/// already observe route-refusal behaviour that has no public unit-level
/// seam of its own.
/// </summary>
public sealed class LaneClearanceTests
{
    private static readonly CombatLoadout Kampilan =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    /// <summary>
    /// RU-30's own acceptance measurement, F-B: on a melee-only roster --
    /// combat preset <see cref="CombatPresetId.PrecolonialPhilippinesV2"/>,
    /// every agent Kampilan-armed, no ranged weapon in play, so the ranged
    /// standoff mechanic RU-21 added cannot confound this reading -- the
    /// <see cref="BattleSimulation.RouteRefusalLaneNotClear"/> counter
    /// collapses under <see cref="MovementPresetId.MonotoneAllyClearanceV9"/>
    /// relative to the same measurement on the identical roster and seed
    /// under <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/>'s
    /// absolute rule. This is the F-A root-cause finding under direct test:
    /// the absolute rule refused a route for standing inside another
    /// warrior's clearance radius regardless of which direction the route
    /// moved, so most of what it counted was movement away from a violation,
    /// not into one -- exactly what the monotone rule stops counting.
    /// </summary>
    [Fact]
    public void MeleeOnlyRoster_RouteRefusalLaneNotClear_CollapsesUnderV9RelativeToV6()
    {
        const int TicksToRun = 5_000;

        var v6Scenario = CreateMeleeScenario(MovementPresetId.EquipmentRelativeFootworkV6)
            with
        {
            TickLimit = TicksToRun,
        };
        var v9Scenario = CreateMeleeScenario(MovementPresetId.MonotoneAllyClearanceV9)
            with
        {
            TickLimit = TicksToRun,
        };

        var v6 = BattleSimulation.CreateForTesting(v6Scenario, CreateMeleeRoster(v6Scenario));
        var v9 = BattleSimulation.CreateForTesting(v9Scenario, CreateMeleeRoster(v9Scenario));

        for (var tick = 0;
             tick < TicksToRun && v6.Outcome == BattleOutcome.Ongoing;
             tick++)
        {
            v6.AdvanceOneTick();
        }

        for (var tick = 0;
             tick < TicksToRun && v9.Outcome == BattleOutcome.Ongoing;
             tick++)
        {
            v9.AdvanceOneTick();
        }

        // Measured once, from this exact fixture (seed 1, the six-agent
        // melee-only roster below, 5,000 ticks): V6's absolute rule refuses
        // 30,000 -- every living agent, every tick, the roster spawns close
        // enough together that each one starts inside an ally's clearance
        // radius and the absolute rule then refuses every route out, in
        // every direction, forever. V9's monotone rule refuses 57, because
        // standing still or moving away from that starting violation is now
        // legal. The tenfold margin below is a collapse bar, not the exact
        // observed ratio (30,000 / 57 =~ 526x): loose enough to stay green
        // under an unrelated future tuning change to this roster's spacing,
        // tight enough that a regression back toward the absolute rule's
        // behaviour still fails it.
        const int CollapseFactor = 10;

        Assert.True(
            v6.RouteRefusalLaneNotClear > 0,
            "The V6 control run must itself refuse at least one route on " +
            "lane-not-clear grounds, or this measurement proves nothing " +
            "about a collapse.");
        Assert.True(
            v9.RouteRefusalLaneNotClear * CollapseFactor < v6.RouteRefusalLaneNotClear,
            $"Expected RouteRefusalLaneNotClear to collapse under V9 by at " +
            $"least {CollapseFactor}x relative to V6: V6 recorded " +
            $"{v6.RouteRefusalLaneNotClear}, V9 recorded " +
            $"{v9.RouteRefusalLaneNotClear}.");
    }

    // ----- Directed single-tick pin: absolute rule (every preset but V9) -----

    /// <summary>
    /// A warrior already inside an ally's clearance radius -- the ally sits
    /// 1,000 raw units away, strictly inside the Kampilan row's clearance
    /// radius of 1,536 raw units (<see cref="MovementRouteRules.ClearanceRadiusRaw"/>
    /// applied to <c>allyClearanceBodyDiametersBasisPoints: 15_000</c> over a
    /// 1,024-raw body diameter) -- is refused a route toward a distant enemy
    /// even though that route's one-tick step moves the actor strictly
    /// farther from the ally, not closer. Measured directly: under
    /// <see cref="MovementPresetId.EquipmentRelativeFootworkV6"/> the actor's
    /// position and <see cref="Movement.FootworkPhase"/> are unchanged after
    /// the tick. This is the F-A root-cause behaviour RU-30 exists to narrow.
    /// </summary>
    [Fact]
    public void AbsoluteRule_UnderV6_RefusesARouteThatMovesAwayFromAnAlreadyTooCloseAlly()
    {
        var scenario = CreateDuelScenario(MovementPresetId.EquipmentRelativeFootworkV6);
        var actor = CreateMeleeAgent(1, factionId: 0, xRaw: ActorXRaw, yRaw: ActorYRaw, scenario);
        var ally = CreateMeleeAgent(
            2, factionId: 0, xRaw: ActorXRaw - AllyOffsetRaw, yRaw: ActorYRaw, scenario);
        var enemy = CreateDistantEnemy(scenario);

        var simulation = BattleSimulation.CreateForTesting(scenario, actor, ally, enemy);
        simulation.AdvanceOneTick();

        Assert.Equal(ActorXRaw, actor.XRaw);
        Assert.Equal(FootworkPhase.Refuse, actor.FootworkPhase);
    }

    // ----- Directed single-tick pin: monotone rule (V9 only) -----

    /// <summary>
    /// The identical starting geometry as
    /// <see cref="AbsoluteRule_UnderV6_RefusesARouteThatMovesAwayFromAnAlreadyTooCloseAlly"/>
    /// -- actor already inside the same ally's clearance radius, same route
    /// toward the same distant enemy, same one-tick step that moves strictly
    /// farther from the ally -- accepted instead of refused under
    /// <see cref="MovementPresetId.MonotoneAllyClearanceV9"/>: the actor's
    /// position moves and its phase is not
    /// <see cref="Movement.FootworkPhase.Refuse"/>. The only variable between
    /// this test and its V6 counterpart is the movement preset, so the
    /// difference in outcome is attributable to the monotone rule alone.
    /// </summary>
    [Fact]
    public void MonotoneRule_UnderV9_AcceptsARouteThatMovesAwayFromAnAlreadyTooCloseAlly()
    {
        var scenario = CreateDuelScenario(MovementPresetId.MonotoneAllyClearanceV9);
        var actor = CreateMeleeAgent(1, factionId: 0, xRaw: ActorXRaw, yRaw: ActorYRaw, scenario);
        var ally = CreateMeleeAgent(
            2, factionId: 0, xRaw: ActorXRaw - AllyOffsetRaw, yRaw: ActorYRaw, scenario);
        var enemy = CreateDistantEnemy(scenario);

        var simulation = BattleSimulation.CreateForTesting(scenario, actor, ally, enemy);
        simulation.AdvanceOneTick();

        Assert.True(
            actor.XRaw > ActorXRaw,
            $"Expected the actor to move away from the ally; XRaw stayed " +
            $"at {actor.XRaw}.");
        Assert.NotEqual(FootworkPhase.Refuse, actor.FootworkPhase);
    }

    /// <summary>
    /// The monotone rule is a monotonicity constraint, not "V9 never
    /// refuses": with the same already-too-close ally now placed on the
    /// enemy's side of the actor -- so the identical route toward the enemy
    /// moves the actor strictly closer to the ally, tightening the
    /// pre-existing violation instead of easing it -- V9 refuses the route
    /// exactly as the absolute rule would. Proves the new branch in
    /// <c>IsLaneClearOfAllies</c> discriminates on direction rather than
    /// passing every candidate under the new preset.
    /// </summary>
    [Fact]
    public void MonotoneRule_UnderV9_StillRefusesARouteThatMovesCloserToAnAlreadyTooCloseAlly()
    {
        var scenario = CreateDuelScenario(MovementPresetId.MonotoneAllyClearanceV9);
        var actor = CreateMeleeAgent(1, factionId: 0, xRaw: ActorXRaw, yRaw: ActorYRaw, scenario);
        var ally = CreateMeleeAgent(
            2, factionId: 0, xRaw: ActorXRaw + AllyOffsetRaw, yRaw: ActorYRaw, scenario);
        var enemy = CreateDistantEnemy(scenario);

        var simulation = BattleSimulation.CreateForTesting(scenario, actor, ally, enemy);
        simulation.AdvanceOneTick();

        Assert.Equal(ActorXRaw, actor.XRaw);
        Assert.Equal(FootworkPhase.Refuse, actor.FootworkPhase);
    }

    // ----- Helpers: the two-agent-plus-enemy directed fixture -----

    private const int ActorXRaw = 2_000;
    private const int ActorYRaw = 2_000;

    // Strictly inside the Kampilan row's 1,536-raw-unit clearance radius
    // (allyClearanceBodyDiametersBasisPoints: 15_000 over the duel
    // scenario's 1,024-raw body diameter), so the ally already violates the
    // actor's clearance at tick start regardless of which side it sits on.
    private const int AllyOffsetRaw = 1_000;

    private static Scenario CreateDuelScenario(MovementPresetId movementPreset) =>
        CreateMeleeScenario(movementPreset) with
        {
            MapWidth = 1_000,
            MapHeight = 1_000,
            AgentsPerFaction = 2,
        };

    // 100 world units east of the actor: far past AttackRangeRaw (5 world
    // units) and well inside PerceptionRangeRaw (200 world units), so the
    // actor selects it as a threat and proposes a direct approach route
    // without triggering an attack on the tick this fixture observes.
    private static AgentState CreateDistantEnemy(Scenario scenario) =>
        CreateMeleeAgent(
            3,
            factionId: 1,
            xRaw: ActorXRaw + (100 * FixedPoint.Scale),
            yRaw: ActorYRaw,
            scenario);

    // ----- Helpers: mirrors RangedStandoffTests.CreateMeleeScenario/Roster -----

    private static Scenario CreateMeleeScenario(MovementPresetId movementPreset) =>
        new(
            Seed: 1,
            MapWidth: 300,
            MapHeight: 200,
            AgentsPerFaction: 3,
            TickRate: 20,
            TickLimit: 4_000)
        {
            MaximumHitPoints = 60,
            DamagePerAttack = 8,
            AttackRangeRaw = 5 * FixedPoint.Scale,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 5,
            LastStandThresholdAgents = 0,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = movementPreset,
        };

    private static AgentState[] CreateMeleeRoster(Scenario scenario) =>
        new[]
        {
            CreateMeleeAgent(1, factionId: 0, xRaw: 0, yRaw: 0, scenario),
            CreateMeleeAgent(2, factionId: 0, xRaw: 0, yRaw: 1_200, scenario),
            CreateMeleeAgent(3, factionId: 0, xRaw: 0, yRaw: 2_400, scenario),
            CreateMeleeAgent(4, factionId: 1, xRaw: 20 * FixedPoint.Scale, yRaw: 0, scenario),
            CreateMeleeAgent(5, factionId: 1, xRaw: 20 * FixedPoint.Scale, yRaw: 1_200, scenario),
            CreateMeleeAgent(6, factionId: 1, xRaw: 20 * FixedPoint.Scale, yRaw: 2_400, scenario),
        };

    private static AgentState CreateMeleeAgent(
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
            Kampilan);
}
