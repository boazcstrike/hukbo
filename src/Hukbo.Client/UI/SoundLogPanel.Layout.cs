using Hukbo.Client.Audio;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// Pure layout math, hit testing, and colour selection for
/// <see cref="SoundLogPanel"/>. No <c>SpriteBatch</c> and no
/// <c>GraphicsDevice</c>: these are the helpers the panel's tests exercise
/// directly.
/// </summary>
internal sealed partial class SoundLogPanel
{
    internal const int BindingRowHeight = 15;
    internal const int CueRowHeight = 15;
    internal const int MinimumThumbHeight = 18;

    private const int Padding = 10;
    private const int Gap = 6;
    private const int HeaderHeight = 24;
    private const int PathHeight = 14;
    private const int SectionHeaderHeight = 16;
    private const int MinimumCueRowCount = 3;
    private const int MuteWidth = 54;
    private const int ScrollbarWidth = 8;
    private const int RowsPerWheelDetent = 3;
    private const int MouseWheelDeltaPerDetent = 120;

    internal static SoundLogPanelLayout CalculateLayout(Rectangle bounds)
    {
        var inner = ComputeInnerBounds(bounds);
        var headerHeight = Math.Min(HeaderHeight, inner.Height);
        var header = new Rectangle(
            inner.Left,
            inner.Top,
            inner.Width,
            headerHeight);
        var muteWidth = Math.Min(MuteWidth, Math.Max(0, header.Width / 3));
        var mute = new Rectangle(
            header.Right - muteWidth,
            header.Top,
            muteWidth,
            header.Height);
        var pathTop = Math.Min(inner.Bottom, header.Bottom + 2);
        var path = new Rectangle(
            inner.Left,
            pathTop,
            inner.Width,
            Math.Min(PathHeight, Math.Max(0, inner.Bottom - pathTop)));

        var contentTop = Math.Min(inner.Bottom, path.Bottom + Gap);
        var available = Math.Max(0, inner.Bottom - contentTop);

        // The expected-files list is what tells the owner how to name a file, so
        // it gets the room it needs first — capped only by the cue rows the log
        // must still be able to show.
        var desiredBindingsHeight =
            SectionHeaderHeight +
            (SoundCatalog.AllSounds.Count * BindingRowHeight);
        var reservedCueHeight =
            SectionHeaderHeight + (MinimumCueRowCount * CueRowHeight);
        var bindingsHeight = Math.Min(
            desiredBindingsHeight,
            Math.Max(0, available - Gap - reservedCueHeight));
        var bindings = new Rectangle(
            inner.Left,
            contentTop,
            inner.Width,
            bindingsHeight);
        var bindingRowsTop = Math.Min(
            bindings.Bottom,
            bindings.Top + SectionHeaderHeight);
        var bindingRows = new Rectangle(
            bindings.Left,
            bindingRowsTop,
            bindings.Width,
            Math.Max(0, bindings.Bottom - bindingRowsTop));

        var cueTop = Math.Min(inner.Bottom, bindings.Bottom + Gap);
        var cueList = new Rectangle(
            inner.Left,
            cueTop,
            inner.Width,
            Math.Max(0, inner.Bottom - cueTop));
        var cueRowsTop = Math.Min(
            cueList.Bottom,
            cueList.Top + SectionHeaderHeight);
        var cueRows = new Rectangle(
            cueList.Left,
            cueRowsTop,
            Math.Max(0, cueList.Width - ScrollbarWidth - 4),
            Math.Max(0, cueList.Bottom - cueRowsTop));
        var scrollbar = new Rectangle(
            Math.Max(cueList.Left, cueList.Right - ScrollbarWidth),
            cueRowsTop,
            Math.Min(ScrollbarWidth, cueList.Width),
            Math.Max(0, cueList.Bottom - cueRowsTop));

        return new SoundLogPanelLayout(
            header,
            mute,
            path,
            bindings,
            bindingRows,
            cueList,
            cueRows,
            scrollbar);
    }

    private static Rectangle ComputeInnerBounds(Rectangle bounds)
    {
        var horizontalPadding = Math.Min(
            Padding,
            Math.Max(0, bounds.Width / 4));
        var verticalPadding = Math.Min(
            Padding,
            Math.Max(0, bounds.Height / 8));
        return new Rectangle(
            bounds.Left + horizontalPadding,
            bounds.Top + verticalPadding,
            Math.Max(0, bounds.Width - (horizontalPadding * 2)),
            Math.Max(0, bounds.Height - (verticalPadding * 2)));
    }

