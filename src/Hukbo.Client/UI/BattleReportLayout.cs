using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// The seven regions of <see cref="BattleReportPanel"/>, stacked
/// top-to-bottom inside the panel's own padded interior:
/// <see cref="HeaderBounds"/> and <see cref="CloseButtonBounds"/> share a
/// row, then <see cref="FactionTotalsBounds"/>, then
/// <see cref="HighlightsBounds"/>, then <see cref="LeaderboardHeaderBounds"/>
/// followed by <see cref="LeaderboardListBounds"/> and its
/// <see cref="ScrollbarBounds"/>.
/// </summary>
internal readonly record struct BattleReportPanelLayout(
    Rectangle Bounds,
    Rectangle HeaderBounds,
    Rectangle CloseButtonBounds,
    Rectangle FactionTotalsBounds,
    Rectangle HighlightsBounds,
    Rectangle LeaderboardHeaderBounds,
    Rectangle LeaderboardListBounds,
    Rectangle ScrollbarBounds);

/// <summary>
/// Pure layout math for <see cref="BattleReportPanel"/>. No
/// <c>GraphicsDevice</c>/<c>SpriteBatch</c> here — <see cref="Calculate"/> and
/// <see cref="GetVisibleRowCount"/> are the helpers the panel's tests
/// exercise directly.
/// </summary>
internal static class BattleReportLayout
{
    internal const int PreferredWidth = 720;
    internal const int PreferredHeight = 560;
    internal const int MinimumWidth = 480;

    private const int Margin = 20;
    private const int Padding = 16;

    // Carries the Title rung ("BATTLE REPORT", measured 35px real line
    // spacing).
    private const int HeaderHeight = 40;
    private const int CloseButtonSize = 28;
    private const int SectionGap = 10;

    // Two lines at the Body rung (measured 24px real line spacing).
    private const int FactionTotalsLineHeight = 24;
    private const int FactionTotalsHeight = FactionTotalsLineHeight * 2;

    // Up to four lines at the Caption rung (measured 20px real line
    // spacing): first blood, decisive kill, longest survivor, and the
    // kill-credit heuristic disclosure. The first three are omitted, not
    // blanked, when their field is null; the disclosure line always draws.
    private const int HighlightsLineHeight = 20;
    internal const int HighlightsLineCount = 4;
    private const int HighlightsHeight = HighlightsLineHeight * HighlightsLineCount;

    // "WARRIOR LEADERBOARD" draws at the Caption rung (measured 20px). 25
    // clears it.
    private const int LeaderboardHeaderHeight = 25;

    // Reuses BattleEventLogPanel.RowHeight (30, internal there) directly so
    // the two panels' row rhythm never drifts apart. ScrollbarWidth mirrors
    // BattleEventLogPanel's own private constant of the same value (8): it
    // cannot be referenced directly because that field is private to a file
    // outside this workstream's ownership, so the value is duplicated here
    // rather than shared.
    internal static int RowHeight => BattleEventLogPanel.RowHeight;
    private const int ScrollbarWidth = 8;
    private const int ScrollbarGap = 4;

