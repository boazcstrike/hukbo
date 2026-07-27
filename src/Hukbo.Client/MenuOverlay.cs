using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client;

internal readonly record struct MenuInteraction(
    ClientCommand Command,
    string? SelectedThemeId,
    GoreIntensity? SelectedGoreIntensity,
    bool PointerConsumed)
{
    public static MenuInteraction None =>
        new(ClientCommand.None, null, null, false);
}

internal sealed class MenuOverlay
{
    internal static readonly (string Label, ClientCommand Command)[]
        ButtonDefinitions =
        [
            ("Play", ClientCommand.Play),
            ("Pause", ClientCommand.Pause),
            ("Next Round", ClientCommand.NextRound),
            ("Army Composition", ClientCommand.OpenArmyComposition),
            ("Full Reset", ClientCommand.FullReset),
            ("Exit Game", ClientCommand.Exit),
        ];

    private readonly UiButton[] _buttons = ButtonDefinitions
        .Select(definition => new UiButton(definition.Label, definition.Command))
        .ToArray();

    private readonly UiThemeSelector _themeSelector;
    private readonly GoreIntensitySelector _goreSelector;
    private readonly UiMenuLayout _layout;
    private readonly UiThemeSelectorLayout _selectorLayout;
    private readonly UiTextRoles _textRoles;
    private int _focusedControlIndex;

    public MenuOverlay(
        IReadOnlyList<UiTheme> themes,
        UiThemeStandards standards)
    {
        _themeSelector = new UiThemeSelector(themes, standards);
        _goreSelector = new GoreIntensitySelector(standards);
        _layout = standards.Shared.Menu;
        _selectorLayout = standards.Shared.Selector;
        _textRoles = standards.Shared.TextRoles;
    }

    public bool IsVisible { get; private set; }

    /// <summary>
    /// Focus index 0 is the theme selector, indices 1..N are the N buttons, and
    /// the gore selector takes the terminal index N+1. Appending rather than
    /// interleaving leaves every existing button index unchanged.
    /// </summary>
    internal static int GoreSelectorControlIndex =>
        ButtonDefinitions.Length + 1;

    internal static int ControlCount => ButtonDefinitions.Length + 2;

    internal static bool IsButtonControlIndex(int controlIndex) =>
        controlIndex > 0 && controlIndex <= ButtonDefinitions.Length;

    /// <summary>
    /// The offset, from the panel's top edge, of the bottom of the lowest
    /// control. The panel data must leave room for this above its helper line,
    /// which the client tests assert directly.
    /// </summary>
    internal static int CalculateContentBottomOffset(
        UiMenuLayout layout,
        UiThemeSelectorLayout selectorLayout,
        int buttonCount) =>
        CalculateGoreSelectorTopOffset(layout, selectorLayout, buttonCount) +
        selectorLayout.Height;

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
        string activeThemeId,
        GoreIntensity activeGoreIntensity)
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

        // Evaluated after the button loop so a hovered button is never
        // clobbered by the terminal control.
        if (_goreSelector.Bounds.Contains(input.MousePosition))
        {
            hoveredControlIndex = GoreSelectorControlIndex;
        }

        var resolvedFocus = ResolveFocusedControlIndex(
            _focusedControlIndex,
            focusDirection,
            hoveredControlIndex,
            ControlCount);
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
                null,
                true);
        }

        var goreInteraction = _goreSelector.Update(
            input,
            _focusedControlIndex == GoreSelectorControlIndex,
            activeGoreIntensity);
        if (goreInteraction.SelectedGoreIntensity is { } selectedGoreIntensity)
        {
            return new MenuInteraction(
                ClientCommand.None,
                null,
                selectedGoreIntensity,
                true);
        }

        if (input.WasLeftMousePressed() &&
            IsButtonControlIndex(hoveredControlIndex))
        {
            return new MenuInteraction(
                _buttons[hoveredControlIndex - 1].Command,
                null,
                null,
                true);
        }

        if (IsButtonControlIndex(_focusedControlIndex) &&
            (input.WasPressed(Keys.Enter) ||
             input.WasPressed(Keys.Space)))
        {
            var focusedButton = _buttons[_focusedControlIndex - 1];
            return new MenuInteraction(
                focusedButton.IsEnabled
                    ? focusedButton.Command
                    : ClientCommand.None,
                null,
                null,
                true);
        }

        return new MenuInteraction(ClientCommand.None, null, null, true);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        Rectangle screenBounds,
        UiTheme theme,
        GoreIntensity activeGoreIntensity)
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
            fonts.Get(_textRoles.MenuTitle),
            "HUKBO",
            new Vector2(
                panelBounds.Center.X,
                panelBounds.Top + _layout.TitleTopOffset),
            theme.Colors.TextPrimary);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.MenuSubtitle),
            "Simulation controls",
            new Vector2(
                panelBounds.Center.X,
                panelBounds.Top + _layout.SubtitleTopOffset),
            theme.Colors.TextSecondary);

        _themeSelector.Draw(
            spriteBatch,
            pixel,
            fonts,
            theme,
            _focusedControlIndex == 0);

        foreach (var button in _buttons)
        {
            button.Draw(
                spriteBatch,
                pixel,
                fonts.Get(_textRoles.MenuButton),
                theme);
        }

        _goreSelector.Draw(
            spriteBatch,
            pixel,
            fonts,
            theme,
            activeGoreIntensity,
            _focusedControlIndex == GoreSelectorControlIndex);

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.MenuHelper),
            "Esc closes  |  Up/Down focus  |  Left/Right change",
            new Vector2(
                panelBounds.Center.X,
                panelBounds.Bottom - _layout.HelperBottomOffset),
            theme.Colors.TextSecondary);
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

        _goreSelector.Bounds = new Rectangle(
            buttonLeft,
            panel.Top + CalculateGoreSelectorTopOffset(
                _layout,
                _selectorLayout,
                _buttons.Length),
            _layout.ButtonWidth,
            _selectorLayout.Height);
    }

    private static int CalculateGoreSelectorTopOffset(
        UiMenuLayout layout,
        UiThemeSelectorLayout selectorLayout,
        int buttonCount)
    {
        var buttonTopOffset =
            layout.SelectorTopOffset +
            selectorLayout.Height +
            layout.SelectorGap;
        var buttonBandHeight = buttonCount <= 0
            ? 0
            : (buttonCount * layout.ButtonHeight) +
              ((buttonCount - 1) * layout.ButtonGap);
        return buttonTopOffset + buttonBandHeight + layout.SelectorGap;
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
