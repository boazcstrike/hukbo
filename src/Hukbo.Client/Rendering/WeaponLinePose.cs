// Attack-animation-v2, task 11. What is left of the retired swing
// presentation: the two value types the pawn geometry still reads to place a
// weapon line. The systems that used to produce them —
// SwingAnimation/SwingAnimationSystem, SwingGeometry's phase evaluator, and
// SwingPoseResolver — are gone, replaced by the contact-latched attack rig in
// AttackAnimation, AttackAnimationSystem, AttackGeometry, and
// AttackPoseResolver. Nothing evaluates a swing timeline any more; these are
// pure geometry inputs, produced only by PawnGeometry's own conversion from
// an AttackPose.

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
/// Rotation of the weapon line about the grip, with the swing direction
/// already applied, so a warrior striking left rotates opposite one striking
/// right.
/// </param>
/// <param name="TorsoLeanX">
/// Torso offset in pawn units, x component. The lean runs along the swing
/// direction and is already multiplied by it here, because this record carries
/// no direction of its own for a caller to apply.
/// </param>
/// <param name="TorsoLeanY">Torso offset in pawn units, y component.</param>
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
