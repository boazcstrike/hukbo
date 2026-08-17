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
        ShieldCompatible: false,
        ContactAngleShare: 0.42f,
        TrailLagShare: 0.26f);

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
        ShieldCompatible: false,

        // The head-led signature: the smallest share of the arc spent by the
        // contact instant, so the mass arrives late, with a short dense trail
        // behind it rather than a long sweep.
        ContactAngleShare: 0.24f,
        TrailLagShare: 0.15f);

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
        ShieldCompatible: true,
        ContactAngleShare: 0.38f,
        TrailLagShare: 0.30f);

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
        ShieldCompatible: true,
        ContactAngleShare: 0.40f,
        TrailLagShare: 0.24f);

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
        ShieldCompatible: true,
        // A hurl releases the shaft at full commitment, so almost the whole
        // arc is spent by the contact instant; there is no trail, so the lag
        // share is inert and stated as zero rather than invented.
        ContactAngleShare: 0.85f,
        TrailLagShare: 0f);

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
        ShieldCompatible: false,
        // A bowstring release is over the instant it starts: the whole of the
        // small arc is spent at contact. No trail, so no lag.
        ContactAngleShare: 0.95f,
        TrailLagShare: 0f);

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
        ShieldCompatible: false,
        // A discharge is not a swing at all. The barrel is already level when
        // the shot leaves it, so the whole of the token arc is spent at
        // contact and the recoil is what moves afterwards. No trail, no lag.
        ContactAngleShare: 1f,
        TrailLagShare: 0f);

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

    /// <summary>
    /// The paired-stance overlay a loadout is entitled to, or <c>null</c> when
    /// it is not. A solo warrior has none, and a two-handed family has none
    /// even while carrying a shield identity — the profile's own
    /// <see cref="AttackMotionProfile.ShieldCompatible"/> is what decides, so
    /// an illegal pairing is suppressed here rather than being drawn and
    /// looking like a fifth weapon family.
    /// </summary>
    /// <remarks>
    /// Unknown weapon and shield identities still fail explicitly, for the
    /// same reason <see cref="Resolve"/> does: a new Core loadout must not
    /// silently inherit another one's presentation.
    /// </remarks>
    public static ShieldMotionOverlay? ResolveShieldOverlay(
        WeaponId weapon,
        ShieldId shield)
    {
        var profile = Resolve(weapon);

        return shield switch
        {
            ShieldId.None => null,
            ShieldId.TallHardwood => profile.ShieldCompatible
                ? PairedStance
                : null,
            // T4: the narrow shield takes the same paired-stance overlay
            // path as TallHardwood — it is a real shield for motion-catalog
            // purposes, not a variant of solo.
            ShieldId.NarrowBreastHigh => profile.ShieldCompatible
                ? PairedStance
                : null,
            _ => throw new ArgumentOutOfRangeException(nameof(shield), shield, null),
        };
    }

    // PROVISIONAL paired-stance choreography. The block stays between the
    // defender and the weapon line, so the paired stance keeps most of its
    // reach but restrains the lateral travel and the body rotation that would
    // swing the shield out of the exchange.
    private static readonly ShieldMotionOverlay PairedStance = new(
        LateralScale: 0.62f,
        TorsoRotationScale: 0.55f,
        ExtensionScale: 0.92f);
}
