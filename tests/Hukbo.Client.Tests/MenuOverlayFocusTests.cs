using Hukbo.Client.Settings;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.Tests;

public sealed class MenuOverlayFocusTests
{
    [Fact]
    public void KeyboardMoveWinsOverStationaryMouseHover()
    {
        var resolved = MenuOverlay.ResolveFocusedControlIndex(
            currentIndex: 0,
            keyboardDirection: 1,
            hoveredIndex: 0,
            controlCount: 6);

        Assert.Equal(1, resolved);
    }

    [Fact]
    public void HoverChangesFocusWhenKeyboardDidNotMove()
    {
        var resolved = MenuOverlay.ResolveFocusedControlIndex(
            currentIndex: 1,
            keyboardDirection: 0,
            hoveredIndex: 4,
            controlCount: 6);

        Assert.Equal(4, resolved);
    }

    [Fact]
    public void KeyboardFocusWrapsAtBothEnds()
    {
        Assert.Equal(
            5,
            MenuOverlay.ResolveFocusedControlIndex(0, -1, 0, 6));
        Assert.Equal(
            0,
            MenuOverlay.ResolveFocusedControlIndex(5, 1, 5, 6));
    }

    [Theory]
    [InlineData(Keys.Tab, null, 1)]
    [InlineData(Keys.Tab, Keys.LeftShift, -1)]
    [InlineData(Keys.Tab, Keys.RightShift, -1)]
    [InlineData(Keys.Down, null, 1)]
    [InlineData(Keys.Up, null, -1)]
    public void KeyboardDirectionHonorsBackwardTab(
        Keys pressed,
        Keys? heldModifier,
        int expected)
    {
        var keys = heldModifier is { } modifier
            ? new KeyboardState(pressed, modifier)
            : new KeyboardState(pressed);
        var input = new InputEdges(
            new KeyboardState(),
            keys,
            default,
            default);

        Assert.Equal(
            expected,
            MenuOverlay.ResolveKeyboardFocusDirection(input));
    }

    [Fact]
    public void TheGoreSelectorTakesTheIndexAfterEveryButton()
    {
        Assert.Equal(
            MenuOverlay.ButtonDefinitions.Length + 1,
            MenuOverlay.GoreSelectorControlIndex);
    }

    /// <summary>
    /// The settings selectors occupy the right column in the order they were
    /// added: gore, motion, auto camera, UI scale, then startup display. The
    /// panel-style selector was added after all of them and, despite sitting
    /// in the button column visually, takes the next control index in the
    /// same allocation chain rather than a column-relative one. Each new
    /// selector takes the terminal index and grows
    /// <see cref="MenuOverlay.ControlCount"/> by one, which leaves every
    /// existing index unchanged.
    /// </summary>
    [Fact]
    public void TheSettingsSelectorsStackInTheOrderTheyWereAdded()
    {
        Assert.Equal(
            MenuOverlay.GoreSelectorControlIndex + 1,
            MenuOverlay.MotionSelectorControlIndex);
        Assert.Equal(
            MenuOverlay.MotionSelectorControlIndex + 1,
            MenuOverlay.AutoCameraSelectorControlIndex);
        Assert.Equal(
            MenuOverlay.AutoCameraSelectorControlIndex + 1,
            MenuOverlay.UiScaleSelectorControlIndex);
        Assert.Equal(
            MenuOverlay.UiScaleSelectorControlIndex + 1,
            MenuOverlay.DisplayModeSelectorControlIndex);
        Assert.Equal(
            MenuOverlay.DisplayModeSelectorControlIndex + 1,
            MenuOverlay.UiChromeSelectorControlIndex);
        Assert.Equal(
            MenuOverlay.UiChromeSelectorControlIndex + 1,
            MenuOverlay.ControlCount);
    }

