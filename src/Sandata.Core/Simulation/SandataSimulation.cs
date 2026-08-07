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
        PendingIntents = SelectIntents(view, slots);

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

    private readonly record struct SensingOutcome(
        ImmutableArray<ImmutableArray<ContactMemoryEntry>> ContactMemoryByIndex,
        ImmutableArray<int> AlertLevelByFaction);

    private SensingOutcome EvaluateSensing(TickStartView view, MissionState state, long currentTick) =>
        new(ImmutableArray<ImmutableArray<ContactMemoryEntry>>.Empty, ImmutableArray<int>.Empty);

    private static MissionState CommitSensing(MissionState state, SensingOutcome outcome) => state;

    private static void ComputeSquadGrouping(TickStartView view, Span<SquadSlot> slots)
    {
    }

    private void AdvancePathService(long currentTick)
    {
    }

    private static ImmutableArray<IntentSelectionResult> SelectIntents(
        TickStartView view, ReadOnlySpan<SquadSlot> slots) =>
        ImmutableArray<IntentSelectionResult>.Empty;

    private static ImmutableArray<MovementProposal> ComputeMovementProposals(
        TickStartView view, ReadOnlySpan<SquadSlot> slots, MissionState state) =>
        ImmutableArray<MovementProposal>.Empty;
}
