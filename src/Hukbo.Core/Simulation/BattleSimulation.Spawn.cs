using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;

namespace Hukbo.Core.Simulation;

public sealed partial class BattleSimulation
{
    public static BattleSimulation Create(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        return Create(scenario, CombatPresetRegistry.Get(scenario.CombatPreset));
    }

    /// <summary>
    /// Builds a full spawn-placed battle on a caller-supplied ruleset, so a
    /// test can run the canonical workload against a tuning variant of the
    /// scenario's own preset.
    /// </summary>
    /// <remarks>
    /// The other testing factory takes explicit agents and never runs spawn
    /// placement, and no agents can be lifted out of a simulation built here:
    /// <see cref="Agents"/> and <see cref="CreateSnapshot"/> both return
    /// <see cref="AgentView"/> and the underlying states are private. A
    /// 200-agent seeded battle is therefore only reachable through this
    /// overload.
    /// </remarks>
    /// <param name="scenario">The scenario to build.</param>
    /// <param name="rules">The ruleset the simulation runs on.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="rules"/> declares a different roster from the registered
    /// entry for the scenario's combat preset. Scenario validation checks
    /// roster counts against the <em>registry</em>, so a differing roster would
    /// leave the scenario validated against one roster while the simulation ran
    /// on another.
    /// </exception>
    internal static BattleSimulation Create(Scenario scenario, CombatRuleset rules)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(rules);
        scenario.Validate();
        AssertRosterMatchesRegisteredPreset(scenario, rules);

        // Resolved here, ahead of the constructor's own
        // MovementPresetRegistry.Get(scenario.MovementPreset) call at line
        // 237, purely so CreateAgent below can scale each agent's raw
        // movement speed by its shield's encumbrance at spawn: the
        // constructor does not run until every AgentState already exists.
        // Both lookups resolve the same registry entry for the same
        // scenario, so this costs one extra dictionary-free switch, not a
        // second source of truth.
        var movementRules = MovementPresetRegistry.Get(scenario.MovementPreset);

