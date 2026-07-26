using Hukbo.Client.Presentation;
using Hukbo.Client.Theming;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.UI;

internal sealed partial class BattleEventLogPanel
{
    internal const int RowHeight = 30;
    internal const int MinimumThumbHeight = 18;
    internal const int DetailLineCount = 6;

    private const int Padding = 10;
    private const int Gap = 6;
    private const int HeaderHeight = 28;
    private const int FilterRowHeight = 26;
    private const int ListHeaderHeight = 25;
    private const int ScrollbarWidth = 8;
    private const int RowsPerWheelDetent = 3;
    private const int MouseWheelDeltaPerDetent = 120;
    private const int MaximumSearchLength = 28;
    private const int DetailsHeaderHeight = 26;
    private const int DetailLineHeight = 18;
    private const int DetailsBottomPadding = 6;
    private const int MinimumDetailsHeight =
        DetailsHeaderHeight +
        (DetailLineCount * DetailLineHeight) +
        DetailsBottomPadding;

    private readonly List<FormattedEvent> _formattedRows = [];
    private Point _pointerPosition;
    private long? _cachedDetailsSequence;
    private int _cachedDetailsWidth;
    private string[] _cachedDetails = [];

    public Rectangle Bounds { get; private set; }

    public bool HasKeyboardFocus =>
        KeyboardFocusTarget != BattleEventKeyboardFocusTarget.None;

    internal BattleEventKeyboardFocusTarget KeyboardFocusTarget
    {
        get;
        private set;
    }

    public UiInteraction Update(
        InputEdges input,
        BattleEventFeed feed,
        Rectangle bounds)
    {
        Bounds = bounds;
        _pointerPosition = input.MousePosition;
        var layout = CalculateLayout(bounds);
        var pointerInside = Bounds.Contains(input.MousePosition);
        var visibleRowCount = GetVisibleRowCount(layout);
        var visibleEntries = feed.GetVisibleEntries(visibleRowCount);

        HandlePointerClick(
            input,
            feed,
            layout,
            pointerInside,
            visibleRowCount,
            visibleEntries);
        HandleWheelScroll(input, feed, pointerInside, visibleRowCount);

        if (HasKeyboardFocus)
        {
            HandleKeyboard(input, feed, visibleRowCount);
        }

        return new UiInteraction(ClientCommand.None, pointerInside);
    }

    public bool HandleEscape(InputEdges input, BattleEventFeed feed)
    {
        if (!HasKeyboardFocus || !input.WasPressed(Keys.Escape))
        {
            return false;
        }

        if (feed.HasActiveFilters)
        {
            feed.ClearFilters();
        }

        KeyboardFocusTarget = BattleEventKeyboardFocusTarget.None;
        return true;
    }

    public void ReleaseKeyboardFocusIfPointerLeaves(
        InputEdges input,
        Rectangle bounds)
    {
        if (!HasKeyboardFocus ||
            !ShouldReleaseKeyboardFocus(
                input.WasLeftMousePressed(),
                input.MousePosition,
                bounds))
        {
            return;
        }

        KeyboardFocusTarget = BattleEventKeyboardFocusTarget.None;
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        BattleEventFeed feed,
        Rectangle bounds,
        UiTheme theme)
    {
        Bounds = bounds;
        var layout = CalculateLayout(bounds);
        SynchronizeCache(feed.Entries);

        spriteBatch.Draw(pixel, Bounds, theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            Bounds,
            theme.Colors.PanelBorder,
            theme.Metrics.BorderThickness);

        DrawHeader(spriteBatch, pixel, font, feed, layout, theme);
        DrawFilters(spriteBatch, pixel, font, feed, layout, theme);
        DrawList(spriteBatch, pixel, font, feed, layout, theme);
        DrawDetails(spriteBatch, pixel, font, feed, layout, theme);
    }

