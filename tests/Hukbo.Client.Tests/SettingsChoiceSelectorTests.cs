using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.Tests;

/// <summary>
/// Shared-mechanics contract for <see cref="SettingsChoiceSelector{T}"/>,
/// exercised through two independently-shaped generic instantiations (a
/// five-option enum and a two-option enum) so the contract is proven against
/// the type, not against one concrete option table. This is also the home
/// for every mechanic that <see cref="MotionIntensitySelector"/>,
/// <see cref="GoreIntensitySelector"/>, and <see cref="AutoCameraModeSelector"/>
/// inherit by delegation: each of those keeps only a thin per-type wiring
/// test (construction, option names, persistence key) in its own file.
/// </summary>
public sealed class SettingsChoiceSelectorTests
{
    [Fact]
    public void UiScaleSelectorCyclesAllPersistedTiers()
    {
        var selector = CreateUiScaleSelector();

        Assert.Equal(
            ["Auto", "100%", "125%", "150%", "200%"],
            selector.OptionNames);
        Assert.Equal(
            UiScale.Percent200,
            selector.GetPrevious(UiScale.Auto));
        Assert.Equal(
            UiScale.Auto,
            selector.GetNext(UiScale.Percent200));
    }

    [Fact]
    public void DisplaySelectorStatesThatChoiceAppliesNextLaunch()
    {
        var selector = CreateDisplaySelector();

        Assert.Equal(
            "NEXT LAUNCH  -  2 / 2",
            selector.GetSelectedMarkerText(StartupDisplayMode.Fullscreen));
    }

    [Fact]
    public void AnUndefinedCurrentValueResolvesToTheFirstOption()
    {
        var selector = CreateUiScaleSelector();

        Assert.Equal(
            UiScale.Percent100,
            selector.GetNext((UiScale)99));
        Assert.Equal("1 / 5", selector.GetPositionText((UiScale)99));
    }

    [Theory]
    [InlineData(Keys.Left, UiScale.Percent125, UiScale.Percent100)]
    [InlineData(Keys.Right, UiScale.Percent125, UiScale.Percent150)]
    [InlineData(Keys.Enter, UiScale.Percent125, UiScale.Percent150)]
    [InlineData(Keys.Space, UiScale.Percent125, UiScale.Percent150)]
    public void FocusedKeyboardActivationSelectsTheAdjacentValue(
        Keys key,
        UiScale currentValue,
        UiScale expectedValue)
    {
        var selector = CreateUiScaleSelector();

        Assert.Equal(
            expectedValue,
            selector.GetKeyboardSelection(key, true, currentValue));
        Assert.Null(selector.GetKeyboardSelection(key, false, currentValue));
    }

    [Fact]
    public void UnrelatedKeysSelectNothingEvenWhenFocused()
    {
        var selector = CreateUiScaleSelector();

        Assert.Null(
            selector.GetKeyboardSelection(
                Keys.Down,
                true,
                UiScale.Percent125));
    }

    [Fact]
    public void PointerActivationSelectsTargetButHoverDoesNot()
    {
        var selector = CreateDisplaySelector();
        selector.Bounds = new Rectangle(100, 100, 280, 96);

        Assert.Equal(
            StartupDisplayMode.Windowed,
            selector.GetPointerSelection(
                selector.PreviousBounds.Center,
                true,
                StartupDisplayMode.Fullscreen));
        Assert.Equal(
            StartupDisplayMode.Fullscreen,
            selector.GetPointerSelection(
                selector.NextBounds.Center,
                true,
                StartupDisplayMode.Windowed));
        Assert.Null(
            selector.GetPointerSelection(
                selector.NextBounds.Center,
                false,
                StartupDisplayMode.Windowed));
        Assert.Null(
            selector.GetPointerSelection(
                selector.Bounds.Center,
                true,
                StartupDisplayMode.Windowed));
    }

    [Fact]
    public void ArrowTargetsMeetTheConfiguredMinimum()
    {
        var standards = LoadStandards();
        var selector = CreateDisplaySelector();
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

    private static SettingsChoiceSelector<UiScale> CreateUiScaleSelector() =>
        new(
            "UI SCALE",
            [
                UiScale.Auto,
                UiScale.Percent100,
                UiScale.Percent125,
                UiScale.Percent150,
                UiScale.Percent200,
            ],
            ["Auto", "100%", "125%", "150%", "200%"],
            "ACTIVE",
            LoadStandards());

    private static SettingsChoiceSelector<StartupDisplayMode>
        CreateDisplaySelector() =>
        new(
            "STARTUP DISPLAY",
            [StartupDisplayMode.Windowed, StartupDisplayMode.Fullscreen],
            ["Windowed", "Fullscreen"],
            "NEXT LAUNCH",
            LoadStandards());

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
