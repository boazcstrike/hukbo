using System.Collections.Immutable;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

internal readonly record struct ArmyCompositionStepperRowLayout(
    Rectangle RowBounds,
    Rectangle LabelBounds,
    Rectangle MinusBounds,
    Rectangle ValueBounds,
    Rectangle PlusBounds);

internal readonly record struct ArmyCompositionPanelLayout(
    Rectangle PanelBounds,
    Rectangle TitleBounds,
    ImmutableArray<ArmyCompositionStepperRowLayout> CategoryRows,
    ArmyCompositionStepperRowLayout UnitsPerTeamRow,
    Rectangle UnassignedBounds,
    Rectangle DistributeEvenlyBounds,
    Rectangle ResetToDefaultBounds,
    Rectangle CancelBounds,
    Rectangle ApplyBounds);

internal sealed partial class ArmyCompositionPanel
{
    internal static ArmyCompositionPanelLayout CalculateLayout(
        Rectangle screenBounds,
        UiArmyCompositionLayout metrics)
    {
        metrics = new UiArmyCompositionLayout(
            UiScaleContext.Pixels(metrics.PanelWidth),
            UiScaleContext.Pixels(metrics.PanelHeight),
            UiScaleContext.Pixels(metrics.RowHeight),
            UiScaleContext.Pixels(metrics.RowGap),
            UiScaleContext.Pixels(metrics.StepperWidth),
            UiScaleContext.Pixels(metrics.ArrowWidth));

        var panelBounds = new Rectangle(
            screenBounds.Center.X - (metrics.PanelWidth / 2),
            screenBounds.Center.Y - (metrics.PanelHeight / 2),
            metrics.PanelWidth,
            metrics.PanelHeight);

        var margin = metrics.RowGap * 2;
        var rowLeft = panelBounds.Left + margin;
        var rowWidth = panelBounds.Width - (margin * 2);
        var top = panelBounds.Top + margin;

        var titleBounds = new Rectangle(
            rowLeft,
            top,
            rowWidth,
            metrics.RowHeight);
        top += metrics.RowHeight + metrics.RowGap;

        var categoryRows = ImmutableArray.CreateBuilder<
            ArmyCompositionStepperRowLayout>(
            ArmyCompositionStepper.CategoryCount);
        for (var index = 0; index < ArmyCompositionStepper.CategoryCount; index++)
        {
            categoryRows.Add(CreateStepperRow(rowLeft, top, rowWidth, metrics));
            top += metrics.RowHeight + metrics.RowGap;
        }

        var unitsPerTeamRow = CreateStepperRow(rowLeft, top, rowWidth, metrics);
        top += metrics.RowHeight + metrics.RowGap;

        var unassignedBounds = new Rectangle(
            rowLeft,
            top,
            rowWidth,
            metrics.RowHeight);
        top += metrics.RowHeight + metrics.RowGap;

        var distributeEvenlyBounds = new Rectangle(
            rowLeft,
            top,
            rowWidth,
            metrics.RowHeight);
        top += metrics.RowHeight + metrics.RowGap;

        var resetToDefaultBounds = new Rectangle(
            rowLeft,
            top,
            rowWidth,
            metrics.RowHeight);
        top += metrics.RowHeight + metrics.RowGap;

        var halfWidth = (rowWidth - metrics.RowGap) / 2;
        var cancelBounds = new Rectangle(
            rowLeft,
            top,
            halfWidth,
            metrics.RowHeight);
        var applyBounds = new Rectangle(
            rowLeft + halfWidth + metrics.RowGap,
            top,
            rowWidth - halfWidth - metrics.RowGap,
            metrics.RowHeight);

        return new ArmyCompositionPanelLayout(
            panelBounds,
            titleBounds,
            categoryRows.MoveToImmutable(),
            unitsPerTeamRow,
            unassignedBounds,
            distributeEvenlyBounds,
            resetToDefaultBounds,
            cancelBounds,
            applyBounds);
    }

    private static ArmyCompositionStepperRowLayout CreateStepperRow(
        int left,
        int top,
        int width,
        UiArmyCompositionLayout metrics)
    {
        var rowBounds = new Rectangle(left, top, width, metrics.RowHeight);
        var stepperLeft = rowBounds.Right - metrics.StepperWidth;
        var minusBounds = new Rectangle(
            stepperLeft,
            top,
            metrics.ArrowWidth,
            metrics.RowHeight);
        var plusBounds = new Rectangle(
            rowBounds.Right - metrics.ArrowWidth,
            top,
            metrics.ArrowWidth,
            metrics.RowHeight);
        var valueBounds = new Rectangle(
            minusBounds.Right,
            top,
            metrics.StepperWidth - (metrics.ArrowWidth * 2),
            metrics.RowHeight);
        var labelBounds = new Rectangle(
            left,
            top,
            stepperLeft - left,
            metrics.RowHeight);

        return new ArmyCompositionStepperRowLayout(
            rowBounds,
            labelBounds,
            minusBounds,
            valueBounds,
            plusBounds);
    }

    private static int GetHoveredControlIndex(
        ArmyCompositionPanelLayout layout,
        Point pointer)
    {
        for (var index = 0; index < layout.CategoryRows.Length; index++)
        {
            if (layout.CategoryRows[index].RowBounds.Contains(pointer))
            {
                return index;
            }
        }

        if (layout.UnitsPerTeamRow.RowBounds.Contains(pointer))
        {
            return UnitsPerTeamControlIndex;
        }

        if (layout.DistributeEvenlyBounds.Contains(pointer))
        {
            return DistributeEvenlyControlIndex;
        }

        if (layout.ResetToDefaultBounds.Contains(pointer))
        {
            return ResetToDefaultControlIndex;
        }

        if (layout.CancelBounds.Contains(pointer))
        {
            return CancelControlIndex;
        }

        if (layout.ApplyBounds.Contains(pointer))
        {
            return ApplyControlIndex;
        }

        return -1;
    }

    private static int GetArrowClickDirection(
        ArmyCompositionPanelLayout layout,
        int focusedControlIndex,
        Point pointer)
    {
        var row = focusedControlIndex >= 0 &&
            focusedControlIndex < layout.CategoryRows.Length
            ? layout.CategoryRows[focusedControlIndex]
            : focusedControlIndex == UnitsPerTeamControlIndex
                ? layout.UnitsPerTeamRow
                : (ArmyCompositionStepperRowLayout?)null;

        if (row is null)
        {
            return 0;
        }

        if (row.Value.MinusBounds.Contains(pointer))
        {
            return -1;
        }

        return row.Value.PlusBounds.Contains(pointer) ? 1 : 0;
    }
}
