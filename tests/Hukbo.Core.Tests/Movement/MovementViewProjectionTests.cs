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
/// <para>
/// The three pressure-interrupt members of the V7 design are covered here as
/// well, and they reach the view by a different route. Only
/// <c>BrokeOffUnderPressure</c> is authoritative agent state; the running
/// pressure value and this warrior's own threshold are assembled by
/// <c>BattleSimulation.UpdateViews</c> from derived per-tick scratch and from
/// the running ruleset, neither of which an <c>AgentState</c> holds. Those two
/// are therefore asserted against a running simulation rather than against a
/// field, and all three are asserted to stay at their defaults under a preset
/// that does not apply the interrupt.
/// </para>
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

    [Fact]
    public void PressureMembersDefaultOnAViewBuiltWithoutNamingThem()
    {
        // The three pressure-interrupt members rest on the same argument the
        // five footwork members above do: AgentView is the type every
        // presentation test constructs by hand, so a trailing member without a
        // default would break all of those call sites at compile time. This
        // also pins that no member declared before them lost its own default,
        // because the construction below names ten arguments and no more.
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

        Assert.False(defaulted.BrokeOffUnderPressure);
        Assert.Equal(0, defaulted.PressureBasisPoints);
        Assert.Equal(0, defaulted.PressureThresholdBasisPoints);
        Assert.False(untouched.BrokeOffUnderPressure);
        Assert.Equal(0, untouched.PressureBasisPoints);
        Assert.Equal(0, untouched.PressureThresholdBasisPoints);
    }

    [Fact]
    public void VSevenViewsCarryARunningPressureValueOnEveryLivingTick()
    {
        // Two Kampilan warriors close from 20480 raw apart and stay locked
        // together. The Kampilan row registers a threshold of 10,000 basis
        // points, while a duel puts exactly one enemy against one ally in the
        // support ring — a saturating-free 10,000 on signal A, weighted at
        // 5,000, with signal B rounding to zero against a million maximum hit
        // points and signal C zero because neither ring ever loses a member.
        // The weighted value therefore settles at exactly 5,000, live and
        // permanently below the bar, and the interrupt never fires in this
        // battle at all. That is precisely the case channel 3 of design
        // section 3, question 8 exists for: the row still has to show a number
        // on every tick, because a value that only appeared on a firing tick
        // would let a spectator witness a break-off but never predict one.
        var scenario = CreateScenario() with
        {
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV7,
        };
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 112_640, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        var highestPressureSeen = 0;
        var readingsWithLivePressureAndNoBreakOff = 0;
        for (var tick = 0; tick < 60; tick++)
        {
            simulation.AdvanceOneTick();
            foreach (var view in simulation.Agents)
            {
                // A million hit points against one damage per attack keeps
                // both warriors alive for the whole run, so every reading
                // below is a living-agent reading.
                Assert.True(view.IsAlive);

                // The threshold is a per-loadout constant, carried on every
                // tick whatever phase the warrior is in.
                Assert.Equal(10_000, view.PressureThresholdBasisPoints);
                highestPressureSeen = Math.Max(
                    highestPressureSeen, view.PressureBasisPoints);
                if (view.PressureBasisPoints > 0 &&
                    !view.BrokeOffUnderPressure)
                {
                    readingsWithLivePressureAndNoBreakOff++;
                }
            }
        }

        Assert.Equal(5_000, highestPressureSeen);
        Assert.True(readingsWithLivePressureAndNoBreakOff > 0);
    }

    [Fact]
    public void VSevenCorpsesCarryNoPressureOnTheTickTheyDie()
    {
        // The scratch slot is cleared by the next tick's footwork stage, not
        // by the death itself, so on the tick a warrior dies it still holds
        // the value computed before the killing blow landed. The projection's
        // living-agent gate is what keeps that stale number off the view, so a
        // corpse reads like a warrior under a legacy preset.
        var scenario = CreateScenario() with
        {
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV7,

            // Twelve hit points against one damage per attack, rather than a
            // single lethal blow. A duel that ends on the first exchange dies
            // on the same tick it enters the support ring, which leaves the
            // scratch slot at zero and makes the assertions below vacuous.
            // Trading for a while first puts a real number in the slot the
            // projection then has to refuse.
            MaximumHitPoints = 12,
            DamagePerAttack = 1,

            // A body diameter of one whole unit puts the preset's six-body-
            // diameter support radius at 6144 raw, barely outside the 5120
            // attack range, so a Kampilan pair that lunges and recovers spends
            // most of its ticks with an empty support ring and can die on a
            // tick whose slot legitimately holds zero. Doubling the body
            // widens the ring to 12288 and keeps the enemy inside it for the
            // whole exchange, which is what makes the stale value real.
            BodyRadiusRaw = FixedPoint.Scale,
        };
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 112_640, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        // The last pressure each warrior showed while alive, so the assertions
        // below cannot pass vacuously: a corpse reading zero proves nothing
        // unless the number it would otherwise have carried was not zero.
        var lastLivingPressure = new Dictionary<ulong, int>();
        var corpsesInspected = 0;
        for (var tick = 0; tick < 200 && corpsesInspected == 0; tick++)
        {
            simulation.AdvanceOneTick();
            foreach (var view in simulation.Agents)
            {
                if (view.IsAlive)
                {
                    lastLivingPressure[view.EntityId] = view.PressureBasisPoints;
                    continue;
                }

                corpsesInspected++;

                // The killing blow lands from inside the attack range, which
                // is inside the support radius, so the tick before this one
                // put an enemy in this warrior's support ring and left a
                // non-zero value in the slot the projection is refusing to
                // read.
                Assert.True(
                    lastLivingPressure[view.EntityId] > 0,
                    $"Entity {view.EntityId} died on tick {tick} carrying " +
                    "no prior pressure, so the zero asserted below proves " +
                    "nothing. Retune the fixture rather than the assertion.");
                Assert.False(view.BrokeOffUnderPressure);
                Assert.Equal(0, view.PressureBasisPoints);
                Assert.Equal(0, view.PressureThresholdBasisPoints);
            }
        }

        Assert.True(corpsesInspected > 0);
    }

    [Fact]
    public void VSixSnapshotsKeepAllThreePressureMembersAtTheirDefaults()
    {
        // V6 uses equipment-relative footwork and so takes the same stages the
        // interrupt lives in, but registers AppliesPressureInterrupt false.
        // Both pressure scratch arrays are therefore zero-length, and the
        // projection must not index either of them.
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 112_640, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, west, east);

        for (var tick = 0; tick < 60; tick++)
        {
            simulation.AdvanceOneTick();
            Assert.All(simulation.Agents, view =>
            {
                Assert.False(view.BrokeOffUnderPressure);
                Assert.Equal(0, view.PressureBasisPoints);
                Assert.Equal(0, view.PressureThresholdBasisPoints);
            });
        }

        Assert.All(simulation.CreateSnapshot().Agents, view =>
        {
            Assert.False(view.BrokeOffUnderPressure);
            Assert.Equal(0, view.PressureBasisPoints);
            Assert.Equal(0, view.PressureThresholdBasisPoints);
        });
    }

    [Fact]
    public void LegacyPresetSnapshotsKeepAllThreePressureMembersAtTheirDefaults()
    {
        // V4 registers no movement profile rows at all, so
        // MovementRuleset.ResolveLoadoutProfile throws for every loadout under
        // it. The projection must never reach that call on this path.
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

        Assert.All(simulation.CreateSnapshot().Agents, view =>
        {
            Assert.False(view.BrokeOffUnderPressure);
            Assert.Equal(0, view.PressureBasisPoints);
            Assert.Equal(0, view.PressureThresholdBasisPoints);
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
