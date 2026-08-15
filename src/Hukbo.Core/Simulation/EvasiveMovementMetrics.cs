using Hukbo.Core.Movement;

namespace Hukbo.Core.Simulation;

/// <summary>
/// Aggregate in-fight evasion counters for one completed headless run, as
/// required by task 7 of the 2026-08-15 in-fight evasion plan. Together these
/// fields put a real number behind every anti-goal bar in section 8 of that
/// plan's design document.
/// </summary>
/// <remarks>
/// <para>
/// These counters are <b>derived</b> observability data, modelled directly on
/// <see cref="MovementBehaviorMetrics"/>. They are never hashed, never
/// snapshotted, and never persisted, so they cannot influence a simulation
/// outcome. Unlike that type, <i>every</i> field here is reconstructed outside
/// the simulation by comparing the current tick's <see cref="AgentView"/>s
/// against the previous tick's: the simulation accumulates nothing on their
/// behalf, and no counter here has a partner counter inside
/// <see cref="BattleSimulation"/>.
/// </para>
/// <para>
/// The definitions deliberately match
/// <c>tests/Hukbo.Core.Tests/Movement/EvasionCalibrationHarness.cs</c>, the
/// twenty-seed instrument that measured the V13 baseline these bars are written
/// against, so the two instruments can be compared directly. In particular the
/// rooted test uses the same 60-raw threshold, and the retention test uses each
/// warrior's own <c>AttackRangeRaw</c> rather than body contact. Body contact is
/// the wrong test and the design says so: <c>CollisionGeometry.Overlaps</c> uses
/// a strict comparison, so a committed position never sits below
/// <c>(2 * bodyRadius)^2</c>, a tangency-inclusive contact test is satisfied
/// only at exactly that value, and the measured contact count over the whole
/// twenty-seed matrix was therefore exactly zero.
/// </para>
/// <para>
/// Two same-seed runs of the same build must produce identical values in every
/// field. Every one of the five <see cref="EvasiveAction"/> fields is identically
/// zero under any movement preset other than
/// <see cref="MovementPresetId.EvasiveFootworkV14"/>, which is the only preset
/// that ever resolves an action other than <see cref="EvasiveAction.None"/>. The
/// six movement fields are nonzero under every preset, because every battle
/// moves somebody.
/// </para>
/// </remarks>
/// <param name="LivingAgentTicks">
/// Total agent-ticks on which a warrior was alive at both ends of the step —
/// alive on the tick just advanced and alive on the tick before it. An
/// agent-tick count, not a count of distinct agents; likewise for every other
/// field below. A warrior that died on this tick contributes neither a living
/// agent-tick nor a travel sample, because dying is neither standing still nor
/// moving. This is the denominator of the rooted share and of the two share
/// ceilings on evasive action.
/// </param>
/// <param name="RootedAgentTicks">
/// Total living agent-ticks whose per-tick displacement was strictly below
/// <see cref="EvasiveMovementMetrics.RootedDisplacementThresholdRawPerTick"/>
/// raw units. Divided by <see cref="LivingAgentTicks"/> this is the rooted
/// share, the figure the whole feature exists to move downwards.
/// </param>
/// <param name="TotalTravelRaw">
/// The sum of every living agent-tick's displacement magnitude, in raw
/// fixed-point units, floored to an integer once per sample by
/// <c>FixedPoint.IntegerSquareRoot</c>. Divided by the run's spawned agent
/// count this is travel per living agent, the figure the marathon ceiling is
/// written against.
/// </param>
/// <param name="ReachRetentionAgentTicks">
/// Total living agent-ticks on which the warrior held a selected target that
/// was a living warrior of the other faction, and that target's centre lay
/// within the holder's own <c>AttackRangeRaw</c> of the holder's centre. This
/// is the numeric form of "movement during the battle, not away from it".
/// </param>
/// <param name="TargetHeldAgentTicks">
/// Total living agent-ticks on which the warrior held a selected target that
/// was a living warrior of the other faction, at any range at all. The
/// denominator <see cref="ReachRetentionAgentTicks"/> is a share of.
/// </param>
/// <param name="SlipLateralAgentTicks">
/// Total living agent-ticks whose resolved action was
/// <see cref="EvasiveAction.SlipLateral"/>; likewise for the four fields that
/// follow, one per non-<see cref="EvasiveAction.None"/> member of that enum.
/// </param>
/// <param name="DodgeIncomingAgentTicks">
/// Total living agent-ticks whose resolved action was
/// <see cref="EvasiveAction.DodgeIncoming"/>.
/// </param>
/// <param name="GiveGroundAgentTicks">
/// Total living agent-ticks whose resolved action was
/// <see cref="EvasiveAction.GiveGround"/>. The one rung with a directional
/// bias, and the one carrying its own separate ceiling.
/// </param>
/// <param name="BreakOffAgentTicks">
/// Total living agent-ticks whose resolved action was
/// <see cref="EvasiveAction.BreakOff"/>.
/// </param>
/// <param name="BreakOffArmedAgentTicks">
/// Total living agent-ticks whose resolved action was
/// <see cref="EvasiveAction.BreakOffArmed"/> — the carrier state a warrior
/// holds for the one tick between an intercepted exchange against it and the
/// break step it owes.
/// </param>
/// <param name="NetDisplacementSumRaw">
/// The sum, over every spawned agent slot, of the straight-line distance in raw
/// units from that agent's spawn position to its position at the run's terminal
/// tick. Corpses are included and keep their final position, because where the
/// dead lie is part of where the battle was fought. Divided by the spawned
/// agent count this is mean net drift, the figure the "nobody may leave the
/// battle" bar is written against.
/// </param>
public readonly record struct EvasiveMovementMetrics(
    long LivingAgentTicks,
    long RootedAgentTicks,
    long TotalTravelRaw,
    long ReachRetentionAgentTicks,
    long TargetHeldAgentTicks,
    long SlipLateralAgentTicks,
    long DodgeIncomingAgentTicks,
    long GiveGroundAgentTicks,
    long BreakOffAgentTicks,
    long BreakOffArmedAgentTicks,
    long NetDisplacementSumRaw)
{
    /// <summary>
    /// The per-tick displacement, in raw fixed-point units, strictly below
    /// which a living warrior counts as rooted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the value of <c>GaitGeometry.CrawlThresholdRawPerTick</c> in
    /// <c>Hukbo.Client</c> — the renderer's legibility floor, the speed below
    /// which the gait animation stops swinging legs and the pawn reads as
    /// standing still. It is restated here as a literal because
    /// <c>Hukbo.Core</c> may never reference the client, and it is deliberately
    /// the renderer's number rather than a number of this type's own choosing:
    /// the anti-goal it serves is about what a spectator sees, so the threshold
    /// has to be the one the spectator's pawn actually uses.
    /// </para>
    /// <para>
    /// The same literal, for the same reason, appears in
    /// <c>EvasionCalibrationHarness</c>. If the client constant ever moves,
    /// both have to move with it or the rooted share stops meaning what its
    /// name says. Nothing enforces that automatically, because neither
    /// <c>Hukbo.Core</c> nor its test project may reach across the
    /// simulation-to-client boundary to read it.
    /// </para>
    /// </remarks>
    public const int RootedDisplacementThresholdRawPerTick = 60;
}

