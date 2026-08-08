using System;
using Microsoft.Xna.Framework;

namespace Sandata.Client.UI;

/// <summary>
/// Right-column contact list, design section 11's HUD element list: every
/// remembered enemy, its tier, and its last-known-cell age in ticks. Pure
/// layout and content helpers only. Content is built from primitives rather
/// than <c>Sandata.Core.Simulation.ContactMemoryEntry</c> directly, since that
/// record's <c>ContactTier</c> field is itself a raw backing value for the
/// not-yet-declared <c>ContactTier</c> enum task 35 owns — this file follows
/// the same raw-<see langword="int"/> convention rather than inventing a
/// second, competing enum.
/// </summary>
internal static class ContactList
{
    /// <summary>Distance from the window edge to the list.</summary>
    internal const int Margin = 12;

    /// <summary>The list's fixed width, before any window clamp.</summary>
    internal const int Width = 280;

    /// <summary>Height reserved for the list's header row.</summary>
    internal const int HeaderHeight = 24;

    /// <summary>One contact row's fixed height.</summary>
    internal const int RowHeight = 22;

    /// <summary>The most contact rows shown before the list stops growing.</summary>
    internal const int MaxVisibleRows = 10;

    /// <summary>
    /// The default vertical offset from the top of the window, leaving room
    /// for a mission clock or other top-anchored element above the list. A
    /// caller composing the full HUD may pass its own offset instead.
    /// </summary>
    internal const int DefaultTopOffset = 64;

    /// <summary>
    /// The list's bounding rectangle, anchored to the right edge of
    /// <paramref name="windowBounds"/> starting at <paramref name="topOffset"/>.
    /// Grows with <paramref name="contactCount"/> up to
    /// <see cref="MaxVisibleRows"/>, then clamps to whatever vertical space
    /// remains above the bottom margin.
    /// </summary>
    internal static Rectangle CalculateBounds(
        Rectangle windowBounds,
        int contactCount,
        int topOffset = DefaultTopOffset)
    {
        var visibleRows = Math.Clamp(contactCount, 0, MaxVisibleRows);
        var preferredHeight = HeaderHeight + (visibleRows * RowHeight);
        var maxHeight = Math.Max(0, windowBounds.Bottom - topOffset - Margin);
        var height = Math.Min(preferredHeight, maxHeight);
        var width = Math.Min(Width, Math.Max(0, windowBounds.Width - (Margin * 2)));

        return new Rectangle(
            windowBounds.Right - Margin - width,
            topOffset,
            width,
            height);
    }

    /// <summary>
    /// The rectangle for contact row <paramref name="rowIndex"/> (zero-based)
    /// within a list already positioned at <paramref name="listBounds"/>.
    /// </summary>
    internal static Rectangle CalculateRowBounds(Rectangle listBounds, int rowIndex) =>
        new(
            listBounds.Left,
            listBounds.Top + HeaderHeight + (rowIndex * RowHeight),
            listBounds.Width,
            RowHeight);

    /// <summary>
    /// One contact row's plain content: the raw contact-tier value (by design
    /// section 4's own listed order, <c>0</c> Unknown, <c>1</c> a
    /// question-mark sighting, <c>2</c> Identified — pending task 35's
    /// authoritative enum) and the age, in ticks, of the last-known cell.
    /// </summary>
    internal readonly record struct ContactContent(int Tier, long LastSeenAgeTicks);

    /// <summary>Formats the tier label for <paramref name="content"/>.</summary>
    internal static string FormatTierLabel(ContactContent content) => content.Tier switch
    {
        0 => "Unknown",
        1 => "?",
        2 => "Identified",
        _ => "Unknown",
    };

    /// <summary>Formats the last-known-cell age line, for example "42t".</summary>
    internal static string FormatAgeLine(ContactContent content) =>
        $"{content.LastSeenAgeTicks}t";
}
