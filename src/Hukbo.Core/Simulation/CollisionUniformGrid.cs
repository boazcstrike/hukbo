namespace Hukbo.Core.Simulation;

/// <summary>
/// A deterministic uniform-grid broad phase over living agent bodies. It exists
/// to answer two questions cheaply: which unordered pairs of living bodies are
/// in contact this tick, and whether a proposed position would touch any body
/// already committed this tick.
/// </summary>
/// <remarks>
/// <para>
/// The grid is a derived accelerator, never authoritative state. It is not
/// hashed, not snapshotted, and not persisted. Its only contract is that it
/// produces exactly what an O(n^2) scan over the same bodies would produce, in
/// exactly one order.
/// </para>
/// <para>
/// Determinism rests on three properties. Occupied cells are visited in
/// ascending cell Y then ascending cell X, obtained by sorting packed cell keys
/// rather than by enumerating the lookup dictionary. Each cell's neighbourhood
/// is scanned in one fixed three-by-three offset order. Every emitted pair is
/// normalised to (lower entity ID, higher entity ID) and the finished list is
/// sorted with <see cref="CollisionPair.CompareTo"/>, so the output cannot
/// depend on the order the caller happened to supply bodies in.
/// </para>
/// <para>
/// A pair is emitted only from the body with the lower entity ID, so scanning
/// the full three-by-three neighbourhood still yields each unordered pair
/// exactly once, without a de-duplication set.
/// </para>
/// <para>
/// The contact predicate is <see cref="CollisionGeometry.IsContact"/>, which is
/// inclusive: exact tangency is a candidate pair. The strict
/// <see cref="CollisionGeometry.Overlaps"/> test belongs to the resolver, which
/// decides that tangency is a legal resting position.
/// </para>
/// <para>
/// Dead bodies are skipped on the way in. Corpses never collide and never
/// generate pairs, so a battlefield full of them costs nothing here.
/// </para>
/// <para>
/// All storage is reused between calls and grows only when capacity is
/// insufficient, so a warm tick allocates nothing.
/// </para>
/// </remarks>
internal sealed class CollisionUniformGrid
{
    /// <summary>Terminator for the per-cell body chains.</summary>
    private const int NoSlot = -1;

    private const int InitialCapacity = 64;

    /// <summary>
    /// The three-by-three neighbourhood in one fixed order: ascending offset Y,
    /// then ascending offset X, including the cell itself. A cell size of at
    /// least one body diameter is enforced by
    /// <see cref="ValidateBodyRadius"/>, which is what makes this neighbourhood
    /// sufficient: bodies two cells apart on an axis are separated by strictly
    /// more than one cell size, hence by strictly more than one diameter.
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
    /// Maps a packed cell key to its index in <see cref="_cellKeys"/>. Used for
    /// lookup only; its enumeration order never reaches the output.
    /// </summary>
    private readonly Dictionary<long, int> _cellIndexByKey = [];

    private readonly List<CollisionPair> _pairs = [];

    /// <summary>Bodies in insertion order. Index doubles as the chain slot.</summary>
    private CollisionBody[] _bodies = new CollisionBody[InitialCapacity];

    /// <summary>Next slot in the same cell, or <see cref="NoSlot"/>.</summary>
    private int[] _nextSlotInCell = new int[InitialCapacity];

    /// <summary>Packed key of each occupied cell, in first-touch order.</summary>
    private long[] _cellKeys = new long[InitialCapacity];

    /// <summary>First slot of each occupied cell, indexed as <see cref="_cellKeys"/>.</summary>
    private int[] _cellHeadSlots = new int[InitialCapacity];

    /// <summary>Scratch copy of <see cref="_cellKeys"/> sorted for traversal.</summary>
    private long[] _traversalOrder = new long[InitialCapacity];

    private int _bodyCount;

    private int _cellCount;

