using System;
using Microsoft.Xna.Framework;
using Sandata.Client.Theming;

namespace Sandata.Client.UI;

/// <summary>
/// Layout and content helpers for the go-code panel: design section 16's
/// go-codes, each a letter assigned to waypoints across several operators,
/// where releasing the letter is itself an order
/// (<c>OrderKind.GoCodeRelease</c>).
/// </summary>
/// <remarks>
/// Design section 11's HUD element list has no row for this panel; it
/// predates the order layer design in section 16. This file only computes
/// this panel's own bounds and content, in the
/// <c>CalculateBounds(windowBounds, ...)</c> shape <c>OperatorInspector</c>,
/// <c>RosterStrip</c>, <c>ContactList</c>, and <c>SandataControlBar</c>
/// already use. Task 71 owns composing it into the HUD layout, choosing its
/// final anchor relative to the other panels, and amending section 11.
/// </remarks>
internal static class GoCodePanel
{
    /// <summary>Distance in pixels this panel keeps from the window edge, and between rows and its border.</summary>
    internal const int Margin = 12; // PROVISIONAL: matches OperatorInspector.Margin; no design value given.

    /// <summary>Fixed panel width in pixels.</summary>
    internal const int Width = 200; // PROVISIONAL: no design value given.

    /// <summary>Height in pixels reserved for the panel's title row.</summary>
    internal const int HeaderHeight = 20; // PROVISIONAL: matches ContactList.HeaderHeight.

    /// <summary>Height in pixels of one go-code row.</summary>
    internal const int RowHeight = 20; // PROVISIONAL: matches RosterStrip's tile-row rhythm.

    /// <summary>The largest number of go-code rows this panel ever reserves height for at once.</summary>
    internal const int MaxVisibleRows = 8; // PROVISIONAL: matches ContactList.MaxVisibleRows.

    /// <summary>
    /// One go-code entry: the letter assigned, how many operators currently
    /// carry it, and whether it has already been released.
    /// </summary>
    internal readonly record struct GoCodeEntry(char Letter, int OperatorCount, bool IsReleased);

    /// <summary>
    /// Computes this panel's screen-space bounds, anchored to the window's
    /// top-right corner and clamped so it never exceeds the window on either
    /// axis. Task 71 may re-anchor it once it is composed alongside the
    /// other HUD panels; this is this panel's own, independent placement.
    /// </summary>
    internal static Rectangle CalculateBounds(Rectangle windowBounds, int goCodeCount)
    {
        var visibleRows = Math.Clamp(goCodeCount, 0, MaxVisibleRows);
        var preferredHeight = HeaderHeight + (visibleRows * RowHeight) + (Margin * 2);
        var maxHeight = Math.Max(0, windowBounds.Height - (Margin * 2));
        var height = Math.Min(preferredHeight, maxHeight);

        var maxWidth = Math.Max(0, windowBounds.Width - (Margin * 2));
        var width = Math.Min(Width, maxWidth);

        return new Rectangle(
            windowBounds.Right - Margin - width,
            windowBounds.Top + Margin,
            width,
            height);
    }

    /// <summary>Computes the bounds of one row within a panel already positioned by <see cref="CalculateBounds"/>.</summary>
    internal static Rectangle CalculateRowBounds(Rectangle panelBounds, int rowIndex) =>
        new(
            panelBounds.Left,
            panelBounds.Top + HeaderHeight + (rowIndex * RowHeight),
            panelBounds.Width,
            RowHeight);

    /// <summary>Formats one go-code entry as the single line of text its row displays.</summary>
    internal static string FormatEntryLine(GoCodeEntry entry) =>
        $"{entry.Letter}: {entry.OperatorCount} ops" + (entry.IsReleased ? " (released)" : string.Empty);

    /// <summary>Resolves the semantic theme color for one go-code entry's row.</summary>
    internal static Color ResolveEntryColor(SandataThemeColors colors, GoCodeEntry entry) =>
        entry.IsReleased ? colors.StatusSuccess : colors.TextPrimary;
}
