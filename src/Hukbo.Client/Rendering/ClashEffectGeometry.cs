using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Rendering;

/// <summary>
/// The screen-space shape of one clash cross at one moment.
/// </summary>
/// <param name="ApparentScale">Zoom-derived scale, clamped to its bounds.</param>
/// <param name="Progress">Progress through the effect lifetime, zero to one.</param>
/// <param name="Alpha">Opacity, one at the start and zero at the end.</param>
/// <param name="ArmLength">Half-length of each arm of the cross.</param>
/// <param name="ArmThickness">Thickness of each arm.</param>
internal readonly record struct ClashEffectLayout(
    float ApparentScale,
    float Progress,
    float Alpha,
    float ArmLength,
    float ArmThickness);

/// <summary>
/// Pure mapping from one clash effect and the camera zoom to its layout.
/// </summary>
/// <remarks>
/// The apparent scale is clamped by the same three constants
/// <see cref="PawnGeometry"/> and <see cref="HitEffectGeometry"/> use, so a
/// cross grows and stops growing with everything else pawn-sized rather than
/// becoming a full-screen flash at high zoom.
/// </remarks>
internal static class ClashEffectGeometry
{
    private const float MinimumApparentScale = 0.72f;
    private const float MaximumApparentScale = 2.40f;
    private const float ZoomScale = 1.35f;

    // PROVISIONAL. Arm half-length in pawn units at an apparent scale of one.
    // A shield takes the blow on a broad board and a deflection is the
    // glancing one of the three, so they read wider and smaller in turn.
    private const float ShieldArmLength = 4.4f;
    private const float ParryArmLength = 5.2f;
    private const float DeflectArmLength = 3.8f;

    /// <summary>PROVISIONAL. Share the cross expands over its lifetime.</summary>
    private const float ArmGrowth = 0.35f;

    /// <summary>PROVISIONAL. Arm thickness in pawn units.</summary>
    private const float ArmThickness = 1.1f;

    public static ClashEffectLayout Create(ClashEffect effect, float cameraZoom)
    {
        if (!float.IsFinite(cameraZoom) || cameraZoom < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cameraZoom));
        }

        var apparentScale = Math.Clamp(
            cameraZoom * ZoomScale,
            MinimumApparentScale,
            MaximumApparentScale);
        var progress = Math.Clamp(
            effect.AgeSeconds / effect.LifetimeSeconds,
            0f,
            1f);

        return new ClashEffectLayout(
            apparentScale,
            progress,
            Alpha: Math.Clamp(1f - progress, 0f, 1f),
            ArmLength: ResolveArmLength(effect.Resolution) *
                apparentScale *
                (1f + (progress * ArmGrowth)),
            ArmThickness: MathF.Max(1f, ArmThickness * apparentScale));
    }

    /// <summary>
    /// Rejects a resolution that draws no cross rather than returning a
    /// zero-length arm. Only the three contact outcomes reach the pool, so a
    /// landed blow or a void arriving here is a wiring defect and should say
    /// so instead of drawing nothing.
    /// </summary>
    private static float ResolveArmLength(AttackResolution resolution) =>
        resolution switch
        {
            AttackResolution.ShieldBlocked => ShieldArmLength,
            AttackResolution.Parried => ParryArmLength,
            AttackResolution.Deflected => DeflectArmLength,
            _ => throw new ArgumentOutOfRangeException(
                nameof(resolution),
                resolution,
                "Only a contact resolution draws a clash cross."),
        };
}
