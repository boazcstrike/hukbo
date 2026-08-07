namespace Sandata.Core.Navigation;

/// <summary>
/// One group's path request: the authoritative, snapshotted tuple design
/// section 4 names — "Per group: the destination record and the outstanding
/// path request <c>(groupId, startCellIndex, goalCellIndex, requestTick)</c>"
/// — and section 7's "Fixed-latency path amortisation" governs.
///
/// <para>
/// <b>This record is authoritative; the polyline it produces is not.</b>
/// Design section 4, "What is derived and never hashed": "A path is a pure
/// function of the nav data, the start cell, and the goal cell. The
/// <i>request</i> is authoritative and snapshotted; the <i>result</i> is not.
/// On resume, every outstanding and every published path is recomputed from
/// its stored request record before the first tick executes." A save file or
/// a snapshot carries values shaped like this record — never a resulting
/// cell-index list, never an A* scratch array.
/// </para>
///
/// <para>
/// <see cref="StartCellIndex"/> is fixed at the tick the request was made,
/// not re-read from the group's live position on resume. Design section 4 is
/// explicit about why: "That works only because the request stores the start
/// <i>cell index at request time</i>, not the position at resume time." A
/// resume that instead asked "where is this group right now" would recompute
/// a different path than the one that was actually in effect the tick before
/// the snapshot was taken, which is exactly the kind of drift a save/resume
/// equivalence test exists to catch.
/// </para>
/// </summary>
/// <param name="GroupId">
/// The requesting group's identity. Design section 8 derives group identity
/// as the minimum living entity id in the group's union-find component; this
/// record does not care how the caller derived it, only that it is stable
/// for the group's lifetime.
/// </param>
/// <param name="StartCellIndex">
/// The nav grid cell index the group was at when this request was made. Not
/// re-derived on resume — see the remarks above.
/// </param>
/// <param name="GoalCellIndex">The nav grid cell index the group is trying to reach.</param>
/// <param name="RequestTick">
/// The tick this request was made. The path becomes valid at exactly
/// <c>RequestTick + PathLatencyTicks</c>, regardless of how many searches the
/// machine actually completed in between.
/// </param>
public readonly record struct PathRequest(
    int GroupId,
    int StartCellIndex,
    int GoalCellIndex,
    long RequestTick);
