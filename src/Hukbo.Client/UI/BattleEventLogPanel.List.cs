using Hukbo.Client.Presentation;
using Hukbo.Client.Theming;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

/// <summary>
/// Event stream (row list) drawing for <see cref="BattleEventLogPanel"/>.
/// </summary>
internal sealed partial class BattleEventLogPanel
{
    // ScenarioSeed joins the cache key because the actor label now carries a
    // warrior name derived from it: a new match reuses this cache's rows only
    // when the seed behind those names is still the same one.
    private readonly record struct FormattedEvent(
        BattleEvent BattleEvent,
        int RowWidth,
        ulong ScenarioSeed,
        string Tick,
        string Actor,
        string Action);

    private void DrawList(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        UiTheme theme)
    {
        var captionFont = fonts.Get(UiFontRole.Caption);
        DrawListChrome(spriteBatch, pixel, captionFont, feed, layout, theme);

        var panelState = GetPanelState(
            feed.Entries.Count,
            feed.FilteredEntries.Count);
        if (panelState != BattleEventPanelState.Events)
        {
            DrawEmptyListState(
                spriteBatch,
                fonts,
                layout,
                panelState,
                theme);
            DrawScrollbar(
                spriteBatch,
                pixel,
                feed,
                layout,
                visibleRowCount: 0,
                theme);
            return;
        }

        var visibleRowCount = GetVisibleRowCount(layout);
        var visibleRows = feed.GetVisibleEntries(visibleRowCount);
        var hoveredIndex = HitTestVisibleRow(
            layout,
            _pointerPosition,
            visibleRows.Length);
        for (var index = 0; index < visibleRows.Length; index++)
        {
            DrawRow(
                spriteBatch,
                pixel,
                captionFont,
                feed,
                layout,
                theme,
                visibleRows[index],
                index,
                hoveredIndex);
        }

        DrawScrollbar(
            spriteBatch,
            pixel,
            feed,
            layout,
            visibleRowCount,
            theme);
    }

