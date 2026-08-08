using Hukbo.Client.Settings;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Presentation phases after an authoritative attack has already reached
/// contact. There is deliberately no anticipation phase: Core's attack event
/// is the contact authority, not a request to begin a speculative wind-up.
/// </summary>
internal enum AttackAnimationPhase
{
    /// <summary>The event-baked contact waiting to contribute to a draw.</summary>
    Contact = 0,

    /// <summary>A short post-draw hold for a lethal contact.</summary>
    LethalHold = 1,

    /// <summary>The weapon and body ease away from committed contact.</summary>
    Recovery = 2,

    /// <summary>The settled, weapon-specific guarded pose.</summary>
    Readiness = 3,
}

/// <summary>
/// One bounded, presentation-only attack timeline. It preserves the complete
/// authoritative contact context while its age and motion policy remain
/// cosmetic and unhashed.
/// </summary>
internal readonly record struct AttackAnimation(
    long Sequence,
    long Tick,
    ulong AttackerEntityId,
    ulong DefenderEntityId,
    int Damage,
    int FactionId,
    WeaponId Weapon,
    ShieldId AttackerShield,
    BodyPart HitLocation,
    AttackResolution Resolution,
    int? ComboPosition,
    bool IsLethal,
    float DirectionX,
    float DirectionY,
    MotionIntensity MotionIntensity,
    AttackMotionProfile MotionProfile,
    float AgeSeconds,
    bool AwaitingDrawAcknowledgement)
{
    /// <summary>
    /// PROVISIONAL presentation time. Keeps a lethal defender available just
    /// beyond the guaranteed contact draw before ordinary recovery begins.
    /// </summary>
    public const float LethalHoldSeconds = 0.10f;

    public AttackAnimationPhase Phase
    {
        get
        {
            if (AwaitingDrawAcknowledgement)
            {
                return AttackAnimationPhase.Contact;
            }

            if (IsLethal && AgeSeconds < LethalHoldSeconds)
            {
                return AttackAnimationPhase.LethalHold;
            }

            return RecoveryAgeSeconds < MotionProfile.RecoverySeconds
                ? AttackAnimationPhase.Recovery
                : AttackAnimationPhase.Readiness;
        }
    }

    /// <summary>
    /// Smoothstep recovery progress from committed contact to the guarded ready
    /// pose. The value stays zero through contact and a lethal hold, reaches
    /// exactly one at readiness, and allocates nothing.
    /// </summary>
    public float RecoveryProgress
    {
        get
        {
            if (AwaitingDrawAcknowledgement)
            {
                return 0f;
            }

            var linear = Math.Clamp(
                RecoveryAgeSeconds / MotionProfile.RecoverySeconds,
                0f,
                1f);
            return linear * linear * (3f - (2f * linear));
        }
    }

    private float RecoveryAgeSeconds =>
        MathF.Max(0f, AgeSeconds - (IsLethal ? LethalHoldSeconds : 0f));
}
