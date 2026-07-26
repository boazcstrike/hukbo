namespace Hukbo.Core.Simulation;

/// <summary>
/// Aggregate collision counters for one completed headless run, as required by
/// the collision policy decision record, section 10 "Reported metrics".
/// </summary>
/// <remarks>
/// <para>
/// These counters are <b>derived</b> observability data. They are never hashed,
/// never snapshotted, and never persisted, so they cannot influence a simulation
/// outcome. They supplement the per-agent
/// <see cref="MovementResolution"/> explanation shown to a spectator; they do
/// not replace it.
/// </para>
/// <para>
/// Two same-seed runs of the same build must produce identical values in every
/// field.
/// </para>
/// <para>
/// <b>No penetration percentiles are reported, deliberately.</b> Under the
/// approved <see cref="CollisionPolicy.Solid"/> contract, penetration between
/// two living bodies is identically zero at the end of every tick, so a p50/p95
/// histogram would be a column of zeros carrying no information. If a future
/// decision record introduces a soft contact policy, percentiles become
/// meaningful and may be added then — not before.
/// </para>
/// </remarks>
/// <param name="CandidatePairs">
/// Total number of broad-phase candidate pairs generated across the run,
/// summed over ticks.
/// </param>
/// <param name="ContactPairs">
/// Total number of candidate pairs that were actually in contact across the
/// run, summed over ticks.
/// </param>
/// <param name="AcceptedMoves">
/// Total number of movement proposals that resolved to an accepted destination
/// across the run, summed over ticks.
/// </param>
/// <param name="BlockedAgentTicks">
/// Total agent-ticks spent blocked: one unit per agent per tick that resolved
/// to <see cref="MovementResolution.Blocked"/>. This is an agent-tick count,
/// not a count of distinct agents.
/// </param>
/// <param name="AttackCapableAgentTicks">
/// Total agent-ticks in which an agent had a target inside attack reach. This
/// is an agent-tick count, not a count of distinct agents. Compared against
/// <see cref="BlockedAgentTicks"/> it shows that being blocked does not remove
/// an agent from combat.
/// </param>
/// <param name="LongestBlockedStreakTicks">
/// The longest run of consecutive ticks any single agent spent blocked. A
/// running maximum, not a sum.
/// </param>
/// <param name="MaximumFrontWidthRaw">
/// The widest contact front observed in any tick, in raw fixed-point units. A
/// running maximum, not a sum.
/// </param>
/// <param name="MaximumFrontDepthRaw">
/// The deepest contact front observed in any tick, in raw fixed-point units. A
/// running maximum, not a sum.
/// </param>
/// <param name="MaximumPenetrationRaw">
/// The deepest overlap between two living bodies observed at the end of any
/// tick, in raw fixed-point units. A running maximum, not a sum.
/// <b>This is a guard metric, not a tuning signal.</b> The approved policy is
/// <see cref="CollisionPolicy.Solid"/>, under which penetration is never
/// permitted in any amount, so a correct run reports exactly <c>0</c>. Any
/// nonzero value is a contract violation and must be treated as a defect in the
/// collision resolver.
/// </param>
public readonly record struct CollisionMetrics(
    long CandidatePairs,
    long ContactPairs,
    long AcceptedMoves,
    long BlockedAgentTicks,
    long AttackCapableAgentTicks,
    int LongestBlockedStreakTicks,
    int MaximumFrontWidthRaw,
    int MaximumFrontDepthRaw,
    int MaximumPenetrationRaw);

/// <summary>
/// Accumulates per-tick collision counts into the run aggregate reported as a
/// <see cref="CollisionMetrics"/>.
/// </summary>
/// <remarks>
/// <para>
/// A mutable struct so that a caller can hold it as a field and feed it once per
/// tick without allocating. Running totals are held as <see cref="long"/> and
/// added in a <c>checked</c> context, so a run long enough to overflow fails
/// loudly instead of silently reporting a wrapped count.
/// </para>
/// <para>
/// A default-constructed accumulator is the valid empty state, and
/// <see cref="Reset"/> returns an accumulator to exactly that state.
/// </para>
/// </remarks>
internal struct CollisionMetricsAccumulator
{
    private long _candidatePairs;
    private long _contactPairs;
    private long _acceptedMoves;
    private long _blockedAgentTicks;
    private long _attackCapableAgentTicks;
    private int _longestBlockedStreakTicks;
    private int _maximumFrontWidthRaw;
    private int _maximumFrontDepthRaw;
    private int _maximumPenetrationRaw;

