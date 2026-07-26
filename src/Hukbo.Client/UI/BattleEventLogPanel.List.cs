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
    private readonly record struct FormattedEvent(
        BattleEvent BattleEvent,
        int RowWidth,
        string Tick,
        string Actor,
        string Action);

    private void DrawList(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        UiTheme theme)
    {
        DrawListChrome(spriteBatch, pixel, font, feed, layout, theme);

        var panelState = GetPanelState(
            feed.Entries.Count,
            feed.FilteredEntries.Count);
        if (panelState != BattleEventPanelState.Events)
        {
            DrawEmptyListState(
                spriteBatch,
                font,
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
                font,
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
        spriteBatch.DrawString(
            font,
            "EVENT STREAM",
            new Vector2(layout.ListBounds.Left + 8, layout.ListBounds.Top + 6),
            theme.Colors.TextDisabled,
            0f,
            Vector2.Zero,
            0.53f,
            SpriteEffects.None,
            0f);
        var statusText = feed.IsPinnedToBottom ? "[LIVE]" : "[INSPECTING]";
        var statusColor = feed.IsPinnedToBottom
            ? theme.Colors.StatusSuccess
            : theme.Colors.NewEvent;
        spriteBatch.DrawString(
            font,
            statusText,
            new Vector2(
                Math.Max(layout.ListBounds.Left + 100, layout.ListBounds.Right - 82),
                layout.ListBounds.Top + 6),
            statusColor,
            0f,
            Vector2.Zero,
            0.52f,
            SpriteEffects.None,
            0f);
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
            index == hoveredIndex);
    }

    private void DrawRowText(
        SpriteBatch spriteBatch,
        SpriteFont font,
        UiTheme theme,
        BattleEvent battleEvent,
        Rectangle rowBounds,
        bool isSelected,
        bool isHovered)
    {
        var formatted = GetFormattedRow(
            battleEvent,
            Math.Max(8, rowBounds.Width));
        var foregrounds = GetRowForegrounds(
            theme,
            isSelected,
            isHovered,
            battleEvent.FactionId);
        var tickX = rowBounds.Left + 9;
        var actorX = tickX + 54;
        var actionX = actorX + Math.Min(100, Math.Max(65, rowBounds.Width / 3));
        spriteBatch.DrawString(
            font,
            formatted.Tick,
            new Vector2(tickX, rowBounds.Top + 8),
            foregrounds.Tick,
            0f,
            Vector2.Zero,
            0.50f,
            SpriteEffects.None,
            0f);
        spriteBatch.DrawString(
            font,
            formatted.Actor,
            new Vector2(actorX, rowBounds.Top + 7),
            foregrounds.Actor,
            0f,
            Vector2.Zero,
            0.55f,
            SpriteEffects.None,
            0f);
        if (actionX < rowBounds.Right)
        {
            spriteBatch.DrawString(
                font,
                formatted.Action,
                new Vector2(actionX, rowBounds.Top + 7),
                foregrounds.Action,
                0f,
                Vector2.Zero,
                0.55f,
                SpriteEffects.None,
                0f);
        }
    }

    private static void DrawEmptyListState(
        SpriteBatch spriteBatch,
        SpriteFont font,
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
        var center = layout.RowsBounds.Center.ToVector2();
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            title,
            center - new Vector2(0, 8),
            theme.Colors.TextPrimary,
            0.58f);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            hint,
            center + new Vector2(0, 11),
            theme.Colors.TextSecondary,
            0.48f);
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
        int rowWidth)
    {
        for (var index = 0; index < _formattedRows.Count; index++)
        {
            var formattedRow = _formattedRows[index];
            if (formattedRow.BattleEvent == battleEvent &&
                formattedRow.RowWidth == rowWidth)
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
        var actionCharacters = Math.Max(6, (rowWidth - 165) / 7);
        var formatted = new FormattedEvent(
            battleEvent,
            rowWidth,
            $"T{battleEvent.Tick:00000}",
            ClipLabel(
                BattleEventFormatter.GetActorLabel(battleEvent),
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
