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
/// Cycles the three ambient-motion levels. Mirrors
/// <see cref="GoreIntensitySelector"/>'s shape exactly: every decision is a
/// pure method the tests exercise directly, and <see cref="Draw"/> only
/// paints what those methods already decided.
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

    private static readonly Keys[] ActivationKeys =
    [
        Keys.Left,
        Keys.Right,
        Keys.Enter,
        Keys.Space,
    ];

    private readonly UiThemeSelectorLayout _layout;
    private readonly UiTextRoles _textRoles;

    public MotionIntensitySelector(UiThemeStandards standards)
    {
        ArgumentNullException.ThrowIfNull(standards);
        _layout = standards.Shared.Selector;
        _textRoles = standards.Shared.TextRoles;
    }

    public Rectangle Bounds { get; set; }

    public Rectangle PreviousBounds =>
        new(Bounds.Left, Bounds.Top, GetArrowWidth(), Bounds.Height);

    public Rectangle NextBounds =>
        new(
            Bounds.Right - GetArrowWidth(),
            Bounds.Top,
            GetArrowWidth(),
            Bounds.Height);

    public IReadOnlyList<string> OptionNames => Names;

    public static string GetDisplayName(MotionIntensity value) =>
        Names[GetIndex(value)];

    public MotionIntensity GetPrevious(MotionIntensity current) =>
        GetRelative(current, -1);

    public MotionIntensity GetNext(MotionIntensity current) =>
        GetRelative(current, 1);

    public string GetPositionText(MotionIntensity current) =>
        $"{GetIndex(current) + 1} / {Options.Length}";

    public string GetSelectedMarkerText(MotionIntensity current) =>
        $"ACTIVE  -  {GetPositionText(current)}";

    public MotionIntensity? GetKeyboardSelection(
        Keys key,
        bool isFocused,
        MotionIntensity current)
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

    public MotionIntensity? GetPointerSelection(
        Point pointer,
        bool activated,
        MotionIntensity current)
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

    public MotionSelectorInteraction Update(
        InputEdges input,
        bool isFocused,
        MotionIntensity current)
    {
        var pointerInside = Bounds.Contains(input.MousePosition);
        var pointerSelection = GetPointerSelection(
            input.MousePosition,
            input.WasLeftMousePressed(),
            current);
        if (pointerSelection is { } pointerValue)
        {
            return new MotionSelectorInteraction(pointerValue, true);
        }

        if (isFocused)
        {
            foreach (var key in ActivationKeys)
            {
                if (input.WasPressed(key))
                {
                    return new MotionSelectorInteraction(
                        GetKeyboardSelection(key, true, current),
                        true);
                }
            }
        }

        return new MotionSelectorInteraction(null, pointerInside);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        UiTheme activeTheme,
        MotionIntensity current,
        bool isFocused)
    {
        var colors = activeTheme.Colors;
        spriteBatch.Draw(pixel, Bounds, colors.PanelAlternate);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            Bounds,
            isFocused ? colors.ActionFocus : colors.PanelBorder,
            UiScaleContext.Pixels(
                isFocused
                    ? activeTheme.Metrics.FocusThickness
                    : activeTheme.Metrics.BorderThickness));

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.SelectorArrow),
            "<",
            PreviousBounds.Center.ToVector2(),
            colors.TextPrimary);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.SelectorArrow),
            ">",
            NextBounds.Center.ToVector2(),
            colors.TextPrimary);

        var centerX = Bounds.Center.X;
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.SelectorLabel),
            Label,
            new Vector2(
                centerX,
                Bounds.Top +
                    UiScaleContext.Pixels(_layout.LabelTopOffset)),
            colors.TextSecondary);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.SelectorName),
            GetDisplayName(current),
            new Vector2(
                centerX,
                Bounds.Top +
                    UiScaleContext.Pixels(_layout.NameTopOffset)),
            colors.TextPrimary);

        // The level is stated as text as well as position, so the control never
        // relies on color alone to say which level is active.
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.SelectorMarker),
            GetSelectedMarkerText(current),
            new Vector2(
                centerX,
                Bounds.Top +
                    UiScaleContext.Pixels(_layout.MarkerTopOffset)),
            colors.Selection);
    }

    private int GetArrowWidth() =>
        Math.Max(
            UiScaleContext.Pixels(_layout.ArrowWidth),
            UiScaleContext.Pixels(_layout.MinimumTargetSize));

    private static MotionIntensity GetRelative(
        MotionIntensity current,
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
