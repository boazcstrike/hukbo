namespace Sandata.Core.Collision;

/// <summary>
/// One entity's proposed move for a tick: where it started and where it would
/// like to end up. <see cref="GroupId"/> and <see cref="SlotIndex"/> are the
/// first two keys of the eventual squad-aware commit order described in
/// design section 8; groups do not exist yet, so every caller passes zero for
/// both today, which collapses the three-key order to plain ascending
/// <see cref="EntityId"/> without the comparator itself needing to change
/// later.
/// </summary>
internal readonly record struct SandataCollisionMoveRequest(
    ulong EntityId,
    int StartXRaw,
    int StartYRaw,
    int DesiredXRaw,
    int DesiredYRaw,
    ulong GroupId,
    int SlotIndex);

/// <summary>How a request's move was settled.</summary>
internal enum SandataMovementResolution
{
    /// <summary>The desired position committed exactly as proposed.</summary>
    Moved,

    /// <summary>The entity was not moving and its start position committed exactly as proposed.</summary>
    Held,

    /// <summary>
    /// The desired position would overlap another body, so the entity
    /// committed at its own start position instead, once that start
    /// position was itself confirmed clear. See the remarks on
    /// <see cref="SandataCollisionResolver.CommitOne"/> for what happens on
    /// the rare path where the start position is not clear either — that
    /// path never reports <see cref="Blocked"/>, it reports
    /// <see cref="Separated"/>, so every occurrence of this value is a
    /// position this method independently verified was free of every other
    /// body at commit time.
    /// </summary>
    Blocked,

    /// <summary>
    /// A fixed, deterministic ring search found this entity's committed
    /// position rather than either of its own proposed points. This
    /// happens in two situations, both documented on
    /// <see cref="SandataCollisionResolver.CommitOne"/>: the desired
    /// position exactly coincided with another body, or the desired
    /// position was rejected and the entity's own start position was, at
    /// that moment, also occupied by another body.
    /// </summary>
    Separated,
}

/// <summary>The settled outcome of one request, after every entity in the tick has committed.</summary>
internal readonly record struct SandataCollisionMoveResult(
    ulong EntityId,
    int CommittedXRaw,
    int CommittedYRaw,
    SandataMovementResolution Resolution);

/// <summary>
/// Sandata's own three-phase collision resolver: propose, prioritise, commit
/// sequentially. This is the reimplementation the design calls for in place
/// of sharing <c>Hukbo.Core.Simulation.CollisionResolver</c> — see the remarks
/// on <see cref="SandataCollisionGrid"/> for why.
/// </summary>
/// <remarks>
/// <para>
/// <b>Propose.</b> The caller builds the full list of
/// <see cref="SandataCollisionMoveRequest"/> values for the tick before
/// calling <see cref="Resolve"/>, one per living entity, each computed only
/// from the previous tick's committed positions. No request is built from
/// another request in the same list, so no entity's proposal can see another
/// entity's proposal — this resolver enforces nothing further for that
/// property; it is a contract on the caller, exactly as it is in the Hukbo
/// original.
/// </para>
/// <para>
/// <b>Prioritise.</b> The requests are sorted once into a total order — by
/// ascending <see cref="SandataCollisionMoveRequest.GroupId"/>, then ascending
/// <see cref="SandataCollisionMoveRequest.SlotIndex"/>, then ascending
/// <see cref="SandataCollisionMoveRequest.EntityId"/> — using a manual
/// insertion sort over an index array. Sandata's roster is small (an indoor
/// operator squad, not a battlefield), so an insertion sort's simplicity is
/// worth more than a faster algorithm's asymptotics, and it needs neither
/// <c>PriorityQueue&lt;</c> nor a delegate-allocating LINQ ordering.
/// </para>
/// <para>
/// <b>Commit, sequentially.</b> Walking the prioritised order, each request is
/// tested against the real, current position of every other body in the
/// tick — not only the ones already committed. Before every single request is
/// tested, <see cref="_committedGrid"/> is rebuilt from <see cref="_liveBodies"/>,
/// a table that starts as every request's own start position and is updated,
/// in place, to that request's settled committed position the instant it is
/// decided. This is what makes the grid a true "who is standing where right
/// now" index at every step: an entity that has not had its own turn yet is
/// still represented by its real start position, and an entity that already
/// committed is represented by its real committed position, never by a stale
/// value from before the tick began. A desired position that does not overlap
/// anything in that current index is accepted outright. A desired position
/// that exactly coincides with another body is repaired by
/// <see cref="TrySeparate"/>, walking the fixed direction order east, west,
/// north, south at one body diameter per step until a clear position is
/// found. Any other overlap falls through to the entity's own start position —
/// but only once that start position is itself confirmed clear against the
/// same current index; see the remarks on <see cref="CommitOne"/> for the
/// written rule covering the case where it is not.
/// </para>
/// <para>
/// Never a force, an impulse, or a push-apart: every accepted position is
/// tested exactly, and rejection falls back to holding still or to a fixed,
/// pre-declared repair direction — never to nudging a body by some fraction of
/// a computed vector, which is exactly the rigid-body shortcut
/// <c>CLAUDE.md</c> section 9 forbids.
/// </para>
/// <para>
/// The resolver owns one <see cref="SandataCollisionGrid"/> as its working
/// index and one <see cref="List{T}"/> of <see cref="SandataCollisionBody"/>
/// as <see cref="_liveBodies"/>, and reuses both, plus one scratch priority
/// index array, across calls, so a warm tick — one whose request count has
/// been seen before, so none of the three needs to grow — allocates nothing
/// beyond the result list the caller asked for. Rebuilding the grid ahead of
/// every request, rather than seeding it once and incrementally removing one
/// body per turn, costs this resolver O(n) grid work per request instead of
/// O(1): <see cref="SandataCollisionGrid"/> exposes no way to remove a single
/// body once inserted, only <see cref="SandataCollisionGrid.Clear"/> and
/// re-insertion of everything. That is an accepted trade against Sandata's
/// own stated scale — an indoor operator squad, not a battlefield roster —
/// the same trade this file already makes for its insertion sort over a
/// faster comparison-sort algorithm.
/// </para>
/// </remarks>
internal sealed class SandataCollisionResolver
{
    private readonly SandataCollisionGrid _committedGrid;
    private readonly int _bodyRadiusRaw;
    private readonly List<SandataCollisionMoveResult> _results = [];

