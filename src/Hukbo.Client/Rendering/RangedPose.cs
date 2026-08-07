using Hukbo.Core.Simulation;

namespace Hukbo.Client.Rendering;

/// <summary>
/// The pose one ranged weapon puts a pawn in at one moment of its five-phase
/// draw cycle (<see cref="RangedPhase"/>). A neutral pose, which is
/// <c>default</c>, is a pawn standing exactly as it does today: no weapon
/// rotation, no reach change, no lean, no drawn tension — mirroring
/// <c>SwingPose</c>'s and <c>GaitPose</c>'s own zero-is-neutral defaults.
/// </summary>
/// <remarks>
/// <see cref="WeaponAngleRadians"/> and <see cref="ExtensionRatio"/> are named
/// and scaled identically to the same two fields on <c>SwingPose</c>
/// deliberately: design section 8.3 of
/// <c>docs/plans/2026-08-07-ranged-units-design.md</c> is explicit that both
/// poses write "the same two channels... into the same <c>ApplySwing</c>
/// call", because there is only one weapon line per pawn and a ranged pose
/// suppresses the swing pose for that pawn on that frame rather than the two
/// being summed.
/// </remarks>
/// <param name="Phase">Which phase of the draw cycle the pawn is in.</param>
/// <param name="WeaponAngleRadians">
/// Rotation of the weapon line about the grip. Zero is the neutral carried
/// orientation; the sign and magnitude convention matches
/// <c>SwingPose.WeaponAngleRadians</c>.
/// </param>
/// <param name="ExtensionRatio">
/// How far along the reach the weapon line currently sits, in the same units
/// as <c>SwingPose.ExtensionRatio</c>: zero is the neutral, undrawn length; a
/// negative value retracts the line toward the grip, and a value at or below
/// roughly <c>-1</c> is the Bangkaw's spent-shaft "vanished" keyframe.
/// </param>
/// <param name="TorsoLeanX">
/// Torso offset in pawn units, x component. Negative leans away from the
/// target — the Bangkaw's cocked-back <see cref="RangedPhase.Draw"/> keyframe
/// — positive leans toward it.
/// </param>
/// <param name="TorsoLeanY">Torso offset in pawn units, y component.</param>
/// <param name="DrawTension">
/// How taut a held draw currently is, zero to one. Meaningful only for the
/// Busog, whose <see cref="RangedPhase.Draw"/> rises through this channel,
/// whose <see cref="RangedPhase.Ready"/> holds it at its peak, and whose
/// <see cref="RangedPhase.Release"/> snaps it back to zero. Always zero for a
/// weapon with no held-tension channel.
/// </param>
internal readonly record struct RangedPose(
    RangedPhase Phase,
    float WeaponAngleRadians,
    float ExtensionRatio,
    float TorsoLeanX,
    float TorsoLeanY,
    float DrawTension);
