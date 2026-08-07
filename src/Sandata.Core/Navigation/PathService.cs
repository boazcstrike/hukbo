using System.Collections.Immutable;

namespace Sandata.Core.Navigation;

/// <summary>
/// One vertex of a line-of-sight-smoothed path polyline, in raw world units —
/// the same unit <see cref="PathSmoothing.Smooth"/> writes and
/// <see cref="NavGrid"/> measures cells in. Not a <c>FixedPoint</c> value:
/// the nav grid and the smoothing pass both work in plain integer world
/// units, matching <see cref="NavGrid.CellSizeWu"/>, so this type carries the
/// coordinate pair verbatim rather than re-scaling it.
/// </summary>
/// <param name="X">The vertex's X coordinate, in world units.</param>
/// <param name="Y">The vertex's Y coordinate, in world units.</param>
public readonly record struct PathPoint(long X, long Y);

/// <summary>
/// Owns every group's autonomous path request and published path, under
/// design section 7's fixed-latency amortisation rule and section 5 stage 7:
/// "Path service: publish paths whose latency elapsed, enqueue new requests,
/// run at most one A* per group." One instance belongs to one mission; this
/// type is not a singleton and holds no static state, because a later
/// milestone adds an order layer that supplies authored, player-drawn
/// polylines from outside this service, and this service must keep owning
/// autonomous group paths alongside that without either one reaching into a
/// shared global.
///
/// <para>
/// <b>Two shapes are published, and they are not the same thing.</b>
/// <see cref="GetCurrentPath"/> returns the line-of-sight-smoothed polyline —
/// design section 7's 2026-08-07 amendment, "greedy line-of-sight smoothing,
/// over the corridor, using the wall bucket index this design already
/// builds" — which is what a mover should actually walk. Its predecessor,
/// <see cref="Funnel.StringPull"/>, is still ported and tested but is not on
/// this publish path: a grid A* corridor is a chain of single-cell-wide
/// portals, and the funnel cannot straighten what such a narrow portal never
/// gave it room to straighten, per the amendment's measurement.
/// <see cref="GetCurrentCorridor"/> returns the raw ordered nav grid
/// cell-index corridor <see cref="NavSearch.TryFindPath"/> found before
/// smoothing touched it, for a caller that genuinely wants cells rather than
/// a walkable line. Both are derived, never hashed, never snapshotted —
/// design section 4's "Published path polylines and their cumulative
/// arclengths" under "What is derived and never hashed" — and both are
/// recomputed from the same authoritative <see cref="PathRequest"/> on every
/// resume.
/// </para>
///
/// <para>
/// <b>The rule this type exists to enforce, verbatim from design section 7:</b>
/// "A path requested at tick <c>t</c> becomes valid at tick
/// <c>t + PathLatencyTicks</c>, regardless of how many searches the machine
/// actually completed. [...] Until a path is valid, the group's units hold
/// their current intent. There is no 'no path yet, move directly at the
/// goal' fallback, because that is precisely the branch that would make the
/// simulation depend on scheduling." This type never offers such a fallback:
/// <see cref="GetCurrentPath"/> returns an empty path, and
/// <see cref="GetReasonCode"/> reports why, for exactly as long as that is
/// true.
/// </para>
///
/// <para>
/// <b>Search timing versus publish timing are deliberately decoupled.</b>
/// <see cref="Advance"/> runs a group's one allowed A* search the first time
/// it is called after that group's request is issued — "the search itself
/// may execute on any tick in that window" — but the resulting path is not
/// copied into <see cref="GetCurrentPath"/> until <paramref name="currentTick"/>
/// reaches the request's publish tick. Nothing here places a limit on how
/// many groups may search on the same call: design section 7 is explicit
/// that the amortisation is "never a per-tick budget", because a budget
/// would make one group's arrival time depend on how many other groups
/// happened to request a path the same tick.
/// </para>
///
/// <para>
/// <b>Per-group state lives in a flat, linearly searched list, never a
/// <c>Dictionary</c>.</b> Group counts in v0.1 are small — one entry per
/// squad, not per operator — so a linear scan by <c>GroupId</c> costs
/// nothing that matters, and it keeps this type on the same footing as
/// every other Sandata.Core collection the source-hygiene test enforces.
/// </para>
/// </summary>
public sealed class PathService
{
    /// <summary>
    /// Starting capacity for <see cref="_scratchSmoothedX"/> and
    /// <see cref="_scratchSmoothedY"/>, grown on demand and never shrunk —
    /// see <see cref="SmoothCorridor"/>.
    /// </summary>
    private const int InitialSmoothedCapacity = 16;

