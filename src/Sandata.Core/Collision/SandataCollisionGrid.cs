using Sandata.Core.Mathematics;

namespace Sandata.Core.Collision;

/// <summary>
/// One agent's positional input to Sandata's collision broad phase. Coordinates
/// are raw fixed-point world units and may be negative — an indoor map's
/// origin is not guaranteed to sit at the map's own corner.
/// </summary>
internal readonly record struct SandataCollisionBody(
    ulong EntityId,
    int XRaw,
    int YRaw,
    bool IsAlive);

/// <summary>
/// Sandata's own deterministic uniform-grid broad phase over living operator
/// bodies. This is a derived accelerator, never authoritative state: it is not
/// hashed, not snapshotted, and not persisted. Its only contract is that it
/// produces exactly what an O(n^2) scan over the same bodies would produce, in
/// exactly one order.
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberate duplicate of the shape already proven in
/// <c>Hukbo.Core.Simulation</c> — propose a candidate pair against a bounded
/// neighbourhood, prioritise by a total order, normalise every pair to
/// (lower entity ID, higher entity ID) and sort the finished list — written
/// fresh here because the design's tier-2 extraction of the shared collision
/// module is deferred until Sandata's own collision needs are known. See
/// design section 3 of <c>docs/plans/2026-08-07-sandata-scaffold-design.md</c>.
/// </para>
/// <para>
/// The one structural difference from the Hukbo original is the cell index:
/// this grid never uses a keyed dictionary. Cells are found through a hand
/// written open-addressing table over flat arrays, keyed by a packed,
/// order-preserving cell coordinate, so a warm tick never allocates and the
/// determinism contract's ban on <c>Dictionary&lt;</c> and <c>HashSet&lt;</c>
/// inside <c>Sandata.Core</c> holds without exception.
/// </para>
/// <para>
/// Cell coordinates are computed with <see cref="IntegerMath.FloorDiv"/> rather
/// than the built-in truncating <c>/</c>, because a body standing at a negative
/// world coordinate must not be folded into cell zero: truncating division maps
/// both <c>-1</c> and <c>0</c> onto the same quotient, merging two distinct
/// cells at the one place every map's coordinate system is anchored.
/// </para>
/// <para>
/// Determinism rests on three properties. Occupied cells are visited in
/// ascending cell Y then ascending cell X, obtained by sorting packed cell
/// keys rather than by enumerating the hash table. Each cell's neighbourhood is
/// scanned in one fixed three-by-three offset order. Every emitted pair is
/// normalised to (lower entity ID, higher entity ID) and the finished list is
/// sorted with <see cref="SandataCollisionPair.CompareTo"/>, so the output
/// cannot depend on the order the caller happened to supply bodies in.
/// </para>
/// <para>
/// A pair is emitted only from the body with the lower entity ID, so scanning
/// the full three-by-three neighbourhood still yields each unordered pair
/// exactly once, without a de-duplication set.
/// </para>
/// <para>
/// The contact predicate, <see cref="IsContact"/>, is inclusive: exact
/// tangency is a candidate pair. The strict <see cref="Overlaps"/> test belongs
/// to <see cref="SandataCollisionResolver"/>, which decides that tangency is a
/// legal resting position.
/// </para>
/// <para>
/// All storage is reused between calls and grows only when capacity is
/// insufficient, so a warm tick allocates nothing.
/// </para>
/// </remarks>
internal sealed class SandataCollisionGrid
{
    /// <summary>Terminator for the per-cell body chains, and the "not found" answer from a hash lookup.</summary>
    private const int NoSlot = -1;

    private const int InitialBodyCapacity = 64;

    /// <summary>Must stay a power of two so the hash mask can replace a modulo.</summary>
    private const int InitialHashCapacity = 64;

    /// <summary>
    /// The three-by-three neighbourhood in one fixed order: ascending offset Y,
    /// then ascending offset X, including the cell itself. A cell size of at
    /// least one body diameter, enforced by <see cref="ValidateBodyRadius"/>, is
    /// what makes this neighbourhood sufficient: bodies two cells apart on an
    /// axis are separated by strictly more than one cell size, hence by
    /// strictly more than one diameter.
    /// </summary>
    private static readonly (int X, int Y)[] NeighbourOffsets =
    [
        (-1, -1),
        (0, -1),
        (1, -1),
        (-1, 0),
        (0, 0),
        (1, 0),
        (-1, 1),
        (0, 1),
        (1, 1),
    ];

