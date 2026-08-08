using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Sandata.Client.UI;

/// <summary>
/// Continuous drag-capture pointer state, design section 11's "pointer
/// priority": "Marquee drag capture is a new state in that chain and sits
/// above the in-world layer and below every panel, so a drag starting on a
/// panel never becomes a marquee." <see cref="Begin"/> is the enforcement
/// point — a drag whose start position falls inside any open panel's bounds
/// is refused, exactly as if a higher-priority panel had already consumed the
/// pointer-down event before this layer ever saw it.
/// </summary>
/// <remarks>
/// A pure record, updated by replacement rather than in place: the caller
/// (task 69's wiring) holds one instance across frames and reassigns it from
/// <see cref="Begin"/>, <see cref="WithCurrentPosition"/>, and <see cref="End"/>,
/// the same continuous-state shape <c>Rendering.SandataCamera</c> exposes
/// through <c>WorldToScreen</c>/<c>ScreenToWorld</c> for its own per-frame
/// state.
/// </remarks>
internal readonly record struct DragCapture(bool IsActive, Point StartPosition, Point CurrentPosition)
{
    /// <summary>No drag in progress.</summary>
    internal static readonly DragCapture Inactive = default;

    /// <summary>
    /// Starts a drag at <paramref name="startPosition"/>, unless that position
    /// falls inside one of <paramref name="panelBounds"/> — in which case the
    /// pointer-down is consumed by the higher-priority panel layer and this
    /// method returns <see cref="Inactive"/> rather than an active drag, so no
    /// marquee is ever produced from it.
    /// </summary>
    internal static DragCapture Begin(Point startPosition, IReadOnlyList<Rectangle> panelBounds)
    {
        foreach (var bounds in panelBounds)
        {
            if (bounds.Contains(startPosition))
            {
                return Inactive;
            }
        }

        return new DragCapture(true, startPosition, startPosition);
    }

    /// <summary>
    /// One frame's pointer movement while the drag is held. A no-op on an
    /// inactive capture — there is nothing to extend.
    /// </summary>
    internal DragCapture WithCurrentPosition(Point currentPosition) =>
        IsActive ? this with { CurrentPosition = currentPosition } : this;

    /// <summary>Releases the drag, returning to <see cref="Inactive"/>.</summary>
    internal DragCapture End() => Inactive;

    /// <summary>
    /// The in-world marquee rectangle this drag currently describes, or
    /// <see langword="null"/> when the capture is inactive — either because it
    /// never started (refused by <see cref="Begin"/>) or because it has
    /// already ended.
    /// </summary>
    internal Rectangle? MarqueeBounds => IsActive
        ? RectangleFromCorners(StartPosition, CurrentPosition)
        : null;

    /// <summary>
    /// The axis-aligned rectangle spanning two arbitrary corner points, in
    /// either drag direction.
    /// </summary>
    private static Rectangle RectangleFromCorners(Point a, Point b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        return new Rectangle(left, top, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }
}
