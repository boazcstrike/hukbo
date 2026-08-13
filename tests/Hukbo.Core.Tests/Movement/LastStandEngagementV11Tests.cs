using System.Collections.Generic;
using System.Linq;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests.Movement;

/// <summary>
/// Coverage for <see cref="MovementPresetId.LastStandEngagementV11"/>, the
/// preset that carries the two regroup yields of the last-stand engagement
/// plan. The tests fall into three groups: the preset is registered and
/// inherits every <see cref="MovementPresetId.BattlefieldRealismV10"/>
/// behaviour; each of the two yields fires under V11 and is inert under V10;
/// and neither yield depends on the incidental order of the agent array.
/// </summary>
/// <remarks>
/// <para>
/// The observable for both yields is the follower's <c>Move</c> event target
/// rather than its end-of-tick <see cref="AgentIntent"/>. Intent is not usable
/// here: attack gathering re-marks a warrior that strikes as
/// <see cref="AgentIntent.Attacking"/> regardless of the intent target
/// selection assigned, so a follower with an enemy inside its reach ends the
/// tick <c>Attacking</c> under every preset. The <c>Move</c> event target is
/// unambiguous — a regrouping follower's move names its rally agent, and a
/// pursuing warrior's move names its enemy — and it is what a spectator reads
/// in the battle event log.
/// </para>
/// <para>
/// The geometry below is chosen so the two aim points lie on opposite sides of
/// the follower along the Y axis. Under <see cref="CreateTestScenario"/>'s body
/// radius of half a world unit, the trail sits <c>12 * 0.5 = 6</c> world units
/// behind the rally agent and the jitter reaches at most <c>6 * 0.5 = 3</c>
/// further, so a follower standing well below its rally agent must move toward
/// increasing Y to regroup and toward decreasing Y to fight. No assertion
/// depends on the jitter draw.
/// </para>
/// <para>
/// Three constraints the layout has to satisfy at once, each of which the first
/// draft of this file broke. The rally agent's own nearest enemy must be the
/// one intended for it and not the follower's, because target selection is
/// nearest-first and the rally agent's target is what sets the direction the
/// trail is measured against. The follower's nearest enemy must likewise be
/// its own. And the follower must stand <em>behind</em> the rally agent along
/// that direction of travel: a follower ahead of its leader is inside the
/// give-way corridor and steps purely sideways, which moves it along neither
/// of the two directions under test.
/// </para>
/// </remarks>
public sealed class LastStandEngagementV11Tests
{
    // ----- The preset itself -----

    [Fact]
    public void LastStandEngagementV11IsRegistered()
    {
        Assert.True(
            MovementPresetRegistry.IsRegistered(
                MovementPresetId.LastStandEngagementV11));
    }

    [Fact]
    public void LastStandEngagementV11CarriesItsOwnIdentity()
    {
        var v10 = MovementPresetRegistry.Get(MovementPresetId.BattlefieldRealismV10);
        var v11 = MovementPresetRegistry.Get(MovementPresetId.LastStandEngagementV11);

        Assert.Equal(MovementPresetId.LastStandEngagementV11, v11.Id);

        // The ruleset is a verbatim restatement of V10's field values, so every
        // tunable matches; only the folded Id separates the two content hashes.
        Assert.Equal(v10.CohesionRadiusMultiplier, v11.CohesionRadiusMultiplier);
        Assert.Equal(v10.CloseRadiusMultiplier, v11.CloseRadiusMultiplier);
        Assert.Equal(v10.CohesionCycleTicks, v11.CohesionCycleTicks);
        Assert.Equal(v10.CohesionDutyTicks, v11.CohesionDutyTicks);
        Assert.Equal(v10.ArrivalTaperMultiplier, v11.ArrivalTaperMultiplier);
        Assert.Equal(v10.OffsetUnit, v11.OffsetUnit);
        Assert.Equal(
            v10.NarrowsCohesionScanToCohesionCapableContingents,
            v11.NarrowsCohesionScanToCohesionCapableContingents);
        Assert.Equal(v10.SelectsLeaderByRank, v11.SelectsLeaderByRank);
        Assert.Equal(
            v10.UsesEquipmentRelativeFootwork,
            v11.UsesEquipmentRelativeFootwork);
        Assert.Equal(v10.AppliesPressureInterrupt, v11.AppliesPressureInterrupt);
        Assert.NotEqual(v10.ContentHash, v11.ContentHash);
    }