    /// <summary>
    /// One entry per request in the current <see cref="Resolve"/> call, indexed
    /// the same way as the caller's own <c>requests</c> list (not by priority
    /// order). Every entry starts as that request's own start position and is
    /// overwritten, in place, with the request's settled committed position as
    /// soon as it is decided — which is this resolver's stand-in for "remove
    /// the body's stale entry and insert its real one" on a grid type that
    /// supports neither a targeted remove nor a targeted update. See the class
    /// remarks for why this table, rebuilt into <see cref="_committedGrid"/>
    /// ahead of every single request, replaces the seed-once-and-incrementally-
    /// remove shape a grid with a remove primitive would have supported.
    /// </summary>
    private readonly List<SandataCollisionBody> _liveBodies = [];

    private int[] _priorityOrder = new int[16];

    /// <param name="cellSizeRaw">The edge length of one square broad-phase cell in raw fixed-point units.</param>
    /// <param name="bodyRadiusRaw">The common body radius in raw fixed-point units.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="cellSizeRaw"/> is zero or negative, or
    /// <paramref name="bodyRadiusRaw"/> is negative or too large for the
    /// requested cell size.
    /// </exception>
    internal SandataCollisionResolver(int cellSizeRaw, int bodyRadiusRaw)
    {
        _committedGrid = new SandataCollisionGrid(cellSizeRaw);
        _committedGrid.ValidateRadiusForQueries(bodyRadiusRaw);
        _bodyRadiusRaw = bodyRadiusRaw;
    }

    /// <summary>
    /// Settles every request in <paramref name="requests"/> against the
    /// others, in commit-order, and returns one result per request in that
    /// same commit order.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="requests"/> is <see langword="null"/>.</exception>
    internal IReadOnlyList<SandataCollisionMoveResult> Resolve(IReadOnlyList<SandataCollisionMoveRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        _results.Clear();
        _liveBodies.Clear();

        var count = requests.Count;

        if (count == 0)
        {
            return _results;
        }

        EnsurePriorityOrderCapacity(count);

        for (var index = 0; index < count; index++)
        {
            _priorityOrder[index] = index;

            // Every entity's live position starts as its own start position:
            // this is the seed step. An entity nobody has reached yet keeps
            // this exact value for the rest of the call, so every later
            // request sees it as a real, occupied position — closing the
            // hole where the highest-priority request used to see an empty
            // grid.
            _liveBodies.Add(new SandataCollisionBody(requests[index].EntityId, requests[index].StartXRaw, requests[index].StartYRaw, IsAlive: true));
        }

        SortPriorityOrder(requests, count);

        for (var orderPosition = 0; orderPosition < count; orderPosition++)
        {
            var requestIndex = _priorityOrder[orderPosition];
            var request = requests[requestIndex];

            // Rebuild the working grid from the live table before testing
            // this request, so it is checked against everyone's real current
            // position: already-processed entities at their settled commit,
            // everyone else still at their real start.
            _committedGrid.Clear();

            for (var bodyIndex = 0; bodyIndex < count; bodyIndex++)
            {
                _committedGrid.Insert(_liveBodies[bodyIndex]);
            }

            var result = CommitOne(request);

            // This is the "remove" half of the seed-and-remove shape: the
            // request's own turn is done, so its live entry stops being its
            // start position and becomes its real, settled commit for every
            // request still to come.
            _liveBodies[requestIndex] = new SandataCollisionBody(result.EntityId, result.CommittedXRaw, result.CommittedYRaw, IsAlive: true);

            _results.Add(result);
        }

        return _results;
    }

