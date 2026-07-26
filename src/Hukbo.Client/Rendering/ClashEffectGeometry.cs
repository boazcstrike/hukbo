using Hukbo.Client.Presentation;

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
/// <b>No-op stub.</b> It reports an empty layout, so nothing is drawn.
/// </remarks>
internal static class ClashEffectGeometry
{
    public static ClashEffectLayout Create(ClashEffect effect, float cameraZoom)
    {
        if (!float.IsFinite(cameraZoom) || cameraZoom < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cameraZoom));
        }

        return default;
    }
}
