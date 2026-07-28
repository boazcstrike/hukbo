using Hukbo.Client.Audio;
using Hukbo.Client.Presentation;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

/// <summary>
/// The sound log view: which files the game expects, which of them it found, and
/// what it did with each cue. Hidden by default and independent of the battle
/// event log, so the two views never compete for the same rows.
/// </summary>
internal sealed partial class SoundLogPanel
{
    private const int StatusColumnWidth = 74;
    private const string Ellipsis = "...";

    private Point _pointerPosition;

    // The expected-files rows are produced by the pure static BuildBindingRows
    // from an IReadOnlyList<SoundBinding> the audio layer owns, so there is no
    // mutable model to host the offset the way SoundCueLog hosts the cue log's.
    // The panel keeps it instead, clamped against a list it does not own,
    // exactly as BattleReportPanel keeps _scrollStart for its leaderboard.
    private int _bindingScrollStart;

    public Rectangle Bounds { get; private set; }

    public UiInteraction Update(
        InputEdges input,
        SoundDirector director,
        Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(director);

        Bounds = bounds;
        _pointerPosition = input.MousePosition;
        var pointerInside = bounds.Contains(input.MousePosition);
        if (!pointerInside)
        {
            return new UiInteraction(ClientCommand.None, false);
        }

        var layout = CalculateLayout(bounds);
        if (input.WasLeftMousePressed() &&
            HitTestMute(layout, input.MousePosition))
        {
            director.ToggleMute();
        }

        // The viewport height can change between one frame and the next, so the
        // bindings offset is re-clamped here as well as in Draw.
        var totalBindingRowCount =
            BuildBindingRows(director.Player.Bindings).Count;
        var visibleBindingRowCount = GetVisibleBindingRowCount(layout);
        _bindingScrollStart = ClampBindingScroll(
            _bindingScrollStart,
            totalBindingRowCount,
            visibleBindingRowCount);

        var rowDelta = GetScrollRowDelta(input.ScrollWheelDelta);
        if (rowDelta != 0)
        {
            // The wheel moves the list under the pointer, and the panel
            // consumes the notch either way so it never reaches the camera.
            if (GetWheelTarget(layout, input.MousePosition) ==
                SoundLogScrollTarget.Bindings)
            {
                _bindingScrollStart = ClampBindingScroll(
                    _bindingScrollStart + rowDelta,
                    totalBindingRowCount,
                    visibleBindingRowCount);
            }
            else
            {
                director.Log.Scroll(rowDelta, GetVisibleCueRowCount(layout));
            }
        }

        return new UiInteraction(ClientCommand.None, true);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        SoundDirector director,
        Rectangle bounds,
        UiTheme theme)
    {
        Bounds = bounds;
        var layout = CalculateLayout(bounds);

        spriteBatch.Draw(pixel, bounds, theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            bounds,
            theme.Colors.PanelBorder,
            theme.Metrics.BorderThickness);

        var titleFont = fonts.Get(UiFontRole.Title);
        var captionFont = fonts.Get(UiFontRole.Caption);
        DrawHeader(spriteBatch, pixel, titleFont, captionFont, director, layout, theme);
        DrawBindings(spriteBatch, pixel, captionFont, director, layout, theme);
        DrawCues(spriteBatch, pixel, captionFont, director, layout, theme);
    }

