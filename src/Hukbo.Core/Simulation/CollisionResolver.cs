namespace Hukbo.Core.Simulation;

/// <summary>
/// One agent's positional input to the collision stage: where it stood when the
/// tick began, and where movement intent would like it to stand. Coordinates are
/// raw fixed-point units.
/// </summary>
/// <remarks>
/// The preferred destination is meaningful only when
/// <paramref name="HasProposal"/> is <see langword="true"/>. A request without a
/// proposal is a body that is standing still this tick; it still occupies ground
/// and still blocks everyone else.
/// </remarks>
/// <param name="EntityId">The moving agent.</param>
/// <param name="StartXRaw">Tick-start centre X.</param>
/// <param name="StartYRaw">Tick-start centre Y.</param>
/// <param name="PreferredXRaw">Requested centre X, at the full step.</param>
/// <param name="PreferredYRaw">Requested centre Y, at the full step.</param>
/// <param name="HasProposal">
/// <see langword="true"/> when the agent proposed movement this tick.
/// </param>
/// <param name="PriorityKey">
/// This tick's contested-ground priority, from
/// <see cref="CollisionPriority.Resolve"/>. Lower resolves first. Meaningful
/// only for a mover; a stationary body is committed before any mover regardless.
/// </param>
internal readonly record struct CollisionMoveRequest(
    ulong EntityId,
    int StartXRaw,
    int StartYRaw,
    int PreferredXRaw,
    int PreferredYRaw,
    bool HasProposal,
    ulong PriorityKey);

/// <summary>
/// Where one agent actually finished the collision stage, and the authoritative
/// reason it finished there.
/// </summary>
/// <param name="EntityId">The agent this result belongs to.</param>
/// <param name="XRaw">Committed centre X.</param>
/// <param name="YRaw">Committed centre Y.</param>
/// <param name="Resolution">
/// The spectator-visible explanation, written by the collision stage and
/// included in the state hash.
/// </param>
internal readonly record struct CollisionMoveResult(
    ulong EntityId,
    int XRaw,
    int YRaw,
    MovementResolution Resolution);

/// <summary>
/// The <see cref="CollisionPolicy.Solid"/> movement resolver. It takes one
/// movement proposal per living agent and returns one committed position per
/// agent such that no two committed bodies strictly overlap.
/// </summary>
/// <remarks>
/// <para>
/// <b>Contract.</b> Requests must be strictly ascending by
/// <see cref="CollisionMoveRequest.EntityId"/>, must contain living agents only,
/// and must start at non-negative coordinates. Corpses never collide, so they are
/// filtered out before they reach this stage rather than being skipped here.
/// </para>
/// <para>
/// <b>Precondition.</b> No two request start positions may strictly overlap.
/// Spawn resolution guarantees this on the first tick and the resolver's own
/// output invariant carries it forward on every later tick. The one exception the
/// resolver repairs is an exact co-location, which can only arise from a test
/// constructor or an unresolved spawn. Two bodies that merely overlap without
/// sharing a centre are deliberately left exactly where they are: inventing a
/// repair for an input state that spawn validation already forbids would add an
/// untestable code path and a second, weaker notion of a legal position.
/// </para>
/// <para>
/// <b>Order.</b> Stationary bodies are committed first, in ascending entity ID,
/// then movers, in ascending
/// <see cref="CollisionMoveRequest.PriorityKey"/>. Stationary bodies must go
/// first because a standing agent would otherwise have its ground taken by a
/// mover that arrives before the standing agent is ever considered. Movers are
/// ordered by a key that reshuffles every tick rather than by entity ID,
/// because a fixed order hands every cross-faction contest of an entire battle
/// to the faction holding the low IDs; see <see cref="CollisionPriority"/>. The
/// key's low half is the entity ID, so the order is strict and total no matter
/// how the sort behaves on equal keys.
/// </para>
/// <para>
/// <b>Obstacles.</b> A candidate position is tested against every other body:
/// those already committed this tick, and those still pending at their tick-start
/// positions. Testing the pending movers is what makes the output invariant hold
/// unconditionally. Without it a mover resolved earlier could step onto ground a
/// mover resolved later has not yet vacated, and that later mover's last-resort
/// "hold position" fallback would then commit an overlap. A plain head-on
/// approach between two tangent agents is enough to produce that case.
/// </para>
/// <para>
/// <b>Rejection test.</b> A candidate is refused only on strict penetration,
/// <see cref="CollisionGeometry.Overlaps"/>. Exact tangency is an accepted
/// resting position, which is what lets a packed front settle instead of
/// jittering by one raw unit forever. The grid's inclusive
/// <see cref="CollisionUniformGrid.AnyContact"/> is therefore used only as a
/// broad-phase negative filter over committed bodies: when it answers
/// <see langword="false"/> there is provably no penetration, and when it answers
/// <see langword="true"/> the strict predicate decides.
/// </para>
/// <para>
/// <b>Budget.</b> Collision may only reduce displacement. Every candidate other
/// than the exact co-location repair is either the preferred destination, a
/// single-axis projection of it, a strictly shorter step along the same
/// direction, or the start position, and boundary clamping can only pull a
/// candidate back towards the map interior. The co-location repair is the one
/// documented exemption, is bounded, applies at most once per agent per tick, and
/// is reported as <see cref="MovementResolution.Separated"/>.
/// </para>
/// <para>
/// <b>Termination.</b> Every mover evaluates a fixed, bounded candidate list and
/// then stops. There is no convergence loop, no iteration count, and no wall-clock
/// condition anywhere in this class.
/// </para>
/// <para>
/// <b>Allocation.</b> All storage is reused between calls and grows only when
/// capacity is insufficient, so a warm tick allocates nothing.
/// </para>
/// </remarks>
internal sealed class CollisionResolver
{
    /// <summary>
    /// The co-location repair directions, in the fixed order <c>+X, -X, +Y, -Y</c>.
    /// </summary>
    private static readonly (int X, int Y)[] SeparationDirections =
    [
        (1, 0),
        (-1, 0),
        (0, 1),
        (0, -1),
    ];

