using System.Collections.Immutable;
using Hukbo.Core.Movement;
using Sandata.Core.Collision;
using Sandata.Core.Geometry;
using Sandata.Core.Mathematics;
using Sandata.Core.Movement;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Sensing;
using Sandata.Core.Squads;

namespace Sandata.Core.Simulation;

/// <summary>
/// The single production caller of <see cref="TickStage"/>'s fourteen-stage
/// table. Task 49 walks stages 1 through 9; stages 10 through 14 are declared
/// below as clearly marked not-yet-implemented members so the full fourteen-
/// stage shape is visible to a later task, per the task 49 brief.
/// </summary>
/// <remarks>
/// <para>
/// <b>The frozen view's lifetime.</b> <see cref="TickStartView"/> is captured
/// once, during stage 3 (<see cref="CaptureTickStartView"/>), and released
/// exactly once, between stage 9 and stage 10 — see <see cref="TickStage"/>'s
/// own remarks for the two binding rules this enforces. Stage 5's alert and
/// contact-memory writes are therefore evaluated against the frozen view
/// during stage 5 but committed into <see cref="State"/> only after the view
/// is released, so no stage 5-through-9 operator can observe another
/// operator's stage-5 write from the same tick.
/// </para>
/// <para>
/// <b>What this task honestly implements.</b> See this class's per-stage
/// method remarks, and task 49's own final report, for exactly which stages
/// are complete, partial, or blocked on a missing callee.
/// </para>
/// </remarks>
public sealed class SandataSimulation
{
    private readonly Mission _mission;
    private readonly SandataRuleset _ruleset;
    private readonly NavGrid _navGrid;
    private readonly WallBuckets _wallBuckets;
    private readonly PathService _pathService;

    /// <summary>
    /// Creates a simulation caller bound to one mission, its resolved
    /// ruleset, the baked navigation artifacts stages 5 and 7 need, and the
    /// authoritative state to begin ticking from.
    /// </summary>
    public SandataSimulation(
        Mission mission,
        SandataRuleset ruleset,
        NavGrid navGrid,
        WallBuckets wallBuckets,
        MissionState initialState)
    {
        ArgumentNullException.ThrowIfNull(mission);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(navGrid);
        ArgumentNullException.ThrowIfNull(wallBuckets);
        ArgumentNullException.ThrowIfNull(initialState);

        _mission = mission;
        _ruleset = ruleset;
        _navGrid = navGrid;
        _wallBuckets = wallBuckets;

        // Call-site obligation for stage 7: PathService takes its latency as
        // a constructor parameter and deliberately does not read the
        // ruleset itself, so this caller passes ruleset.PathLatencyTicks.
        _pathService = new PathService(ruleset.PathLatencyTicks);

        State = initialState;
    }

    /// <summary>The mission this simulation is ticking.</summary>
    public Mission Mission => _mission;

    /// <summary>The ruleset this simulation was constructed with.</summary>
    public SandataRuleset Ruleset => _ruleset;

    /// <summary>The current authoritative state, as of the last completed stage.</summary>
    public MissionState State { get; private set; }

    /// <summary>
    /// Stage 9's write-only movement proposal buffer from the most recently
    /// completed tick — the "later run" (stages 10 through 14) consumes this.
    /// Empty before the first call to <see cref="RunTick"/>.
    /// </summary>
    internal ImmutableArray<MovementProposal> PendingMovementProposals { get; private set; } =
        ImmutableArray<MovementProposal>.Empty;

    /// <summary>
    /// Stage 8's selected intent per living operator from the most recently
    /// completed tick.
    /// </summary>
    public ImmutableArray<IntentSelectionResult> PendingIntents { get; private set; } =
        ImmutableArray<IntentSelectionResult>.Empty;

