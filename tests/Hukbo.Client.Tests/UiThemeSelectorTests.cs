using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.Tests;

public sealed class UiThemeSelectorTests
{
    [Fact]
    public void ExposesSixOrderedNamesAndVisibleSelectedMarker()
    {
        var selector = CreateSelector();

        Assert.Equal(
            [
                "Command",
                "Field Manual",
                "Signal",
                "Broadcast",
                "High Contrast",
                "Cebu 1521 — Provisional",
            ],
            selector.ThemeNames);
        Assert.Equal("ACTIVE  -  1 / 6", selector.GetSelectedMarkerText("command"));
        Assert.Equal(
            "ACTIVE  -  6 / 6",
            selector.GetSelectedMarkerText("datu-court"));
        Assert.Equal(
            "PROVISIONAL RECONSTRUCTION",
            UiThemeSelector.GetSelectorLabelText("datu-court"));
    }

    [Fact]
    public void ProvisionalMarkerFitsTheArrowSafeMinimumViewportColumn()
    {
        var catalog = LoadCatalog();
        var menu = new MenuOverlay(catalog.Themes, catalog.Standards);
        var selector = new UiThemeSelector(catalog.Themes, catalog.Standards)
        {
            Bounds = menu.GetControlBounds(
                new Rectangle(0, 0, 1024, 720))[0],
        };
        var marker = selector.GetSelectedMarkerText("datu-court");
        var safeWidth = selector.NextBounds.Left -
            selector.PreviousBounds.Right;
        var estimatedWidth = marker.Length *
            UiFontRamp.GetApproximateAdvancePx(
                catalog.Standards.Shared.TextRoles.SelectorMarker);

        Assert.True(
            estimatedWidth <= safeWidth,
            $"Marker needs {estimatedWidth}px but the arrow-safe column " +
            $"offers {safeWidth}px.");

        var evidenceLabel = UiThemeSelector.GetSelectorLabelText("datu-court");
        var evidenceWidth = evidenceLabel.Length *
            UiFontRamp.GetApproximateAdvancePx(
                catalog.Standards.Shared.TextRoles.SelectorLabel);
        Assert.True(evidenceWidth <= selector.Bounds.Width);
    }

    [Fact]
    public void PreviousAndNextWrapAtBothEnds()
    {
        var selector = CreateSelector();

        Assert.Equal("datu-court", selector.GetPreviousId("command"));
        Assert.Equal("command", selector.GetNextId("datu-court"));
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
            "datu-court",
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
        var catalog = LoadCatalog();
        return new UiThemeSelector(catalog.Themes, catalog.Standards);
    }

    private static UiThemeCatalog LoadCatalog()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");
        return UiThemeCatalog.Load(path);
    }
}
