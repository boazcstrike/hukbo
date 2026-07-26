using Hukbo.Client.Presentation;

namespace Hukbo.Client.Rendering;

/// <summary>
/// The four phases of one swing, in the order they are visited.
/// </summary>
internal enum SwingPhase
{
    /// <summary>The weapon pulls back outside the body silhouette.</summary>
    Anticipation = 0,

    /// <summary>The arc sweeps through.</summary>
    Strike = 1,

    /// <summary>Held at full extension; where the clash reads.</summary>
    ImpactHold = 2,

    /// <summary>Return to neutral.</summary>
    Recovery = 3,
}

/// <summary>
/// The pose one swing puts a pawn in at one moment. A neutral pose, which is
/// <c>default</c>, is a pawn standing as it does today.
/// </summary>
/// <param name="Phase">Which phase the swing is in.</param>
/// <param name="PhaseProgress">Progress through that phase, zero to one.</param>
/// <param name="WeaponAngleRadians">
/// Rotation of the weapon line about the grip.
/// </param>
/// <param name="TorsoLeanX">Torso offset along the swing direction.</param>
/// <param name="TorsoLeanY">Torso offset across the swing direction.</param>
/// <param name="ExtensionRatio">
/// How far along the reach the weapon tip has travelled. A landed blow stops
/// on the target, an evaded blow follows through past it, and the three
/// contact outcomes recoil.
/// </param>
/// <param name="TrailStrength">
/// Strength of the arc trail, zero when no trail is drawn.
/// </param>
internal readonly record struct SwingPose(
    SwingPhase Phase,
    float PhaseProgress,
    float WeaponAngleRadians,
    float TorsoLeanX,
    float TorsoLeanY,
    float ExtensionRatio,
    float TrailStrength);

/// <summary>
/// Pure mapping from one in-flight swing to a phase and a pose.
/// </summary>
/// <remarks>
/// <b>No-op stub.</b> It reports the first phase and a neutral pose whatever
/// the swing, which is the pre-change appearance of a pawn: a weapon line that
/// never moves.
/// </remarks>
internal static class SwingGeometry
{
    /// <summary>
    /// Resolves which phase a swing is in from its progress through the total
    /// duration.
    /// </summary>
    /// <param name="progress">Progress, zero to one.</param>
    public static SwingPhase ResolvePhase(float progress)
    {
        if (!float.IsFinite(progress) || progress < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(progress));
        }

        return SwingPhase.Anticipation;
    }

    /// <summary>
    /// Resolves the pose one swing puts a pawn in.
    /// </summary>
    public static SwingPose ResolvePose(SwingAnimation swing) => default;
}
