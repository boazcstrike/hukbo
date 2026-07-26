using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal sealed class HitEffectSystem
{
    private const float PulseSeconds = 0.09f;

    private readonly Dictionary<ulong, AgentView> _agentsById = [];
    private readonly HashSet<ulong> _deathEntityIds = [];
    private readonly HitEffect[] _effects;
    private int _count;

    public HitEffectSystem(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _effects = new HitEffect[capacity];
    }

    public ReadOnlySpan<HitEffect> ActiveEffects =>
        _effects.AsSpan(0, _count);

    public void Ingest(
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<AgentView> agents)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(agents);

        _agentsById.Clear();
        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            _agentsById[agent.EntityId] = agent;
        }

        _deathEntityIds.Clear();
        for (var index = 0; index < events.Count; index++)
        {
            var battleEvent = events[index];
            if (battleEvent.Kind == BattleEventKind.Death)
            {
                _deathEntityIds.Add(battleEvent.SourceEntityId);
            }
        }

        for (var index = 0; index < events.Count; index++)
        {
            var battleEvent = events[index];
            if (battleEvent.Kind != BattleEventKind.Damage ||
                battleEvent.TargetEntityId is not { } targetEntityId ||
                !_agentsById.TryGetValue(targetEntityId, out var agent))
            {
                continue;
            }

            Add(
                new HitEffect(
                    battleEvent.Sequence,
                    targetEntityId,
                    agent.XRaw,
                    agent.YRaw,
                    battleEvent.Value,
                    _deathEntityIds.Contains(targetEntityId),
                    AgeSeconds: 0f));
        }
    }

    public void Advance(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        var writeIndex = 0;
        for (var readIndex = 0; readIndex < _count; readIndex++)
        {
            var advanced = _effects[readIndex] with
            {
                AgeSeconds = _effects[readIndex].AgeSeconds + elapsedSeconds,
            };
            if (advanced.AgeSeconds >= advanced.LifetimeSeconds)
            {
                continue;
            }

            _effects[writeIndex] = advanced;
            writeIndex++;
        }

        Array.Clear(_effects, writeIndex, _count - writeIndex);
        _count = writeIndex;
    }

    public float GetPulseStrength(ulong entityId)
    {
        var maximumStrength = 0f;
        for (var index = 0; index < _count; index++)
        {
            var effect = _effects[index];
            if (effect.TargetEntityId != entityId || effect.IsLethal)
            {
                continue;
            }

            var strength = Math.Clamp(
                1f - (effect.AgeSeconds / PulseSeconds),
                0f,
                1f);
            maximumStrength = MathF.Max(maximumStrength, strength);
        }

        return maximumStrength;
    }

    public void Clear()
    {
        Array.Clear(_effects, 0, _count);
        _count = 0;
        _agentsById.Clear();
        _deathEntityIds.Clear();
    }

    private void Add(HitEffect effect)
    {
        if (_count < _effects.Length)
        {
            _effects[_count] = effect;
            _count++;
            return;
        }

        var replacementIndex = 0;
        for (var index = 1; index < _count; index++)
        {
            var candidate = _effects[index];
            var current = _effects[replacementIndex];
            if (candidate.AgeSeconds > current.AgeSeconds ||
                (candidate.AgeSeconds == current.AgeSeconds &&
                 candidate.Sequence < current.Sequence))
            {
                replacementIndex = index;
            }
        }

        _effects[replacementIndex] = effect;
    }
}