    /// <summary>
    /// With the last stand disabled outright, V11 has nothing left that
    /// distinguishes it from V10, so a full battle — deployment planning
    /// included, which is why this goes through <c>Create</c> rather than
    /// <c>CreateForTesting</c> — must run byte-identically under both. This is
    /// the test that catches a battlefield-realism gate left keyed to V10
    /// alone: a missed gate would silently drop V10's cohort deployment, its
    /// ranged retreat rung, or its melee-threat scratch from V11.
    /// </summary>
    /// <remarks>
    /// <c>Scenario.MovementPreset</c> folds directly into
    /// <c>ComputeStateHash()</c>, and <c>MovementRuleset.ContentHash</c> folds
    /// its own <c>Id</c>, so two presets can never produce a literally equal
    /// state hash however identically their agents behave. The equality proved
    /// here is the one the V10 suite already uses for the same reason: full
    /// per-agent field equality every tick plus full <c>LastEvents</c>
    /// equality.
    /// </remarks>
    [Fact]
    public void WithNoLastStandV11RunsByteIdenticallyToV10()
    {
        var v10Scenario = Scenario.CreateDefault(seed: 1, totalAgents: 40) with
        {
            LastStandThresholdAgents = 0,
            MovementPreset = MovementPresetId.BattlefieldRealismV10,
        };
        var v11Scenario = v10Scenario with
        {
            MovementPreset = MovementPresetId.LastStandEngagementV11,
        };

        var v10 = BattleSimulation.Create(v10Scenario);
        var v11 = BattleSimulation.Create(v11Scenario);

        var comparedTicks = 0;
        for (var tick = 0; tick < 800 && v10.Outcome == BattleOutcome.Ongoing; tick++)
        {
            v10.AdvanceOneTick();
            v11.AdvanceOneTick();
            comparedTicks++;

            Assert.Equal(v10.Tick, v11.Tick);
            Assert.Equal(v10.Outcome, v11.Outcome);
            Assert.Equal(v10.LastEvents, v11.LastEvents);
            Assert.Equal(v10.Agents.Count, v11.Agents.Count);

            for (var i = 0; i < v10.Agents.Count; i++)
            {
                var left = v10.Agents[i];
                var right = v11.Agents[i];

                Assert.Equal(left.EntityId, right.EntityId);
                Assert.Equal(left.XRaw, right.XRaw);
                Assert.Equal(left.YRaw, right.YRaw);
                Assert.Equal(left.HitPoints, right.HitPoints);
                Assert.Equal(left.Intent, right.Intent);
                Assert.Equal(left.IsAlive, right.IsAlive);
                Assert.Equal(left.TargetEntityId, right.TargetEntityId);
                Assert.Equal(left.MovementResolution, right.MovementResolution);
            }
        }

        Assert.True(comparedTicks > 50, $"Only compared {comparedTicks} ticks.");
    }

    // ----- Yield one: the rally agent is itself engaged -----

    /// <summary>
    /// The defect smoke row <c>LS-1</c> reports, in miniature. The rally agent
    /// stands inside its own weapon reach of an enemy, so it is fighting; its
    /// follower has an enemy of its own a hundred world units away in the
    /// opposite direction. Under V11 the follower walks at that enemy instead
    /// of parking behind the leader.
    /// </summary>
    [Fact]
    public void AFollowerYieldsRegroupingWhileItsRallyAgentIsEngaged()
    {
        var simulation = CreateEngagedLeaderBattle(
            MovementPresetId.LastStandEngagementV11);
        var startYRaw = AgentByEntityId(simulation, FollowerId).YRaw;

        simulation.AdvanceOneTick();
        AssertLayoutInvariants(simulation);

        var move = Assert.Single(
            simulation.LastEvents,
            evt => evt.Kind == BattleEventKind.Move &&
                evt.SourceEntityId == FollowerId);
        Assert.Equal(FollowerEnemyId, move.TargetEntityId);
        Assert.True(
            AgentByEntityId(simulation, FollowerId).YRaw < startYRaw,
            "Expected the follower to close on its own enemy, which lies " +
            "toward decreasing Y, rather than on the trail point behind its " +
            "rally agent, which lies toward increasing Y.");
    }

