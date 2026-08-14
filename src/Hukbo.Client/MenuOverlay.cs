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
    MotionIntensity? SelectedMotionIntensity,
    AutoCameraMode? SelectedAutoCameraMode,
    UiScale? SelectedUiScale,
    StartupDisplayMode? SelectedStartupDisplayMode,
    bool PointerConsumed,
    UiChromeStyle? SelectedUiChromeStyle = null)
{
    public static MenuInteraction None =>
        new(
            ClientCommand.None,
            null,
            null,
            null,
            null,
            null,
            null,
            false);
}

internal sealed class MenuOverlay
{
    private const int SafeMargin = 16;
    private const int ResponsivePanelWidth = 760;
    private const int ResponsivePanelHeight = 680;
    private const int ColumnGap = 40;
    private const int SettingsSelectorGap = 8;

    /// <summary>
    /// Nine-slice margin, in unscaled pixels, passed to
    /// <see cref="UiNineSlice.DrawPanel"/> for the menu panel. Matches the
    /// 12-pixel margin the chrome atlas is authored at, documented on
    /// <c>src/Hukbo.Client/Content/Textures/README.md</c>; scaled through
    /// <see cref="UiScaleContext.Pixels"/> like every other chrome metric so
    /// corners stay proportional as the interface scale changes.
    /// </summary>
    private const int NineSliceMarginPixels = 12;

    /// <summary>
    /// The number of selectors stacked in the settings column (the right-hand
    /// column, at <c>settingsLeft</c> in <see cref="Layout"/>): gore, motion,
    /// auto camera, UI scale, then startup display. Six selector fields exist
    /// on this class, but the theme selector is not one of these five — it is
    /// laid out in the button column instead, sharing <c>buttonLeft</c> with
    /// the button stack, and its height is already added explicitly as the
    /// standalone <c>selectorHeight</c> term in
    /// <see cref="CalculateContentBottomOffset"/>'s button-column branch. Do
    /// not raise this to 6 to "include" the theme selector; that would
    /// double-count it and overstate the settings-column height by one
    /// selector's worth of space. If a selector is added to the settings
    /// column, raise this constant; if one is added to the button column
    /// instead, extend the button-column branch's formula, not this constant.
    /// <c>MenuOverlayFocusTests.SettingsColumnFormulaMatchesActualSettingsColumnGeometry</c>
    /// fails if the two ever drift apart.
    /// </summary>
    private const int SettingsSelectorCount = 5;

    /// <summary>
    /// The number of selectors stacked in the button column (the left-hand
    /// column, at <c>buttonLeft</c> in <see cref="Layout"/>): the theme
    /// selector, then the chrome selector directly beneath it. The settings
    /// column was already full at five selectors against its 657-pixel
    /// budget (see the UI chrome nine-slice design document, section 10a), so
    /// the chrome selector was placed here instead. Both selectors' heights
    /// and gaps are reserved explicitly by
    /// <see cref="CalculateContentBottomOffset"/>'s button-column branch; do
    /// not fold this into <see cref="SettingsSelectorCount"/>, since that
    /// constant governs the settings column's arithmetic only.
    /// </summary>
    private const int ButtonColumnSelectorCount = 2;

    internal static readonly (string Label, ClientCommand Command)[]
        ButtonDefinitions =
        [
            ("Play", ClientCommand.Play),
            ("Pause", ClientCommand.Pause),
            ("Next Round", ClientCommand.NextRound),
            ("Army Composition", ClientCommand.OpenArmyComposition),
            ("Full Reset", ClientCommand.FullReset),
            // RequestExit, not Exit: both in-application quit paths get the
            // same confirmation, so the menu is not the less safe route.
            ("Exit Game", ClientCommand.RequestExit),
        ];

    private readonly UiButton[] _buttons = ButtonDefinitions
        .Select(definition => new UiButton(definition.Label, definition.Command))
        .ToArray();

