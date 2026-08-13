using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

internal sealed class HitEffectSystem
{
    private const float PulseSeconds = 0.09f;

    /// <summary>
    /// PROVISIONAL legibility tuning (2026-08-13, the lethal blow
    /// legibility plan): a killing blow gets its own, longer pulse instead
    /// of being excluded from the pulse entirely. This must stay strictly
    /// shorter than
    /// <see cref="AttackAnimation.LethalHoldSeconds"/>, because once the hold
    /// expires there is no pawn left for the pulse to play over.
    /// </summary>
    private const float LethalPulseSeconds = 0.30f;

    private readonly Dictionary<ulong, AgentView> _agentsById = [];
    private readonly HashSet<ulong> _deathEntityIds = [];
    private readonly Dictionary<ulong, float> _pulseByEntityId = [];
    private readonly HitEffect[] _effects;
    private int _count;

    public HitEffectSystem(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _effects = new HitEffect[capacity];
    }

    public ReadOnlySpan<HitEffect> ActiveEffects =>
        _effects.AsSpan(0, _count);

    /// <summary>
    /// Starts the impact ring owned by one atomic contact bundle. Aggregate
    /// Damage events never reach this path, so multiple landed attacks retain
    /// their individual damage and only the dispatcher-owned lethal contact
    /// receives the lethal treatment.
    /// </summary>
    public void StartContact(
        AttackContactBundle contact,
        AgentView defender)
    {
        if (contact.Resolution != AttackResolution.Landed)
        {
            return;
        }

        Add(
            new HitEffect(
                contact.Sequence,
                contact.DefenderEntityId,
                defender.XRaw,
                defender.YRaw,
                contact.Damage,
                contact.IsLethal,
                AgeSeconds: 0f));
    }

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
            if (effect.TargetEntityId != entityId)
            {
                continue;
            }

            var pulseSeconds = effect.IsLethal ? LethalPulseSeconds : PulseSeconds;
            var strength = Math.Clamp(
                1f - (effect.AgeSeconds / pulseSeconds),
                0f,
                1f);
            maximumStrength = MathF.Max(maximumStrength, strength);
        }

        return maximumStrength;
    }

    /// <summary>
    /// Builds the pulse strength of every entity that currently has at least
    /// one live effect, ordinary or lethal, in a single pass over the live
    /// effects, and returns a read-only view over the result.
    /// </summary>
    /// <remarks>
    /// This is not a cache. The lookup is rebuilt from scratch on every call
    /// and retains nothing from the previous call: the backing storage is
    /// cleared before the pass begins, so no entry can survive a rebuild, and
    /// there is therefore no invalidation or staleness rule to get wrong. The
    /// caller is expected to build it once per frame, after
    /// <see cref="Ingest"/> and <see cref="Advance"/> have run, and to read it
    /// only for the remainder of that frame. The returned view is a ref struct
    /// precisely so that it cannot be stored in a field and survive the frame
    /// that produced it.
    ///
    /// The strength arithmetic below is deliberately written out again rather
    /// than shared with <see cref="GetPulseStrength"/>. That keeps
    /// <see cref="GetPulseStrength"/> an independent reference implementation,
    /// so the equivalence test compares two separate computations instead of
    /// two calls into the same one.
    /// </remarks>
    public HitPulseLookup BuildPulseLookup()
    {
        _pulseByEntityId.Clear();
        for (var index = 0; index < _count; index++)
        {
            var effect = _effects[index];
            var pulseSeconds = effect.IsLethal ? LethalPulseSeconds : PulseSeconds;
            var strength = Math.Clamp(
                1f - (effect.AgeSeconds / pulseSeconds),
                0f,
                1f);
            if (_pulseByEntityId.TryGetValue(
                    effect.TargetEntityId,
                    out var recorded))
            {
                strength = MathF.Max(recorded, strength);
            }

            _pulseByEntityId[effect.TargetEntityId] = strength;
        }

        return new HitPulseLookup(_pulseByEntityId);
    }

    public void Clear()
    {
        Array.Clear(_effects, 0, _count);
        _count = 0;
        _agentsById.Clear();
        _deathEntityIds.Clear();
        _pulseByEntityId.Clear();
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

/// <summary>
/// A read-only view over the pulse strengths built by
/// <see cref="HitEffectSystem.BuildPulseLookup"/> for the current frame.
/// </summary>
/// <remarks>
/// This is a ref struct so that it can only live on the stack. It cannot be
/// stored in a field, boxed, or captured, which is what structurally
/// guarantees that a lookup never outlives the frame that built it. The view
/// borrows the system's storage rather than owning a copy, so it is valid only
/// until the next <see cref="HitEffectSystem.BuildPulseLookup"/> or
/// <see cref="HitEffectSystem.Clear"/> call.
/// </remarks>
internal readonly ref struct HitPulseLookup
{
    private readonly Dictionary<ulong, float>? _pulseByEntityId;

    internal HitPulseLookup(Dictionary<ulong, float> pulseByEntityId)
    {
        _pulseByEntityId = pulseByEntityId;
    }

    /// <summary>
    /// Returns the same value <see cref="HitEffectSystem.GetPulseStrength"/>
    /// would return for <paramref name="entityId"/> against the effects the
    /// lookup was built from, in one hashed read rather than a full scan.
    /// </summary>
    public float GetPulseStrength(ulong entityId) =>
        _pulseByEntityId is not null &&
        _pulseByEntityId.TryGetValue(entityId, out var strength)
            ? strength
            : 0f;
}
