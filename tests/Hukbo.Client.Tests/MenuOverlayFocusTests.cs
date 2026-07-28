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

    [Fact]
    public void TheGoreSelectorTakesTheIndexAfterEveryButton()
    {
        Assert.Equal(
            MenuOverlay.ButtonDefinitions.Length + 1,
            MenuOverlay.GoreSelectorControlIndex);
    }

    /// <summary>
    /// The settings selectors stack below the button band in the order they
    /// were added: gore, then motion (VIS-032), then auto camera. Each new one
    /// takes the terminal index and grows
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
            MenuOverlay.ControlCount);
    }

    [Fact]
    public void KeyboardFocusWrapsThroughTheTerminalAutoCameraSelectorIndex()
    {
        var controlCount = MenuOverlay.ControlCount;
        var autoCameraIndex = MenuOverlay.AutoCameraSelectorControlIndex;

        Assert.Equal(
            autoCameraIndex,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: 0,
                keyboardDirection: -1,
                hoveredIndex: -1,
                controlCount: controlCount));
        Assert.Equal(
            0,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: autoCameraIndex,
                keyboardDirection: 1,
                hoveredIndex: -1,
                controlCount: controlCount));
        Assert.Equal(
            autoCameraIndex,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: autoCameraIndex - 1,
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

        Assert.True(
            requiredHeight <=
            standards.Shared.Menu.PanelHeight -
            standards.Shared.Menu.HelperBottomOffset,
            $"Menu content needs {requiredHeight}px but the panel only " +
            $"offers {standards.Shared.Menu.PanelHeight} px.");
    }
}
