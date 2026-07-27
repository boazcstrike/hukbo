using Hukbo.Client.Presentation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// Pure layout math and hit testing for <see cref="BattleEventLogPanel"/>.
/// No <c>SpriteBatch</c>/<c>GraphicsDevice</c> here — these are the helpers
/// the panel's tests exercise directly.
/// </summary>
internal sealed partial class BattleEventLogPanel
{
    private readonly record struct FilterRowLayout(
        Rectangle Kind,
        Rectangle Faction,
        Rectangle Actor,
        Rectangle Search,
        Rectangle Reset,
        int ContentTop);

    private readonly record struct ContentAreaLayout(
        Rectangle List,
        Rectangle Rows,
        Rectangle Scrollbar,
        Rectangle Details);

    internal static BattleEventPanelLayout CalculateLayout(Rectangle bounds)
    {
        var inner = ComputeInnerBounds(bounds);
        var (header, latest) = ComputeHeaderLayout(inner);
        var filterRows = ComputeFilterRowLayout(inner, header);
        var content = ComputeContentAreaLayout(inner, filterRows.ContentTop);

        return new BattleEventPanelLayout(
            header,
            latest,
            filterRows.Kind,
            filterRows.Faction,
            filterRows.Actor,
            filterRows.Search,
            filterRows.Reset,
            content.List,
            content.Rows,
            content.Scrollbar,
            content.Details);
    }

    private static Rectangle ComputeInnerBounds(Rectangle bounds)
    {
        var horizontalPadding = Math.Min(Padding, Math.Max(0, bounds.Width / 4));
        var verticalPadding = Math.Min(Padding, Math.Max(0, bounds.Height / 8));
        return new Rectangle(
            bounds.Left + horizontalPadding,
            bounds.Top + verticalPadding,
            Math.Max(0, bounds.Width - (horizontalPadding * 2)),
            Math.Max(0, bounds.Height - (verticalPadding * 2)));
    }

    private static (Rectangle Header, Rectangle Latest) ComputeHeaderLayout(
        Rectangle inner)
    {
        var headerHeight = Math.Min(HeaderHeight, inner.Height);
        var header = new Rectangle(
            inner.Left,
            inner.Top,
            inner.Width,
            headerHeight);
        var latestWidth = Math.Min(72, Math.Max(0, header.Width / 4));
        var latest = new Rectangle(
            header.Right - latestWidth,
            header.Top,
            latestWidth,
            header.Height);
        return (header, latest);
    }

    private static FilterRowLayout ComputeFilterRowLayout(
        Rectangle inner,
        Rectangle header)
    {
        var controlsTop = Math.Min(inner.Bottom, header.Bottom + Gap);
        var availableAfterHeader = Math.Max(0, inner.Bottom - controlsTop);
        var controlsHeight = Math.Min(
            (FilterRowHeight * 2) + Gap,
            availableAfterHeader);
        var firstFilterHeight = Math.Min(FilterRowHeight, controlsHeight);
        var secondFilterTop = Math.Min(
            inner.Bottom,
            controlsTop + firstFilterHeight + Gap);
        var secondFilterHeight = Math.Min(
            FilterRowHeight,
            Math.Max(0, inner.Bottom - secondFilterTop));
        var controlGap = Math.Min(Gap, Math.Max(0, inner.Width / 8));
        var compactControlsWidth = Math.Max(
            0,
            inner.Width - (controlGap * 2));
        var compactControlWidth = compactControlsWidth / 3;
        var kind = new Rectangle(
            inner.Left,
            controlsTop,
            compactControlWidth,
            firstFilterHeight);
        var faction = new Rectangle(
            Math.Min(inner.Right, kind.Right + controlGap),
            controlsTop,
            compactControlWidth,
            firstFilterHeight);
        var actor = new Rectangle(
            Math.Min(inner.Right, faction.Right + controlGap),
            controlsTop,
            Math.Max(0, inner.Right - faction.Right - controlGap),
            firstFilterHeight);
        var resetWidth = Math.Min(62, Math.Max(0, inner.Width / 4));
        var search = new Rectangle(
            inner.Left,
            secondFilterTop,
            Math.Max(0, inner.Width - resetWidth - controlGap),
            secondFilterHeight);
        var reset = new Rectangle(
            Math.Min(inner.Right, search.Right + controlGap),
            secondFilterTop,
            Math.Max(0, inner.Right - search.Right - controlGap),
            secondFilterHeight);
        var contentTop = Math.Min(
            inner.Bottom,
            secondFilterTop + secondFilterHeight + Gap);

        return new FilterRowLayout(kind, faction, actor, search, reset, contentTop);
    }

    private static ContentAreaLayout ComputeContentAreaLayout(
        Rectangle inner,
        int contentTop)
    {
        var availableContentHeight = Math.Max(0, inner.Bottom - contentTop);
        var detailsHeight = Math.Min(
            MaximumDetailsHeight,
            Math.Max(
                Math.Min(MinimumDetailsHeight, availableContentHeight),
                availableContentHeight / 3));
        var detailsTop = Math.Max(contentTop, inner.Bottom - detailsHeight);
        var details = new Rectangle(
            inner.Left,
            detailsTop,
            inner.Width,
            Math.Max(0, inner.Bottom - detailsTop));
        var listBottom = Math.Max(contentTop, details.Top - Gap);
        var list = new Rectangle(
            inner.Left,
            contentTop,
            inner.Width,
            Math.Max(0, listBottom - contentTop));
        var rowsTop = Math.Min(list.Bottom, list.Top + ListHeaderHeight);
        var rows = new Rectangle(
            list.Left,
            rowsTop,
            Math.Max(0, list.Width - ScrollbarWidth - 4),
            Math.Max(0, list.Bottom - rowsTop));
        var scrollbar = new Rectangle(
            Math.Max(list.Left, list.Right - ScrollbarWidth),
            rowsTop,
            Math.Min(ScrollbarWidth, list.Width),
            Math.Max(0, list.Bottom - rowsTop));

        return new ContentAreaLayout(list, rows, scrollbar, details);
    }

