namespace Sandata.Core.Navigation;

/// <summary>
/// A uniform integer grid over map space: one cell per <see cref="CellSizeWu"/>
/// world units on a side, holding no per-node arrays of its own beyond its
/// dimensions. Every per-node array a later task adds — passability, the
/// clearance field, the A* scratch arrays — is sized <c>Width * Height</c> and
/// indexed by the same flat <c>nodeIndex</c> this type computes, per design
/// section 7 ("Data structures: flat arrays, no dictionaries") of
/// docs/plans/2026-08-07-sandata-scaffold-design.md.
///
/// <para>
/// <c>nodeIndex = (y * Width) + x</c>. No <c>Dictionary</c>, no
/// <c>HashSet</c>, and no <c>PriorityQueue</c> reaches this type or anything
/// built on it — enumeration order for either of the first two changes with
/// capacity growth, and the third makes no stability promise, so neither can
/// be allowed near a determinism-sensitive index.
/// </para>
/// </summary>
public sealed class NavGrid
{
    /// <summary>
    /// World units per grid cell. A power of two, so <see cref="WorldToCellCoordinate"/>
    /// converts by an arithmetic right shift rather than a division — see
    /// design section 7, "The cell size is a power of two, so the
    /// world-to-cell conversion is a shift rather than a division."
    /// </summary>
    public const int CellSizeWu = 4;

    /// <summary>
    /// <c>log2(CellSizeWu)</c>. Kept as its own named constant, rather than
    /// computed from <see cref="CellSizeWu"/> at every call site, so a future
    /// change to the cell size is caught by <see cref="CellSizeWu"/> no
    /// longer matching <c>1 &lt;&lt; CellSizeShiftAmount</c> instead of
    /// silently reintroducing a division.
    /// </summary>
    private const int CellSizeShiftAmount = 2; // 1 << 2 == 4 == CellSizeWu

    /// <summary>
    /// The largest grid dimension, in cells, this type accepts on either
    /// axis. Design section 7: "the maximum supported map, 2048 by 2048 wu,
    /// is 512 by 512 cells, or 262,144 nodes — four flat <c>int</c> arrays of
    /// that length is 4 MB, allocated once at load."
    /// </summary>
    public const int MaxDimensionCells = 512;

    /// <summary>
    /// The number of cells along the X axis.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// The number of cells along the Y axis.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// <c>Width * Height</c> — the length every per-node array sized against
    /// this grid must have.
    /// </summary>
    public int CellCount { get; }

    /// <summary>
    /// Creates a grid of <paramref name="width"/> by <paramref name="height"/>
    /// cells. Both dimensions must be strictly positive and no larger than
    /// <see cref="MaxDimensionCells"/>.
    /// </summary>
    public NavGrid(int width, int height)
    {
        if (width <= 0 || width > MaxDimensionCells)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                $"Grid width must be between 1 and {MaxDimensionCells} cells.");
        }

        if (height <= 0 || height > MaxDimensionCells)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                height,
                $"Grid height must be between 1 and {MaxDimensionCells} cells.");
        }

        Width = width;
        Height = height;
        CellCount = checked(width * height);
    }

    /// <summary>
    /// Whether cell coordinate <paramref name="x"/>, <paramref name="y"/>
    /// falls inside this grid.
    /// </summary>
    public bool IsInBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>
    /// The flat node index for cell coordinate <paramref name="x"/>,
    /// <paramref name="y"/>: <c>(y * Width) + x</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The coordinate is outside <c>[0, Width) x [0, Height)</c>.
    /// </exception>
    public int CellIndex(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                (x, y),
                $"Cell ({x}, {y}) is outside the {Width} by {Height} grid.");
        }

        return (y * Width) + x;
    }

    /// <summary>
    /// The non-throwing form of <see cref="CellIndex"/>: returns
    /// <see langword="false"/> and sets <paramref name="cellIndex"/> to
    /// <c>-1</c> when the coordinate is out of bounds, instead of throwing.
    /// </summary>
    public bool TryGetCellIndex(int x, int y, out int cellIndex)
    {
        if (!IsInBounds(x, y))
        {
            cellIndex = -1;
            return false;
        }

        cellIndex = (y * Width) + x;
        return true;
    }

    /// <summary>
    /// The X coordinate a flat <paramref name="cellIndex"/> decodes to:
    /// <c>cellIndex % Width</c>. The inverse of the X half of
    /// <see cref="CellIndex"/>.
    /// </summary>
    public int CellX(int cellIndex) => cellIndex % Width;

    /// <summary>
    /// The Y coordinate a flat <paramref name="cellIndex"/> decodes to:
    /// <c>cellIndex / Width</c>. The inverse of the Y half of
    /// <see cref="CellIndex"/>.
    /// </summary>
    public int CellY(int cellIndex) => cellIndex / Width;

    /// <summary>
    /// Converts a coordinate expressed in whole world units (the unit the
    /// <c>.hkmap</c> format's <c>WALL</c>, <c>DOOR</c>, <c>SPAWN</c>, and
    /// <c>OBJECTIVE</c> records use) to the cell coordinate that contains it,
    /// by an arithmetic right shift rather than a division.
    ///
    /// <para>
    /// C#'s <c>&gt;&gt;</c> on a signed integer is an arithmetic shift: it
    /// sign-extends, which makes it floor toward negative infinity rather
    /// than truncate toward zero the way <c>/</c> does. For a power-of-two
    /// divisor this is exactly <see cref="Mathematics.IntegerMath.FloorDiv"/>'s
    /// contract, reached without a division. Map-space coordinates are always
    /// non-negative (design section 12: "the format cannot express a negative
    /// number at all"), so truncating and flooring agree there regardless;
    /// this method floors unconditionally so the same conversion is already
    /// correct wherever a later task feeds it a signed relative offset
    /// instead of an absolute map coordinate.
    /// </para>
    /// </summary>
    public static int WorldToCellCoordinate(long worldUnits) => (int)(worldUnits >> CellSizeShiftAmount);
}
