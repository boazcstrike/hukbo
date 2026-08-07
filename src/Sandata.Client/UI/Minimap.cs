using System;
using Microsoft.Xna.Framework;
using Sandata.Client.Theming;
using Sandata.Core.Navigation;

namespace Sandata.Client.UI;

/// <summary>
/// Top-left minimap panel, design section 11's HUD element list: "a bordered
/// panel drawing the nav grid's passability at one pixel per cell, no
/// interaction." Task 46 scaffolds the layout and color-resolution helpers
/// only — nothing here reads pointer input or produces a click target, matching
/// the "no interaction" clause verbatim.
/// </summary>
/// <remarks>
/// Pure-helper shape only, matching <c>RosterStrip</c> and
/// <c>ContactList</c>: bounds and per-cell rectangles are computed from plain
/// values, never from a <c>SpriteBatch</c> or a <c>GraphicsDevice</c>. This
/// file reads <see cref="NavGrid"/>'s public <c>Width</c>/<c>Height</c>/
/// <c>IsInBounds</c> surface only, never its internal bake state.
/// </remarks>
internal static class Minimap
{
    /// <summary>Distance from the window edge to the panel.</summary>
    internal const int Margin = 12;

    /// <summary>The panel's border thickness, in pixels, on every side.</summary>
    internal const int BorderThickness = 1;

    /// <summary>
    /// Screen pixels drawn per nav-grid cell. Design section 11 pins this at
    /// one pixel per cell: the minimap does not scale with zoom or window
    /// size, so a grid larger than the available window space simply runs off
    /// it rather than being scaled down.
    /// </summary>
    internal const int PixelsPerCell = 1;

    /// <summary>
    /// The panel's own bounding rectangle, anchored to the top-left corner of
    /// <paramref name="windowBounds"/>. Sized to <paramref name="grid"/>'s
    /// full width and height in cells, plus the border on every side.
    /// </summary>
    internal static Rectangle CalculateBounds(Rectangle windowBounds, NavGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var contentWidth = grid.Width * PixelsPerCell;
        var contentHeight = grid.Height * PixelsPerCell;

        return new Rectangle(
            windowBounds.Left + Margin,
            windowBounds.Top + Margin,
            contentWidth + (BorderThickness * 2),
            contentHeight + (BorderThickness * 2));
    }

    /// <summary>
    /// The one-pixel rectangle for cell (<paramref name="cellX"/>,
    /// <paramref name="cellY"/>), positioned inside a panel already at
    /// <paramref name="panelBounds"/>, just past the border. Deterministic,
    /// zoom-free arithmetic: the minimap is its own fixed projection, never
    /// <see cref="Rendering.SandataCamera"/>'s world-to-screen conversion.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The cell coordinate is outside <paramref name="grid"/>.
    /// </exception>
    internal static Rectangle CalculateCellPixelBounds(Rectangle panelBounds, NavGrid grid, int cellX, int cellY)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (!grid.IsInBounds(cellX, cellY))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cellX),
                (cellX, cellY),
                $"Cell ({cellX}, {cellY}) is outside the {grid.Width} by {grid.Height} grid.");
        }

        return new Rectangle(
            panelBounds.Left + BorderThickness + (cellX * PixelsPerCell),
            panelBounds.Top + BorderThickness + (cellY * PixelsPerCell),
            PixelsPerCell,
            PixelsPerCell);
    }

    /// <summary>
    /// The theme color a cell's <see cref="NavCellFlags"/> draws as.
    /// <see cref="NavCellFlags.Open"/> reuses
    /// <see cref="SandataThemeColors.ArenaSurface"/>,
    /// <see cref="NavCellFlags.Door"/> reuses
    /// <see cref="SandataThemeColors.StatusWarning"/> (a closed door needs
    /// attention: planner-passable, mover-blocked), and everything else —
    /// which today is only <see cref="NavCellFlags.Blocked"/> — reuses
    /// <see cref="SandataThemeColors.ArenaBorder"/>, the same role a wall's
    /// in-world silhouette already draws with.
    /// </summary>
    internal static Color ResolveCellColor(SandataThemeColors colors, NavCellFlags flags) => flags switch
    {
        NavCellFlags.Open => colors.ArenaSurface,
        NavCellFlags.Door => colors.StatusWarning,
        _ => colors.ArenaBorder,
    };
}
