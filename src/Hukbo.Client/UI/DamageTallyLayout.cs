using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// One faction's section of the damage card: a header line that also carries
/// the column captions, then <see cref="VisibleRowCount"/> rows stacked inside
/// <see cref="RowsBounds"/>. Row <c>n</c> is
/// <see cref="DamageTallyLayout.GetRowBounds"/>.
/// </summary>
internal readonly record struct DamageTallySectionLayout(
    int VisibleRowCount,
    Rectangle HeaderBounds,
    Rectangle RowsBounds);

/// <summary>
/// The regions of <see cref="DamageTallyPanel"/>, stacked top to bottom:
/// <see cref="TitleBounds"/>, the two faction sections, then
/// <see cref="FooterBounds"/>.
/// </summary>
internal readonly record struct DamageTallyPanelLayout(
    Rectangle Bounds,
    Rectangle TitleBounds,
    DamageTallySectionLayout FirstSection,
    DamageTallySectionLayout SecondSection,
    Rectangle FooterBounds)
{
    public DamageTallySectionLayout GetSection(int factionId) =>
        factionId switch
        {
            0 => FirstSection,
            1 => SecondSection,
            _ => throw new ArgumentOutOfRangeException(nameof(factionId), factionId, null),
        };
}

/// <summary>
/// The five columns of one row, left to right and never overlapping.
/// </summary>
internal readonly record struct DamageTallyRowColumns(
    Rectangle Label,
    Rectangle Damage,
    Rectangle Share,
    Rectangle Blows,
    Rectangle Alive);

/// <summary>
/// The row under the pointer: which faction's section and which row of the
/// sorted list.
/// </summary>
internal readonly record struct DamageTallyRowHit(int FactionId, int RowIndex);

/// <summary>
/// Pure layout math for <see cref="DamageTallyPanel"/>, modelled on
/// <see cref="BattleReportLayout"/>. No <c>GraphicsDevice</c> or
/// <c>SpriteBatch</c> here.
/// </summary>
internal static class DamageTallyLayout
{
    internal const int PreferredWidth = 600;

    private const int Margin = 20;
    private const int Padding = 12;

    // Body rung, measured 24px real line spacing.
    private const int TitleHeight = 28;

    // Caption rung, measured 20px real line spacing.
    private const int SectionHeaderHeight = 24;
    private const int RowHeightBaseline = 26;
    private const int FooterHeight = 24;
    private const int SectionGap = 8;

    // Column widths, fixed so the numbers line up from row to row; the label
    // column takes whatever is left.
    private const int DamageColumnWidth = 76;
    private const int ShareColumnWidth = 52;
    private const int BlowsColumnWidth = 60;
    private const int AliveColumnWidth = 92;

    internal static int RowHeight => UiScaleContext.Pixels(RowHeightBaseline);

    /// <summary>
    /// Places the card against the top-right corner of
    /// <paramref name="arenaContentBounds"/>, inside the margin. When the
    /// arena is too short for every row, the lowest rows are dropped: the
    /// lists are sorted by damage, so the rows that go are the weakest. The
    /// two factions share the row budget, and a faction with few rows lends
    /// its unused space to the other.
    /// </summary>
    /// <param name="firstRowCount">Rows in faction 0's sorted list.</param>
    /// <param name="secondRowCount">Rows in faction 1's sorted list.</param>
    internal static DamageTallyPanelLayout Calculate(
        Rectangle arenaContentBounds,
        int firstRowCount,
        int secondRowCount)
    {
        var margin = UiScaleContext.Pixels(Margin);
        var padding = UiScaleContext.Pixels(Padding);
        var titleHeight = UiScaleContext.Pixels(TitleHeight);
        var sectionHeaderHeight = UiScaleContext.Pixels(SectionHeaderHeight);
        var footerHeight = UiScaleContext.Pixels(FooterHeight);
        var sectionGap = UiScaleContext.Pixels(SectionGap);
        var rowHeight = RowHeight;

        var width = Math.Min(
            UiScaleContext.Pixels(PreferredWidth),
            Math.Max(0, arenaContentBounds.Width - (margin * 2)));
        var availableHeight = Math.Max(0, arenaContentBounds.Height - (margin * 2));

        var chromeHeight = (padding * 2) + titleHeight + footerHeight +
            (2 * (sectionHeaderHeight + sectionGap));
        var rowBudget = Math.Max(0, availableHeight - chromeHeight) / rowHeight;
        var half = rowBudget / 2;
        var firstVisible = Math.Min(
            Math.Max(0, firstRowCount),
            Math.Max(half, rowBudget - Math.Max(0, secondRowCount)));
        var secondVisible = Math.Min(
            Math.Max(0, secondRowCount),
            rowBudget - firstVisible);

        var height = Math.Min(
            availableHeight,
            chromeHeight + ((firstVisible + secondVisible) * rowHeight));
        var bounds = new Rectangle(
            arenaContentBounds.Right - margin - width,
            arenaContentBounds.Top + margin,
            width,
            height);

        var innerLeft = bounds.Left + padding;
        var innerWidth = Math.Max(0, width - (padding * 2));
        var cursor = bounds.Top + padding;

        var title = new Rectangle(innerLeft, cursor, innerWidth, titleHeight);
        cursor = title.Bottom;

        var first = LayoutSection(
            innerLeft, innerWidth, ref cursor, sectionHeaderHeight, rowHeight, firstVisible);
        cursor += sectionGap;
        var second = LayoutSection(
            innerLeft, innerWidth, ref cursor, sectionHeaderHeight, rowHeight, secondVisible);
        cursor += sectionGap;

        var footer = new Rectangle(innerLeft, cursor, innerWidth, footerHeight);

        return new DamageTallyPanelLayout(bounds, title, first, second, footer);
    }

