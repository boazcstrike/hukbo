using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The spectator projection of the five authoritative footwork fields
/// (weapon-relative movement design, section 15.1): <c>AgentState.ToView</c>
/// maps facing, retained pace, tactical posture, footwork phase, and the
/// footwork timer positionally onto the five trailing-default
/// <see cref="AgentView"/> members, and all five survive
/// <see cref="BattleSimulation.CreateSnapshot"/>. Tests hold their own
/// <c>AgentState</c> references, which <c>CreateForTesting</c> retains, so
/// every snapshot assertion compares the view against the authoritative
/// field it projects rather than against a re-derived value.
/// </summary>
public sealed class MovementViewProjectionTests
{
    private static readonly CombatLoadout Kampilan =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    [Fact]
    public void ToViewMapsAllFiveMovementFieldsPositionally()
    {
        var scenario = CreateScenario();
        var agent = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        agent.Facing = Facing16.SouthWest;
        agent.MovementPaceRaw = 345;
        agent.TacticalPosture = TacticalPosture.Yield;
        agent.FootworkPhase = FootworkPhase.Recover;
        agent.FootworkTicksRemaining = 2;

        var view = agent.ToView(isLeader: false);

        Assert.Equal(Facing16.SouthWest, view.Facing);
        Assert.Equal(345, view.MovementPaceRaw);
        Assert.Equal(TacticalPosture.Yield, view.TacticalPosture);
        Assert.Equal(FootworkPhase.Recover, view.FootworkPhase);
        Assert.Equal(2, view.FootworkTicksRemaining);
    }

    [Fact]
    public void ViewTrailingDefaultsMatchAFreshLegacyAgent()
    {
        // A view constructed without naming the five members — the shape
        // every presentation test written before the fields existed uses —
        // must agree with the projection of an agent no equipment-relative
        // stage ever touched.
        var scenario = CreateScenario();
        var untouched = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario)
            .ToView(isLeader: false);
        var defaulted = new AgentView(
            EntityId: 1,
            FactionId: 0,
            XRaw: 51_200,
            YRaw: 51_200,
            HitPoints: scenario.MaximumHitPoints,
            MaximumHitPoints: scenario.MaximumHitPoints,
            TargetEntityId: null,
            Intent: AgentIntent.Idle,
            IsAlive: true,
            Loadout: Kampilan);

        Assert.Equal(Facing16.None, defaulted.Facing);
        Assert.Equal(0, defaulted.MovementPaceRaw);
        Assert.Equal(TacticalPosture.None, defaulted.TacticalPosture);
        Assert.Equal(FootworkPhase.None, defaulted.FootworkPhase);
        Assert.Equal(0, defaulted.FootworkTicksRemaining);
        Assert.Equal(defaulted.Facing, untouched.Facing);
        Assert.Equal(defaulted.MovementPaceRaw, untouched.MovementPaceRaw);
        Assert.Equal(defaulted.TacticalPosture, untouched.TacticalPosture);
        Assert.Equal(defaulted.FootworkPhase, untouched.FootworkPhase);
        Assert.Equal(
            defaulted.FootworkTicksRemaining,
            untouched.FootworkTicksRemaining);
    }

    [Fact]
    public void VSixMovementFieldsSurviveCreateSnapshot()
    {
        // A mirrored V6 duel starting 20480 raw apart: far outside the
        // Kampilan preferred distance, so both open in Approach and build
        // retained pace as they close. After ten ticks every one of the
        // five authoritative fields is away from its default on both
        // agents, so the snapshot equality below is not vacuous.
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 112_640, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        for (var tick = 0; tick < 10; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var snapshot = simulation.CreateSnapshot();
        var agents = new[] { west, east };
        Assert.All(agents, agent =>
        {
            var view = snapshot.Agents.Single(
                candidate => candidate.EntityId == agent.EntityId);
            Assert.NotEqual(Facing16.None, view.Facing);
            Assert.True(view.MovementPaceRaw > 0);
            Assert.NotEqual(TacticalPosture.None, view.TacticalPosture);
            Assert.NotEqual(FootworkPhase.None, view.FootworkPhase);
            Assert.Equal(agent.Facing, view.Facing);
            Assert.Equal(agent.MovementPaceRaw, view.MovementPaceRaw);
            Assert.Equal(agent.TacticalPosture, view.TacticalPosture);
            Assert.Equal(agent.FootworkPhase, view.FootworkPhase);
            Assert.Equal(
                agent.FootworkTicksRemaining,
                view.FootworkTicksRemaining);
        });
    }

    [Fact]
    public void LegacyPresetSnapshotsKeepAllFiveFieldsAtTheirDefaults()
    {
        var scenario = CreateScenario() with
        {
            MovementPreset = MovementPresetId.PersistentContingentsV4,
        };
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 112_640, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        for (var tick = 0; tick < 20; tick++)
        {
            simulation.AdvanceOneTick();
        }

        var snapshot = simulation.CreateSnapshot();
        Assert.All(snapshot.Agents, view =>
        {
            Assert.Equal(Facing16.None, view.Facing);
            Assert.Equal(0, view.MovementPaceRaw);
            Assert.Equal(TacticalPosture.None, view.TacticalPosture);
            Assert.Equal(FootworkPhase.None, view.FootworkPhase);
            Assert.Equal(0, view.FootworkTicksRemaining);
        });
    }

    private static Scenario CreateScenario() =>
        new(
            Seed: 1,
            MapWidth: 200,
            MapHeight: 100,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 5_000)
        {
            MaximumHitPoints = 1_000_000,
            DamagePerAttack = 1,
            AttackRangeRaw = 5 * FixedPoint.Scale,
            PerceptionRangeRaw = 200 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 5,
            LastStandThresholdAgents = 0,
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };

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
            Kampilan);
}
