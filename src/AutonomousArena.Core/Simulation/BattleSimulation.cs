using System.Collections.ObjectModel;
using AutonomousArena.Core.Determinism;
using AutonomousArena.Core.Mathematics;

namespace AutonomousArena.Core.Simulation;

/// <summary>
/// Authoritative, deterministic, fixed-tick battle state.
/// </summary>
public sealed class BattleSimulation
{
    private readonly AgentState[] _agentStates;
    private readonly Dictionary<ulong, int> _agentIndexes;
    private readonly int[] _damageTotals;
    private ReadOnlyCollection<AgentView> _agents;
    private ReadOnlyCollection<BattleEvent> _lastEvents;
    private long _eventSequence;

    private BattleSimulation(Scenario scenario, AgentState[] agents)
    {
        Scenario = scenario;
        _agentStates = agents;
        _agentIndexes = new Dictionary<ulong, int>(agents.Length);
        _damageTotals = new int[agents.Length];

        for (var index = 0; index < agents.Length; index++)
        {
            if (!_agentIndexes.TryAdd(agents[index].EntityId, index))
            {
                throw new ArgumentException(
                    $"Duplicate entity ID {agents[index].EntityId}.",
                    nameof(agents));
            }
        }

        _agents = BuildViews();
        _lastEvents = Array.AsReadOnly<BattleEvent>([]);
    }

    public Scenario Scenario { get; }

    public long Tick { get; private set; }

    public BattleOutcome Outcome { get; private set; }

    public IReadOnlyList<AgentView> Agents => _agents;

    public IReadOnlyList<BattleEvent> LastEvents => _lastEvents;

    public static BattleSimulation Create(Scenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();

        var random = new SplitMix64(scenario.Seed);
        var agents = new AgentState[scenario.TotalAgents];
        var mapWidthRaw = checked(scenario.MapWidth * FixedPoint.Scale);
        var mapHeightRaw = checked(scenario.MapHeight * FixedPoint.Scale);
        var horizontalBandRaw = Math.Max(1, mapWidthRaw / 10);
        var verticalMarginRaw = Math.Min(FixedPoint.Scale * 8, mapHeightRaw / 4);
        var usableHeightRaw = Math.Max(
            1,
            mapHeightRaw - (verticalMarginRaw * 2));

        for (var index = 0; index < scenario.AgentsPerFaction; index++)
        {
            var leftX = (mapWidthRaw / 4) + random.NextInt(horizontalBandRaw);
            var leftY = verticalMarginRaw + random.NextInt(usableHeightRaw);
            agents[index] = CreateAgent(
                checked((ulong)index + 1),
                factionId: 0,
                leftX,
                leftY,
                scenario);
        }

        for (var index = 0; index < scenario.AgentsPerFaction; index++)
        {
            var rightX = checked(
                (int)(((long)mapWidthRaw * 3) / 4) -
                random.NextInt(horizontalBandRaw));
            var rightY = verticalMarginRaw + random.NextInt(usableHeightRaw);
            var stateIndex = scenario.AgentsPerFaction + index;
            agents[stateIndex] = CreateAgent(
                checked((ulong)stateIndex + 1),
                factionId: 1,
                rightX,
                rightY,
                scenario);
        }

        return new BattleSimulation(scenario, agents);
    }

    internal static BattleSimulation CreateForTesting(
        Scenario scenario,
        params AgentState[] agents)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(agents);
        scenario.Validate();

        if (agents.Length == 0)
        {
            throw new ArgumentException(
                "At least one agent is required.",
                nameof(agents));
        }