    private void HandlePointerClick(
        InputEdges input,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        bool pointerInside,
        int visibleRowCount,
        ReadOnlySpan<BattleEvent> visibleEntries)
    {
        if (!input.WasLeftMousePressed() || !pointerInside)
        {
            return;
        }

        KeyboardFocusTarget = GetKeyboardFocusTarget(
            layout,
            input.MousePosition);

        if (HandleControlClick(input, feed, layout, visibleRowCount))
        {
            return;
        }

        HandleListAreaClick(
            input,
            feed,
            layout,
            visibleRowCount,
            visibleEntries);
    }

    private static bool HandleControlClick(
        InputEdges input,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        int visibleRowCount)
    {
        if (layout.LatestBounds.Contains(input.MousePosition))
        {
            feed.ReturnToLatest(visibleRowCount);
            return true;
        }

        if (layout.KindFilterBounds.Contains(input.MousePosition))
        {
            feed.SetKindFilter(GetNextKind(feed.KindFilter));
            return true;
        }

        if (layout.FactionFilterBounds.Contains(input.MousePosition))
        {
            feed.SetFactionFilter(GetNextFaction(feed.FactionFilter));
            return true;
        }

        if (layout.ActorFilterBounds.Contains(input.MousePosition))
        {
            if (TryGetActorFilterForClick(
                    feed.ActorFilter,
                    feed.SelectedEvent,
                    out var actorFilter))
            {
                feed.SetActorFilter(actorFilter);
            }

            return true;
        }

        if (feed.HasActiveFilters &&
            layout.ResetBounds.Contains(input.MousePosition))
        {
            feed.ClearFilters();
            return true;
        }

        return false;
    }

    private static void HandleListAreaClick(
        InputEdges input,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        int visibleRowCount,
        ReadOnlySpan<BattleEvent> visibleEntries)
    {
        if (layout.ScrollbarTrackBounds.Contains(input.MousePosition))
        {
            PageFromScrollbar(
                feed,
                layout,
                visibleRowCount,
                input.MousePosition);
            return;
        }

        var visibleIndex = HitTestVisibleRow(
            layout,
            input.MousePosition,
            visibleEntries.Length);
        if (visibleIndex >= 0)
        {
            feed.Select(
                visibleEntries[visibleIndex].Sequence,
                visibleRowCount);
        }
    }

    private static void HandleWheelScroll(
        InputEdges input,
        BattleEventFeed feed,
        bool pointerInside,
        int visibleRowCount)
    {
        if (!pointerInside || input.ScrollWheelDelta == 0)
        {
            return;
        }

        var detents = Math.Max(
            1,
            Math.Abs(input.ScrollWheelDelta) /
            MouseWheelDeltaPerDetent);
        var direction = input.ScrollWheelDelta > 0 ? -1 : 1;
        feed.Scroll(
            direction * detents * RowsPerWheelDetent,
            visibleRowCount);
    }

    private void HandleKeyboard(
        InputEdges input,
        BattleEventFeed feed,
        int visibleRowCount)
    {
        if (input.WasPressed(Keys.Up))
        {
            feed.MoveSelection(-1, visibleRowCount);
        }
        else if (input.WasPressed(Keys.Down))
        {
            feed.MoveSelection(1, visibleRowCount);
        }
        else if (input.WasPressed(Keys.Home))
        {
            feed.SelectFirst(visibleRowCount);
        }
        else if (input.WasPressed(Keys.End))
        {
            feed.SelectLast(visibleRowCount);
        }

        if (KeyboardFocusTarget !=
            BattleEventKeyboardFocusTarget.Search)
        {
            return;
        }

        var query = feed.TextFilter;
        if (input.WasPressed(Keys.Back) && query.Length > 0)
        {
            feed.SetTextFilter(query[..^1]);
            return;
        }

        if (query.Length >= MaximumSearchLength)
        {
            return;
        }

        if (TryReadTextCharacter(input, out var character))
        {
            feed.SetTextFilter(query + character);
        }
    }