    /// <summary>
    /// The only production submission door into <see cref="OrderQueue"/> this
    /// simulation exposes — call-site obligation for stage 1: wraps
    /// <see cref="OrderQueue.SubmitValidated"/>, the only public submission
    /// door on that type (<c>OrderQueue.Submit</c> is deliberately private).
    /// The submitted order, if accepted, becomes visible to stage 1's
    /// <see cref="ApplyOrders"/> starting on the tick <see cref="RunTick"/>
    /// is next called for a tick equal to <paramref name="targetTick"/>.
    /// </summary>
    public (OrderQueue Queue, Order? Submitted, OrderRejection? Rejection) SubmitOrder(
        long targetTick,
        int factionId,
        ImmutableArray<ulong> addressees,
        OrderKind kind,
        ImmutableArray<OrderPathNode> pathNodes = default)
    {
        var result = State.OrderQueue.SubmitValidated(
            targetTick, factionId, addressees, kind, _navGrid, _wallBuckets, pathNodes);

        State = State with { OrderQueue = result.Queue };
        return result;
    }

    /// <summary>
    /// Call-site obligation for stage 1's other door: wraps
    /// <see cref="OrderQueue.RestoreForResume"/>, the resume-only restore
    /// path a save/load caller uses to rebuild an <see cref="OrderQueue"/>
    /// from a snapshot without re-running validation.
    /// </summary>
    public static OrderQueue RestoreOrderQueue(
        long nextOrderId, long nextOrderSequence, ImmutableArray<Order> orders) =>
        OrderQueue.RestoreForResume(nextOrderId, nextOrderSequence, orders);

    /// <summary>
    /// Runs stages 1 through 9 of <see cref="TickStage"/>'s table, in that
    /// exact numeric order, for <paramref name="currentTick"/>. Stages 10
    /// through 14 are not called from here — see this class's stage 10-14
    /// members and task 49's report for why.
    /// </summary>
    public void RunTick(long currentTick)
    {
        // Stage 1.
        State = ApplyOrders(State, currentTick);

        // Stage 2.
        State = ApplySpawnAndDespawn(State);

        // Stage 3.
        var bodies = BuildCollisionBodies(State);
        var grid = new SandataCollisionGrid(CollisionCellSizeRaw);
        grid.Rebuild(bodies, CollisionBodyRadiusRaw);
        var view = CaptureTickStartView(State, grid);

        // Stage 4.
        State = ApplyDoorMutations(State);

        // Stage 5 (evaluated only — committed after the view is released).
        var sensing = EvaluateSensing(view, State, currentTick);

        // Stage 6.
        var slots = new SquadSlot[view.Count];
        ComputeSquadGrouping(view, slots);

        // Stage 7.
        AdvancePathService(currentTick);

        // Stage 8.
        PendingIntents = SelectIntents(view, slots, sensing);

        // Stage 9.
        PendingMovementProposals = ComputeMovementProposals(view, slots, State);

        view.Release();

        // Stage 5's deferred commit: safe now that no stage 5-through-9
        // reader can observe it mid-range.
        State = CommitSensing(State, sensing);
    }

    // ---- Stage 10 through 14: not implemented by task 49. -----------------
    // Declared here, per the task brief, so the pipeline's full fourteen-
    // stage shape is visible to whichever task extends it next. Every one of
    // these throws unconditionally; none is called by RunTick above.

    /// <summary>
    /// Stage 10, <see cref="TickStage.LocalAvoidanceAndCollision"/>. Not
    /// implemented by task 49 — this stage's job is resolving and committing
    /// stage 9's <see cref="PendingMovementProposals"/> through
    /// <c>Movement.LocalAvoidance</c>, which is a later task's scope.
    /// </summary>
    internal void ResolveLocalAvoidanceAndCollision() =>
        throw new NotImplementedException(
            "Stage 10 (LocalAvoidanceAndCollision) is not implemented by task 49.");

    /// <summary>Stage 11, <see cref="TickStage.WeaponChain"/>. Not implemented by task 49.</summary>
    internal void AdvanceWeaponChain() =>
        throw new NotImplementedException("Stage 11 (WeaponChain) is not implemented by task 49.");