    internal static int HitTestVisibleRow(
        BattleEventPanelLayout layout,
        Point point,
        int visibleEntryCount)
    {
        if (!layout.RowsBounds.Contains(point) || visibleEntryCount <= 0)
        {
            return -1;
        }

        var index = (point.Y - layout.RowsBounds.Top) / RowHeight;
        return index < visibleEntryCount ? index : -1;
    }

    internal static BattleEventFilterTarget HitTestFilter(
        BattleEventPanelLayout layout,
        Point point)
    {
        if (layout.KindFilterBounds.Contains(point))
        {
            return BattleEventFilterTarget.Kind;
        }

        if (layout.FactionFilterBounds.Contains(point))
        {
            return BattleEventFilterTarget.Faction;
        }

        if (layout.ActorFilterBounds.Contains(point))
        {
            return BattleEventFilterTarget.Actor;
        }

        if (layout.SearchBounds.Contains(point))
        {
            return BattleEventFilterTarget.Search;
        }

        return layout.ResetBounds.Contains(point)
            ? BattleEventFilterTarget.Reset
            : BattleEventFilterTarget.None;
    }

    internal static BattleEventKeyboardFocusTarget GetKeyboardFocusTarget(
        BattleEventPanelLayout layout,
        Point point)
    {
        if (layout.SearchBounds.Contains(point))
        {
            return BattleEventKeyboardFocusTarget.Search;
        }

        return layout.HeaderBounds.Contains(point) ||
               layout.KindFilterBounds.Contains(point) ||
               layout.FactionFilterBounds.Contains(point) ||
               layout.ActorFilterBounds.Contains(point) ||
               layout.ResetBounds.Contains(point) ||
               layout.ListBounds.Contains(point) ||
               layout.DetailsBounds.Contains(point)
            ? BattleEventKeyboardFocusTarget.List
            : BattleEventKeyboardFocusTarget.None;
    }

    internal static Rectangle GetDetailLineBounds(
        BattleEventPanelLayout layout,
        int lineIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lineIndex);
        if (lineIndex >= DetailLineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        }

        return new Rectangle(
            layout.DetailsBounds.Left + 9,
            layout.DetailsBounds.Top +
            DetailsHeaderHeight +
            (lineIndex * DetailLineHeight),
            Math.Max(0, layout.DetailsBounds.Width - 18),
            DetailLineHeight);
    }

    internal static Rectangle GetScrollbarThumb(
        Rectangle trackBounds,
        int totalEntryCount,
        int visibleRowCount,
        int scrollStart)
    {
        if (trackBounds.Height <= 0 ||
            totalEntryCount <= 0 ||
            visibleRowCount >= totalEntryCount)
        {
            return trackBounds;
        }

        var thumbHeight = Math.Min(
            trackBounds.Height,
            Math.Max(
                MinimumThumbHeight,
                (int)Math.Round(
                    trackBounds.Height *
                    (visibleRowCount / (double)totalEntryCount))));
        var maximumStart = totalEntryCount - visibleRowCount;
        var clampedStart = Math.Clamp(scrollStart, 0, maximumStart);
        var travel = trackBounds.Height - thumbHeight;
        var thumbTop = trackBounds.Top +
            (int)Math.Round(travel * (clampedStart / (double)maximumStart));
        return new Rectangle(
            trackBounds.Left,
            thumbTop,
            trackBounds.Width,
            thumbHeight);
    }

    internal static BattleEventPanelState GetPanelState(
        int retainedCount,
        int filteredCount) =>
        retainedCount == 0
            ? BattleEventPanelState.NoEvents
            : filteredCount == 0
                ? BattleEventPanelState.NoMatches
                : BattleEventPanelState.Events;

    internal static bool ShouldReleaseKeyboardFocus(
        bool wasLeftMousePressed,
        Point pointerPosition,
        Rectangle bounds) =>
        wasLeftMousePressed && !bounds.Contains(pointerPosition);

    private static int GetVisibleRowCount(BattleEventPanelLayout layout) =>
        Math.Max(0, layout.RowsBounds.Height / RowHeight);

    private static void PageFromScrollbar(
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        int visibleRowCount,
        Point pointerPosition)
    {
        var thumb = GetScrollbarThumb(
            layout.ScrollbarTrackBounds,
            feed.FilteredEntries.Count,
            visibleRowCount,
            feed.GetScrollStart(visibleRowCount));
        if (pointerPosition.Y < thumb.Top)
        {
            feed.Scroll(-Math.Max(1, visibleRowCount), visibleRowCount);
        }
        else if (pointerPosition.Y >= thumb.Bottom)
        {
            feed.Scroll(Math.Max(1, visibleRowCount), visibleRowCount);
        }
    }
}
