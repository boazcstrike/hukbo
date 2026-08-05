using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.Tests;

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
    public void KeyboardAndPointerSelectionRequireActivation()
    {
        var selector = CreateDisplaySelector();
        selector.Bounds = new Rectangle(100, 100, 280, 96);

        Assert.Equal(
            StartupDisplayMode.Fullscreen,
            selector.GetKeyboardSelection(
                Keys.Right,
                true,
                StartupDisplayMode.Windowed));
        Assert.Null(
            selector.GetKeyboardSelection(
                Keys.Right,
                false,
                StartupDisplayMode.Windowed));
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

        Assert.True(
            selector.PreviousBounds.Width >=
            standards.Shared.Selector.MinimumTargetSize);
        Assert.True(
            selector.NextBounds.Width >=
            standards.Shared.Selector.MinimumTargetSize);
        Assert.False(
            selector.PreviousBounds.Intersects(selector.NextBounds));
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