    private void DrawHeader(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont titleFont,
        SpriteFont captionFont,
        SoundDirector director,
        SoundLogPanelLayout layout,
        UiTheme theme)
    {
        var bindings = director.Player.Bindings;
        var unavailableCount = SoundCatalog.CountUnavailable(bindings);

        UiPrimitives.DrawText(
            spriteBatch,
            titleFont,
            "SOUND LOG",
            new Vector2(
                layout.HeaderBounds.Left,
                layout.HeaderBounds.Top + HeaderTitleTopOffset),
            theme.Colors.TextPrimary);
        UiPrimitives.DrawText(
            spriteBatch,
            captionFont,
            SoundCueFormatter.FormatAvailability(
                unavailableCount,
                bindings.Count) +
                "  " +
                SoundCueFormatter.FormatLoad(
                    director.SoundingVoices,
                    director.NextCueGain),
            new Vector2(
                layout.HeaderBounds.Left,
                layout.HeaderBounds.Top + HeaderCaptionTopOffset),
            unavailableCount == 0
                ? theme.Colors.StatusSuccess
                : theme.Colors.TextSecondary);

        var isMuteHovered = layout.MuteBounds.Contains(_pointerPosition);
        var muteFill = director.IsMuted
            ? theme.Colors.ActionActive
            : isMuteHovered
                ? theme.Colors.ActionHover
                : theme.Colors.ActionDefault;
        spriteBatch.Draw(pixel, layout.MuteBounds, muteFill);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            layout.MuteBounds,
            theme.Colors.PanelBorder,
            1);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            captionFont,
            director.IsMuted ? "MUTED" : "MUTE",
            layout.MuteBounds.Center.ToVector2(),
            theme.Colors.TextInverse);

        UiPrimitives.DrawText(
            spriteBatch,
            captionFont,
            ClipPathTail(
                director.Player.DirectoryPath,
                GetMaximumCharacters(layout.PathBounds.Width)),
            new Vector2(layout.PathBounds.Left, layout.PathBounds.Top),
            theme.Colors.TextSecondary);
    }

    private void DrawBindings(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        SoundDirector director,
        SoundLogPanelLayout layout,
        UiTheme theme)
    {
        UiPrimitives.DrawText(
            spriteBatch,
            font,
            "EXPECTED FILES",
            new Vector2(layout.BindingsBounds.Left, layout.BindingsBounds.Top),
            theme.Colors.TextSecondary);

        var rows = BuildBindingRows(director.Player.Bindings);
        var visibleRowCount = GetVisibleBindingRowCount(layout);
        _bindingScrollStart = ClampBindingScroll(
            _bindingScrollStart,
            rows.Count,
            visibleRowCount);
        if (visibleRowCount <= 0 || rows.Count == 0)
        {
            return;
        }

        // Every viewport row carries real content. There is no "+N more" line:
        // the list scrolls, so the rows past the viewport are reached with the
        // wheel and the thumb below is the affordance that says so — the same
        // arrangement the cue log in this panel already uses.
        var drawnRowCount = Math.Min(
            visibleRowCount,
            rows.Count - _bindingScrollStart);
        for (var offset = 0; offset < drawnRowCount; offset++)
        {
            var row = rows[_bindingScrollStart + offset];
            var rowBounds = GetBindingRowBounds(layout, offset);
            var nameWidth = Math.Max(
                0,
                rowBounds.Width - StatusColumnWidth);
            UiPrimitives.DrawText(
                spriteBatch,
                font,
                ClipText(
                    row.Label,
                    GetMaximumCharacters(nameWidth)),
                new Vector2(rowBounds.Left, rowBounds.Top),
                theme.Colors.TextPrimary);
            UiPrimitives.DrawText(
                spriteBatch,
                font,
                row.StatusText,
                new Vector2(
                    rowBounds.Right - StatusColumnWidth + 4,
                    rowBounds.Top),
                GetBindingStatusColor(theme.Colors, row.Status));
        }

        if (rows.Count <= visibleRowCount)
        {
            return;
        }

        spriteBatch.Draw(
            pixel,
            layout.BindingScrollbarTrackBounds,
            theme.Colors.PanelAlternate);
        spriteBatch.Draw(
            pixel,
            GetScrollbarThumb(
                layout.BindingScrollbarTrackBounds,
                rows.Count,
                visibleRowCount,
                _bindingScrollStart),
            theme.Colors.ActionDefault);
    }

    private static void DrawCues(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        SoundDirector director,
        SoundLogPanelLayout layout,
        UiTheme theme)
    {
        var log = director.Log;
        UiPrimitives.DrawText(
            spriteBatch,
            font,
            $"CUE LOG  {log.Entries.Count}",
            new Vector2(layout.CueListBounds.Left, layout.CueListBounds.Top),
            theme.Colors.TextSecondary);

        var visibleRowCount = GetVisibleCueRowCount(layout);
        if (visibleRowCount <= 0)
        {
            return;
        }

        if (log.Entries.Count == 0)
        {
            var emptyBounds = GetCueRowBounds(layout, 0);
            UiPrimitives.DrawText(
                spriteBatch,
                font,
                "No cues yet.",
                new Vector2(emptyBounds.Left, emptyBounds.Top),
                theme.Colors.TextDisabled);
            return;
        }

        var visibleEntries = log.GetVisibleEntries(visibleRowCount);
        for (var index = 0; index < visibleEntries.Length; index++)
        {
            var cue = visibleEntries[index];
            var rowBounds = GetCueRowBounds(layout, index);
            UiPrimitives.DrawText(
                spriteBatch,
                font,
                ClipText(
                    SoundCueFormatter.Format(cue),
                    GetMaximumCharacters(rowBounds.Width)),
                new Vector2(rowBounds.Left, rowBounds.Top),
                GetCueStatusColor(theme.Colors, cue.Status));
        }

        if (log.Entries.Count <= visibleRowCount)
        {
            return;
        }

        spriteBatch.Draw(
            pixel,
            layout.ScrollbarTrackBounds,
            theme.Colors.PanelAlternate);
        spriteBatch.Draw(
            pixel,
            GetScrollbarThumb(
                layout.ScrollbarTrackBounds,
                log.Entries.Count,
                visibleRowCount,
                log.GetScrollStart(visibleRowCount)),
            theme.Colors.ActionDefault);
    }

    private static int GetMaximumCharacters(int availableWidth) =>
        Math.Max(
            0,
            availableWidth /
                UiFontRamp.GetApproximateAdvancePx(UiFontRole.Caption));

    /// <summary>
    /// Trims text to fit a row, keeping the start.
    /// </summary>
    internal static string ClipText(string text, int maximumCharacters)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (maximumCharacters <= 0)
        {
            return string.Empty;
        }

        if (text.Length <= maximumCharacters)
        {
            return text;
        }

        if (maximumCharacters <= Ellipsis.Length)
        {
            return Ellipsis[..maximumCharacters];
        }

        return string.Concat(
            text.AsSpan(0, maximumCharacters - Ellipsis.Length),
            Ellipsis);
    }

    /// <summary>
    /// Trims a path to fit a row, keeping the tail — the folder the owner drops
    /// files into matters more than the drive it sits on.
    /// </summary>
    internal static string ClipPathTail(string path, int maximumCharacters)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (maximumCharacters <= 0)
        {
            return string.Empty;
        }

        if (path.Length <= maximumCharacters)
        {
            return path;
        }

        if (maximumCharacters <= Ellipsis.Length)
        {
            return Ellipsis[..maximumCharacters];
        }

        return string.Concat(
            Ellipsis,
            path.AsSpan(path.Length - maximumCharacters + Ellipsis.Length));
    }
}