    /// <param name="cellSizeRaw">
    /// The edge length of one square cell in raw fixed-point units.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="cellSizeRaw"/> is zero or negative.
    /// </exception>
    internal CollisionUniformGrid(int cellSizeRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellSizeRaw);

        CellSizeRaw = cellSizeRaw;
    }

    /// <summary>The edge length of one square cell in raw fixed-point units.</summary>
    internal int CellSizeRaw { get; }

    /// <summary>
    /// Every unordered pair of living bodies in contact after the last
    /// <see cref="Rebuild"/>, sorted ascending by
    /// <see cref="CollisionPair.CompareTo"/> with each pair present exactly once.
    /// </summary>
    /// <remarks>
    /// The list is owned by the grid and is overwritten by the next
    /// <see cref="Rebuild"/> or <see cref="Clear"/>. Callers read it within the
    /// tick that produced it and never retain it.
    /// </remarks>
    internal IReadOnlyList<CollisionPair> Pairs => _pairs;

    /// <summary>
    /// The same collection as <see cref="Pairs"/>, exposed as its concrete
    /// <see cref="List{T}"/> type so a per-tick <c>foreach</c> can bind to
    /// <see cref="List{T}.Enumerator"/> instead of boxing through
    /// <see cref="IEnumerator{T}"/>. Carries the exact same ownership and
    /// lifetime rules as <see cref="Pairs"/>.
    /// </summary>
    internal List<CollisionPair> PairsList => _pairs;

    /// <summary>
    /// Runs the whole broad phase: discards the previous contents, indexes every
    /// living body, and produces <see cref="Pairs"/>.
    /// </summary>
    /// <param name="bodies">
    /// The bodies to index. Dead bodies are skipped rather than rejected.
    /// </param>
    /// <param name="bodyRadiusRaw">The common body radius in raw units.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="bodies"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bodyRadiusRaw"/> is negative or so large that a body
    /// diameter exceeds <see cref="CellSizeRaw"/>, or a living body has a
    /// negative coordinate.
    /// </exception>
    internal void Rebuild(IReadOnlyList<CollisionBody> bodies, int bodyRadiusRaw)
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
        _cellIndexByKey.Clear();
        _pairs.Clear();
        _bodyCount = 0;
        _cellCount = 0;
    }

    /// <summary>
    /// Indexes one body so that later <see cref="AnyContact"/> queries can see
    /// it. This is the incremental path the resolver uses as it commits agents
    /// one at a time; it does not update <see cref="Pairs"/>.
    /// </summary>
    /// <remarks>
    /// A dead body is ignored, because corpses never collide. Its coordinates
    /// are therefore not validated: a corpse may sit anywhere.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A living <paramref name="body"/> has a negative coordinate. Real agent
    /// positions are clamped to <c>[R, dimension - R]</c>, so the grid needs no
    /// negative origin handling.
    /// </exception>
    internal void Insert(in CollisionBody body)
    {
        if (!body.IsAlive)
        {
            return;
        }

        ValidateCoordinates(body.XRaw, body.YRaw);

        var key = PackCellKey(CellCoordinate(body.XRaw), CellCoordinate(body.YRaw));
        var slot = _bodyCount;

        EnsureCapacity(slot + 1);
        _bodies[slot] = body;

        if (_cellIndexByKey.TryGetValue(key, out var cellIndex))
        {
            _nextSlotInCell[slot] = _cellHeadSlots[cellIndex];
            _cellHeadSlots[cellIndex] = slot;
        }
        else
        {
            _nextSlotInCell[slot] = NoSlot;
            _cellKeys[_cellCount] = key;
            _cellHeadSlots[_cellCount] = slot;
            _cellIndexByKey.Add(key, _cellCount);
            _cellCount++;
        }

        _bodyCount++;
    }

    /// <summary>
    /// Removes one body from the index so that later queries no longer see it.
    /// This is what lets the resolver's pending index shrink as movers resolve,
    /// one mover at a time, without rebuilding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A dead body is ignored on the way in by <see cref="Insert"/>, so removing
    /// one is a no-op. Removing a body that was never inserted is also a no-op
    /// rather than a throw: a caller that owns a body's lifetime already knows
    /// whether it inserted that body, and turning a harmless double removal into
    /// an exception would buy nothing.
    /// </para>
    /// <para>
    /// Removal unlinks the body's slot from its cell chain. That chain holds at
    /// most four bodies, by the same packing argument that bounds every query
    /// here: centres pairwise at least one diameter apart cannot fit more than
    /// four into a square cell of edge one diameter. So an unlink walks at most
    /// four slots.
    /// </para>
    /// <para>
    /// Removal does not update <see cref="Pairs"/>, exactly as <see cref="Insert"/>
    /// does not. An index mutated by removal is not one <see cref="Rebuild"/>
    /// produced and its <see cref="Pairs"/> list is not meaningful.
    /// </para>
    /// <para>
    /// The removed slot stays allocated and stays counted by the body count. That
    /// count drives capacity growth, not traversal, and no traversal can reach a
    /// slot that is no longer on a chain.
    /// </para>
    /// </remarks>
    internal void Remove(in CollisionBody body)
    {
        if (!body.IsAlive)
        {
            return;
        }

        var key = PackCellKey(CellCoordinate(body.XRaw), CellCoordinate(body.YRaw));

        if (!_cellIndexByKey.TryGetValue(key, out var cellIndex))
        {
            return;
        }

        var slot = _cellHeadSlots[cellIndex];
        var previousSlot = NoSlot;

        while (slot != NoSlot)
        {
            if (_bodies[slot].EntityId == body.EntityId)
            {
                if (previousSlot == NoSlot)
                {
                    _cellHeadSlots[cellIndex] = _nextSlotInCell[slot];
                }
                else
                {
                    _nextSlotInCell[previousSlot] = _nextSlotInCell[slot];
                }

                return;
            }

            previousSlot = slot;
            slot = _nextSlotInCell[slot];
        }
    }

    /// <summary>
    /// True when a body of <paramref name="bodyRadiusRaw"/> centred at
    /// (<paramref name="xRaw"/>, <paramref name="yRaw"/>) would touch or
    /// penetrate any indexed body other than
    /// <paramref name="excludeEntityId"/>.
    /// </summary>
    /// <remarks>
    /// The predicate is inclusive, matching <see cref="Pairs"/>. The resolver
    /// wants the stricter overlap test for a committed position, so it must not
    /// treat a <see langword="true"/> answer here as proof of penetration.
    /// </remarks>
    /// <param name="excludeEntityId">
    /// The entity being moved, so that it does not collide with its own
    /// previously indexed body.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A coordinate is negative, or <paramref name="bodyRadiusRaw"/> is negative
    /// or so large that a body diameter exceeds <see cref="CellSizeRaw"/>.
    /// </exception>
    internal bool AnyContact(
        int xRaw,
        int yRaw,
        int bodyRadiusRaw,
        ulong excludeEntityId)
    {
        ValidateBodyRadius(bodyRadiusRaw);
        ValidateCoordinates(xRaw, yRaw);

        var cellX = CellCoordinate(xRaw);
        var cellY = CellCoordinate(yRaw);

        foreach (var offset in NeighbourOffsets)
        {
            var slot = FirstSlotInCell(cellX + offset.X, cellY + offset.Y);

            while (slot != NoSlot)
            {
                var other = _bodies[slot];

                if (other.EntityId != excludeEntityId &&
                    CollisionGeometry.IsContact(
                        xRaw,
                        yRaw,
                        other.XRaw,
                        other.YRaw,
                        bodyRadiusRaw))
                {
                    return true;
                }

                slot = _nextSlotInCell[slot];
            }
        }

        return false;
    }

    /// <summary>
    /// True when a body of <paramref name="bodyRadiusRaw"/> centred at
    /// (<paramref name="xRaw"/>, <paramref name="yRaw"/>) would strictly
    /// penetrate any indexed body other than
    /// <paramref name="excludeEntityId"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The predicate is the strict <see cref="CollisionGeometry.Overlaps"/>, so
    /// exact tangency answers <see langword="false"/>. Tangency is a legal
    /// resting position and the resolver depends on that, which is why this
    /// query exists alongside the inclusive <see cref="AnyContact"/> rather than
    /// replacing it.
    /// </para>
    /// <para>
    /// The three-by-three neighbourhood is sufficient here for the same reason it
    /// is sufficient for contact, and a fortiori: strict overlap is a subset of
    /// contact, so any pair this predicate reports is a pair the inclusive
    /// predicate would also report. The cell-size guard in
    /// <see cref="ValidateBodyRadius"/> is what establishes the contact case, and
    /// it therefore covers this one.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A coordinate is negative, or <paramref name="bodyRadiusRaw"/> is negative
    /// or so large that a body diameter exceeds <see cref="CellSizeRaw"/>.
    /// </exception>
    internal bool AnyOverlap(
        int xRaw,
        int yRaw,
        int bodyRadiusRaw,
        ulong excludeEntityId)
    {
        ValidateBodyRadius(bodyRadiusRaw);
        ValidateCoordinates(xRaw, yRaw);

        return AnyOverlapUnchecked(xRaw, yRaw, bodyRadiusRaw, excludeEntityId);
    }

    /// <summary>
    /// <see cref="AnyOverlap"/> without the per-call argument validation, for the
    /// resolver's inner loop, which runs this query tens of millions of times a
    /// tick on arguments that are invariant or provably in range.
    /// </summary>
    /// <remarks>
    /// The caller takes on what <see cref="AnyOverlap"/> would have checked: the
    /// radius must satisfy <see cref="ValidateBodyRadius"/> and both coordinates
    /// must be non-negative. Passing arguments that do not is a caller defect and
    /// produces a wrong answer rather than an exception.
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
                    CollisionGeometry.Overlaps(
                        xRaw,
                        yRaw,
                        other.XRaw,
                        other.YRaw,
                        bodyRadiusRaw))
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
    /// <remarks>
    /// Exact coincidence is a subset of contact, so the neighbourhood argument in
    /// <see cref="AnyOverlap"/> carries here too. It is in fact stronger than
    /// needed — a coincident body maps to the same cell, so the centre cell alone
    /// would answer — but the full neighbourhood is walked so that every query on
    /// this type shares one traversal shape and cannot drift apart from the
    /// others.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A coordinate is negative.
    /// </exception>
    internal bool AnyCoincident(int xRaw, int yRaw, ulong excludeEntityId)
    {
        ValidateCoordinates(xRaw, yRaw);

        return AnyCoincidentUnchecked(xRaw, yRaw, excludeEntityId);
    }

    /// <summary>
    /// <see cref="AnyCoincident"/> without the per-call coordinate validation.
    /// The caller guarantees both coordinates are non-negative.
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
                    CollisionGeometry.IsCoincident(xRaw, yRaw, other.XRaw, other.YRaw))
                {
                    return true;
                }

                slot = _nextSlotInCell[slot];
            }
        }

        return false;
    }

    /// <summary>
    /// Rejects a radius this index cannot serve, once, on behalf of a caller that
    /// will then use the unchecked queries. The resolver holds a radius fixed for
    /// its whole lifetime, so it validates at construction rather than per query.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bodyRadiusRaw"/> is negative or so large that a body
    /// diameter exceeds <see cref="CellSizeRaw"/>.
    /// </exception>
    internal void ValidateRadiusForQueries(int bodyRadiusRaw) =>
        ValidateBodyRadius(bodyRadiusRaw);

    /// <summary>
    /// Packs a cell coordinate pair into one sortable key. Cell coordinates are
    /// non-negative and below 2^31, so the key is non-negative and ascending key
    /// order is exactly ascending cell Y then ascending cell X.
    /// </summary>
    private static long PackCellKey(int cellX, int cellY) =>
        ((long)cellY << 32) | (uint)cellX;

    private static int UnpackCellX(long key) => (int)(uint)key;

    private static int UnpackCellY(long key) => (int)(key >> 32);

    private static void ValidateCoordinates(int xRaw, int yRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(xRaw);
        ArgumentOutOfRangeException.ThrowIfNegative(yRaw);
    }

    private static void Grow<T>(ref T[] buffer, int requiredLength, int usedLength)
    {
        var grown = new T[Math.Max(requiredLength, buffer.Length * 2)];

        Array.Copy(buffer, grown, usedLength);
        buffer = grown;
    }

    /// <summary>
    /// Sorts the occupied cells, then walks each cell's bodies against its
    /// three-by-three neighbourhood, emitting a pair only from the body with the
    /// lower entity ID.
    /// </summary>
    private void GeneratePairs(int bodyRadiusRaw)
    {
        _pairs.Clear();

        if (_bodyCount < 2)
        {
            return;
        }

        Array.Copy(_cellKeys, _traversalOrder, _cellCount);
        Array.Sort(_traversalOrder, 0, _cellCount);

        for (var orderIndex = 0; orderIndex < _cellCount; orderIndex++)
        {
            var key = _traversalOrder[orderIndex];
            var cellX = UnpackCellX(key);
            var cellY = UnpackCellY(key);
            var slot = _cellHeadSlots[_cellIndexByKey[key]];

            while (slot != NoSlot)
            {
                AppendContactsOf(_bodies[slot], cellX, cellY, bodyRadiusRaw);
                slot = _nextSlotInCell[slot];
            }
        }

        _pairs.Sort();
    }

    private void AppendContactsOf(
        in CollisionBody body,
        int cellX,
        int cellY,
        int bodyRadiusRaw)
    {
        foreach (var offset in NeighbourOffsets)
        {
            var slot = FirstSlotInCell(cellX + offset.X, cellY + offset.Y);

            while (slot != NoSlot)
            {
                var other = _bodies[slot];

                if (other.EntityId > body.EntityId &&
                    CollisionGeometry.IsContact(
                        body.XRaw,
                        body.YRaw,
                        other.XRaw,
                        other.YRaw,
                        bodyRadiusRaw))
                {
                    _pairs.Add(CollisionPair.Create(body.EntityId, other.EntityId));
                }

                slot = _nextSlotInCell[slot];
            }
        }
    }

    /// <summary>
    /// The first slot of the given cell, or <see cref="NoSlot"/> when the cell is
    /// off the non-negative quadrant or holds no body.
    /// </summary>
    private int FirstSlotInCell(int cellX, int cellY)
    {
        if (cellX < 0 || cellY < 0)
        {
            return NoSlot;
        }

        return _cellIndexByKey.TryGetValue(PackCellKey(cellX, cellY), out var cellIndex)
            ? _cellHeadSlots[cellIndex]
            : NoSlot;
    }

    private int CellCoordinate(int valueRaw) => valueRaw / CellSizeRaw;

    /// <summary>
    /// Grows every per-slot buffer to hold <paramref name="requiredSlots"/>
    /// bodies. The cell buffers are grown alongside because the number of
    /// occupied cells can never exceed the number of bodies.
    /// </summary>
    private void EnsureCapacity(int requiredSlots)
    {
        if (requiredSlots <= _bodies.Length)
        {
            return;
        }

        Grow(ref _bodies, requiredSlots, _bodyCount);
        Grow(ref _nextSlotInCell, requiredSlots, _bodyCount);
        Grow(ref _cellKeys, requiredSlots, _cellCount);
        Grow(ref _cellHeadSlots, requiredSlots, _cellCount);
        Grow(ref _traversalOrder, requiredSlots, 0);
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
}