    internal static BattleReportPanelLayout Calculate(Rectangle arenaContentBounds)
    {
        var preferredWidth = UiScaleContext.Pixels(PreferredWidth);
        var preferredHeight = UiScaleContext.Pixels(PreferredHeight);
        var minimumWidth = UiScaleContext.Pixels(MinimumWidth);
        var margin = UiScaleContext.Pixels(Margin);
        var padding = UiScaleContext.Pixels(Padding);
        var headerBaselineHeight = UiScaleContext.Pixels(HeaderHeight);
        var closeButtonSize = UiScaleContext.Pixels(CloseButtonSize);
        var sectionGap = UiScaleContext.Pixels(SectionGap);
        var factionTotalsBaselineHeight =
            UiScaleContext.Pixels(FactionTotalsHeight);
        var highlightsBaselineHeight =
            UiScaleContext.Pixels(HighlightsHeight);
        var leaderboardHeaderBaselineHeight =
            UiScaleContext.Pixels(LeaderboardHeaderHeight);
        var scrollbarWidth = UiScaleContext.Pixels(ScrollbarWidth);
        var scrollbarGap = UiScaleContext.Pixels(ScrollbarGap);

        var width = Math.Min(
            preferredWidth,
            Math.Max(minimumWidth, arenaContentBounds.Width - (margin * 2)));
        width = Math.Min(width, Math.Max(0, arenaContentBounds.Width));
        var height = Math.Min(
            preferredHeight,
            Math.Max(0, arenaContentBounds.Height - (margin * 2)));
        var bounds = new Rectangle(
            arenaContentBounds.Center.X - (width / 2),
            arenaContentBounds.Center.Y - (height / 2),
            width,
            height);

        var inner = new Rectangle(
            bounds.Left + padding,
            bounds.Top + padding,
            Math.Max(0, bounds.Width - (padding * 2)),
            Math.Max(0, bounds.Height - (padding * 2)));

        var headerHeight = Math.Min(
            headerBaselineHeight,
            Math.Max(0, inner.Height));
        var header = new Rectangle(
            inner.Left,
            inner.Top,
            Math.Max(0, inner.Width - closeButtonSize - sectionGap),
            headerHeight);
        var closeButton = new Rectangle(
            inner.Right - closeButtonSize,
            inner.Top + ((headerHeight - closeButtonSize) / 2),
            closeButtonSize,
            closeButtonSize);

        var factionTotalsTop = header.Bottom + sectionGap;
        var factionTotalsHeight = Math.Min(
            factionTotalsBaselineHeight,
            Math.Max(0, inner.Bottom - factionTotalsTop));
        var factionTotals = new Rectangle(
            inner.Left,
            factionTotalsTop,
            inner.Width,
            factionTotalsHeight);

        var highlightsTop = factionTotals.Bottom + sectionGap;
        var highlightsHeight = Math.Min(
            highlightsBaselineHeight,
            Math.Max(0, inner.Bottom - highlightsTop));
        var highlights = new Rectangle(
            inner.Left,
            highlightsTop,
            inner.Width,
            highlightsHeight);

        var leaderboardHeaderTop = highlights.Bottom + sectionGap;
        var leaderboardHeaderHeight = Math.Min(
            leaderboardHeaderBaselineHeight,
            Math.Max(0, inner.Bottom - leaderboardHeaderTop));
        var leaderboardHeader = new Rectangle(
            inner.Left,
            leaderboardHeaderTop,
            inner.Width,
            leaderboardHeaderHeight);

        var leaderboardListTop = leaderboardHeader.Bottom;
        var leaderboardListHeight = Math.Max(0, inner.Bottom - leaderboardListTop);
        var leaderboardListWidth = Math.Max(
            0,
            inner.Width - scrollbarWidth - scrollbarGap);
        var leaderboardList = new Rectangle(
            inner.Left,
            leaderboardListTop,
            leaderboardListWidth,
            leaderboardListHeight);
        var scrollbar = new Rectangle(
            leaderboardList.Right + scrollbarGap,
            leaderboardListTop,
            scrollbarWidth,
            leaderboardListHeight);

        return new BattleReportPanelLayout(
            bounds,
            header,
            closeButton,
            factionTotals,
            highlights,
            leaderboardHeader,
            leaderboardList,
            scrollbar);
    }

    /// <summary>
    /// How many leaderboard rows fit inside
    /// <see cref="BattleReportPanelLayout.LeaderboardListBounds"/>, following
    /// the same height-divided-by-row-height clipping approach
    /// <c>BattleEventLogPanel</c> uses for its own event list.
    /// </summary>
    internal static int GetVisibleRowCount(BattleReportPanelLayout layout) =>
        Math.Max(0, layout.LeaderboardListBounds.Height / RowHeight);
}
