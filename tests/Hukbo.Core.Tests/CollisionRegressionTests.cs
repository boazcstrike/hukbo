using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;

namespace Hukbo.Core.Tests;

/// <summary>
/// End-to-end regression tests for the solid-disc collision contract recorded in
/// <c>docs/decisions/2026-07-27-collision-policy.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every test here drives a real <see cref="BattleSimulation"/>, so it observes
/// the integrated tick pipeline — cooldowns, intent, proposal, resolution,
/// commit, attack, outcome — rather than the collision components in isolation.
/// The component suites (<c>CollisionGeometryTests</c>, <c>CollisionPairTests</c>,
/// <c>CollisionUniformGridTests</c>, <c>CollisionResolverTests</c>,
/// <c>CollisionMetricsTests</c>) already cover the pieces; what they cannot see
/// is whether the pieces are wired together in the approved order.
/// </para>
/// <para>
/// Each test is named for the acceptance row of
/// <c>docs/plans/2026-07-27-formation-collision-mechanics.md</c> that it locks.
/// </para>
/// <para>
/// The post-tick overlap invariant is checked by brute force over every ordered
/// pair, deliberately not through <c>CollisionUniformGrid</c>. Using the grid
/// would let a broad-phase defect hide the very overlap the invariant exists to
/// catch.
/// </para>
/// </remarks>
public sealed class CollisionRegressionTests
{
    private const int Scale = FixedPoint.Scale;

    private const int BodyRadiusRaw = CollisionRules.DefaultBodyRadiusRaw;

    /// <summary>Both axes of the hand-built map, in raw fixed-point units.</summary>
    private const int MapDimensionRaw = 200 * Scale;

    /// <summary>The largest legal centre coordinate on either axis.</summary>
    private const int MaximumCentreRaw = MapDimensionRaw - BodyRadiusRaw;

    /// <summary>
    /// Rows of a hand-built line are spaced exactly one body diameter apart, so
    /// neighbours rest in tangent contact. Tangency is clearance, not collision.
    /// Nine world units at the enlarged 4.25-world-unit collision radius (task
    /// C1, docs/plans/2026-07-28-collision-report-and-shell.md); it was eight
    /// before that change.
    /// </summary>
    private const int RowSpacingWorld = 9;

    /// <summary>
    /// Acceptance row <c>Head-on</c> / the authoritative post-tick invariant of
    /// decision record section 1: no two living agents may strictly overlap at
    /// the end of any tick, for the whole length of a real battle.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void PostTickInvariant_NoTwoLivingAgentsEverStrictlyOverlap(ulong seed)
    {
        var scenario = Scenario.CreateDefault(seed, totalAgents: 60);
        var simulation = BattleSimulation.Create(scenario);

        FailOnLivingOverlap(simulation, scenario.BodyRadiusRaw, seed);

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();
            FailOnLivingOverlap(simulation, scenario.BodyRadiusRaw, seed);
        }