    private readonly List<GroupState> _groups = [];
    private readonly NavSearch _search = new();
    private readonly List<int> _scratchPathCells = [];
    private readonly List<int> _scratchExpandedCells = [];
    private long[] _scratchSmoothedX = new long[InitialSmoothedCapacity];
    private long[] _scratchSmoothedY = new long[InitialSmoothedCapacity];

    /// <summary>
    /// Creates a path service that publishes a group's path exactly
    /// <paramref name="pathLatencyTicks"/> ticks after that group's request
    /// was issued, regardless of when the underlying search actually
    /// completes. This value is a ruleset constant in the caller's
    /// possession — <see cref="PathService"/> does not read
    /// <c>SandataRuleset</c> itself, so it stays usable in a test or a tool
    /// that has no ruleset to hand.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pathLatencyTicks"/> is negative.</exception>
    public PathService(int pathLatencyTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pathLatencyTicks);
        PathLatencyTicks = pathLatencyTicks;
    }

    /// <summary>The fixed number of ticks between a request and that request's path becoming valid.</summary>
    public int PathLatencyTicks { get; }

    /// <summary>
    /// Records a new destination for <paramref name="groupId"/>, unless that
    /// group already has an outstanding request — design section 7: "a group
    /// with an outstanding request does not enqueue a second one." The
    /// existing request, and the path it will eventually publish, is left
    /// completely untouched by a call this method ignores; the caller is not
    /// told whether its request was accepted or ignored, because nothing
    /// meaningful changes about the group either way — its path is still on
    /// its way, on schedule.
    /// </summary>
    /// <param name="groupId">The requesting group's identity, per design section 8.</param>
    /// <param name="startCellIndex">
    /// The nav grid cell the group occupies right now. Stored verbatim on the
    /// request and never re-derived later — see <see cref="PathRequest"/>'s
    /// remarks on why the start cell is fixed at request time.
    /// </param>
    /// <param name="goalCellIndex">The nav grid cell the group is trying to reach.</param>
    /// <param name="requestTick">
    /// The current tick. The resulting path, if any, becomes valid at
    /// <c>requestTick + PathLatencyTicks</c>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="requestTick"/> is negative.</exception>
    public void RequestPath(int groupId, int startCellIndex, int goalCellIndex, long requestTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requestTick);

        var group = FindOrCreateGroup(groupId);
        if (group.HasOutstandingRequest)
        {
            return;
        }

        group.Request = new PathRequest(groupId, startCellIndex, goalCellIndex, requestTick);
        group.HasOutstandingRequest = true;
        group.SearchCompleted = false;
        group.PendingOutcome = NavSearchOutcome.Unreachable;
        group.PendingPath = ImmutableArray<int>.Empty;
    }

    /// <summary>
    /// Runs one tick's worth of path-service work, per design section 5 stage
    /// 7: every group with an outstanding request whose search has not yet
    /// run gets exactly one A* search against <paramref name="grid"/> and
    /// <paramref name="blocked"/>, and every group whose request's fixed
    /// latency has elapsed as of <paramref name="currentTick"/> has its
    /// result published — a found path becomes the group's current path, and
    /// an unreachable result clears it, so the group holds position under an
    /// inspectable <see cref="PathReasonCode.Unreachable"/> rather than
    /// continuing toward a goal that was never reachable in the first place.
    /// </summary>
    /// <param name="currentTick">The tick this call executes as. Stage 7 reads the frozen tick-start view, per design section 5.</param>
    /// <param name="grid">The nav grid to search. Not retained past this call.</param>
    /// <param name="blocked">
    /// One entry per cell in <paramref name="grid"/>, matching
    /// <see cref="NavSearch.TryFindPath"/>'s own contract. Not retained past
    /// this call.
    /// </param>
    /// <param name="wallBuckets">
    /// The wall segment index a published path's line-of-sight smoothing
    /// queries against, per <see cref="PathSmoothing.Smooth"/>. Not retained
    /// past this call.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> or <paramref name="wallBuckets"/> is <see langword="null"/>.</exception>
    public void Advance(long currentTick, NavGrid grid, ReadOnlySpan<bool> blocked, WallBuckets wallBuckets)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(wallBuckets);

        foreach (var group in _groups)
        {
            if (!group.HasOutstandingRequest)
            {
                continue;
            }

            if (!group.SearchCompleted)
            {
                RunSearch(group, grid, blocked);
            }

            var publishTick = checked(group.Request.RequestTick + PathLatencyTicks);
            if (currentTick < publishTick)
            {
                continue;
            }

            Publish(group, grid, wallBuckets);
        }
    }

    /// <summary>
    /// Whether <paramref name="groupId"/> currently has a request whose path
    /// has not yet been published — either its search has not run, or it has
    /// run but the fixed latency has not yet elapsed.
    /// </summary>
    public bool HasOutstandingRequest(int groupId) => FindGroup(groupId)?.HasOutstandingRequest ?? false;

    /// <summary>
    /// Retrieves the authoritative request last accepted for
    /// <paramref name="groupId"/> — the tuple design section 4 stores in the
    /// snapshot — whether or not it has been published yet. This is exactly
    /// what a save/resume recomputes a path from: rebuilding the nav grid,
    /// then calling <see cref="NavSearch.TryFindPath"/> again with this
    /// request's <see cref="PathRequest.StartCellIndex"/> and
    /// <see cref="PathRequest.GoalCellIndex"/> and running
    /// <see cref="PathSmoothing.Smooth"/> over the resulting corridor,
    /// reproduces the identical smoothed polyline this service published
    /// from it.
    /// </summary>
    /// <returns><see langword="false"/> if <paramref name="groupId"/> has never made a request.</returns>
    public bool TryGetRequest(int groupId, out PathRequest request)
    {
        var group = FindGroup(groupId);
        if (group is null)
        {
            request = default;
            return false;
        }

        request = group.Request;
        return true;
    }

    /// <summary>
    /// The group's current published path, as a line-of-sight-smoothed
    /// polyline of world-unit vertices from its start position to its goal
    /// position inclusive, oldest first — the shape a mover should actually
    /// walk, per design section 7's 2026-08-07 amendment. Empty when the
    /// group has never published a path, or when its most recently published
    /// search found the goal unreachable — see <see cref="GetReasonCode"/> to
    /// tell those two apart, and to tell either apart from a path that is
    /// merely still awaiting its latency. See <see cref="GetCurrentCorridor"/>
    /// for the raw cell sequence this polyline was smoothed from.
    /// </summary>
    public ImmutableArray<PathPoint> GetCurrentPath(int groupId) => FindGroup(groupId)?.CurrentPath ?? ImmutableArray<PathPoint>.Empty;

    /// <summary>
    /// The group's current published path as the raw ordered sequence of nav
    /// grid cell indices <see cref="NavSearch.TryFindPath"/> found, from its
    /// start cell to its goal cell inclusive, before line-of-sight smoothing
    /// turned it into <see cref="GetCurrentPath"/>'s polyline. Kept available
    /// for a caller that genuinely wants cells rather than a walkable line.
    /// Empty and non-empty exactly when <see cref="GetCurrentPath"/> is —
    /// both are published together by <see cref="Publish"/> from the same
    /// search result.
    /// </summary>
    public ImmutableArray<int> GetCurrentCorridor(int groupId) => FindGroup(groupId)?.CurrentCorridor ?? ImmutableArray<int>.Empty;

    /// <summary>
    /// Why <see cref="GetCurrentPath"/> currently returns what it returns,
    /// for the operator inspector and for tests. See
    /// <see cref="PathReasonCode"/>'s own members for the exact rule each one
    /// follows.
    /// </summary>
    public PathReasonCode GetReasonCode(int groupId)
    {
        var group = FindGroup(groupId);
        if (group is null)
        {
            return PathReasonCode.NoDestinationRequested;
        }

        if (!group.CurrentCorridor.IsEmpty)
        {
            return PathReasonCode.PathValid;
        }

        return group.HasOutstandingRequest
            ? PathReasonCode.AwaitingLatency
            : PathReasonCode.Unreachable;
    }

    private void RunSearch(GroupState group, NavGrid grid, ReadOnlySpan<bool> blocked)
    {
        group.PendingOutcome = _search.TryFindPath(
            grid,
            group.Request.StartCellIndex,
            group.Request.GoalCellIndex,
            blocked,
            _scratchPathCells,
            _scratchExpandedCells);

        group.PendingPath = group.PendingOutcome == NavSearchOutcome.PathFound
            ? _scratchPathCells.ToImmutableArray()
            : ImmutableArray<int>.Empty;

        group.SearchCompleted = true;
    }

    /// <summary>
    /// Publishes <paramref name="group"/>'s search result — a found path
    /// becomes both <see cref="GroupState.CurrentCorridor"/> (the raw cells)
    /// and, line-of-sight-smoothed via <see cref="SmoothCorridor"/>,
    /// <see cref="GroupState.CurrentPath"/> (the polyline
    /// <see cref="GetCurrentPath"/> returns); an unreachable result clears
    /// both. Instance rather than static because smoothing reuses this
    /// service's own scratch buffers.
    /// </summary>
    private void Publish(GroupState group, NavGrid grid, WallBuckets wallBuckets)
    {
        if (group.PendingOutcome == NavSearchOutcome.PathFound)
        {
            group.CurrentCorridor = group.PendingPath;
            group.CurrentPath = SmoothCorridor(group.PendingPath, grid, wallBuckets);
        }
        else
        {
            group.CurrentCorridor = ImmutableArray<int>.Empty;
            group.CurrentPath = ImmutableArray<PathPoint>.Empty;
        }

        group.HasOutstandingRequest = false;
        group.SearchCompleted = false;
        group.PendingPath = ImmutableArray<int>.Empty;
    }

    /// <summary>
    /// Runs <see cref="PathSmoothing.Smooth"/> over <paramref name="corridor"/>,
    /// growing this service's reused <see cref="_scratchSmoothedX"/> and
    /// <see cref="_scratchSmoothedY"/> arrays on demand — never shrinking
    /// them, and never reallocating on a call a previous, larger corridor
    /// already sized them for — rather than allocating a fresh output pair
    /// every publish, matching how <see cref="_scratchPathCells"/> and
    /// <see cref="_scratchExpandedCells"/> are already reused across calls.
    /// </summary>
    /// <remarks>
    /// The requested capacity is <c>corridor.Length</c> exactly:
    /// <see cref="PathSmoothing.Smooth"/>'s own remarks prove its output can
    /// never hold more points than the corridor holds cells, so this never
    /// under-sizes the call and never allocates more than the true worst
    /// case requires.
    /// </remarks>
    private ImmutableArray<PathPoint> SmoothCorridor(ImmutableArray<int> corridor, NavGrid grid, WallBuckets wallBuckets)
    {
        var capacity = corridor.Length;
        if (_scratchSmoothedX.Length < capacity)
        {
            _scratchSmoothedX = new long[capacity];
            _scratchSmoothedY = new long[capacity];
        }

        var outputX = _scratchSmoothedX.AsSpan(0, capacity);
        var outputY = _scratchSmoothedY.AsSpan(0, capacity);
        var count = PathSmoothing.Smooth(corridor.AsSpan(), outputX, outputY, grid, wallBuckets);

        var builder = ImmutableArray.CreateBuilder<PathPoint>(count);
        for (var i = 0; i < count; i++)
        {
            builder.Add(new PathPoint(outputX[i], outputY[i]));
        }

        return builder.MoveToImmutable();
    }

    private GroupState? FindGroup(int groupId)
    {
        foreach (var group in _groups)
        {
            if (group.GroupId == groupId)
            {
                return group;
            }
        }

        return null;
    }

    private GroupState FindOrCreateGroup(int groupId)
    {
        var existing = FindGroup(groupId);
        if (existing is not null)
        {
            return existing;
        }

        // Kept sorted ascending by GroupId, matching MissionState.Groups'
        // documented order, so an enumeration in a future debug or inspector
        // surface is stable without that surface needing to sort it itself.
        var created = new GroupState(groupId);
        var insertAt = _groups.Count;
        for (var i = 0; i < _groups.Count; i++)
        {
            if (_groups[i].GroupId > groupId)
            {
                insertAt = i;
                break;
            }
        }

        _groups.Insert(insertAt, created);
        return created;
    }

    /// <summary>
    /// One group's mutable path-service bookkeeping. Private and
    /// reference-typed on purpose: every field below is scratch this service
    /// owns and mutates in place, never authoritative simulation state in its
    /// own right — the one field a caller can treat as authoritative is
    /// <see cref="Request"/>, exposed read-only through
    /// <see cref="PathService.TryGetRequest"/>.
    /// </summary>
    private sealed class GroupState(int groupId)
    {
        public int GroupId { get; } = groupId;

        public PathRequest Request { get; set; }

        public bool HasOutstandingRequest { get; set; }

        public bool SearchCompleted { get; set; }

        public NavSearchOutcome PendingOutcome { get; set; } = NavSearchOutcome.Unreachable;

        public ImmutableArray<int> PendingPath { get; set; } = ImmutableArray<int>.Empty;

        /// <summary>The raw cell corridor last published — see <see cref="PathService.GetCurrentCorridor"/>.</summary>
        public ImmutableArray<int> CurrentCorridor { get; set; } = ImmutableArray<int>.Empty;

        /// <summary>The funnel-smoothed polyline last published — see <see cref="PathService.GetCurrentPath"/>.</summary>
        public ImmutableArray<PathPoint> CurrentPath { get; set; } = ImmutableArray<PathPoint>.Empty;
    }
}