    /// <summary>
    /// Decides one request's settled position against <see cref="_committedGrid"/>
    /// as it stands for this request's own turn — see <see cref="Resolve"/> for
    /// how that grid is kept current. Does not mutate the grid or
    /// <see cref="_liveBodies"/> itself; the caller commits the result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The written rule for an occupied start position.</b> The desired
    /// position is tried first, then, if it is rejected, the entity's own
    /// start position is tried as a fallback — but that fallback is only ever
    /// committed once this method has independently confirmed the start
    /// position is itself clear of every other body at this moment. An
    /// entity's start position can be occupied at fallback time even though
    /// it was that entity's own real position moments ago, because a
    /// higher-priority entity processed earlier in this same call can have
    /// committed onto it — see the class remarks on <see cref="_liveBodies"/>
    /// for exactly how that becomes visible here. When the start position is
    /// occupied, this method does not commit into it: doing so would recreate
    /// the exact overlap this resolver exists to prevent. Instead it repairs
    /// the position with the same fixed, deterministic ring search
    /// (<see cref="TrySeparate"/>) already used for an exact coincidence at
    /// the desired position, walking east, west, north, south outward from the
    /// entity's own start position this time rather than its desired one, and
    /// reports <see cref="SandataMovementResolution.Separated"/> rather than
    /// <see cref="SandataMovementResolution.Blocked"/>. If that search also
    /// exhausts its bounded ring count without finding daylight — the same
    /// "caller defect, not a case this method papers over" bound documented on
    /// <see cref="TrySeparate"/> itself — this method has no remaining
    /// non-overlapping candidate to offer and commits into the occupied start
    /// position as an absolute last resort, the one place in this resolver
    /// where the no-overlap invariant is not guaranteed. That last resort has
    /// never been observed to trigger against any fixture in this codebase;
    /// it exists so this method always returns a value rather than throwing
    /// out of an already-degenerate scene.
    /// </para>
    /// <para>
    /// A partial overlap at the desired position — one that is not an exact
    /// coincidence — never reaches <see cref="TrySeparate"/> at the desired
    /// position at all; it falls straight through to the start-position check
    /// above. That is sufficient for correctness, not an oversight: once the
    /// start-position check and its own separation fallback are both in
    /// place, a partial overlap can no longer produce an overlapping commit,
    /// because the entity either holds at its own already-clear start or is
    /// separated away from it.
    /// </para>
    /// </remarks>
    private SandataCollisionMoveResult CommitOne(in SandataCollisionMoveRequest request)
    {
        var isMoving = request.DesiredXRaw != request.StartXRaw || request.DesiredYRaw != request.StartYRaw;

        if (!_committedGrid.AnyOverlapUnchecked(request.DesiredXRaw, request.DesiredYRaw, _bodyRadiusRaw, request.EntityId))
        {
            var resolution = isMoving ? SandataMovementResolution.Moved : SandataMovementResolution.Held;
            return new SandataCollisionMoveResult(request.EntityId, request.DesiredXRaw, request.DesiredYRaw, resolution);
        }

        if (_committedGrid.AnyCoincidentUnchecked(request.DesiredXRaw, request.DesiredYRaw, request.EntityId) &&
            TrySeparate(request.DesiredXRaw, request.DesiredYRaw, request.EntityId, out var separatedFromDesiredXRaw, out var separatedFromDesiredYRaw))
        {
            return new SandataCollisionMoveResult(request.EntityId, separatedFromDesiredXRaw, separatedFromDesiredYRaw, SandataMovementResolution.Separated);
        }

        if (!_committedGrid.AnyOverlapUnchecked(request.StartXRaw, request.StartYRaw, _bodyRadiusRaw, request.EntityId))
        {
            return new SandataCollisionMoveResult(request.EntityId, request.StartXRaw, request.StartYRaw, SandataMovementResolution.Blocked);
        }

        if (TrySeparate(request.StartXRaw, request.StartYRaw, request.EntityId, out var separatedFromStartXRaw, out var separatedFromStartYRaw))
        {
            return new SandataCollisionMoveResult(request.EntityId, separatedFromStartXRaw, separatedFromStartYRaw, SandataMovementResolution.Separated);
        }

        // Absolute last resort: see the written rule in the remarks above.
        return new SandataCollisionMoveResult(request.EntityId, request.StartXRaw, request.StartYRaw, SandataMovementResolution.Blocked);
    }

