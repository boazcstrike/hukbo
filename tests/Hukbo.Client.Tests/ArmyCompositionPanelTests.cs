using System.Collections.Immutable;
using System.Globalization;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

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
            PanelWidth: 640,
            PanelHeight: 648,
            RowHeight: 44,
            RowGap: 8,
            StepperWidth: 148,
            ArrowWidth: 44);

    [Fact]
    public void EveryCategoryIsOneRosterEntryOfTheActivePreset()
    {
        // A category is a roster entry, not a weapon: preset V2 fields a solo
        // and a shielded loadout for each one-handed weapon. Scenario
        // validation requires RosterCounts to match the roster length exactly,
        // so a mismatch here is a battle that refuses to start.
        var rosterLength = CombatPresetRegistry
            .Get(CombatPresetId.PrecolonialPhilippinesV2)
            .Roster
            .Count;

        Assert.Equal(ArmyCompositionStepper.CategoryCount, rosterLength);
        Assert.Equal(
            Hukbo.Client.Settings.ArmyComposition.CategoryCount,
            rosterLength);
        Assert.Equal(rosterLength, ArmyCompositionPanel.CategoryLabels.Count);
    }

    [Fact]
    public void EveryCategoryLabelNamesItsWeaponAndItsGrip()
    {
        var labels = ArmyCompositionPanel.CategoryLabels;

        // Pair form throughout, so no label is a bare cultural name.
        foreach (var label in labels)
        {
            Assert.Contains(" — ", label, StringComparison.Ordinal);
        }

        // A weapon that appears twice must say which of the two it is, or the
        // spectator cannot tell the rows apart.
        foreach (var group in labels.GroupBy(
            label => label.Split(" (")[0],
            StringComparer.Ordinal))
        {
            if (group.Count() == 1)
            {
                continue;
            }

            Assert.Contains(group, label => label.EndsWith("(solo)", StringComparison.Ordinal));
            Assert.Contains(
                group,
                label => label.EndsWith("(shielded)", StringComparison.Ordinal));
        }

        Assert.Equal(
            labels.Count,
            labels.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryLaidOutRowFitsInsideThePanel()
    {
        // The panel height is theme data while the row count is code, so the
        // two can drift apart silently: growing the roster from four entries
        // to six pushed the Apply row past a 560px panel. This is the check
        // that catches it rather than a spectator finding a clipped button.
        var layout = ArmyCompositionPanel.CalculateLayout(
            new Rectangle(0, 0, 1280, 720),
            TestArmyCompositionLayout);
        var margin = TestArmyCompositionLayout.RowGap * 2;

        Assert.Equal(
            ArmyCompositionStepper.CategoryCount,
            layout.CategoryRows.Length);

        foreach (var bounds in AllRowBounds(layout))
        {
            Assert.True(
                bounds.Top >= layout.PanelBounds.Top + margin,
                $"A row starting at {bounds.Top} sits above the panel's top " +
                $"margin at {layout.PanelBounds.Top + margin}.");
            Assert.True(
                bounds.Bottom <= layout.PanelBounds.Bottom - margin,
                $"A row ending at {bounds.Bottom} falls past the panel's " +
                $"bottom margin at {layout.PanelBounds.Bottom - margin}.");
        }
    }

    [Fact]
    public void EveryRowLabelFitsItsLabelBoundsUnderTheConservativeAdvanceEstimate()
    {
        // EveryLaidOutRowFitsInsideThePanel only checks vertical extents, so a
        // label that overruns its own box horizontally was invisible to the
        // suite: at the shipped 420/260 metrics the longest category label,
        // "Kalis — Thrusting Blade (shielded)", needed roughly 408px of a
        // 128px label box. This test measures every row's label against its
        // box directly so that overflow fails here instead of shipping.
        var layout = ArmyCompositionPanel.CalculateLayout(
            new Rectangle(0, 0, 1280, 720),
            TestArmyCompositionLayout);
        var advancePx = UiFontRamp.GetApproximateAdvancePx(UiFontRole.Label);

        for (var index = 0; index < layout.CategoryRows.Length; index++)
        {
            var label = ArmyCompositionPanel.CategoryLabels[index];
            var row = layout.CategoryRows[index];
            Assert.True(
                label.Length * advancePx <= row.LabelBounds.Width,
                $"\"{label}\" needs {label.Length * advancePx}px but its " +
                $"label box is only {row.LabelBounds.Width}px wide.");
        }

        const string unitsPerTeamLabel = "Units Per Team";
        Assert.True(
            unitsPerTeamLabel.Length * advancePx
                <= layout.UnitsPerTeamRow.LabelBounds.Width,
            $"\"{unitsPerTeamLabel}\" needs " +
            $"{unitsPerTeamLabel.Length * advancePx}px but its label box is " +
            $"only {layout.UnitsPerTeamRow.LabelBounds.Width}px wide.");
    }

    [Fact]
    public void EveryValueBoxFitsTheLargestNumberTheStepperCanShow()
    {
        // Raising the units-per-team ceiling from 250 to 500 changes what these
        // boxes have to display. It happens not to change how wide that is,
        // because 500 is still three digits, and this test is what says so
        // rather than leaving it to be noticed on screen. It does not replace
        // the manual window-fit check: the panel's own size is theme data and
        // is verified by EveryLaidOutRowFitsInsideThePanel and by a human
        // looking at a real window.
        var widest = ArmyCompositionStepper.MaximumUnitsPerTeam.ToString(
            CultureInfo.InvariantCulture);
        Assert.Equal(3, widest.Length);

        var layout = ArmyCompositionPanel.CalculateLayout(
            new Rectangle(0, 0, 1280, 720),
            TestArmyCompositionLayout);
        var advancePx = UiFontRamp.GetApproximateAdvancePx(UiFontRole.Label);
        var requiredPx = widest.Length * advancePx;

        foreach (var row in layout.CategoryRows)
        {
            Assert.True(
                requiredPx <= row.ValueBounds.Width,
                $"A category value of \"{widest}\" needs {requiredPx}px but " +
                $"its value box is only {row.ValueBounds.Width}px wide.");
        }

        Assert.True(
            requiredPx <= layout.UnitsPerTeamRow.ValueBounds.Width,
            $"A units-per-team value of \"{widest}\" needs {requiredPx}px " +
            "but its value box is only " +
            $"{layout.UnitsPerTeamRow.ValueBounds.Width}px wide.");
    }

    private static IEnumerable<Rectangle> AllRowBounds(
        ArmyCompositionPanelLayout layout)
    {
        yield return layout.TitleBounds;
        foreach (var row in layout.CategoryRows)
        {
            yield return row.RowBounds;
        }

        yield return layout.UnitsPerTeamRow.RowBounds;
        yield return layout.UnassignedBounds;
        yield return layout.DistributeEvenlyBounds;
        yield return layout.ResetToDefaultBounds;
        yield return layout.CancelBounds;
        yield return layout.ApplyBounds;
    }

    [Fact]
    public void FocusWrapsAcrossEveryPanelControl()
    {
        // One control per roster category plus the units-per-team stepper and
        // the four buttons. Derived from the category count so growing the
        // roster cannot leave the last category unreachable by keyboard.
        Assert.Equal(
            ArmyCompositionStepper.CategoryCount + 5,
            ArmyCompositionPanel.ControlCount);
        Assert.Equal(
            ArmyCompositionPanel.ControlCount - 1,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: 0,
                keyboardDirection: -1,
                hoveredIndex: -1,
                controlCount: ArmyCompositionPanel.ControlCount));
        Assert.Equal(
            0,
            MenuOverlay.ResolveFocusedControlIndex(
                currentIndex: ArmyCompositionPanel.ControlCount - 1,
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
