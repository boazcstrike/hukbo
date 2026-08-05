using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.UI;

internal readonly record struct ThemeSelectorInteraction(
    string? SelectedThemeId,
    bool PointerConsumed);

internal sealed class UiThemeSelector
{
    private readonly IReadOnlyList<UiTheme> _themes;
    private readonly UiThemeSelectorLayout _layout;
    private readonly UiTextRoles _textRoles;

    public UiThemeSelector(
        IReadOnlyList<UiTheme> themes,
        UiThemeStandards standards)
    {
        if (themes.Count != standards.RequiredThemeCount)
        {
            throw new ArgumentException(
                "Theme count must match the configured standards.",
                nameof(themes));
        }

        _themes = themes;
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

    public IReadOnlyList<string> ThemeNames =>
        _themes.Select(theme => theme.DisplayName).ToArray();

    public string GetPreviousId(string currentId) =>
        GetRelativeId(currentId, -1);

    public string GetNextId(string currentId) =>
        GetRelativeId(currentId, 1);

    public string GetPositionText(string currentId)
    {
        var index = GetIndex(currentId);
        return $"{index + 1} / {_themes.Count}";
    }

    public string GetSelectedMarkerText(string currentId) =>
        $"ACTIVE  -  {GetPositionText(currentId)}";

    public static string GetSelectorLabelText(string currentId) =>
        currentId == "datu-court"
            ? "PROVISIONAL RECONSTRUCTION"
            : "VISUAL THEME";

    public string? GetKeyboardSelection(
        Keys key,
        bool isFocused,
        string currentId)
    {
        if (!isFocused)
        {
            return null;
        }

        return key switch
        {
            Keys.Left => GetPreviousId(currentId),
            Keys.Right or Keys.Enter or Keys.Space => GetNextId(currentId),
            _ => null,
        };
    }

    public string? GetPointerSelection(
        Point pointer,
        bool activated,
        string currentId)
    {
        if (!activated)
        {
            return null;
        }

        if (PreviousBounds.Contains(pointer))
        {
            return GetPreviousId(currentId);
        }

        return NextBounds.Contains(pointer)
            ? GetNextId(currentId)
            : null;
    }

    public ThemeSelectorInteraction Update(
        InputEdges input,
        bool isFocused,
        string currentId)
    {
        var pointerInside = Bounds.Contains(input.MousePosition);
        var pointerSelection = GetPointerSelection(
            input.MousePosition,
            input.WasLeftMousePressed(),
            currentId);
        if (pointerSelection is not null)
        {
            return new ThemeSelectorInteraction(pointerSelection, true);
        }

        if (isFocused)
        {
            foreach (var key in new[]
                     {
                         Keys.Left,
                         Keys.Right,
                         Keys.Enter,
                         Keys.Space,
                     })
            {
                if (input.WasPressed(key))
                {
                    return new ThemeSelectorInteraction(
                        GetKeyboardSelection(key, true, currentId),
                        true);
                }
            }
        }

        return new ThemeSelectorInteraction(null, pointerInside);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        UiTheme activeTheme,
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
            GetSelectorLabelText(activeTheme.Id),
            new Vector2(
                centerX,
                Bounds.Top +
                    UiScaleContext.Pixels(_layout.LabelTopOffset)),
            colors.TextSecondary);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.SelectorName),
            activeTheme.DisplayName,
            new Vector2(
                centerX,
                Bounds.Top +
                    UiScaleContext.Pixels(_layout.NameTopOffset)),
            colors.TextPrimary);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.SelectorMarker),
            GetSelectedMarkerText(activeTheme.Id),
            new Vector2(
                centerX,
                Bounds.Top +
                    UiScaleContext.Pixels(_layout.MarkerTopOffset)),
            colors.Selection);

        var swatches = new[]
        {
            colors.PanelSurface,
            colors.ActionDefault,
            colors.TeamA,
            colors.TeamB,
            colors.Selection,
        };
        var totalWidth =
            (swatches.Length *
                UiScaleContext.Pixels(_layout.SwatchWidth)) +
            ((swatches.Length - 1) *
                UiScaleContext.Pixels(_layout.SwatchGap));
        var left = centerX - (totalWidth / 2);
        var top =
            Bounds.Bottom -
            UiScaleContext.Pixels(_layout.Padding) -
            UiScaleContext.Pixels(_layout.SwatchHeight);
        for (var index = 0; index < swatches.Length; index++)
        {
            var swatch = new Rectangle(
                left + (index *
                    (UiScaleContext.Pixels(_layout.SwatchWidth) +
                        UiScaleContext.Pixels(_layout.SwatchGap))),
                top,
                UiScaleContext.Pixels(_layout.SwatchWidth),
                UiScaleContext.Pixels(_layout.SwatchHeight));
            spriteBatch.Draw(pixel, swatch, swatches[index]);
        }
    }

    private int GetArrowWidth() =>
        Math.Max(
            UiScaleContext.Pixels(_layout.ArrowWidth),
            UiScaleContext.Pixels(_layout.MinimumTargetSize));

    private string GetRelativeId(string currentId, int direction)
    {
        var index = GetIndex(currentId);
        index = (index + direction + _themes.Count) % _themes.Count;
        return _themes[index].Id;
    }

    private int GetIndex(string currentId)
    {
        for (var index = 0; index < _themes.Count; index++)
        {
            if (_themes[index].Id == currentId)
            {
                return index;
            }
        }

        return 0;
    }
}
