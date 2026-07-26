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
}
