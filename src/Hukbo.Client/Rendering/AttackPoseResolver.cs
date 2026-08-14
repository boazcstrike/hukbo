using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Render-facing offsets for one active attack. All positions are relative to
/// the authoritative pawn position and use presentation units only.
/// </summary>
internal readonly record struct AttackPose(
    AttackAnimationPhase Phase,
    Vector2 TorsoOffset,
    float TorsoRotationRadians,
    float StanceWeight,
    Vector2 WeaponHand,
    bool HasSupportHand,
    Vector2 WeaponTip,
    float TrailStrength,
    bool HasShield,
    AttackResolution Resolution,
    bool IsLethal);

/// <summary>
/// Transforms target-local attack geometry into a true 360-degree world-space
/// pose. This is pure static math over value types and allocates nothing.
/// </summary>
internal static class AttackPoseResolver
{
    public static AttackPose Resolve(AttackAnimation animation)
    {
        if (!float.IsFinite(animation.DirectionX))
        {
            throw new ArgumentOutOfRangeException(nameof(animation));
        }

        if (!float.IsFinite(animation.DirectionY))
        {
            throw new ArgumentOutOfRangeException(nameof(animation));
        }

        // atan2 normalizes the heading independently of the supplied vector's
        // magnitude. A coincident attacker and target deterministically keep
        // the neutral +x heading instead of producing a zero or NaN basis.
        var heading = MathF.Atan2(animation.DirectionY, animation.DirectionX);
        var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var right = new Vector2(-forward.Y, forward.X);
        var geometry = AttackGeometry.Evaluate(animation);

        var torsoOffset = ToWorld(
            geometry.TorsoForwardOffset,
            geometry.TorsoLateralOffset,
            forward,
            right);
        var weaponHand = torsoOffset + ToWorld(
            forwardOffset: 0.22f,
            lateralOffset: 0.30f +
                (geometry.WeaponLateralOffset * 0.18f),
            forward,
            right);
        var weaponDirection = ToWorld(
            MathF.Cos(geometry.WeaponAngleRadians),
            MathF.Sin(geometry.WeaponAngleRadians),
            forward,
            right);
        var weaponTip = weaponHand +
            (weaponDirection * geometry.WeaponReach);

        var hasSupportHand = animation.MotionProfile.HandCount == 2;
        var hasShield = ResolveShieldOverlay(
            animation.AttackerShield,
            animation.MotionProfile.ShieldCompatible);

        return new AttackPose(
            animation.Phase,
            torsoOffset,
            geometry.TorsoRotationRadians,
            geometry.StanceWeight,
            weaponHand,
            hasSupportHand,
            weaponTip,
            geometry.TrailStrength,
            hasShield,
            animation.Resolution,
            animation.IsLethal);
    }

    private static bool ResolveShieldOverlay(
        ShieldId shield,
        bool shieldCompatible) =>
        shield switch
        {
            ShieldId.None => false,
            ShieldId.TallHardwood => shieldCompatible,
            _ => throw new ArgumentOutOfRangeException(
                nameof(shield),
                shield,
                null),
        };

    private static Vector2 ToWorld(
        float forwardOffset,
        float lateralOffset,
        Vector2 forward,
        Vector2 right) =>
        (forward * forwardOffset) + (right * lateralOffset);
}