    /// <summary>
    /// The number of rungs on the truncation ladder. The ladder walks
    /// <c>m &gt;&gt; 1, m &gt;&gt; 2, ...</c> down to and including a length of
    /// one, where <c>m</c> is the preferred movement length. Scenario validation
    /// caps a movement speed at one body radius, and at the approved speed of
    /// 3,072 raw units eleven shifts reach exactly one, so a mover evaluates at
    /// most fourteen candidates per tick.
    /// </summary>
    private const int MaximumTruncationRungs = 11;

    private const int InitialCapacity = 64;

    private readonly CollisionUniformGrid _grid;

    private readonly List<CollisionMoveResult> _results = [];

    private readonly int _bodyRadiusRaw;

    private readonly int _diameterRaw;

    private readonly int _mapWidthRaw;

    private readonly int _mapHeightRaw;

    /// <summary>Bodies committed so far this tick, in commit order.</summary>
    private CollisionBody[] _committed = new CollisionBody[InitialCapacity];

    /// <summary>
    /// Indices into the request list of every mover, in resolution order:
    /// ascending <see cref="CollisionMoveRequest.PriorityKey"/>.
    /// </summary>
    private int[] _moverIndices = new int[InitialCapacity];

    /// <summary>
    /// The priority keys of <see cref="_moverIndices"/>, kept as a parallel
    /// array so the pair can be sorted without a comparison delegate and
    /// therefore without allocating on a warm tick.
    /// </summary>
    private ulong[] _moverKeys = new ulong[InitialCapacity];

    private int _committedCount;

    private int _moverCount;

    /// <param name="bodyRadiusRaw">
    /// The common body radius in raw fixed-point units.
    /// </param>
    /// <param name="mapWidthRaw">Map width in raw fixed-point units.</param>
    /// <param name="mapHeightRaw">Map height in raw fixed-point units.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bodyRadiusRaw"/> is not positive, or either map dimension
    /// is narrower than one body diameter. A map that cannot hold one body has no
    /// legal centre at all, which the boundary rule cannot express.
    /// </exception>
    internal CollisionResolver(int bodyRadiusRaw, int mapWidthRaw, int mapHeightRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bodyRadiusRaw);

        var diameterRaw = checked(2 * bodyRadiusRaw);

        ArgumentOutOfRangeException.ThrowIfLessThan(mapWidthRaw, diameterRaw);
        ArgumentOutOfRangeException.ThrowIfLessThan(mapHeightRaw, diameterRaw);

