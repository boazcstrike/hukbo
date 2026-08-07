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
    /// The desired position would overlap an already-committed body, so the
    /// entity committed at its start position instead.
    /// </summary>
    Blocked,

    /// <summary>
    /// The entity's position exactly coincided with an already-committed
    /// body, so it committed at a deterministic offset from that position
    /// instead of either.
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
/// tested against every already-committed body this tick, never against
/// another still-pending proposal. A desired position that does not overlap
/// anything committed so far is accepted outright. A desired position that
/// exactly coincides with an already-committed body is repaired by
/// <see cref="TrySeparate"/>, walking the fixed direction order east, west,
/// north, south at one body diameter per step until a clear position is
/// found. Any other overlap holds the entity at its start position rather
/// than moving it into the collision.
/// </para>
/// <para>
/// Never a force, an impulse, or a push-apart: every accepted position is
/// tested exactly, and rejection falls back to holding still or to a fixed,
/// pre-declared repair direction — never to nudging a body by some fraction of
/// a computed vector, which is exactly the rigid-body shortcut
/// <c>CLAUDE.md</c> section 9 forbids.
/// </para>
/// <para>
/// The resolver owns one <see cref="SandataCollisionGrid"/> as its committed
/// index and clears it at the start of every <see cref="Resolve"/> call rather
/// than allocating a fresh one, and it reuses one scratch index array across
/// calls, so a warm tick allocates nothing beyond the result list the caller
/// asked for.
/// </para>
/// </remarks>
internal sealed class SandataCollisionResolver
{
    private readonly SandataCollisionGrid _committedGrid;
    private readonly int _bodyRadiusRaw;
    private readonly List<SandataCollisionMoveResult> _results = [];

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

        _committedGrid.Clear();
        _results.Clear();

        var count = requests.Count;

        if (count == 0)
        {
            return _results;
        }

        EnsurePriorityOrderCapacity(count);

        for (var index = 0; index < count; index++)
        {
            _priorityOrder[index] = index;
        }

        SortPriorityOrder(requests, count);

        for (var orderPosition = 0; orderPosition < count; orderPosition++)
        {
            var request = requests[_priorityOrder[orderPosition]];
            _results.Add(CommitOne(request));
        }

        return _results;
    }

    private SandataCollisionMoveResult CommitOne(in SandataCollisionMoveRequest request)
    {
        var isMoving = request.DesiredXRaw != request.StartXRaw || request.DesiredYRaw != request.StartYRaw;

        if (!_committedGrid.AnyOverlapUnchecked(request.DesiredXRaw, request.DesiredYRaw, _bodyRadiusRaw, request.EntityId))
        {
            var resolution = isMoving ? SandataMovementResolution.Moved : SandataMovementResolution.Held;
            return CommitAt(request.EntityId, request.DesiredXRaw, request.DesiredYRaw, resolution);
        }

        if (_committedGrid.AnyCoincidentUnchecked(request.DesiredXRaw, request.DesiredYRaw, request.EntityId) &&
            TrySeparate(request.DesiredXRaw, request.DesiredYRaw, request.EntityId, out var separatedXRaw, out var separatedYRaw))
        {
            return CommitAt(request.EntityId, separatedXRaw, separatedYRaw, SandataMovementResolution.Separated);
        }

        return CommitAt(request.EntityId, request.StartXRaw, request.StartYRaw, SandataMovementResolution.Blocked);
    }

    private SandataCollisionMoveResult CommitAt(ulong entityId, int xRaw, int yRaw, SandataMovementResolution resolution)
    {
        _committedGrid.Insert(new SandataCollisionBody(entityId, xRaw, yRaw, IsAlive: true));
        return new SandataCollisionMoveResult(entityId, xRaw, yRaw, resolution);
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