    /// <summary>
    /// Prefix for an indented per-class sub-row, keeping it visually distinct
    /// from a slot's own header row.
    /// </summary>
    private const string ClassRowIndent = "  ";

    /// <summary>
    /// Flattens every slot's binding into the rows the "EXPECTED FILES"
    /// section draws: one header row per slot, plus — for a hit-location
    /// driven slot — one indented sub-row per acoustic class showing its raw,
    /// pre-fallback variant count.
    /// </summary>
    internal static IReadOnlyList<SoundBindingRow> BuildBindingRows(
        IReadOnlyList<SoundBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        var rows = new List<SoundBindingRow>();
        foreach (var binding in bindings)
        {
            rows.Add(
                new SoundBindingRow(
                    binding.FileName,
                    FormatRowStatus(binding.Status, binding.VariantCount),
                    binding.Status));

            foreach (var classCount in binding.ClassCounts)
            {
                rows.Add(
                    new SoundBindingRow(
                        ClassRowIndent + HitClassCatalog.GetToken(classCount.HitClass),
                        FormatRowStatus(classCount.Status, classCount.Count),
                        classCount.Status));
            }
        }

        return rows;
    }

    private static string FormatRowStatus(SoundBindingStatus status, int variantCount) =>
        status == SoundBindingStatus.Ready
            ? $"{SoundCatalog.GetStatusLabel(status)} ({variantCount})"
            : SoundCatalog.GetStatusLabel(status);

    internal static int GetVisibleBindingRowCount(SoundLogPanelLayout layout) =>
        Math.Max(0, layout.BindingRowsBounds.Height / BindingRowHeight);

    internal static int GetVisibleCueRowCount(SoundLogPanelLayout layout) =>
        Math.Max(0, layout.CueRowsBounds.Height / CueRowHeight);

    internal static Rectangle GetBindingRowBounds(
        SoundLogPanelLayout layout,
        int rowIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);

        return new Rectangle(
            layout.BindingRowsBounds.Left,
            layout.BindingRowsBounds.Top + (rowIndex * BindingRowHeight),
            layout.BindingRowsBounds.Width,
            BindingRowHeight);
    }

    internal static Rectangle GetCueRowBounds(
        SoundLogPanelLayout layout,
        int rowIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);

        return new Rectangle(
            layout.CueRowsBounds.Left,
            layout.CueRowsBounds.Top + (rowIndex * CueRowHeight),
            layout.CueRowsBounds.Width,
            CueRowHeight);
    }

    internal static bool HitTestMute(
        SoundLogPanelLayout layout,
        Point point) =>
        layout.MuteBounds.Contains(point);

    internal static Color GetBindingStatusColor(
        UiThemeColors colors,
        SoundBindingStatus status)
    {
        ArgumentNullException.ThrowIfNull(colors);

        return status switch
        {
            SoundBindingStatus.Ready => colors.StatusSuccess,
            SoundBindingStatus.Missing => colors.TextDisabled,
            SoundBindingStatus.LoadFailed => colors.StatusDanger,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                null),
        };
    }

    internal static Color GetCueStatusColor(
        UiThemeColors colors,
        SoundCueStatus status)
    {
        ArgumentNullException.ThrowIfNull(colors);

        return status switch
        {
            SoundCueStatus.Played => colors.StatusSuccess,
            SoundCueStatus.Missing => colors.StatusWarning,
            SoundCueStatus.LoadFailed => colors.StatusDanger,
            SoundCueStatus.Muted => colors.TextDisabled,
            SoundCueStatus.Suppressed => colors.StatusInfo,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                null),
        };
    }

    internal static int GetScrollRowDelta(int scrollWheelDelta)
    {
        if (scrollWheelDelta == 0)
        {
            return 0;
        }

        var detents = Math.Max(
            1,
            Math.Abs(scrollWheelDelta) / MouseWheelDeltaPerDetent);
        var direction = scrollWheelDelta > 0 ? -1 : 1;
        return direction * detents * RowsPerWheelDetent;
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
}
