using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

[Collection(UiScaleContextCollectionDefinition.Name)]
public sealed class UiScaledChromeTests
{
    [Theory]
    [InlineData(UiScale.Percent100)]
    [InlineData(UiScale.Percent125)]
    [InlineData(UiScale.Percent150)]
    [InlineData(UiScale.Percent200)]
    public void EverySelectorScalesItsArrowTargetsWithTheOuterBounds(
        UiScale scale)
    {
        var catalog = LoadCatalog();
        var standards = catalog.Standards;

        try
        {
            UiScaleContext.Set(scale);
            var bounds = new Rectangle(
                0,
                0,
                UiScaleContext.Pixels(standards.Shared.Menu.ButtonWidth),
                UiScaleContext.Pixels(standards.Shared.Selector.Height));
            var expectedArrowWidth = Math.Max(
                UiScaleContext.Pixels(
                    standards.Shared.Selector.ArrowWidth),
                UiScaleContext.Pixels(
                    standards.Shared.Selector.MinimumTargetSize));

            var theme = new UiThemeSelector(
                catalog.Themes,
                standards)
            {
                Bounds = bounds,
            };
            var gore = new GoreIntensitySelector(standards)
            {
                Bounds = bounds,
            };
            var motion = new MotionIntensitySelector(standards)
            {
                Bounds = bounds,
            };
            var camera = new AutoCameraModeSelector(standards)
            {
                Bounds = bounds,
            };
            var setting = new SettingsChoiceSelector<StartupDisplayMode>(
                "STARTUP DISPLAY",
                [
                    StartupDisplayMode.Windowed,
                    StartupDisplayMode.Fullscreen,
                ],
                ["Windowed", "Fullscreen"],
                "NEXT LAUNCH",
                standards)
            {
                Bounds = bounds,
            };

            AssertTarget(theme.PreviousBounds, theme.NextBounds);
            AssertTarget(gore.PreviousBounds, gore.NextBounds);
            AssertTarget(motion.PreviousBounds, motion.NextBounds);
            AssertTarget(camera.PreviousBounds, camera.NextBounds);
            AssertTarget(setting.PreviousBounds, setting.NextBounds);

            void AssertTarget(Rectangle previous, Rectangle next)
            {
                Assert.Equal(expectedArrowWidth, previous.Width);
                Assert.Equal(expectedArrowWidth, next.Width);
                Assert.True(
                    previous.Height >=
                    UiScaleContext.Pixels(
                        standards.Shared.Selector.MinimumTargetSize));
                Assert.False(previous.Intersects(next));
            }
        }
        finally
        {
            UiScaleContext.Set(UiScale.Percent100);
        }
    }

    [Theory]
    [InlineData(UiScale.Percent100)]
    [InlineData(UiScale.Percent125)]
    [InlineData(UiScale.Percent150)]
    [InlineData(UiScale.Percent200)]
    public void ArmyCompositionScalesEveryThemeDefinedMetric(UiScale scale)
    {
        var standards = LoadCatalog().Standards;
        var metrics = new UiArmyCompositionLayout(
            PanelWidth: 640,
            PanelHeight: 648,
            RowHeight: 44,
            RowGap: 8,
            StepperWidth: 148,
            ArrowWidth: 44);

        try
        {
            UiScaleContext.Set(scale);
            var screenBounds = new Rectangle(
                0,
                0,
                UiScaleContext.Pixels(1280),
                UiScaleContext.Pixels(720));
            var layout = ArmyCompositionPanel.CalculateLayout(
                screenBounds,
                metrics,
                standards.Shared.Selector);

            Assert.Equal(
                UiScaleContext.Pixels(metrics.PanelWidth),
                layout.PanelBounds.Width);
            Assert.Equal(
                UiScaleContext.Pixels(metrics.PanelHeight),
                layout.PanelBounds.Height);

            foreach (var row in layout.CategoryRows.Append(
                         layout.UnitsPerTeamRow))
            {
                Assert.Equal(
                    UiScaleContext.Pixels(metrics.RowHeight),
                    row.RowBounds.Height);
                Assert.Equal(
                    UiScaleContext.Pixels(metrics.StepperWidth),
                    row.MinusBounds.Width +
                    row.ValueBounds.Width +
                    row.PlusBounds.Width);
                Assert.Equal(
                    UiScaleContext.Pixels(metrics.ArrowWidth),
                    row.MinusBounds.Width);
                Assert.Equal(
                    UiScaleContext.Pixels(metrics.ArrowWidth),
                    row.PlusBounds.Width);
                Assert.True(layout.PanelBounds.Contains(row.RowBounds));
            }

            Assert.True(layout.PanelBounds.Contains(layout.ApplyBounds));
            Assert.True(layout.PanelBounds.Contains(layout.CancelBounds));
        }
        finally
        {
            UiScaleContext.Set(UiScale.Percent100);
        }
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