    private readonly UiThemeSelector _themeSelector;
    private readonly GoreIntensitySelector _goreSelector;
    private readonly MotionIntensitySelector _motionSelector;
    private readonly AutoCameraModeSelector _autoCameraSelector;
    private readonly SettingsChoiceSelector<UiScale> _uiScaleSelector;
    private readonly SettingsChoiceSelector<StartupDisplayMode>
        _displayModeSelector;
    private readonly SettingsChoiceSelector<UiChromeStyle> _uiChromeSelector;
    private readonly UiMenuLayout _layout;
    private readonly UiThemeSelectorLayout _selectorLayout;
    private readonly UiTextRoles _textRoles;
    private readonly UiEntranceMotion _entrance = new();
    private int _focusedControlIndex;

    public MenuOverlay(
        IReadOnlyList<UiTheme> themes,
        UiThemeStandards standards)
    {
        _themeSelector = new UiThemeSelector(themes, standards);
        _goreSelector = new GoreIntensitySelector(standards);
        _motionSelector = new MotionIntensitySelector(standards);
        _autoCameraSelector = new AutoCameraModeSelector(standards);
        _uiScaleSelector = new SettingsChoiceSelector<UiScale>(
            "UI SCALE",
            [
                UiScale.Auto,
                UiScale.Percent100,
                UiScale.Percent125,
                UiScale.Percent150,
                UiScale.Percent200,
            ],
            ["Auto", "100%", "125%", "150%", "200%"],
            "PREFERRED",
            standards);
        _displayModeSelector =
            new SettingsChoiceSelector<StartupDisplayMode>(
                "STARTUP DISPLAY",
                [
                    StartupDisplayMode.Windowed,
                    StartupDisplayMode.Fullscreen,
                ],
                ["Windowed", "Fullscreen"],
                "NEXT LAUNCH",
                standards);
        _uiChromeSelector = new SettingsChoiceSelector<UiChromeStyle>(
            "PANEL STYLE",
            [
                UiChromeStyle.Procedural,
                UiChromeStyle.NineSlice,
            ],
            ["Procedural", "Nine-Slice"],
            "ACTIVE",
            standards);
        _layout = standards.Shared.Menu;
        _selectorLayout = standards.Shared.Selector;
        _textRoles = standards.Shared.TextRoles;
    }

    public bool IsVisible { get; private set; }

    internal float ScrimOpacity => _entrance.ScrimOpacity;

    internal float EntranceOpacity => _entrance.PanelOpacity;

    /// <summary>
    /// Focus index 0 is the theme selector, indices 1..N are the N buttons,
    /// and the gore selector takes index N+1. Appending rather than
    /// interleaving leaves every existing button index unchanged.
    /// </summary>
    internal static int GoreSelectorControlIndex =>
        ButtonDefinitions.Length + 1;

    /// <summary>
    /// The motion selector is appended beside the gore selector and takes
    /// the new terminal index, one past <see cref="GoreSelectorControlIndex"/>.
    /// </summary>
    internal static int MotionSelectorControlIndex =>
        GoreSelectorControlIndex + 1;

    /// <summary>
    /// The auto-camera selector is appended below the motion selector and
    /// takes the new terminal index, one past
    /// <see cref="MotionSelectorControlIndex"/>.
    /// </summary>
    internal static int AutoCameraSelectorControlIndex =>
        MotionSelectorControlIndex + 1;

    internal static int UiScaleSelectorControlIndex =>
        AutoCameraSelectorControlIndex + 1;

    internal static int DisplayModeSelectorControlIndex =>
        UiScaleSelectorControlIndex + 1;

    /// <summary>
    /// The chrome selector is appended after the display-mode selector and
    /// takes the new terminal index, even though it is laid out in the
    /// button column, second from the top, directly under the theme
    /// selector. Appending rather than interleaving keeps every existing
    /// control's focus index unchanged; only the tab order, not the visual
    /// position, moved it to the end.
    /// </summary>
    internal static int UiChromeSelectorControlIndex =>
        DisplayModeSelectorControlIndex + 1;

