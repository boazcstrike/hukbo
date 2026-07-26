using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.Tests;

public sealed class GoreIntensitySelectorTests
{
    [Fact]
    public void ExposesThreeOrderedNamesAndVisibleSelectedMarker()
    {
        var selector = CreateSelector();

        Assert.Equal(
            ["Off", "Stylized", "Full"],
            selector.OptionNames);
        Assert.Equal(
            "ACTIVE  -  2 / 3",
            selector.GetSelectedMarkerText(GoreIntensity.Stylized));
    }

    [Fact]
    public void PreviousAndNextWrapAtBothEnds()
    {
        var selector = CreateSelector();

        Assert.Equal(
            GoreIntensity.Full,
            selector.GetPrevious(GoreIntensity.Off));
        Assert.Equal(
            GoreIntensity.Off,
            selector.GetNext(GoreIntensity.Full));
    }

    [Fact]
    public void AnUndefinedCurrentValueResolvesToTheFirstOption()
    {
        var selector = CreateSelector();

        Assert.Equal(
            GoreIntensity.Stylized,
            selector.GetNext((GoreIntensity)99));
        Assert.Equal("1 / 3", selector.GetPositionText((GoreIntensity)99));
    }

    // The value parameters are ints because GoreIntensity is internal and a
    // public test signature cannot expose it.
    [Theory]
    [InlineData(Keys.Left, (int)GoreIntensity.Stylized, (int)GoreIntensity.Off)]
    [InlineData(Keys.Right, (int)GoreIntensity.Stylized, (int)GoreIntensity.Full)]
    [InlineData(Keys.Enter, (int)GoreIntensity.Stylized, (int)GoreIntensity.Full)]
    [InlineData(Keys.Space, (int)GoreIntensity.Stylized, (int)GoreIntensity.Full)]
    public void FocusedKeyboardActivationSelectsTheAdjacentValue(
        Keys key,
        int currentValue,
        int expectedValue)
    {
        var selector = CreateSelector();
        var current = (GoreIntensity)currentValue;

        Assert.Equal(
            (GoreIntensity)expectedValue,
            selector.GetKeyboardSelection(key, true, current));
        Assert.Null(selector.GetKeyboardSelection(key, false, current));
    }

    [Fact]
    public void UnrelatedKeysSelectNothingEvenWhenFocused()
    {
        var selector = CreateSelector();

        Assert.Null(
            selector.GetKeyboardSelection(
                Keys.Down,
                true,
                GoreIntensity.Stylized));
    }

    [Fact]
    public void PointerActivationSelectsTargetButHoverDoesNot()
    {
        var selector = CreateSelector();
        selector.Bounds = new Rectangle(100, 100, 280, 96);

        Assert.Equal(
            GoreIntensity.Off,
            selector.GetPointerSelection(
                selector.PreviousBounds.Center,
                true,
                GoreIntensity.Stylized));
        Assert.Equal(
            GoreIntensity.Full,
            selector.GetPointerSelection(
                selector.NextBounds.Center,
                true,
                GoreIntensity.Stylized));
        Assert.Null(
            selector.GetPointerSelection(
                selector.NextBounds.Center,
                false,
                GoreIntensity.Stylized));
        Assert.Null(
            selector.GetPointerSelection(
                selector.Bounds.Center,
                true,
                GoreIntensity.Stylized));
    }

    [Fact]
    public void ArrowTargetsMeetTheConfiguredMinimumTargetSize()
    {
        var standards = LoadStandards();
        var selector = new GoreIntensitySelector(standards);
        selector.Bounds = new Rectangle(
            0,
            0,
            standards.Shared.Menu.ButtonWidth,
            standards.Shared.Selector.Height);

        var minimum = standards.Shared.Selector.MinimumTargetSize;
        Assert.True(selector.PreviousBounds.Width >= minimum);
        Assert.True(selector.PreviousBounds.Height >= minimum);
        Assert.True(selector.NextBounds.Width >= minimum);
        Assert.True(selector.NextBounds.Height >= minimum);
        Assert.False(selector.PreviousBounds.Intersects(selector.NextBounds));
    }

    private static GoreIntensitySelector CreateSelector() =>
        new(LoadStandards());

    private static UiThemeStandards LoadStandards()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");
        return UiThemeCatalog.Load(path).Standards;
    }
}
