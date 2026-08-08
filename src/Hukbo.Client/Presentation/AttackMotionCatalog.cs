using Hukbo.Core.Combat;

namespace Hukbo.Client.Presentation;

/// <summary>
/// Exhaustive mapping from stable weapon identity to immutable procedural
/// attack motion. The values tune Client presentation only; they never infer
/// or replace damage, reach, cadence, or any other Core combat property.
/// </summary>
internal static class AttackMotionCatalog
{
    // Documented, form uncertain physical class. The two-hand profile follows
    // Hukbo's current gameplay configuration only. Its exact choreography is a
    // Provisional reconstruction, not a historical technique claim.
    private static readonly AttackMotionProfile Kampilan = new(
        AttackMotionFamily.CommittedCleaver,
        VisualExtensionEnvelope: 1.15f,
        ArcRadians: 1.45f,
        LateralBias: 0.34f,
        RecoilStrength: 0.52f,
        RecoverySeconds: 0.30f,
        HandCount: 2,
        TrailEligible: true,
        ShieldCompatible: false);

    // Documented, form uncertain physical class. The two-hand, head-weighted
    // presentation is Hukbo gameplay choreography and a Provisional
    // reconstruction; it is not a measured period Wasay motion.
    private static readonly AttackMotionProfile Wasay = new(
        AttackMotionFamily.HeadWeightedChop,
        VisualExtensionEnvelope: 1.05f,
        ArcRadians: 1.10f,
        LateralBias: 0.18f,
        RecoilStrength: 0.72f,
        RecoverySeconds: 0.36f,
        HandCount: 2,
        TrailEligible: true,
        ShieldCompatible: false);

    // Documented name and weapon class; conservative form. The direct thrust
    // and recovery cut remain Provisional reconstruction and do not claim a
    // documented historical stance or technique.
    private static readonly AttackMotionProfile Kalis = new(
        AttackMotionFamily.LinearThrustCut,
        VisualExtensionEnvelope: 1.25f,
        ArcRadians: 0.35f,
        LateralBias: 0.10f,
        RecoilStrength: 0.34f,
        RecoverySeconds: 0.20f,
        HandCount: 1,
        TrailEligible: true,
        ShieldCompatible: true);

    // The Itak name/form pairing and all choreography are Provisional
    // reconstruction. This compact work-blade motion is a presentation choice,
    // not evidence of one standardized sixteenth-century fighting method.
    private static readonly AttackMotionProfile Itak = new(
        AttackMotionFamily.CompactChopSlash,
        VisualExtensionEnvelope: 1.00f,
        ArcRadians: 0.85f,
        LateralBias: 0.25f,
        RecoilStrength: 0.40f,
        RecoverySeconds: 0.17f,
        HandCount: 1,
        TrailEligible: true,
        ShieldCompatible: true);

    // Documented weapon class (Pigafetta, iron-tipped lances at Mactan) and
    // documented thrown role (Pigafetta, bamboo spears hurled at Mactan). The
    // one-hand overhand hurl choreography itself is a Provisional
    // reconstruction. Spear infantry pairing with a narrow shield is
    // consistent with the research doc's defensive-equipment notes, so the
    // free off-hand keeps ShieldCompatible true.
    private static readonly AttackMotionProfile Bangkaw = new(
        AttackMotionFamily.OverhandThrow,
        VisualExtensionEnvelope: 1.30f,
        ArcRadians: 0.95f,
        LateralBias: 0.15f,
        RecoilStrength: 0.45f,
        RecoverySeconds: 0.28f,
        HandCount: 1,
        TrailEligible: false,
        ShieldCompatible: true);

    // Documented weapon (Pigafetta, Mactan 1521; Legazpi's specimen shipment).
    // The draw-and-release choreography is a Provisional reconstruction. Both
    // hands are visibly committed to the bow and string, matching the
    // research doc's note that archers carry small or no shield.
    private static readonly AttackMotionProfile Busog = new(
        AttackMotionFamily.DrawAndRelease,
        VisualExtensionEnvelope: 1.20f,
        ArcRadians: 0.40f,
        LateralBias: 0.05f,
        RecoilStrength: 0.30f,
        RecoverySeconds: 0.22f,
        HandCount: 2,
        TrailEligible: false,
        ShieldCompatible: false);

    // Documented, form uncertain (Legazpi 1567; matchlocks attested c. 1543-67
    // per the WeaponId remarks). The braced, near-static level-and-discharge
    // choreography is a Provisional reconstruction; the strong recoil value
    // reflects a matchlock's visible kick, not a measured historical figure.
    // Both hands brace the stock, matching the research doc's note that
    // arquebusiers carry small or no shield.
    private static readonly AttackMotionProfile Arquebus = new(
        AttackMotionFamily.BracedDischarge,
        VisualExtensionEnvelope: 1.10f,
        ArcRadians: 0.15f,
        LateralBias: 0.00f,
        RecoilStrength: 0.85f,
        RecoverySeconds: 0.45f,
        HandCount: 2,
        TrailEligible: false,
        ShieldCompatible: false);

    /// <summary>
    /// Resolves the single motion profile declared for a weapon identity.
    /// Unknown identities fail explicitly so a new Core weapon cannot silently
    /// inherit unrelated choreography.
    /// </summary>
    public static AttackMotionProfile Resolve(WeaponId weapon) =>
        weapon switch
        {
            WeaponId.Kampilan => Kampilan,
            WeaponId.Wasay => Wasay,
            WeaponId.Kalis => Kalis,
            WeaponId.Itak => Itak,
            WeaponId.Bangkaw => Bangkaw,
            WeaponId.Busog => Busog,
            WeaponId.Arquebus => Arquebus,
            _ => throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null),
        };
}