    /// <summary>
    /// The same layout under V10 keeps the behaviour the tester saw: the
    /// follower is dragged to the trail point and the leader duels alone. This
    /// is the inertness half of the yield — the shared last-stand code is
    /// unversioned, so every preset before V11 has to be proved untouched.
    /// </summary>
    [Fact]
    public void UnderV10AFollowerStillRegroupsWhileItsRallyAgentIsEngaged()
    {
        var simulation = CreateEngagedLeaderBattle(
            MovementPresetId.BattlefieldRealismV10);
        var startYRaw = AgentByEntityId(simulation, FollowerId).YRaw;

        simulation.AdvanceOneTick();
        AssertLayoutInvariants(simulation);

        var move = Assert.Single(
            simulation.LastEvents,
            evt => evt.Kind == BattleEventKind.Move &&
                evt.SourceEntityId == FollowerId);
        Assert.Equal(RallyId, move.TargetEntityId);
        Assert.True(
            AgentByEntityId(simulation, FollowerId).YRaw > startYRaw,
            "Expected V10 to keep dragging the follower to the trail point " +
            "behind its rally agent, toward increasing Y.");
    }

    /// <summary>
    /// The gathering phase is deliberately untouched. While the rally agent is
    /// still travelling toward an enemy it has not reached, its followers trail
    /// behind it under V11 exactly as they do under V10 — the column on the
    /// march that smoke rows 76 and 77 passed on.
    /// </summary>
    [Fact]
    public void AFollowerStillRegroupsWhileItsRallyAgentIsStillTravelling()
    {
        var simulation = CreateTravellingLeaderBattle(
            MovementPresetId.LastStandEngagementV11);
        var startYRaw = AgentByEntityId(simulation, FollowerId).YRaw;

        simulation.AdvanceOneTick();
        AssertLayoutInvariants(simulation);

        var move = Assert.Single(
            simulation.LastEvents,
            evt => evt.Kind == BattleEventKind.Move &&
                evt.SourceEntityId == FollowerId);
        Assert.Equal(RallyId, move.TargetEntityId);
        Assert.True(
            AgentByEntityId(simulation, FollowerId).YRaw > startYRaw,
            "Expected the follower to keep trailing its still-travelling " +
            "rally agent, toward increasing Y.");
    }

    // ----- Yield two: the follower's own enemy is inside its own reach -----

    /// <summary>
    /// The second, smaller instance of the same theme, named in the design's
    /// section 2. The rally agent is far from any enemy and still travelling,
    /// so yield one does not apply; the follower has an enemy inside its own
    /// weapon reach but outside body contact, which is exactly the band in
    /// which the existing override still drags it away.
    /// </summary>
    [Fact]
    public void AFollowerYieldsRegroupingWhenItsOwnEnemyIsInsideItsOwnReach()
    {
        var simulation = CreateEnemyInReachBattle(
            MovementPresetId.LastStandEngagementV11);
        var startYRaw = AgentByEntityId(simulation, FollowerId).YRaw;

        simulation.AdvanceOneTick();
        AssertLayoutInvariants(simulation);

        var move = Assert.Single(
            simulation.LastEvents,
            evt => evt.Kind == BattleEventKind.Move &&
                evt.SourceEntityId == FollowerId);
        Assert.Equal(FollowerEnemyId, move.TargetEntityId);
        Assert.True(
            AgentByEntityId(simulation, FollowerId).YRaw < startYRaw,
            "Expected the follower to close on the enemy already inside its " +
            "own reach, toward decreasing Y.");
    }

    [Fact]
    public void UnderV10AFollowerWithAnEnemyInReachIsStillDraggedToTheTrailPoint()
    {
        var simulation = CreateEnemyInReachBattle(
            MovementPresetId.BattlefieldRealismV10);
        var startYRaw = AgentByEntityId(simulation, FollowerId).YRaw;

        simulation.AdvanceOneTick();
        AssertLayoutInvariants(simulation);

        var move = Assert.Single(
            simulation.LastEvents,
            evt => evt.Kind == BattleEventKind.Move &&
                evt.SourceEntityId == FollowerId);
        Assert.Equal(RallyId, move.TargetEntityId);
        Assert.True(
            AgentByEntityId(simulation, FollowerId).YRaw > startYRaw,
            "Expected V10 to keep dragging a follower with an enemy in reach " +
            "toward the trail point, toward increasing Y.");
    }

