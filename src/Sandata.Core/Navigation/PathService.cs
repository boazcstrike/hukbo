using System.Collections.Immutable;

namespace Sandata.Core.Navigation;

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
    private readonly List<GroupState> _groups = [];
    private readonly NavSearch _search = new();
    private readonly List<int> _scratchPathCells = [];
    private readonly List<int> _scratchExpandedCells = [];

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
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> is <see langword="null"/>.</exception>
    public void Advance(long currentTick, NavGrid grid, ReadOnlySpan<bool> blocked)
    {
        ArgumentNullException.ThrowIfNull(grid);

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

            Publish(group);
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
    /// <see cref="PathRequest.GoalCellIndex"/>, reproduces the identical
    /// polyline this service published from it.
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
    /// The group's current published path, as a sequence of nav grid cell
    /// indices from its start cell to its goal cell inclusive, oldest first.
    /// Empty when the group has never published a path, or when its most
    /// recently published search found the goal unreachable — see
    /// <see cref="GetReasonCode"/> to tell those two apart, and to tell
    /// either apart from a path that is merely still awaiting its latency.
    /// </summary>
    public ImmutableArray<int> GetCurrentPath(int groupId) => FindGroup(groupId)?.CurrentPath ?? ImmutableArray<int>.Empty;

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

        if (!group.CurrentPath.IsEmpty)
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

    private static void Publish(GroupState group)
    {
        group.CurrentPath = group.PendingOutcome == NavSearchOutcome.PathFound
            ? group.PendingPath
            : ImmutableArray<int>.Empty;

        group.HasOutstandingRequest = false;
        group.SearchCompleted = false;
        group.PendingPath = ImmutableArray<int>.Empty;
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

        public ImmutableArray<int> CurrentPath { get; set; } = ImmutableArray<int>.Empty;
    }
}