    /// <summary>Stage 12, <see cref="TickStage.FireProposal"/>. Not implemented by task 49.</summary>
    internal void ProposeFire() =>
        throw new NotImplementedException("Stage 12 (FireProposal) is not implemented by task 49.");

    /// <summary>Stage 13, <see cref="TickStage.DamageResolution"/>. Not implemented by task 49.</summary>
    internal void ResolveDamage() =>
        throw new NotImplementedException("Stage 13 (DamageResolution) is not implemented by task 49.");

    /// <summary>Stage 14, <see cref="TickStage.StateHash"/>. Not implemented by task 49.</summary>
    internal void ComputeStateHash() =>
        throw new NotImplementedException("Stage 14 (StateHash) is not implemented by task 49.");

    // ---- Stages 1 through 9: implemented below, one commit group at a time. ----

    /// <summary>
    /// The uniform collision grid's cell edge length, in raw fixed-point
    /// units. <b>PROVISIONAL</b> — no ruleset field carries this value; no
    /// production caller of <see cref="SandataCollisionGrid"/> existed before
    /// this task to pin one, so this is a placeholder pending a real tuning
    /// pass.
    /// </summary>
    private const int CollisionCellSizeRaw = 256;

    /// <summary>
    /// The operator body radius, in raw fixed-point units, used to rebuild
    /// this tick's collision broad phase. <b>PROVISIONAL</b> for the same
    /// reason as <see cref="CollisionCellSizeRaw"/>.
    /// </summary>
    private const int CollisionBodyRadiusRaw = 32;

    /// <summary>
    /// Stage 1. Applies every order whose <see cref="Order.TargetTick"/>
    /// equals <paramref name="currentTick"/>, read from
    /// <c>state.OrderQueue.InApplicationOrder()</c> — already sorted by
    /// <c>(TargetTick, OrderSequence)</c>, so this method does no sorting of
    /// its own. This stage never calls <see cref="OrderQueue.SubmitValidated"/>
    /// (see <see cref="SubmitOrder"/> for that door); it only reads orders
    /// already accepted into the queue by some earlier submission.
    /// </summary>
    /// <remarks>
    /// For a <see cref="OrderKind.MoveAlongPath"/> order, every addressee's
    /// existing <see cref="OrderAssignment"/> (if any) is replaced by a new
    /// one naming this order's <see cref="Order.PathNodes"/>, starting at
    /// node index 0. For every other <see cref="OrderKind"/> — only
    /// <see cref="OrderKind.Hold"/> exists in this worktree today — the
    /// addressee's existing assignment is cleared and nothing is added back.
    /// <b>PROVISIONAL reconstruction:</b> design section 16 states that an
    /// <see cref="OrderAssignment"/>'s presence or absence is the whole
    /// movement-source selector, but does not enumerate every
    /// <see cref="OrderKind"/>'s exact effect on that assignment; clearing on
    /// every non-<see cref="OrderKind.MoveAlongPath"/> kind is this task's
    /// best-effort reading, not a value taken from that section verbatim.
    /// </remarks>
    private static MissionState ApplyOrders(MissionState state, long currentTick)
    {
        var assignments = state.OrderAssignments;

        foreach (var order in state.OrderQueue.InApplicationOrder())
        {
            if (order.TargetTick != currentTick)
            {
                continue;
            }

            foreach (var addressee in order.Addressees)
            {
                assignments = WithoutAssignment(assignments, addressee);

                if (order.Kind == OrderKind.MoveAlongPath)
                {
                    var assignment = new OrderAssignment(addressee, order.OrderId, CurrentNodeIndex: 0)
                    {
                        PathNodes = order.PathNodes,
                    };
                    assignments = assignments.Add(assignment);
                }
            }
        }

        if (assignments.Length > 1)
        {
            var builder = assignments.ToBuilder();
            builder.Sort(static (left, right) => left.EntityId.CompareTo(right.EntityId));
            assignments = builder.ToImmutable();
        }

        return state with { OrderAssignments = assignments };
    }

