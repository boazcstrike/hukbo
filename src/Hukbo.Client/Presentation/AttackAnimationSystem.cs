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

    /// <summary>
    /// PROVISIONAL presentation constant. The ceiling on how fast an attack
    /// timeline may be played, however fast the battle itself is being played.
    /// </summary>
    /// <remarks>
    /// Attack timelines are the only presentation system aged by playback-
    /// scaled time; blood, dust, clash crosses, hit effects, and defender
    /// reactions all age in unscaled real seconds. Without a ceiling, a 4x
    /// battle drew the Itak's 0.17-second recovery in 0.0425 seconds, which is
    /// between two and three frames at 60 Hz, and the swing could not be read
    /// as one action at all — the 4x half of the CL-7 smoke failure recorded
    /// on 2026-08-11 in docs/development/smoke-checklist.md. Two is chosen so
    /// that 1x and 2x playback stay exactly faithful to the simulation and
    /// only the two fastest speeds trade fidelity for legibility. Above the
    /// ceiling a timeline lags the tick that produced it, and a following
    /// contact from the same attacker replaces it through <c>Upsert</c>
    /// exactly as a combo contact already does.
    /// </remarks>
    internal const float MaximumAnimationSpeedMultiplier = 2f;

    public AttackAnimationSystem(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _animations = new AttackAnimation[capacity];
    }

    /// <summary>
    /// The playback multiplier an attack timeline is actually aged by, which
    /// is the requested one held down to
    /// <see cref="MaximumAnimationSpeedMultiplier"/>. Pure, so the ceiling is
    /// testable without a system, a graphics device, or a frame.
    /// </summary>
    internal static float ResolveAnimationSpeedMultiplier(
        float speedMultiplier) =>
        MathF.Min(speedMultiplier, MaximumAnimationSpeedMultiplier);

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
    /// Advances acknowledged timelines by playback-scaled presentation time,
    /// with the scale held down to
    /// <see cref="MaximumAnimationSpeedMultiplier"/> so a swing stays readable
    /// at the fastest playback speeds. A newly latched contact ignores any
    /// elapsed value until its actual draw acknowledges the matching sequence.
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

        var scaledSeconds =
            elapsedSeconds * ResolveAnimationSpeedMultiplier(speedMultiplier);
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
