using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// The equipment-relative footwork pipeline of the weapon-relative movement
/// design, sections 6 and 8 through 11, observed through whole ticks: the
/// posture and provisional-phase stage, route selection under the
/// destination precedence chain of section 11.1, lane clearance and the
/// two-step finalisation of section 9.4, the pace and facing model of
/// section 6, attack acceptance marking and <c>Commit</c> entry of section
/// 9.6, same-tick death cleanup, mirrored reflection, the legacy control of
/// section 11.3, and the crowded allocation ceiling. Tests hold their own
/// <c>AgentState</c> references, which <c>CreateForTesting</c> retains, so
/// authoritative footwork state is read directly. Every exact coordinate
/// asserted below is hand-computed from the design's integer formulas; the
/// derivations live in the comments beside each assertion.
/// </summary>
public sealed class MovementPipelineIntegrationTests
{
    private static readonly CombatLoadout Kampilan =
        new(WeaponId.Kampilan, ArmorId.LightOrganic, ShieldId.None);

    // With body radius 512 and movement speed 512, the Kampilan profile
    // resolves to: desired forward pace 501 (512 * 9800 / 10000), lateral
    // 419, backward 358, committed 153, acceleration step 256, deceleration
    // step 307, clearance radius 1536 (1.5 body diameters), preferred
    // distance 5888 against a Kampilan (5120 * 11500 / 10000), immediate
    // context radius 2560, support radius 6144, contact distance 1024.

    // ----- Initial facing and the legacy control (11.3) -----

