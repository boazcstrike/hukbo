namespace Hukbo.Core.Simulation;

/// <summary>
/// Aggregate weapon-movement behaviour counters for one completed headless
/// run, as required by the weapon-relative movement design, section 16
/// "Derived observability".
/// </summary>
/// <remarks>
/// <para>
/// These counters are <b>derived</b> observability data, modelled directly on
/// <see cref="CollisionMetrics"/>. They are never hashed, never snapshotted,
/// and never persisted, so they cannot influence a simulation outcome. All
/// but <see cref="ConflictDenials"/> are reconstructed outside the simulation
/// by comparing the current tick's <see cref="AgentView"/>s against the
/// previous tick's; the denial counter is the one value a view cannot show —
/// a denied agent is indistinguishable from a blocked one — so the simulation
/// accumulates it in the same observability-only manner as its collision
/// counters.
/// </para>
/// <para>
/// Two same-seed runs of the same build must produce identical values in
/// every field. Every field is identically zero under a movement preset
/// whose <see cref="Movement.MovementRuleset.UsesEquipmentRelativeFootwork"/>
/// is <see langword="false"/>, which never resolves a posture, phase, or
/// facing at all.
/// </para>
/// </remarks>
/// <param name="ApproachAgentTicks">
/// Total agent-ticks living warriors spent in
/// <see cref="Movement.FootworkPhase.Approach"/>. An agent-tick count, not a
/// count of distinct agents; likewise for every other phase field below.
/// </param>
/// <param name="EngageAgentTicks">
/// Total agent-ticks spent in <see cref="Movement.FootworkPhase.Engage"/>.
/// </param>
/// <param name="CommitAgentTicks">
/// Total agent-ticks spent in <see cref="Movement.FootworkPhase.Commit"/>.
/// </param>
/// <param name="RecoverAgentTicks">
/// Total agent-ticks spent in <see cref="Movement.FootworkPhase.Recover"/>.
/// </param>
/// <param name="RefuseAgentTicks">
/// Total agent-ticks spent in <see cref="Movement.FootworkPhase.Refuse"/>.
/// The phase is re-resolved every tick, so each unit is one route-refusal
/// finalisation: a provisional phase whose every route candidate failed lane
/// clearance that tick.
/// </param>
/// <param name="DisengageAgentTicks">
/// Total agent-ticks spent in <see cref="Movement.FootworkPhase.Disengage"/>.
/// </param>
/// <param name="RegroupAgentTicks">
/// Total agent-ticks spent in <see cref="Movement.FootworkPhase.Regroup"/>.
/// </param>
/// <param name="PursueAgentTicks">
/// Total agent-ticks spent in <see cref="Movement.FootworkPhase.Pursue"/>.
/// </param>
/// <param name="PostureTransitions">
/// Total number of ticks on which a living warrior's
/// <see cref="Movement.TacticalPosture"/> differed from its own posture one
/// tick earlier, summed across warriors and ticks.
/// </param>
/// <param name="FacingStepsTurned">
/// Total 22.5-degree facing sectors living warriors turned through, summed
/// across warriors and ticks: each tick contributes the circular separation
/// between the warrior's previous and current <see cref="Movement.Facing16"/>.
/// </param>
/// <param name="DisengagementEntries">
/// Total number of ticks on which a living warrior entered
/// <see cref="Movement.FootworkPhase.Disengage"/> from any other phase. An
/// entry count, so an unbroken disengagement spell counts once here however
/// many agent-ticks it adds to <see cref="DisengageAgentTicks"/>.
/// </param>
/// <param name="ConflictDenials">
/// Total number of movement proposals the friendly-clearance conflict pass
/// (weapon-relative movement design, section 10.6) rejected across the run,
/// read from the simulation's own observability counter.
/// </param>
/// <param name="RouteRefusalNoCandidatesBuilt">
/// Total number of <c>TryProposeEquipmentRoute</c> calls that finalised
/// <see cref="Movement.FootworkPhase.Refuse"/> because
/// <c>BuildEquipmentRouteCandidates</c> produced zero candidates for the
/// tick — the provisional phase had no threat, no facing, or no non-zero
/// delta to route toward. One of the four rejection-reason counters
/// decomposing <see cref="RefuseAgentTicks"/>; like
/// <see cref="ConflictDenials"/>, a reason a view cannot show, so it is read
/// from the simulation's own observability counter rather than reconstructed
/// from consecutive views.
/// </param>
/// <param name="RouteRefusalStepEndpointRejected">
/// Total number of <c>TryProposeEquipmentRoute</c> calls whose last
/// attempted candidate was rejected because
/// <c>MovementRouteRules.StepEndpoint</c> found no legal step for it. One of
/// the four rejection-reason counters decomposing
/// <see cref="RefuseAgentTicks"/>.
/// </param>
/// <param name="RouteRefusalDirectCandidateOmitted">
/// Total number of <c>TryProposeEquipmentRoute</c> calls whose last
/// attempted candidate was rejected because
/// <c>ShouldOmitDirectCandidate</c> ruled out a direct approach subject to
/// second-threat omission. One of the four rejection-reason counters
/// decomposing <see cref="RefuseAgentTicks"/>.
/// </param>
/// <param name="RouteRefusalLaneNotClear">
/// Total number of <c>TryProposeEquipmentRoute</c> calls whose last
/// attempted candidate was rejected because
/// <c>IsLaneClearOfAllies</c> found the route too close to a friendly agent.
/// One of the four rejection-reason counters decomposing
/// <see cref="RefuseAgentTicks"/>.
/// </param>
public readonly record struct MovementBehaviorMetrics(
    long ApproachAgentTicks,
    long EngageAgentTicks,
    long CommitAgentTicks,
    long RecoverAgentTicks,
    long RefuseAgentTicks,
    long DisengageAgentTicks,
    long RegroupAgentTicks,
    long PursueAgentTicks,
    long PostureTransitions,
    long FacingStepsTurned,
    long DisengagementEntries,
    long ConflictDenials,
    long RouteRefusalNoCandidatesBuilt = 0,
    long RouteRefusalStepEndpointRejected = 0,
    long RouteRefusalDirectCandidateOmitted = 0,
    long RouteRefusalLaneNotClear = 0);