    /// <summary>
    /// Walks <see cref="SandataCollisionGrid.SeparationDirections"/> — east,
    /// west, north, south, in that fixed order — at one body diameter per
    /// step, and returns the first offset position that overlaps nothing
    /// already committed. Every candidate at a given step count is tried
    /// before the step count grows, so the search always finds the nearest
    /// clear ring before a farther one.
    /// </summary>
    /// <remarks>
    /// Bounded at sixteen steps outward. A collision resolver operating on a
    /// handful of indoor operators has no legitimate reason to need more ring
    /// steps than that to find daylight; hitting the bound is a caller defect
    /// (for example, an unreasonably crowded scene), not a case this method
    /// tries to paper over.
    /// </remarks>
    private bool TrySeparate(int xRaw, int yRaw, ulong entityId, out int separatedXRaw, out int separatedYRaw)
    {
        var stepRaw = 2 * _bodyRadiusRaw;

        for (var ring = 1; ring <= 16; ring++)
        {
            foreach (var direction in SandataCollisionGrid.SeparationDirections)
            {
                var candidateXRaw = xRaw + (direction.X * stepRaw * ring);
                var candidateYRaw = yRaw + (direction.Y * stepRaw * ring);

                if (!_committedGrid.AnyOverlapUnchecked(candidateXRaw, candidateYRaw, _bodyRadiusRaw, entityId))
                {
                    separatedXRaw = candidateXRaw;
                    separatedYRaw = candidateYRaw;
                    return true;
                }
            }
        }

        separatedXRaw = xRaw;
        separatedYRaw = yRaw;
        return false;
    }

    /// <summary>
    /// Insertion sort over <see cref="_priorityOrder"/>, comparing the
    /// requests those indices point at by ascending
    /// (<see cref="SandataCollisionMoveRequest.GroupId"/>,
    /// <see cref="SandataCollisionMoveRequest.SlotIndex"/>,
    /// <see cref="SandataCollisionMoveRequest.EntityId"/>). Chosen over
    /// <see cref="Array.Sort{T}(T[])"/> with a delegate so the comparison
    /// stays a plain inline expression rather than a per-call allocation, and
    /// because Sandata's roster size makes an insertion sort's simplicity a
    /// better trade than a faster algorithm's asymptotics.
    /// </summary>
    private void SortPriorityOrder(IReadOnlyList<SandataCollisionMoveRequest> requests, int count)
    {
        for (var i = 1; i < count; i++)
        {
            var candidate = _priorityOrder[i];
            var candidateRequest = requests[candidate];
            var j = i - 1;

            while (j >= 0 && IsHigherPriority(candidateRequest, requests[_priorityOrder[j]]))
            {
                _priorityOrder[j + 1] = _priorityOrder[j];
                j--;
            }

            _priorityOrder[j + 1] = candidate;
        }
    }

    /// <summary>
    /// True when <paramref name="left"/> commits strictly before
    /// <paramref name="right"/> under the (GroupId, SlotIndex, EntityId) order.
    /// </summary>
    private static bool IsHigherPriority(in SandataCollisionMoveRequest left, in SandataCollisionMoveRequest right)
    {
        if (left.GroupId != right.GroupId)
        {
            return left.GroupId < right.GroupId;
        }

        if (left.SlotIndex != right.SlotIndex)
        {
            return left.SlotIndex < right.SlotIndex;
        }

        return left.EntityId < right.EntityId;
    }

    private void EnsurePriorityOrderCapacity(int requiredLength)
    {
        if (requiredLength <= _priorityOrder.Length)
        {
            return;
        }

        _priorityOrder = new int[Math.Max(requiredLength, _priorityOrder.Length * 2)];
    }
}