    /// <summary>
    /// Returns the accumulator to the state of a freshly constructed one, so a
    /// reused instance cannot leak counts from a previous run.
    /// </summary>
    internal void Reset() => this = default;

    /// <summary>
    /// Folds one tick's collision counts into the run aggregate.
    /// </summary>
    /// <param name="candidatePairs">
    /// Broad-phase candidate pairs generated this tick.
    /// </param>
    /// <param name="contactPairs">
    /// Candidate pairs that were actually in contact this tick.
    /// </param>
    /// <param name="acceptedMoves">
    /// Movement proposals that resolved to an accepted destination this tick.
    /// </param>
    /// <param name="blockedAgents">
    /// Agents that resolved to <see cref="MovementResolution.Blocked"/> this
    /// tick.
    /// </param>
    /// <param name="attackCapableAgents">
    /// Agents with a target inside attack reach this tick.
    /// </param>
    /// <param name="frontWidthRaw">
    /// Contact front width this tick, in raw fixed-point units. Kept as a
    /// running maximum.
    /// </param>
    /// <param name="frontDepthRaw">
    /// Contact front depth this tick, in raw fixed-point units. Kept as a
    /// running maximum.
    /// </param>
    /// <param name="penetrationRaw">
    /// Deepest living-body overlap at the end of this tick, in raw fixed-point
    /// units. Kept as a running maximum, and expected to be <c>0</c> on every
    /// tick under the approved <see cref="CollisionPolicy.Solid"/> contract.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any argument is negative. Every input is a count or a nonnegative
    /// distance, so a negative value is a caller defect. The accumulator is left
    /// unchanged when an argument is rejected.
    /// </exception>
    /// <exception cref="OverflowException">
    /// A running total would exceed <see cref="long.MaxValue"/>.
    /// </exception>
    internal void AddTick(
        int candidatePairs,
        int contactPairs,
        int acceptedMoves,
        int blockedAgents,
        int attackCapableAgents,
        int frontWidthRaw,
        int frontDepthRaw,
        int penetrationRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(candidatePairs);
        ArgumentOutOfRangeException.ThrowIfNegative(contactPairs);
        ArgumentOutOfRangeException.ThrowIfNegative(acceptedMoves);
        ArgumentOutOfRangeException.ThrowIfNegative(blockedAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(attackCapableAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(frontWidthRaw);
        ArgumentOutOfRangeException.ThrowIfNegative(frontDepthRaw);
        ArgumentOutOfRangeException.ThrowIfNegative(penetrationRaw);

        checked
        {
            _candidatePairs += candidatePairs;
            _contactPairs += contactPairs;
            _acceptedMoves += acceptedMoves;
            _blockedAgentTicks += blockedAgents;
            _attackCapableAgentTicks += attackCapableAgents;
        }

        _maximumFrontWidthRaw = Math.Max(_maximumFrontWidthRaw, frontWidthRaw);
        _maximumFrontDepthRaw = Math.Max(_maximumFrontDepthRaw, frontDepthRaw);
        _maximumPenetrationRaw = Math.Max(_maximumPenetrationRaw, penetrationRaw);
    }

    /// <summary>
    /// Offers one agent's completed blocked streak, keeping the longest seen.
    /// </summary>
    /// <param name="streakTicks">
    /// Length of the streak in ticks. Zero is legal and is a no-op.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="streakTicks"/> is negative.
    /// </exception>
    internal void ObserveBlockedStreak(int streakTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(streakTicks);

        _longestBlockedStreakTicks = Math.Max(_longestBlockedStreakTicks, streakTicks);
    }

    /// <summary>
    /// Projects the accumulated counts into the reported value. Reading does not
    /// consume the accumulator, so it may be called any number of times.
    /// </summary>
    internal readonly CollisionMetrics ToMetrics() =>
        new(
            _candidatePairs,
            _contactPairs,
            _acceptedMoves,
            _blockedAgentTicks,
            _attackCapableAgentTicks,
            _longestBlockedStreakTicks,
            _maximumFrontWidthRaw,
            _maximumFrontDepthRaw,
            _maximumPenetrationRaw);
}
