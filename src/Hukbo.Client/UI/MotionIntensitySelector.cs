using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.UI;

internal readonly record struct MotionSelectorInteraction(
    MotionIntensity? SelectedMotionIntensity,
    bool PointerConsumed);

/// <summary>
/// Cycles the three ambient-motion levels. All mechanics — bounds, wrapping,
/// keyboard and pointer selection, motion, and drawing — are delegated to the
/// shared <see cref="SettingsChoiceSelector{T}"/>; this type declares only the
/// per-type option table, names, and label, mirroring
/// <see cref="GoreIntensitySelector"/> and <see cref="AutoCameraModeSelector"/>.
/// </summary>
internal sealed class MotionIntensitySelector
{
    private const string Label = "MOTION INTENSITY";

    private static readonly MotionIntensity[] Options =
    [
        MotionIntensity.Off,
        MotionIntensity.Reduced,
        MotionIntensity.Full,
    ];

    private static readonly string[] Names =
    [
        "Off",
        "Reduced",
        "Full",
    ];

    private readonly SettingsChoiceSelector<MotionIntensity> _selector;

    public MotionIntensitySelector(UiThemeStandards standards)
    {
        ArgumentNullException.ThrowIfNull(standards);
        _selector = new SettingsChoiceSelector<MotionIntensity>(
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

    public static string GetDisplayName(MotionIntensity value) =>
        Names[GetIndex(value)];

    public MotionIntensity GetPrevious(MotionIntensity current) =>
        _selector.GetPrevious(current);

    public MotionIntensity GetNext(MotionIntensity current) =>
        _selector.GetNext(current);

    public string GetPositionText(MotionIntensity current) =>
        _selector.GetPositionText(current);

    public string GetSelectedMarkerText(MotionIntensity current) =>
        _selector.GetSelectedMarkerText(current);

    public MotionIntensity? GetKeyboardSelection(
        Keys key,
        bool isFocused,
        MotionIntensity current) =>
        _selector.GetKeyboardSelection(key, isFocused, current);

    /// <summary>
    /// Delegates to <see cref="SettingsChoiceSelector{T}.GetPointerSelection"/>,
    /// whose extra <c>!Bounds.Contains(pointer)</c> guard is a no-op here: both
    /// <see cref="PreviousBounds"/> and <see cref="NextBounds"/> share
    /// <see cref="Bounds"/>'s top and height and are anchored inside its left
    /// and right edges, so any pointer landing in either arrow rectangle
    /// already satisfies that guard. A pointer failing the guard therefore also
    /// fails both arrow-rectangle checks, and the two implementations agree on
    /// every input.
    /// </summary>
    public MotionIntensity? GetPointerSelection(
        Point pointer,
        bool activated,
        MotionIntensity current) =>
        _selector.GetPointerSelection(pointer, activated, current);

    public MotionSelectorInteraction Update(
        InputEdges input,
        bool isFocused,
        MotionIntensity current)
    {
        var result = _selector.Update(input, isFocused, current);
        return new MotionSelectorInteraction(
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
        MotionIntensity current) =>
        _selector.AdvanceMotion(input, elapsed, intensity, current);

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        UiTheme activeTheme,
        MotionIntensity current,
        bool isFocused) =>
        _selector.Draw(
            spriteBatch, pixel, fonts, activeTheme, current, isFocused);

    /// <summary>
    /// An unknown value resolves to the first option rather than throwing, so a
    /// corrupt persisted level can still be cycled back to a valid one.
    /// </summary>
    private static int GetIndex(MotionIntensity current)
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
