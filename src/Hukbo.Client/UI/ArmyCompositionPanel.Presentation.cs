using System.Globalization;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.UI;

internal sealed partial class ArmyCompositionPanel
{
    public ArmyCompositionInteraction Update(
        InputEdges input,
        Rectangle screenBounds)
    {
        var layout = CalculateLayout(screenBounds, _metrics);
        var pointerInsidePanel = layout.PanelBounds.Contains(
            input.MousePosition);

        var keyboardDirection = 0;
        if (input.WasPressed(Keys.Down))
        {
            keyboardDirection = 1;
        }
        else if (input.WasPressed(Keys.Up))
        {
            keyboardDirection = -1;
        }

        var hoveredControlIndex = GetHoveredControlIndex(
            layout,
            input.MousePosition);
        MoveFocus(keyboardDirection, hoveredControlIndex);

        var isShiftHeld =
            input.IsDown(Keys.LeftShift) || input.IsDown(Keys.RightShift);
        if (input.WasPressed(Keys.Left))
        {
            AdjustFocusedValue(-1, isShiftHeld);
        }
        else if (input.WasPressed(Keys.Right))
        {
            AdjustFocusedValue(1, isShiftHeld);
        }

        if (input.WasLeftMousePressed() && pointerInsidePanel)
        {
            var clickInteraction = HandlePointerClick(
                layout,
                hoveredControlIndex,
                isShiftHeld,
                input.MousePosition);
            if (clickInteraction is { } consumed)
            {
                return consumed;
            }
        }
        else if (input.WasPressed(Keys.Enter) || input.WasPressed(Keys.Space))
        {
            var activated = Activate();
            if (activated.Result != ArmyCompositionPanelResult.None)
            {
                return activated;
            }
        }

        return new ArmyCompositionInteraction(
            ArmyCompositionPanelResult.None,
            pointerInsidePanel);
    }

    private ArmyCompositionInteraction? HandlePointerClick(
        ArmyCompositionPanelLayout layout,
        int hoveredControlIndex,
        bool isShiftHeld,
        Point pointer)
    {
        var arrowDirection = GetArrowClickDirection(
            layout,
            _focusedControlIndex,
            pointer);
        if (arrowDirection != 0)
        {
            AdjustFocusedValue(arrowDirection, isShiftHeld);
            return null;
        }

        if (hoveredControlIndex != _focusedControlIndex)
        {
            return null;
        }

        var activated = Activate();
        return activated.Result != ArmyCompositionPanelResult.None
            ? activated
            : null;
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        Rectangle screenBounds,
        UiTheme theme)
    {
        var layout = CalculateLayout(screenBounds, _metrics);
        var colors = theme.Colors;

        spriteBatch.Draw(pixel, screenBounds, colors.OverlayScrim);
        spriteBatch.Draw(pixel, layout.PanelBounds, colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            layout.PanelBounds,
            colors.PanelBorder,
            theme.Metrics.BorderThickness);

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            "ARMY COMPOSITION",
            layout.TitleBounds.Center.ToVector2(),
            colors.TextPrimary);

        for (var index = 0; index < layout.CategoryRows.Length; index++)
        {
            var count = _draft.CategoryCounts[index];
            DrawStepperRow(
                spriteBatch,
                pixel,
                font,
                layout.CategoryRows[index],
                CategoryLabels[index],
                count,
                ArmyCompositionStepper.IsCategoryDecrementDisabled(count),
                ArmyCompositionStepper.IsCategoryIncrementDisabled(
                    count,
                    _draft.UnitsPerTeam),
                index == _focusedControlIndex,
                theme);
        }

        DrawStepperRow(
            spriteBatch,
            pixel,
            font,
            layout.UnitsPerTeamRow,
            "Units Per Team",
            _draft.UnitsPerTeam,
            ArmyCompositionStepper.IsUnitsPerTeamDecrementDisabled(
                _draft.UnitsPerTeam),
            ArmyCompositionStepper.IsUnitsPerTeamIncrementDisabled(
                _draft.UnitsPerTeam),
            _focusedControlIndex == UnitsPerTeamControlIndex,
            theme);

