using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Sandata.Client.UI;

/// <summary>
/// One ordered HUD event line — a plain, presentation-only record, not tied
/// to any <c>Sandata.Core</c> event type (none exists yet). <see cref="Tick"/>
/// is the authoritative simulation tick the event happened on, never a wall
/// clock reading.
/// </summary>
internal readonly record struct HudEvent(long Tick, string Text);

/// <summary>
/// Right-column spectator event log, design section 11's HUD element list
/// and CLAUDE.md section 5's "the battle event feed retains at most 200
/// ordered events" rule, restated here for Sandata's own client. Anchored
/// below <see cref="ContactList"/>, per design section 11's "right column,
/// below contacts".
/// </summary>
internal sealed class SandataEventLog
{
    /// <summary>The most events this log keeps; the oldest are discarded first.</summary>
    internal const int MaxRetainedEvents = 200;

    private readonly List<HudEvent> _events = [];

    /// <summary>
    /// The retained events, oldest first. Never more than
    /// <see cref="MaxRetainedEvents"/> entries.
    /// </summary>
    internal IReadOnlyList<HudEvent> Events => _events;

    /// <summary>
    /// Appends <paramref name="hudEvent"/>, then discards the oldest entries
    /// beyond <see cref="MaxRetainedEvents"/> if the log has grown past it.
    /// </summary>
    internal void Add(HudEvent hudEvent)
    {
        _events.Add(hudEvent);

        var overflow = _events.Count - MaxRetainedEvents;
        if (overflow > 0)
        {
            _events.RemoveRange(0, overflow);
        }
    }

    /// <summary>Distance below <paramref name="contactListBounds"/> before the log starts.</summary>
    internal const int Gap = 8;

    /// <summary>Distance from the bottom of the window to the log's own bottom edge.</summary>
    internal const int BottomMargin = 12;

    /// <summary>
    /// The log's bounding rectangle: same left edge and width as
    /// <paramref name="contactListBounds"/>, starting <see cref="Gap"/> below
    /// it and extending down to <see cref="BottomMargin"/> above the bottom
    /// of <paramref name="windowBounds"/>.
    /// </summary>
    internal static Rectangle CalculateBounds(Rectangle windowBounds, Rectangle contactListBounds)
    {
        var top = contactListBounds.Bottom + Gap;
        var height = Math.Max(0, windowBounds.Bottom - top - BottomMargin);

        return new Rectangle(
            contactListBounds.Left,
            top,
            contactListBounds.Width,
            height);
    }
}
