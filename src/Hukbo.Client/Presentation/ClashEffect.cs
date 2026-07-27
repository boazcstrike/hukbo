using Hukbo.Core.Combat;

namespace Hukbo.Client.Presentation;

/// <summary>
/// A small bright cross where two weapons, or a weapon and a shield, met.
/// Presentation only. It fires for the three contact resolutions and for
/// neither a landed blow nor a void: for a void the absence is the signal.
/// </summary>
/// <param name="Sequence">The sequence of the attack event that caused it.</param>
/// <param name="AttackerEntityId">The attacking warrior.</param>
/// <param name="TargetEntityId">The defending warrior.</param>
/// <param name="XRaw">Contact midpoint, raw fixed-point x.</param>
/// <param name="YRaw">Contact midpoint, raw fixed-point y.</param>
/// <param name="Resolution">Which defensive surface took the blow.</param>
/// <param name="AgeSeconds">Presentation seconds elapsed.</param>
internal readonly record struct ClashEffect(
    long Sequence,
    ulong AttackerEntityId,
    ulong TargetEntityId,
    int XRaw,
    int YRaw,
    AttackResolution Resolution,
    float AgeSeconds)
{
    /// <summary>PROVISIONAL. Short enough to read as an impact.</summary>
    public float LifetimeSeconds => 0.14f;

    /// <summary>
    /// Whether a resolution puts a cross on screen. Declared here so the
    /// system, the geometry, and any later reader share one statement of the
    /// rule rather than three lists of enum members that can drift apart.
    /// </summary>
    public static bool FiresFor(AttackResolution resolution) =>
        resolution is AttackResolution.ShieldBlocked or
            AttackResolution.Parried or
            AttackResolution.Deflected;
}
