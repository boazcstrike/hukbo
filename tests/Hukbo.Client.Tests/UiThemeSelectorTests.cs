using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.Tests;

public sealed class UiThemeSelectorTests
{
    [Fact]
    public void ExposesFiveOrderedNamesAndVisibleSelectedMarker()
    {
        var selector = CreateSelector();

        Assert.Equal(
            [
                "Command",
                "Field Manual",
                "Signal",
                "Broadcast",
                "High Contrast",
            ],
            selector.ThemeNames);
        Assert.Equal("ACTIVE  -  1 / 5", selector.GetSelectedMarkerText("command"));
    }

    [Fact]
    public void PreviousAndNextWrapAtBothEnds()
    {
        var selector = CreateSelector();

        Assert.Equal("high-contrast", selector.GetPreviousId("command"));
        Assert.Equal("command", selector.GetNextId("high-contrast"));
    }

    [Theory]
    [InlineData(Keys.Left, "field-manual", "command")]
    [InlineData(Keys.Right, "field-manual", "signal")]
    [InlineData(Keys.Enter, "field-manual", "signal")]
    [InlineData(Keys.Space, "field-manual", "signal")]
    public void FocusedKeyboardActivationSelectsAdjacentTheme(
        Keys key,
        string currentId,
        string expectedId)
    {
        var selector = CreateSelector();

        Assert.Equal(
            expectedId,
            selector.GetKeyboardSelection(key, true, currentId));
        Assert.Null(selector.GetKeyboardSelection(key, false, currentId));
    }

    [Fact]
    public void PointerActivationSelectsTargetButHoverDoesNot()
    {
        var selector = CreateSelector();
        selector.Bounds = new Rectangle(100, 100, 280, 96);

        Assert.Equal(
            "high-contrast",
            selector.GetPointerSelection(
                selector.PreviousBounds.Center,
                true,
                "command"));
        Assert.Equal(
            "field-manual",
            selector.GetPointerSelection(
                selector.NextBounds.Center,
                true,
                "command"));
        Assert.Null(
            selector.GetPointerSelection(
                selector.NextBounds.Center,
                false,
                "command"));
    }

    private static UiThemeSelector CreateSelector()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");
        var catalog = UiThemeCatalog.Load(path);
        return new UiThemeSelector(catalog.Themes, catalog.Standards);
    }
}
