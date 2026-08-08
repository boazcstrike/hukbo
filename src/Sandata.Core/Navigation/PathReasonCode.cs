namespace Sandata.Core.Navigation;

/// <summary>
/// Explains, for one group, why <see cref="PathService.GetCurrentPath"/>
/// currently returns what it returns. Design section 7, "Fixed-latency path
/// amortisation": "A group with no valid path and no current path holds
/// position and emits an inspectable reason code, which is what
/// <c>SIMULATION-GAME-STANDARDS.md</c> section 10 question 8 requires" — can
/// a spectator discover this effect without reading source code. This enum is
/// that inspectable value: the operator inspector (a later client task) shows
/// it directly rather than a spectator ever having to guess why a squad
/// stopped moving.
///
/// <para>
/// <b>Append-only</b> for the same reason every other Sandata enum is:
/// nothing in v0.1 folds this value into a hash, but a later task's
/// inspector or golden fixture may pin a specific member's numeric value, and
/// reordering would silently invalidate it.
/// </para>
/// </summary>
public enum PathReasonCode
{
    /// <summary>
    /// No destination has ever been requested for this group. There is
    /// nothing to hold a reason against — the group has simply never asked
    /// <see cref="PathService"/> for anywhere to go.
    /// </summary>
    NoDestinationRequested = 0,

    /// <summary>
    /// A path exists and <see cref="PathService.GetCurrentPath"/> returns it.
    /// A new request may also be outstanding underneath this one — the old
    /// path keeps being reported, and keeps guiding movement, until the new
    /// one's fixed latency elapses and it is published in its place. This is
    /// the amortisation itself: a group in the middle of re-routing is never
    /// without a path just because a better one is on its way.
    /// </summary>
    PathValid = 1,

    /// <summary>
    /// A request is outstanding and its fixed latency has not yet elapsed,
    /// and the group has no earlier published path to fall back on while it
    /// waits. There is deliberately no "move at the goal directly" fallback
    /// here — the group holds its current position instead.
    /// </summary>
    AwaitingLatency = 2,

    /// <summary>
    /// The most recent request to resolve found no route at all between its
    /// start and goal cells. The group holds its current position; there is
    /// no path to report and no fallback movement toward the unreachable
    /// goal.
    /// </summary>
    Unreachable = 3,
}
