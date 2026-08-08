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
    Vector2 Forward,
    Vector2 Right,
    Vector2 TorsoOffset,
    float TorsoRotationRadians,
    float StanceWeight,
    Vector2 WeaponHand,
    Vector2 SupportHand,
    bool HasSupportHand,
    Vector2 WeaponTip,
    Vector2 TrailStart,
    Vector2 TrailEnd,
    float TrailStrength,
    Vector2 ShieldHand,
    bool HasShield);

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

        var trailDirection = ToWorld(
            MathF.Cos(geometry.TrailAngleRadians),
            MathF.Sin(geometry.TrailAngleRadians),
            forward,
            right);
        var trailStart = weaponHand +
            (trailDirection * geometry.WeaponReach * 0.88f);

        var hasSupportHand = animation.MotionProfile.HandCount == 2;
        var supportHand = torsoOffset + ToWorld(
            forwardOffset: -0.04f,
            lateralOffset: 0.04f +
                (geometry.WeaponLateralOffset * 0.08f),
            forward,
            right);
        var hasShield = ResolveShieldOverlay(
            animation.AttackerShield,
            animation.MotionProfile.ShieldCompatible);
        var shieldHand = torsoOffset + ToWorld(
            forwardOffset: 0.16f,
            lateralOffset: -0.58f,
            forward,
            right);

        return new AttackPose(
            animation.Phase,
            forward,
            right,
            torsoOffset,
            geometry.TorsoRotationRadians,
            geometry.StanceWeight,
            weaponHand,
            supportHand,
            hasSupportHand,
            weaponTip,
            trailStart,
            weaponTip,
            geometry.TrailStrength,
            shieldHand,
            hasShield);
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