    internal static int ControlCount => UiChromeSelectorControlIndex + 1;

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
        Math.Max(
            UiScaleContext.Pixels(layout.SelectorTopOffset) +
                (ButtonColumnSelectorCount *
                    UiScaleContext.Pixels(selectorLayout.Height)) +
                (ButtonColumnSelectorCount *
                    UiScaleContext.Pixels(SettingsSelectorGap)) +
                (buttonCount * UiScaleContext.Pixels(layout.ButtonHeight)) +
                (Math.Max(0, buttonCount - 1) *
                    UiScaleContext.Pixels(layout.ButtonGap)),
            UiScaleContext.Pixels(layout.SelectorTopOffset) +
                (SettingsSelectorCount *
                    UiScaleContext.Pixels(selectorLayout.Height)) +
                ((SettingsSelectorCount - 1) *
                    UiScaleContext.Pixels(SettingsSelectorGap)));

    public void Open()
    {
        IsVisible = true;
        _focusedControlIndex = 0;
        _entrance.Begin();
    }

    public void Close()
    {
        IsVisible = false;
        ResetVisualState();
        _entrance.Reset();
    }

    public MenuInteraction Update(
        InputEdges input,
        Rectangle screenBounds,
        string activeThemeId,
        GoreIntensity activeGoreIntensity,
        MotionIntensity activeMotionIntensity,
        AutoCameraMode activeAutoCameraMode,
        UiScale activeUiScale,
        StartupDisplayMode activeStartupDisplayMode,
        UiChromeStyle activeUiChromeStyle,
        TimeSpan elapsed)
    {
        if (!IsVisible)
        {
            return MenuInteraction.None;
        }

        _entrance.Advance(
            elapsed,
            activeMotionIntensity,
            UiEntranceMotion.ModalPanelDuration,
            hasScrim: true);
        Layout(screenBounds);

        // All seven selector instances advance their motion in one pass here,
        // before the early-returning interaction chain below. That chain
        // returns as soon as any one selector reports a selection; advancing
        // motion inside it would starve every selector below the one that
        // fired on that frame, stalling their transitions mid-flight.
        _themeSelector.AdvanceMotion(
            input, elapsed, activeMotionIntensity, activeThemeId);
        _goreSelector.AdvanceMotion(
            input, elapsed, activeMotionIntensity, activeGoreIntensity);
        _motionSelector.AdvanceMotion(
            input, elapsed, activeMotionIntensity, activeMotionIntensity);
        _autoCameraSelector.AdvanceMotion(
            input, elapsed, activeMotionIntensity, activeAutoCameraMode);
        _uiScaleSelector.AdvanceMotion(
            input, elapsed, activeMotionIntensity, activeUiScale);
        _displayModeSelector.AdvanceMotion(
            input, elapsed, activeMotionIntensity, activeStartupDisplayMode);
        _uiChromeSelector.AdvanceMotion(
            input, elapsed, activeMotionIntensity, activeUiChromeStyle);

        var focusDirection = ResolveKeyboardFocusDirection(input);

        var hoveredControlIndex = _themeSelector.Bounds.Contains(
            input.MousePosition)
            ? 0
            : -1;
        for (var index = 0; index < _buttons.Length; index++)
        {
            var button = _buttons[index];
            button.Update(
                input,
                elapsed,
                activeMotionIntensity,
                index + 1 == _focusedControlIndex);

            if (button.IsHovered)
            {
                hoveredControlIndex = index + 1;
            }
        }

        // Evaluated after the button loop so a hovered button is never
        // clobbered by a terminal control. The settings selectors are checked
        // in stacking order, lowest last, so the lowest wins when two of them
        // somehow overlap.
        if (_goreSelector.Bounds.Contains(input.MousePosition))
        {
            hoveredControlIndex = GoreSelectorControlIndex;
        }

        if (_motionSelector.Bounds.Contains(input.MousePosition))
        {
            hoveredControlIndex = MotionSelectorControlIndex;
        }

        if (_autoCameraSelector.Bounds.Contains(input.MousePosition))
        {
            hoveredControlIndex = AutoCameraSelectorControlIndex;
        }

        if (_uiScaleSelector.Bounds.Contains(input.MousePosition))
        {
            hoveredControlIndex = UiScaleSelectorControlIndex;
        }

        if (_displayModeSelector.Bounds.Contains(input.MousePosition))
        {
            hoveredControlIndex = DisplayModeSelectorControlIndex;
        }

        if (_uiChromeSelector.Bounds.Contains(input.MousePosition))
        {
            hoveredControlIndex = UiChromeSelectorControlIndex;
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
                elapsed,
                activeMotionIntensity,
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
                null,
                null,
                null,
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
                null,
                null,
                null,
                null,
                true);
        }

