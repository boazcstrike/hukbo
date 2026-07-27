using Hukbo.Client.Presentation;
using Hukbo.Client.Theming;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

/// <summary>
/// Filter row drawing and filter-cycling logic for
/// <see cref="BattleEventLogPanel"/>.
/// </summary>
internal sealed partial class BattleEventLogPanel
{
    private void DrawFilters(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        UiTheme theme)
    {
        var font = fonts.Get(UiFontRole.Caption);
        DrawKindFactionActorControls(spriteBatch, pixel, font, feed, layout, theme);
        DrawSearchControl(spriteBatch, pixel, font, feed, layout, theme);
        DrawResetControl(spriteBatch, pixel, font, feed, layout, theme);
    }

    private void DrawKindFactionActorControls(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        UiTheme theme)
    {
        DrawControl(
            spriteBatch,
            pixel,
            font,
            layout.KindFilterBounds,
            BattleEventFormatter.GetKindLabel(feed.KindFilter),
            feed.KindFilter.HasValue,
            isFocused: false,
            theme);
        DrawControl(
            spriteBatch,
            pixel,
            font,
            layout.FactionFilterBounds,
            feed.FactionFilter switch
            {
                0 => "TEAM BLUE",
                1 => "TEAM RED",
                int faction => $"FACTION {faction}",
                null => "ALL TEAMS",
            },
            feed.FactionFilter.HasValue,
            isFocused: false,
            theme);
        DrawControl(
            spriteBatch,
            pixel,
            font,
            layout.ActorFilterBounds,
            GetActorFilterLabel(
                feed.ActorFilter,
                feed.SelectedEvent),
            feed.ActorFilter.HasValue,
            isFocused: false,
            theme,
            isEnabled: IsActorFilterControlEnabled(
                feed.ActorFilter,
                feed.SelectedEvent));
    }

    private void DrawSearchControl(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        UiTheme theme)
    {
        var searchHasFocus = KeyboardFocusTarget ==
            BattleEventKeyboardFocusTarget.Search;
        var searchLabel = feed.TextFilter.Length > 0
            ? $"SEARCH  {feed.TextFilter}{(searchHasFocus ? "_" : string.Empty)}"
            : searchHasFocus
                ? "SEARCH  _"
                : "SEARCH  type to filter";
        DrawControl(
            spriteBatch,
            pixel,
            font,
            layout.SearchBounds,
            searchLabel,
            feed.TextFilter.Length > 0,
            searchHasFocus,
            theme,
            horizontalAlignment: TextAlignment.Left);
    }

    private void DrawResetControl(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        UiTheme theme)
    {
        if (feed.HasActiveFilters)
        {
            DrawControl(
                spriteBatch,
                pixel,
                font,
                layout.ResetBounds,
                "RESET",
                isActive: false,
                isFocused: false,
                theme);
            return;
        }

        spriteBatch.Draw(
            pixel,
            layout.ResetBounds,
            theme.Colors.ActionDisabled);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            layout.ResetBounds,
            theme.Colors.PanelBorder,
            1);
    }

    internal static bool TryGetActorFilterForClick(
        ulong? activeActorFilter,
        BattleEvent? selectedEvent,
        out ulong? actorFilter)
    {
        if (activeActorFilter.HasValue)
        {
            actorFilter = null;
            return true;
        }

        if (selectedEvent is { } selected)
        {
            actorFilter = selected.SourceEntityId;
            return true;
        }

        actorFilter = null;
        return false;
    }

    internal static string GetActorFilterLabel(
        ulong? actorId,
        BattleEvent? selectedEvent) =>
        actorId is { } activeActorId
            ? $"CLEAR #{activeActorId}"
            : selectedEvent is { } selected
                ? $"FILTER #{selected.SourceEntityId}"
                : "SELECT ROW";

    internal static bool IsActorFilterControlEnabled(
        ulong? actorId,
        BattleEvent? selectedEvent) =>
        actorId.HasValue || selectedEvent.HasValue;

    private static BattleEventKind? GetNextKind(BattleEventKind? current) =>
        current switch
        {
            null => BattleEventKind.Move,
            BattleEventKind.Move => BattleEventKind.Attack,
            BattleEventKind.Attack => BattleEventKind.Damage,
            BattleEventKind.Damage => BattleEventKind.Death,
            BattleEventKind.Death => BattleEventKind.Outcome,
            _ => null,
        };

    private static int? GetNextFaction(int? current) =>
        current switch
        {
            null => 0,
            0 => 1,
            _ => null,
        };
}