        var random = new SplitMix64(scenario.Seed);
        var agents = new AgentState[scenario.TotalAgents];
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);

        var rosterCountsAreEmpty = scenario.RosterCounts.IsDefaultOrEmpty;
        var expandedRosterIndices = rosterCountsAreEmpty
            ? ImmutableArray<int>.Empty
            : RosterCountExpansion.Expand(scenario.RosterCounts);

        // One resolution rule serves the spawn loops and the V6 deployment
        // assignment below, so the assignment can never rank a warrior by
        // one loadout and spawn it with another.
        CombatLoadout ResolveSpawnLoadout(ulong entityId, int localIndex) =>
            rosterCountsAreEmpty
                ? rules.ResolveLoadout(entityId)
                : rules.Roster[expandedRosterIndices[localIndex]];

        // Faction 0's own fielded Datu count, resolved before deployment is
        // planned so FormationPlanner can size
        // MovementPresetId.ContingentShapeV12's contingents off it without
        // ever learning what a Datu is (tasks 6 and 7 of
        // the contingent shape task plan). One deployment is
        // planned once and mirrored onto both factions below, so faction 0's
        // chief count is the one value the planner needs; every preset
        // before V12 ignores it entirely.
        var fieldedChiefCount = 0;
        for (var index = 0; index < scenario.AgentsPerFaction; index++)
        {
            if (ResolveSpawnLoadout(checked((ulong)index + 1), index).Rank == RankId.Datu)
            {
                fieldedChiefCount++;
            }
        }

        // One deployment is planned and mirrored across the vertical centre
        // line, so the two armies open in exactly the same shape. Both are
        // drawn from the same roster, so any positional difference at tick 0
        // would be seed noise that the battle then amplifies.
        var deployment = FormationPlanner.PlanFactionDeployment(
            scenario,
            fieldedChiefCount,
            ref random);

        // V6 alone reassigns warriors across the slots already planned
        // above (weapon-relative movement design, section 12). The
        // permutation is a pure function of the deployment and the resolved
        // loadouts — `random` is not consulted, so the SplitMix64 stream is
        // exactly what the planner left it — and it ranks the canonical,
        // unmirrored slots before the faction-1 mirror below, each faction
        // pairing its own loadouts against that same canonical ranking, so
        // equal faction-local loadout multisets keep the exact mirror.
        var faction0Deployment = deployment;
        var faction1Deployment = deployment;
        var movement = MovementPresetRegistry.Get(scenario.MovementPreset);
        if (movement.UsesEquipmentRelativeFootwork)
        {
            var faction0Loadouts = new CombatLoadout[scenario.AgentsPerFaction];
            var faction1Loadouts = new CombatLoadout[scenario.AgentsPerFaction];
            for (var index = 0; index < scenario.AgentsPerFaction; index++)
            {
                faction0Loadouts[index] = ResolveSpawnLoadout(
                    checked((ulong)index + 1), index);
                faction1Loadouts[index] = ResolveSpawnLoadout(
                    checked((ulong)(scenario.AgentsPerFaction + index) + 1),
                    index);
            }

            faction0Deployment = EquipmentDeploymentAssignment.AssignForFaction(
                deployment, faction0Loadouts, movement);
            faction1Deployment = EquipmentDeploymentAssignment.AssignForFaction(
                deployment, faction1Loadouts, movement);
        }
        else if (UsesBattlefieldRealism(scenario.MovementPreset))
        {
            // Gated on preset identity, not on a movement-ruleset field:
            // V10's registered ruleset is a verbatim copy of V8's, so
            // UsesEquipmentRelativeFootwork is false for it and the V6
            // branch above never fires for V10. This mirrors the V8 gate at
            // the retreat rung further down (Scenario.MovementPreset ==
            // MovementPresetId.RangedStandoffV8). CohortDeploymentAssignment
            // never draws — random is not consulted here either — so the
            // SplitMix64 stream is exactly what the planner left it, and it
            // runs on the canonical, unmirrored deployment before the
            // faction-1 mirror below, each faction pairing its own
            // loadouts against that same canonical ranking (battlefield
            // realism design, sections 4.2 to 4.6).
            var faction0Loadouts = new CombatLoadout[scenario.AgentsPerFaction];
            var faction1Loadouts = new CombatLoadout[scenario.AgentsPerFaction];
            for (var index = 0; index < scenario.AgentsPerFaction; index++)
            {
                faction0Loadouts[index] = ResolveSpawnLoadout(
                    checked((ulong)index + 1), index);
                faction1Loadouts[index] = ResolveSpawnLoadout(
                    checked((ulong)(scenario.AgentsPerFaction + index) + 1),
                    index);
            }

            // Not covered by UsesBattlefieldRealism or by
            // YieldsLastStandEngagement: this is its own single-value identity
            // test, and it is the one a reader of those two predicates misses.
            // A preset omitted here deploys its armies in a different shape
            // from V13's, which silently invalidates every comparison between
            // the two.
            // MovementPresetId.ContingentCohesionBeforeContactV15 is admitted
            // here for the same reason it is admitted to the
            // battlefield-realism and last-stand gates: it is defined as a
            // strict superset of V13's behaviour, so it inherits the
            // lateral-riffle cohort traversal unchanged before the
            // contingent-cohesion gate of its own is layered on top.
            var spreadCohortsLaterally =
                scenario.MovementPreset is MovementPresetId.CohortLateralSpreadV13
                    or MovementPresetId.EvasiveFootworkV14
                    or MovementPresetId.ContingentCohesionBeforeContactV15
                    or MovementPresetId.SeparatingContactV17;
            faction0Deployment = CohortDeploymentAssignment.AssignForFaction(
                deployment, faction0Loadouts, rules, spreadCohortsLaterally);
            faction1Deployment = CohortDeploymentAssignment.AssignForFaction(
                deployment, faction1Loadouts, rules, spreadCohortsLaterally);
        }

        for (var index = 0; index < scenario.AgentsPerFaction; index++)
        {
            var entityId = checked((ulong)index + 1);
            var loadout = ResolveSpawnLoadout(entityId, index);
            var (xRaw, yRaw, contingentId) = faction0Deployment[index];
            agents[index] = CreateAgent(
                entityId,
                factionId: 0,
                xRaw,
                yRaw,
                scenario,
                rules,
                movementRules,
                loadout,
                contingentId);
        }

        for (var index = 0; index < scenario.AgentsPerFaction; index++)
        {
            var (leftXRaw, leftYRaw, contingentId) = faction1Deployment[index];
            var rightX = checked(mapWidthRaw - leftXRaw);
            var rightY = leftYRaw;
            var stateIndex = scenario.AgentsPerFaction + index;
            var entityId = checked((ulong)stateIndex + 1);
            // Faction-local index, not entityId/stateIndex: RosterCounts
            // describes one faction, and reusing the global index would
            // continue faction 1's category offset from wherever faction 0
            // stopped, silently giving the two factions different armies.
            // The same reasoning applies to contingentId, mirrored from the
            // one deployment plan alongside the position: it is the
            // faction-local dealing index, not a value tied to faction 0.
            var loadout = ResolveSpawnLoadout(entityId, index);
            agents[stateIndex] = CreateAgent(
                entityId,
                factionId: 1,
                rightX,
                rightY,
                scenario,
                rules,
                movementRules,
                loadout,
                contingentId);
        }

        ResolveSpawnPlacement(agents, scenario);

        return new BattleSimulation(scenario, agents, rules);
    }

    internal static BattleSimulation CreateForTesting(
        Scenario scenario,
        params AgentState[] agents)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        return CreateForTesting(
            scenario,
            CombatPresetRegistry.Get(scenario.CombatPreset),
            agents);
    }

    /// <summary>
    /// Builds a battle from explicit agents on a caller-supplied ruleset. This
    /// is the only sanctioned way to give a test a clash-neutral
    /// configuration: no shipped loadout pairing is clash-neutral, and
    /// hand-picking seeds and entity identifiers whose roll happens to land
    /// would be silently invalidated by any later tuning or mixer change.
    /// </summary>
    /// <param name="scenario">The scenario to build.</param>
    /// <param name="rules">The ruleset the simulation runs on.</param>
    /// <param name="agents">The agents to place, in any order.</param>
    /// <exception cref="ArgumentException">
    /// No agent was supplied, or <paramref name="rules"/> declares a different
    /// roster from the registered entry for the scenario's combat preset.
    /// </exception>
    internal static BattleSimulation CreateForTesting(
        Scenario scenario,
        CombatRuleset rules,
        params AgentState[] agents)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(agents);
        scenario.Validate();
        AssertRosterMatchesRegisteredPreset(scenario, rules);

        if (agents.Length == 0)
        {
            throw new ArgumentException(
                "At least one agent is required.",
                nameof(agents));
        }

        var orderedAgents = agents.OrderBy(agent => agent.EntityId).ToArray();
        return new BattleSimulation(scenario, orderedAgents, rules);
    }

    /// <summary>
    /// Rejects an injected ruleset whose roster disagrees with the registered
    /// entry for the scenario's combat preset.
    /// </summary>
    /// <remarks>
    /// <see cref="Scenario.Validate"/> checks roster counts against the
    /// registry and is deliberately left alone, so injecting a differently
    /// rostered ruleset would validate the scenario against one roster and run
    /// it on another. The sanctioned use is a tuning variant of the same
    /// preset, where the roster is identical.
    /// </remarks>
    private static void AssertRosterMatchesRegisteredPreset(
        Scenario scenario,
        CombatRuleset rules)
    {
        var registered = CombatPresetRegistry.Get(scenario.CombatPreset);
        if (rules.Roster.SequenceEqual(registered.Roster))
        {
            return;
        }

        throw new ArgumentException(
            "The supplied ruleset declares a different roster from the " +
            $"registered entry for combat preset {scenario.CombatPreset}. " +
            "Scenario validation checks roster counts against the registry, " +
            "so the scenario and the simulation would disagree.",
            nameof(rules));
    }

    /// <summary>
    /// Makes the initial placement collision-free before the first tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a safety net rather than the primary repair.
    /// <see cref="FormationPlanner"/> already keeps every planned pair at least
    /// one raw unit clear of tangency, and the two factions occupy opposite
    /// halves of the map, so this pass normally finds nothing to move and the
    /// mirror survives intact. It re-engages only where a crowded map pushes
    /// the planner's fallback lattice past the edge of its own half, and there
    /// an approximate mirror is the correct degradation.
    /// </para>
    /// <para>
    /// Overlaps are repaired deterministically: agents are placed in ascending
    /// entity ID, and an agent that lands on an occupied spot is relocated by
    /// scanning rings of the eight compass offsets at increasing radius in one
    /// fixed order. The random stream is never consulted during relocation, so
    /// repairing a spawn cannot shift the seed sequence for anything that
    /// follows.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// An agent could not be placed. Scenario validation rejects impossible
    /// densities up front, so reaching this means the ring scan was exhausted
    /// and the placement is genuinely infeasible rather than merely crowded.
    /// </exception>
    private static void ResolveSpawnPlacement(
        AgentState[] agents,
        Scenario scenario)
    {
        var bodyRadiusRaw = scenario.BodyRadiusRaw;
        var stepRaw = checked(2 * bodyRadiusRaw);
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(scenario.MapHeight * FixedPoint.Scale);
        var maximumRing = checked(
            (Math.Max(mapWidthRaw, mapHeightRaw) / stepRaw) + 1);
        var grid = new CollisionUniformGrid(stepRaw);

        foreach (var agent in agents)
        {
            agent.XRaw = CollisionGeometry.ClampCenterToBounds(
                agent.XRaw,
                mapWidthRaw,
                bodyRadiusRaw);
            agent.YRaw = CollisionGeometry.ClampCenterToBounds(
                agent.YRaw,
                mapHeightRaw,
                bodyRadiusRaw);

            var isOccupied = grid.AnyContact(
                agent.XRaw,
                agent.YRaw,
                bodyRadiusRaw,
                agent.EntityId);
            if (isOccupied &&
                !TryRelocateSpawn(
                    grid,
                    agent,
                    bodyRadiusRaw,
                    stepRaw,
                    mapWidthRaw,
                    mapHeightRaw,
                    maximumRing))
            {
                throw new InvalidOperationException(
                    $"Entity {agent.EntityId} could not be placed without " +
                    "overlapping another body. Reduce the agent count or " +
                    "enlarge the map.");
            }

            grid.Insert(
                new CollisionBody(
                    agent.EntityId,
                    agent.XRaw,
                    agent.YRaw,
                    agent.IsAlive));
        }
    }

    private static bool TryRelocateSpawn(
        CollisionUniformGrid grid,
        AgentState agent,
        int bodyRadiusRaw,
        int stepRaw,
        int mapWidthRaw,
        int mapHeightRaw,
        int maximumRing)
    {
        ReadOnlySpan<(int X, int Y)> offsets =
        [
            (1, 0), (1, 1), (0, 1), (-1, 1),
            (-1, 0), (-1, -1), (0, -1), (1, -1),
        ];
        var originX = agent.XRaw;
        var originY = agent.YRaw;

        for (var ring = 1; ring <= maximumRing; ring++)
        {
            var spanRaw = checked((long)ring * stepRaw);

            foreach (var offset in offsets)
            {
                var candidateX = originX + (offset.X * spanRaw);
                var candidateY = originY + (offset.Y * spanRaw);

                if (candidateX < bodyRadiusRaw ||
                    candidateX > mapWidthRaw - bodyRadiusRaw ||
                    candidateY < bodyRadiusRaw ||
                    candidateY > mapHeightRaw - bodyRadiusRaw)
                {
                    continue;
                }

                var nextX = checked((int)candidateX);
                var nextY = checked((int)candidateY);
                if (grid.AnyContact(nextX, nextY, bodyRadiusRaw, agent.EntityId))
                {
                    continue;
                }

                agent.XRaw = nextX;
                agent.YRaw = nextY;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves this warrior's damage, reach, and cooldown once, at spawn,
    /// from the weapon and shield they carry, and writes them into the
    /// per-agent fields that already exist and are already hashed.
    /// </summary>
    /// <remarks>
    /// This is the only place a weapon profile is read. Every tick stage goes
    /// on reading exactly the <see cref="AgentState"/> fields it read before,
    /// including the single approved reach test in
    /// <c>IsWithinAttackRange</c>, so intent selection and attack gathering
    /// still cannot disagree and no second reach path exists.
    /// <para>
    /// A preset that declares no profiles — version 1 — falls back to the
    /// scenario's global values, which is what its replays were recorded
    /// against.
    /// </para>
    /// </remarks>
    private static AgentState CreateAgent(
        ulong entityId,
        int factionId,
        int xRaw,
        int yRaw,
        Scenario scenario,
        CombatRuleset rules,
        MovementRuleset movementRules,
        CombatLoadout loadout,
        int contingentId)
    {
        var profile = rules.HasWeaponProfiles
            ? rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield)
            : new WeaponProfile(
                scenario.DamagePerAttack,
                scenario.AttackRangeRaw,
                scenario.AttackCooldownTicks);

        var level = rules.HasRankLevels
            ? rules.ResolveLevel(loadout.Rank)
            : scenario.PlaceholderFighterLevel;

        // Shield-projectile-block design section 6.1: scaled once, here, at
        // spawn, directly on the agent's raw movement speed — never through
        // a loadout movement profile row, because the shipped movement
        // pipeline registers no row for a ranged loadout and an
        // equipment-relative preset throws for one. Integer arithmetic in a
        // long intermediate so the multiply cannot overflow regardless of
        // scenario tuning, then narrowed back with checked, matching every
        // other basis-point scale in this file. Every preset whose
        // MovementRuleset.AppliesShieldEncumbrance is false resolves
        // MovementRuleset.FullShieldPaceBasisPoints (10,000) for every
        // shield, so movementSpeedRaw is scenario.MovementSpeedRaw times
        // 10,000 divided by 10,000: byte-identical to today's value for
        // every preset up to and including
        // MovementPresetId.CohortLateralSpreadV13.
        var shieldPaceBasisPoints =
            movementRules.ResolveShieldPaceBasisPoints(loadout.Shield);
        var movementSpeedRaw = checked((int)(
            (long)scenario.MovementSpeedRaw * shieldPaceBasisPoints /
            MovementRuleset.FullShieldPaceBasisPoints));

        return new AgentState(
            entityId,
            factionId,
            xRaw,
            yRaw,
            scenario.MaximumHitPoints,
            // Armor reaches movement here, once per warrior at spawn, rather
            // than through a LoadoutMovementProfile row. Those rows are empty
            // for every preset whose UsesEquipmentRelativeFootwork is false,
            // which includes every movement preset from V10 onward and so the
            // one the client ships, meaning a pace modifier hung there would
            // never be read by the running game. This is the same shape the
            // weapon profile above already uses: a per-loadout value resolved
            // once at spawn into an immutable AgentState field.
            //
            // It composes with the shield encumbrance computed just above
            // rather than replacing it: a warrior carrying both a heavy shield
            // and a rigid cuirass is slowed by both, in that order. The order
            // is fixed rather than incidental, because integer division makes
            // the two scalings non-commutative at the margin, and the ordering
            // reaches the state hash.
            //
            // Armor scaling is unconditional and is still a no-op for every
            // combat preset up to and including version 7, because all of them
            // field ArmorId.LightOrganic on every roster row and light armor
            // carries a flat 10,000 basis points, which returns the input
            // unchanged. Their agents spawn with a byte-identical movement
            // speed, so their recorded replays keep reproducing.
            ArmorWeightClasses.ScaleMovementSpeed(
                movementSpeedRaw,
                loadout.Armor),
            scenario.PerceptionRangeRaw,
            profile.AttackRangeRaw,
            profile.DamagePerAttack,
            profile.AttackCooldownTicks,
            loadout,
            level,
            contingentId);
    }
}
