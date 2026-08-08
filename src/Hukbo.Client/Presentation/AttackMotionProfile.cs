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
internal readonly record struct AttackMotionProfile(
    AttackMotionFamily Family,
    float VisualExtensionEnvelope,
    float ArcRadians,
    float LateralBias,
    float RecoilStrength,
    float RecoverySeconds,
    int HandCount,
    bool TrailEligible,
    bool ShieldCompatible);
