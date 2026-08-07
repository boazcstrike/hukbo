using System;
using Microsoft.Xna.Framework;

namespace Sandata.Client.UI;

/// <summary>
/// Top-right mission clock and tick counter, design section 11's HUD element
/// list. Pure layout and formatting helpers only — no <c>SpriteBatch</c>, no
/// font, and no wall clock: the only time value this file ever sees is the
/// authoritative simulation tick the caller already holds.
/// </summary>
internal static class MissionClock
{
    /// <summary>Distance from the window edge to the clock panel.</summary>
    internal const int Margin = 12;

    /// <summary>The clock panel's fixed width, before any window clamp.</summary>
    internal const int Width = 160;

    /// <summary>The clock panel's fixed height, before any window clamp.</summary>
    internal const int Height = 40;

    /// <summary>
    /// The clock panel's bounding rectangle, anchored to the top-right corner
    /// of <paramref name="windowBounds"/>. Clamped so a window narrower or
    /// shorter than the panel's preferred size never produces a rectangle
    /// that extends past the window.
    /// </summary>
    internal static Rectangle CalculateBounds(Rectangle windowBounds)
    {
        var width = Math.Min(Width, Math.Max(0, windowBounds.Width - (Margin * 2)));
        var height = Math.Min(Height, Math.Max(0, windowBounds.Height - (Margin * 2)));

        return new Rectangle(
            windowBounds.Right - Margin - width,
            windowBounds.Top + Margin,
            width,
            height);
    }

    /// <summary>Formats the raw tick counter, for example "Tick 4200".</summary>
    internal static string FormatTickLine(long tick) => $"Tick {tick}";

    /// <summary>
    /// Formats the tick count as a derived mm:ss clock, using
    /// <paramref name="tickRate"/> (ticks per second) to convert. The
    /// simulation's authoritative time stays the integer tick; this string is
    /// a read-only presentation of it, never a value fed back into the
    /// simulation.
    /// </summary>
    internal static string FormatDerivedClockLine(long tick, int tickRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tickRate, 0);

        var wholeSeconds = tick / tickRate;
        var minutes = wholeSeconds / 60;
        var seconds = wholeSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}