    /// <summary>
    /// Body contact is not the threshold either yield uses; weapon reach is.
    /// This pins the distinction directly, because the two are three world
    /// units apart in <see cref="CreateTestScenario"/> and a reader could
    /// otherwise not tell which one the yield fired on.
    /// </summary>
    [Fact]
    public void TheYieldFiresAtWeaponReachRatherThanAtBodyContact()
    {
        var scenario = CreateTestScenario(MovementPresetId.LastStandEngagementV11);
        var contactSquared = CollisionGeometry.ContactSquaredDistance(
            scenario.BodyRadiusRaw);
        var separationRaw = (long)EngagementSeparationRaw;

        Assert.True(
            separationRaw * separationRaw > contactSquared,
            "The engaged layout must place the pair outside body contact, or " +
            "the test could not tell weapon reach from contact.");
        Assert.True(
            separationRaw <= scenario.AttackRangeRaw,
            "The engaged layout must place the pair inside weapon reach.");
    }

    // ----- Determinism -----

    /// <summary>
    /// Neither yield may depend on where an agent happens to sit in the agent
    /// array. The rally agent's engagement is derived in its own pass before
    /// any intent is assigned, as a minimum over squared distances, precisely
    /// so that a follower processed before its own leader reads the same answer
    /// as one processed after it.
    /// </summary>
    [Fact]
    public void TheYieldIsUnchangedByAgentArrayPermutation()
    {
        var scenario = CreateTestScenario(MovementPresetId.LastStandEngagementV11);

        var orderingA = BattleSimulation.CreateForTesting(
            scenario, EngagedLeaderAgents(scenario));
        var orderingB = BattleSimulation.CreateForTesting(
            scenario, EngagedLeaderAgents(scenario).Reverse().ToArray());

        orderingA.AdvanceOneTick();
        orderingB.AdvanceOneTick();

        var followerA = AgentByEntityId(orderingA, FollowerId);
        var followerB = AgentByEntityId(orderingB, FollowerId);

        Assert.Equal((followerA.XRaw, followerA.YRaw), (followerB.XRaw, followerB.YRaw));
        Assert.Equal(orderingA.ComputeStateHash(), orderingB.ComputeStateHash());
        Assert.Equal(orderingA.LastEvents, orderingB.LastEvents);
    }