    private static bool TryReadTextCharacter(
        InputEdges input,
        out char character)
    {
        for (var offset = 0; offset < 26; offset++)
        {
            if (input.WasPressed((Keys)((int)Keys.A + offset)))
            {
                character = (char)('a' + offset);
                return true;
            }
        }

        for (var offset = 0; offset < 10; offset++)
        {
            if (input.WasPressed((Keys)((int)Keys.D0 + offset)) ||
                input.WasPressed((Keys)((int)Keys.NumPad0 + offset)))
            {
                character = (char)('0' + offset);
                return true;
            }
        }

        if (input.WasPressed(Keys.Space))
        {
            character = ' ';
            return true;
        }

        character = default;
        return false;
    }

    private void DrawHeader(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        UiTheme theme)
    {
        var countText =
            $"{feed.FilteredEntries.Count}/{feed.Entries.Count}";
        spriteBatch.DrawString(
            font,
            "BATTLE EVENTS",
            new Vector2(layout.HeaderBounds.Left, layout.HeaderBounds.Top + 4),
            theme.Colors.TextPrimary,
            0f,
            Vector2.Zero,
            0.72f,
            SpriteEffects.None,
            0f);
        spriteBatch.DrawString(
            font,
            countText,
            new Vector2(
                Math.Max(
                    layout.HeaderBounds.Left,
                    layout.LatestBounds.Left - 51),
                layout.HeaderBounds.Top + 5),
            theme.Colors.TextSecondary,
            0f,
            Vector2.Zero,
            0.60f,
            SpriteEffects.None,
            0f);

        var latestLabel = feed.NewEventCount > 0
            ? $"LATEST +{feed.NewEventCount}"
            : "LATEST";
        DrawControl(
            spriteBatch,
            pixel,
            font,
            layout.LatestBounds,
            latestLabel,
            isActive: feed.IsPinnedToBottom,
            isFocused: false,
            theme,
            textScale: 0.52f);
    }

    private void DrawControl(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        Rectangle bounds,
        string label,
        bool isActive,
        bool isFocused,
        UiTheme theme,
        float textScale = 0.55f,
        TextAlignment horizontalAlignment = TextAlignment.Center,
        bool isEnabled = true)
    {
        var isHovered =
            isEnabled &&
            bounds.Contains(_pointerPosition);
        var fill = !isEnabled
            ? theme.Colors.ActionDisabled
            : isActive
                ? theme.Colors.ActionActive
                : isHovered
                    ? theme.Colors.ActionHover
                    : theme.Colors.ActionDefault;

        spriteBatch.Draw(pixel, bounds, fill);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            bounds,
            isEnabled && isFocused
                ? theme.Colors.ActionFocus
                : theme.Colors.PanelBorder,
            isEnabled && isFocused
                ? theme.Metrics.FocusThickness
                : 1);
        var textColor = isEnabled
            ? theme.Colors.TextInverse
            : theme.Colors.TextDisabled;

        if (horizontalAlignment == TextAlignment.Center)
        {
            UiPrimitives.DrawCenteredText(
                spriteBatch,
                font,
                label,
                bounds.Center.ToVector2(),
                textColor,
                textScale);
            return;
        }

        spriteBatch.DrawString(
            font,
            ClipLabel(label, Math.Max(4, (bounds.Width - 12) / 7)),
            new Vector2(bounds.Left + 7, bounds.Top + 7),
            textColor,
            0f,
            Vector2.Zero,
            textScale,
            SpriteEffects.None,
            0f);
    }

    private static string ClipLabel(string label, int maximumCharacters)
    {
        const string Ellipsis = "...";

        if (maximumCharacters <= 0)
        {
            return string.Empty;
        }

        if (label.Length <= maximumCharacters)
        {
            return label;
        }

        if (maximumCharacters <= Ellipsis.Length)
        {
            return Ellipsis[..maximumCharacters];
        }

        return string.Concat(
            label.AsSpan(0, maximumCharacters - Ellipsis.Length),
            Ellipsis);
    }

    private enum TextAlignment
    {
        Left,
        Center,
    }
}
