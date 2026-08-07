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
/// Cycles the three camera-assistant modes. Mirrors
/// <see cref="MotionIntensitySelector"/>'s shape exactly: every decision is a
/// pure method the tests exercise directly, and <see cref="Draw"/> only
/// paints what those methods already decided.
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

    private static readonly Keys[] ActivationKeys =
    [
        Keys.Left,
        Keys.Right,
        Keys.Enter,
        Keys.Space,
    ];

    private readonly UiThemeSelectorLayout _layout;
    private readonly UiTextRoles _textRoles;
    private readonly UiSelectorMotion _motion = new();

    public AutoCameraModeSelector(UiThemeStandards standards)
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

    public static string GetDisplayName(AutoCameraMode value) =>
        Names[GetIndex(value)];

    public AutoCameraMode GetPrevious(AutoCameraMode current) =>
        GetRelative(current, -1);

    public AutoCameraMode GetNext(AutoCameraMode current) =>
        GetRelative(current, 1);

    public string GetPositionText(AutoCameraMode current) =>
        $"{GetIndex(current) + 1} / {Options.Length}";

    public string GetSelectedMarkerText(AutoCameraMode current) =>
        $"ACTIVE  -  {GetPositionText(current)}";

    public AutoCameraMode? GetKeyboardSelection(
        Keys key,
        bool isFocused,
        AutoCameraMode current)
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

    public AutoCameraMode? GetPointerSelection(
        Point pointer,
        bool wasPressed,
        AutoCameraMode current)
    {
        if (!wasPressed || !Bounds.Contains(pointer))
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

    public AutoCameraSelectorInteraction Update(
        InputEdges input,
        bool isFocused,
        AutoCameraMode current)
    {
        var pointerInside = Bounds.Contains(input.MousePosition);
        var pointerSelection = GetPointerSelection(
            input.MousePosition,
            input.WasLeftMousePressed(),
            current);
        if (pointerSelection is { } pointerValue)
        {
            return new AutoCameraSelectorInteraction(pointerValue, true);
        }

        if (isFocused)
        {
            foreach (var key in ActivationKeys)
            {
                if (input.WasPressed(key))
                {
                    return new AutoCameraSelectorInteraction(
                        GetKeyboardSelection(key, true, current),
                        true);
                }
            }
        }

        return new AutoCameraSelectorInteraction(null, pointerInside);
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
        AutoCameraMode current)
    {
        _motion.AdvanceMotion(
            input.MousePosition,
            PreviousBounds,
            NextBounds,
            GetSelectedMarkerText(current),
            elapsed,
            intensity);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        UiTheme activeTheme,
        AutoCameraMode current,
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
            _motion.PreviousArrowColor(colors));
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.SelectorArrow),
            ">",
            NextBounds.Center.ToVector2(),
            _motion.NextArrowColor(colors));

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

        // The mode is stated as text as well as position, so the control never
        // relies on color alone to say which mode is active.
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.SelectorMarker),
            GetSelectedMarkerText(current),
            new Vector2(
                centerX,
                Bounds.Top +
                    UiScaleContext.Pixels(_layout.MarkerTopOffset)),
            _motion.MarkerColor(colors));
    }

    private int GetArrowWidth() =>
        Math.Max(
            UiScaleContext.Pixels(_layout.ArrowWidth),
            UiScaleContext.Pixels(_layout.MinimumTargetSize));

    private static AutoCameraMode GetRelative(
        AutoCameraMode current,
        int direction)
    {
        var index = GetIndex(current);
        index = (index + direction + Options.Length) % Options.Length;
        return Options[index];
    }

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
