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

    /// <summary>
    /// How much further a lethal contact carries the same resolution's
    /// response. PROVISIONAL presentation choreography.
    /// </summary>
    private const float LethalReactionScale = 1.55f;

    /// <summary>
    /// PROVISIONAL legibility tuning (CLAUDE.md section 7). The lethal side
    /// must stay strictly greater than <see cref="LethalHoldSeconds"/>
    /// (0.34s), which must stay strictly greater than
    /// <c>HitEffectSystem.LethalPulseSeconds</c> (0.30s): 0.50 &gt; 0.34
    /// &gt; 0.30. If this reaction expired first, the hold in
    /// <see cref="DefenderReactionSystem.IsLethalHoldActive"/> would be
    /// silently capped by the reaction's own removal, and the pawn would
    /// vanish mid-pulse instead of holding through it. See
    /// the lethal blow legibility design.
    /// </summary>
    public float LifetimeSeconds => IsLethal ? 0.50f : 0.18f;

    /// <summary>
    /// The presentation-only displacement this defender is drawn at, in pawn
    /// units before apparent scale, in world axes. Largest at contact and
    /// exactly zero once <see cref="LifetimeSeconds"/> has elapsed, so the
    /// defender always returns to the authoritative position it never left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The five outcomes are separated the way design section 8 separates
    /// them, from the defender's side of the same contact: a landed blow
    /// drives the defender back along the attack line; a shield braces into
    /// it, which is the only outcome that travels toward the attacker; a parry
    /// redirects hard across the line; a deflection is the shallow version of
    /// the same redirection; and an evasion steps off the line altogether.
    /// </para>
    /// <para>
    /// Every magnitude here is PROVISIONAL presentation choreography. None of
    /// it is a measured historical quantity, and none of it reaches the
    /// simulation: this value is added at the draw anchor and discarded, never
    /// written back to a position, a target, or a hash.
    /// </para>
    /// </remarks>
    public (float X, float Y) ResolveOffset()
    {
        var decay = ResolveDecay();
        if (decay <= 0f)
        {
            return (0f, 0f);
        }

        var (along, lateral) = Resolution switch
        {
            AttackResolution.Landed => (0.85f, 0.10f),
            AttackResolution.ShieldBlocked => (-0.30f, 0.20f),
            AttackResolution.Parried => (0.18f, -0.72f),
            AttackResolution.Deflected => (0.34f, 0.40f),
            AttackResolution.Evaded => (0.12f, 0.66f),
            _ => throw new ArgumentOutOfRangeException(
                nameof(Resolution),
                Resolution,
                null),
        };

        var scale = decay * (IsLethal ? LethalReactionScale : 1f);
        var alongScaled = along * scale;
        var lateralScaled = lateral * scale;

        // The perpendicular of the attack direction, so the lateral channel
        // redirects across the line the blow arrived on rather than across
        // screen space.
        return (
            (DirectionX * alongScaled) - (DirectionY * lateralScaled),
            (DirectionY * alongScaled) + (DirectionX * lateralScaled));
    }

    /// <summary>
    /// Cubic ease-out from one at contact to exactly zero at expiry. Cubic
    /// rather than linear so the displacement reads as a struck body settling
    /// rather than sliding back at a constant rate.
    /// </summary>
    private float ResolveDecay()
    {
        var remaining = 1f - Math.Clamp(AgeSeconds / LifetimeSeconds, 0f, 1f);
        return remaining * remaining * remaining;
    }
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
