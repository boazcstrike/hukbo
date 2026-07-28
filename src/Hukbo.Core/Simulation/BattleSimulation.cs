using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Hukbo.Core.Combat;
using Hukbo.Core.Determinism;
using Hukbo.Core.Mathematics;

namespace Hukbo.Core.Simulation;

/// <summary>
/// Authoritative, deterministic, fixed-tick battle state.
/// </summary>
public sealed class BattleSimulation
{
    private static readonly ReadOnlyCollection<BattleEvent> EmptyEvents =
        Array.AsReadOnly<BattleEvent>([]);

    /// <summary>
    /// The number of factions a battle has. Two, everywhere: faction 0 and
    /// faction 1. Named so the per-faction observability buffers in
    /// <see cref="GatherAndCommitAttacks"/> do not carry a bare literal.
    /// </summary>
    private const int FactionCount = 2;

    private readonly CombatRuleset _rules;
    private readonly AgentState[] _agentStates;
    private readonly Dictionary<ulong, int> _agentIndexes;
    private readonly int[] _damageTotals;
    private readonly (int XRaw, int YRaw, ulong TargetId)?[] _movementProposals;
    private readonly (int SourceIndex, int TargetIndex, BodyPart HitLocation,
        AttackResolution Resolution, int? ComboPosition)[] _attackProposals;
    private readonly AgentView[] _agentViews;
    private readonly ReadOnlyCollection<AgentView> _agents;
    private readonly CollisionScratch _collision;

    // Per-faction last-stand state, recomputed by one forward scan at the top
    // of every SelectTargetsAndIntents call. Allocated once here so the scan
    // never allocates per tick. Index 0 is faction 0, index 1 is faction 1.
    // A rally entity ID of 0 means the faction has no living agent this tick;
    // 0 is never a valid EntityId (AgentState rejects it), so it is a safe
    // sentinel.
    private readonly int[] _factionLivingCounts;
    private readonly ulong[] _factionRallyEntityIds;

    // Double-buffered event storage: each tick writes into whichever of these
    // two lists is not currently exposed through _lastEvents, so a caller
    // that retains one tick's LastEvents value keeps seeing that tick's data,
    // untouched, until this same buffer comes back around to be reused. The
    // matching ReadOnlyCollection wrapper for each buffer is built once, here,
    // never per tick, since List<T>.AsReadOnly would otherwise allocate a new
    // wrapper every tick even though the pair of backing lists never changes.
    private readonly List<BattleEvent> _eventBufferA;
    private readonly List<BattleEvent> _eventBufferB;
    private readonly ReadOnlyCollection<BattleEvent> _eventViewA;
    private readonly ReadOnlyCollection<BattleEvent> _eventViewB;
    private bool _nextEventBufferIsA = true;
    private ReadOnlyCollection<BattleEvent> _lastEvents;
    private long _eventSequence;
    private CollisionTickMetrics _lastTickCollision;
    private CombatMetrics _lastTickCombat;
    private FactionCombatMetrics _lastTickCombatByFaction;

    private BattleSimulation(
        Scenario scenario,
        AgentState[] agents,
        CombatRuleset rules)
    {
        Scenario = scenario;
        _rules = rules;
        _agentStates = agents;
        _agentIndexes = new Dictionary<ulong, int>(agents.Length);
        _damageTotals = new int[agents.Length];
        _movementProposals =
            new (int XRaw, int YRaw, ulong TargetId)?[agents.Length];
        _attackProposals =
            new (int SourceIndex, int TargetIndex, BodyPart HitLocation,
                AttackResolution Resolution, int? ComboPosition)[agents.Length];
        _agentViews = new AgentView[agents.Length];
        _agents = Array.AsReadOnly(_agentViews);
        _collision = new CollisionScratch(scenario, agents.Length);
        _factionLivingCounts = new int[2];
        _factionRallyEntityIds = new ulong[2];
        _eventBufferA = new List<BattleEvent>(agents.Length * 2);
        _eventBufferB = new List<BattleEvent>(agents.Length * 2);
        _eventViewA = new ReadOnlyCollection<BattleEvent>(_eventBufferA);
        _eventViewB = new ReadOnlyCollection<BattleEvent>(_eventBufferB);

        for (var index = 0; index < agents.Length; index++)
        {
            if (!_agentIndexes.TryAdd(agents[index].EntityId, index))
            {
                throw new ArgumentException(
                    $"Duplicate entity ID {agents[index].EntityId}.",
                    nameof(agents));
            }
        }

        UpdateViews();
        _lastEvents = EmptyEvents;
    }

    public Scenario Scenario { get; }

    public long Tick { get; private set; }

    public BattleOutcome Outcome { get; private set; }

    public IReadOnlyList<AgentView> Agents => _agents;

    /// <summary>
    /// Every event emitted by the tick just completed, in emission order.
    /// </summary>
    /// <remarks>
    /// The returned collection is owned by the simulation and is overwritten
    /// by a future <see cref="AdvanceOneTick"/> call once its backing buffer
    /// comes back around for reuse. Callers read it within the tick that
    /// produced it and never retain it.
    /// </remarks>
    public IReadOnlyList<BattleEvent> LastEvents => _lastEvents;

    /// <summary>
    /// Derived collision counters for the tick just completed. Observability
    /// only: never hashed, never snapshotted, never persisted.
    /// </summary>
    internal CollisionTickMetrics LastTickCollision => _lastTickCollision;

    /// <summary>
    /// Derived attack-resolution counters for the tick just completed.
    /// Observability only: never hashed, never snapshotted, never persisted.
    /// </summary>
    /// <remarks>
    /// Public so a presentation layer can report authoritative counts rather
    /// than re-deriving them from the event stream. Reading this cannot affect
    /// the simulation: it is assigned once at the end of
    /// <see cref="GatherAndCommitAttacks"/> and never read back by any
    /// simulation stage.
    /// </remarks>
    public CombatMetrics LastTickCombat => _lastTickCombat;

    /// <summary>
    /// The same counters as <see cref="LastTickCombat"/>, split by the faction
    /// of the attacker. <see cref="FactionCombatMetrics.Total"/> equals
    /// <see cref="LastTickCombat"/> by construction — the undivided value is
    /// computed from this one, not counted separately.
    /// </summary>
    /// <remarks>
    /// This is one tick. A caller wanting battle totals sums it across ticks;
    /// the simulation deliberately holds no running total, because that would
    /// be mutable run-scoped state existing only for observability. See
    /// <see cref="FactionCombatMetrics"/>.
    /// </remarks>
    public FactionCombatMetrics LastTickCombatByFaction =>
        _lastTickCombatByFaction;

    /// <summary>
    /// Longest run of consecutive blocked ticks any single agent has reached.
    /// </summary>
    internal int LongestBlockedStreakTicks => _collision.LongestBlockedStreakTicks;

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

