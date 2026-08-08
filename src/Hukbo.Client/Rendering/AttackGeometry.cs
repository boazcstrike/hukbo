using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Rendering;

/// <summary>
/// One allocation-free sample of a weapon-family curve in target-local space.
/// Forward is the local x axis and the attacker's right is the local y axis.
/// All values are provisional Client choreography and have no combat authority.
/// </summary>
internal readonly record struct AttackGeometrySample(
    float WeaponAngleRadians,
    float WeaponReach,
    float WeaponLateralOffset,
    float TrailAngleRadians,
    float TrailStrength,
    float TorsoForwardOffset,
    float TorsoLateralOffset,
    float TorsoRotationRadians,
    float StanceWeight);

/// <summary>
/// Pure target-local curve evaluation for authoritative contact animations.
/// World-facing resolution is deliberately left to <see cref="AttackPoseResolver"/>.
/// </summary>
internal static class AttackGeometry
{
    public static AttackGeometrySample Evaluate(AttackAnimation animation)
    {
        var profile = animation.MotionProfile;
        var comboSide = ResolveComboSide(animation.ComboPosition);
        var outcome = ResolveOutcome(animation.Resolution, profile.RecoilStrength);
        var recovery = EaseWeightedRecovery(animation.RecoveryProgress);

        // A legal paired loadout restrains the weapon-side motion so the block
        // stays in the exchange. An illegal one resolves to no overlay at all
        // and evaluates exactly as the solo stance does.
        var overlay = AttackMotionCatalog.ResolveShieldOverlay(
            animation.Weapon,
            animation.AttackerShield);
        var lateralScale = overlay?.LateralScale ?? 1f;
        var rotationScale = overlay?.TorsoRotationScale ?? 1f;
        var extensionScale = overlay?.ExtensionScale ?? 1f;

        var contactAngle =
            profile.ArcRadians * profile.ContactAngleShare * comboSide *
            outcome.AngleMultiplier;
        var contactReach = MathF.Max(
            0.35f,
            profile.VisualExtensionEnvelope * outcome.ReachMultiplier *
            extensionScale);
        var contactLateral =
            profile.LateralBias * comboSide * outcome.LateralMultiplier *
            lateralScale;
        var contactTrailAngle = contactAngle -
            (profile.ArcRadians * profile.TrailLagShare * comboSide);
        var contactTorsoForward =
            0.18f +
            ((profile.VisualExtensionEnvelope - 1f) * 1.4f) +
            (profile.RecoilStrength * 0.08f);
        var policy = ResolveMotionPolicy(animation.MotionIntensity);
        var contactTorsoLateral = -contactLateral * 0.34f;
        var contactTorsoRotation = -contactAngle * 0.28f * rotationScale;

        return new AttackGeometrySample(
            WeaponAngleRadians: Lerp(contactAngle, 0f, recovery),
            WeaponReach: Lerp(contactReach, 1f, recovery),
            WeaponLateralOffset: Lerp(contactLateral, 0f, recovery),
            TrailAngleRadians: Lerp(contactTrailAngle, 0f, recovery),
            TrailStrength: profile.TrailEligible
                ? EaseTrailFade(animation.RecoveryProgress) * policy.TrailScale
                : 0f,
            TorsoForwardOffset:
                Lerp(contactTorsoForward, 0f, recovery) * policy.BodyScale,
            TorsoLateralOffset:
                Lerp(contactTorsoLateral, 0f, recovery) * policy.BodyScale,
            TorsoRotationRadians:
                Lerp(contactTorsoRotation, 0f, recovery) * policy.BodyScale,
            StanceWeight: 1f - recovery);
    }

    /// <summary>
    /// How much of the nonessential motion the spectator's accessibility
    /// setting keeps (design section 10). Direction, contact, reach, and
    /// outcome are deliberately not scaled by any of the three: a reduced or
    /// disabled setting may never hide whether a blow landed, was blocked, was
    /// parried, was deflected, or missed. What it scales is the body
    /// exaggeration and the trail, which carry no combat meaning of their own.
    /// </summary>
    internal static MotionPolicy ResolveMotionPolicy(MotionIntensity intensity) =>
        intensity switch
        {
            MotionIntensity.Full => new MotionPolicy(1f, 1f),
            MotionIntensity.Reduced => new MotionPolicy(0.65f, 0.55f),
            MotionIntensity.Off => new MotionPolicy(0.35f, 0f),
            _ => throw new ArgumentOutOfRangeException(
                nameof(intensity),
                intensity,
                null),
        };

    /// <param name="BodyScale">
    /// How much of the torso lean and counter-rotation survives.
    /// </param>
    /// <param name="TrailScale">
    /// How much of the trail survives. Zero at
    /// <see cref="MotionIntensity.Off"/>, where the afterimage is removed
    /// entirely.
    /// </param>
    internal readonly record struct MotionPolicy(
        float BodyScale,
        float TrailScale);

    /// <summary>
    /// Smoothstep recovery keeps zero slope at contact and readiness, avoiding
    /// a mechanical velocity discontinuity at either phase boundary.
    /// </summary>
    internal static float EaseWeightedRecovery(float progress)
    {
        var clamped = Math.Clamp(progress, 0f, 1f);
        return clamped * clamped * (3f - (2f * clamped));
    }

    /// <summary>
    /// Cubic decay preserves the event-baked contact edge, then removes the
    /// trail rapidly enough that readiness has no residual afterimage.
    /// </summary>
    internal static float EaseTrailFade(float progress)
    {
        var remaining = 1f - Math.Clamp(progress, 0f, 1f);
        return remaining * remaining * remaining;
    }

    private static float ResolveComboSide(int? comboPosition) =>
        comboPosition is { } position && (position & 1) == 0 ? -1f : 1f;

    private static OutcomeShape ResolveOutcome(
        AttackResolution resolution,
        float recoilStrength) =>
        resolution switch
        {
            AttackResolution.Landed => new OutcomeShape(1f, 1f, 1f),
            AttackResolution.ShieldBlocked => new OutcomeShape(
                -0.35f,
                1f - (recoilStrength * 0.22f),
                0.65f),
            AttackResolution.Parried => new OutcomeShape(
                -0.60f,
                1f - (recoilStrength * 0.14f),
                1.35f),
            AttackResolution.Deflected => new OutcomeShape(
                0.55f,
                1f + (recoilStrength * 0.04f),
                1.10f),
            AttackResolution.Evaded => new OutcomeShape(
                1.20f,
                1f + (recoilStrength * 0.12f),
                1.15f),
            _ => throw new ArgumentOutOfRangeException(
                nameof(resolution),
                resolution,
                null),
        };

    private static float Lerp(float from, float to, float amount) =>
        from + ((to - from) * amount);

    private readonly record struct OutcomeShape(
        float AngleMultiplier,
        float ReachMultiplier,
        float LateralMultiplier);
}