    /// <summary>
    /// The co-location repair directions, in the fixed order east, west, north,
    /// south. Shared with <see cref="SandataCollisionResolver"/> so both types
    /// separate coincident bodies the same deterministic way.
    /// </summary>
    internal static readonly (int X, int Y)[] SeparationDirections =
    [
        (1, 0),
        (-1, 0),
        (0, 1),
        (0, -1),
    ];

    // Open-addressing hash table over packed, order-preserving cell keys. No
    // keyed dictionary and no hash set anywhere in this type: every one of
    // these is a plain array, resolved by linear probing.
    private ulong[] _hashKeys = new ulong[InitialHashCapacity];
    private int[] _hashHeadSlot = new int[InitialHashCapacity];
    private bool[] _hashOccupied = new bool[InitialHashCapacity];
    private int _hashMask = InitialHashCapacity - 1;
    private int _hashCount;

    private readonly List<SandataCollisionPair> _pairs = [];

    /// <summary>Bodies in insertion order. Index doubles as the chain slot.</summary>
    private SandataCollisionBody[] _bodies = new SandataCollisionBody[InitialBodyCapacity];

    /// <summary>Next slot in the same cell, or <see cref="NoSlot"/>.</summary>
    private int[] _nextSlotInCell = new int[InitialBodyCapacity];

    /// <summary>Packed key of each occupied cell, in first-touch order.</summary>
    private ulong[] _occupiedCellKeys = new ulong[InitialBodyCapacity];

    /// <summary>Parallel to <see cref="_occupiedCellKeys"/>: the matching hash-table index, so <see cref="Clear"/> can mark it free without a lookup.</summary>
    private int[] _occupiedCellHashIndex = new int[InitialBodyCapacity];

    /// <summary>Scratch copy of <see cref="_occupiedCellKeys"/>, sorted for traversal.</summary>
    private ulong[] _traversalOrder = new ulong[InitialBodyCapacity];

    private int _bodyCount;

    private int _occupiedCellCount;

    /// <param name="cellSizeRaw">The edge length of one square cell in raw fixed-point units.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cellSizeRaw"/> is zero or negative.</exception>
    internal SandataCollisionGrid(int cellSizeRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellSizeRaw);