    private void DrawListChrome(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        UiTheme theme)
    {
        spriteBatch.Draw(
            pixel,
            layout.ListBounds,
            theme.Colors.PanelAlternate);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            layout.ListBounds,
            KeyboardFocusTarget ==
            BattleEventKeyboardFocusTarget.List
                ? theme.Colors.ActionFocus
                : theme.Colors.PanelBorder,
            KeyboardFocusTarget ==
            BattleEventKeyboardFocusTarget.List
                ? theme.Metrics.FocusThickness
                : 1);
        UiPrimitives.DrawText(
            spriteBatch,
            font,
            "EVENT STREAM",
            new Vector2(layout.ListBounds.Left + 8, layout.ListBounds.Top + 6),
            theme.Colors.TextDisabled);
        var statusText = feed.IsPinnedToBottom ? "[LIVE]" : "[INSPECTING]";
        var statusColor = feed.IsPinnedToBottom
            ? theme.Colors.StatusSuccess
            : theme.Colors.NewEvent;
        UiPrimitives.DrawText(
            spriteBatch,
            font,
            statusText,
            new Vector2(
                Math.Max(layout.ListBounds.Left + 100, layout.ListBounds.Right - 82),
                layout.ListBounds.Top + 6),
            statusColor);
    }

    private void DrawRow(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        UiTheme theme,
        BattleEvent battleEvent,
        int index,
        int hoveredIndex)
    {
        var rowBounds = new Rectangle(
            layout.RowsBounds.Left,
            layout.RowsBounds.Top + (index * RowHeight),
            layout.RowsBounds.Width,
            Math.Min(
                RowHeight,
                layout.RowsBounds.Bottom -
                (layout.RowsBounds.Top + (index * RowHeight))));
        var isSelected =
            battleEvent.Sequence == feed.SelectedSequence;
        if (isSelected || index == hoveredIndex)
        {
            spriteBatch.Draw(
                pixel,
                rowBounds,
                isSelected
                    ? theme.Colors.Selection
                    : theme.Colors.ActionHover);
        }

        spriteBatch.Draw(
            pixel,
            new Rectangle(
                rowBounds.Left,
                rowBounds.Top,
                Math.Min(4, rowBounds.Width),
                rowBounds.Height),
            GetKindColor(battleEvent.Kind, theme));
        if (isSelected)
        {
            UiPrimitives.DrawBorder(
                spriteBatch,
                pixel,
                rowBounds,
                theme.Colors.ActionFocus,
                1);
        }

        DrawRowText(
            spriteBatch,
            font,
            theme,
            battleEvent,
            rowBounds,
            isSelected,
            index == hoveredIndex,
            feed.ScenarioSeed);
    }

    private void DrawRowText(
        SpriteBatch spriteBatch,
        SpriteFont font,
        UiTheme theme,
        BattleEvent battleEvent,
        Rectangle rowBounds,
        bool isSelected,
        bool isHovered,
        ulong scenarioSeed)
    {
        var formatted = GetFormattedRow(
            battleEvent,
            Math.Max(8, rowBounds.Width),
            scenarioSeed);
        var foregrounds = GetRowForegrounds(
            theme,
            isSelected,
            isHovered,
            battleEvent.FactionId);
        var tickX = rowBounds.Left + 9;
        var actorX = tickX + 54;
        var actionX = actorX + Math.Min(100, Math.Max(65, rowBounds.Width / 3));
        UiPrimitives.DrawText(
            spriteBatch,
            font,
            formatted.Tick,
            new Vector2(tickX, rowBounds.Top + 8),
            foregrounds.Tick);
        UiPrimitives.DrawText(
            spriteBatch,
            font,
            formatted.Actor,
            new Vector2(actorX, rowBounds.Top + 7),
            foregrounds.Actor);
        if (actionX < rowBounds.Right)
        {
            UiPrimitives.DrawText(
                spriteBatch,
                font,
                formatted.Action,
                new Vector2(actionX, rowBounds.Top + 7),
                foregrounds.Action);
        }
    }

    private static void DrawEmptyListState(
        SpriteBatch spriteBatch,
        UiFontSet fonts,
        BattleEventPanelLayout layout,
        BattleEventPanelState state,
        UiTheme theme)
    {
        var title = state == BattleEventPanelState.NoEvents
            ? "Waiting for battle events"
            : "No events match these filters";
        var hint = state == BattleEventPanelState.NoEvents
            ? "Events appear as the simulation advances."
            : "Adjust a filter or choose RESET.";
        // MeasureString returns the font's full real line spacing as the
        // height of any single-line string (Body 24px, Caption 20px), and
        // DrawCenteredText centers on that measured box. The previous
        // offsets (8 and 11) were tuned for the old resampled text, which
        // measured under 11px tall; at the real bakes they put the title's
        // box bottom (centerY + 4) below the hint's box top (centerY + 1),
        // a 3px overlap. -10 and 14 clear both boxes with a 2px gap.
        var center = layout.RowsBounds.Center.ToVector2();
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(UiFontRole.Body),
            title,
            center - new Vector2(0, 10),
            theme.Colors.TextPrimary);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            fonts.Get(UiFontRole.Caption),
            hint,
            center + new Vector2(0, 14),
            theme.Colors.TextSecondary);
    }

    private static void DrawScrollbar(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        int visibleRowCount,
        UiTheme theme)
    {
        spriteBatch.Draw(
            pixel,
            layout.ScrollbarTrackBounds,
            theme.Colors.CanvasBackground);
        var thumb = GetScrollbarThumb(
            layout.ScrollbarTrackBounds,
            feed.FilteredEntries.Count,
            visibleRowCount,
            feed.GetScrollStart(visibleRowCount));
        spriteBatch.Draw(pixel, thumb, theme.Colors.ActionDefault);
    }

    private FormattedEvent GetFormattedRow(
        BattleEvent battleEvent,
        int rowWidth,
        ulong scenarioSeed)
    {
        for (var index = 0; index < _formattedRows.Count; index++)
        {
            var formattedRow = _formattedRows[index];
            if (formattedRow.BattleEvent == battleEvent &&
                formattedRow.RowWidth == rowWidth &&
                formattedRow.ScenarioSeed == scenarioSeed)
            {
                return formattedRow;
            }

            if (formattedRow.BattleEvent.Sequence == battleEvent.Sequence)
            {
                _formattedRows.RemoveAt(index);
                break;
            }
        }

        var actorCharacters = Math.Max(8, Math.Min(15, rowWidth / 25));
        var actionCharacters = Math.Max(
            6,
            (rowWidth - 165) /
                UiFontRamp.GetApproximateAdvancePx(UiFontRole.Caption));
        var formatted = new FormattedEvent(
            battleEvent,
            rowWidth,
            scenarioSeed,
            $"T{battleEvent.Tick:00000}",
            ClipLabel(
                BattleEventFormatter.GetRowActorLabel(battleEvent, scenarioSeed),
                actorCharacters),
            ClipLabel(
                BattleEventFormatter.GetActionLabel(battleEvent),
                actionCharacters));
        _formattedRows.Add(formatted);
        return formatted;
    }

    private void SynchronizeCache(IReadOnlyList<BattleEvent> entries)
    {
        if (entries.Count == 0)
        {
            _formattedRows.Clear();
            return;
        }

        var oldestSequence = entries[0].Sequence;
        var newestSequence = entries[^1].Sequence;
        for (var index = _formattedRows.Count - 1; index >= 0; index--)
        {
            var sequence = _formattedRows[index].BattleEvent.Sequence;
            if (sequence < oldestSequence || sequence > newestSequence)
            {
                _formattedRows.RemoveAt(index);
            }
        }
    }

    private static Color GetKindColor(
        BattleEventKind kind,
        UiTheme theme) =>
        kind switch
        {
            BattleEventKind.Move => theme.Colors.StatusInfo,
            BattleEventKind.Attack => theme.Colors.StatusWarning,
            BattleEventKind.Damage => theme.Colors.StatusDanger,
            BattleEventKind.Death => theme.Colors.OtherFaction,
            BattleEventKind.Outcome => theme.Colors.StatusSuccess,
            _ => theme.Colors.TextSecondary,
        };

    internal static BattleEventRowForegrounds GetRowForegrounds(
        UiTheme theme,
        bool isSelected,
        bool isHovered,
        int? factionId)
    {
        if (isSelected || isHovered)
        {
            return new BattleEventRowForegrounds(
                theme.Colors.TextInverse,
                theme.Colors.TextInverse,
                theme.Colors.TextInverse);
        }

        return new BattleEventRowForegrounds(
            theme.Colors.TextDisabled,
            FactionColorPalette.GetThemeColor(
                factionId,
                theme,
                theme.Colors.TextPrimary),
            theme.Colors.TextSecondary);
    }
}
