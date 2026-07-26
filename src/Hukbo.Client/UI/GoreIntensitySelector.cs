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
/// Cycles the three blood rendering levels. Mirrors
/// <see cref="UiThemeSelector"/>'s shape: every decision is a pure method the
/// tests exercise directly, and <see cref="Draw"/> only paints what those
/// methods already decided.
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

    private static readonly Keys[] ActivationKeys =
    [
        Keys.Left,
        Keys.Right,
        Keys.Enter,
        Keys.Space,
    ];

    private readonly UiThemeSelectorLayout _layout;
    private readonly UiTextScales _textScales;

    public GoreIntensitySelector(UiThemeStandards standards)
    {
        ArgumentNullException.ThrowIfNull(standards);
        _layout = standards.Shared.Selector;
        _textScales = standards.Shared.TextScales;
    }

    public Rectangle Bounds { get; set; }

    public Rectangle PreviousBounds =>
        new(Bounds.Left, Bounds.Top, _layout.ArrowWidth, Bounds.Height);

    public Rectangle NextBounds =>
        new(
            Bounds.Right - _layout.ArrowWidth,
            Bounds.Top,
            _layout.ArrowWidth,
            Bounds.Height);

    public IReadOnlyList<string> OptionNames => Names;

    public static string GetDisplayName(GoreIntensity value) =>
        Names[GetIndex(value)];

    public GoreIntensity GetPrevious(GoreIntensity current) =>
        GetRelative(current, -1);

    public GoreIntensity GetNext(GoreIntensity current) =>
        GetRelative(current, 1);

    public string GetPositionText(GoreIntensity current) =>
        $"{GetIndex(current) + 1} / {Options.Length}";

    public string GetSelectedMarkerText(GoreIntensity current) =>
        $"ACTIVE  -  {GetPositionText(current)}";

    public GoreIntensity? GetKeyboardSelection(
        Keys key,
        bool isFocused,
        GoreIntensity current)
    {
        if (!isFocused)
        {
            return null;
        }

        return key switch
        {
            Keys.Left => GetPrevious(current),
            Keys.Right or Keys.Enter or Keys.Space => GetNext(current),
            _ => null,
        };
    }

    public GoreIntensity? GetPointerSelection(
        Point pointer,
        bool activated,
        GoreIntensity current)
    {
        if (!activated)
        {
            return null;
        }

        if (PreviousBounds.Contains(pointer))
        {
            return GetPrevious(current);
        }

        return NextBounds.Contains(pointer)
            ? GetNext(current)
            : null;
    }

    public GoreSelectorInteraction Update(
        InputEdges input,
        bool isFocused,
        GoreIntensity current)
    {
        var pointerInside = Bounds.Contains(input.MousePosition);
        var pointerSelection = GetPointerSelection(
            input.MousePosition,
            input.WasLeftMousePressed(),
            current);
        if (pointerSelection is { } pointerValue)
        {
            return new GoreSelectorInteraction(pointerValue, true);
        }

        if (isFocused)
        {
            foreach (var key in ActivationKeys)
            {
                if (input.WasPressed(key))
                {
                    return new GoreSelectorInteraction(
                        GetKeyboardSelection(key, true, current),
                        true);
                }
            }
        }

        return new GoreSelectorInteraction(null, pointerInside);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        UiTheme activeTheme,
        GoreIntensity current,
        bool isFocused)
    {
        var colors = activeTheme.Colors;
        spriteBatch.Draw(pixel, Bounds, colors.PanelAlternate);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            Bounds,
            isFocused ? colors.ActionFocus : colors.PanelBorder,
            isFocused
                ? activeTheme.Metrics.FocusThickness
                : activeTheme.Metrics.BorderThickness);

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            "<",
            PreviousBounds.Center.ToVector2(),
            colors.TextPrimary,
            _textScales.SelectorArrow);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            ">",
            NextBounds.Center.ToVector2(),
            colors.TextPrimary,
            _textScales.SelectorArrow);

        var centerX = Bounds.Center.X;
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            Label,
            new Vector2(centerX, Bounds.Top + _layout.LabelTopOffset),
            colors.TextSecondary,
            _textScales.SelectorLabel);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            GetDisplayName(current),
            new Vector2(centerX, Bounds.Top + _layout.NameTopOffset),
            colors.TextPrimary,
            _textScales.SelectorName);

        // The level is stated as text as well as position, so the control never
        // relies on color alone to say which level is active.
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            GetSelectedMarkerText(current),
            new Vector2(centerX, Bounds.Top + _layout.MarkerTopOffset),
            colors.Selection,
            _textScales.SelectorMarker);
    }

    private static GoreIntensity GetRelative(
        GoreIntensity current,
        int direction)
    {
        var index = GetIndex(current);
        index = (index + direction + Options.Length) % Options.Length;
        return Options[index];
    }

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
