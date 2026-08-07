using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.Tests;

public sealed class MotionIntensitySelectorTests
{
    [Fact]
    public void ExposesThreeOrderedNamesAndVisibleSelectedMarker()
    {
        var selector = CreateSelector();

        Assert.Equal(
            ["Off", "Reduced", "Full"],
            selector.OptionNames);
        Assert.Equal(
            "ACTIVE  -  2 / 3",
            selector.GetSelectedMarkerText(MotionIntensity.Reduced));
    }

    [Fact]
    public void PreviousAndNextWrapAtBothEnds()
    {
        var selector = CreateSelector();

        Assert.Equal(
            MotionIntensity.Full,
            selector.GetPrevious(MotionIntensity.Off));
        Assert.Equal(
            MotionIntensity.Off,
            selector.GetNext(MotionIntensity.Full));
    }

    [Fact]
    public void AnUndefinedCurrentValueResolvesToTheFirstOption()
    {
        var selector = CreateSelector();

        Assert.Equal(
            MotionIntensity.Reduced,
            selector.GetNext((MotionIntensity)99));
        Assert.Equal("1 / 3", selector.GetPositionText((MotionIntensity)99));
    }

    // The value parameters are ints because MotionIntensity is internal and a
    // public test signature cannot expose it.
    [Theory]
    [InlineData(
        Keys.Left,
        (int)MotionIntensity.Reduced,
        (int)MotionIntensity.Off)]
    [InlineData(
        Keys.Right,
        (int)MotionIntensity.Reduced,
        (int)MotionIntensity.Full)]
    [InlineData(
        Keys.Enter,
        (int)MotionIntensity.Reduced,
        (int)MotionIntensity.Full)]
    [InlineData(
        Keys.Space,
        (int)MotionIntensity.Reduced,
        (int)MotionIntensity.Full)]
    public void FocusedKeyboardActivationSelectsTheAdjacentValue(
        Keys key,
        int currentValue,
        int expectedValue)
    {
        var selector = CreateSelector();
        var current = (MotionIntensity)currentValue;

        Assert.Equal(
            (MotionIntensity)expectedValue,
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
                MotionIntensity.Reduced));
    }

    [Fact]
    public void PointerActivationSelectsTargetButHoverDoesNot()
    {
        var selector = CreateSelector();
        selector.Bounds = new Rectangle(100, 100, 280, 96);

        Assert.Equal(
            MotionIntensity.Off,
            selector.GetPointerSelection(
                selector.PreviousBounds.Center,
                true,
                MotionIntensity.Reduced));
        Assert.Equal(
            MotionIntensity.Full,
            selector.GetPointerSelection(
                selector.NextBounds.Center,
                true,
                MotionIntensity.Reduced));
        Assert.Null(
            selector.GetPointerSelection(
                selector.NextBounds.Center,
                false,
                MotionIntensity.Reduced));
        Assert.Null(
            selector.GetPointerSelection(
                selector.Bounds.Center,
                true,
                MotionIntensity.Reduced));
    }

    [Fact]
    public void ArrowTargetsMeetTheConfiguredMinimumTargetSize()
    {
        var standards = LoadStandards();
        var selector = new MotionIntensitySelector(standards);
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

    private static MotionIntensitySelector CreateSelector() =>
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
