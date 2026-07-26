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
    public void TheGoreSelectorTakesTheTerminalIndexAfterEveryButton()
    {
        Assert.Equal(
            MenuOverlay.ButtonDefinitions.Length + 2,
            MenuOverlay.ControlCount);
        Assert.Equal(
            MenuOverlay.ButtonDefinitions.Length + 1,
            MenuOverlay.GoreSelectorControlIndex);
    }

    [Fact]
    public void KeyboardFocusWrapsThroughTheTerminalGoreSelectorIndex()
    {
        var controlCount = MenuOverlay.ControlCount;
        var goreIndex = MenuOverlay.GoreSelectorControlIndex;

        Assert.Equal(
            goreIndex,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: 0,
                keyboardDirection: -1,
                hoveredIndex: -1,
                controlCount: controlCount));
        Assert.Equal(
            0,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: goreIndex,
                keyboardDirection: 1,
                hoveredIndex: -1,
                controlCount: controlCount));
        Assert.Equal(
            goreIndex,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: goreIndex - 1,
                keyboardDirection: 1,
                hoveredIndex: -1,
                controlCount: controlCount));
    }

    [Fact]
    public void HoveringTheGoreSelectorMovesFocusToItsTerminalIndex()
    {
        var resolved = MenuOverlay.ResolveFocusedControlIndex(
            currentIndex: 0,
            keyboardDirection: 0,
            hoveredIndex: MenuOverlay.GoreSelectorControlIndex,
            controlCount: MenuOverlay.ControlCount);

        Assert.Equal(MenuOverlay.GoreSelectorControlIndex, resolved);
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
