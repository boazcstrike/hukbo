using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Fixed-capacity pool of clash effects, copying the shape of
/// <see cref="HitEffectSystem"/>.
/// </summary>
/// <remarks>
/// It fires for the three contact resolutions only. A landed blow is already
/// announced by the impact ring and the blood, and a void draws nothing at all
/// because the absence is what a spectator reads.
/// </remarks>
internal sealed class ClashEffectSystem
{
    private readonly Dictionary<ulong, AgentView> _agentsById = [];
    private readonly ClashEffect[] _effects;
    private int _count;

    public ClashEffectSystem(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _effects = new ClashEffect[capacity];
    }

    public ReadOnlySpan<ClashEffect> ActiveEffects => _effects.AsSpan(0, _count);

    /// <summary>
    /// Starts the contact cross directly from an atomic bundle. Landed and
    /// evaded contacts intentionally add no clash effect.
    /// </summary>
    public void StartContact(
        AttackContactBundle contact,
        AgentView attacker,
        AgentView defender)
    {
        if (!ClashEffect.FiresFor(contact.Resolution))
        {
            return;
        }

        Add(
            new ClashEffect(
                contact.Sequence,
                contact.AttackerEntityId,
                contact.DefenderEntityId,
                (attacker.XRaw + defender.XRaw) / 2,
                (attacker.YRaw + defender.YRaw) / 2,
                contact.Resolution,
                AgeSeconds: 0f));
    }

    /// <summary>
    /// Places one effect at the contact midpoint for every attack event that
    /// resolved to a contact outcome and whose attacker and target are both
    /// present in the supplied views.
    /// </summary>
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

        for (var index = 0; index < events.Count; index++)
        {
            var battleEvent = events[index];
            if (battleEvent.Kind != BattleEventKind.Attack ||
                battleEvent.TargetEntityId is not { } targetEntityId ||
                battleEvent.Resolution is not { } resolution ||
                !ClashEffect.FiresFor(resolution) ||
                !_agentsById.TryGetValue(
                    battleEvent.SourceEntityId,
                    out var attacker) ||
                !_agentsById.TryGetValue(targetEntityId, out var victim))
            {
                continue;
            }

            Add(
                new ClashEffect(
                    battleEvent.Sequence,
                    battleEvent.SourceEntityId,
                    targetEntityId,
                    (attacker.XRaw + victim.XRaw) / 2,
                    (attacker.YRaw + victim.YRaw) / 2,
                    resolution,
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

    public void Clear()
    {
        Array.Clear(_effects, 0, _count);
        _count = 0;
        _agentsById.Clear();
    }

    /// <summary>
    /// Appends, or overwrites the oldest entry once the pool is full, breaking
    /// an age tie on the lowest sequence. A pool filled inside one tick has
    /// nothing but that tie-break to order its entries.
    /// </summary>
    private void Add(ClashEffect effect)
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
