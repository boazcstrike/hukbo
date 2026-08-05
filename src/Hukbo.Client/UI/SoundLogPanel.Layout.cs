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
    // The row, header, and section heights below are sized against the real
    // baked line spacing of the two rungs this panel draws, not the pixel
    // size alone. `SpriteFont.LineSpacing` is the font's own answer to "how
    // tall does one line need to be", computed by the FreeType rasterizer
    // from the face's ascent, descent, and line gap at the baked size — it is
    // consistently taller than the naive pixel-size estimate because it
    // reserves room for descenders and internal leading that a raw glyph
    // height does not. Both values were measured by baking
    // `Content/Fonts/UiCaption.spritefont` and `Content/Fonts/UiTitle.spritefont`
    // through the same `FontDescriptionImporter`/`FontDescriptionProcessor`
    // pair the real `Content.mgcb` build uses (Reach profile, DesktopGL,
    // `PremultiplyAlpha=True`, `TextureFormat=Color`) and reading the
    // resulting `SpriteFontContent.VerticalLineSpacing`:
    // Rajdhani-SemiBold at 12px (the `Caption` rung) measured 20, and
    // Bebas Neue at 22px (the `Title` rung) measured 35. Guessing from pixel
    // size alone (the "12px needs about 15px" rule of thumb) would have
    // undershot both figures.
    private static int CaptionLineSpacing => UiScaleContext.Pixels(20);
    private static int TitleLineSpacing => UiScaleContext.Pixels(35);

    /// <summary>
    /// Vertical offset of the "SOUND LOG" title's draw position from the
    /// header's top edge. Small and fixed, independent of the line spacing
    /// constants above, so the title never touches the panel's own border.
    /// </summary>
    private static int HeaderTitleTopOffset => UiScaleContext.Pixels(2);

    /// <summary>
    /// Clearance between the bottom of the title line and the top of the
    /// availability caption line stacked beneath it.
    /// </summary>
    private static int HeaderLineGap => UiScaleContext.Pixels(2);

    /// <summary>
    /// Clearance reserved below the availability caption line before the
    /// header's own bottom edge.
    /// </summary>
    private static int HeaderBottomPadding => UiScaleContext.Pixels(3);

    /// <summary>
    /// Vertical offset of the availability caption's draw position from the
    /// header's top edge — directly beneath the title's own line box.
    /// </summary>
    internal static int HeaderCaptionTopOffset =>
        HeaderTitleTopOffset + TitleLineSpacing + HeaderLineGap;

    internal static int BindingRowHeight => CaptionLineSpacing;
    internal static int CueRowHeight => CaptionLineSpacing;
    internal static int MinimumThumbHeight => UiScaleContext.Pixels(18);

    private static int Padding => UiScaleContext.Pixels(10);
    private static int Gap => UiScaleContext.Pixels(6);

    // Tall enough to stack the Title-rung title line and the Caption-rung
    // availability line beneath it without either one clipping into the
    // panel's own header/path seam. See the derivation note above.
    private static int HeaderHeight =>
        HeaderCaptionTopOffset + CaptionLineSpacing + HeaderBottomPadding;

    private static int PathHeight => CaptionLineSpacing;
    private static int SectionHeaderHeight => CaptionLineSpacing;
    private const int MinimumCueRowCount = 3;
    private static int MuteWidth => UiScaleContext.Pixels(54);
    private static int ScrollbarWidth => UiScaleContext.Pixels(8);
    private static int HeaderPathGap => UiScaleContext.Pixels(2);
    private static int ScrollbarGap => UiScaleContext.Pixels(4);
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
        var pathTop = Math.Min(
            inner.Bottom,
            header.Bottom + HeaderPathGap);
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
        //
        // `desiredBindingsHeight` caps the viewport, not the list. That is
        // deliberate and must stay: the list scrolls, so every row is reachable
        // whatever the viewport height, while removing the cap would let the
        // expected-files section keep growing on a tall window until the cue
        // log was left with nothing but its three reserved rows.
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
            Math.Max(
                0,
                bindings.Width - ScrollbarWidth - ScrollbarGap),
            Math.Max(0, bindings.Bottom - bindingRowsTop));
        var bindingScrollbar = new Rectangle(
            Math.Max(bindings.Left, bindings.Right - ScrollbarWidth),
            bindingRowsTop,
            Math.Min(ScrollbarWidth, bindings.Width),
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
            Math.Max(
                0,
                cueList.Width - ScrollbarWidth - ScrollbarGap),
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
            scrollbar,
            bindingScrollbar);
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

    /// <summary>
    /// Holds a scroll offset for the expected-files list inside the range the
    /// viewport can actually show: never above the first row, and never past
    /// the point where the last row sits on the bottom line. The result is zero
    /// whenever every row already fits, so a short list cannot scroll at all.
    /// </summary>
    internal static int ClampBindingScroll(
        int scrollStart,
        int totalRowCount,
        int visibleRowCount)
    {
        var maximumStart = Math.Max(0, totalRowCount - visibleRowCount);
        return Math.Clamp(scrollStart, 0, maximumStart);
    }

    /// <summary>
    /// Decides which of the panel's two lists a wheel notch moves: the
    /// innermost scrollable region under the pointer. Over the expected-files
    /// area the wheel moves the bindings, and everywhere else inside the panel
    /// it moves the cue log — which is both the region under the pointer over
    /// the cue list itself and the fallback over the header, the path line, and
    /// the mute button. The fallback is what stops a notch being swallowed, and
    /// the panel consumes the notch either way so it never reaches the arena
    /// camera behind it.
    /// </summary>
    internal static SoundLogScrollTarget GetWheelTarget(
        SoundLogPanelLayout layout,
        Point pointer) =>
        layout.BindingsBounds.Contains(pointer)
            ? SoundLogScrollTarget.Bindings
            : SoundLogScrollTarget.Cues;

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