        var random = new SplitMix64(scenario.Seed);
        var agents = new AgentState[scenario.TotalAgents];
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
        // One deployment is planned and mirrored across the vertical centre
        // line, so the two armies open in exactly the same shape. Both are
        // drawn from the same roster, so any positional difference at tick 0
        // would be seed noise that the battle then amplifies.
        var deployment = FormationPlanner.PlanFactionDeployment(
            scenario,
            ref random);
        var rosterCountsAreEmpty = scenario.RosterCounts.IsDefaultOrEmpty;
        var expandedRosterIndices = rosterCountsAreEmpty
            ? ImmutableArray<int>.Empty
            : RosterCountExpansion.Expand(scenario.RosterCounts);

        for (var index = 0; index < scenario.AgentsPerFaction; index++)
        {
            var entityId = checked((ulong)index + 1);
            var loadout = rosterCountsAreEmpty
                ? rules.ResolveLoadout(entityId)
                : rules.Roster[expandedRosterIndices[index]];
            agents[index] = CreateAgent(
                entityId,
                factionId: 0,
                deployment[index].XRaw,
                deployment[index].YRaw,
                scenario,
                rules,
                loadout);
        }

        for (var index = 0; index < scenario.AgentsPerFaction; index++)
        {
            var rightX = checked(mapWidthRaw - deployment[index].XRaw);
            var rightY = deployment[index].YRaw;
            var stateIndex = scenario.AgentsPerFaction + index;
            var entityId = checked((ulong)stateIndex + 1);
            // Faction-local index, not entityId/stateIndex: RosterCounts
            // describes one faction, and reusing the global index would
            // continue faction 1's category offset from wherever faction 0
            // stopped, silently giving the two factions different armies.
            var loadout = rosterCountsAreEmpty
                ? rules.ResolveLoadout(entityId)
                : rules.Roster[expandedRosterIndices[index]];
            agents[stateIndex] = CreateAgent(
                entityId,
                factionId: 1,
                rightX,
                rightY,
                scenario,
                rules,
                loadout);
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

    public void AdvanceOneTick()
    {
        if (Outcome != BattleOutcome.Ongoing)
        {
            _lastEvents = EmptyEvents;
            return;
        }

        Tick = checked(Tick + 1);

        // Write into whichever buffer is not currently exposed through
        // _lastEvents, so a caller still holding a prior tick's LastEvents
        // value keeps seeing that tick's data unchanged. See the field
        // comment above _eventBufferA for the full scheme.
        var events = _nextEventBufferIsA ? _eventBufferA : _eventBufferB;
        events.Clear();

        DecrementCooldowns();
        SelectTargetsAndIntents();
        GatherMovementProposals();
        ResolveCollisions();
        CommitMovement(events);
        MeasureCollision();
        GatherAndCommitAttacks(events);
        ResolveOutcome(events);

        UpdateViews();
        if (events.Count == 0)
        {
            _lastEvents = EmptyEvents;
        }
        else
        {
            _lastEvents = _nextEventBufferIsA ? _eventViewA : _eventViewB;
            _nextEventBufferIsA = !_nextEventBufferIsA;
        }
    }

    public ulong ComputeStateHash() => ComputeStateHash(_rules.ContentHash);

    /// <summary>
    /// Computes the state hash folding a caller-supplied ruleset content hash
    /// in place of this simulation's own.
    /// </summary>
    /// <remarks>
    /// The parameterless overload unconditionally folds the running ruleset's
    /// content hash, so there is no way through it to reproduce a hash recorded
    /// before that content hash moved. Reaching
    /// <see cref="StateHasher.Compute"/> directly is not an option either: it
    /// needs the agent states, which are private.
    /// </remarks>
    /// <param name="contentHash">The content hash to fold.</param>
    internal ulong ComputeStateHash(ulong contentHash) =>
        StateHasher.Compute(
            Scenario,
            Tick,
            Outcome,
            _eventSequence,
            _agentStates,
            contentHash);

    public BattleSnapshot CreateSnapshot()
    {
        var agents = Array.AsReadOnly(_agents.ToArray());
        var events = Array.AsReadOnly(_lastEvents.ToArray());
        return new BattleSnapshot(
            Tick,
            Outcome,
            agents,
            events,
            ComputeStateHash());
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
        CombatLoadout loadout)
    {
        var profile = rules.HasWeaponProfiles
            ? rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield)
            : new WeaponProfile(
                scenario.DamagePerAttack,
                scenario.AttackRangeRaw,
                scenario.AttackCooldownTicks);

        return new AgentState(
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
            loadout,
            scenario.PlaceholderFighterLevel);
    }

    /// <summary>
    /// Resolves the <see cref="WeaponProfile"/> that governs one warrior's
    /// attack-combination attributes at the moment of an attack attempt.
    /// Mirrors the fallback <see cref="CreateAgent"/> uses for damage, reach,
    /// and cooldown: a preset that declares no weapon profiles — version 1 —
    /// falls back to a synthetic profile built from the scenario's global
    /// values, whose combo fields default to the record's own no-op
    /// defaults (see <see cref="WeaponProfile"/>), so version 1 rolls the
    /// same combo checks every other preset does and they simply never
    /// succeed.
    /// </summary>
    /// <remarks>
    /// Resolved fresh on every attack attempt rather than cached on
    /// <see cref="AgentState"/> at spawn, unlike damage/reach/cooldown:
    /// those three are read every tick regardless of whether an attack is
    /// attempted, but the combo fields are read only on the attempts that
    /// pass the pre-check gate, which per-tick is a small fraction of the
    /// full roster.
    /// </remarks>
    private WeaponProfile ResolveAttackerWeaponProfile(CombatLoadout loadout) =>
        _rules.HasWeaponProfiles
            ? _rules.ResolveWeaponProfile(loadout.Weapon, loadout.Shield)
            : new WeaponProfile(
                Scenario.DamagePerAttack,
                Scenario.AttackRangeRaw,
                Scenario.AttackCooldownTicks);

    /// <summary>
    /// Clears an attacker's active attack combination. Called by the pre-
    /// check gate in <see cref="GatherAndCommitAttacks"/> per plan section
    /// 3(a): the moment this attacker discovers — on any tick, whether or
    /// not it is itself off cooldown — that it has no living target or that
    /// its target is out of reach, its chain is over. A no-op when no chain
    /// is active, so callers do not need to test
    /// <see cref="AgentState.ComboStepsRemaining"/> first.
    /// </summary>
    private static void ClearActiveComboChain(AgentState attacker)
    {
        if (attacker.ComboStepsRemaining <= 0)
        {
            return;
        }

        attacker.ComboStepsRemaining = 0;
        attacker.ComboTargetEntityId = null;
    }

    private void DecrementCooldowns()
    {
        foreach (var agent in _agentStates)
        {
            if (agent.IsAlive && agent.AttackCooldownRemaining > 0)
            {
                agent.AttackCooldownRemaining--;
            }
        }
    }