    /// <summary>
    /// The termination bar the shared last-stand path already holds, re-run
    /// against the new preset. The yields only ever hand a follower back to
    /// ordinary pursuit, which is the path a battle above the last-stand
    /// threshold takes every tick, so a stall here would mean the yield had
    /// created a configuration neither path produces on its own.
    /// </summary>
    /// <remarks>
    /// Run at <see cref="FormationRules.MaximumLastStandThresholdAgents"/> and
    /// at the same eighteen-agent size
    /// <c>LastStandFormationTests.NoLastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughTwoHundred</c>
    /// uses, so both factions are in their last stand from tick zero. The seed
    /// count is sixty rather than that test's two hundred: this bar guards a
    /// yield that can only ever reduce time spent in the rally formation,
    /// whereas that one guards the formation deadlock itself, which the same
    /// test's own remarks record as occurring on a low single-digit percentage
    /// of seeds and therefore needing the wider sweep.
    /// </remarks>
    [Fact]
    public void NoV11LastStandBattleStallsAtTheTickLimitAcrossSeedsOneThroughSixty()
    {
        const int TotalAgents = 18;
        const ulong LastSeed = 60;
        var stalledSeeds = new List<string>();
        var seedsThatEnteredTheLastStand = 0;
        var totalTicks = 0L;

        for (ulong seed = 1; seed <= LastSeed; seed++)
        {
            var scenario = Scenario.CreateDefault(seed, totalAgents: TotalAgents) with
            {
                LastStandThresholdAgents = FormationRules.MaximumLastStandThresholdAgents,
                MovementPreset = MovementPresetId.LastStandEngagementV11,
            };
            var simulation = BattleSimulation.Create(scenario);

            while (simulation.Outcome == BattleOutcome.Ongoing &&
                simulation.Tick < scenario.TickLimit)
            {
                simulation.AdvanceOneTick();
            }

            totalTicks = checked(totalTicks + simulation.Tick);
            if (simulation.Tick > 0)
            {
                // Nine per faction is exactly the threshold, so every battle
                // that ran a tick at all was in its last stand from tick zero.
                seedsThatEnteredTheLastStand++;
            }

            if (simulation.Tick >= scenario.TickLimit)
            {
                var living0 = simulation.Agents.Count(
                    agent => agent.FactionId == 0 && agent.IsAlive);
                var living1 = simulation.Agents.Count(
                    agent => agent.FactionId == 1 && agent.IsAlive);
                stalledSeeds.Add(
                    $"seed {seed} (living counts [{living0}, {living1}])");
            }
        }

        // The guard against a vacuous pass: a sweep whose battles all ended on
        // tick zero would satisfy the stall assertion below without ever having
        // exercised the yield at all.
        Assert.Equal((int)LastSeed, seedsThatEnteredTheLastStand);
        Assert.True(
            totalTicks > 1_000,
            $"The sweep simulated only {totalTicks} ticks in total, too few " +
            "to have exercised the endgame it is meant to bar.");
        Assert.True(
            stalledSeeds.Count == 0,
            "Expected every V11 last-stand battle to resolve before the tick " +
            $"limit; these did not: {string.Join(", ", stalledSeeds)}.");
    }

    // ----- Fixtures -----

    private const ulong RallyId = 2;
    private const ulong FollowerId = 5;
    private const ulong RallyEnemyId = 100;
    private const ulong FollowerEnemyId = 101;

    // Three world units: outside the one-world-unit body contact distance
    // CreateTestScenario's half-unit body radius produces, and inside its
    // five-world-unit attack range.
    private const int EngagementSeparationRaw = 3 * FixedPoint.Scale;

    // Every agent sits on one vertical line, so the rally agent's direction of
    // travel is purely +Y and the trail lies purely -Y from it.
    private const int AxisXRaw = 1_000 * FixedPoint.Scale;
    private const int RallyYRaw = 1_000 * FixedPoint.Scale;

    // Three hundred world units below the rally agent: far enough that the
    // rally agent's own enemy, whichever layout places it, stays nearer to the
    // rally agent than the follower's enemy does.
    private const int FollowerYRaw = 700 * FixedPoint.Scale;

    // A hundred and fifty world units above the rally agent: outside the
    // five-unit reach, so the rally agent is travelling rather than fighting,
    // and still nearer to it than the follower's enemy three hundred below.
    private const int TravellingEnemyYRaw = 1_150 * FixedPoint.Scale;

    /// <summary>
    /// The rally agent stands <see cref="EngagementSeparationRaw"/> from an
    /// enemy — inside its own weapon reach, outside body contact — while its
    /// follower has an enemy of its own a hundred world units further down.
    /// The trail point lies toward increasing Y and the follower's enemy
    /// toward decreasing Y, so the two candidate destinations can never be
    /// confused for one another.
    /// </summary>
    private static BattleSimulation CreateEngagedLeaderBattle(
        MovementPresetId preset)
    {
        var scenario = CreateTestScenario(preset);
        return BattleSimulation.CreateForTesting(
            scenario, EngagedLeaderAgents(scenario));
    }

    private static AgentState[] EngagedLeaderAgents(Scenario scenario) =>
    [
        CreateAgent(RallyId, factionId: 0, AxisXRaw, RallyYRaw, scenario),
        CreateAgent(FollowerId, factionId: 0, AxisXRaw, FollowerYRaw, scenario),
        CreateAgent(
            RallyEnemyId,
            factionId: 1,
            AxisXRaw,
            checked(RallyYRaw + EngagementSeparationRaw),
            scenario),
        CreateAgent(
            FollowerEnemyId,
            factionId: 1,
            AxisXRaw,
            checked(FollowerYRaw - (100 * FixedPoint.Scale)),
            scenario),
    ];

