using Hukbo.Client.Settings;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Fixed-capacity attack timelines with at most one active animation per
/// attacker. Incoming combo contacts replace recovery with a fresh baked
/// contact; they never restart at anticipation.
/// </summary>
internal sealed class AttackAnimationSystem
{
    private readonly AttackAnimation[] _animations;
    private int _count;

    public AttackAnimationSystem(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _animations = new AttackAnimation[capacity];
    }

    public ReadOnlySpan<AttackAnimation> ActiveAnimations =>
        _animations.AsSpan(0, _count);

    /// <summary>
    /// Installs one authoritative contact at age zero. Direction is supplied by
    /// the Client's post-tick attacker and defender views and retained exactly
    /// for target-local geometry to consume later.
    /// </summary>
    public void Ingest(
        AttackContactBundle contact,
        float directionX,
        float directionY,
        MotionIntensity motionIntensity)
    {
        if (!float.IsFinite(directionX))
        {
            throw new ArgumentOutOfRangeException(nameof(directionX));
        }

        if (!float.IsFinite(directionY))
        {
            throw new ArgumentOutOfRangeException(nameof(directionY));
        }

        if (!Enum.IsDefined(motionIntensity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(motionIntensity),
                motionIntensity,
                null);
        }

        var animation = new AttackAnimation(
            contact.Sequence,
            contact.Tick,
            contact.AttackerEntityId,
            contact.DefenderEntityId,
            contact.Damage,
            contact.FactionId,
            contact.Weapon,
            contact.AttackerShield,
            contact.HitLocation,
            contact.Resolution,
            contact.ComboPosition,
            contact.IsLethal,
            directionX,
            directionY,
            motionIntensity,
            AttackMotionCatalog.Resolve(contact.Weapon),
            AgeSeconds: 0f,
            AwaitingDrawAcknowledgement: true);
        Upsert(animation);
    }

    /// <summary>
    /// Advances acknowledged timelines by playback-scaled presentation time.
    /// A newly latched contact ignores any elapsed value until its actual draw
    /// acknowledges the matching sequence.
    /// </summary>
    public void Advance(float elapsedSeconds, float speedMultiplier = 1f)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        if (!float.IsFinite(speedMultiplier) || speedMultiplier <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        }

        var scaledSeconds = elapsedSeconds * speedMultiplier;
        if (!float.IsFinite(scaledSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        for (var index = 0; index < _count; index++)
        {
            var animation = _animations[index];
            if (animation.AwaitingDrawAcknowledgement)
            {
                continue;
            }

            _animations[index] = animation with
            {
                AgeSeconds = animation.AgeSeconds + scaledSeconds,
            };
        }
    }

    /// <summary>
    /// Releases a contact latch only when both attacker and event sequence
    /// match. This prevents a late acknowledgment from an earlier draw from
    /// consuming a combo contact that arrived for the same attacker.
    /// </summary>
    public bool AcknowledgeDraw(ulong attackerEntityId, long sequence)
    {
        for (var index = 0; index < _count; index++)
        {
            var animation = _animations[index];
            if (animation.AttackerEntityId != attackerEntityId ||
                animation.Sequence != sequence ||
                !animation.AwaitingDrawAcknowledgement)
            {
                continue;
            }

            _animations[index] = animation with
            {
                AwaitingDrawAcknowledgement = false,
            };
            return true;
        }

        return false;
    }

    public bool TryGetAnimation(
        ulong attackerEntityId,
        out AttackAnimation animation)
    {
        for (var index = 0; index < _count; index++)
        {
            if (_animations[index].AttackerEntityId != attackerEntityId)
            {
                continue;
            }

            animation = _animations[index];
            return true;
        }

        animation = default;
        return false;
    }

    public void Clear()
    {
        Array.Clear(_animations, 0, _count);
        _count = 0;
    }

    private void Upsert(AttackAnimation animation)
    {
        for (var index = 0; index < _count; index++)
        {
            if (_animations[index].AttackerEntityId !=
                animation.AttackerEntityId)
            {
                continue;
            }

            _animations[index] = animation;
            return;
        }

        if (_count < _animations.Length)
        {
            _animations[_count] = animation;
            _count++;
            return;
        }

        // A scenario/caller capacity mismatch cannot grow this store. Retain
        // the newest contact by replacing the oldest tick, then sequence.
        var replacementIndex = 0;
        for (var index = 1; index < _count; index++)
        {
            var candidate = _animations[index];
            var current = _animations[replacementIndex];
            if (candidate.Tick < current.Tick ||
                (candidate.Tick == current.Tick &&
                 candidate.Sequence < current.Sequence))
            {
                replacementIndex = index;
            }
        }

        _animations[replacementIndex] = animation;
    }
}
