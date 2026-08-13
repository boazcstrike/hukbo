using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.UI;

internal readonly record struct GoreSelectorInteraction(
    GoreIntensity? SelectedGoreIntensity,
    bool PointerConsumed);

/// <summary>
/// Cycles the three blood rendering levels. All mechanics — bounds, wrapping,
/// keyboard and pointer selection, motion, and drawing — are delegated to the
/// shared <see cref="SettingsChoiceSelector{T}"/>; this type declares only the
/// per-type option table, names, and label, mirroring
/// <see cref="MotionIntensitySelector"/> and <see cref="AutoCameraModeSelector"/>.
/// </summary>
internal sealed class GoreIntensitySelector
{
    private const string Label = "GORE INTENSITY";

    private static readonly GoreIntensity[] Options =
    [
        GoreIntensity.Off,
        GoreIntensity.Stylized,
        GoreIntensity.Full,
    ];

    private static readonly string[] Names =
    [
        "Off",
        "Stylized",
        "Full",
    ];

    private readonly SettingsChoiceSelector<GoreIntensity> _selector;

    public GoreIntensitySelector(UiThemeStandards standards)
    {
        ArgumentNullException.ThrowIfNull(standards);
        _selector = new SettingsChoiceSelector<GoreIntensity>(
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

    public static string GetDisplayName(GoreIntensity value) =>
        Names[GetIndex(value)];

    public GoreIntensity GetPrevious(GoreIntensity current) =>
        _selector.GetPrevious(current);

    public GoreIntensity GetNext(GoreIntensity current) =>
        _selector.GetNext(current);

    public string GetPositionText(GoreIntensity current) =>
        _selector.GetPositionText(current);

    public string GetSelectedMarkerText(GoreIntensity current) =>
        _selector.GetSelectedMarkerText(current);

    public GoreIntensity? GetKeyboardSelection(
        Keys key,
        bool isFocused,
        GoreIntensity current) =>
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
    public GoreIntensity? GetPointerSelection(
        Point pointer,
        bool activated,
        GoreIntensity current) =>
        _selector.GetPointerSelection(pointer, activated, current);

    public GoreSelectorInteraction Update(
        InputEdges input,
        bool isFocused,
        GoreIntensity current)
    {
        var result = _selector.Update(input, isFocused, current);
        return new GoreSelectorInteraction(
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
        GoreIntensity current) =>
        _selector.AdvanceMotion(input, elapsed, intensity, current);

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        UiTheme activeTheme,
        GoreIntensity current,
        bool isFocused) =>
        _selector.Draw(
            spriteBatch, pixel, fonts, activeTheme, current, isFocused);

    /// <summary>
    /// An unknown value resolves to the first option rather than throwing, so a
    /// corrupt persisted level can still be cycled back to a valid one.
    /// </summary>
    private static int GetIndex(GoreIntensity current)
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