    [Fact]
    public void VSixInitialFacingIsEastForFactionZeroAndWestForFactionOne()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 112_640, 51_200, scenario);

        _ = BattleSimulation.CreateForTesting(scenario, west, east);

        Assert.Equal(Facing16.East, west.Facing);
        Assert.Equal(Facing16.West, east.Facing);
    }

    [Fact]
    public void ALegacyPresetRunsNoEquipmentStageAtAll()
    {
        var scenario = CreateScenario() with
        {
            MovementPreset = MovementPresetId.PersistentContingentsV4,
        };
        var agents = new[]
        {
            CreateAgent(1, factionId: 0, 92_160, 51_200, scenario),
            CreateAgent(2, factionId: 0, 92_160, 55_296, scenario),
            CreateAgent(3, factionId: 1, 112_640, 51_200, scenario),
            CreateAgent(4, factionId: 1, 112_640, 55_296, scenario),
        };
        var simulation = BattleSimulation.CreateForTesting(scenario, agents);

        for (var tick = 0; tick < 20; tick++)
        {
            simulation.AdvanceOneTick();
        }

        Assert.All(agents, agent =>
        {
            Assert.Equal(Facing16.None, agent.Facing);
            Assert.Equal(0, agent.MovementPaceRaw);
            Assert.Equal(TacticalPosture.None, agent.TacticalPosture);
            Assert.Equal(FootworkPhase.None, agent.FootworkPhase);
            Assert.Equal(0, agent.FootworkTicksRemaining);
        });
        Assert.Equal(0L, simulation.MovementConflictDenialsForTesting);
        Assert.Equal(0L, simulation.LocalMovementContextDerivationsForTesting);
    }

    // ----- Posture (design 8) -----

    [Fact]
    public void PosturesAreWrittenOntoEveryLivingMemberEachTick()
    {
        // One warrior against three: 1 * 2 <= 3 is exact double
        // outnumbering, branch 3, Withdraw. The trio holds a five-to-four
        // advantage (12 >= 5), branch 6, Advance.
        var scenario = CreateScenario();
        var lone = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var first = CreateAgent(2, factionId: 1, 92_160, 49_152, scenario);
        var second = CreateAgent(3, factionId: 1, 92_160, 51_200, scenario);
        var third = CreateAgent(4, factionId: 1, 92_160, 53_248, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, lone, first, second, third);

        simulation.AdvanceOneTick();

        Assert.Equal(TacticalPosture.Withdraw, lone.TacticalPosture);
        Assert.Equal(TacticalPosture.Advance, first.TacticalPosture);
        Assert.Equal(TacticalPosture.Advance, second.TacticalPosture);
        Assert.Equal(TacticalPosture.Advance, third.TacticalPosture);
    }

    // ----- Phase progression (design 9) -----

    [Fact]
    public void ApproachBecomesEngageInsideThePreferredDistance()
    {
        // A mirrored duel starting 20480 raw apart: far outside the 5888
        // preferred distance, so both open in Approach and cross into
        // Engage as they close.
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 112_640, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, west, east);

        // Cooldowns are pinned high before every tick so no attack enters
        // Commit and hides the Approach-to-Engage transition under
        // observation here.
        west.AttackCooldownRemaining = 100;
        east.AttackCooldownRemaining = 100;
        simulation.AdvanceOneTick();
        Assert.Equal(FootworkPhase.Approach, west.FootworkPhase);
        Assert.Equal(FootworkPhase.Approach, east.FootworkPhase);

        var sawEngage = false;
        for (var tick = 0; tick < 30 && !sawEngage; tick++)
        {
            west.AttackCooldownRemaining = 100;
            east.AttackCooldownRemaining = 100;
            simulation.AdvanceOneTick();
            sawEngage = west.FootworkPhase == FootworkPhase.Engage;
        }

        Assert.True(sawEngage, "The duel never reached Engage.");
        Assert.Equal(FootworkPhase.Engage, east.FootworkPhase);
    }

    /// <summary>
    /// Contract H: Engage is not a stop line. Two warriors open inside the
    /// preferred band (5520 &lt;= 5888) but outside attack range (5520 &gt;
    /// 5120); their first-tick 256-unit steps close the gap to 5008, inside
    /// reach, and the existing post-movement attack gate fires the same
    /// tick with no one-tick delay — after which both accepted attackers
    /// enter Commit with the profile's full three-tick duration.
    /// </summary>
    [Fact]
    public void EngageCrossesThePreferredBandAndAttacksTheSameTick()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 97_680, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, west, east);

        simulation.AdvanceOneTick();

        Assert.Equal(1, simulation.Tick);
        Assert.Equal(
            2,
            simulation.LastEvents.Count(
                battleEvent => battleEvent.Kind == BattleEventKind.Attack));
        Assert.Equal(FootworkPhase.Commit, west.FootworkPhase);
        Assert.Equal(3, west.FootworkTicksRemaining);
        Assert.Equal(FootworkPhase.Commit, east.FootworkPhase);
        Assert.Equal(3, east.FootworkTicksRemaining);
    }

    /// <summary>
    /// The full attack lifecycle of design sections 9.1 and 9.5 on a
    /// body-contact duel: the accepted attack enters Commit counting its
    /// entry tick, Commit decrements twice, expires into Recover with the
    /// profile recovery duration, Recover decrements to expiry, and the
    /// tick after expiry resolves Engage against the target inside the
    /// preferred distance. Cooldowns are pinned high between ticks so no
    /// second attack re-enters Commit mid-sequence.
    /// </summary>
    [Fact]
    public void CommitRunsItsFullLifecycleIntoRecover()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 93_184, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, west, east);

        simulation.AdvanceOneTick();
        Assert.Equal(FootworkPhase.Commit, west.FootworkPhase);
        Assert.Equal(3, west.FootworkTicksRemaining);

        var expected = new (FootworkPhase Phase, int Ticks)[]
        {
            (FootworkPhase.Commit, 2),
            (FootworkPhase.Commit, 1),
            (FootworkPhase.Recover, 3),
            (FootworkPhase.Recover, 2),
            (FootworkPhase.Recover, 1),
            (FootworkPhase.Engage, 0),
        };
        foreach (var (phase, ticks) in expected)
        {
            west.AttackCooldownRemaining = 100;
            east.AttackCooldownRemaining = 100;
            simulation.AdvanceOneTick();
            Assert.Equal(phase, west.FootworkPhase);
            Assert.Equal(ticks, west.FootworkTicksRemaining);
        }
    }

    [Fact]
    public void AnAcceptedAttackInterruptsRecoverWithAFreshCommit()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 93_184, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, west, east);
        west.FootworkPhase = FootworkPhase.Recover;
        west.FootworkTicksRemaining = 5;

        simulation.AdvanceOneTick();

        // The provisional step decremented Recover to 4, but the accepted
        // attack replaced it with a fresh Commit at the full duration.
        Assert.Equal(FootworkPhase.Commit, west.FootworkPhase);
        Assert.Equal(3, west.FootworkTicksRemaining);
    }

    // ----- Death cleanup (design 9.6) -----

    [Fact]
    public void DeathClearsFootworkStateAtomicallyAndRetainsFacing()
    {
        var scenario = CreateScenario() with
        {
            MaximumHitPoints = 30,
            DamagePerAttack = 10,
            AttackCooldownTicks = 1,
        };
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 93_184, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, west, east);

        AgentState? dead = null;
        for (var tick = 0; tick < 80 && dead is null; tick++)
        {
            simulation.AdvanceOneTick();
            if (!west.IsAlive)
            {
                dead = west;
            }
            else if (!east.IsAlive)
            {
                dead = east;
            }
        }

        Assert.NotNull(dead);

        // The death tick itself already shows the cleaned state: the agent
        // was attacking every tick, so on its death tick it was an accepted
        // attacker too, and death cleanup won over Commit entry.
        Assert.Equal(0, dead.MovementPaceRaw);
        Assert.Equal(TacticalPosture.None, dead.TacticalPosture);
        Assert.Equal(FootworkPhase.None, dead.FootworkPhase);
        Assert.Equal(0, dead.FootworkTicksRemaining);

        // Final facing is retained as readable spectator information.
        Assert.NotEqual(Facing16.None, dead.Facing);
    }

    // ----- Lane clearance and the two-step finalisation (9.4, 10.5) -----

    /// <summary>
    /// Every candidate lane blocked finalises Refuse from Approach: the
    /// direct endpoint (844 raw from the ally ahead) and both 22.5-degree
    /// obliques (869 raw) all sit strictly inside the 1536 clearance
    /// radius, so the approacher emits no proposal, refuses, and retains
    /// zero pace.
    /// </summary>
    [Fact]
    public void AnApproachWithEveryLaneBlockedFinalisesRefuse()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var allyAhead = CreateAgent(2, factionId: 0, 52_300, 51_200, scenario);
        var enemy = CreateAgent(3, factionId: 1, 71_680, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, allyAhead, enemy);

        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Refuse, actor.FootworkPhase);
        Assert.Equal(0, actor.FootworkTicksRemaining);
        Assert.Equal(51_200, actor.XRaw);
        Assert.Equal(51_200, actor.YRaw);
        Assert.Equal(0, actor.MovementPaceRaw);
    }

    /// <summary>
    /// A blocked lane must not erase a safety lifecycle: a disengaging
    /// warrior whose only route — toward its nearest ally, its contingent
    /// leader being itself — is blocked retains Disengage and simply holds,
    /// rather than refusing. The enemy-to-ally support ratio here is
    /// exactly the 20000-basis-point entry threshold (4 enemies against 2
    /// allies), proving entry equality enters through the whole pipeline.
    /// </summary>
    [Fact]
    public void ABlockedDisengageRetainsItsPhaseAndHolds()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var allyAhead = CreateAgent(2, factionId: 0, 52_300, 51_200, scenario);
        var enemies = new[]
        {
            CreateAgent(3, factionId: 1, 48_128, 53_248, scenario),
            CreateAgent(4, factionId: 1, 48_128, 49_152, scenario),
            CreateAgent(5, factionId: 1, 53_760, 54_272, scenario),
            CreateAgent(6, factionId: 1, 53_760, 48_128, scenario),
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario, [actor, allyAhead, .. enemies]);

        // Pinned high so the actor's own attack cannot enter Commit and
        // mask the retained Disengage this test observes.
        actor.AttackCooldownRemaining = 100;
        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Disengage, actor.FootworkPhase);
        Assert.Equal(51_200, actor.XRaw);
        Assert.Equal(51_200, actor.YRaw);
        Assert.Equal(0, actor.MovementPaceRaw);
    }

    [Fact]
    public void PursueWithNoLivingEnemyAtAllFinalisesRefuse()
    {
        // A world with no enemy resolves posture Pursue (branch 2) and a
        // provisional Pursue phase, which has nothing to pursue: no
        // candidate exists, and Refuse may finalise from Pursue.
        var scenario = CreateScenario();
        var lone = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, lone);

        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Refuse, lone.FootworkPhase);
        Assert.Equal(BattleOutcome.Faction0Victory, simulation.Outcome);
    }

    // ----- The second-threat rule (design 10.4) -----

    /// <summary>
    /// Exact equality keeps the direct candidate. The second threat stands
    /// at x-offset 245 — exactly half the 490-unit direct step — so the
    /// endpoint's squared distance to it equals the start's
    /// (245&#178; + 2438&#178; on both sides), and the actor takes the
    /// direct step (490, 0).
    /// </summary>
    [Fact]
    public void TheSecondThreatRuleKeepsDirectOnExactEquality()
    {
        var scenario = CreateScenario() with
        {
            AttackRangeRaw = 1_024,
            MovementSpeedRaw = 500,
        };
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var target = CreateAgent(2, factionId: 1, 53_600, 51_200, scenario);
        var second = CreateAgent(3, factionId: 1, 51_445, 53_638, scenario);
        // A distant ally exactly at the support radius balances the ratio
        // (two support enemies against two support allies stays below the
        // 20000-basis-point disengage entry) without touching the actor's
        // lanes, so the phase under observation stays Approach.
        var farAlly = CreateAgent(4, factionId: 0, 51_200, 45_056, scenario);
        actor.MovementPaceRaw = 500;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, target, second, farAlly);

        simulation.AdvanceOneTick();

        Assert.Equal(51_690, actor.XRaw);
        Assert.Equal(51_200, actor.YRaw);
    }

    /// <summary>
    /// One raw unit closer omits it. Shifting the second threat to
    /// x-offset 246 makes the direct endpoint strictly closer
    /// (244&#178; &lt; 246&#178; at equal y), so the direct candidate is
    /// omitted and the actor takes its clockwise sideA oblique instead:
    /// delta (2217, 918), pace 490 over distance 2399, endpoint
    /// (+452, +187).
    /// </summary>
    [Fact]
    public void TheSecondThreatRuleOmitsDirectWhenStrictlyCloser()
    {
        var scenario = CreateScenario() with
        {
            AttackRangeRaw = 1_024,
            MovementSpeedRaw = 500,
        };
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var target = CreateAgent(2, factionId: 1, 53_600, 51_200, scenario);
        var second = CreateAgent(3, factionId: 1, 51_446, 53_638, scenario);
        // The same ratio-balancing far ally as the equality variant.
        var farAlly = CreateAgent(4, factionId: 0, 51_200, 45_056, scenario);
        actor.MovementPaceRaw = 500;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, target, second, farAlly);

        simulation.AdvanceOneTick();

        Assert.Equal(51_652, actor.XRaw);
        Assert.Equal(51_387, actor.YRaw);
    }

    // ----- Facing and pace (design 6) -----

    [Fact]
    public void FacingTurnsAtMostTheOrdinaryBudgetPerTick()
    {
        // The threat stands due north (sector 12). From East the shorter
        // arc is four counter-clockwise steps; the Kampilan budget of two
        // reaches NorthEast on tick one and North on tick two.
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var enemy = CreateAgent(2, factionId: 1, 51_200, 30_720, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, enemy);

        simulation.AdvanceOneTick();
        Assert.Equal(Facing16.NorthEast, actor.Facing);

        simulation.AdvanceOneTick();
        Assert.Equal(Facing16.North, actor.Facing);
    }

    [Fact]
    public void CommitCapsTheTurnBudgetAtTheCommittedSteps()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var enemy = CreateAgent(2, factionId: 1, 51_200, 30_720, scenario);
        actor.FootworkPhase = FootworkPhase.Commit;
        actor.FootworkTicksRemaining = 5;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, enemy);

        simulation.AdvanceOneTick();

        // The committed budget is one sector, so the same due-north threat
        // only turns East to EastNorthEast this tick.
        Assert.Equal(Facing16.EastNorthEast, actor.Facing);
        Assert.Equal(FootworkPhase.Commit, actor.FootworkPhase);
        Assert.Equal(4, actor.FootworkTicksRemaining);
    }

    /// <summary>
    /// The committed pace cap and deceleration: a warrior mid-Commit at
    /// full pace 512 wants min(band 9800, committed 3000) = 153 raw, decays
    /// by the 307 deceleration step to 205 on the first tick, and reaches
    /// the 153 cap on the second — each tick's displacement equal to the
    /// resulting pace, because the route is the direct east line.
    /// </summary>
    [Fact]
    public void CommitCapsPaceAndDeceleratesTowardTheCommittedCap()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var enemy = CreateAgent(2, factionId: 1, 61_440, 51_200, scenario);
        actor.FootworkPhase = FootworkPhase.Commit;
        actor.FootworkTicksRemaining = 5;
        actor.MovementPaceRaw = 512;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, enemy);

        simulation.AdvanceOneTick();
        Assert.Equal(51_405, actor.XRaw);
        Assert.Equal(205, actor.MovementPaceRaw);

        simulation.AdvanceOneTick();
        Assert.Equal(51_558, actor.XRaw);
        Assert.Equal(153, actor.MovementPaceRaw);
    }

    /// <summary>
    /// Recover backs away opposite the facing at the backward band: facing
    /// stays East toward the threat, the route runs west (separation 8),
    /// and the first-tick pace is the 256 acceleration step toward the
    /// 358 backward cap — an exact 256-unit step west.
    /// </summary>
    [Fact]
    public void RecoverBacksAwayAtTheBackwardBand()
    {
        var scenario = CreateScenario();
        var actor = CreateAgent(1, factionId: 0, 51_200, 51_200, scenario);
        var enemy = CreateAgent(2, factionId: 1, 71_680, 51_200, scenario);
        actor.FootworkPhase = FootworkPhase.Recover;
        actor.FootworkTicksRemaining = 5;
        var simulation = BattleSimulation.CreateForTesting(
            scenario, actor, enemy);

        simulation.AdvanceOneTick();

        Assert.Equal(50_944, actor.XRaw);
        Assert.Equal(51_200, actor.YRaw);
        Assert.Equal(Facing16.East, actor.Facing);
        Assert.Equal(FootworkPhase.Recover, actor.FootworkPhase);
        Assert.Equal(4, actor.FootworkTicksRemaining);
    }

    /// <summary>
    /// The retained pace commits as min(proposed, actually moved): two
    /// warriors 1124 raw apart both propose 256-unit closing steps that
    /// would interpenetrate, the solid resolver truncates them, and each
    /// warrior's pace equals the distance it truly covered — strictly less
    /// than the proposal.
    /// </summary>
    [Fact]
    public void RetainedPaceCommitsAsTheMinimumOfProposedAndActual()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 93_284, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, west, east);

        simulation.AdvanceOneTick();

        var westMoved = (int)FixedPoint.IntegerSquareRoot(
            CollisionGeometry.SquaredDistance(
                92_160, 51_200, west.XRaw, west.YRaw));
        var eastMoved = (int)FixedPoint.IntegerSquareRoot(
            CollisionGeometry.SquaredDistance(
                93_284, 51_200, east.XRaw, east.YRaw));

        Assert.Equal(westMoved, west.MovementPaceRaw);
        Assert.Equal(eastMoved, east.MovementPaceRaw);
        Assert.True(
            west.MovementPaceRaw < 256 && east.MovementPaceRaw < 256,
            "The resolver let both closing steps through untruncated, so " +
            "this scenario proves nothing about the pace commit.");
    }

    // ----- The precedence chain (design 11.1), adjacent pairs -----

    /// <summary>
    /// Dead outranks everything: once a warrior dies it emits no movement
    /// and no attack, its position freezes, and its facing is retained.
    /// Asserted on the tick after a 1v2 death while the battle is still
    /// ongoing.
    /// </summary>
    [Fact]
    public void DeadOutranksTheBodyContactAttackingHold()
    {
        var scenario = CreateScenario() with
        {
            MaximumHitPoints = 30,
            DamagePerAttack = 10,
            AttackCooldownTicks = 1,
        };
        var lone = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var first = CreateAgent(2, factionId: 1, 93_184, 51_200, scenario);
        var second = CreateAgent(3, factionId: 1, 92_160, 52_224, scenario);
        // A distant same-faction survivor keeps the battle ongoing past the
        // contested warrior's death, so the tick after it can be observed.
        var survivor = CreateAgent(4, factionId: 0, 10_240, 10_240, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, lone, first, second, survivor);

        for (var tick = 0; tick < 80 && lone.IsAlive; tick++)
        {
            simulation.AdvanceOneTick();
        }

        Assert.False(lone.IsAlive);
        Assert.Equal(BattleOutcome.Ongoing, simulation.Outcome);

        var frozenX = lone.XRaw;
        var frozenY = lone.YRaw;
        var frozenFacing = lone.Facing;
        simulation.AdvanceOneTick();

        Assert.Equal(frozenX, lone.XRaw);
        Assert.Equal(frozenY, lone.YRaw);
        Assert.Equal(frozenFacing, lone.Facing);
        Assert.DoesNotContain(
            simulation.LastEvents,
            battleEvent => battleEvent.SourceEntityId == lone.EntityId);
    }

    [Fact]
    public void AttackingHoldOutranksLastStandRegrouping()
    {
        var scenario = CreateScenario() with
        {
            LastStandThresholdAgents = 5,
        };
        var rally = CreateAgent(1, factionId: 0, 20_480, 51_200, scenario);
        var holder = CreateAgent(2, factionId: 0, 92_160, 51_200, scenario);
        var enemy = CreateAgent(3, factionId: 1, 93_184, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, rally, holder, enemy);

        simulation.AdvanceOneTick();

        // At exact body contact the holder keeps its Attacking intent —
        // Regrouping only ever overrides Moving — and proposes no movement
        // even though the last-stand threshold is active.
        Assert.Equal(AgentIntent.Attacking, holder.Intent);
        Assert.Equal(92_160, holder.XRaw);
        Assert.Equal(51_200, holder.YRaw);
    }

    /// <summary>
    /// The last-stand rally destination outranks the V6 disengage route.
    /// The actor's support ratio sits exactly at the disengage entry
    /// threshold, so its committed phase is Disengage — but its Regrouping
    /// intent takes the rally give-way aim (a sideways escape south out of
    /// the rally agent's forward corridor, hand-computed to land at
    /// +5, +256) instead of the disengage step due north toward its
    /// nearest ally.
    /// </summary>
    [Fact]
    public void LastStandRegroupingOutranksTheDisengageRoute()
    {
        var scenario = CreateScenario() with
        {
            LastStandThresholdAgents = 5,
        };
        var rally = CreateAgent(1, factionId: 0, 20_480, 51_200, scenario);
        var allyNorth = CreateAgent(2, factionId: 0, 61_440, 45_056, scenario);
        var actor = CreateAgent(3, factionId: 0, 61_440, 51_200, scenario);
        var enemies = new[]
        {
            CreateAgent(4, factionId: 1, 64_512, 50_176, scenario),
            CreateAgent(5, factionId: 1, 65_536, 51_200, scenario),
            CreateAgent(6, factionId: 1, 64_512, 53_248, scenario),
            CreateAgent(7, factionId: 1, 65_536, 54_272, scenario),
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario, [rally, allyNorth, actor, .. enemies]);

        // Pinned high so the actor's own attack cannot enter Commit and
        // mask the retained Disengage this test observes.
        actor.AttackCooldownRemaining = 100;
        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Disengage, actor.FootworkPhase);
        Assert.Equal(61_445, actor.XRaw);
        Assert.Equal(51_456, actor.YRaw);
    }

    /// <summary>
    /// The V6 disengage route outranks the contingent-cohesion
    /// destination. The contingent resolves Hold — one straggler thirty
    /// units from its leader — and cohesion's give-way corridor would
    /// step the actor north; but the actor's support ratio sits at the
    /// disengage entry threshold, and Disengage steps it south toward its
    /// nearest ally instead.
    /// </summary>
    [Fact]
    public void TheDisengageRouteOutranksContingentCohesion()
    {
        var scenario = CreateScenario();
        var leader = CreateAgent(1, factionId: 0, 30_720, 51_200, scenario);
        var member = CreateAgent(2, factionId: 0, 30_720, 53_248, scenario);
        var actor = CreateAgent(3, factionId: 0, 61_440, 51_200, scenario);
        var allySouth = CreateAgent(4, factionId: 0, 61_440, 57_344, scenario);
        var enemies = new[]
        {
            CreateAgent(5, factionId: 1, 61_440, 46_080, scenario),
            CreateAgent(6, factionId: 1, 60_416, 46_080, scenario),
            CreateAgent(7, factionId: 1, 62_464, 46_080, scenario),
            CreateAgent(8, factionId: 1, 61_440, 45_056, scenario),
        };
        var simulation = BattleSimulation.CreateForTesting(
            scenario, [leader, member, actor, allySouth, .. enemies]);

        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Disengage, actor.FootworkPhase);
        Assert.Equal(61_440, actor.XRaw);
        Assert.Equal(51_456, actor.YRaw);
    }

    /// <summary>
    /// The contingent-cohesion destination outranks the remaining V6
    /// routes. The same Hold contingent without the local enemy pressure
    /// resolves posture Regroup and phase Regroup, whose own route would
    /// step the actor south toward its nearest ally — but cohesion wins,
    /// and its give-way corridor escape steps the actor due north out of
    /// the leader's line of advance.
    /// </summary>
    [Fact]
    public void ContingentCohesionOutranksTheEquipmentRegroupRoute()
    {
        var scenario = CreateScenario();
        var leader = CreateAgent(1, factionId: 0, 30_720, 51_200, scenario);
        var member = CreateAgent(2, factionId: 0, 30_720, 53_248, scenario);
        var actor = CreateAgent(3, factionId: 0, 61_440, 51_200, scenario);
        var allySouth = CreateAgent(4, factionId: 0, 61_440, 57_344, scenario);
        var enemy = CreateAgent(5, factionId: 1, 163_840, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(
            scenario, [leader, member, actor, allySouth, enemy]);

        simulation.AdvanceOneTick();

        Assert.Equal(FootworkPhase.Regroup, actor.FootworkPhase);
        Assert.Equal(61_440, actor.XRaw);
        Assert.Equal(50_944, actor.YRaw);
    }

    /// <summary>
    /// The V6 route replaces ordinary pursuit wholesale: a plain duel's
    /// first-tick step is the pace-model's 256-unit acceleration step, not
    /// the legacy proposal's full 512-unit speed step.
    /// </summary>
    [Fact]
    public void TheEquipmentRouteReplacesOrdinaryPursuit()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 112_640, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, west, east);

        simulation.AdvanceOneTick();

        Assert.Equal(92_416, west.XRaw);
        Assert.Equal(112_384, east.XRaw);
    }

    // ----- Mirrored reflection (design 6.2, 10.3) -----

    /// <summary>
    /// A symmetric duel stays an exact mirror: positions reflect across the
    /// vertical centre line, facings reflect sector for sector, and pace
    /// and phase match, tick after tick. Damage is minimal and hit points
    /// enormous so combat asymmetries in the clash rolls cannot remove
    /// either body from the geometry.
    /// </summary>
    [Fact]
    public void AMirroredDuelReflectsExactlyWhileBothLive()
    {
        var scenario = CreateScenario();
        var west = CreateAgent(1, factionId: 0, 92_160, 51_200, scenario);
        var east = CreateAgent(2, factionId: 1, 112_640, 51_200, scenario);
        var simulation = BattleSimulation.CreateForTesting(scenario, west, east);
        var mapWidthRaw = scenario.MapWidth * FixedPoint.Scale;

        for (var tick = 0; tick < 40; tick++)
        {
            simulation.AdvanceOneTick();

            Assert.True(west.IsAlive && east.IsAlive);
            Assert.Equal(mapWidthRaw, west.XRaw + east.XRaw);
            Assert.Equal(west.YRaw, east.YRaw);
            Assert.Equal(west.MovementPaceRaw, east.MovementPaceRaw);
            Assert.Equal(west.FootworkPhase, east.FootworkPhase);
            Assert.Equal(
                (8 - (int)west.Facing + 16) % 16,
                (int)east.Facing);
        }
    }

    // ----- Determinism and allocation -----

    [Fact]
    public void TwoIdenticalVSixBattlesStayHashIdenticalTickForTick()
    {
        var scenario = Scenario.CreateDefault(seed: 5, totalAgents: 40) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV2,
            MovementPreset = MovementPresetId.EquipmentRelativeFootworkV6,
        };
        var first = BattleSimulation.Create(scenario);
        var second = BattleSimulation.Create(scenario);

        for (var tick = 0; tick < 200; tick++)
        {
            first.AdvanceOneTick();
            second.AdvanceOneTick();
            Assert.Equal(first.ComputeStateHash(), second.ComputeStateHash());
            Assert.Equal(first.Outcome, second.Outcome);
        }
    }

    /// <summary>
    /// The V6 counterpart of the crowded legacy allocation test: two lines
    /// pressed into one another under the equipment-relative pipeline, with
    /// the same 16,384-byte warm-window ceiling. Any per-tick list, lambda
    /// capture, or boxed enumerator in the new stages blows through it by
    /// orders of magnitude.
    /// </summary>
    [Fact]
    public void RepeatedVSixCollisionTicksHaveBoundedAllocations()
    {
        const int measuredTicks = 1_000;
        const long maximumAllocatedBytes = 16_384;
        const int agentsPerFaction = 12;
        var scenario = CreateScenario() with
        {
            TickLimit = (measuredTicks * 2) + 100,
            AttackCooldownTicks = 20,
        };

        var agents = new List<AgentState>(agentsPerFaction * 2);
        for (var index = 0; index < agentsPerFaction; index++)
        {
            agents.Add(CreateAgent(
                checked((ulong)index + 1),
                factionId: 0,
                checked((90 - (index % 3)) * FixedPoint.Scale),
                checked((40 + index) * FixedPoint.Scale),
                scenario));
            agents.Add(CreateAgent(
                checked((ulong)(agentsPerFaction + index) + 1),
                factionId: 1,
                checked((110 + (index % 3)) * FixedPoint.Scale),
                checked((40 + index) * FixedPoint.Scale),
                scenario));
        }

        var simulation = BattleSimulation.CreateForTesting(
            scenario, [.. agents]);

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
        Assert.True(
            firstWindowBytes <= maximumAllocatedBytes,
            $"V6 collision ticks allocated {firstWindowBytes:N0} bytes; " +
            $"expected at most {maximumAllocatedBytes:N0}.");
        Assert.True(
            secondWindowBytes <= maximumAllocatedBytes,
            $"A warm V6 window allocated {secondWindowBytes:N0} bytes; " +
            $"expected at most {maximumAllocatedBytes:N0}.");
    }

    // ----- Helpers -----

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