        var unassignedColor = Unassigned == 0
            ? colors.StatusSuccess
            : colors.StatusWarning;
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            $"Unassigned: {Unassigned.ToString(CultureInfo.InvariantCulture)}",
            layout.UnassignedBounds.Center.ToVector2(),
            unassignedColor);

        DrawActionRow(
            spriteBatch,
            pixel,
            font,
            layout.DistributeEvenlyBounds,
            "Distribute Evenly",
            isEnabled: true,
            _focusedControlIndex == DistributeEvenlyControlIndex,
            theme);
        DrawActionRow(
            spriteBatch,
            pixel,
            font,
            layout.ResetToDefaultBounds,
            "Reset to Default",
            isEnabled: true,
            _focusedControlIndex == ResetToDefaultControlIndex,
            theme);
        DrawActionRow(
            spriteBatch,
            pixel,
            font,
            layout.CancelBounds,
            "Cancel",
            isEnabled: true,
            _focusedControlIndex == CancelControlIndex,
            theme);
        DrawActionRow(
            spriteBatch,
            pixel,
            font,
            layout.ApplyBounds,
            "Apply",
            CanApply,
            _focusedControlIndex == ApplyControlIndex,
            theme);
    }

    private static void DrawStepperRow(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        ArmyCompositionStepperRowLayout row,
        string label,
        int value,
        bool isDecrementDisabled,
        bool isIncrementDisabled,
        bool isFocused,
        UiTheme theme)
    {
        var colors = theme.Colors;
        spriteBatch.DrawString(
            font,
            label,
            new Vector2(
                row.LabelBounds.Left,
                row.LabelBounds.Top + (row.LabelBounds.Height / 4)),
            colors.TextPrimary);

        DrawArrow(
            spriteBatch,
            pixel,
            font,
            row.MinusBounds,
            "-",
            isDecrementDisabled,
            isFocused,
            theme);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            value.ToString(CultureInfo.InvariantCulture),
            row.ValueBounds.Center.ToVector2(),
            colors.TextPrimary);
        DrawArrow(
            spriteBatch,
            pixel,
            font,
            row.PlusBounds,
            "+",
            isIncrementDisabled,
            isFocused,
            theme);
    }

    // At-limit arrows use ActionDisabled AND a dimmed glyph: colour alone
    // never carries the meaning that a stepper has hit its bound.
    private static void DrawArrow(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        Rectangle bounds,
        string glyph,
        bool isDisabled,
        bool isFocused,
        UiTheme theme)
    {
        var colors = theme.Colors;
        spriteBatch.Draw(
            pixel,
            bounds,
            isDisabled ? colors.ActionDisabled : colors.ActionDefault);
        if (isFocused && !isDisabled)
        {
            UiPrimitives.DrawBorder(
                spriteBatch,
                pixel,
                bounds,
                colors.ActionFocus,
                theme.Metrics.FocusThickness);
        }

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            glyph,
            bounds.Center.ToVector2(),
            isDisabled ? colors.TextDisabled : colors.TextInverse);
    }

    private static void DrawActionRow(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        Rectangle bounds,
        string label,
        bool isEnabled,
        bool isFocused,
        UiTheme theme)
    {
        var colors = theme.Colors;
        spriteBatch.Draw(
            pixel,
            bounds,
            isEnabled ? colors.ActionDefault : colors.ActionDisabled);
        if (isFocused && isEnabled)
        {
            UiPrimitives.DrawBorder(
                spriteBatch,
                pixel,
                bounds,
                colors.ActionFocus,
                theme.Metrics.FocusThickness);
        }

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            label,
            bounds.Center.ToVector2(),
            isEnabled ? colors.TextInverse : colors.TextDisabled);
    }
}