    private static ImmutableArray<OrderAssignment> WithoutAssignment(
        ImmutableArray<OrderAssignment> assignments, ulong entityId)
    {
        if (assignments.IsDefaultOrEmpty)
        {
            return ImmutableArray<OrderAssignment>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<OrderAssignment>(assignments.Length);
        foreach (var assignment in assignments)
        {
            if (assignment.EntityId != entityId)
            {
                builder.Add(assignment);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Stage 2. No spawn or despawn trigger source exists anywhere in this
    /// worktree, so this stage is an honest pass-through, per
    /// <see cref="TickStage.SpawnAndDespawn"/>'s own remarks: the state it
    /// receives is the state it returns, unchanged.
    /// </summary>
    private static MissionState ApplySpawnAndDespawn(MissionState state) => state;

    /// <summary>
    /// Builds this tick's collision broad-phase bodies from every operator
    /// <see cref="ApplyOrders"/> and <see cref="ApplySpawnAndDespawn"/> left
    /// behind, in <see cref="MissionState.Operators"/> order (already
    /// ascending by <see cref="OperatorState.EntityId"/>).
    /// </summary>
    private static ImmutableArray<SandataCollisionBody> BuildCollisionBodies(MissionState state)
    {
        var operators = state.Operators;
        if (operators.IsDefaultOrEmpty)
        {
            return ImmutableArray<SandataCollisionBody>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<SandataCollisionBody>(operators.Length);
        foreach (var op in operators)
        {
            builder.Add(new SandataCollisionBody(
                op.EntityId, op.PositionX.RawValue, op.PositionY.RawValue, op.Health > 0));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Stage 3. Rebuilds this tick's collision uniform grid (already done by
    /// <see cref="RunTick"/> before calling this method) and freezes the
    /// tick-start view stages 5 through 9 read.
    /// </summary>
    private static TickStartView CaptureTickStartView(MissionState state, SandataCollisionGrid grid) =>
        new(state, grid.Pairs);

    /// <summary>
    /// Stage 4. No door-trigger source (a breach action, a switch) exists
    /// anywhere in this worktree, so this stage is an honest pass-through,
    /// per <see cref="TickStage.DoorMutation"/>'s own remarks: the state it
    /// receives is the state it returns, unchanged.
    /// </summary>
    private static MissionState ApplyDoorMutations(MissionState state) => state;

    /// <summary>
    /// Half of stage 5's vision cone's total angular width, in
    /// <see cref="Bam16"/> raw units — a quarter turn either side of facing,
    /// 180 degrees total. <b>PROVISIONAL</b>: no field on <see cref="SandataRuleset"/>
    /// names a cone width (its confirmed field list is <c>TickRate</c>,
    /// <c>MsToTickConversionRuleId</c>, <c>PathLatencyTicks</c>,
    /// <c>GroupCohesionRadius</c>, <c>LoweredWallDistanceWu</c>,
    /// <c>AimToleranceBam</c>), so this is a placeholder pending a real
    /// tuning pass, exactly like <see cref="CollisionCellSizeRaw"/> above.
    /// </summary>
    private const ushort VisionConeHalfWidthBam = (ushort)(Bam16.UnitsPerTurn / 4);

    private readonly record struct SensingOutcome(
        ImmutableArray<ImmutableArray<ContactMemoryEntry>> ContactMemoryByIndex,
        ImmutableArray<int> AlertLevelByFaction);

    /// <summary>
    /// Stage 5, evaluated only — see <see cref="RunTick"/>'s remarks on why
    /// the result is not written into <see cref="State"/> until
    /// <see cref="CommitSensing"/> runs after the frozen view is released.
    /// Updates every living operator's <see cref="ContactMemoryEntry"/> array
    /// against <paramref name="view"/> (vision cone via <see cref="VisionCone.Contains"/>,
    /// then wall line of sight via <see cref="LineOfSight.IsVisible"/>, both
    /// gated to <see cref="ContactMemory.DetectRangeWu"/>), then folds each
    /// living operator's resulting best contact tier into its faction's
    /// alert level via <see cref="AlertRules.EvaluateFaction"/>. A dead
    /// operator's contact memory is carried forward unchanged — it observes
    /// nothing — and a dead operator is never itself observable, since only
    /// living enemies are scanned as candidates below.
    /// </summary>
    private SensingOutcome EvaluateSensing(TickStartView view, MissionState state, long currentTick)
    {
        var count = view.Count;
        var contactMemoryByIndex = ImmutableArray.CreateBuilder<ImmutableArray<ContactMemoryEntry>>(count);
        var hasIdentifiedByIndex = new bool[count];
        var observationBuffer = new ContactObservation[count];
        var maxDetectRangeSquaredWu = checked((long)ContactMemory.DetectRangeWu * ContactMemory.DetectRangeWu);

        for (var i = 0; i < count; i++)
        {
            if (!view.IsAlive(i))
            {
                contactMemoryByIndex.Add(view.ContactMemory(i));
                continue;
            }

            var faction = view.Faction(i);
            var facing = Bam16.FromFacing16(view.Facing(i));
            var originX = view.PositionXWu(i);
            var originY = view.PositionYWu(i);
            var observationCount = 0;

            for (var j = 0; j < count; j++)
            {
                if (i == j || !view.IsAlive(j) || view.Faction(j) == faction)
                {
                    continue;
                }

                var targetX = view.PositionXWu(j);
                var targetY = view.PositionYWu(j);
                var dx = targetX - originX;
                var dy = targetY - originY;
                var rangeSquared = checked((dx * dx) + (dy * dy));

                if (!VisionCone.Contains(facing, VisionConeHalfWidthBam, maxDetectRangeSquaredWu, dx, dy))
                {
                    continue;
                }

                if (!LineOfSight.IsVisible(originX, originY, targetX, targetY, _navGrid, _wallBuckets))
                {
                    continue;
                }

                var cellX = NavGrid.WorldToCellCoordinate(targetX);
                var cellY = NavGrid.WorldToCellCoordinate(targetY);
                _navGrid.TryGetCellIndex(cellX, cellY, out var cellIndex);

                observationBuffer[observationCount++] = new ContactObservation(
                    view.EntityIds[j], HasLineOfSightThisTick: true, rangeSquared, cellIndex);
            }

            var updatedMemory = ContactMemory.Update(
                view.ContactMemory(i), observationBuffer.AsSpan(0, observationCount), currentTick);
            contactMemoryByIndex.Add(updatedMemory);

            foreach (var entry in updatedMemory.AsSpan())
            {
                if (entry.ContactTier == (int)ContactTier.Identified)
                {
                    hasIdentifiedByIndex[i] = true;
                    break;
                }
            }
        }

        // Design section 4: faction is a two-valued selector, always 0 or 1
        // — see FactionAlertState's own remarks. AlertRules.EvaluateFaction
        // is called once per faction, over that faction's own living
        // operators' per-operator observations, per TickStage.AlertAndSensing's
        // remarks.
        var faction0 = new AlertTriggerObservation[count];
        var faction1 = new AlertTriggerObservation[count];
        var faction0Count = 0;
        var faction1Count = 0;

        for (var i = 0; i < count; i++)
        {
            if (!view.IsAlive(i))
            {
                continue;
            }

            var observation = new AlertTriggerObservation(hasIdentifiedByIndex[i], false, false, false);
            if (view.Faction(i) == 0)
            {
                faction0[faction0Count++] = observation;
            }
            else
            {
                faction1[faction1Count++] = observation;
            }
        }

        var level0 = (int)AlertRules.EvaluateFaction(
            PreviousAlertLevel(state, 0), faction0.AsSpan(0, faction0Count));
        var level1 = (int)AlertRules.EvaluateFaction(
            PreviousAlertLevel(state, 1), faction1.AsSpan(0, faction1Count));

        return new SensingOutcome(
            contactMemoryByIndex.ToImmutable(),
            ImmutableArray.Create(level0, level1));
    }

    private static AlertLevel PreviousAlertLevel(MissionState state, int factionId)
    {
        foreach (var factionAlert in state.FactionAlerts)
        {
            if (factionAlert.FactionId == factionId)
            {
                return (AlertLevel)factionAlert.AlertLevel;
            }
        }

        return AlertLevel.Calm;
    }

    /// <summary>
    /// Stage 5's deferred commit, run by <see cref="RunTick"/> only after
    /// <see cref="TickStartView.Release"/> — writes <paramref name="outcome"/>
    /// into <paramref name="state"/>'s <see cref="MissionState.Operators"/>
    /// and <see cref="MissionState.FactionAlerts"/>.
    /// </summary>
    private static MissionState CommitSensing(MissionState state, SensingOutcome outcome)
    {
        var operators = state.Operators;
        if (operators.IsDefaultOrEmpty)
        {
            return state;
        }

        var updatedOperators = ImmutableArray.CreateBuilder<OperatorState>(operators.Length);
        for (var i = 0; i < operators.Length; i++)
        {
            updatedOperators.Add(operators[i] with { ContactMemory = outcome.ContactMemoryByIndex[i] });
        }

        var factionAlerts = ImmutableArray.Create(
            new FactionAlertState(0, outcome.AlertLevelByFaction[0]),
            new FactionAlertState(1, outcome.AlertLevelByFaction[1]));

        return state with
        {
            Operators = updatedOperators.ToImmutable(),
            FactionAlerts = factionAlerts,
        };
    }

    /// <summary>
    /// Stage 6. Call-site obligation: derives one <see cref="SquadSlot"/> per
    /// tick-start-view entry via <see cref="SquadGrouping.Compute"/>'s one
    /// overload, gated by <see cref="SandataRuleset.GroupCohesionRadius"/>.
    /// Purely derived — nothing here is written back into
    /// <see cref="MissionState"/>, per that method's own remarks.
    /// </summary>
    private void ComputeSquadGrouping(TickStartView view, Span<SquadSlot> slots)
    {
        var count = view.Count;
        var isAlive = new bool[count];
        var factions = new int[count];
        var xRaw = new int[count];
        var yRaw = new int[count];

        for (var i = 0; i < count; i++)
        {
            isAlive[i] = view.IsAlive(i);
            factions[i] = view.Faction(i);
            xRaw[i] = view.PositionXRaw(i);
            yRaw[i] = view.PositionYRaw(i);
        }

        SquadGrouping.Compute(
            view.EntityIds, isAlive, factions, xRaw, yRaw,
            _ruleset.GroupCohesionRadius, view.Pairs, slots);
    }

    /// <summary>
    /// Stage 7. Call-site obligation: advances <see cref="_pathService"/> by
    /// one tick against <see cref="_navGrid"/> and <see cref="_wallBuckets"/>.
    /// <b>PROVISIONAL</b> <paramref name="currentTick"/>'s <c>blocked</c> span
    /// is all-<see langword="false"/> — no door-driven dynamic blocker source
    /// exists in this worktree (stage 4 is the same honest pass-through), so
    /// every cell reports passable to the search this call may run. No
    /// autonomous destination-request source exists either, so this stage's
    /// search-and-publish machinery runs every tick but, in this wave, never
    /// has an outstanding request to act on — see <see cref="TickStage.PathService"/>'s
    /// own remarks.
    /// </summary>
    private void AdvancePathService(long currentTick)
    {
        var blocked = new bool[_navGrid.CellCount];
        _pathService.Advance(currentTick, _navGrid, blocked, _wallBuckets);
    }

    /// <summary>
    /// Stage 8. Call-site obligation: assembles one
    /// <see cref="IntentSelectionInput"/> per tick-start-view entry from
    /// <paramref name="view"/>, stage 5's frozen-view-evaluated
    /// <paramref name="sensing"/> (not yet committed into
    /// <see cref="MissionState"/> — see <see cref="RunTick"/>'s remarks), and
    /// stage 7's <see cref="PathService.GetReasonCode"/>, then calls
    /// <see cref="IntentSelection.SelectAll"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="IntentSelectionInput.IsAtBreachPoint"/> is hardcoded
    /// <see langword="false"/>: <b>PROVISIONAL</b>, no breach-point source
    /// exists in this worktree. <see cref="SquadSlot.GroupId"/> (design
    /// section 8's <see langword="ulong"/> "minimum entity id") is narrowed
    /// to the <see langword="int"/> <see cref="PathService.GetReasonCode"/>
    /// actually takes — a real type-domain mismatch between the two this
    /// task did not introduce and is out of scope to fix. It is inert this
    /// wave: <see cref="AdvancePathService"/> never calls
    /// <see cref="PathService.RequestPath"/> for any group, so
    /// <see cref="PathService.GetReasonCode"/> answers
    /// <see cref="PathReasonCode.NoDestinationRequested"/> for every group id
    /// regardless of the narrowing's exact numeric result.
    /// </remarks>
    private ImmutableArray<IntentSelectionResult> SelectIntents(
        TickStartView view, ReadOnlySpan<SquadSlot> slots, SensingOutcome sensing)
    {
        var count = view.Count;
        var inputs = new IntentSelectionInput[count];

        for (var i = 0; i < count; i++)
        {
            var bestTier = ContactTier.Unknown;
            var memory = sensing.ContactMemoryByIndex[i];
            if (!memory.IsDefaultOrEmpty)
            {
                foreach (var entry in memory.AsSpan())
                {
                    var tier = (ContactTier)entry.ContactTier;
                    if (tier > bestTier)
                    {
                        bestTier = tier;
                    }
                }
            }

            var groupId = unchecked((int)slots[i].GroupId);
            var pathReasonCode = _pathService.GetReasonCode(groupId);

            inputs[i] = new IntentSelectionInput(
                view.EntityIds[i],
                view.Health(i),
                view.SuppressionCounter(i),
                bestTier,
                false,
                pathReasonCode);
        }

        return IntentSelection.SelectAll(inputs);
    }

    /// <summary>
    /// The inverse of <see cref="WorldUnits.FromFixedPoint"/>, scoped to this
    /// file: one world unit is exactly <c>1,024</c> raw
    /// <see cref="FixedPoint"/> units (<see cref="FixedPoint.Scale"/>), per
    /// that type's own remarks. <see cref="WorldUnits"/> only ever needed the
    /// raw-to-world-unit direction before this task; stage 9 is the first
    /// caller that needs the reverse, to place a <see cref="MovementProposal"/>'s
    /// desired position (raw domain) from an <see cref="OrderPathNode"/>
    /// (world-unit domain). Kept private here rather than added to
    /// <see cref="WorldUnits"/> itself, since editing that file is out of
    /// this task's scope.
    /// </summary>
    private static int RawFromWorldUnits(long worldUnits) => checked((int)(worldUnits << 10));

    /// <summary>
    /// Stage 9. Call-site obligation: chooses each living operator's
    /// movement source (an authored <see cref="OrderAssignment"/> or the
    /// autonomous squad slot — <see cref="OrderAssignment"/>'s own presence
    /// or absence is the whole selector, per its remarks), excludes ordered
    /// operators from <see cref="MovementSource.SlotTargetingRoster"/>
    /// implicitly by branching on that same presence check per operator, and
    /// produces one write-only <see cref="MovementProposal"/> per living
    /// operator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ordered branch.</b> An operator with an <see cref="OrderAssignment"/>
    /// walks directly toward <c>PathNodes[CurrentNodeIndex]</c> (clamped into
    /// range), converted world-unit to raw via <see cref="RawFromWorldUnits"/>
    /// — design section 16: "An authored polyline is authoritative, not
    /// derived... never re-smoothed", so this stage places the desired point
    /// at the node itself rather than through <see cref="SlotTargets.ComputeTarget"/>'s
    /// arclength machinery, which is the autonomous branch's tool over a
    /// group's own shared path, not an individual operator's authored one.
    /// </para>
    /// <para>
    /// <b>Autonomous branch, and the formation-collapse gap this task
    /// reports rather than forces.</b> Design section 8's slot-target formula
    /// (<see cref="SlotTargets.ComputeTarget"/>) needs the operator's group's
    /// own shared published path, sampled by arclength, to place a target;
    /// <see cref="FormationCollapse"/>'s formation half-width gate needs that
    /// same live path's leader-clearance context to mean anything. No group
    /// ever has a published path this wave: <see cref="AdvancePathService"/>
    /// never calls <see cref="PathService.RequestPath"/> for any group (no
    /// autonomous destination-request source exists in this worktree), so
    /// <see cref="PathService.GetCurrentPath"/> is empty for every group,
    /// every tick — the same fact <see cref="TickStage.PathService"/>'s own
    /// remarks already state. With no path to sample, <see cref="SlotTargets"/>
    /// and <see cref="FormationCollapse"/> have nothing to compute against, so
    /// this task does not call either for the autonomous branch; an
    /// unassigned operator's honest desired position this wave is its own
    /// current position — hold — until a future task wires an autonomous
    /// destination-request source into stage 7.
    /// </para>
    /// </remarks>
    private static ImmutableArray<MovementProposal> ComputeMovementProposals(
        TickStartView view, ReadOnlySpan<SquadSlot> slots, MissionState state)
    {
        var count = view.Count;
        var assignments = state.OrderAssignments;
        var builder = ImmutableArray.CreateBuilder<MovementProposal>();

        for (var i = 0; i < count; i++)
        {
            if (!view.IsAlive(i))
            {
                continue;
            }

            var entityId = view.EntityIds[i];
            var startXRaw = view.PositionXRaw(i);
            var startYRaw = view.PositionYRaw(i);
            var slot = slots[i];

            var assignment = FindAssignment(assignments, entityId);

            int desiredXRaw;
            int desiredYRaw;

            if (assignment is not null)
            {
                var nodes = assignment.PathNodes;
                if (nodes.IsDefaultOrEmpty)
                {
                    desiredXRaw = startXRaw;
                    desiredYRaw = startYRaw;
                }
                else
                {
                    var nodeIndex = Math.Clamp(assignment.CurrentNodeIndex, 0, nodes.Length - 1);
                    var node = nodes[nodeIndex];
                    desiredXRaw = RawFromWorldUnits(node.X);
                    desiredYRaw = RawFromWorldUnits(node.Y);
                }
            }
            else
            {
                desiredXRaw = startXRaw;
                desiredYRaw = startYRaw;
            }

            builder.Add(new MovementProposal(
                entityId, startXRaw, startYRaw, desiredXRaw, desiredYRaw,
                slot.GroupId, slot.SlotIndex ?? 0));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Binary search for <paramref name="entityId"/> within
    /// <paramref name="assignments"/>, which <see cref="ApplyOrders"/> keeps
    /// sorted ascending by <see cref="OrderAssignment.EntityId"/> — the same
    /// flat-sorted-array convention <see cref="MovementSource.SlotTargetingRoster"/>'s
    /// own remarks describe, in place of a banned <c>Dictionary&lt;&gt;</c>.
    /// </summary>
    private static OrderAssignment? FindAssignment(ImmutableArray<OrderAssignment> assignments, ulong entityId)
    {
        if (assignments.IsDefaultOrEmpty)
        {
            return null;
        }

        var low = 0;
        var high = assignments.Length - 1;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var candidate = assignments[mid];

            if (candidate.EntityId == entityId)
            {
                return candidate;
            }

            if (candidate.EntityId < entityId)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return null;
    }
}