        var motionInteraction = _motionSelector.Update(
            input,
            _focusedControlIndex == MotionSelectorControlIndex,
            activeMotionIntensity);
        if (motionInteraction.SelectedMotionIntensity is
            { } selectedMotionIntensity)
        {
            return new MenuInteraction(
                ClientCommand.None,
                null,
                null,
                selectedMotionIntensity,
                null,
                null,
                null,
                true);
        }

        var autoCameraInteraction = _autoCameraSelector.Update(
            input,
            _focusedControlIndex == AutoCameraSelectorControlIndex,
            activeAutoCameraMode);
        if (autoCameraInteraction.SelectedAutoCameraMode is
            { } selectedAutoCameraMode)
        {
            return new MenuInteraction(
                ClientCommand.None,
                null,
                null,
                null,
                selectedAutoCameraMode,
                null,
                null,
                true);
        }

        var uiScaleInteraction = _uiScaleSelector.Update(
            input,
            _focusedControlIndex == UiScaleSelectorControlIndex,
            activeUiScale);
        if (uiScaleInteraction.SelectedValue is { } selectedUiScale)
        {
            return new MenuInteraction(
                ClientCommand.None,
                null,
                null,
                null,
                null,
                selectedUiScale,
                null,
                true);
        }

        var displayModeInteraction = _displayModeSelector.Update(
            input,
            _focusedControlIndex == DisplayModeSelectorControlIndex,
            activeStartupDisplayMode);
        if (displayModeInteraction.SelectedValue is { } selectedDisplayMode)
        {
            return new MenuInteraction(
                ClientCommand.None,
                null,
                null,
                null,
                null,
                null,
                selectedDisplayMode,
                true);
        }

        var uiChromeInteraction = _uiChromeSelector.Update(
            input,
            _focusedControlIndex == UiChromeSelectorControlIndex,
            activeUiChromeStyle);
        if (uiChromeInteraction.SelectedValue is { } selectedUiChromeStyle)
        {
            return new MenuInteraction(
                ClientCommand.None,
                null,
                null,
                null,
                null,
                null,
                null,
                true,
                selectedUiChromeStyle);
        }

        if (input.WasLeftMousePressed() &&
            IsButtonControlIndex(hoveredControlIndex))
        {
            return new MenuInteraction(
                _buttons[hoveredControlIndex - 1].Command,
                null,
                null,
                null,
                null,
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
                null,
                null,
                null,
                null,
                true);
        }

