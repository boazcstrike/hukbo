using System.Collections.Immutable;
using Hukbo.Client.UI;

namespace Hukbo.Client.Tests;

public sealed class ArmyCompositionPanelTests
{
    private static ArmyComposition CreateComposition(
        int a,
        int b,
        int c,
        int d,
        int unitsPerTeam) =>
        new(ImmutableArray.Create(a, b, c, d), unitsPerTeam);

    private static ArmyCompositionPanel CreatePanel(ArmyComposition saved) =>
        new(saved, TestArmyCompositionLayout);

    private static Hukbo.Client.Theming.UiArmyCompositionLayout
        TestArmyCompositionLayout =>
        new(
            PanelWidth: 420,
            PanelHeight: 560,
            RowHeight: 44,
            RowGap: 8,
            StepperWidth: 260,
            ArrowWidth: 44);

    [Fact]
    public void FocusWrapsAcrossTheNinePanelControls()
    {
        Assert.Equal(9, ArmyCompositionPanel.ControlCount);
        Assert.Equal(
            8,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: 0,
                keyboardDirection: -1,
                hoveredIndex: -1,
                controlCount: ArmyCompositionPanel.ControlCount));
        Assert.Equal(
            0,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: 8,
                keyboardDirection: 1,
                hoveredIndex: -1,
                controlCount: ArmyCompositionPanel.ControlCount));
    }

    [Fact]
    public void ApplyIsDisabledWhileAnyUnitsAreUnassigned()
    {
        var saved = CreateComposition(50, 50, 50, 50, 200);
        var draft = CreateComposition(40, 50, 50, 50, 200);

        Assert.False(ArmyCompositionPanel.IsApplyEnabled(draft, saved));
    }

    [Fact]
    public void ApplyIsDisabledWhenTheDraftEqualsTheSavedComposition()
    {
        var saved = CreateComposition(50, 50, 50, 50, 200);
        var draft = CreateComposition(50, 50, 50, 50, 200);

        Assert.False(ArmyCompositionPanel.IsApplyEnabled(draft, saved));
    }

    [Fact]
    public void ApplyIsEnabledWhenBalancedAndChanged()
    {
        var saved = CreateComposition(50, 50, 50, 50, 200);
        var draft = CreateComposition(60, 40, 50, 50, 200);

        Assert.True(ArmyCompositionPanel.IsApplyEnabled(draft, saved));
    }

    [Theory]
    [InlineData(50, 50, 50, 50, 200, 0)]
    [InlineData(40, 50, 50, 50, 200, 10)]
    [InlineData(60, 60, 60, 60, 200, -40)]
    public void TheUnassignedReadoutIsTheTotalMinusTheCategorySum(
        int a,
        int b,
        int c,
        int d,
        int unitsPerTeam,
        int expectedUnassigned)
    {
        var composition = CreateComposition(a, b, c, d, unitsPerTeam);

        Assert.Equal(expectedUnassigned, composition.Unassigned);
    }

    [Fact]
    public void CancelDiscardsTheDraftAndRestoresTheSavedComposition()
    {
        var saved = CreateComposition(50, 50, 50, 50, 200);
        var panel = CreatePanel(saved);

        panel.AdjustFocusedValue(direction: 1, isShiftHeld: false);
        Assert.NotEqual(saved, panel.Draft);

        var interaction = panel.PerformAction(
            ArmyCompositionPanelAction.Cancel);

        Assert.Equal(ArmyCompositionPanelResult.Cancelled, interaction.Result);
        Assert.Equal(saved, panel.Draft);
        Assert.Equal(saved, panel.Saved);
    }

    [Fact]
    public void ResetToDefaultRecomputesTheEvenSplitAtTheCurrentTotal()
    {
        var saved = CreateComposition(50, 50, 50, 50, 200);
        var panel = CreatePanel(saved);

        panel.MoveFocus(
            keyboardDirection: 0,
            hoveredControlIndex: ArmyCompositionPanel.UnitsPerTeamControlIndex);
        panel.AdjustFocusedValue(direction: 1, isShiftHeld: false);
        Assert.Equal(210, panel.Draft.UnitsPerTeam);

        panel.MoveFocus(keyboardDirection: 0, hoveredControlIndex: 0);
        panel.AdjustFocusedValue(direction: 1, isShiftHeld: false);
        Assert.NotEqual(0, panel.Unassigned);

        panel.PerformAction(ArmyCompositionPanelAction.ResetToDefault);

        Assert.Equal(0, panel.Unassigned);
        Assert.Equal(
            ArmyCompositionStepper.DistributeEvenly(210).ToArray(),
            panel.Draft.CategoryCounts.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EnterAndSpaceDoNothingOnAStepperRow(int stepperControlIndex)
    {
        Assert.Equal(
            ArmyCompositionPanelAction.None,
            ArmyCompositionPanel.ResolveActivatedAction(stepperControlIndex));
    }

    [Fact]
    public void EnterAndSpaceDoNothingOnTheUnitsPerTeamStepperRow()
    {
        Assert.Equal(
            ArmyCompositionPanelAction.None,
            ArmyCompositionPanel.ResolveActivatedAction(
                ArmyCompositionPanel.UnitsPerTeamControlIndex));
    }
}