    private void SelectTargetsAndIntents()
    {
        ComputeRallyAgents();

        foreach (var agent in _agentStates)
        {
            if (!agent.IsAlive)
            {
                agent.TargetEntityId = null;
                agent.Intent = AgentIntent.Dead;
                continue;
            }

            AgentState? selectedTarget = null;
            var selectedDistance = long.MaxValue;
            var perceptionSquared = checked(
                (long)agent.PerceptionRangeRaw * agent.PerceptionRangeRaw);

            foreach (var candidate in _agentStates)
            {
                if (!candidate.IsAlive || candidate.FactionId == agent.FactionId)
                {
                    continue;
                }

                // Cheap axis-aligned rejection before the squared-distance
                // check below: if |dx| already exceeds the (unsquared)
                // perception range, then dx*dx alone exceeds range*range,
                // so dx*dx + dy*dy > range*range necessarily (dy*dy is
                // never negative). Every candidate rejected here would also
                // have been rejected by the perception test that follows,
                // so the surviving candidate set and its scan order are
                // unchanged. Written as two comparisons instead of an
                // absolute value so no negation of deltaX/deltaY is needed.
                var perceptionRangeRaw = (long)agent.PerceptionRangeRaw;
                var deltaX = (long)candidate.XRaw - agent.XRaw;
                if (deltaX > perceptionRangeRaw || deltaX < -perceptionRangeRaw)
                {
                    continue;
                }

                var deltaY = (long)candidate.YRaw - agent.YRaw;
                if (deltaY > perceptionRangeRaw || deltaY < -perceptionRangeRaw)
                {
                    continue;
                }

                var distance = SquaredDistance(agent, candidate);
                if (distance > perceptionSquared)
                {
                    continue;
                }

                if (distance < selectedDistance ||
                    (distance == selectedDistance &&
                        (selectedTarget is null ||
                            candidate.EntityId < selectedTarget.EntityId)))
                {
                    selectedTarget = candidate;
                    selectedDistance = distance;
                }
            }

            agent.TargetEntityId = selectedTarget?.EntityId;
            if (selectedTarget is null)
            {
                agent.Intent = AgentIntent.Idle;
                continue;
            }

            // An agent keeps advancing until its body meets the target's, even
            // once the target is already inside reach. Attacking is reserved
            // for an agent that has arrived. One that strikes while still
            // closing is re-marked Attacking by attack gathering, so a
            // spectator still sees it fighting.
            agent.Intent = selectedDistance <= CollisionGeometry
                .ContactSquaredDistance(Scenario.BodyRadiusRaw)
                ? AgentIntent.Attacking
                : AgentIntent.Moving;

            // Regrouping only overrides an intent that would otherwise be
            // Moving: Attacking beats Regrouping (the same-tick conflict
            // rule), and the rally agent itself is exempt and keeps its
            // ordinary nearest-enemy intent.
            if (Scenario.LastStandThresholdAgents > 0 &&
                agent.Intent == AgentIntent.Moving &&
                _factionLivingCounts[agent.FactionId] <=
                    Scenario.LastStandThresholdAgents &&
                agent.EntityId != _factionRallyEntityIds[agent.FactionId])
            {
                agent.Intent = AgentIntent.Regrouping;
            }
        }
    }

    /// <summary>
    /// One forward scan over the agent array, computing the living count and
    /// the lowest living <see cref="AgentState.EntityId"/> per faction. The
    /// comparison is against <see cref="AgentState.EntityId"/> explicitly, so
    /// the result does not depend on the incidental order of
    /// <see cref="_agentStates"/>. Runs before any intent is assigned, so no
    /// warrior's intent can depend on scan order either.
    /// </summary>
    private void ComputeRallyAgents()
    {
        _factionLivingCounts[0] = 0;
        _factionLivingCounts[1] = 0;
        _factionRallyEntityIds[0] = 0;
        _factionRallyEntityIds[1] = 0;

        foreach (var candidate in _agentStates)
        {
            if (!candidate.IsAlive)
            {
                continue;
            }

            var faction = candidate.FactionId;
            _factionLivingCounts[faction]++;

            if (_factionRallyEntityIds[faction] == 0 ||
                candidate.EntityId < _factionRallyEntityIds[faction])
            {
                _factionRallyEntityIds[faction] = candidate.EntityId;
            }
        }
    }