        CellSizeRaw = cellSizeRaw;
    }

    /// <summary>The edge length of one square cell in raw fixed-point units.</summary>
    internal int CellSizeRaw { get; }

    /// <summary>
    /// Every unordered pair of living bodies in contact after the last
    /// <see cref="Rebuild"/>, sorted ascending by
    /// <see cref="SandataCollisionPair.CompareTo"/> with each pair present
    /// exactly once.
    /// </summary>
    /// <remarks>
    /// The list is owned by the grid and is overwritten by the next
    /// <see cref="Rebuild"/> or <see cref="Clear"/>. Callers read it within the
    /// tick that produced it and never retain it.
    /// </remarks>
    internal IReadOnlyList<SandataCollisionPair> Pairs => _pairs;

    /// <summary>
    /// Rejects a radius this index cannot serve, once, on behalf of a caller
    /// that will then use the unchecked queries. <see cref="SandataCollisionResolver"/>
    /// holds a radius fixed for its whole lifetime, so it validates at
    /// construction rather than per query.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bodyRadiusRaw"/> is negative or so large that a body
    /// diameter exceeds <see cref="CellSizeRaw"/>.
    /// </exception>
    internal void ValidateRadiusForQueries(int bodyRadiusRaw) => ValidateBodyRadius(bodyRadiusRaw);

    /// <summary>
    /// Runs the whole broad phase: discards the previous contents, indexes every
    /// living body, and produces <see cref="Pairs"/>.
    /// </summary>
    /// <param name="bodies">The bodies to index. Dead bodies are skipped rather than rejected.</param>
    /// <param name="bodyRadiusRaw">The common body radius in raw units.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bodies"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bodyRadiusRaw"/> is negative or so large that a body
    /// diameter exceeds <see cref="CellSizeRaw"/>.
    /// </exception>
    internal void Rebuild(IReadOnlyList<SandataCollisionBody> bodies, int bodyRadiusRaw)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ValidateBodyRadius(bodyRadiusRaw);

        Clear();

        for (var index = 0; index < bodies.Count; index++)
        {
            Insert(bodies[index]);
        }

        GeneratePairs(bodyRadiusRaw);
    }

    /// <summary>
    /// Empties the grid and discards <see cref="Pairs"/>, keeping every
    /// allocated buffer for reuse.
    /// </summary>
    internal void Clear()
    {
        for (var index = 0; index < _occupiedCellCount; index++)
        {
            _hashOccupied[_occupiedCellHashIndex[index]] = false;
        }

        _occupiedCellCount = 0;
        _hashCount = 0;
        _pairs.Clear();
        _bodyCount = 0;
    }

    /// <summary>
    /// Indexes one body so that later queries can see it. This is the
    /// incremental path <see cref="SandataCollisionResolver"/> uses as it
    /// commits operators one at a time; it does not update <see cref="Pairs"/>.
    /// </summary>
    /// <remarks>
    /// A dead body is ignored, because a corpse never collides.
    /// </remarks>
    internal void Insert(in SandataCollisionBody body)
    {
        if (!body.IsAlive)
        {
            return;
        }

        var key = PackCellKey(CellCoordinate(body.XRaw), CellCoordinate(body.YRaw));
        var slot = _bodyCount;

        EnsureBodyCapacity(slot + 1);
        _bodies[slot] = body;

        var hashIndex = FindOrCreateHashSlot(key, out var created);

        if (created)
        {
            _hashHeadSlot[hashIndex] = NoSlot;

            EnsureOccupiedCapacity(_occupiedCellCount + 1);
            _occupiedCellKeys[_occupiedCellCount] = key;
            _occupiedCellHashIndex[_occupiedCellCount] = hashIndex;
            _occupiedCellCount++;
        }

        _nextSlotInCell[slot] = _hashHeadSlot[hashIndex];
        _hashHeadSlot[hashIndex] = slot;

        _bodyCount++;
    }

    /// <summary>
    /// True when a body of <paramref name="bodyRadiusRaw"/> centred at
    /// (<paramref name="xRaw"/>, <paramref name="yRaw"/>) would strictly
    /// penetrate any indexed body other than <paramref name="excludeEntityId"/>.
    /// </summary>
    /// <remarks>
    /// The predicate is the strict <see cref="Overlaps"/>, so exact tangency
    /// answers <see langword="false"/>. Tangency is a legal resting position.
    /// </remarks>
    internal bool AnyOverlapUnchecked(
        int xRaw,
        int yRaw,
        int bodyRadiusRaw,
        ulong excludeEntityId)
    {
        var cellX = CellCoordinate(xRaw);
        var cellY = CellCoordinate(yRaw);

        foreach (var offset in NeighbourOffsets)
        {
            var slot = FirstSlotInCell(cellX + offset.X, cellY + offset.Y);

            while (slot != NoSlot)
            {
                var other = _bodies[slot];

                if (other.EntityId != excludeEntityId &&
                    Overlaps(xRaw, yRaw, other.XRaw, other.YRaw, bodyRadiusRaw))
                {
                    return true;
                }

                slot = _nextSlotInCell[slot];
            }
        }

        return false;
    }

    /// <summary>
    /// True when an indexed body other than <paramref name="excludeEntityId"/>
    /// sits at exactly (<paramref name="xRaw"/>, <paramref name="yRaw"/>).
    /// </summary>
    internal bool AnyCoincidentUnchecked(int xRaw, int yRaw, ulong excludeEntityId)
    {
        var cellX = CellCoordinate(xRaw);
        var cellY = CellCoordinate(yRaw);

        foreach (var offset in NeighbourOffsets)
        {
            var slot = FirstSlotInCell(cellX + offset.X, cellY + offset.Y);

            while (slot != NoSlot)
            {
                var other = _bodies[slot];

                if (other.EntityId != excludeEntityId &&
                    other.XRaw == xRaw &&
                    other.YRaw == yRaw)
                {
                    return true;
                }

                slot = _nextSlotInCell[slot];
            }
        }

        return false;
    }

    /// <summary>
    /// Squared distance between two body centres, in squared raw units. Widened
    /// to <see cref="long"/> and checked so a valid map's coordinate range
    /// cannot silently wrap.
    /// </summary>
    internal static long SquaredDistance(int aXRaw, int aYRaw, int bXRaw, int bYRaw)
    {
        var deltaX = (long)bXRaw - aXRaw;
        var deltaY = (long)bYRaw - aYRaw;
        return checked((deltaX * deltaX) + (deltaY * deltaY));
    }

    /// <summary>
    /// The squared centre distance at which two bodies of
    /// <paramref name="bodyRadiusRaw"/> exactly touch, that is
    /// <c>(2 * bodyRadiusRaw)^2</c>.
    /// </summary>
    internal static long ContactSquaredDistance(int bodyRadiusRaw)
    {
        var diameterRaw = checked(2L * bodyRadiusRaw);
        return checked(diameterRaw * diameterRaw);
    }

    /// <summary>
    /// True when two bodies strictly penetrate:
    /// <c>squaredDistance &lt; (2 * bodyRadiusRaw)^2</c>. This is the invariant
    /// a committed position must never violate.
    /// </summary>
    internal static bool Overlaps(int aXRaw, int aYRaw, int bXRaw, int bYRaw, int bodyRadiusRaw) =>
        SquaredDistance(aXRaw, aYRaw, bXRaw, bYRaw) < ContactSquaredDistance(bodyRadiusRaw);

    /// <summary>
    /// True when two bodies touch or penetrate:
    /// <c>squaredDistance &lt;= (2 * bodyRadiusRaw)^2</c>. This is the
    /// pair-candidate predicate: it admits the tangent resting position that
    /// <see cref="Overlaps"/> deliberately rejects.
    /// </summary>
    internal static bool IsContact(int aXRaw, int aYRaw, int bXRaw, int bYRaw, int bodyRadiusRaw) =>
        SquaredDistance(aXRaw, aYRaw, bXRaw, bYRaw) <= ContactSquaredDistance(bodyRadiusRaw);

    /// <summary>
    /// The cell coordinate for one raw axis value, by floor division rather
    /// than truncating division, so a negative coordinate lands in the correct
    /// negative cell instead of sharing cell zero with the positive side of the
    /// origin.
    /// </summary>
    private int CellCoordinate(int valueRaw) => IntegerMath.FloorDiv(valueRaw, CellSizeRaw);

    /// <summary>
    /// Packs a cell coordinate pair into one order-preserving key. Each signed
    /// axis is mapped onto its unsigned counterpart by flipping the sign bit —
    /// the classic bijection that turns signed comparison order into unsigned
    /// comparison order — then the mapped Y half occupies the high 32 bits and
    /// the mapped X half the low 32 bits, so ascending key order is exactly
    /// ascending cell Y then ascending cell X, over the full negative-to-positive
    /// range of both axes.
    /// </summary>
    private static ulong PackCellKey(int cellX, int cellY)
    {
        var sortableX = unchecked((uint)cellX) ^ 0x8000_0000u;
        var sortableY = unchecked((uint)cellY) ^ 0x8000_0000u;
        return ((ulong)sortableY << 32) | sortableX;
    }

    /// <summary>
    /// Finds the hash-table slot for <paramref name="key"/>, or the empty slot
    /// where it belongs, by linear probing. <paramref name="createIfMissing"/>
    /// controls whether an empty probe result is returned as a fresh slot or as
    /// <see cref="NoSlot"/>.
    /// </summary>
    private int FindOrCreateHashSlot(ulong key, out bool created)
    {
        GrowHashIfNeeded();

        var index = (int)(MixKey(key) & (ulong)_hashMask);

        while (_hashOccupied[index])
        {
            if (_hashKeys[index] == key)
            {
                created = false;
                return index;
            }

            index = (index + 1) & _hashMask;
        }

        _hashOccupied[index] = true;
        _hashKeys[index] = key;
        _hashCount++;
        created = true;
        return index;
    }

    /// <summary>
    /// Finds the hash-table slot for an already-inserted <paramref name="key"/>,
    /// or <see cref="NoSlot"/> if the key was never inserted. Never grows and
    /// never creates, so it is safe on a lookup-only path.
    /// </summary>
    private int FindExistingHashSlot(ulong key)
    {
        var index = (int)(MixKey(key) & (ulong)_hashMask);

        while (_hashOccupied[index])
        {
            if (_hashKeys[index] == key)
            {
                return index;
            }

            index = (index + 1) & _hashMask;
        }

        return NoSlot;
    }

    /// <summary>
    /// A cheap integer mix over a packed cell key, spreading its bits before
    /// masking into the hash table's index range. Uses the fixed-point
    /// splittable mixer's finishing step rather than inventing a new one, so
    /// the mixing quality is already proven elsewhere in this codebase.
    /// </summary>
    private static ulong MixKey(ulong key)
    {
        var mixed = key;
        mixed ^= mixed >> 33;
        mixed *= 0xFF51AFD7ED558CCDUL;
        mixed ^= mixed >> 33;
        mixed *= 0xC4CEB9FE1A85EC53UL;
        mixed ^= mixed >> 33;
        return mixed;
    }

    /// <summary>
    /// The first slot of the given cell, or <see cref="NoSlot"/> when the cell
    /// holds no body.
    /// </summary>
    private int FirstSlotInCell(int cellX, int cellY)
    {
        var hashIndex = FindExistingHashSlot(PackCellKey(cellX, cellY));
        return hashIndex == NoSlot ? NoSlot : _hashHeadSlot[hashIndex];
    }

    /// <summary>
    /// Sorts the occupied cells, then walks each cell's bodies against its
    /// three-by-three neighbourhood, emitting a pair only from the body with
    /// the lower entity ID.
    /// </summary>
    private void GeneratePairs(int bodyRadiusRaw)
    {
        _pairs.Clear();

        if (_bodyCount < 2)
        {
            return;
        }

        Array.Copy(_occupiedCellKeys, _traversalOrder, _occupiedCellCount);
        Array.Sort(_traversalOrder, 0, _occupiedCellCount);

        for (var orderIndex = 0; orderIndex < _occupiedCellCount; orderIndex++)
        {
            var key = _traversalOrder[orderIndex];
            var hashIndex = FindExistingHashSlot(key);
            var slot = _hashHeadSlot[hashIndex];

            while (slot != NoSlot)
            {
                var body = _bodies[slot];
                AppendContactsOf(body, CellCoordinate(body.XRaw), CellCoordinate(body.YRaw), bodyRadiusRaw);
                slot = _nextSlotInCell[slot];
            }
        }

        _pairs.Sort();
    }

    private void AppendContactsOf(in SandataCollisionBody body, int cellX, int cellY, int bodyRadiusRaw)
    {
        foreach (var offset in NeighbourOffsets)
        {
            var slot = FirstSlotInCell(cellX + offset.X, cellY + offset.Y);

            while (slot != NoSlot)
            {
                var other = _bodies[slot];

                if (other.EntityId > body.EntityId &&
                    IsContact(body.XRaw, body.YRaw, other.XRaw, other.YRaw, bodyRadiusRaw))
                {
                    _pairs.Add(SandataCollisionPair.Create(body.EntityId, other.EntityId));
                }

                slot = _nextSlotInCell[slot];
            }
        }
    }

    /// <summary>
    /// Rejects a radius the grid cannot serve. A cell narrower than one body
    /// diameter would let two contacting bodies land more than one cell apart,
    /// which the three-by-three neighbourhood would miss.
    /// </summary>
    private void ValidateBodyRadius(int bodyRadiusRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bodyRadiusRaw);

        if (2L * bodyRadiusRaw > CellSizeRaw)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bodyRadiusRaw),
                bodyRadiusRaw,
                "A collision grid cell must be at least one body diameter wide.");
        }
    }

    private static void Grow<T>(ref T[] buffer, int requiredLength, int usedLength)
    {
        var grown = new T[Math.Max(requiredLength, buffer.Length * 2)];
        Array.Copy(buffer, grown, usedLength);
        buffer = grown;
    }

    private void EnsureBodyCapacity(int requiredSlots)
    {
        if (requiredSlots <= _bodies.Length)
        {
            return;
        }

        Grow(ref _bodies, requiredSlots, _bodyCount);
        Grow(ref _nextSlotInCell, requiredSlots, _bodyCount);
    }

    private void EnsureOccupiedCapacity(int requiredCells)
    {
        if (requiredCells <= _occupiedCellKeys.Length)
        {
            return;
        }

        Grow(ref _occupiedCellKeys, requiredCells, _occupiedCellCount);
        Grow(ref _occupiedCellHashIndex, requiredCells, _occupiedCellCount);
        Grow(ref _traversalOrder, requiredCells, 0);
    }

    /// <summary>
    /// Rehashes into a table twice the size once the table is more than
    /// seven-tenths full, so linear probing stays short. Every occupied cell's
    /// recorded hash index in <see cref="_occupiedCellHashIndex"/> is updated to
    /// match, because the rehash can move it.
    /// </summary>
    private void GrowHashIfNeeded()
    {
        var capacity = _hashKeys.Length;

        if (_hashCount * 10 < capacity * 7)
        {
            return;
        }

        var newCapacity = capacity * 2;
        var newKeys = new ulong[newCapacity];
        var newHeadSlot = new int[newCapacity];
        var newOccupied = new bool[newCapacity];
        var newMask = newCapacity - 1;

        for (var index = 0; index < _occupiedCellCount; index++)
        {
            var oldHashIndex = _occupiedCellHashIndex[index];
            var key = _hashKeys[oldHashIndex];
            var headSlot = _hashHeadSlot[oldHashIndex];

            var newIndex = (int)(MixKey(key) & (ulong)newMask);

            while (newOccupied[newIndex])
            {
                newIndex = (newIndex + 1) & newMask;
            }

            newKeys[newIndex] = key;
            newHeadSlot[newIndex] = headSlot;
            newOccupied[newIndex] = true;
            _occupiedCellHashIndex[index] = newIndex;
        }

        _hashKeys = newKeys;
        _hashHeadSlot = newHeadSlot;
        _hashOccupied = newOccupied;
        _hashMask = newMask;
    }
}
