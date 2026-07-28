using Hukbo.Client.Settings;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Pure sway math for battlefield grass clusters (battlefield-environment-
/// design.md, "Wind and motion", R-W5.1, R-W5.2, R-W5.4, R-W5.5). A single
/// small sine oscillator, evaluated per visible cluster per frame from a
/// client-side time accumulator
/// (<see cref="Hukbo.Client.Presentation.PresentationCoordinator"/>)
/// and the cluster's own generation-time
/// <see cref="GrassCluster.Phase"/> — never the simulation, never wall-clock
/// time. Sine over triangle is OD-W4-b's pinned implementation choice: the
/// design leaves either wave shape acceptable so long as the formula never
/// drifts silently, and this type plus <c>GrassSwayTests</c> is that pin.
/// Touches only value types, so it is fully unit tested with no graphics
/// device.
/// </summary>
internal static class GrassSway
{
    /// <summary>
    /// PROVISIONAL: oscillation frequency in Hertz, pinned below 1 Hz per
    /// R-W5.1 — grass should read as a slow, ambient lean rather than a
    /// flicker.
    /// </summary>
    internal const float SwayFrequencyHz = 0.35f;

    /// <summary>
    /// PROVISIONAL: the maximum magnitude, in screen pixels at camera zoom
    /// 1, the returned offset can ever reach (R-W5.2). Kept far below
    /// <see cref="GrassGeometry.GrassFreeBorderMargin"/> (a full ground
    /// cell, in world units, at every zoom) so a swayed tuft can never cross
    /// the map border — the border-clip step in the design's "Wind and
    /// motion" section holds by construction, with no clipping logic needed
    /// in this pure helper.
    /// </summary>
    internal const float SwayAmplitudePixels = 1.5f;

    /// <summary>
    /// PROVISIONAL: the vertical component's scale relative to the
    /// horizontal one. Kept strictly below 1 so the returned vector's
    /// magnitude never exceeds <see cref="SwayAmplitudePixels"/> — grass
    /// reads as leaning sideways with a slight vertical give rather than
    /// sliding in a straight line.
    /// </summary>
    private const float VerticalComponentFactor = 0.4f;

    /// <summary>
    /// PROVISIONAL: the amplitude factor <see cref="MotionIntensity.Reduced"/>
    /// resolves to in <see cref="ResolveAmplitudeFactor"/> — half of
    /// <see cref="MotionIntensity.Full"/>'s factor of 1 (R-W5.8).
    /// </summary>
    internal const float ReducedAmplitudeFactor = 0.5f;

    /// <summary>
    /// The sway offset, in screen pixels at camera zoom 1, for one cluster
    /// at <paramref name="timeSeconds"/> on the client's unscaled
    /// presentation clock. <paramref name="phase"/> is the cluster's own
    /// generation-time phase (R-W5.4), so two replays of the same seed
    /// produce identical per-cluster motion at the same wall time.
    /// <paramref name="amplitudeScale"/> is the caller-supplied
    /// <c>MotionIntensity</c> factor (0 at Off, 0.5 at Reduced, 1 at Full)
    /// or 0 for a suppressed/trampled or high-contrast-theme cluster.
    /// Exactly <see cref="Vector2.Zero"/> at <paramref name="amplitudeScale"/>
    /// 0, returned before any trigonometric evaluation, so the motion-off
    /// path is bit-identical to a static backdrop (R-W5.5). Pure and
    /// allocation free.
    /// </summary>
    public static Vector2 GrassSwayOffset(
        float timeSeconds,
        float phase,
        float amplitudeScale)
    {
        if (!float.IsFinite(timeSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(timeSeconds));
        }

        if (!float.IsFinite(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        if (!float.IsFinite(amplitudeScale) ||
            amplitudeScale < 0f ||
            amplitudeScale > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(amplitudeScale));
        }

        if (amplitudeScale == 0f)
        {
            return Vector2.Zero;
        }

        var angle = (MathF.Tau * SwayFrequencyHz * timeSeconds) + phase;
        var x = MathF.Sin(angle) * SwayAmplitudePixels * amplitudeScale;
        var y = MathF.Cos(angle) *
            SwayAmplitudePixels *
            VerticalComponentFactor *
            amplitudeScale;
        return new Vector2(x, y);
    }

    /// <summary>
    /// The single amplitude factor every grass-sway consumer feeds into
    /// <see cref="GrassSwayOffset"/>, combining every gate on sway into one
    /// pure function (battlefield-environment-design.md, "Wind and motion"
    /// and "Reduced-motion setting"; R-W5.6, R-W5.7, R-W5.8, R-W5.9). Any
    /// zeroing input wins over every other input, in this precedence:
    /// <paramref name="isHighContrastTheme"/> (R-W5.7),
    /// <paramref name="zoomBand"/> at <see cref="GrassZoomBand.Far"/>
    /// (R-W5.6), then <paramref name="isSuppressed"/> — a cluster inside a
    /// trample radius (R-W5.9, R-W4.7). Only once none of those fire does
    /// <paramref name="setting"/> decide: <see cref="MotionIntensity.Off"/>
    /// is 0, <see cref="MotionIntensity.Reduced"/> is
    /// <see cref="ReducedAmplitudeFactor"/>, <see cref="MotionIntensity.Full"/>
    /// is 1 (R-W5.8). An undefined <paramref name="setting"/> value falls
    /// back to 0, the same fail-safe direction as every other gate here.
    /// Pure and allocation free.
    /// </summary>
    public static float ResolveAmplitudeFactor(
        MotionIntensity setting,
        bool isHighContrastTheme,
        GrassZoomBand zoomBand,
        bool isSuppressed)
    {
        if (isHighContrastTheme || zoomBand == GrassZoomBand.Far || isSuppressed)
        {
            return 0f;
        }

        return setting switch
        {
            MotionIntensity.Off => 0f,
            MotionIntensity.Reduced => ReducedAmplitudeFactor,
            MotionIntensity.Full => 1f,
            _ => 0f,
        };
    }
}
