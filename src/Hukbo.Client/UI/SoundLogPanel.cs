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
    private const float TitleScale = 0.62f;
    private const float SectionScale = 0.52f;
    private const float RowScale = 0.48f;
    private const float MuteScale = 0.50f;
    private const int CharacterWidthEstimate = 6;
    private const int StatusColumnWidth = 74;
    private const string Ellipsis = "...";

    private Point _pointerPosition;

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

        var rowDelta = GetScrollRowDelta(input.ScrollWheelDelta);
        if (rowDelta != 0)
        {
            director.Log.Scroll(rowDelta, GetVisibleCueRowCount(layout));
        }

        return new UiInteraction(ClientCommand.None, true);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
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

        DrawHeader(spriteBatch, pixel, font, director, layout, theme);
        DrawBindings(spriteBatch, font, director, layout, theme);
        DrawCues(spriteBatch, pixel, font, director, layout, theme);
    }

    private void DrawHeader(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        SoundDirector director,
        SoundLogPanelLayout layout,
        UiTheme theme)
    {
        var bindings = director.Player.Bindings;
        var unavailableCount = SoundCatalog.CountUnavailable(bindings);

        DrawText(
            spriteBatch,
            font,
            "SOUND LOG",
            new Vector2(layout.HeaderBounds.Left, layout.HeaderBounds.Top + 3),
            theme.Colors.TextPrimary,
            TitleScale);
        DrawText(
            spriteBatch,
            font,
            SoundCueFormatter.FormatAvailability(
                unavailableCount,
                bindings.Count),
            new Vector2(layout.HeaderBounds.Left, layout.HeaderBounds.Top + 15),
            unavailableCount == 0
                ? theme.Colors.StatusSuccess
                : theme.Colors.TextSecondary,
            SectionScale);

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
            font,
            director.IsMuted ? "MUTED" : "MUTE",
            layout.MuteBounds.Center.ToVector2(),
            theme.Colors.TextInverse,
            MuteScale);

        DrawText(
            spriteBatch,
            font,
            ClipPathTail(
                director.Player.DirectoryPath,
                GetMaximumCharacters(layout.PathBounds.Width)),
            new Vector2(layout.PathBounds.Left, layout.PathBounds.Top),
            theme.Colors.TextSecondary,
            RowScale);
    }

    private static void DrawBindings(
        SpriteBatch spriteBatch,
        SpriteFont font,
        SoundDirector director,
        SoundLogPanelLayout layout,
        UiTheme theme)
    {
        DrawText(
            spriteBatch,
            font,
            "EXPECTED FILES",
            new Vector2(layout.BindingsBounds.Left, layout.BindingsBounds.Top),
            theme.Colors.TextSecondary,
            SectionScale);

        var rows = BuildBindingRows(director.Player.Bindings);
        var visibleRowCount = GetVisibleBindingRowCount(layout);
        if (visibleRowCount <= 0 || rows.Count == 0)
        {
            return;
        }

        var hasOverflow = rows.Count > visibleRowCount;
        var drawnRowCount = hasOverflow ? visibleRowCount - 1 : rows.Count;
        for (var index = 0; index < drawnRowCount; index++)
        {
            var row = rows[index];
            var rowBounds = GetBindingRowBounds(layout, index);
            var nameWidth = Math.Max(
                0,
                rowBounds.Width - StatusColumnWidth);
            DrawText(
                spriteBatch,
                font,
                ClipText(
                    row.Label,
                    GetMaximumCharacters(nameWidth)),
                new Vector2(rowBounds.Left, rowBounds.Top),
                theme.Colors.TextPrimary,
                RowScale);
            DrawText(
                spriteBatch,
                font,
                row.StatusText,
                new Vector2(
                    rowBounds.Right - StatusColumnWidth + 4,
                    rowBounds.Top),
                GetBindingStatusColor(theme.Colors, row.Status),
                RowScale);
        }

        if (!hasOverflow)
        {
            return;
        }

        var overflowBounds = GetBindingRowBounds(layout, drawnRowCount);
        DrawText(
            spriteBatch,
            font,
            $"+{rows.Count - drawnRowCount} more (enlarge the panel)",
            new Vector2(overflowBounds.Left, overflowBounds.Top),
            theme.Colors.TextSecondary,
            RowScale);
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
        DrawText(
            spriteBatch,
            font,
            $"CUE LOG  {log.Entries.Count}",
            new Vector2(layout.CueListBounds.Left, layout.CueListBounds.Top),
            theme.Colors.TextSecondary,
            SectionScale);

        var visibleRowCount = GetVisibleCueRowCount(layout);
        if (visibleRowCount <= 0)
        {
            return;
        }

        if (log.Entries.Count == 0)
        {
            var emptyBounds = GetCueRowBounds(layout, 0);
            DrawText(
                spriteBatch,
                font,
                "No cues yet.",
                new Vector2(emptyBounds.Left, emptyBounds.Top),
                theme.Colors.TextDisabled,
                RowScale);
            return;
        }

        var visibleEntries = log.GetVisibleEntries(visibleRowCount);
        for (var index = 0; index < visibleEntries.Length; index++)
        {
            var cue = visibleEntries[index];
            var rowBounds = GetCueRowBounds(layout, index);
            DrawText(
                spriteBatch,
                font,
                ClipText(
                    SoundCueFormatter.Format(cue),
                    GetMaximumCharacters(rowBounds.Width)),
                new Vector2(rowBounds.Left, rowBounds.Top),
                GetCueStatusColor(theme.Colors, cue.Status),
                RowScale);
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

    private static void DrawText(
        SpriteBatch spriteBatch,
        SpriteFont font,
        string text,
        Vector2 position,
        Color color,
        float scale) =>
        spriteBatch.DrawString(
            font,
            text,
            position,
            color,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f);

    private static int GetMaximumCharacters(int availableWidth) =>
        Math.Max(0, availableWidth / CharacterWidthEstimate);

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
