using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.UI;

internal readonly record struct SettingsChoiceInteraction<T>(
    T? SelectedValue,
    bool PointerConsumed)
    where T : struct, Enum;

/// <summary>
/// Shared selector mechanics for the two startup-oriented choices added
/// together by settings schema 8. Existing selectors remain unchanged; this
/// removes duplication only between these new controls.
/// </summary>
internal sealed class SettingsChoiceSelector<T>
    where T : struct, Enum
{
    private static readonly Keys[] ActivationKeys =
    [
        Keys.Left,
        Keys.Right,
        Keys.Enter,
        Keys.Space,
    ];

    private readonly string _label;
    private readonly T[] _options;
    private readonly string[] _names;
    private readonly string _markerPrefix;
    private readonly UiThemeSelectorLayout _layout;
    private readonly UiTextRoles _textRoles;
    private readonly UiSelectorMotion _motion = new();

    public SettingsChoiceSelector(
        string label,
        IReadOnlyList<T> options,
        IReadOnlyList<string> names,
        string markerPrefix,
        UiThemeStandards standards)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(markerPrefix);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(standards);
        if (options.Count == 0 || options.Count != names.Count)
        {
            throw new ArgumentException(
                "Selector options and names must be non-empty and have equal counts.");
        }

        _label = label;
        _options = [.. options];
        _names = [.. names];
        _markerPrefix = markerPrefix;
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

    public IReadOnlyList<string> OptionNames => _names;

    public string GetDisplayName(T value) => _names[GetIndex(value)];

    public T GetPrevious(T current) => GetRelative(current, -1);

    public T GetNext(T current) => GetRelative(current, 1);

    public string GetPositionText(T current) =>
        $"{GetIndex(current) + 1} / {_options.Length}";

    public string GetSelectedMarkerText(T current) =>
        $"{_markerPrefix}  -  {GetPositionText(current)}";

    public T? GetKeyboardSelection(Keys key, bool isFocused, T current)
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

    public T? GetPointerSelection(Point pointer, bool wasPressed, T current)
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

    public SettingsChoiceInteraction<T> Update(
        InputEdges input,
        bool isFocused,
        T current)
    {
        var pointerInside = Bounds.Contains(input.MousePosition);
        var pointerSelection = GetPointerSelection(
            input.MousePosition,
            input.WasLeftMousePressed(),
            current);
        if (pointerSelection is { } pointerValue)
        {
            return new SettingsChoiceInteraction<T>(pointerValue, true);
        }

        if (isFocused)
        {
            foreach (var key in ActivationKeys)
            {
                if (input.WasPressed(key))
                {
                    return new SettingsChoiceInteraction<T>(
                        GetKeyboardSelection(key, true, current),
                        true);
                }
            }
        }

        return new SettingsChoiceInteraction<T>(null, pointerInside);
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
        T current)
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
        UiTheme theme,
        T current,
        bool isFocused)
    {
        var colors = theme.Colors;
        spriteBatch.Draw(pixel, Bounds, colors.PanelAlternate);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            Bounds,
            isFocused ? colors.ActionFocus : colors.PanelBorder,
            UiScaleContext.Pixels(
                isFocused
                    ? theme.Metrics.FocusThickness
                    : theme.Metrics.BorderThickness));

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
            _label,
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

    private T GetRelative(T current, int direction)
    {
        var index = GetIndex(current);
        index = (index + direction + _options.Length) % _options.Length;
        return _options[index];
    }

    private int GetIndex(T current)
    {
        for (var index = 0; index < _options.Length; index++)
        {
            if (EqualityComparer<T>.Default.Equals(_options[index], current))
            {
                return index;
            }
        }

        return 0;
    }
}
