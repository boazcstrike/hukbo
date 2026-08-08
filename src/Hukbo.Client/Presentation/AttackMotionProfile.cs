namespace Hukbo.Client.Presentation;

/// <summary>
/// Immutable, presentation-only parameters for one procedural weapon-motion
/// family. Every value is provisional choreography and has no simulation or
/// historical-measurement authority.
/// </summary>
/// <param name="Family">The procedural family this profile evaluates.</param>
/// <param name="VisualExtensionEnvelope">
/// Maximum visual extension as a ratio of the neutral drawn weapon vector.
/// This is not combat reach and must never be copied into Core weapon data.
/// </param>
/// <param name="ArcRadians">Maximum visible angular travel, in radians.</param>
/// <param name="LateralBias">
/// Signed target-local side bias used by the procedural pose.
/// </param>
/// <param name="RecoilStrength">Normalized visual recoil strength.</param>
/// <param name="RecoverySeconds">Presentation seconds used by recovery.</param>
/// <param name="HandCount">Number of hands visibly committed to the weapon.</param>
/// <param name="TrailEligible">
/// Whether visible detail tiers may draw the family's bounded trail or
/// afterimage.
/// </param>
/// <param name="ShieldCompatible">
/// Whether the weapon family permits the existing shield-paired pose overlay.
/// </param>
/// <param name="ContactAngleShare">
/// How much of <paramref name="ArcRadians"/> the weapon has already travelled
/// at the contact instant. A family that leads with the edge commits a large
/// share; a head-weighted family commits a small one, so its mass reads as
/// arriving late.
/// </param>
/// <param name="TrailLagShare">
/// How far behind the weapon the trailing end of the arc sits, as a share of
/// <paramref name="ArcRadians"/>. A dense, short trail uses a small share; a
/// long sweep uses a large one.
/// </param>
internal readonly record struct AttackMotionProfile(
    AttackMotionFamily Family,
    float VisualExtensionEnvelope,
    float ArcRadians,
    float LateralBias,
    float RecoilStrength,
    float RecoverySeconds,
    int HandCount,
    bool TrailEligible,
    bool ShieldCompatible,
    float ContactAngleShare,
    float TrailLagShare);

/// <summary>
/// The presentation-only adjustment a legal shield-paired loadout applies on
/// top of its weapon's own family. It is an overlay, never a family of its
/// own: design section 7 keeps shielded Kalis and shielded Itak inside the
/// <see cref="AttackMotionFamily.LinearThrustCut"/> and
/// <see cref="AttackMotionFamily.CompactChopSlash"/> families they already
/// belong to.
/// </summary>
/// <remarks>
/// Every scale is a fraction of the solo motion, so a paired stance can only
/// restrain the weapon-side motion — keeping the block between the defender
/// and the weapon line — and never exaggerate it. All three values are
/// PROVISIONAL presentation choreography.
/// </remarks>
/// <param name="LateralScale">
/// How much of the solo lateral travel the paired stance keeps.
/// </param>
/// <param name="TorsoRotationScale">
/// How much of the solo body rotation the paired stance keeps.
/// </param>
/// <param name="ExtensionScale">
/// How much of the solo reach the paired stance keeps.
/// </param>
internal readonly record struct ShieldMotionOverlay(
    float LateralScale,
    float TorsoRotationScale,
    float ExtensionScale);
