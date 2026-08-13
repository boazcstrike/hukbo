using System.Globalization;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.UI;

internal sealed partial class ArmyCompositionPanel
{
    /// <summary>
    /// Static form used by callers that never advance motion — delegates
    /// with <see cref="TimeSpan.Zero"/> and <see cref="MotionIntensity.Off"/>,
    /// which <see cref="UiTransition.AdvanceTo"/> always snaps under, so the
    /// arrows render exactly as before this overload existed.
    /// </summary>
    public ArmyCompositionInteraction Update(
        InputEdges input,
        Rectangle screenBounds) =>
        Update(input, screenBounds, TimeSpan.Zero, MotionIntensity.Off);

    public ArmyCompositionInteraction Update(
        InputEdges input,
        Rectangle bounds,
        TimeSpan elapsed,
        MotionIntensity motionIntensity)
    {
        var layout = CalculateLayout(bounds, _metrics, _selectorLayout);
        AdvanceArrowMotion(input, layout, elapsed, motionIntensity);
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

    /// <summary>
    /// Advances every stepper row's arrow-hover motion toward the current
    /// pointer position. Bounds are read from <paramref name="layout"/>
    /// rather than recomputed, so the arrow hit rectangles this frame's
    /// click handling uses are exactly the ones this frame's colours were
    /// derived from. The marker text is a constant empty string — this
    /// reuse only reads the two arrow-hover channels, never the pulse that
    /// <see cref="UiSelectorMotion"/> also carries for a selector's active
    /// marker, so nothing here can ever trigger it.
    /// </summary>
    private void AdvanceArrowMotion(
        InputEdges input,
        ArmyCompositionPanelLayout layout,
        TimeSpan elapsed,
        MotionIntensity motionIntensity)
    {
        for (var index = 0; index < layout.CategoryRows.Length; index++)
        {
            var row = layout.CategoryRows[index];
            _categoryArrowMotion[index].AdvanceMotion(
                input.MousePosition,
                row.MinusBounds,
                row.PlusBounds,
                string.Empty,
                elapsed,
                motionIntensity);
        }

        _unitsPerTeamArrowMotion.AdvanceMotion(
            input.MousePosition,
            layout.UnitsPerTeamRow.MinusBounds,
            layout.UnitsPerTeamRow.PlusBounds,
            string.Empty,
            elapsed,
            motionIntensity);

        _movementPresetSelector.Bounds = layout.MovementPresetBounds;
        _movementPresetSelector.AdvanceMotion(
            input,
            elapsed,
            motionIntensity,
            _draftMovementPreset);
    }

    /// <summary>
    /// Arrow hit-test for the movement preset row. A separate helper from
    /// <see cref="GetArrowClickDirection"/> because that one reads
    /// <see cref="ArmyCompositionStepperRowLayout"/> bounds, and the
    /// movement preset row is a reused <see cref="SettingsChoiceSelector{T}"/>
    /// whose arrow rectangles are derived from its own <c>Bounds</c> instead.
    /// </summary>
    private int GetMovementPresetArrowClickDirection(Point pointer)
    {
        if (_movementPresetSelector.PreviousBounds.Contains(pointer))
        {
            return -1;
        }

        return _movementPresetSelector.NextBounds.Contains(pointer) ? 1 : 0;
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
        if (arrowDirection == 0 &&
            _focusedControlIndex == MovementPresetControlIndex)
        {
            arrowDirection = GetMovementPresetArrowClickDirection(pointer);
        }

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
        UiFontSet fonts,
        Rectangle screenBounds,
        UiTheme theme)
    {
        var layout = CalculateLayout(screenBounds, _metrics, _selectorLayout);
        var colors = theme.Colors;
        var titleFont = fonts.Get(UiFontRole.Title);
        var bodyFont = fonts.Get(UiFontRole.Body);
        var labelFont = fonts.Get(UiFontRole.Label);
        var subtitleFont = fonts.Get(UiFontRole.Subtitle);

        spriteBatch.Draw(pixel, screenBounds, colors.OverlayScrim);
        spriteBatch.Draw(pixel, layout.PanelBounds, colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            layout.PanelBounds,
            colors.PanelBorder,
            UiScaleContext.Pixels(theme.Metrics.BorderThickness));

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            titleFont,
            "ARMY COMPOSITION",
            layout.TitleBounds.Center.ToVector2(),
            colors.TextPrimary);

        for (var index = 0; index < layout.CategoryRows.Length; index++)
        {
            var count = _draft.CategoryCounts[index];
            DrawStepperRow(
                spriteBatch,
                pixel,
                labelFont,
                subtitleFont,
                layout.CategoryRows[index],
                CategoryLabels[index],
                count,
                ArmyCompositionStepper.IsCategoryDecrementDisabled(count),
                ArmyCompositionStepper.IsCategoryIncrementDisabled(
                    count,
                    _draft.UnitsPerTeam),
                index == _focusedControlIndex,
                _categoryArrowMotion[index],
                theme);
        }

        DrawStepperRow(
            spriteBatch,
            pixel,
            labelFont,
            subtitleFont,
            layout.UnitsPerTeamRow,
            "Units Per Team",
            _draft.UnitsPerTeam,
            ArmyCompositionStepper.IsUnitsPerTeamDecrementDisabled(
                _draft.UnitsPerTeam),
            ArmyCompositionStepper.IsUnitsPerTeamIncrementDisabled(
                _draft.UnitsPerTeam),
            _focusedControlIndex == UnitsPerTeamControlIndex,
            _unitsPerTeamArrowMotion,
            theme);

        var unassignedColor = Unassigned == 0
            ? colors.StatusSuccess
            : colors.StatusWarning;
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            bodyFont,
            $"Unassigned: {Unassigned.ToString(CultureInfo.InvariantCulture)}",
            layout.UnassignedBounds.Center.ToVector2(),
            unassignedColor);