/// <summary>
/// Accumulates per-tick movement behaviour counts into the run aggregate
/// reported as a <see cref="MovementBehaviorMetrics"/>.
/// </summary>
/// <remarks>
/// <para>
/// A mutable struct so that a caller can hold it as a field and feed it once
/// per tick without allocating, exactly like
/// <see cref="CollisionMetricsAccumulator"/>. Running totals are held as
/// <see cref="long"/> and added in a <c>checked</c> context, so a run long
/// enough to overflow fails loudly instead of silently reporting a wrapped
/// count.
/// </para>
/// <para>
/// A default-constructed accumulator is the valid empty state, and
/// <see cref="Reset"/> returns an accumulator to exactly that state.
/// </para>
/// </remarks>
internal struct MovementBehaviorMetricsAccumulator
{
    private long _approachAgentTicks;
    private long _engageAgentTicks;
    private long _commitAgentTicks;
    private long _recoverAgentTicks;
    private long _refuseAgentTicks;
    private long _disengageAgentTicks;
    private long _regroupAgentTicks;
    private long _pursueAgentTicks;
    private long _postureTransitions;
    private long _facingStepsTurned;
    private long _disengagementEntries;
    private long _conflictDenials;
    private long _routeRefusalNoCandidatesBuilt;
    private long _routeRefusalStepEndpointRejected;
    private long _routeRefusalDirectCandidateOmitted;
    private long _routeRefusalLaneNotClear;

    /// <summary>
    /// Returns the accumulator to the state of a freshly constructed one, so
    /// a reused instance cannot leak counts from a previous run.
    /// </summary>
    internal void Reset() => this = default;