    /// <summary>
    /// Reads tick-start state only. Nothing is committed here, so no agent can
    /// see another agent's move while proposals are still being formed.
    /// </summary>
    private void GatherMovementProposals()
    {
        Array.Clear(_movementProposals);

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            if (agent.Intent == AgentIntent.Moving &&
                agent.TargetEntityId is { } enemyTargetId)
            {
                var target = _agentStates[_agentIndexes[enemyTargetId]];
                _movementProposals[index] = BuildMovementProposal(agent, target);
                continue;
            }

            if (agent.Intent == AgentIntent.Regrouping)
            {
                _movementProposals[index] = BuildRegroupingProposal(agent);
            }
        }
    }

    /// <summary>
    /// Builds the movement proposal that closes a regrouping follower on its
    /// faction's rally agent plus that follower's own fixed positional bias
    /// (<see cref="RallyOffset"/>). Returns <c>null</c> — propose no movement
    /// — in two cases: the arrived-guard, when the follower's squared distance
    /// to its aim point is already at or inside
    /// <see cref="CollisionGeometry.ContactSquaredDistance"/>, which stops the
    /// one-raw-unit movement-floor twitch and the resulting <c>Move</c>-event
    /// flood a settled cluster would otherwise emit every tick; and the
    /// defensive case where the rally entity ID is the zero sentinel or the
    /// rally agent cannot be resolved to a living agent, which falls back to
    /// no movement rather than throwing.
    /// </summary>
    /// <remarks>
    /// The follower's aim point trails <see cref="FormationRules.RallyTrailRadiusMultiplier"/>
    /// body radii behind the rally agent, opposite the rally agent's own
    /// direction of travel, before the jitter offset is applied. Without the
    /// trail, a follower whose jitter offset happens to point along the rally
    /// agent's forward arc parks permanently in front of its own leader and
    /// blocks it — the rally agent is exempt from regrouping and never routes
    /// around its own formation, so that block never clears. Two factions
    /// doing this simultaneously deadlock the whole battle at the tick limit.
    /// See the type-level remarks on <see cref="FormationRules"/> for the
    /// clearance derivation.
    /// </remarks>
    /// <remarks>
    /// The trail alone is not enough: a follower can start the tick already
    /// ahead of the rally agent, in which case its trail-behind aim point
    /// sits on the far side of the leader's own body, and reaching it in a
    /// straight line means walking backward through the leader. See
    /// <see cref="TryComputeGiveWayAimPoint"/> for the sideways escape this
    /// method checks first.
    /// </remarks>
    private (int XRaw, int YRaw, ulong TargetId)? BuildRegroupingProposal(
        AgentState agent)
    {
        var rallyEntityId = _factionRallyEntityIds[agent.FactionId];
        if (rallyEntityId == 0 ||
            !_agentIndexes.TryGetValue(rallyEntityId, out var rallyIndex))
        {
            return null;
        }

        var rallyAgent = _agentStates[rallyIndex];
        if (!rallyAgent.IsAlive)
        {
            return null;
        }

        var direction = ComputeRallyDirection(rallyAgent);

        if (direction.DistanceRaw > 0 &&
            TryComputeGiveWayAimPoint(agent, rallyAgent, direction) is
            { XRaw: var giveWayXRaw, YRaw: var giveWayYRaw })
        {
            return BuildMovementProposal(
                agent,
                giveWayXRaw,
                giveWayYRaw,
                rallyAgent.EntityId);
        }

        var (trailBaseXRaw, trailBaseYRaw) = ComputeRallyTrailBase(
            rallyAgent,
            direction);

        var (offsetXRaw, offsetYRaw) = RallyOffset.Compute(
            Scenario.Seed,
            agent.EntityId,
            Scenario.BodyRadiusRaw);

        // The aim point is computed in long and saturated into int the same
        // way CollisionResolver.ToCoordinate saturates a candidate coordinate
        // before its own boundary clamp: safe, because ClampCenterToBounds
        // immediately pulls anything outside the map back to the edge.
        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);
        var aimXRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked((long)trailBaseXRaw + offsetXRaw)),
            mapWidthRaw,
            Scenario.BodyRadiusRaw);
        var aimYRaw = CollisionGeometry.ClampCenterToBounds(
            SaturateToInt32(checked((long)trailBaseYRaw + offsetYRaw)),
            mapHeightRaw,
            Scenario.BodyRadiusRaw);

        var squaredDistanceToAim = CollisionGeometry.SquaredDistance(
            agent.XRaw,
            agent.YRaw,
            aimXRaw,
            aimYRaw);
        if (squaredDistanceToAim <=
            CollisionGeometry.ContactSquaredDistance(Scenario.BodyRadiusRaw))
        {
            return null;
        }

        // The event log names the rally agent as the target, not the enemy
        // the follower would otherwise be chasing, so a spectator reads
        // "entity N moved toward entity <rally>" during a last stand.
        return BuildMovementProposal(
            agent,
            aimXRaw,
            aimYRaw,
            rallyAgent.EntityId);
    }

    /// <summary>
    /// Computes the rally agent's direction of travel — the vector from the
    /// rally agent to its own enemy target, plus the integer distance between
    /// them — used both by <see cref="ComputeRallyTrailBase"/> and by
    /// <see cref="TryComputeGiveWayAimPoint"/> for the corridor test. Returns
    /// a zero <c>DistanceRaw</c> sentinel, meaning "no direction", when the
    /// rally agent has no target, when the target cannot be resolved to a
    /// living agent, or when the rally agent is already exactly at its
    /// target's position.
    /// </summary>
    private (long DeltaXRaw, long DeltaYRaw, long DistanceRaw) ComputeRallyDirection(
        AgentState rallyAgent)
    {
        if (rallyAgent.TargetEntityId is not { } rallyTargetId ||
            !_agentIndexes.TryGetValue(rallyTargetId, out var rallyTargetIndex))
        {
            return (0, 0, 0);
        }

        var rallyTarget = _agentStates[rallyTargetIndex];
        var deltaXRaw = (long)rallyTarget.XRaw - rallyAgent.XRaw;
        var deltaYRaw = (long)rallyTarget.YRaw - rallyAgent.YRaw;
        var distanceRaw = IntegerSquareRoot(
            checked((deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw)));

        return (deltaXRaw, deltaYRaw, distanceRaw);
    }

    /// <summary>
    /// Computes the point <see cref="FormationRules.RallyTrailRadiusMultiplier"/>
    /// body radii behind the rally agent, opposite the rally agent's own
    /// direction of travel — the point a follower's jitter offset is added
    /// to. Falls back to the rally agent's raw position (no trail) when
    /// <paramref name="direction"/> carries the "no direction" sentinel
    /// (<c>DistanceRaw == 0</c>), since there is no direction of travel to
    /// trail behind. That fallback preserves the pre-fix behaviour in that
    /// corner, which is otherwise untouched by this method's own logic.
    /// </summary>
    private (int XRaw, int YRaw) ComputeRallyTrailBase(
        AgentState rallyAgent,
        (long DeltaXRaw, long DeltaYRaw, long DistanceRaw) direction)
    {
        if (direction.DistanceRaw == 0)
        {
            return (rallyAgent.XRaw, rallyAgent.YRaw);
        }

        var trailRaw = FormationRules.ComputeRallyTrailRaw(Scenario.BodyRadiusRaw);
        var trailXRaw = SaturateToInt32(checked(
            rallyAgent.XRaw - (direction.DeltaXRaw * trailRaw / direction.DistanceRaw)));
        var trailYRaw = SaturateToInt32(checked(
            rallyAgent.YRaw - (direction.DeltaYRaw * trailRaw / direction.DistanceRaw)));

        return (trailXRaw, trailYRaw);
    }

    /// <summary>
    /// Checks whether a regrouping follower's tick-start position falls
    /// inside its own rally agent's forward give-way corridor and, if so,
    /// returns a pure-sideways aim point that clears it. Returns
    /// <see langword="null"/> when the follower is not in the corridor, in
    /// which case <see cref="BuildRegroupingProposal"/> falls back to the
    /// ordinary trail-plus-jitter aim point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Let <c>L</c> be the rally agent's tick-start position, <c>d</c> the
    /// unit direction from <c>L</c> toward the rally agent's own target (the
    /// same direction <see cref="ComputeRallyTrailBase"/> trails behind),
    /// <c>F</c> the follower's tick-start position, and <c>r = F - L</c>.
    /// <c>forward</c> is the scalar projection of <c>r</c> onto <c>d</c>;
    /// <c>lateral</c> is the scalar projection of <c>r</c> onto the
    /// perpendicular of <c>d</c>. The follower is in the corridor when
    /// <c>forward &gt; 0</c> (ahead of the rally agent) and
    /// <c>|lateral| &lt; corridor half-width</c>.
    /// </para>
    /// <para>
    /// The escape point is <c>F</c> plus a step purely along the perpendicular
    /// of <c>d</c>, signed toward the side the follower is already on
    /// (<c>sign(lateral)</c>) so the step can only move it further from the
    /// corridor centre, never across it. A follower sitting exactly on the
    /// leader's axis (<c>lateral == 0</c>) always steps toward the same,
    /// fixed perpendicular side — never decided by iteration or array order.
    /// Because the step has no component along <c>d</c>, the follower's
    /// forward position is unchanged: this is sideways motion only, so the
    /// follower can never re-enter the corridor by taking this step.
    /// </para>
    /// </remarks>
    private (int XRaw, int YRaw)? TryComputeGiveWayAimPoint(
        AgentState agent,
        AgentState rallyAgent,
        (long DeltaXRaw, long DeltaYRaw, long DistanceRaw) direction)
    {
        var relativeXRaw = (long)agent.XRaw - rallyAgent.XRaw;
        var relativeYRaw = (long)agent.YRaw - rallyAgent.YRaw;

        var forwardRaw = checked(
            (relativeXRaw * direction.DeltaXRaw) +
            (relativeYRaw * direction.DeltaYRaw)) / direction.DistanceRaw;
        if (forwardRaw <= 0)
        {
            return null;
        }

        var lateralRaw = checked(
            (relativeXRaw * direction.DeltaYRaw) -
            (relativeYRaw * direction.DeltaXRaw)) / direction.DistanceRaw;
        var corridorHalfWidthRaw =
            FormationRules.ComputeRallyCorridorHalfWidthRaw(Scenario.BodyRadiusRaw);
        if (Math.Abs(lateralRaw) >= corridorHalfWidthRaw)
        {
            return null;
        }

        // Tie-break: exactly on the leader's axis always steps toward the
        // fixed "+" perpendicular side, so the escape direction never
        // depends on which side of zero a rounding error happened to land
        // on, or on the order agents were supplied in.
        var perpendicularSign = lateralRaw < 0 ? -1L : 1L;
        var stepOutRaw = checked(corridorHalfWidthRaw + Scenario.BodyRadiusRaw);

        var aimXRaw = SaturateToInt32(checked(
            agent.XRaw +
            (perpendicularSign * direction.DeltaYRaw * stepOutRaw /
                direction.DistanceRaw)));
        var aimYRaw = SaturateToInt32(checked(
            agent.YRaw -
            (perpendicularSign * direction.DeltaXRaw * stepOutRaw /
                direction.DistanceRaw)));

        var mapWidthRaw = checked(Scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(Scenario.MapHeight * FixedPoint.Scale);
        aimXRaw = CollisionGeometry.ClampCenterToBounds(
            aimXRaw,
            mapWidthRaw,
            Scenario.BodyRadiusRaw);
        aimYRaw = CollisionGeometry.ClampCenterToBounds(
            aimYRaw,
            mapHeightRaw,
            Scenario.BodyRadiusRaw);

        return (aimXRaw, aimYRaw);
    }

    /// <summary>
    /// Saturates a long coordinate into <see cref="int"/>, mirroring
    /// <c>CollisionResolver.ToCoordinate</c>. Safe because the caller always
    /// runs the result through <see cref="CollisionGeometry.ClampCenterToBounds"/>
    /// immediately afterward.
    /// </summary>
    private static int SaturateToInt32(long valueRaw) =>
        (int)Math.Clamp(valueRaw, int.MinValue, int.MaxValue);

    /// <summary>
    /// Hands every living agent to the solid-disc resolver. Agents without a
    /// proposal are still submitted: they occupy space, and a stationary body
    /// would otherwise have its ground taken by a mover resolved before it.
    /// Each mover carries this tick's contested-ground priority, which is a
    /// pure hash of the seed, the tick, and the agent rather than a draw from
    /// any stream.
    /// </summary>
    private void ResolveCollisions()
    {
        _collision.BeginTick();

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                continue;
            }

            var proposal = _movementProposals[index];
            _collision.Requests.Add(
                new CollisionMoveRequest(
                    agent.EntityId,
                    agent.XRaw,
                    agent.YRaw,
                    proposal?.XRaw ?? agent.XRaw,
                    proposal?.YRaw ?? agent.YRaw,
                    proposal is not null,
                    // Only a mover is ordered by its key, so a standing agent
                    // does not pay for a mix it will never be sorted by.
                    proposal is null
                        ? 0
                        : CollisionPriority.Resolve(
                            Scenario.Seed,
                            Tick,
                            agent.EntityId)));
        }

        _collision.Resolver.Resolve(_collision.Requests);
    }

    /// <summary>
    /// The single position commit for the tick. Resolver results are ordered to
    /// match the living agents in ascending entity ID, which is the same order
    /// they were submitted in.
    /// </summary>
    private void CommitMovement(List<BattleEvent> events)
    {
        var results = _collision.Resolver.Results;
        var resultIndex = 0;

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive)
            {
                agent.MovementResolution = MovementResolution.None;
                _collision.RecordBlocked(index, isBlocked: false);
                continue;
            }

            var result = results[resultIndex];
            resultIndex++;

            var previousX = agent.XRaw;
            var previousY = agent.YRaw;
            agent.XRaw = result.XRaw;
            agent.YRaw = result.YRaw;
            agent.MovementResolution = result.Resolution;
            _collision.RecordBlocked(
                index,
                result.Resolution == MovementResolution.Blocked);

            var deltaX = (long)agent.XRaw - previousX;
            var deltaY = (long)agent.YRaw - previousY;
            if (deltaX == 0 && deltaY == 0)
            {
                continue;
            }

            var movedRaw = checked((int)IntegerSquareRoot(
                checked((deltaX * deltaX) + (deltaY * deltaY))));
            AddEvent(
                events,
                BattleEventKind.Move,
                agent.EntityId,
                _movementProposals[index]?.TargetId,
                movedRaw,
                agent.FactionId);
        }
    }

    /// <summary>
    /// Derives this tick's collision counters from committed positions. Pure
    /// observation: nothing here writes agent state, and none of it is hashed.
    /// </summary>
    private void MeasureCollision()
    {
        _collision.Bodies.Clear();
        foreach (var agent in _agentStates)
        {
            _collision.Bodies.Add(
                new CollisionBody(
                    agent.EntityId,
                    agent.XRaw,
                    agent.YRaw,
                    agent.IsAlive));
        }

        _collision.Grid.Rebuild(
            _collision.Bodies,
            _collision.ContactBandRadiusRaw);

        var contactDistanceRaw = checked(2 * Scenario.BodyRadiusRaw);
        var contactPairs = 0;
        var penetrationRaw = 0;
        var minimumX = int.MaxValue;
        var maximumX = int.MinValue;
        var minimumY = int.MaxValue;
        var maximumY = int.MinValue;

        foreach (var pair in _collision.Grid.PairsList)
        {
            var left = _agentStates[_agentIndexes[pair.LowEntityId]];
            var right = _agentStates[_agentIndexes[pair.HighEntityId]];
            var separationRaw = checked((int)IntegerSquareRoot(
                CollisionGeometry.SquaredDistance(
                    left.XRaw,
                    left.YRaw,
                    right.XRaw,
                    right.YRaw)));
            penetrationRaw = Math.Max(
                penetrationRaw,
                contactDistanceRaw - separationRaw);

            if (left.FactionId == right.FactionId)
            {
                continue;
            }

            contactPairs++;
        }

        // The front spans agents holding an enemy in reach, not strictly
        // touching bodies. The resolver leaves every living pair at or beyond
        // the contact distance, so strict touching means a squared distance of
        // exactly (2R)^2 — a Pythagorean coincidence on an integer lattice — and
        // a span built on it would read zero through an entire battle. Contact
        // pairs are counted separately, over a proximity band.
        var attackCapableAgents = 0;
        foreach (var agent in _agentStates)
        {
            if (!agent.IsAlive || agent.TargetEntityId is not { } targetId)
            {
                continue;
            }

            var target = _agentStates[_agentIndexes[targetId]];
            if (!target.IsAlive || !IsWithinAttackRange(agent, target))
            {
                continue;
            }

            attackCapableAgents++;
            minimumX = Math.Min(minimumX, agent.XRaw);
            maximumX = Math.Max(maximumX, agent.XRaw);
            minimumY = Math.Min(minimumY, agent.YRaw);
            maximumY = Math.Max(maximumY, agent.YRaw);
        }

        _lastTickCollision = new CollisionTickMetrics(
            _collision.Grid.Pairs.Count,
            contactPairs,
            _collision.Resolver.AcceptedMoveCount,
            _collision.Resolver.BlockedCount,
            attackCapableAgents,
            attackCapableAgents == 0 ? 0 : checked(maximumY - minimumY),
            attackCapableAgents == 0 ? 0 : checked(maximumX - minimumX),
            penetrationRaw);
    }

    private void GatherAndCommitAttacks(List<BattleEvent> events)
    {
        Array.Clear(_damageTotals);
        var proposalCount = 0;

        // Derived observability counters for this tick. Locals rather than
        // state, folded into the reported value once at the end of the loop,
        // so nothing here can be read by the simulation itself.
        //
        // Each counter is indexed by the attacking agent's faction so the same
        // loop yields both the undivided total and the per-faction split, with
        // no second pass and no new query. Two factions exist, so these are
        // fixed-size stack arrays; the totals are summed at the end rather than
        // tracked separately, which makes the split a partition by
        // construction rather than by assertion.
        Span<int> accepted = stackalloc int[FactionCount];
        Span<int> landed = stackalloc int[FactionCount];
        Span<int> shieldBlocked = stackalloc int[FactionCount];
        Span<int> parried = stackalloc int[FactionCount];
        Span<int> deflected = stackalloc int[FactionCount];
        Span<int> evaded = stackalloc int[FactionCount];

        for (var sourceIndex = 0;
             sourceIndex < _agentStates.Length;
             sourceIndex++)
        {
            var source = _agentStates[sourceIndex];
            if (!source.IsAlive)
            {
                continue;
            }

            // Plan section 3(a): a chaining attacker's pre-check runs ahead
            // of the cooldown check below, not after it, so that an attacker
            // still mid-cooldown discovers a dead or out-of-reach target — and
            // clears its chain — on the very tick that becomes true, rather
            // than only once its own cooldown next lets it act.
            if (source.TargetEntityId is not { } targetId)
            {
                ClearActiveComboChain(source);
                continue;
            }

            var target = _agentStates[_agentIndexes[targetId]];
            if (!target.IsAlive)
            {
                ClearActiveComboChain(source);
                continue;
            }

            if (!IsWithinAttackRange(source, target))
            {
                ClearActiveComboChain(source);
                continue;
            }

            if (source.AttackCooldownRemaining != 0)
            {
                continue;
            }

            // 3(b): resolve the attack. Unchanged from before this feature —
            // the cooldown write that used to happen here moves below, after
            // the combo transition, because which cooldown to write now
            // depends on the combo outcome.
            source.Intent = AgentIntent.Attacking;
            var targetIndex = _agentIndexes[target.EntityId];
            var hitLocation = HitLocationResolver.Resolve(
                _rules,
                source.Loadout,
                target.Loadout,
                Scenario.Seed,
                Tick,
                source.EntityId,
                target.EntityId);

            // Resolved inline, immediately after the hit location, in the same
            // pass. A second pass over the proposals would be a second place
            // the attack tuple has to stay consistent, and a per-target buffer
            // would be state whose staleness nothing checks. The clash costs no
            // draw from any generator, so nothing downstream shifts merely
            // because this call was added.
            var resolution = ClashResolver.Resolve(
                _rules.ClashProfile,
                Scenario.Seed,
                Tick,
                source.EntityId,
                target.EntityId,
                source.Loadout.Weapon,
                target.Loadout.Weapon,
                target.Loadout.Shield);

            var comboPosition = ResolveComboTransition(source, target, resolution);

            _attackProposals[proposalCount] =
                (sourceIndex, targetIndex, hitLocation, resolution, comboPosition);
            proposalCount++;

            // Only a landed blow reaches the damage total. Every other
            // resolution still emitted its attack event above and still burned
            // the attacker's cooldown; it simply carries no damage.
            if (resolution == AttackResolution.Landed)
            {
                _damageTotals[targetIndex] = checked(
                    _damageTotals[targetIndex] + source.DamagePerAttack);
            }

            // Credited to the attacker's faction. Every accepted attack has
            // exactly one attacker, so this is a partition of proposalCount.
            var attackerFaction = source.FactionId;
            accepted[attackerFaction]++;

            switch (resolution)
            {
                case AttackResolution.Landed:
                    landed[attackerFaction]++;
                    break;
                case AttackResolution.ShieldBlocked:
                    shieldBlocked[attackerFaction]++;
                    break;
                case AttackResolution.Parried:
                    parried[attackerFaction]++;
                    break;
                case AttackResolution.Deflected:
                    deflected[attackerFaction]++;
                    break;
                default:
                    evaded[attackerFaction]++;
                    break;
            }
        }

        _lastTickCombatByFaction = new FactionCombatMetrics(
            new CombatMetrics(
                accepted[0],
                landed[0],
                shieldBlocked[0],
                parried[0],
                deflected[0],
                evaded[0]),
            new CombatMetrics(
                accepted[1],
                landed[1],
                shieldBlocked[1],
                parried[1],
                deflected[1],
                evaded[1]));

        // Derived from the split rather than counted separately, so the two can
        // never disagree. proposalCount is asserted equal to the summed
        // accepted count below: it is maintained by the loop for indexing
        // _attackProposals, so it is an independent witness that the per-faction
        // credit above missed nothing.
        _lastTickCombat = _lastTickCombatByFaction.Total;

        if (_lastTickCombat.AcceptedAttacks != proposalCount)
        {
            throw new InvalidOperationException(
                "Per-faction attack accounting lost or duplicated an accepted " +
                "attack: the faction split sums to " +
                $"{_lastTickCombat.AcceptedAttacks} but {proposalCount} " +
                "attacks were resolved this tick.");
        }

        for (var index = 0; index < proposalCount; index++)
        {
            var proposal = _attackProposals[index];
            var source = _agentStates[proposal.SourceIndex];
            var target = _agentStates[proposal.TargetIndex];
            AddAttackEvent(
                events,
                source.EntityId,
                target.EntityId,
                proposal.Resolution == AttackResolution.Landed
                    ? source.DamagePerAttack
                    : 0,
                source.FactionId,
                source.Loadout.Weapon,
                source.Loadout.Shield,
                proposal.HitLocation,
                proposal.Resolution,
                proposal.ComboPosition);
        }

        for (var index = 0; index < _damageTotals.Length; index++)
        {
            var damage = _damageTotals[index];
            if (damage == 0)
            {
                continue;
            }

            var target = _agentStates[index];
            target.HitPoints = Math.Max(0, target.HitPoints - damage);
            AddEvent(
                events,
                BattleEventKind.Damage,
                target.EntityId,
                target.EntityId,
                damage,
                target.FactionId);
        }

        for (var index = 0; index < _damageTotals.Length; index++)
        {
            var agent = _agentStates[index];
            if (_damageTotals[index] == 0 || agent.IsAlive)
            {
                continue;
            }

            agent.TargetEntityId = null;
            agent.Intent = AgentIntent.Dead;
            AddEvent(
                events,
                BattleEventKind.Death,
                agent.EntityId,
                null,
                0,
                agent.FactionId);
        }
    }

    /// <summary>
    /// Plan section 3(c): the full combo-transition algorithm, run
    /// immediately after one attack attempt's <see cref="AttackResolution"/>
    /// is known and immediately before the attack proposal is buffered.
    /// Mutates <paramref name="source"/>'s
    /// <see cref="AgentState.ComboStepsRemaining"/> and
    /// <see cref="AgentState.ComboTargetEntityId"/>, writes the cooldown this
    /// blow earns into <see cref="AgentState.AttackCooldownRemaining"/> —
    /// replacing the unconditional pre-resolution write this preset family
    /// used before attack combinations existed — and returns this attack
    /// event's chain-position value, or <c>null</c> when the blow is not
    /// part of a chain.
    /// </summary>
    /// <remarks>
    /// The six numbered steps below are plan section 3(c)'s six numbered
    /// steps, in the same order, and must not be reordered or simplified:
    /// this is the determinism-critical core of the feature.
    /// <para>
    /// Every branch below that writes the plan's "normal" (non-combo)
    /// cooldown reads it from <see cref="AgentState.AttackCooldownTicks"/>
    /// rather than from <c>weaponProfile.AttackCooldownTicks</c>. The two are
    /// bit-identical for every agent <see cref="CreateAgent"/> ever produces
    /// — both are the same ruleset lookup against the same immutable
    /// loadout — so this is not a behavioural choice, only which of two
    /// equal sources to read. <see cref="AgentState.AttackCooldownTicks"/>
    /// is preferred because it is the single cached value every other tick
    /// stage already reads (see <see cref="CreateAgent"/>'s remarks on why a
    /// second reach path must never exist), rather than opening a second,
    /// redundant profile lookup that could only ever disagree with it by
    /// construction error.
    /// </para>
    /// </remarks>
    private int? ResolveComboTransition(
        AgentState source,
        AgentState target,
        AttackResolution resolution)
    {
        var weaponProfile = ResolveAttackerWeaponProfile(source.Loadout);

        // Step 1.
        var wasChaining = source.ComboStepsRemaining > 0;

        // Step 2: Question 1's strict target-binding check. The retarget, if
        // any, already happened earlier this same tick in
        // SelectTargetsAndIntents.
        var retargeted =
            wasChaining && source.ComboTargetEntityId != source.TargetEntityId;

        // Step 3.
        if (retargeted)
        {
            source.ComboStepsRemaining = 0;
            source.ComboTargetEntityId = null;
            wasChaining = false;
        }

        int? comboPosition;
        int cooldown;

        if (resolution != AttackResolution.Landed)
        {
            // Step 4: a non-landed attempt is neither a roll nor a break.
            // ComboStepsRemaining and ComboTargetEntityId are left exactly as
            // they are post step 3.
            comboPosition = null;
            cooldown = wasChaining
                ? weaponProfile.ComboCooldownTicks
                : source.AttackCooldownTicks;
        }
        else if (!wasChaining)
        {
            // Step 5: an unchained landed blow, eligible to open a chain.
            var openRoll = (int)(ComboResolver.MixCombo(
                Scenario.Seed,
                Tick,
                source.EntityId,
                target.EntityId,
                source.Loadout.Weapon,
                comboStepsRemaining: 0,
                ComboResolver.ComboOpenTag) % ClashProfile.BasisPointScale);

            if (openRoll < weaponProfile.ComboOpenChanceBasisPoints)
            {
                var maxSteps = Math.Min(source.Level, weaponProfile.ComboMaxSteps);
                comboPosition = 1;
                source.ComboStepsRemaining = maxSteps - 1;
                source.ComboTargetEntityId = target.EntityId;
                cooldown = source.ComboStepsRemaining > 0
                    ? weaponProfile.ComboCooldownTicks
                    : source.AttackCooldownTicks;
            }
            else
            {
                comboPosition = null;
                cooldown = source.AttackCooldownTicks;
            }
        }
        else
        {
            // Step 6: a continuing blow candidate.
            var maxSteps = Math.Min(source.Level, weaponProfile.ComboMaxSteps);
            var thisPosition = maxSteps - source.ComboStepsRemaining + 1;
            var continueRoll = (int)(ComboResolver.MixCombo(
                Scenario.Seed,
                Tick,
                source.EntityId,
                target.EntityId,
                source.Loadout.Weapon,
                source.ComboStepsRemaining,
                ComboResolver.ComboContinueTag) % ClashProfile.BasisPointScale);
            var continuationSucceeded =
                continueRoll < weaponProfile.ComboContinueChanceBasisPoints;

            // Mirrors however _damageTotals's accumulation reads
            // target.HitPoints for this same purpose: against the value as
            // it stands before the end-of-pass-1 damage-application loop
            // applies it, not adjusted for any other attacker's damage
            // accumulated this same tick.
            var killedByThisBlow =
                target.HitPoints - source.DamagePerAttack <= 0;

            // The blow landed, so it always counts, regardless of what
            // happens next.
            comboPosition = thisPosition;

            // Design 3.2's exact order: check 1 (continuation), check 2 (max
            // length reached), check 3 (the target dies on this blow). Check
            // 4 ("target out of reach") cannot fire here — the pre-check
            // above already guaranteed the target is in range for this
            // tick's attempt — it fires on a later tick via that pre-check's
            // clearing clause instead.
            var chainSurvives = continuationSucceeded &&
                thisPosition < maxSteps &&
                !killedByThisBlow;

            if (chainSurvives)
            {
                // Guaranteed > 0 here, since thisPosition < maxSteps was
                // just confirmed.
                source.ComboStepsRemaining -= 1;
                cooldown = weaponProfile.ComboCooldownTicks;
            }
            else
            {
                source.ComboStepsRemaining = 0;
                source.ComboTargetEntityId = null;
                cooldown = source.AttackCooldownTicks;
            }
        }

        source.AttackCooldownRemaining = cooldown;
        return comboPosition;
    }

    private void ResolveOutcome(List<BattleEvent> events)
    {
        var faction0Alive = false;
        var faction1Alive = false;

        foreach (var agent in _agentStates)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            if (agent.FactionId == 0)
            {
                faction0Alive = true;
            }
            else
            {
                faction1Alive = true;
            }
        }

        Outcome = (faction0Alive, faction1Alive) switch
        {
            (false, false) => BattleOutcome.Draw,
            (true, false) => BattleOutcome.Faction0Victory,
            (false, true) => BattleOutcome.Faction1Victory,
            _ when Tick >= Scenario.TickLimit => BattleOutcome.Draw,
            _ => BattleOutcome.Ongoing,
        };

        if (Outcome == BattleOutcome.Ongoing)
        {
            return;
        }

        var winningFaction = Outcome switch
        {
            BattleOutcome.Faction0Victory => 0,
            BattleOutcome.Faction1Victory => 1,
            _ => (int?)null,
        };
        AddEvent(
            events,
            BattleEventKind.Outcome,
            sourceEntityId: 0,
            targetEntityId: null,
            value: (int)Outcome,
            factionId: winningFaction);
    }

    /// <summary>
    /// Builds a movement proposal that closes an agent on an enemy it is
    /// fighting. Delegates to the point-taking overload, stopping short by
    /// one body diameter so bodies come to rest touching rather than
    /// overlapping — see the point-taking overload's remarks for why a rally
    /// point is handled differently.
    /// </summary>
    private (int XRaw, int YRaw, ulong TargetId) BuildMovementProposal(
        AgentState agent,
        AgentState target) =>
        BuildMovementProposal(
            agent,
            target.XRaw,
            target.YRaw,
            target.EntityId,
            // Agents close to body contact, not merely to weapon reach.
            // Stopping at reach left four world units of permanent air
            // between opposing front ranks, so bodies never touched and the
            // collision stage only ever saw allies queueing. Attacks still
            // resolve at reach, which is wider than the diameter, so a rank
            // pressed into contact fights and the rank behind it can reach
            // past.
            stopShortRaw: checked(2 * Scenario.BodyRadiusRaw));

    /// <summary>
    /// Builds a movement proposal toward an arbitrary destination point
    /// rather than another agent's live position. Used for last-stand rally
    /// movement, where a follower wants to actually reach its aim point
    /// rather than stop short of it the way it would closing on an enemy
    /// body — so this overload walks all the way in (<c>stopShortRaw: 0</c>)
    /// instead of duplicating the enemy-closing overload's stopping-distance
    /// arithmetic.
    /// </summary>
    private (int XRaw, int YRaw, ulong TargetId) BuildMovementProposal(
        AgentState agent,
        int destinationXRaw,
        int destinationYRaw,
        ulong targetId) =>
        BuildMovementProposal(
            agent,
            destinationXRaw,
            destinationYRaw,
            targetId,
            stopShortRaw: 0);

    private (int XRaw, int YRaw, ulong TargetId) BuildMovementProposal(
        AgentState agent,
        int destinationXRaw,
        int destinationYRaw,
        ulong targetId,
        int stopShortRaw)
    {
        var deltaX = (long)destinationXRaw - agent.XRaw;
        var deltaY = (long)destinationYRaw - agent.YRaw;
        var distanceSquared = checked((deltaX * deltaX) + (deltaY * deltaY));
        var distance = IntegerSquareRoot(distanceSquared);

        var desiredMovement = Math.Max(1, distance - stopShortRaw);
        var movement = Math.Min(agent.MovementSpeedRaw, desiredMovement);
        var moveX = checked(deltaX * movement / Math.Max(1, distance));
        var moveY = checked(deltaY * movement / Math.Max(1, distance));

        if (moveX == 0 && moveY == 0)
        {
            if (Math.Abs(deltaX) >= Math.Abs(deltaY))
            {
                moveX = Math.Sign(deltaX);
            }
            else
            {
                moveY = Math.Sign(deltaY);
            }
        }

        var nextX = CollisionGeometry.ClampCenterToBounds(
            checked(agent.XRaw + (int)moveX),
            checked(Scenario.MapWidth * FixedPoint.Scale),
            Scenario.BodyRadiusRaw);
        var nextY = CollisionGeometry.ClampCenterToBounds(
            checked(agent.YRaw + (int)moveY),
            checked(Scenario.MapHeight * FixedPoint.Scale),
            Scenario.BodyRadiusRaw);
        return (nextX, nextY, targetId);
    }

    /// <summary>
    /// The single approved reach test. Attack range is measured centre to
    /// centre, never surface to surface, so intent selection and attack
    /// gathering cannot disagree about who can strike whom.
    /// </summary>
    private static bool IsWithinAttackRange(AgentState source, AgentState target) =>
        IsWithinAttackRange(source, SquaredDistance(source, target));

    private static bool IsWithinAttackRange(
        AgentState source,
        long squaredDistance) =>
        squaredDistance <= checked(
            (long)source.AttackRangeRaw * source.AttackRangeRaw);

    private static long SquaredDistance(AgentState left, AgentState right)
    {
        var deltaX = (long)right.XRaw - left.XRaw;
        var deltaY = (long)right.YRaw - left.YRaw;
        return checked((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static long IntegerSquareRoot(long value)
    {
        var remainder = checked((ulong)value);
        ulong root = 0;
        var bit = 1UL << 62;

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

        return checked((long)root);
    }

    private void AddEvent(
        List<BattleEvent> events,
        BattleEventKind kind,
        ulong sourceEntityId,
        ulong? targetEntityId,
        int value,
        int? factionId)
    {
        _eventSequence = checked(_eventSequence + 1);
        events.Add(
            BattleEvent.NonAttack(
                _eventSequence,
                Tick,
                kind,
                sourceEntityId,
                targetEntityId,
                value,
                factionId));
    }

    /// <summary>
    /// Emits one attack event.
    /// </summary>
    /// <remarks>
    /// <paramref name="resolution"/> is required, unlike the optional
    /// parameter on the public <see cref="BattleEvent.Attack"/> factory. The
    /// factory keeps its default so that the twenty call sites in tests and
    /// presentation code that do not care about defensive resolution keep
    /// compiling; here the default would be a way for production code to emit
    /// an unresolved attack as though it had landed, and nothing downstream
    /// would notice.
    /// </remarks>
    private void AddAttackEvent(
        List<BattleEvent> events,
        ulong sourceEntityId,
        ulong targetEntityId,
        int damage,
        int factionId,
        WeaponId weapon,
        ShieldId shield,
        BodyPart hitLocation,
        AttackResolution resolution,
        int? comboPosition)
    {
        _eventSequence = checked(_eventSequence + 1);
        events.Add(
            BattleEvent.Attack(
                _eventSequence,
                Tick,
                sourceEntityId,
                targetEntityId,
                damage,
                factionId,
                weapon,
                shield,
                hitLocation,
                resolution,
                comboPosition));
    }

    private void UpdateViews()
    {
        for (var index = 0; index < _agentStates.Length; index++)
        {
            _agentViews[index] = _agentStates[index].ToView();
        }
    }
}
