using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.UI;

internal readonly record struct AutoCameraSelectorInteraction(
    AutoCameraMode? SelectedAutoCameraMode,
    bool PointerConsumed);

/// <summary>
/// Cycles the three camera-assistant modes. All mechanics — bounds, wrapping,
/// keyboard and pointer selection, motion, and drawing — are delegated to the
/// shared <see cref="SettingsChoiceSelector{T}"/>; this type declares only the
/// per-type option table, names, and label, mirroring
/// <see cref="MotionIntensitySelector"/> and <see cref="GoreIntensitySelector"/>.
/// </summary>
internal sealed class AutoCameraModeSelector
{
    private const string Label = "AUTO CAMERA";

    private static readonly AutoCameraMode[] Options =
    [
        AutoCameraMode.Off,
        AutoCameraMode.Assisted,
        AutoCameraMode.Follow,
    ];

    private static readonly string[] Names =
    [
        "Off",
        "Assisted",
        "Follow",
    ];

    private readonly SettingsChoiceSelector<AutoCameraMode> _selector;

    public AutoCameraModeSelector(UiThemeStandards standards)
    {
        ArgumentNullException.ThrowIfNull(standards);
        _selector = new SettingsChoiceSelector<AutoCameraMode>(
            Label,
            Options,
            Names,
            "ACTIVE",
            standards);
    }

    public Rectangle Bounds
    {
        get => _selector.Bounds;
        set => _selector.Bounds = value;
    }

    public Rectangle PreviousBounds => _selector.PreviousBounds;

    public Rectangle NextBounds => _selector.NextBounds;

    public IReadOnlyList<string> OptionNames => _selector.OptionNames;

    public static string GetDisplayName(AutoCameraMode value) =>
        Names[GetIndex(value)];

    public AutoCameraMode GetPrevious(AutoCameraMode current) =>
        _selector.GetPrevious(current);

    public AutoCameraMode GetNext(AutoCameraMode current) =>
        _selector.GetNext(current);

    public string GetPositionText(AutoCameraMode current) =>
        _selector.GetPositionText(current);

    public string GetSelectedMarkerText(AutoCameraMode current) =>
        _selector.GetSelectedMarkerText(current);

    public AutoCameraMode? GetKeyboardSelection(
        Keys key,
        bool isFocused,
        AutoCameraMode current) =>
        _selector.GetKeyboardSelection(key, isFocused, current);

    /// <summary>
    /// Delegates directly to
    /// <see cref="SettingsChoiceSelector{T}.GetPointerSelection"/>: its
    /// original concrete body already tested <c>!Bounds.Contains(pointer)</c>
    /// before the two arrow rectangles, matching the generic method exactly,
    /// so there is no behaviour to reconcile here.
    /// </summary>
    public AutoCameraMode? GetPointerSelection(
        Point pointer,
        bool wasPressed,
        AutoCameraMode current) =>
        _selector.GetPointerSelection(pointer, wasPressed, current);

    public AutoCameraSelectorInteraction Update(
        InputEdges input,
        bool isFocused,
        AutoCameraMode current)
    {
        var result = _selector.Update(input, isFocused, current);
        return new AutoCameraSelectorInteraction(
            result.SelectedValue,
            result.PointerConsumed);
    }

    /// <summary>
    /// Advances the shared arrow-hover and marker-pulse motion. Called once
    /// per visible-menu frame from <see cref="MenuOverlay.Update"/>, before
    /// the early-returning interaction chain, so a selection reported by any
    /// other selector never stalls this one's transitions mid-flight.
    /// </summary>
    public void AdvanceMotion(
        InputEdges input,
        TimeSpan elapsed,
        MotionIntensity intensity,
        AutoCameraMode current) =>
        _selector.AdvanceMotion(input, elapsed, intensity, current);

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        UiTheme activeTheme,
        AutoCameraMode current,
        bool isFocused) =>
        _selector.Draw(
            spriteBatch, pixel, fonts, activeTheme, current, isFocused);

    /// <summary>
    /// An unrecognised mode reports as the first option rather than throwing,
    /// so a corrupt persisted value cannot take the menu down with it.
    /// </summary>
    private static int GetIndex(AutoCameraMode current)
    {
        for (var index = 0; index < Options.Length; index++)
        {
            if (Options[index] == current)
            {
                return index;
            }
        }

        return 0;
    }
}