    [Fact]
    public void KeyboardFocusWrapsThroughTheTerminalChromeSelectorIndex()
    {
        var controlCount = MenuOverlay.ControlCount;
        var chromeIndex = MenuOverlay.UiChromeSelectorControlIndex;

        Assert.Equal(
            chromeIndex,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: 0,
                keyboardDirection: -1,
                hoveredIndex: -1,
                controlCount: controlCount));
        Assert.Equal(
            0,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: chromeIndex,
                keyboardDirection: 1,
                hoveredIndex: -1,
                controlCount: controlCount));
        Assert.Equal(
            chromeIndex,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: chromeIndex - 1,
                keyboardDirection: 1,
                hoveredIndex: -1,
                controlCount: controlCount));
    }

    [Fact]
    public void KeyboardFocusMovesFromGoreToMotionGoingForward()
    {
        Assert.Equal(
            MenuOverlay.MotionSelectorControlIndex,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: MenuOverlay.GoreSelectorControlIndex,
                keyboardDirection: 1,
                hoveredIndex: -1,
                controlCount: MenuOverlay.ControlCount));
    }

    [Fact]
    public void HoveringTheGoreSelectorMovesFocusToItsIndex()
    {
        var resolved = MenuOverlay.ResolveFocusedControlIndex(
            currentIndex: 0,
            keyboardDirection: 0,
            hoveredIndex: MenuOverlay.GoreSelectorControlIndex,
            controlCount: MenuOverlay.ControlCount);

        Assert.Equal(MenuOverlay.GoreSelectorControlIndex, resolved);
    }

    [Fact]
    public void HoveringTheMotionSelectorMovesFocusToItsTerminalIndex()
    {
        var resolved = MenuOverlay.ResolveFocusedControlIndex(
            currentIndex: 0,
            keyboardDirection: 0,
            hoveredIndex: MenuOverlay.MotionSelectorControlIndex,
            controlCount: MenuOverlay.ControlCount);

        Assert.Equal(MenuOverlay.MotionSelectorControlIndex, resolved);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void OnlyTheButtonBandActivatesAButton(int index, bool isButton)
    {
        Assert.Equal(isButton, MenuOverlay.IsButtonControlIndex(index));
    }

    [Fact]
    public void ThePanelIsTallEnoughForEveryMenuControl()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");
        var standards = Theming.UiThemeCatalog.Load(path).Standards;

        var requiredHeight = MenuOverlay.CalculateContentBottomOffset(
            standards.Shared.Menu,
            standards.Shared.Selector,
            MenuOverlay.ButtonDefinitions.Length);

        var menu = new MenuOverlay(
            Theming.UiThemeCatalog.Load(path).Themes,
            standards);
        var panel = menu.GetPanelBounds(new(0, 0, 1280, 720));

        Assert.True(
            requiredHeight <=
            panel.Height - standards.Shared.Menu.HelperBottomOffset,
            $"Menu content needs {requiredHeight}px but the panel only " +
            $"offers {panel.Height} px.");
    }

    /// <summary>
    /// <see cref="MenuOverlay.CalculateContentBottomOffset"/> reserves height
    /// for the settings column (gore, motion, auto camera, UI scale, startup
    /// display) using a private selector-count constant, separately from the
    /// theme selector, which lives in the button column instead. This test
    /// reads the settings column's actual on-screen extent from
    /// <see cref="MenuOverlay.GetControlBounds"/> — skipping the theme
    /// selector and every button, which come first in that list — and checks
    /// it never exceeds what the formula reserved. If a selector is ever
    /// appended to the settings column in <c>Layout</c> without raising the
    /// private count constant that feeds the formula, the column grows past
    /// its reservation and this assertion fails, independently of whether
    /// <c>ThePanelIsTallEnoughForEveryMenuControl</c> still happens to pass.
    /// </summary>
    [Fact]
    public void SettingsColumnFormulaMatchesActualSettingsColumnGeometry()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");
        var catalog = Theming.UiThemeCatalog.Load(path);
        var standards = catalog.Standards;
        var menu = new MenuOverlay(catalog.Themes, standards);
        var screen = new Microsoft.Xna.Framework.Rectangle(0, 0, 1280, 720);

        var panel = menu.GetPanelBounds(screen);
        var controls = menu.GetControlBounds(screen);
        var settingsColumnControls = controls
            .Skip(1 + MenuOverlay.ButtonDefinitions.Length)
            .ToArray();
        var actualSettingsColumnBottomOffset =
            settingsColumnControls[^1].Bottom - panel.Top;

        var reservedHeight = MenuOverlay.CalculateContentBottomOffset(
            standards.Shared.Menu,
            standards.Shared.Selector,
            MenuOverlay.ButtonDefinitions.Length);

        Assert.True(
            actualSettingsColumnBottomOffset <= reservedHeight,
            $"Settings column now extends {actualSettingsColumnBottomOffset}px " +
            $"from the panel top, but CalculateContentBottomOffset only " +
            $"reserved {reservedHeight}px. A settings selector was likely " +
            "added without raising MenuOverlay.SettingsSelectorCount.");
    }

    [Theory]
    [InlineData(1024, 720)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(1440, 1920)]
    public void ResponsivePanelContainsEveryControl(int width, int height)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");
        var catalog = Theming.UiThemeCatalog.Load(path);
        var menu = new MenuOverlay(catalog.Themes, catalog.Standards);
        var screen = new Microsoft.Xna.Framework.Rectangle(
            0,
            0,
            width,
            height);
        var panel = menu.GetPanelBounds(screen);
        var controls = menu.GetControlBounds(screen);

        Assert.True(screen.Contains(panel));
        Assert.All(controls, control => Assert.True(panel.Contains(control)));
        for (var left = 0; left < controls.Count; left++)
        {
            for (var right = left + 1; right < controls.Count; right++)
            {
                Assert.False(
                    controls[left].Intersects(controls[right]),
                    $"Control {left} overlaps control {right} at {width}x{height}.");
            }
        }
    }

    [Fact]
    public void EntranceMotion_PreservesFinalControlGeometryAndOffSnaps()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");
        var catalog = Theming.UiThemeCatalog.Load(path);
        var menu = new MenuOverlay(catalog.Themes, catalog.Standards);
        var screen = new Microsoft.Xna.Framework.Rectangle(
            0,
            0,
            1280,
            720);
        menu.Open();

        menu.Update(
            new InputEdges(),
            screen,
            catalog.DefaultThemeId,
            GoreIntensity.Stylized,
            MotionIntensity.Reduced,
            AutoCameraMode.Assisted,
            UiScale.Percent100,
            StartupDisplayMode.Windowed,
            UiChromeStyle.Procedural,
            TimeSpan.FromMilliseconds(40));
        var controls = menu.GetControlBounds(screen).ToArray();

        Assert.InRange(menu.ScrimOpacity, 0.001f, 0.999f);
        Assert.InRange(menu.EntranceOpacity, 0.001f, 0.999f);

        menu.Update(
            new InputEdges(),
            screen,
            catalog.DefaultThemeId,
            GoreIntensity.Stylized,
            MotionIntensity.Off,
            AutoCameraMode.Assisted,
            UiScale.Percent100,
            StartupDisplayMode.Windowed,
            UiChromeStyle.Procedural,
            TimeSpan.Zero);

        Assert.Equal(1f, menu.ScrimOpacity);
        Assert.Equal(1f, menu.EntranceOpacity);
        Assert.Equal(controls, menu.GetControlBounds(screen));
    }
}
