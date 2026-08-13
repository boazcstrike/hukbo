using System.Collections.Immutable;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.Tests;

/// <summary>
/// UI-52 T5b — the army-composition stepper arrows reuse
/// <see cref="UiSelectorMotion"/> (T4) so they animate on the same class of
/// interaction as the five menu selectors, rather than snapping while the
/// rest of the menu eases. Pure per <c>hukbo-client-ui</c>: no
/// <c>SpriteBatch</c>, no <c>ArenaGame</c>. Reads colours through the test
/// seam on <see cref="ArmyCompositionPanel"/> and the pure
/// <see cref="ArmyCompositionPanel.ResolveArrowColor"/> helper.
/// </summary>
public sealed class ArmyCompositionArrowMotionTests
{
    private static readonly TimeSpan SelectorMarkerDuration =
        TimeSpan.FromMilliseconds(120);
    private static readonly Rectangle ScreenBounds = new(0, 0, 1280, 720);

    private static UiArmyCompositionLayout TestLayout =>
        new(
            PanelWidth: 640,
            PanelHeight: 648,
            RowHeight: 44,
            RowGap: 8,
            StepperWidth: 148,
            ArrowWidth: 44);

    private static readonly UiThemeColors Colors = new(
        CanvasBackground: Color.Black,
        ArenaSurface: Color.Black,
        ArenaBorder: Color.Black,
        StatusSurface: Color.Black,
        OverlayScrim: Color.Black,
        PanelSurface: Color.Black,
        PanelAlternate: Color.Black,
        PanelBorder: Color.Black,
        TextPrimary: Color.Blue,
        TextSecondary: Color.Red,
        TextDisabled: Color.Black,
        TextInverse: Color.Black,
        ActionDefault: Color.White,
        ActionHover: Color.Black,
        ActionFocus: Color.Yellow,
        ActionPressed: Color.Black,
        ActionActive: Color.Black,
        ActionDisabled: Color.Gray,
        StatusInfo: Color.Black,
        StatusSuccess: Color.Black,
        StatusWarning: Color.Black,
        StatusDanger: Color.Black,
        TeamA: Color.Black,
        TeamB: Color.Black,
        OtherFaction: Color.Black,
        Selection: Color.Green,
        NewEvent: Color.Black);

    private static ArmyCompositionPanel CreatePanel() =>
        new(
            new Hukbo.Client.UI.ArmyComposition(
                ImmutableArray.Create(50, 50, 50, 50),
                200),
            Hukbo.Core.Movement.MovementPresetId.BattlefieldRealismV10,
            TestLayout,
            TestStandards);

    private static UiThemeStandards TestStandards =>
        UiThemeCatalog.Load(
            Path.Combine(
                AppContext.BaseDirectory,
                "Content",
                "Themes",
                "ui-theme-standards.json")).Standards;

    private static InputEdges CreateInputAt(Point pointer)
    {
        var mouse = new MouseState(
            pointer.X,
            pointer.Y,
            0,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
        return new InputEdges(
            new KeyboardState(),
            new KeyboardState(),
            mouse,
            mouse);
    }

    [Fact]
    public void HoveredArrowRisesToTheHoverColorWhileOthersStayAtTheBaseColor()
    {
        var panel = CreatePanel();
        var layout = ArmyCompositionPanel.CalculateLayout(
            ScreenBounds,
            TestLayout,
            TestStandards.Shared.Selector);
        var hoveredMinus = layout.CategoryRows[0].MinusBounds;

        panel.Update(
            CreateInputAt(hoveredMinus.Center),
            ScreenBounds,
            SelectorMarkerDuration,
            MotionIntensity.Full);

        Assert.Equal(
            Colors.TextPrimary,
            panel.GetCategoryMinusArrowColor(0, Colors));
        Assert.Equal(
            Colors.TextSecondary,
            panel.GetCategoryPlusArrowColor(0, Colors));
        Assert.Equal(
            Colors.TextSecondary,
            panel.GetCategoryMinusArrowColor(1, Colors));
        Assert.Equal(
            Colors.TextSecondary,
            panel.GetUnitsPerTeamMinusArrowColor(Colors));
    }

    [Fact]
    public void MotionOffSnapsTheHoveredArrowToTheHoverColorInOneCall()
    {
        var panel = CreatePanel();
        var layout = ArmyCompositionPanel.CalculateLayout(
            ScreenBounds,
            TestLayout,
            TestStandards.Shared.Selector);
        var hoveredPlus = layout.UnitsPerTeamRow.PlusBounds;

        panel.Update(
            CreateInputAt(hoveredPlus.Center),
            ScreenBounds,
            TimeSpan.FromMilliseconds(1),
            MotionIntensity.Off);

        Assert.Equal(
            Colors.TextPrimary,
            panel.GetUnitsPerTeamPlusArrowColor(Colors));
    }

    [Fact]
    public void ADisabledArrowStaysPinnedToTheDisabledColorEvenWhenHovered()
    {
        var layout = ArmyCompositionPanel.CalculateLayout(
            ScreenBounds,
            TestLayout,
            TestStandards.Shared.Selector);
        var row = layout.CategoryRows[0];
        var motion = new UiSelectorMotion();

        // Drives the underlying motion to a fully hovered state, proving
        // the pin below is not simply "motion never rose".
        motion.AdvanceMotion(
            row.MinusBounds.Center,
            row.MinusBounds,
            row.PlusBounds,
            string.Empty,
            SelectorMarkerDuration,
            MotionIntensity.Full);
        var hoveredColor = motion.PreviousArrowColor(Colors);
        Assert.Equal(Colors.TextPrimary, hoveredColor);

        Assert.Equal(
            Colors.ActionDisabled,
            ArmyCompositionPanel.ResolveArrowColor(
                isDisabled: true,
                hoveredColor,
                Colors));
    }

    [Fact]
    public void ArrowMotionSettlesExactlyAtTheHoverTargetAfterFullDuration()
    {
        var panel = CreatePanel();
        var layout = ArmyCompositionPanel.CalculateLayout(
            ScreenBounds,
            TestLayout,
            TestStandards.Shared.Selector);
        var hoveredMinus = layout.CategoryRows[2].MinusBounds;
        var input = CreateInputAt(hoveredMinus.Center);

        panel.Update(
            input,
            ScreenBounds,
            TimeSpan.FromMilliseconds(30),
            MotionIntensity.Full);
        var midColor = panel.GetCategoryMinusArrowColor(2, Colors);
        Assert.NotEqual(Colors.TextSecondary, midColor);
        Assert.NotEqual(Colors.TextPrimary, midColor);

        panel.Update(
            input,
            ScreenBounds,
            SelectorMarkerDuration,
            MotionIntensity.Full);

        Assert.Equal(
            Colors.TextPrimary,
            panel.GetCategoryMinusArrowColor(2, Colors));
    }
}
