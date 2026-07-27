using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Holds at most one in-flight swing per agent, in a fixed-capacity array
/// sized at construction. An agent cannot accumulate swings and the array
/// cannot grow.
/// </summary>
/// <remarks>
/// The swing clock advances on speed-scaled presentation seconds, unlike
/// <see cref="HitEffectSystem"/> and <see cref="BloodEffectSystem"/>, which
/// advance on unscaled presentation time. Without the scaling a battle running
/// at 4x shows every warrior permanently mid-swing, because the simulation
/// issues attacks four times as often while the animation still takes one
/// unscaled cooldown period to complete.
/// </remarks>
internal sealed class SwingAnimationSystem
{
    private readonly Dictionary<ulong, AgentView> _agentsById = [];
    private readonly SwingAnimation[] _swings;
    private int _count;

    public SwingAnimationSystem(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _swings = new SwingAnimation[capacity];
    }

    public ReadOnlySpan<SwingAnimation> ActiveSwings => _swings.AsSpan(0, _count);

    /// <summary>
    /// Starts a swing for every attack event whose attacker and target are
    /// both present in the supplied views, replacing any swing already in
    /// flight for that attacker.
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
                !_agentsById.TryGetValue(
                    battleEvent.SourceEntityId,
                    out var attacker) ||
                !_agentsById.TryGetValue(targetEntityId, out var victim))
            {
                continue;
            }

            var direction = ResolveDirection(attacker, victim);
            Upsert(
                new SwingAnimation(
                    battleEvent.Sequence,
                    battleEvent.SourceEntityId,
                    targetEntityId,
                    direction.X,
                    direction.Y,
                    resolution,
                    AgeSeconds: 0f));
        }
    }

    /// <summary>
    /// Advances every in-flight swing by speed-scaled presentation seconds and
    /// drops the ones that have run their full duration.
    /// </summary>
    public void Advance(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        var writeIndex = 0;
        for (var readIndex = 0; readIndex < _count; readIndex++)
        {
            var advanced = _swings[readIndex] with
            {
                AgeSeconds = _swings[readIndex].AgeSeconds + elapsedSeconds,
            };
            if (advanced.AgeSeconds >= SwingAnimation.TotalSeconds)
            {
                continue;
            }

            _swings[writeIndex] = advanced;
            writeIndex++;
        }

        Array.Clear(_swings, writeIndex, _count - writeIndex);
        _count = writeIndex;
    }

    /// <summary>
    /// Fetches the swing in flight for one agent, if any.
    /// </summary>
    public bool TryGetSwing(ulong entityId, out SwingAnimation swing)
    {
        for (var index = 0; index < _count; index++)
        {
            if (_swings[index].AttackerEntityId != entityId)
            {
                continue;
            }

            swing = _swings[index];
            return true;
        }

        swing = default;
        return false;
    }

    public void Clear()
    {
        Array.Clear(_swings, 0, _count);
        _count = 0;
        _agentsById.Clear();
    }

    /// <summary>
    /// The unit vector from the attacker toward the victim, following the
    /// <c>BloodEffectSystem.ResolveDirection</c> precedent. Two agents sharing
    /// a position give the zero vector, which the geometry reads as a swing
    /// with no facing rather than as a swing toward the origin.
    /// </summary>
    private static (float X, float Y) ResolveDirection(
        AgentView attacker,
        AgentView victim)
    {
        var deltaX = (float)(victim.XRaw - attacker.XRaw);
        var deltaY = (float)(victim.YRaw - attacker.YRaw);
        var length = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        return length > 0f ? (deltaX / length, deltaY / length) : (0f, 0f);
    }

    /// <summary>
    /// One slot per agent, newest wins. An attacker already holding a swing
    /// has it overwritten in place, so the store cannot grow with repeated
    /// attacks from one warrior. A full store replaces the oldest swing,
    /// breaking an age tie on the lowest sequence, which is the eviction rule
    /// <see cref="HitEffectSystem"/> already uses.
    /// </summary>
    private void Upsert(SwingAnimation swing)
    {
        for (var index = 0; index < _count; index++)
        {
            if (_swings[index].AttackerEntityId != swing.AttackerEntityId)
            {
                continue;
            }

            _swings[index] = swing;
            return;
        }

        if (_count < _swings.Length)
        {
            _swings[_count] = swing;
            _count++;
            return;
        }

        var replacementIndex = 0;
        for (var index = 1; index < _count; index++)
        {
            var candidate = _swings[index];
            var current = _swings[replacementIndex];
            if (candidate.AgeSeconds > current.AgeSeconds ||
                (candidate.AgeSeconds == current.AgeSeconds &&
                 candidate.Sequence < current.Sequence))
            {
                replacementIndex = index;
            }
        }

        _swings[replacementIndex] = swing;
    }
}