        DrawActionRow(
            spriteBatch,
            pixel,
            labelFont,
            layout.DistributeEvenlyBounds,
            "Distribute Evenly",
            isEnabled: true,
            _focusedControlIndex == DistributeEvenlyControlIndex,
            theme);
        DrawActionRow(
            spriteBatch,
            pixel,
            labelFont,
            layout.ResetToDefaultBounds,
            "Reset to Default",
            isEnabled: true,
            _focusedControlIndex == ResetToDefaultControlIndex,
            theme);
        _movementPresetSelector.Bounds = layout.MovementPresetBounds;
        _movementPresetSelector.Draw(
            spriteBatch,
            pixel,
            fonts,
            theme,
            _draftMovementPreset,
            _focusedControlIndex == MovementPresetControlIndex);

        DrawActionRow(
            spriteBatch,
            pixel,
            labelFont,
            layout.CancelBounds,
            "Cancel",
            isEnabled: true,
            _focusedControlIndex == CancelControlIndex,
            theme);
        DrawActionRow(
            spriteBatch,
            pixel,
            labelFont,
            layout.ApplyBounds,
            "Apply",
            CanApply,
            _focusedControlIndex == ApplyControlIndex,
            theme);
    }

    private static void DrawStepperRow(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont labelFont,
        SpriteFont arrowFont,
        ArmyCompositionStepperRowLayout row,
        string label,
        int value,
        bool isDecrementDisabled,
        bool isIncrementDisabled,
        bool isFocused,
        UiSelectorMotion motion,
        UiTheme theme)
    {
        var colors = theme.Colors;
        UiPrimitives.DrawText(
            spriteBatch,
            labelFont,
            label,
            new Vector2(
                row.LabelBounds.Left,
                row.LabelBounds.Top + (row.LabelBounds.Height / 4)),
            colors.TextPrimary);

        DrawArrow(
            spriteBatch,
            pixel,
            arrowFont,
            row.MinusBounds,
            "-",
            isDecrementDisabled,
            isFocused,
            motion.PreviousArrowColor(colors),
            theme);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            labelFont,
            value.ToString(CultureInfo.InvariantCulture),
            row.ValueBounds.Center.ToVector2(),
            colors.TextPrimary);
        DrawArrow(
            spriteBatch,
            pixel,
            arrowFont,
            row.PlusBounds,
            "+",
            isIncrementDisabled,
            isFocused,
            motion.NextArrowColor(colors),
            theme);
    }

    /// <summary>
    /// At-limit arrows use ActionDisabled AND a dimmed glyph: colour alone
    /// never carries the meaning that a stepper has hit its bound. A
    /// disabled arrow also never animates — <paramref name="enabledArrowColor"/>
    /// is only read in the enabled branch, so an at-limit arrow stays pinned
    /// to ActionDisabled no matter how the motion behind it has advanced.
    /// Pure so a test can assert the pin without a <c>SpriteBatch</c>.
    /// </summary>
    internal static Color ResolveArrowColor(
        bool isDisabled,
        Color enabledArrowColor,
        UiThemeColors colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        return isDisabled ? colors.ActionDisabled : enabledArrowColor;
    }

    private static void DrawArrow(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        Rectangle bounds,
        string glyph,
        bool isDisabled,
        bool isFocused,
        Color enabledArrowColor,
        UiTheme theme)
    {
        var colors = theme.Colors;
        spriteBatch.Draw(
            pixel,
            bounds,
            ResolveArrowColor(isDisabled, enabledArrowColor, colors));
        if (isFocused && !isDisabled)
        {
            UiPrimitives.DrawBorder(
                spriteBatch,
                pixel,
                bounds,
                colors.ActionFocus,
                UiScaleContext.Pixels(theme.Metrics.FocusThickness));
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
                UiScaleContext.Pixels(theme.Metrics.FocusThickness));
        }

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            label,
            bounds.Center.ToVector2(),
            isEnabled ? colors.TextInverse : colors.TextDisabled);
    }
}
