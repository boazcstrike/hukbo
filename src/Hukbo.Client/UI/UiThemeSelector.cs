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
    private readonly UiTextScales _textScales;

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
        SpriteFont font,
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
            "VISUAL THEME",
            new Vector2(centerX, Bounds.Top + _layout.LabelTopOffset),
            colors.TextSecondary,
            _textScales.SelectorLabel);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            activeTheme.DisplayName,
            new Vector2(centerX, Bounds.Top + _layout.NameTopOffset),
            colors.TextPrimary,
            _textScales.SelectorName);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            GetSelectedMarkerText(activeTheme.Id),
            new Vector2(centerX, Bounds.Top + _layout.MarkerTopOffset),
            colors.Selection,
            _textScales.SelectorMarker);

        var swatches = new[]
        {
            colors.PanelSurface,
            colors.ActionDefault,
            colors.TeamA,
            colors.TeamB,
            colors.Selection,
        };
        var totalWidth =
            (swatches.Length * _layout.SwatchWidth) +
            ((swatches.Length - 1) * _layout.SwatchGap);
        var left = centerX - (totalWidth / 2);
        var top =
            Bounds.Bottom - _layout.Padding - _layout.SwatchHeight;
        for (var index = 0; index < swatches.Length; index++)
        {
            var swatch = new Rectangle(
                left + (index *
                    (_layout.SwatchWidth + _layout.SwatchGap)),
                top,
                _layout.SwatchWidth,
                _layout.SwatchHeight);
            spriteBatch.Draw(pixel, swatch, swatches[index]);
        }
    }

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
