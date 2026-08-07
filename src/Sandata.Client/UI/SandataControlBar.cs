using System;
using Microsoft.Xna.Framework;

namespace Sandata.Client.UI;

/// <summary>
/// Bottom-centre spectator control bar, design section 11's HUD element list
/// — "reusing Hukbo's control-bar shape": a fixed-width row of equal-sized
/// buttons, centred and anchored above the bottom margin, exactly as
/// <c>Hukbo.Client.UI.ControlBar</c> lays itself out. Pure layout helpers
/// only; this file does not reference <c>Hukbo.Client</c>, since Sandata's
/// client is its own project (design section 3) and only the shape, not the
/// code, is shared.
/// </summary>
internal static class SandataControlBar
{
    /// <summary>Distance from the window edge to the bar.</summary>
    internal const int Margin = 12;

    /// <summary>One button's fixed width.</summary>
    internal const int ButtonWidth = 96;

    /// <summary>One button's fixed height.</summary>
    internal const int ButtonHeight = 36;

    /// <summary>Horizontal gap between adjacent buttons.</summary>
    internal const int ButtonGap = 8;

    /// <summary>
    /// The four spectator controls the bar exposes, in left-to-right order.
    /// </summary>
    internal enum Button
    {
        Pause,
        StepOneTick,
        Speed,
        Restart,
    }

    private static readonly Button[] ButtonOrder =
    [
        Button.Pause,
        Button.StepOneTick,
        Button.Speed,
        Button.Restart,
    ];

    /// <summary>
    /// The bar's own bounding rectangle, anchored to the bottom-centre of
    /// <paramref name="windowBounds"/>, clamped so a narrow window shrinks
    /// the bar rather than overflowing it.
    /// </summary>
    internal static Rectangle CalculateBounds(Rectangle windowBounds)
    {
        var preferredWidth = (ButtonWidth * ButtonOrder.Length) +
            (ButtonGap * (ButtonOrder.Length - 1));
        var width = Math.Min(preferredWidth, Math.Max(0, windowBounds.Width - (Margin * 2)));

        return new Rectangle(
            windowBounds.Center.X - (width / 2),
            windowBounds.Bottom - Margin - ButtonHeight,
            width,
            ButtonHeight);
    }

    /// <summary>
    /// The rectangle for <paramref name="button"/> within a bar already
    /// positioned at <paramref name="barBounds"/>. Buttons past the bar's
    /// clamped width are not clipped individually — a caller that clamped the
    /// bar due to a narrow window is expected to hide or scale buttons
    /// instead, not covered by this v0.1 layout.
    /// </summary>
    internal static Rectangle CalculateButtonBounds(Rectangle barBounds, Button button)
    {
        var index = Array.IndexOf(ButtonOrder, button);
        return new Rectangle(
            barBounds.Left + (index * (ButtonWidth + ButtonGap)),
            barBounds.Top,
            ButtonWidth,
            ButtonHeight);
    }
}