    /// <summary>
    /// Folds one tick's movement behaviour counts into the run aggregate.
    /// </summary>
    /// <param name="approachAgents">
    /// Living agents in <see cref="Movement.FootworkPhase.Approach"/> this
    /// tick; likewise for the seven phase parameters that follow.
    /// </param>
    /// <param name="engageAgents">
    /// Living agents in <see cref="Movement.FootworkPhase.Engage"/> this tick.
    /// </param>
    /// <param name="commitAgents">
    /// Living agents in <see cref="Movement.FootworkPhase.Commit"/> this tick.
    /// </param>
    /// <param name="recoverAgents">
    /// Living agents in <see cref="Movement.FootworkPhase.Recover"/> this
    /// tick.
    /// </param>
    /// <param name="refuseAgents">
    /// Living agents finalised to <see cref="Movement.FootworkPhase.Refuse"/>
    /// this tick.
    /// </param>
    /// <param name="disengageAgents">
    /// Living agents in <see cref="Movement.FootworkPhase.Disengage"/> this
    /// tick.
    /// </param>
    /// <param name="regroupAgents">
    /// Living agents in <see cref="Movement.FootworkPhase.Regroup"/> this
    /// tick.
    /// </param>
    /// <param name="pursueAgents">
    /// Living agents in <see cref="Movement.FootworkPhase.Pursue"/> this tick.
    /// </param>
    /// <param name="postureTransitions">
    /// Living agents whose posture differs from their own one tick earlier.
    /// </param>
    /// <param name="facingStepsTurned">
    /// Facing sectors turned through this tick, summed across living agents.
    /// </param>
    /// <param name="disengagementEntries">
    /// Living agents that entered <see cref="Movement.FootworkPhase.Disengage"/>
    /// this tick from any other phase.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any argument is negative. Every input is a count, so a negative value
    /// is a caller defect. The accumulator is left unchanged when an argument
    /// is rejected.
    /// </exception>
    /// <exception cref="OverflowException">
    /// A running total would exceed <see cref="long.MaxValue"/>.
    /// </exception>
    internal void AddTick(
        int approachAgents,
        int engageAgents,
        int commitAgents,
        int recoverAgents,
        int refuseAgents,
        int disengageAgents,
        int regroupAgents,
        int pursueAgents,
        int postureTransitions,
        int facingStepsTurned,
        int disengagementEntries)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(approachAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(engageAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(commitAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(recoverAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(refuseAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(disengageAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(regroupAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(pursueAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(postureTransitions);
        ArgumentOutOfRangeException.ThrowIfNegative(facingStepsTurned);
        ArgumentOutOfRangeException.ThrowIfNegative(disengagementEntries);

        checked
        {
            _approachAgentTicks += approachAgents;
            _engageAgentTicks += engageAgents;
            _commitAgentTicks += commitAgents;
            _recoverAgentTicks += recoverAgents;
            _refuseAgentTicks += refuseAgents;
            _disengageAgentTicks += disengageAgents;
            _regroupAgentTicks += regroupAgents;
            _pursueAgentTicks += pursueAgents;
            _postureTransitions += postureTransitions;
            _facingStepsTurned += facingStepsTurned;
            _disengagementEntries += disengagementEntries;
        }
    }

    /// <summary>
    /// Records the simulation's cumulative friendly-clearance denial total.
    /// The counter on <see cref="BattleSimulation"/> is already a running
    /// total, so this assigns rather than adds; the latest call wins, and
    /// calling once after the run's final tick is sufficient.
    /// </summary>
    /// <param name="totalDenials">
    /// The cumulative denial count as reported by
    /// <see cref="BattleSimulation.MovementConflictDenials"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="totalDenials"/> is negative.
    /// </exception>
    internal void RecordConflictDenialTotal(long totalDenials)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalDenials);

        _conflictDenials = totalDenials;
    }

    /// <summary>
    /// Records the simulation's cumulative route-rejection-reason totals —
    /// the four-way split of <see cref="BattleSimulation"/>'s
    /// <c>TryProposeEquipmentRoute</c> exit sites that together decompose
    /// <see cref="MovementBehaviorMetrics.RefuseAgentTicks"/> (weapon-relative
    /// movement design, section 16, F-A). Recorded the same way as
    /// <see cref="RecordConflictDenialTotal"/>: the counters on
    /// <see cref="BattleSimulation"/> are already running totals, so this
    /// assigns rather than adds, and calling once after the run's final tick
    /// is sufficient.
    /// </summary>
    /// <param name="noCandidatesBuiltTotal">
    /// The cumulative "no candidates built" total as reported by
    /// <see cref="BattleSimulation.RouteRefusalNoCandidatesBuilt"/>.
    /// </param>
    /// <param name="stepEndpointRejectedTotal">
    /// The cumulative "step endpoint rejected" total as reported by
    /// <see cref="BattleSimulation.RouteRefusalStepEndpointRejected"/>.
    /// </param>
    /// <param name="directCandidateOmittedTotal">
    /// The cumulative "direct candidate omitted" total as reported by
    /// <see cref="BattleSimulation.RouteRefusalDirectCandidateOmitted"/>.
    /// </param>
    /// <param name="laneNotClearTotal">
    /// The cumulative "lane not clear" total as reported by
    /// <see cref="BattleSimulation.RouteRefusalLaneNotClear"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any argument is negative. The accumulator is left unchanged when an
    /// argument is rejected.
    /// </exception>
    internal void RecordRouteRefusalReasonTotals(
        long noCandidatesBuiltTotal,
        long stepEndpointRejectedTotal,
        long directCandidateOmittedTotal,
        long laneNotClearTotal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(noCandidatesBuiltTotal);
        ArgumentOutOfRangeException.ThrowIfNegative(stepEndpointRejectedTotal);
        ArgumentOutOfRangeException.ThrowIfNegative(directCandidateOmittedTotal);
        ArgumentOutOfRangeException.ThrowIfNegative(laneNotClearTotal);

        _routeRefusalNoCandidatesBuilt = noCandidatesBuiltTotal;
        _routeRefusalStepEndpointRejected = stepEndpointRejectedTotal;
        _routeRefusalDirectCandidateOmitted = directCandidateOmittedTotal;
        _routeRefusalLaneNotClear = laneNotClearTotal;
    }

    /// <summary>
    /// Projects the accumulated counts into the reported value. Reading does
    /// not consume the accumulator, so it may be called any number of times.
    /// </summary>
    internal readonly MovementBehaviorMetrics ToMetrics() =>
        new(
            _approachAgentTicks,
            _engageAgentTicks,
            _commitAgentTicks,
            _recoverAgentTicks,
            _refuseAgentTicks,
            _disengageAgentTicks,
            _regroupAgentTicks,
            _pursueAgentTicks,
            _postureTransitions,
            _facingStepsTurned,
            _disengagementEntries,
            _conflictDenials,
            _routeRefusalNoCandidatesBuilt,
            _routeRefusalStepEndpointRejected,
            _routeRefusalDirectCandidateOmitted,
            _routeRefusalLaneNotClear);
}
