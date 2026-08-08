using Hukbo.Core.Combat;
using Hukbo.Core.Simulation;

namespace Hukbo.Client.Presentation;

/// <summary>
/// One presentation-only response at the defender's authoritative post-tick
/// position. It never changes simulation position, health, or intent.
/// </summary>
internal readonly record struct DefenderReaction(
    long Sequence,
    ulong AttackerEntityId,
    ulong DefenderEntityId,
    int XRaw,
    int YRaw,
    float DirectionX,
    float DirectionY,
    AttackResolution Resolution,
    bool IsLethal,
    float AgeSeconds)
{
    /// <summary>PROVISIONAL duration matching the attack timeline's hold.</summary>
    public const float LethalHoldSeconds = AttackAnimation.LethalHoldSeconds;

    public float LifetimeSeconds => IsLethal ? 0.28f : 0.18f;
}

/// <summary>
/// Fixed-capacity, at-most-one-per-defender contact reactions. A newer
/// contact replaces that defender's older response without growing storage.
/// </summary>
internal sealed class DefenderReactionSystem
{
    private readonly DefenderReaction[] _reactions;
    private int _count;

    public DefenderReactionSystem(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _reactions = new DefenderReaction[capacity];
    }

    public ReadOnlySpan<DefenderReaction> ActiveReactions =>
        _reactions.AsSpan(0, _count);

    public void StartContact(
        AttackContactBundle contact,
        AgentView attacker,
        AgentView defender)
    {
        var direction = ResolveDirection(attacker, defender);
        Upsert(
            new DefenderReaction(
                contact.Sequence,
                contact.AttackerEntityId,
                contact.DefenderEntityId,
                defender.XRaw,
                defender.YRaw,
                direction.X,
                direction.Y,
                contact.Resolution,
                contact.IsLethal,
                AgeSeconds: 0f));
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
            var advanced = _reactions[readIndex] with
            {
                AgeSeconds = _reactions[readIndex].AgeSeconds + elapsedSeconds,
            };
            if (advanced.AgeSeconds >= advanced.LifetimeSeconds)
            {
                continue;
            }

            _reactions[writeIndex] = advanced;
            writeIndex++;
        }

        Array.Clear(_reactions, writeIndex, _count - writeIndex);
        _count = writeIndex;
    }

    public bool TryGetReaction(
        ulong defenderEntityId,
        out DefenderReaction reaction)
    {
        for (var index = 0; index < _count; index++)
        {
            if (_reactions[index].DefenderEntityId != defenderEntityId)
            {
                continue;
            }

            reaction = _reactions[index];
            return true;
        }

        reaction = default;
        return false;
    }

    public bool IsLethalHoldActive(ulong defenderEntityId) =>
        TryGetReaction(defenderEntityId, out var reaction) &&
        reaction.IsLethal &&
        reaction.AgeSeconds < DefenderReaction.LethalHoldSeconds;

    public void Clear()
    {
        Array.Clear(_reactions, 0, _count);
        _count = 0;
    }

    private static (float X, float Y) ResolveDirection(
        AgentView attacker,
        AgentView defender)
    {
        var deltaX = (float)(defender.XRaw - attacker.XRaw);
        var deltaY = (float)(defender.YRaw - attacker.YRaw);
        var length = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        return length > 0f
            ? (deltaX / length, deltaY / length)
            : (0f, 0f);
    }

    private void Upsert(DefenderReaction reaction)
    {
        for (var index = 0; index < _count; index++)
        {
            if (_reactions[index].DefenderEntityId != reaction.DefenderEntityId)
            {
                continue;
            }

            _reactions[index] = reaction;
            return;
        }

        if (_count < _reactions.Length)
        {
            _reactions[_count] = reaction;
            _count++;
            return;
        }

        var replacementIndex = 0;
        for (var index = 1; index < _count; index++)
        {
            var candidate = _reactions[index];
            var current = _reactions[replacementIndex];
            if (candidate.AgeSeconds > current.AgeSeconds ||
                (candidate.AgeSeconds == current.AgeSeconds &&
                 candidate.Sequence < current.Sequence))
            {
                replacementIndex = index;
            }
        }

        _reactions[replacementIndex] = reaction;
    }
}