/// <summary>
/// Accumulates per-tick in-fight evasion counts into the run aggregate reported
/// as an <see cref="EvasiveMovementMetrics"/>.
/// </summary>
/// <remarks>
/// <para>
/// A mutable struct so that a caller can hold it as a field and feed it once
/// per tick without allocating, exactly like
/// <see cref="MovementBehaviorMetricsAccumulator"/>. Running totals are held as
/// <see cref="long"/> and added in a <c>checked</c> context, so a run long
/// enough to overflow fails loudly instead of silently reporting a wrapped
/// count.
/// </para>
/// <para>
/// A default-constructed accumulator is the valid empty state, and
/// <see cref="Reset"/> returns an accumulator to exactly that state.
/// </para>
/// </remarks>
internal struct EvasiveMovementMetricsAccumulator
{
    private long _livingAgentTicks;
    private long _rootedAgentTicks;
    private long _totalTravelRaw;
    private long _reachRetentionAgentTicks;
    private long _targetHeldAgentTicks;
    private long _slipLateralAgentTicks;
    private long _dodgeIncomingAgentTicks;
    private long _giveGroundAgentTicks;
    private long _breakOffAgentTicks;
    private long _breakOffArmedAgentTicks;
    private long _netDisplacementSumRaw;

    /// <summary>
    /// Returns the accumulator to the state of a freshly constructed one, so a
    /// reused instance cannot leak counts from a previous run.
    /// </summary>
    internal void Reset() => this = default;