        _bodyRadiusRaw = bodyRadiusRaw;
        _diameterRaw = diameterRaw;
        _mapWidthRaw = mapWidthRaw;
        _mapHeightRaw = mapHeightRaw;
        _grid = new CollisionUniformGrid(diameterRaw);
    }

    /// <summary>
    /// One result per request, in the order the requests were supplied. The list
    /// is owned by the resolver and is overwritten by the next
    /// <see cref="Resolve"/>, so a caller reads it within the tick that produced
    /// it and never retains it.
    /// </summary>
    internal IReadOnlyList<CollisionMoveResult> Results => _results;

    /// <summary>
    /// Movement proposals that resolved to an accepted destination in the last
    /// <see cref="Resolve"/>, that is <see cref="MovementResolution.Moved"/>,
    /// <see cref="MovementResolution.Truncated"/>, or
    /// <see cref="MovementResolution.Slid"/>. The co-location repair is not a
    /// movement proposal and is not counted.
    /// </summary>
    internal int AcceptedMoveCount { get; private set; }

    /// <summary>
    /// Agents that resolved to <see cref="MovementResolution.Blocked"/> in the
    /// last <see cref="Resolve"/>.
    /// </summary>
    internal int BlockedCount { get; private set; }

    /// <summary>
    /// Resolves one tick of movement. On return, <see cref="Results"/> holds one
    /// committed position per request and no two of those positions strictly
    /// overlap.
    /// </summary>
    /// <param name="requests">
    /// The living agents of this tick, strictly ascending by entity ID.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="requests"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="requests"/> is not strictly ascending by entity ID.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A request starts at a negative coordinate. Real agent positions are clamped
    /// to <c>[R, dimension - R]</c>, so a negative start is a caller defect.
    /// </exception>
    internal void Resolve(IReadOnlyList<CollisionMoveRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        Validate(requests);

        Reset(requests);
        CommitStationaryBodies(requests);
        CommitMovers(requests);
    }

    /// <summary>
    /// Rejects an input the resolver cannot serve, before any state is touched, so
    /// a rejected call leaves the previous results intact rather than half
    /// overwritten.
    /// </summary>
    private static void Validate(IReadOnlyList<CollisionMoveRequest> requests)
    {
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];

            if (request.StartXRaw < 0 || request.StartYRaw < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requests),
                    request,
                    "A collision move request must start at a non-negative coordinate.");
            }

            if (index > 0 && requests[index - 1].EntityId >= request.EntityId)
            {
                throw new ArgumentException(
                    "Collision move requests must be strictly ascending by entity ID.",
                    nameof(requests));
            }
        }
    }

    /// <summary>
    /// The integer square root of a non-negative value, by the classic
    /// two-bits-at-a-time method. Integer only: floating point is banned anywhere
    /// near the state hash because its rounding is not guaranteed to be identical
    /// across machines.
    /// </summary>
    private static long IntegerSquareRoot(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var remainder = value;
        var result = 0L;
        var bit = 1L << 62;

        while (bit > remainder)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (remainder >= result + bit)
            {
                remainder -= result + bit;
                result = (result >> 1) + bit;
            }
            else
            {
                result >>= 1;
            }

            bit >>= 2;
        }

        return result;
    }

    /// <summary>
    /// Grows <paramref name="buffer"/> to at least <paramref name="requiredLength"/>
    /// by replacing it with a fresh, larger array, doubling capacity for
    /// amortized cost. The old array's contents are deliberately not copied
    /// into the replacement.
    /// </summary>
    /// <remarks>
    /// Discarding the old contents is safe only because <see cref="Reset"/>,
    /// this method's only caller, always refills every slot of the new array
    /// that this tick will read before any read happens. A copy here would
    /// cost real time on every reallocating tick to preserve data no read
    /// ever depends on.
    /// </remarks>
    private static void Grow<T>(ref T[] buffer, int requiredLength)
    {
        if (requiredLength <= buffer.Length)
        {
            return;
        }

        buffer = new T[Math.Max(requiredLength, buffer.Length * 2)];
    }

    /// <summary>
    /// Discards the previous tick and lays out one placeholder result per request
    /// so that both passes can write their results by request index and still
    /// produce the requested order.
    /// </summary>
    private void Reset(IReadOnlyList<CollisionMoveRequest> requests)
    {
        _grid.Clear();
        _results.Clear();
        _committedCount = 0;
        _moverCount = 0;
        AcceptedMoveCount = 0;
        BlockedCount = 0;

        Grow(ref _committed, requests.Count);
        Grow(ref _moverIndices, requests.Count);
        Grow(ref _moverKeys, requests.Count);

        for (var index = 0; index < requests.Count; index++)
        {
            _results.Add(default);

            if (requests[index].HasProposal)
            {
                _moverIndices[_moverCount] = index;

                // The entity ID is stamped into the low half here rather than
                // trusted from the caller. Array.Sort is an unstable introsort,
                // so two equal keys would permute in an implementation-defined
                // way and the same build could diverge from itself. Composing
                // the ID in makes distinctness structural: keys can only be
                // equal if the entity IDs are, and those are validated unique.
                _moverKeys[_moverCount] =
                    (requests[index].PriorityKey & 0xFFFF_FFFF_0000_0000UL) |
                    (requests[index].EntityId & 0xFFFF_FFFFUL);
                _moverCount++;
            }
        }

        // Ascending priority key: this tick's contested-ground order.
        Array.Sort(_moverKeys, _moverIndices, 0, _moverCount);
    }

    /// <summary>
    /// Pass one. Commits every agent that is standing still, repairing an exact
    /// co-location on the way.
    /// </summary>
    private void CommitStationaryBodies(IReadOnlyList<CollisionMoveRequest> requests)
    {
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];

            if (request.HasProposal)
            {
                continue;
            }

            var xRaw = request.StartXRaw;
            var yRaw = request.StartYRaw;
            var resolution = MovementResolution.None;

            if (IsCoincidentWithCommitted(xRaw, yRaw, request.EntityId))
            {
                resolution = TrySeparate(requests, request, out var separatedXRaw, out var separatedYRaw)
                    ? MovementResolution.Separated
                    : MovementResolution.Blocked;

                if (resolution == MovementResolution.Separated)
                {
                    xRaw = separatedXRaw;
                    yRaw = separatedYRaw;
                }
            }

            Commit(index, request.EntityId, xRaw, yRaw, resolution);
        }
    }

    /// <summary>
    /// Pass two. Commits every mover, taking the first candidate that is legal
    /// against everything else on the field.
    /// </summary>
    private void CommitMovers(IReadOnlyList<CollisionMoveRequest> requests)
    {
        for (var moverIndex = 0; moverIndex < _moverCount; moverIndex++)
        {
            var requestIndex = _moverIndices[moverIndex];
            var request = requests[requestIndex];
            var pendingFrom = moverIndex + 1;

            // 1. The preferred destination at the full step.
            if (TryAccept(
                requests,
                request,
                request.PreferredXRaw,
                request.PreferredYRaw,
                pendingFrom,
                rejectStartPosition: false,
                out var xRaw,
                out var yRaw))
            {
                Commit(requestIndex, request.EntityId, xRaw, yRaw, MovementResolution.Moved);
                continue;
            }

            // 2. and 3. Single-axis slides. A slide that lands back on the start
            // position is not a slide, so it is skipped rather than mislabelled;
            // the hold-position fallback reports that case truthfully.
            if (TryAccept(
                    requests,
                    request,
                    request.PreferredXRaw,
                    request.StartYRaw,
                    pendingFrom,
                    rejectStartPosition: true,
                    out xRaw,
                    out yRaw) ||
                TryAccept(
                    requests,
                    request,
                    request.StartXRaw,
                    request.PreferredYRaw,
                    pendingFrom,
                    rejectStartPosition: true,
                    out xRaw,
                    out yRaw))
            {
                Commit(requestIndex, request.EntityId, xRaw, yRaw, MovementResolution.Slid);
                continue;
            }

            // 4. The truncation ladder along the preferred direction.
            if (TryTruncate(requests, request, pendingFrom, out xRaw, out yRaw))
            {
                Commit(requestIndex, request.EntityId, xRaw, yRaw, MovementResolution.Truncated);
                continue;
            }

            // 5. Hold the tick-start position. Always legal: every committed body
            // was tested against this agent's start while the agent was pending.
            Commit(
                requestIndex,
                request.EntityId,
                request.StartXRaw,
                request.StartYRaw,
                MovementResolution.Blocked);
        }
    }

    /// <summary>
    /// Walks the truncation ladder, taking the first shorter step along the
    /// preferred direction that is legal. Rounding uses the integer form
    /// <c>delta * length / distance</c>, which truncates toward zero; odd
    /// remainders are discarded rather than redistributed, because the solid
    /// resolver moves exactly one agent at a time and so has nothing to split.
    /// </summary>
    private bool TryTruncate(
        IReadOnlyList<CollisionMoveRequest> requests,
        in CollisionMoveRequest request,
        int pendingFrom,
        out int xRaw,
        out int yRaw)
    {
        var deltaXRaw = (long)request.PreferredXRaw - request.StartXRaw;
        var deltaYRaw = (long)request.PreferredYRaw - request.StartYRaw;
        var distanceRaw = IntegerSquareRoot(
            checked((deltaXRaw * deltaXRaw) + (deltaYRaw * deltaYRaw)));

        if (distanceRaw > 0)
        {
            for (var rung = 1; rung <= MaximumTruncationRungs; rung++)
            {
                var lengthRaw = distanceRaw >> rung;

                if (lengthRaw < 1)
                {
                    break;
                }

                var stepXRaw = deltaXRaw * lengthRaw / distanceRaw;
                var stepYRaw = deltaYRaw * lengthRaw / distanceRaw;

                if (stepXRaw == 0 && stepYRaw == 0)
                {
                    continue;
                }

                if (TryAccept(
                    requests,
                    request,
                    request.StartXRaw + stepXRaw,
                    request.StartYRaw + stepYRaw,
                    pendingFrom,
                    rejectStartPosition: true,
                    out xRaw,
                    out yRaw))
                {
                    return true;
                }
            }
        }

        xRaw = request.StartXRaw;
        yRaw = request.StartYRaw;
        return false;
    }

    /// <summary>
    /// Clamps one candidate into the map and tests it. A clamped candidate is
    /// still a candidate, so the contact test runs after clamping, not before.
    /// </summary>
    /// <param name="rejectStartPosition">
    /// Refuses a candidate that lands back on the tick-start position, so that a
    /// zero-length step cannot be reported as movement.
    /// </param>
    private bool TryAccept(
        IReadOnlyList<CollisionMoveRequest> requests,
        in CollisionMoveRequest request,
        long candidateXRaw,
        long candidateYRaw,
        int pendingFrom,
        bool rejectStartPosition,
        out int xRaw,
        out int yRaw)
    {
        xRaw = CollisionGeometry.ClampCenterToBounds(
            ToCoordinate(candidateXRaw),
            _mapWidthRaw,
            _bodyRadiusRaw);
        yRaw = CollisionGeometry.ClampCenterToBounds(
            ToCoordinate(candidateYRaw),
            _mapHeightRaw,
            _bodyRadiusRaw);

        if (rejectStartPosition &&
            xRaw == request.StartXRaw &&
            yRaw == request.StartYRaw)
        {
            return false;
        }

        return IsFree(requests, xRaw, yRaw, request.EntityId, pendingFrom);
    }

    /// <summary>
    /// Displaces a co-located body by exactly one diameter, trying
    /// <see cref="SeparationDirections"/> in order and taking the first that is
    /// inside the map and free. The candidate must be inside the map on its own,
    /// not clamped into it, because clamping would change the displacement the
    /// repair is defined to apply.
    /// </summary>
    /// <remarks>
    /// This repairs an input state that spawn validation already forbids, so it is
    /// best effort: it is tested against every body already committed and every
    /// mover still pending, but the input it repairs has already violated the
    /// no-overlap precondition, and three or more agents sharing one centre can
    /// leave a later one with nowhere legal to go. That case reports
    /// <see cref="MovementResolution.Blocked"/> rather than throwing.
    /// </remarks>
    private bool TrySeparate(
        IReadOnlyList<CollisionMoveRequest> requests,
        in CollisionMoveRequest request,
        out int xRaw,
        out int yRaw)
    {
        foreach (var direction in SeparationDirections)
        {
            var candidateXRaw = (long)request.StartXRaw + ((long)direction.X * _diameterRaw);
            var candidateYRaw = (long)request.StartYRaw + ((long)direction.Y * _diameterRaw);

            if (!IsInsideBounds(candidateXRaw, candidateYRaw))
            {
                continue;
            }

            xRaw = (int)candidateXRaw;
            yRaw = (int)candidateYRaw;

            if (IsFree(requests, xRaw, yRaw, request.EntityId, pendingFrom: 0))
            {
                return true;
            }
        }

        xRaw = request.StartXRaw;
        yRaw = request.StartYRaw;
        return false;
    }

    /// <summary>
    /// True when a body centred at the given position would not strictly penetrate
    /// any other body on the field: neither one already committed this tick, nor
    /// one still pending at its tick-start position.
    /// </summary>
    /// <param name="pendingFrom">
    /// The first index into the mover list that has not been committed yet.
    /// </param>
    private bool IsFree(
        IReadOnlyList<CollisionMoveRequest> requests,
        int xRaw,
        int yRaw,
        ulong entityId,
        int pendingFrom)
    {
        // The grid is inclusive of exact tangency, which is a legal resting
        // position, so a hit here only means "something is close" and must be
        // confirmed with the strict predicate. A miss is conclusive.
        if (_grid.AnyContact(xRaw, yRaw, _bodyRadiusRaw, entityId) &&
            OverlapsCommitted(xRaw, yRaw, entityId))
        {
            return false;
        }

        for (var moverIndex = pendingFrom; moverIndex < _moverCount; moverIndex++)
        {
            var pending = requests[_moverIndices[moverIndex]];

            if (pending.EntityId != entityId &&
                CollisionGeometry.Overlaps(
                    xRaw,
                    yRaw,
                    pending.StartXRaw,
                    pending.StartYRaw,
                    _bodyRadiusRaw))
            {
                return false;
            }
        }

        return true;
    }

    private bool OverlapsCommitted(int xRaw, int yRaw, ulong entityId)
    {
        for (var index = 0; index < _committedCount; index++)
        {
            var body = _committed[index];

            if (body.EntityId != entityId &&
                CollisionGeometry.Overlaps(xRaw, yRaw, body.XRaw, body.YRaw, _bodyRadiusRaw))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the position exactly equals the centre of an already committed
    /// body. The grid answers the cheap negative case: a coincident body is always
    /// in contact, so a miss rules coincidence out.
    /// </summary>
    private bool IsCoincidentWithCommitted(int xRaw, int yRaw, ulong entityId)
    {
        if (!_grid.AnyContact(xRaw, yRaw, _bodyRadiusRaw, entityId))
        {
            return false;
        }

        for (var index = 0; index < _committedCount; index++)
        {
            var body = _committed[index];

            if (body.EntityId != entityId &&
                CollisionGeometry.IsCoincident(xRaw, yRaw, body.XRaw, body.YRaw))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Records one agent's outcome and makes its body an obstacle for everyone
    /// resolved after it.
    /// </summary>
    private void Commit(
        int requestIndex,
        ulong entityId,
        int xRaw,
        int yRaw,
        MovementResolution resolution)
    {
        var body = new CollisionBody(entityId, xRaw, yRaw, IsAlive: true);

        _results[requestIndex] = new CollisionMoveResult(entityId, xRaw, yRaw, resolution);
        _committed[_committedCount] = body;
        _committedCount++;
        _grid.Insert(body);

        switch (resolution)
        {
            case MovementResolution.Moved:
            case MovementResolution.Truncated:
            case MovementResolution.Slid:
                AcceptedMoveCount = checked(AcceptedMoveCount + 1);
                break;

            case MovementResolution.Blocked:
                BlockedCount = checked(BlockedCount + 1);
                break;

            case MovementResolution.None:
            case MovementResolution.Separated:
            default:
                break;
        }
    }

    private bool IsInsideBounds(long xRaw, long yRaw) =>
        xRaw >= _bodyRadiusRaw &&
        xRaw <= (long)_mapWidthRaw - _bodyRadiusRaw &&
        yRaw >= _bodyRadiusRaw &&
        yRaw <= (long)_mapHeightRaw - _bodyRadiusRaw;

    /// <summary>
    /// Saturates a candidate coordinate into <see cref="int"/> before the boundary
    /// clamp, which works in <see cref="int"/>. Saturation is safe because the
    /// clamp pulls anything outside the map back to the edge anyway.
    /// </summary>
    private static int ToCoordinate(long valueRaw) =>
        (int)Math.Clamp(valueRaw, int.MinValue, int.MaxValue);
}