        Assert.NotEqual(BattleOutcome.Ongoing, simulation.Outcome);
    }

    /// <summary>
    /// Acceptance row <c>Multiple blockers</c> / decision record section 6:
    /// collision may only reduce displacement, never add any. The exact
    /// co-location repair is the single documented exemption and is reported as
    /// <see cref="MovementResolution.Separated"/>.
    /// </summary>
    [Fact]
    public void MovementBudget_NoAgentStepsFurtherThanItsSpeedUnlessItWasSeparated()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 60);
        var simulation = BattleSimulation.Create(scenario);
        var previous = SnapshotPositions(simulation);
        var longestStepRaw = 0L;

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();

            foreach (var agent in simulation.Agents)
            {
                var start = previous[agent.EntityId];
                var steppedRaw = IntegerSquareRoot(
                    SquaredDistance(start.XRaw, start.YRaw, agent.XRaw, agent.YRaw));
                previous[agent.EntityId] = (agent.XRaw, agent.YRaw);

                if (agent.MovementResolution == MovementResolution.Separated)
                {
                    continue;
                }

                if (steppedRaw > scenario.MovementSpeedRaw)
                {
                    Assert.Fail(
                        $"Entity {agent.EntityId} moved {steppedRaw} raw units on " +
                        $"tick {simulation.Tick}, exceeding the movement budget of " +
                        $"{scenario.MovementSpeedRaw}, and reported " +
                        $"{agent.MovementResolution} rather than Separated.");
                }

                longestStepRaw = Math.Max(longestStepRaw, steppedRaw);
            }
        }

        Assert.NotEqual(BattleOutcome.Ongoing, simulation.Outcome);
        Assert.Equal(scenario.MovementSpeedRaw, longestStepRaw);
    }

    /// <summary>
    /// Acceptance row <c>Attack eligibility</c> / decision record section 3:
    /// intent selection and attack gathering call one shared reach helper, so
    /// they cannot disagree about who may strike whom.
    /// </summary>
    /// <remarks>
    /// The two lines start in tangent contact and every agent is therefore
    /// already inside reach, so nobody proposes movement and the geometry is
    /// identical at intent time and at attack time. The cooldown is one tick, so
    /// every living agent is ready to strike on every tick and a missing attack
    /// event cannot be excused by a cooldown.
    /// </remarks>
    [Fact]
    public void AttackEligibility_AttackingIntentWithAReadyCooldownAlwaysProducesAnAttack()
    {
        const int rows = 8;
        var scenario = LineScenario(rows) with { AttackCooldownTicks = 1 };
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            BuildOpposingLines(
                scenario,
                leftXRaw: 60 * Scale,
                rightXRaw: (60 * Scale) + (2 * scenario.BodyRadiusRaw),
                rows));
        var matchedAttackers = 0;

        for (var tick = 0; tick < 5; tick++)
        {
            simulation.AdvanceOneTick();

            var attacks = simulation.LastEvents
                .Where(battleEvent => battleEvent.Kind == BattleEventKind.Attack)
                .Select(battleEvent =>
                    (battleEvent.SourceEntityId, battleEvent.TargetEntityId))
                .ToHashSet();
            var living = simulation.Agents
                .Where(agent => agent.IsAlive)
                .ToDictionary(agent => agent.EntityId);

            foreach (var agent in living.Values)
            {
                if (agent.Intent != AgentIntent.Attacking ||
                    agent.TargetEntityId is not { } targetId ||
                    !living.ContainsKey(targetId))
                {
                    continue;
                }

                if (!attacks.Contains((agent.EntityId, (ulong?)targetId)))
                {
                    Assert.Fail(
                        $"Entity {agent.EntityId} finished tick {simulation.Tick} " +
                        $"with intent Attacking against the living entity {targetId} " +
                        "and a ready cooldown, but produced no attack event. Intent " +
                        "selection and attack gathering disagree about reach.");
                }

                matchedAttackers++;
            }

            foreach (var attack in attacks)
            {
                var source = living[attack.SourceEntityId];

                if (source.Intent != AgentIntent.Attacking)
                {
                    Assert.Fail(
                        $"Entity {source.EntityId} attacked on tick " +
                        $"{simulation.Tick} but reports intent {source.Intent}.");
                }
            }

            FailOnLivingOverlap(simulation, scenario.BodyRadiusRaw, scenario.Seed);
        }

        Assert.Equal(rows * 2 * 5, matchedAttackers);
    }

    /// <summary>
    /// Acceptance row <c>Packed front</c> / decision record section 3. Two bodies
    /// pressed into contact sit exactly one diameter apart — eight and a half
    /// world units at the enlarged 4.25-world-unit collision radius (task C1,
    /// docs/plans/2026-07-28-collision-report-and-shell.md); it was eight
    /// before that change — against a twelve-world-unit reach, so a packed
    /// front must deal damage rather than deadlock. This is the row that
    /// proves solid contact did not strangle combat. The separation is derived
    /// from <c>BodyRadiusRaw</c> rather than written out as a whole number of
    /// world units, so the lines stay in true contact if the radius is retuned.
    /// </summary>
    [Fact]
    public void PackedFront_OpposingBodiesInContactStayInsideReachAndDealDamage()
    {
        const int rows = 10;
        var scenario = LineScenario(rows);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            BuildOpposingLines(
                scenario,
                leftXRaw: 60 * Scale,
                rightXRaw: (60 * Scale) + (2 * scenario.BodyRadiusRaw),
                rows));
        var contactTicksWithDamage = 0;

        for (var tick = 0; tick < 12; tick++)
        {
            simulation.AdvanceOneTick();
            FailOnLivingOverlap(simulation, scenario.BodyRadiusRaw, scenario.Seed);

            var dealtDamage = simulation.LastEvents.Any(
                battleEvent => battleEvent.Kind == BattleEventKind.Damage);

            if (dealtDamage &&
                CountOpposingContacts(simulation, scenario.BodyRadiusRaw) > 0)
            {
                contactTicksWithDamage++;
            }
        }

        Assert.True(
            contactTicksWithDamage > 0,
            "A front of opposing bodies in solid contact never dealt damage on a " +
            "tick where a contact pair existed, so collision has deadlocked combat.");

        var wounded = simulation.Agents.Count(
            agent => agent.HitPoints < agent.MaximumHitPoints);
        Assert.Equal(rows * 2, wounded);
    }

    /// <summary>
    /// Acceptance row <c>Packed front</c>, approach half: dense lines that have
    /// to walk into reach through their own crowd still reach it and still deal
    /// damage.
    /// </summary>
    [Fact]
    public void PackedFront_DenseLinesThatMarchIntoReachStillDealDamage()
    {
        const int rows = 10;
        var scenario = LineScenario(rows);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            BuildOpposingLines(
                scenario,
                leftXRaw: 60 * Scale,
                rightXRaw: 100 * Scale,
                rows));
        var firstDamageTick = 0L;

        for (var tick = 0; tick < 40 && firstDamageTick == 0; tick++)
        {
            simulation.AdvanceOneTick();
            FailOnLivingOverlap(simulation, scenario.BodyRadiusRaw, scenario.Seed);

            if (simulation.LastEvents.Any(
                battleEvent => battleEvent.Kind == BattleEventKind.Damage))
            {
                firstDamageTick = simulation.Tick;
            }
        }

        Assert.True(
            firstDamageTick > 0,
            "Two dense opposing lines forty world units apart never dealt damage " +
            "within forty ticks, so the collision stage has blocked the approach.");
    }

    /// <summary>
    /// Acceptance row <c>Dead behavior</c> / decision record sections 4 and 7:
    /// corpses do not move, do not block, and do not generate contacts, and a
    /// living agent may finish a tick standing exactly on one.
    /// </summary>
    /// <remarks>
    /// The mover advances exactly <c>MovementSpeedRaw</c> along X on every tick,
    /// so after ten ticks its centre lands exactly on the corpse's centre. If
    /// corpses collided, the mover would be blocked short of that tick instead.
    /// </remarks>
    [Fact]
    public void DeadBehaviour_CorpsesNeitherMoveNorBlockAndMayBeStoodOn()
    {
        const int corpseXWorld = 50;
        const int ticksToReachCorpse = 10;
        var scenario = LineScenario(agentsPerFaction: 2);
        var corpse = CreateAgent(
            entityId: 2,
            factionId: 1,
            corpseXWorld * Scale,
            50 * Scale,
            scenario);
        corpse.HitPoints = 0;
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(entityId: 1, factionId: 0, 20 * Scale, 50 * Scale, scenario),
            corpse,
            CreateAgent(entityId: 3, factionId: 1, 150 * Scale, 50 * Scale, scenario));

        for (var tick = 0; tick < ticksToReachCorpse; tick++)
        {
            simulation.AdvanceOneTick();

            var corpseView = simulation.Agents.Single(agent => agent.EntityId == 2);
            Assert.Equal(corpseXWorld * Scale, corpseView.XRaw);
            Assert.Equal(50 * Scale, corpseView.YRaw);
            Assert.Equal(MovementResolution.None, corpseView.MovementResolution);
            Assert.Equal(AgentIntent.Dead, corpseView.Intent);
            Assert.DoesNotContain(
                simulation.LastEvents,
                battleEvent => battleEvent.SourceEntityId == 2);
            FailOnLivingOverlap(simulation, scenario.BodyRadiusRaw, scenario.Seed);
        }

        var mover = simulation.Agents.Single(agent => agent.EntityId == 1);
        Assert.Equal(corpseXWorld * Scale, mover.XRaw);
        Assert.Equal(50 * Scale, mover.YRaw);
        Assert.Equal(MovementResolution.Moved, mover.MovementResolution);

        simulation.AdvanceOneTick();

        var movedOn = simulation.Agents.Single(agent => agent.EntityId == 1);
        Assert.Equal(
            (corpseXWorld * Scale) + scenario.MovementSpeedRaw,
            movedOn.XRaw);
    }

    /// <summary>
    /// Acceptance row <c>Corner</c> / decision record section 5: centres are
    /// clamped into <c>[R, dimension - R]</c> on each axis independently, so
    /// corner contact is simply both axes clamping in the same tick.
    /// </summary>
    /// <remarks>
    /// A chase can never breach a wall on its own, because a mover stops at least
    /// one attack range short of its target and the approved attack range is at
    /// least one body diameter. The mover is therefore hand-placed outside the
    /// legal band, which only <c>CreateForTesting</c> permits, and the assertion
    /// is that its first committed position is pulled back inside.
    /// </remarks>
    [Theory]
    [InlineData(0, 0, 60, 60, BodyRadiusRaw, BodyRadiusRaw)]
    [InlineData(MapDimensionRaw, MapDimensionRaw, 140, 140, MaximumCentreRaw, MaximumCentreRaw)]
    [InlineData(0, MapDimensionRaw, 60, 140, BodyRadiusRaw, MaximumCentreRaw)]
    [InlineData(MapDimensionRaw, 0, 140, 60, MaximumCentreRaw, BodyRadiusRaw)]
    [InlineData(0, 100 * Scale, 60, 100, BodyRadiusRaw, 100 * Scale)]
    [InlineData(MapDimensionRaw, 100 * Scale, 140, 100, MaximumCentreRaw, 100 * Scale)]
    [InlineData(100 * Scale, 0, 100, 60, 100 * Scale, BodyRadiusRaw)]
    [InlineData(100 * Scale, MapDimensionRaw, 100, 140, 100 * Scale, MaximumCentreRaw)]
    public void BoundaryAndCorner_CommittedCentresStayInsideTheLegalBandOnBothAxes(
        int startXRaw,
        int startYRaw,
        int targetXWorld,
        int targetYWorld,
        int expectedXRaw,
        int expectedYRaw)
    {
        var scenario = LineScenario(agentsPerFaction: 1);
        var simulation = BattleSimulation.CreateForTesting(
            scenario,
            CreateAgent(entityId: 1, factionId: 0, startXRaw, startYRaw, scenario),
            CreateAgent(
                entityId: 2,
                factionId: 1,
                targetXWorld * Scale,
                targetYWorld * Scale,
                scenario));

        simulation.AdvanceOneTick();

        var mover = simulation.Agents.Single(agent => agent.EntityId == 1);
        Assert.Equal(expectedXRaw, mover.XRaw);
        Assert.Equal(expectedYRaw, mover.YRaw);
        Assert.All(
            simulation.Agents,
            agent =>
            {
                Assert.InRange(agent.XRaw, BodyRadiusRaw, MaximumCentreRaw);
                Assert.InRange(agent.YRaw, BodyRadiusRaw, MaximumCentreRaw);
            });
    }

    /// <summary>
    /// Acceptance row <c>Corner</c>, continuous half: across a whole battle no
    /// agent's centre ever leaves the legal band on either axis.
    /// </summary>
    [Fact]
    public void BoundaryAndCorner_NoCentreLeavesTheLegalBandDuringAWholeBattle()
    {
        var scenario = Scenario.CreateDefault(seed: 4, totalAgents: 60);
        var simulation = BattleSimulation.Create(scenario);
        var maximumXRaw =
            checked((scenario.MapWidth * Scale) - scenario.BodyRadiusRaw);
        var maximumYRaw =
            checked((scenario.MapHeight * Scale) - scenario.BodyRadiusRaw);

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();

            foreach (var agent in simulation.Agents)
            {
                if (agent.XRaw < scenario.BodyRadiusRaw ||
                    agent.XRaw > maximumXRaw ||
                    agent.YRaw < scenario.BodyRadiusRaw ||
                    agent.YRaw > maximumYRaw)
                {
                    Assert.Fail(
                        $"Entity {agent.EntityId} finished tick {simulation.Tick} " +
                        $"at ({agent.XRaw}, {agent.YRaw}), outside the legal band " +
                        $"[{scenario.BodyRadiusRaw}, {maximumXRaw}] by " +
                        $"[{scenario.BodyRadiusRaw}, {maximumYRaw}].");
                }
            }
        }

        Assert.NotEqual(BattleOutcome.Ongoing, simulation.Outcome);
    }

    /// <summary>
    /// Acceptance row <c>Spawn</c> / decision record section 10:
    /// <see cref="BattleSimulation.Create"/> resolves spawn overlaps
    /// deterministically, so the very first tick starts from a collision-free
    /// field even when the spawn bands are crowded.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    [InlineData(6UL)]
    public void Spawn_ACrowdedFieldIsCreatedWithNoOverlappingBodies(ulong seed)
    {
        // Thirty bodies per faction on a two-hundred-by-one-hundred-and-twenty
        // map spawn into a band roughly thirty-two bodies wide, so relocation runs
        // for most of the roster rather than for the occasional unlucky pair.
        var scenario = new Scenario(
            seed,
            MapWidth: 200,
            MapHeight: 120,
            AgentsPerFaction: 30,
            TickRate: 20,
            TickLimit: 1_000);

        var simulation = BattleSimulation.Create(scenario);

        Assert.Equal(0, simulation.Tick);
        FailOnLivingOverlap(simulation, scenario.BodyRadiusRaw, seed);
        Assert.All(
            simulation.Agents,
            agent =>
            {
                Assert.InRange(
                    agent.XRaw,
                    scenario.BodyRadiusRaw,
                    (scenario.MapWidth * Scale) - scenario.BodyRadiusRaw);
                Assert.InRange(
                    agent.YRaw,
                    scenario.BodyRadiusRaw,
                    (scenario.MapHeight * Scale) - scenario.BodyRadiusRaw);
            });
    }

    /// <summary>
    /// Acceptance row <c>Spawn</c> for the canonical scenario the game actually
    /// ships with.
    /// </summary>
    [Fact]
    public void Spawn_TheCanonicalTwoHundredAgentFieldIsCreatedWithNoOverlappingBodies()
    {
        for (ulong seed = 1; seed <= 5; seed++)
        {
            var scenario = Scenario.CreateDefault(seed, totalAgents: 200);
            var simulation = BattleSimulation.Create(scenario);

            FailOnLivingOverlap(simulation, scenario.BodyRadiusRaw, seed);
        }
    }

    /// <summary>
    /// Acceptance row <c>Spectator clarity</c> / decision record section 8: the
    /// authoritative per-agent resolved-movement reason reaches the spectator
    /// through <see cref="AgentView"/>, and an agent that actually changed
    /// position never explains itself with <see cref="MovementResolution.None"/>.
    /// </summary>
    [Fact]
    public void SpectatorClarity_MovementResolutionReachesAgentViewAndExplainsEveryMove()
    {
        var scenario = Scenario.CreateDefault(seed: 1, totalAgents: 60);
        var simulation = BattleSimulation.Create(scenario);
        var previous = SnapshotPositions(simulation);
        var alive = simulation.Agents.ToDictionary(
            agent => agent.EntityId,
            agent => agent.IsAlive);
        var observed = new HashSet<MovementResolution>();

        while (simulation.Outcome == BattleOutcome.Ongoing)
        {
            simulation.AdvanceOneTick();

            foreach (var agent in simulation.Agents)
            {
                var start = previous[agent.EntityId];
                var wasAlive = alive[agent.EntityId];
                var moved = agent.XRaw != start.XRaw || agent.YRaw != start.YRaw;
                previous[agent.EntityId] = (agent.XRaw, agent.YRaw);
                alive[agent.EntityId] = agent.IsAlive;
                observed.Add(agent.MovementResolution);

                if (moved && agent.MovementResolution == MovementResolution.None)
                {
                    Assert.Fail(
                        $"Entity {agent.EntityId} moved from " +
                        $"({start.XRaw}, {start.YRaw}) to " +
                        $"({agent.XRaw}, {agent.YRaw}) on tick {simulation.Tick} " +
                        "but reported no movement resolution, so a spectator " +
                        "cannot discover why it moved.");
                }

                if (wasAlive)
                {
                    continue;
                }

                if (moved || agent.MovementResolution != MovementResolution.None)
                {
                    Assert.Fail(
                        $"Corpse {agent.EntityId} changed position or reported " +
                        $"{agent.MovementResolution} on tick {simulation.Tick}. A " +
                        "corpse neither moves nor resolves movement.");
                }
            }
        }

        Assert.Contains(MovementResolution.None, observed);
        Assert.Contains(MovementResolution.Moved, observed);
        Assert.Contains(MovementResolution.Truncated, observed);
        Assert.Contains(MovementResolution.Slid, observed);
        Assert.Contains(MovementResolution.Blocked, observed);
    }

    /// <summary>
    /// Fails the current test when any two living agents strictly overlap. Every
    /// ordered pair is tested by brute force, in checked <see cref="long"/>
    /// arithmetic, without consulting the uniform grid.
    /// </summary>
    private static void FailOnLivingOverlap(
        BattleSimulation simulation,
        int bodyRadiusRaw,
        ulong seed)
    {
        var agents = simulation.Agents;
        var diameterRaw = checked(2L * bodyRadiusRaw);
        var contactSquared = checked(diameterRaw * diameterRaw);

        for (var left = 0; left < agents.Count; left++)
        {
            if (!agents[left].IsAlive)
            {
                continue;
            }

            for (var right = left + 1; right < agents.Count; right++)
            {
                if (!agents[right].IsAlive)
                {
                    continue;
                }

                var squared = SquaredDistance(
                    agents[left].XRaw,
                    agents[left].YRaw,
                    agents[right].XRaw,
                    agents[right].YRaw);

                if (squared < contactSquared)
                {
                    Assert.Fail(
                        $"Seed {seed} tick {simulation.Tick}: living entities " +
                        $"{agents[left].EntityId} at ({agents[left].XRaw}, " +
                        $"{agents[left].YRaw}) and {agents[right].EntityId} at " +
                        $"({agents[right].XRaw}, {agents[right].YRaw}) are " +
                        $"{squared} squared raw units apart, inside the " +
                        $"{contactSquared} required for solid bodies.");
                }
            }
        }
    }

    private static int CountOpposingContacts(
        BattleSimulation simulation,
        int bodyRadiusRaw)
    {
        var agents = simulation.Agents;
        var diameterRaw = checked(2L * bodyRadiusRaw);
        var contactSquared = checked(diameterRaw * diameterRaw);
        var contacts = 0;

        for (var left = 0; left < agents.Count; left++)
        {
            for (var right = left + 1; right < agents.Count; right++)
            {
                if (!agents[left].IsAlive ||
                    !agents[right].IsAlive ||
                    agents[left].FactionId == agents[right].FactionId)
                {
                    continue;
                }

                var squared = SquaredDistance(
                    agents[left].XRaw,
                    agents[left].YRaw,
                    agents[right].XRaw,
                    agents[right].YRaw);

                if (squared <= contactSquared)
                {
                    contacts++;
                }
            }
        }

        return contacts;
    }

    private static Dictionary<ulong, (int XRaw, int YRaw)> SnapshotPositions(
        BattleSimulation simulation) =>
        simulation.Agents.ToDictionary(
            agent => agent.EntityId,
            agent => (agent.XRaw, agent.YRaw));

    private static long SquaredDistance(int aXRaw, int aYRaw, int bXRaw, int bYRaw)
    {
        var deltaX = (long)bXRaw - aXRaw;
        var deltaY = (long)bYRaw - aYRaw;
        return checked((deltaX * deltaX) + (deltaY * deltaY));
    }

    /// <summary>
    /// The integer square root of a non-negative value. Deliberately integer:
    /// floating point is banned anywhere that can reach a simulation assertion.
    /// </summary>
    private static long IntegerSquareRoot(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var remainder = value;
        var root = 0L;
        var bit = 1L << 62;

        while (bit > remainder)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (remainder >= root + bit)
            {
                remainder -= root + bit;
                root = (root >> 1) + bit;
            }
            else
            {
                root >>= 1;
            }

            bit >>= 2;
        }

        return root;
    }

    private static Scenario LineScenario(int agentsPerFaction) =>
        new(
            Seed: 1,
            MapWidth: 200,
            MapHeight: 200,
            agentsPerFaction,
            TickRate: 20,
            TickLimit: 1_000);

    /// <summary>
    /// Two vertical lines of bodies, one per faction, spaced
    /// <see cref="RowSpacingWorld"/> world units apart down each line so that
    /// neighbours sit just clear of one another. The two X positions are given in
    /// raw fixed-point units rather than world units, so a caller that wants the
    /// lines in exact tangent contact can pass a separation of
    /// <c>2 * scenario.BodyRadiusRaw</c> and get it at any body radius. Entity IDs
    /// ascend down the left line and then down the right one.
    /// </summary>
    private static AgentState[] BuildOpposingLines(
        Scenario scenario,
        int leftXRaw,
        int rightXRaw,
        int rows)
    {
        var agents = new AgentState[rows * 2];

        for (var row = 0; row < rows; row++)
        {
            var yRaw = checked((20 + (row * RowSpacingWorld)) * Scale);
            agents[row] = CreateAgent(
                checked((ulong)row + 1),
                factionId: 0,
                leftXRaw,
                yRaw,
                scenario);
            agents[rows + row] = CreateAgent(
                checked((ulong)(rows + row) + 1),
                factionId: 1,
                rightXRaw,
                yRaw,
                scenario);
        }

        return agents;
    }

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
