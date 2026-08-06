using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// Shared secondary-feedback motion for the five selector controls —
/// <see cref="UiThemeSelector"/>, <see cref="GoreIntensitySelector"/>,
/// <see cref="MotionIntensitySelector"/>, <see cref="AutoCameraModeSelector"/>,
/// and <see cref="SettingsChoiceSelector"/>. Owns two arrow-hover
/// <see cref="UiTransition"/> channels and one <see cref="UiEmphasisPulse"/>
/// for the active marker text. Every output is colour only — no hit
/// rectangle, no bounds, and no positional change is ever produced here.
/// </summary>
internal sealed class UiSelectorMotion
{
    private UiTransition _previousArrow;
    private UiTransition _nextArrow;
    private UiEmphasisPulse _marker;
    private string? _lastMarkerText;

    /// <summary>
    /// Advances both arrow-hover channels toward whether
    /// <paramref name="pointer"/> is inside the matching bounds, and pulses
    /// the marker channel when <paramref name="markerText"/> differs from the
    /// value last observed. The very first observation only seeds that
    /// state — it never pulses, since there is nothing to distinguish it
    /// from.
    /// </summary>
    public void AdvanceMotion(
        Point pointer,
        Rectangle previousBounds,
        Rectangle nextBounds,
        string markerText,
        TimeSpan elapsed,
        MotionIntensity intensity)
    {
        var isMotionEnabled = UiSecondaryMotion.IsEnabled(intensity);
        var duration = UiSecondaryMotion.SelectorMarkerDuration;

        _previousArrow.AdvanceTo(
            previousBounds.Contains(pointer) ? 1f : 0f,
            elapsed,
            duration,
            isMotionEnabled);
        _nextArrow.AdvanceTo(
            nextBounds.Contains(pointer) ? 1f : 0f,
            elapsed,
            duration,
            isMotionEnabled);

        if (_lastMarkerText is not null && _lastMarkerText != markerText)
        {
            _marker.Trigger(isMotionEnabled);
        }

        _lastMarkerText = markerText;
        _marker.Advance(elapsed, duration, isMotionEnabled);
    }

    public Color PreviousArrowColor(UiThemeColors colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        return Color.Lerp(colors.TextSecondary, colors.TextPrimary, _previousArrow.Value);
    }

    public Color NextArrowColor(UiThemeColors colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        return Color.Lerp(colors.TextSecondary, colors.TextPrimary, _nextArrow.Value);
    }

    public Color MarkerColor(UiThemeColors colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        return Color.Lerp(colors.Selection, colors.ActionFocus, _marker.Amount);
    }
}