    internal static Rectangle GetRowBounds(DamageTallySectionLayout section, int rowIndex) =>
        new(
            section.RowsBounds.Left,
            section.RowsBounds.Top + (rowIndex * RowHeight),
            section.RowsBounds.Width,
            RowHeight);

    /// <summary>
    /// Splits a row, or a section header, into its five columns. Numeric
    /// columns are laid right to left from the row's right edge, each
    /// clamped to the room left, so no two columns overlap at any width.
    /// </summary>
    internal static DamageTallyRowColumns GetRowColumns(Rectangle rowBounds)
    {
        var gap = UiScaleContext.Pixels(4);
        var right = rowBounds.Right;

        var alive = TakeFromRight(rowBounds, ref right, UiScaleContext.Pixels(AliveColumnWidth));
        var blows = TakeFromRight(rowBounds, ref right, UiScaleContext.Pixels(BlowsColumnWidth));
        var share = TakeFromRight(rowBounds, ref right, UiScaleContext.Pixels(ShareColumnWidth));
        var damage = TakeFromRight(rowBounds, ref right, UiScaleContext.Pixels(DamageColumnWidth));
        var label = new Rectangle(
            rowBounds.Left,
            rowBounds.Top,
            Math.Max(0, right - rowBounds.Left - gap),
            rowBounds.Height);

        return new DamageTallyRowColumns(label, damage, share, blows, alive);
    }

    /// <summary>
    /// The filled part of a row's bar, anchored left, as a fraction of the
    /// row's width. <paramref name="fraction"/> is clamped to 0..1.
    /// </summary>
    internal static Rectangle GetBarBounds(Rectangle rowBounds, float fraction)
    {
        var clamped = Math.Clamp(float.IsFinite(fraction) ? fraction : 0f, 0f, 1f);
        return new Rectangle(
            rowBounds.Left,
            rowBounds.Top,
            (int)(rowBounds.Width * clamped),
            rowBounds.Height);
    }

    /// <summary>
    /// The row under <paramref name="point"/>, or <c>null</c> when it is
    /// over no row, including a header, the title, the footer, or a row the
    /// layout dropped.
    /// </summary>
    internal static DamageTallyRowHit? HitTestRow(
        DamageTallyPanelLayout layout,
        Point point)
    {
        for (var factionId = 0; factionId < 2; factionId++)
        {
            var section = layout.GetSection(factionId);
            if (section.VisibleRowCount == 0 ||
                !section.RowsBounds.Contains(point))
            {
                continue;
            }

            var rowIndex = (point.Y - section.RowsBounds.Top) / RowHeight;
            return new DamageTallyRowHit(factionId, rowIndex);
        }

        return null;
    }

    private static DamageTallySectionLayout LayoutSection(
        int left,
        int width,
        ref int cursor,
        int headerHeight,
        int rowHeight,
        int visibleRows)
    {
        var header = new Rectangle(left, cursor, width, headerHeight);
        var rows = new Rectangle(left, header.Bottom, width, visibleRows * rowHeight);
        cursor = rows.Bottom;
        return new DamageTallySectionLayout(visibleRows, header, rows);
    }

    private static Rectangle TakeFromRight(Rectangle row, ref int right, int preferredWidth)
    {
        var columnWidth = Math.Clamp(preferredWidth, 0, Math.Max(0, right - row.Left));
        var column = new Rectangle(right - columnWidth, row.Top, columnWidth, row.Height);
        right -= columnWidth;
        return column;
    }
}
