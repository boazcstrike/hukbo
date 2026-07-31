using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

[Collection(UiScaleContextCollectionDefinition.Name)]
public sealed class ArenaGameResponsiveChromeTests
{
    [Fact]
    public void MinimumViewportReservesStatusTextBeforeTheControlBar()
    {
        try
        {
            UiScaleContext.Set(UiScale.Percent100);
            var screen = new Rectangle(0, 0, 1024, 720);
            var controlBar = new ControlBar();
            var controlBounds = controlBar.GetBounds(screen);

            var textBounds = ArenaGame.CalculateStatusTextBounds(
                screen,
                controlBounds);

            Assert.True(screen.Contains(textBounds));
            Assert.True(textBounds.Right < controlBounds.Left);
            Assert.True(textBounds.Width > 0);
        }
        finally
        {
            UiScaleContext.Set(UiScale.Percent100);
        }
    }

    [Theory]
    [InlineData("short", 10, "short")]
    [InlineData("0123456789", 7, "0123...")]
    [InlineData("0123456789", 3, "012")]
    [InlineData("0123456789", 0, "")]
    public void StatusLineClippingNeverExceedsItsCharacterBudget(
        string text,
        int maximumCharacters,
        string expected)
    {
        var clipped = ArenaGame.ClipStatusLine(text, maximumCharacters);

        Assert.Equal(expected, clipped);
        Assert.True(clipped.Length <= maximumCharacters);
    }

    [Fact]
    public void ViewportDiagnosticsPinClientViewportAndBackBufferSeparately()
    {
        var snapshot = ArenaGame.CreateViewportDiagnosticSnapshot(
            clientWidth: 1912,
            clientHeight: 1041,
            viewportWidth: 1912,
            viewportHeight: 1041,
            backBufferWidth: 1920,
            backBufferHeight: 1080,
            displayWidth: 2560,
            displayHeight: 1440,
            uiScale: "Percent125",
            windowMode: "Windowed");

        Assert.Equal("1912x1041", snapshot.ClientDimensions);
        Assert.Equal("1912x1041", snapshot.ViewportDimensions);
        Assert.Equal(1920, snapshot.BackBufferWidth);
        Assert.Equal(1080, snapshot.BackBufferHeight);
        Assert.Equal("2560x1440", snapshot.DisplayDimensions);
        Assert.Equal("Percent125", snapshot.UiScale);
        Assert.Equal("Windowed", snapshot.WindowMode);
    }
}
