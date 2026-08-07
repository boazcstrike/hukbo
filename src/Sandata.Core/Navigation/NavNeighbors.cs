namespace Sandata.Core.Navigation;

/// <summary>
/// The pinned eight-direction neighbour topology of one <see cref="NavGrid"/>
/// cell, in one fixed enumeration order: east, south-east, south, south-west,
/// west, north-west, north, north-east. Design section 7: "Neighbour
/// enumeration reads one pinned static offset table in one pinned order... "
///
/// <para>
/// <b>This order is part of the determinism contract.</b> Grid A* (task 21)
/// expands a node's neighbours in exactly this order, so any code that walks
/// them — the search itself, a tie-breaking rule, an inspector dump — sees
/// the same sequence on every machine and every run. Changing the order, or
/// the offsets, is a determinism-relevant change requiring new golden
/// expectations exactly as <c>CLAUDE.md</c> section 5 requires for an enum
/// reorder.
/// </para>
///
/// <para>
/// Map space has its origin at the top-left with Y increasing downward
/// (design section 12: "the map origin is (0, 0) at the top-left"), so
/// "south" is <c>+Y</c> and "north" is <c>-Y</c> here, matching that
/// convention.
/// </para>
/// </summary>
public static class NavNeighbors
{
    /// <summary>The number of directions in the pinned table.</summary>
    public const int DirectionCount = 8;

    /// <summary>Index of the east direction: offset (+1, 0).</summary>
    public const int East = 0;

    /// <summary>Index of the south-east direction: offset (+1, +1).</summary>
    public const int SouthEast = 1;

    /// <summary>Index of the south direction: offset (0, +1).</summary>
    public const int South = 2;

    /// <summary>Index of the south-west direction: offset (-1, +1).</summary>
    public const int SouthWest = 3;

    /// <summary>Index of the west direction: offset (-1, 0).</summary>
    public const int West = 4;

    /// <summary>Index of the north-west direction: offset (-1, -1).</summary>
    public const int NorthWest = 5;

    /// <summary>Index of the north direction: offset (0, -1).</summary>
    public const int North = 6;

    /// <summary>Index of the north-east direction: offset (+1, -1).</summary>
    public const int NorthEast = 7;

    /// <summary>
    /// The pinned offsets, indexed by the direction constants above, in the
    /// fixed order east, south-east, south, south-west, west, north-west,
    /// north, north-east. Kept private: callers read one direction's offset
    /// through <see cref="OffsetX"/> and <see cref="OffsetY"/> so the pinned
    /// table itself can never be mutated out from under a caller that holds
    /// a reference to it.
    /// </summary>
    private static readonly int[] DeltaX = [1, 1, 0, -1, -1, -1, 0, 1];

    /// <summary>
    /// The pinned offsets' Y component. See <see cref="DeltaX"/>.
    /// </summary>
    private static readonly int[] DeltaY = [0, 1, 1, 1, 0, -1, -1, -1];

    /// <summary>
    /// The X offset of <paramref name="direction"/> (one of the eight
    /// direction constants declared on this type).
    /// </summary>
    public static int OffsetX(int direction) => DeltaX[CheckedDirection(direction)];

    /// <summary>
    /// The Y offset of <paramref name="direction"/> (one of the eight
    /// direction constants declared on this type).
    /// </summary>
    public static int OffsetY(int direction) => DeltaY[CheckedDirection(direction)];

    /// <summary>
    /// Whether <paramref name="direction"/> is one of the four diagonal
    /// directions. In the pinned order every diagonal direction lands on an
    /// odd index and every orthogonal direction on an even one, so this is a
    /// parity check rather than a lookup.
    /// </summary>
    public static bool IsDiagonal(int direction) => (CheckedDirection(direction) & 1) != 0;

    /// <summary>
    /// The two orthogonal directions that flank <paramref name="diagonalDirection"/>
    /// — the pair a diagonal move's corner-cutting check must inspect. For
    /// example south-east's two orthogonal neighbours are east and south:
    /// stepping from a cell to its south-east neighbour cuts a corner exactly
    /// when either of those two is blocked.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="diagonalDirection"/> is not one of the four diagonal
    /// direction constants.
    /// </exception>
    public static (int First, int Second) OrthogonalComponentsOf(int diagonalDirection)
    {
        return diagonalDirection switch
        {
            SouthEast => (East, South),
            SouthWest => (South, West),
            NorthWest => (West, North),
            NorthEast => (North, East),
            _ => throw new ArgumentOutOfRangeException(
                nameof(diagonalDirection),
                diagonalDirection,
                "Only a diagonal direction (south-east, south-west, north-west, or north-east) has orthogonal components."),
        };
    }

    /// <summary>
    /// Whether a diagonal move is rejected by the no-corner-cutting rule:
    /// design section 7, "diagonal moves are rejected when either orthogonal
    /// neighbour is blocked, so a unit never cuts a wall corner." Takes the
    /// two orthogonal neighbours' blocked state directly rather than a grid
    /// and a passability array, so this rule stays testable in isolation from
    /// whatever concrete passability representation a later task chooses.
    /// </summary>
    public static bool IsDiagonalMoveBlocked(bool firstOrthogonalBlocked, bool secondOrthogonalBlocked) =>
        firstOrthogonalBlocked || secondOrthogonalBlocked;

    private static int CheckedDirection(int direction)
    {
        if (direction < 0 || direction >= DirectionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                $"Direction must be between 0 and {DirectionCount - 1}.");
        }

        return direction;
    }
}