        var orderedAgents = agents.OrderBy(agent => agent.EntityId).ToArray();
        return new BattleSimulation(scenario, orderedAgents);
    }

    public void AdvanceOneTick()
    {
        if (Outcome != BattleOutcome.Ongoing)
        {
            _lastEvents = Array.AsReadOnly<BattleEvent>([]);
            return;
        }

        Tick = checked(Tick + 1);
        var events = new List<BattleEvent>(_agentStates.Length * 2);

        DecrementCooldowns();
        SelectTargetsAndIntents();
        GatherAndCommitMovement(events);
        GatherAndCommitAttacks(events);
        ResolveOutcome(events);

        _agents = BuildViews();
        _lastEvents = events.AsReadOnly();
    }

    public ulong ComputeStateHash() =>
        StateHasher.Compute(
            Scenario,
            Tick,
            Outcome,
            _eventSequence,
            _agentStates);

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
            scenario.AttackCooldownTicks);

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

            var attackRangeSquared = checked(
                (long)agent.AttackRangeRaw * agent.AttackRangeRaw);
            agent.Intent = selectedDistance <= attackRangeSquared
                ? AgentIntent.Attacking
                : AgentIntent.Moving;
        }
    }

    private void GatherAndCommitMovement(List<BattleEvent> events)
    {
        var proposals = new (int XRaw, int YRaw, ulong TargetId)?[_agentStates.Length];

        for (var index = 0; index < _agentStates.Length; index++)
        {
            var agent = _agentStates[index];
            if (!agent.IsAlive ||
                agent.Intent != AgentIntent.Moving ||
                agent.TargetEntityId is not { } targetId)
            {
                continue;
            }

            var target = _agentStates[_agentIndexes[targetId]];
            proposals[index] = BuildMovementProposal(agent, target);
        }

        for (var index = 0; index < proposals.Length; index++)
        {
            if (proposals[index] is not { } proposal)
            {
                continue;
            }

            var agent = _agentStates[index];
            var previousX = agent.XRaw;
            var previousY = agent.YRaw;
            agent.XRaw = proposal.XRaw;
            agent.YRaw = proposal.YRaw;
            var deltaX = (long)agent.XRaw - previousX;
            var deltaY = (long)agent.YRaw - previousY;
            var movedRaw = checked((int)IntegerSquareRoot(
                checked((deltaX * deltaX) + (deltaY * deltaY))));
            AddEvent(
                events,
                BattleEventKind.Move,
                agent.EntityId,
                proposal.TargetId,
                movedRaw,
                agent.FactionId);
        }
    }

    private void GatherAndCommitAttacks(List<BattleEvent> events)
    {
        Array.Clear(_damageTotals);
        var proposals = new List<(AgentState Source, AgentState Target)>();

        foreach (var source in _agentStates)
        {
            if (!source.IsAlive ||
                source.TargetEntityId is not { } targetId ||
                source.AttackCooldownRemaining != 0)
            {
                continue;
            }

            var target = _agentStates[_agentIndexes[targetId]];
            if (!target.IsAlive)
            {
                continue;
            }

            var attackRangeSquared = checked(
                (long)source.AttackRangeRaw * source.AttackRangeRaw);
            if (SquaredDistance(source, target) > attackRangeSquared)
            {
                continue;
            }

            source.Intent = AgentIntent.Attacking;
            source.AttackCooldownRemaining = source.AttackCooldownTicks;
            proposals.Add((source, target));
            var targetIndex = _agentIndexes[target.EntityId];
            _damageTotals[targetIndex] = checked(
                _damageTotals[targetIndex] + source.DamagePerAttack);
        }

        foreach (var proposal in proposals)
        {
            AddEvent(
                events,
                BattleEventKind.Attack,
                proposal.Source.EntityId,
                proposal.Target.EntityId,
                proposal.Source.DamagePerAttack,
                proposal.Source.FactionId);
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

    private (int XRaw, int YRaw, ulong TargetId) BuildMovementProposal(
        AgentState agent,
        AgentState target)
    {
        var deltaX = (long)target.XRaw - agent.XRaw;
        var deltaY = (long)target.YRaw - agent.YRaw;
        var distanceSquared = checked((deltaX * deltaX) + (deltaY * deltaY));
        var distance = IntegerSquareRoot(distanceSquared);
        var desiredMovement = Math.Max(1, distance - agent.AttackRangeRaw);
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

        var maximumX = checked(Scenario.MapWidth * FixedPoint.Scale);
        var maximumY = checked(Scenario.MapHeight * FixedPoint.Scale);
        var nextX = Math.Clamp(checked(agent.XRaw + (int)moveX), 0, maximumX);
        var nextY = Math.Clamp(checked(agent.YRaw + (int)moveY), 0, maximumY);
        return (nextX, nextY, target.EntityId);
    }

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
            new BattleEvent(
                _eventSequence,
                Tick,
                kind,
                sourceEntityId,
                targetEntityId,
                value,
                factionId));
    }

    private ReadOnlyCollection<AgentView> BuildViews()
    {
        var views = new AgentView[_agentStates.Length];
        for (var index = 0; index < _agentStates.Length; index++)
        {
            views[index] = _agentStates[index].ToView();
        }

        return Array.AsReadOnly(views);
    }
}
