using System.Collections.Immutable;
using Sandata.Core.Maps;

namespace Sandata.Core.Navigation;

/// <summary>
/// Sandata Stage 0's derived room decomposition of a baked map — design
/// decision 1 of <c>docs/plans/2026-08-14-sandata-clear-the-map-design.md</c>.
/// A 4-connected flood fill (fixed N, E, S, W neighbour order; ascending
/// row-major <c>nodeIndex</c> seed order; a fixed-size <c>int[]</c> FIFO
/// scratch queue, never <c>Queue&lt;T&gt;</c>) runs over a room-boundary array
/// built from every <see cref="WallRecord"/> <b>and every</b>
/// <see cref="DoorRecord"/>, regardless of that door's open or closed
/// <see cref="DoorRecord.State"/>. This is why <see cref="Bake"/> cannot
/// reuse <see cref="NavBake.Bake"/> directly — that method deliberately skips
/// an open door, because it bakes movement passability, not room identity,
/// and an open doorway still separates two rooms even though a mover can
/// walk straight through it. <see cref="Bake"/> instead reuses
/// <see cref="NavBake.SupercoverCells"/>, the same pinned, division-free
/// integer walk, directly.
/// </summary>
/// <remarks>
/// A room's id is the lowest flat <c>nodeIndex</c> among its member cells,
/// which — because the flood fill scans seeds in strictly ascending
/// <c>nodeIndex</c> order and only ever starts a new fill from a cell no
/// earlier fill has claimed — is always the seed cell that started that
/// room's own fill: no lower-indexed cell in the same connected component
/// could still be unvisited when a room's fill begins. This type is a pure
/// function of <paramref name="grid"/>'s dimensions and the wall/door
/// geometry it is baked from — never authored, never snapshotted, and
/// rebuilt identically on every bake, per design section 4's
/// derived-versus-hashed split and <c>CLAUDE.md</c> section 9's "Save
/// derived caches, render data, or metrics into a snapshot" do-not. The
/// authoritative, hashed counterpart this type feeds is
/// <c>Sandata.Core.Simulation.RoomClearState</c>, one entry per
/// <see cref="RoomIds"/> value.
/// </remarks>
public sealed class RoomLayout
{
    private readonly int[] _roomIdByCell;
    private readonly ImmutableArray<int> _roomIds;

    private RoomLayout(int[] roomIdByCell, ImmutableArray<int> roomIds)
    {
        _roomIdByCell = roomIdByCell;
        _roomIds = roomIds;
    }

    /// <summary>Every room's id, strictly ascending. See this type's remarks for what a room id is.</summary>
    public ImmutableArray<int> RoomIds => _roomIds;

    /// <summary>
    /// The room id occupying <paramref name="cellIndex"/>, or <c>-1</c> when
    /// that cell is boundary geometry (a wall or a door, in any state) rather
    /// than open floor belonging to a room.
    /// </summary>
    public int RoomIdAt(int cellIndex) => _roomIdByCell[cellIndex];

    /// <summary>
    /// Locates <paramref name="roomId"/> within <see cref="RoomIds"/> by
    /// binary search over the already-ascending array, or returns a negative
    /// value when it is absent — the flat-array equivalent of a keyed lookup,
    /// with no <c>Dictionary&lt;&gt;</c> and no <c>HashSet&lt;&gt;</c>.
    /// </summary>
    public int IndexOfRoom(int roomId) => _roomIds.AsSpan().BinarySearch(roomId);

    /// <summary>
    /// Bakes a room decomposition of <paramref name="grid"/> from
    /// <paramref name="walls"/> and <paramref name="doors"/>. Every door
    /// counts as boundary regardless of its <see cref="DoorRecord.State"/> —
    /// see this type's own summary for why that differs from
    /// <see cref="NavBake.Bake"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="grid"/>, <paramref name="walls"/>, or
    /// <paramref name="doors"/> is <see langword="null"/>.
    /// </exception>
    public static RoomLayout Bake(NavGrid grid, IReadOnlyList<WallRecord> walls, IReadOnlyList<DoorRecord> doors)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(walls);
        ArgumentNullException.ThrowIfNull(doors);

        var cellCount = grid.CellCount;
        var boundary = new bool[cellCount];

        foreach (var wall in walls)
        {
            RasterizeBoundary(boundary, grid, wall.X1, wall.Y1, wall.X2, wall.Y2);
        }

        foreach (var door in doors)
        {
            // Every door is boundary geometry, open or closed: a room's
            // identity does not change when a door swings open. This is the
            // one deliberate divergence from NavBake.Bake, which skips an
            // open door because it bakes movement passability, not rooms.
            RasterizeBoundary(boundary, grid, door.X1, door.Y1, door.X2, door.Y2);
        }

        var roomIdByCell = new int[cellCount];
        Array.Fill(roomIdByCell, -1);

        // Fixed-size FIFO scratch queue, not Queue<T> — CLAUDE.md section 5's
        // banned-token list bars neither by name, but a flat array indexed by
        // head/tail matches every other flood-fill-shaped structure already
        // in this project and needs no growth policy to reason about.
        var queue = new int[cellCount];
        var roomIdsBuilder = ImmutableArray.CreateBuilder<int>();

        for (var seed = 0; seed < cellCount; seed++)
        {
            if (boundary[seed] || roomIdByCell[seed] != -1)
            {
                continue;
            }

            // seed is, by construction, the lowest unclaimed non-boundary
            // cell index reached so far in this ascending scan, so it is the
            // lowest cell its own about-to-be-discovered component can ever
            // contain: that is this room's id.
            var roomId = seed;
            var head = 0;
            var tail = 0;
            queue[tail] = seed;
            tail++;
            roomIdByCell[seed] = roomId;

            while (head < tail)
            {
                var current = queue[head];
                head++;

                var x = grid.CellX(current);
                var y = grid.CellY(current);

                // Fixed N, E, S, W order — never affects the resulting room
                // membership (flood fill reaches the same set regardless of
                // neighbour visit order), but keeps queue contents themselves
                // reproducible for anyone stepping through a debug trace.
                TryEnqueue(grid, boundary, roomIdByCell, queue, ref tail, x, y - 1, roomId);
                TryEnqueue(grid, boundary, roomIdByCell, queue, ref tail, x + 1, y, roomId);
                TryEnqueue(grid, boundary, roomIdByCell, queue, ref tail, x, y + 1, roomId);
                TryEnqueue(grid, boundary, roomIdByCell, queue, ref tail, x - 1, y, roomId);
            }

            roomIdsBuilder.Add(roomId);
        }

        return new RoomLayout(roomIdByCell, roomIdsBuilder.ToImmutable());
    }

    private static void TryEnqueue(
        NavGrid grid,
        bool[] boundary,
        int[] roomIdByCell,
        int[] queue,
        ref int tail,
        int x,
        int y,
        int roomId)
    {
        if (!grid.TryGetCellIndex(x, y, out var index))
        {
            return;
        }

        if (boundary[index] || roomIdByCell[index] != -1)
        {
            return;
        }

        roomIdByCell[index] = roomId;
        queue[tail] = index;
        tail++;
    }

    private static void RasterizeBoundary(bool[] boundary, NavGrid grid, int x1, int y1, int x2, int y2)
    {
        foreach (var (x, y) in NavBake.SupercoverCells(x1, y1, x2, y2))
        {
            if (grid.TryGetCellIndex(x, y, out var index))
            {
                boundary[index] = true;
            }
        }
    }
}