    /// <summary>
    /// The gathering layout: the rally agent's enemy is a hundred and fifty
    /// world units away, well outside its five-unit reach, so the rally agent
    /// is travelling rather than fighting. The follower's own enemy is likewise
    /// well outside its reach, so neither yield may fire.
    /// </summary>
    private static BattleSimulation CreateTravellingLeaderBattle(
        MovementPresetId preset)
    {
        var scenario = CreateTestScenario(preset);
        return BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(RallyId, factionId: 0, AxisXRaw, RallyYRaw, scenario),
            CreateAgent(FollowerId, factionId: 0, AxisXRaw, FollowerYRaw, scenario),
            CreateAgent(
                RallyEnemyId, factionId: 1, AxisXRaw, TravellingEnemyYRaw, scenario),
            CreateAgent(
                FollowerEnemyId,
                factionId: 1,
                AxisXRaw,
                checked(FollowerYRaw - (100 * FixedPoint.Scale)),
                scenario));
    }

    /// <summary>
    /// The own-reach layout: the rally agent is travelling toward the same
    /// distant enemy the gathering layout uses, so yield one cannot fire, while
    /// the follower's own enemy sits <see cref="EngagementSeparationRaw"/>
    /// away — inside its reach, outside body contact.
    /// </summary>
    private static BattleSimulation CreateEnemyInReachBattle(
        MovementPresetId preset)
    {
        var scenario = CreateTestScenario(preset);
        return BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(RallyId, factionId: 0, AxisXRaw, RallyYRaw, scenario),
            CreateAgent(FollowerId, factionId: 0, AxisXRaw, FollowerYRaw, scenario),
            CreateAgent(
                RallyEnemyId, factionId: 1, AxisXRaw, TravellingEnemyYRaw, scenario),
            CreateAgent(
                FollowerEnemyId,
                factionId: 1,
                AxisXRaw,
                checked(FollowerYRaw - EngagementSeparationRaw),
                scenario));
    }

    /// <summary>
    /// The layout invariants every fixture above depends on, asserted rather
    /// than assumed: the rally agent targets its own enemy, the follower
    /// targets its own, and the follower stands behind the rally agent. A
    /// fixture that broke any one of these would still produce a green
    /// assertion for the wrong reason.
    /// </summary>
    private static void AssertLayoutInvariants(BattleSimulation simulation)
    {
        var rally = AgentByEntityId(simulation, RallyId);
        var follower = AgentByEntityId(simulation, FollowerId);

        Assert.Equal(RallyEnemyId, rally.TargetEntityId);
        Assert.Equal(FollowerEnemyId, follower.TargetEntityId);
        Assert.True(
            follower.YRaw < rally.YRaw,
            "The follower must stand behind the rally agent along its +Y " +
            "direction of travel, or it lands in the give-way corridor and " +
            "steps sideways instead of toward either destination under test.");
    }

    private static AgentView AgentByEntityId(
        BattleSimulation simulation,
        ulong entityId) =>
        Assert.Single(
            simulation.Agents,
            agent => agent.EntityId == entityId);

    /// <summary>
    /// The same shape <c>LastStandFormationTests.CreateTestScenario</c> builds,
    /// with the movement preset named explicitly rather than inherited, and a
    /// last-stand threshold of two so a two-warrior faction is in its last
    /// stand from tick zero.
    /// </summary>
    private static Scenario CreateTestScenario(MovementPresetId preset) =>
        new(
            Seed: 1,
            MapWidth: 2000,
            MapHeight: 2000,
            AgentsPerFaction: 1,
            TickRate: 20,
            TickLimit: 1_000)
        {
            MaximumHitPoints = 100,
            DamagePerAttack = 10,
            AttackRangeRaw = 5 * FixedPoint.Scale,
            PerceptionRangeRaw = 1_000 * FixedPoint.Scale,
            BodyRadiusRaw = FixedPoint.Scale / 2,
            MovementSpeedRaw = FixedPoint.Scale / 2,
            AttackCooldownTicks = 1,
            LastStandThresholdAgents = 2,
            MovementPreset = preset,
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
            new CombatLoadout(
                WeaponId.Kampilan,
                ArmorId.LightOrganic,
                ShieldId.None));
}
