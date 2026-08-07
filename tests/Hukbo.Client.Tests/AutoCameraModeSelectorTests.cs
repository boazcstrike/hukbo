using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.Tests;

public sealed class AutoCameraModeSelectorTests
{
    [Fact]
    public void ExposesThreeOrderedNamesAndVisibleSelectedMarker()
    {
        var selector = CreateSelector();

        Assert.Equal(
            ["Off", "Assisted", "Follow"],
            selector.OptionNames);
        Assert.Equal(
            "ACTIVE  -  2 / 3",
            selector.GetSelectedMarkerText(AutoCameraMode.Assisted));
    }

    [Fact]
    public void PreviousAndNextWrapAtBothEnds()
    {
        var selector = CreateSelector();

        Assert.Equal(
            AutoCameraMode.Follow,
            selector.GetPrevious(AutoCameraMode.Off));
        Assert.Equal(
            AutoCameraMode.Off,
            selector.GetNext(AutoCameraMode.Follow));
    }

    [Fact]
    public void AnUndefinedCurrentValueResolvesToTheFirstOption()
    {
        var selector = CreateSelector();

        Assert.Equal(
            AutoCameraMode.Assisted,
            selector.GetNext((AutoCameraMode)99));
        Assert.Equal("1 / 3", selector.GetPositionText((AutoCameraMode)99));
    }

    // The value parameters are ints because a public test signature reads
    // more clearly against the pinned numeric contract than against the enum.
    [Theory]
    [InlineData(
        Keys.Left,
        (int)AutoCameraMode.Assisted,
        (int)AutoCameraMode.Off)]
    [InlineData(
        Keys.Right,
        (int)AutoCameraMode.Assisted,
        (int)AutoCameraMode.Follow)]
    [InlineData(
        Keys.Enter,
        (int)AutoCameraMode.Assisted,
        (int)AutoCameraMode.Follow)]
    [InlineData(
        Keys.Space,
        (int)AutoCameraMode.Assisted,
        (int)AutoCameraMode.Follow)]
    public void FocusedKeyboardActivationSelectsTheAdjacentValue(
        Keys key,
        int currentValue,
        int expectedValue)
    {
        var selector = CreateSelector();
        var current = (AutoCameraMode)currentValue;

        Assert.Equal(
            (AutoCameraMode)expectedValue,
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
                AutoCameraMode.Assisted));
    }

    [Fact]
    public void PointerActivationSelectsTargetButHoverDoesNot()
    {
        var selector = CreateSelector();
        selector.Bounds = new Rectangle(100, 100, 280, 96);

        Assert.Equal(
            AutoCameraMode.Off,
            selector.GetPointerSelection(
                selector.PreviousBounds.Center,
                true,
                AutoCameraMode.Assisted));
        Assert.Equal(
            AutoCameraMode.Follow,
            selector.GetPointerSelection(
                selector.NextBounds.Center,
                true,
                AutoCameraMode.Assisted));
        Assert.Null(
            selector.GetPointerSelection(
                selector.NextBounds.Center,
                false,
                AutoCameraMode.Assisted));
        Assert.Null(
            selector.GetPointerSelection(
                selector.Bounds.Center,
                true,
                AutoCameraMode.Assisted));
    }

    [Fact]
    public void ArrowTargetsMeetTheConfiguredMinimumTargetSize()
    {
        var standards = LoadStandards();
        var selector = new AutoCameraModeSelector(standards);
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

    private static AutoCameraModeSelector CreateSelector() =>
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