    /// <summary>
    /// Folds one tick's evasion counts into the run aggregate.
    /// </summary>
    /// <param name="livingAgents">
    /// Agents alive at both ends of this tick's step.
    /// </param>
    /// <param name="rootedAgents">
    /// Of those, the ones whose displacement was strictly below
    /// <see cref="EvasiveMovementMetrics.RootedDisplacementThresholdRawPerTick"/>.
    /// </param>
    /// <param name="travelRaw">
    /// The sum of this tick's displacement magnitudes across living agents, in
    /// raw units.
    /// </param>
    /// <param name="reachRetentionAgents">
    /// Living agents holding a living enemy target inside their own attack
    /// range.
    /// </param>
    /// <param name="targetHeldAgents">
    /// Living agents holding a living enemy target at any range.
    /// </param>
    /// <param name="slipLateralAgents">
    /// Living agents whose action this tick was
    /// <see cref="EvasiveAction.SlipLateral"/>; likewise for the four
    /// parameters that follow.
    /// </param>
    /// <param name="dodgeIncomingAgents">
    /// Living agents whose action was <see cref="EvasiveAction.DodgeIncoming"/>.
    /// </param>
    /// <param name="giveGroundAgents">
    /// Living agents whose action was <see cref="EvasiveAction.GiveGround"/>.
    /// </param>
    /// <param name="breakOffAgents">
    /// Living agents whose action was <see cref="EvasiveAction.BreakOff"/>.
    /// </param>
    /// <param name="breakOffArmedAgents">
    /// Living agents whose action was
    /// <see cref="EvasiveAction.BreakOffArmed"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any argument is negative. Every input is a count or a distance sum, so a
    /// negative value is a caller defect. The accumulator is left unchanged
    /// when an argument is rejected.
    /// </exception>
    /// <exception cref="OverflowException">
    /// A running total would exceed <see cref="long.MaxValue"/>.
    /// </exception>
    internal void AddTick(
        int livingAgents,
        int rootedAgents,
        long travelRaw,
        int reachRetentionAgents,
        int targetHeldAgents,
        int slipLateralAgents,
        int dodgeIncomingAgents,
        int giveGroundAgents,
        int breakOffAgents,
        int breakOffArmedAgents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(livingAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(rootedAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(travelRaw);
        ArgumentOutOfRangeException.ThrowIfNegative(reachRetentionAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(targetHeldAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(slipLateralAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(dodgeIncomingAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(giveGroundAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(breakOffAgents);
        ArgumentOutOfRangeException.ThrowIfNegative(breakOffArmedAgents);

        checked
        {
            _livingAgentTicks += livingAgents;
            _rootedAgentTicks += rootedAgents;
            _totalTravelRaw += travelRaw;
            _reachRetentionAgentTicks += reachRetentionAgents;
            _targetHeldAgentTicks += targetHeldAgents;
            _slipLateralAgentTicks += slipLateralAgents;
            _dodgeIncomingAgentTicks += dodgeIncomingAgents;
            _giveGroundAgentTicks += giveGroundAgents;
            _breakOffAgentTicks += breakOffAgents;
            _breakOffArmedAgentTicks += breakOffArmedAgents;
        }
    }

    /// <summary>
    /// Records the run's spawn-to-terminal displacement sum. This one quantity
    /// cannot be accumulated tick by tick — it is a property of the terminal
    /// snapshot rather than of any step — so it is assigned once after the
    /// run's final tick, in the same manner
    /// <see cref="MovementBehaviorMetricsAccumulator.RecordConflictDenialTotal"/>
    /// assigns its running total. The latest call wins.
    /// </summary>
    /// <param name="netDisplacementSumRaw">
    /// The sum over every spawned agent slot of the distance in raw units from
    /// that slot's spawn position to its terminal position.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="netDisplacementSumRaw"/> is negative.
    /// </exception>
    internal void RecordNetDisplacementSum(long netDisplacementSumRaw)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(netDisplacementSumRaw);

        _netDisplacementSumRaw = netDisplacementSumRaw;
    }

    /// <summary>
    /// Projects the accumulated counts into the reported value. Reading does
    /// not consume the accumulator, so it may be called any number of times.
    /// </summary>
    internal readonly EvasiveMovementMetrics ToMetrics() =>
        new(
            _livingAgentTicks,
            _rootedAgentTicks,
            _totalTravelRaw,
            _reachRetentionAgentTicks,
            _targetHeldAgentTicks,
            _slipLateralAgentTicks,
            _dodgeIncomingAgentTicks,
            _giveGroundAgentTicks,
            _breakOffAgentTicks,
            _breakOffArmedAgentTicks,
            _netDisplacementSumRaw);
}