        return new MenuInteraction(
            ClientCommand.None,
            null,
            null,
            null,
            null,
            null,
            null,
            true);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Texture2D? chromeAtlas,
        UiFontSet fonts,
        Rectangle screenBounds,
        UiTheme theme,
        GoreIntensity activeGoreIntensity,
        MotionIntensity activeMotionIntensity,
        AutoCameraMode activeAutoCameraMode,
        UiScale activeUiScale,
        StartupDisplayMode activeStartupDisplayMode,
        UiChromeStyle activeUiChromeStyle)
    {
        if (!IsVisible)
        {
            return;
        }

        Layout(screenBounds);

        var scrimTheme = UiMotionTheme.WithOpacity(theme, ScrimOpacity);
        spriteBatch.Draw(
            pixel,
            screenBounds,
            scrimTheme.Colors.OverlayScrim);
        theme = UiMotionTheme.WithOpacity(theme, EntranceOpacity);

        var panelBounds = GetPanelBounds(screenBounds);
        var shadowOffset = UiScaleContext.Pixels(
            theme.Metrics.ShadowOffset);
        if (shadowOffset > 0)
        {
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    panelBounds.X + shadowOffset,
                    panelBounds.Y + shadowOffset,
                    panelBounds.Width,
                    panelBounds.Height),
                theme.Colors.CanvasBackground);
        }

        // A style of NineSlice with no loaded atlas falls back to the
        // Procedural path rather than crashing: chromeAtlas is nullable
        // because it is only populated once ArenaGame's LoadContent has run,
        // and this branch must stay safe for any caller that draws before
        // that content is available.
        if (activeUiChromeStyle == UiChromeStyle.NineSlice &&
            chromeAtlas is not null)
        {
            UiNineSlice.DrawPanel(
                spriteBatch,
                chromeAtlas,
                panelBounds,
                theme.Colors.PanelSurface,
                theme.Colors.PanelBorder,
                UiScaleContext.Pixels(NineSliceMarginPixels));
        }
        else
        {
            spriteBatch.Draw(pixel, panelBounds, theme.Colors.PanelSurface);
            UiPrimitives.DrawBorder(
                spriteBatch,
                pixel,
                panelBounds,
                theme.Colors.PanelBorder,
                Math.Max(
                    UiScaleContext.Pixels(1),
                    UiScaleContext.Pixels(theme.Metrics.BorderThickness)));
        }

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.MenuTitle),
            "HUKBO",
            new Vector2(
                panelBounds.Center.X,
                panelBounds.Top +
                    UiScaleContext.Pixels(_layout.TitleTopOffset)),
            theme.Colors.TextPrimary);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.MenuSubtitle),
            "Simulation controls",
            new Vector2(
                panelBounds.Center.X,
                panelBounds.Top +
                    UiScaleContext.Pixels(_layout.SubtitleTopOffset)),
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

        _motionSelector.Draw(
            spriteBatch,
            pixel,
            fonts,
            theme,
            activeMotionIntensity,
            _focusedControlIndex == MotionSelectorControlIndex);

        _autoCameraSelector.Draw(
            spriteBatch,
            pixel,
            fonts,
            theme,
            activeAutoCameraMode,
            _focusedControlIndex == AutoCameraSelectorControlIndex);

        _uiScaleSelector.Draw(
            spriteBatch,
            pixel,
            fonts,
            theme,
            activeUiScale,
            _focusedControlIndex == UiScaleSelectorControlIndex);

        _displayModeSelector.Draw(
            spriteBatch,
            pixel,
            fonts,
            theme,
            activeStartupDisplayMode,
            _focusedControlIndex == DisplayModeSelectorControlIndex);

        _uiChromeSelector.Draw(
            spriteBatch,
            pixel,
            fonts,
            theme,
            activeUiChromeStyle,
            _focusedControlIndex == UiChromeSelectorControlIndex);

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(_textRoles.MenuHelper),
            "Esc closes  |  Up/Down focus  |  Left/Right change",
            new Vector2(
                panelBounds.Center.X,
                panelBounds.Bottom -
                    UiScaleContext.Pixels(_layout.HelperBottomOffset)),
            theme.Colors.TextSecondary);
    }

    private void Layout(Rectangle screenBounds)
    {
        var panel = GetPanelBounds(screenBounds);
        var safeMargin = UiScaleContext.Pixels(SafeMargin);
        var columnGap = UiScaleContext.Pixels(ColumnGap);
        var selectorGap = UiScaleContext.Pixels(SettingsSelectorGap);
        var selectorHeight = UiScaleContext.Pixels(_selectorLayout.Height);
        var buttonWidth = Math.Min(
            UiScaleContext.Pixels(_layout.ButtonWidth),
            Math.Max(0, (panel.Width - columnGap - (safeMargin * 2)) / 2));
        var horizontalPadding = Math.Max(
            safeMargin,
            (panel.Width - (buttonWidth * 2) - columnGap) / 2);
        var buttonLeft = panel.Left + horizontalPadding;
        var settingsLeft = panel.Right - horizontalPadding - buttonWidth;
        _themeSelector.Bounds = new Rectangle(
            buttonLeft,
            panel.Top + UiScaleContext.Pixels(_layout.SelectorTopOffset),
            buttonWidth,
            selectorHeight);
        _uiChromeSelector.Bounds = new Rectangle(
            buttonLeft,
            _themeSelector.Bounds.Bottom + selectorGap,
            buttonWidth,
            selectorHeight);
        var buttonTop = _uiChromeSelector.Bounds.Bottom + selectorGap;

        for (var index = 0; index < _buttons.Length; index++)
        {
            _buttons[index].Bounds = new Rectangle(
                buttonLeft,
                buttonTop + (index *
                    (UiScaleContext.Pixels(_layout.ButtonHeight) +
                        UiScaleContext.Pixels(_layout.ButtonGap))),
                buttonWidth,
                UiScaleContext.Pixels(_layout.ButtonHeight));
        }

        _goreSelector.Bounds = new Rectangle(
            settingsLeft,
            panel.Top + UiScaleContext.Pixels(_layout.SelectorTopOffset),
            buttonWidth,
            selectorHeight);

        var motionSelectorTop = _goreSelector.Bounds.Bottom +
            selectorGap;
        _motionSelector.Bounds = new Rectangle(
            settingsLeft,
            motionSelectorTop,
            buttonWidth,
            selectorHeight);

        var autoCameraSelectorTop = _motionSelector.Bounds.Bottom +
            selectorGap;
        _autoCameraSelector.Bounds = new Rectangle(
            settingsLeft,
            autoCameraSelectorTop,
            buttonWidth,
            selectorHeight);

        var uiScaleSelectorTop = _autoCameraSelector.Bounds.Bottom +
            selectorGap;
        _uiScaleSelector.Bounds = new Rectangle(
            settingsLeft,
            uiScaleSelectorTop,
            buttonWidth,
            selectorHeight);

        var displayModeSelectorTop = _uiScaleSelector.Bounds.Bottom +
            selectorGap;
        _displayModeSelector.Bounds = new Rectangle(
            settingsLeft,
            displayModeSelectorTop,
            buttonWidth,
            selectorHeight);
    }

    internal Rectangle GetPanelBounds(Rectangle screenBounds)
    {
        var safeMargin = UiScaleContext.Pixels(SafeMargin);
        var availableWidth = Math.Max(0, screenBounds.Width - (safeMargin * 2));
        var availableHeight = Math.Max(0, screenBounds.Height - (safeMargin * 2));
        var width = Math.Min(
            UiScaleContext.Pixels(ResponsivePanelWidth),
            availableWidth);
        var height = Math.Min(
            UiScaleContext.Pixels(ResponsivePanelHeight),
            availableHeight);
        return new Rectangle(
            screenBounds.Center.X - (width / 2),
            screenBounds.Center.Y - (height / 2),
            width,
            height);
    }

    internal IReadOnlyList<Rectangle> GetControlBounds(Rectangle screenBounds)
    {
        Layout(screenBounds);

        // The chrome selector's bounds sit right after the theme selector's,
        // matching its position in the button column. Kept out of the
        // trailing position deliberately: SettingsColumnFormulaMatchesActual-
        // SettingsColumnGeometry reads the last element of this list as the
        // settings column's bottom-most control, and the chrome selector is
        // not in that column.
        return
        [
            _themeSelector.Bounds,
            _uiChromeSelector.Bounds,
            .. _buttons.Select(button => button.Bounds),
            _goreSelector.Bounds,
            _motionSelector.Bounds,
            _autoCameraSelector.Bounds,
            _uiScaleSelector.Bounds,
            _displayModeSelector.Bounds,
        ];
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

    internal static int ResolveKeyboardFocusDirection(InputEdges input)
    {
        if (input.WasPressed(Keys.Tab))
        {
            return input.IsDown(Keys.LeftShift) ||
                input.IsDown(Keys.RightShift)
                    ? -1
                    : 1;
        }

        if (input.WasPressed(Keys.Down) || input.WasPressed(Keys.S))
        {
            return 1;
        }

        return input.WasPressed(Keys.Up) || input.WasPressed(Keys.W)
            ? -1
            : 0;
    }

    private void ResetVisualState()
    {
        foreach (var button in _buttons)
        {
            button.ResetVisualState();
        }
    }
}
