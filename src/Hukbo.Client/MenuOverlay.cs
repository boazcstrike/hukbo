using Hukbo.Client.Presentation;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client;

internal readonly record struct MenuInteraction(
    ClientCommand Command,
    string? SelectedThemeId,
    bool PointerConsumed)
{
    public static MenuInteraction None =>
        new(ClientCommand.None, null, false);
}

internal sealed class MenuOverlay
{
    private readonly UiButton[] _buttons =
    [
        new("Play", ClientCommand.Play),
        new("Pause", ClientCommand.Pause),
        new("Next Round", ClientCommand.NextRound),
        new("Full Reset", ClientCommand.FullReset),
        new("Exit Game", ClientCommand.Exit),
    ];

    private readonly UiThemeSelector _themeSelector;
    private readonly UiMenuLayout _layout;
    private readonly UiThemeSelectorLayout _selectorLayout;
    private readonly UiTextScales _textScales;
    private int _focusedControlIndex;

    public MenuOverlay(
        IReadOnlyList<UiTheme> themes,
        UiThemeStandards standards)
    {
        _themeSelector = new UiThemeSelector(themes, standards);
        _layout = standards.Shared.Menu;
        _selectorLayout = standards.Shared.Selector;
        _textScales = standards.Shared.TextScales;
    }

    public bool IsVisible { get; private set; }

    public void Open()
    {
        IsVisible = true;
        _focusedControlIndex = 0;
    }

    public void Close()
    {
        IsVisible = false;
        ResetVisualState();
    }

    public MenuInteraction Update(
        InputEdges input,
        Rectangle screenBounds,
        string activeThemeId)
    {
        if (!IsVisible)
        {
            return MenuInteraction.None;
        }

        Layout(screenBounds);

        var focusDirection = 0;
        if (input.WasPressed(Keys.Down) ||
            input.WasPressed(Keys.S) ||
            input.WasPressed(Keys.Tab))
        {
            focusDirection = 1;
        }
        else if (input.WasPressed(Keys.Up) || input.WasPressed(Keys.W))
        {
            focusDirection = -1;
        }

        var hoveredControlIndex = _themeSelector.Bounds.Contains(
            input.MousePosition)
            ? 0
            : -1;
        for (var index = 0; index < _buttons.Length; index++)
        {
            var button = _buttons[index];
            button.Update(input, index + 1 == _focusedControlIndex);

            if (button.IsHovered)
            {
                hoveredControlIndex = index + 1;
            }
        }

        var resolvedFocus = ResolveFocusedControlIndex(
            _focusedControlIndex,
            focusDirection,
            hoveredControlIndex,
            _buttons.Length + 1);
        if (resolvedFocus != _focusedControlIndex)
        {
            _focusedControlIndex = resolvedFocus;
        }

        for (var index = 0; index < _buttons.Length; index++)
        {
            _buttons[index].Update(
                input,
                index + 1 == _focusedControlIndex);
        }

        var themeInteraction = _themeSelector.Update(
            input,
            _focusedControlIndex == 0,
            activeThemeId);
        if (themeInteraction.SelectedThemeId is not null)
        {
            return new MenuInteraction(
                ClientCommand.None,
                themeInteraction.SelectedThemeId,
                true);
        }

        if (input.WasLeftMousePressed() && hoveredControlIndex > 0)
        {
            return new MenuInteraction(
                _buttons[hoveredControlIndex - 1].Command,
                null,
                true);
        }

        if (_focusedControlIndex > 0 &&
            (input.WasPressed(Keys.Enter) ||
             input.WasPressed(Keys.Space)))
        {
            var focusedButton = _buttons[_focusedControlIndex - 1];
            return new MenuInteraction(
                focusedButton.IsEnabled
                    ? focusedButton.Command
                    : ClientCommand.None,
                null,
                true);
        }

        return new MenuInteraction(ClientCommand.None, null, true);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        Rectangle screenBounds,
        UiTheme theme)
    {
        if (!IsVisible)
        {
            return;
        }

        Layout(screenBounds);

        spriteBatch.Draw(pixel, screenBounds, theme.Colors.OverlayScrim);

        var panelBounds = GetPanelBounds(screenBounds);
        if (theme.Metrics.ShadowOffset > 0)
        {
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    panelBounds.X + theme.Metrics.ShadowOffset,
                    panelBounds.Y + theme.Metrics.ShadowOffset,
                    panelBounds.Width,
                    panelBounds.Height),
                theme.Colors.CanvasBackground);
        }

        spriteBatch.Draw(pixel, panelBounds, theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            panelBounds,
            theme.Colors.PanelBorder,
            theme.Metrics.BorderThickness);

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            "HUKBO",
            new Vector2(
                panelBounds.Center.X,
                panelBounds.Top + _layout.TitleTopOffset),
            theme.Colors.TextPrimary,
            _textScales.MenuTitle);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            "Simulation controls",
            new Vector2(
                panelBounds.Center.X,
                panelBounds.Top + _layout.SubtitleTopOffset),
            theme.Colors.TextSecondary,
            _textScales.MenuSubtitle);

        _themeSelector.Draw(
            spriteBatch,
            pixel,
            font,
            theme,
            _focusedControlIndex == 0);

        foreach (var button in _buttons)
        {
            button.Draw(
                spriteBatch,
                pixel,
                font,
                theme,
                _textScales.MenuButton);
        }

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            "Esc closes  |  Up/Down focus  |  Left/Right theme",
            new Vector2(
                panelBounds.Center.X,
                panelBounds.Bottom - _layout.HelperBottomOffset),
            theme.Colors.TextSecondary,
            _textScales.MenuHelper);
    }

    private void Layout(Rectangle screenBounds)
    {
        var panel = GetPanelBounds(screenBounds);
        var buttonLeft = panel.Center.X - (_layout.ButtonWidth / 2);
        _themeSelector.Bounds = new Rectangle(
            buttonLeft,
            panel.Top + _layout.SelectorTopOffset,
            _layout.ButtonWidth,
            _selectorLayout.Height);
        var buttonTop =
            _themeSelector.Bounds.Bottom + _layout.SelectorGap;

        for (var index = 0; index < _buttons.Length; index++)
        {
            _buttons[index].Bounds = new Rectangle(
                buttonLeft,
                buttonTop + (index *
                    (_layout.ButtonHeight + _layout.ButtonGap)),
                _layout.ButtonWidth,
                _layout.ButtonHeight);
        }
    }

    internal static int ResolveFocusedControlIndex(
        int currentIndex,
        int keyboardDirection,
        int hoveredIndex,
        int controlCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(controlCount);
        if (keyboardDirection != 0)
        {
            return (currentIndex +
                Math.Sign(keyboardDirection) +
                controlCount) % controlCount;
        }

        return hoveredIndex >= 0 ? hoveredIndex : currentIndex;
    }

    private Rectangle GetPanelBounds(Rectangle screenBounds) =>
        new(
            screenBounds.Center.X - (_layout.PanelWidth / 2),
            screenBounds.Center.Y - (_layout.PanelHeight / 2),
            _layout.PanelWidth,
            _layout.PanelHeight);

    private void ResetVisualState()
    {
        foreach (var button in _buttons)
        {
            button.ResetVisualState();
        }
    }
}
