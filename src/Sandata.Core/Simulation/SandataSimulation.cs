using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Collision;
using Sandata.Core.Combat;
using Sandata.Core.Determinism;
using Sandata.Core.Events;
using Sandata.Core.Geometry;
using Sandata.Core.Maps;
using Sandata.Core.Mathematics;
using Sandata.Core.Movement;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Sensing;
using Sandata.Core.Squads;
using Sandata.Core.Weapons;

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
    /// Task 79d-2b: every parsed <c>COVER</c> record this simulation's map
    /// carries, read by <see cref="ProposeFire"/> to resolve a target's real
    /// <see cref="CoverState"/> instead of the constant
    /// <see cref="CoverState.NotInCover"/> every shot resolved against
    /// before this task. Never mutated after construction — cover objects
    /// are static map geometry, not simulation state.
    /// </summary>
    private readonly ImmutableArray<CoverRecord> _coverRecords;

    /// <summary>
    /// The chamfer clearance value at every <see cref="_navGrid"/> cell —
    /// <see cref="Navigation.ClearanceField.Build"/>'s output, baked once
    /// here from the same static <see cref="NavGrid.Passability"/> array
    /// stage 5's contact queries already read, and never rebuilt: this
    /// worktree has no dynamic-door source (<see cref="AdvancePathService"/>'s
    /// own remarks), so nothing ever changes a cell's passability after
    /// construction. Stage 9's autonomous branch indexes this array at the
    /// leader's cell to feed <see cref="Squads.FormationCollapse"/>.
    /// </summary>
    private readonly int[] _clearanceField;

    /// <summary>
    /// Stage 10's collision-and-avoidance resolver, constructed once and
    /// reused every tick — <see cref="Movement.LocalAvoidance"/> carries no
    /// per-tick state of its own beyond the resolver it wraps, so there is
    /// nothing to reset between ticks.
    /// </summary>
    private LocalAvoidance? _localAvoidance;

    /// <summary>
    /// Stage 3's physical-contact broad-phase grid, constructed once and
    /// reused every tick. <see cref="SandataCollisionGrid"/>'s own remarks
    /// promise "all storage is reused between calls and grows only when
    /// capacity is insufficient, so a warm tick allocates nothing" — a
    /// promise a fresh instance every tick defeated, since every one of its
    /// backing arrays (and <see cref="Pairs"/>) then starts back at its
    /// small initial capacity and must regrow from scratch. <see cref="Rebuild"/>
    /// clears and re-indexes this tick's bodies before producing
    /// <see cref="Pairs"/>, so nothing from an earlier tick survives into
    /// the next one — see
    /// <c>TickPipelineTests.SandataCollisionGrid_Rebuild_DiscardsThePreviousCallsPairs</c>
    /// for the reuse proof this task adds.
    /// </summary>
    private readonly SandataCollisionGrid _contactGrid = new(CollisionCellSizeRaw);

    /// <summary>
    /// Stage 3's second grid, the squad-cohesion range query — the same
    /// reuse reasoning as <see cref="_contactGrid"/> applies, sized once at
    /// construction from <see cref="_ruleset"/>'s
    /// <see cref="SandataRuleset.GroupCohesionRadiusWu"/>, which never
    /// changes across this simulation's lifetime.
    /// </summary>
    private readonly SandataCollisionGrid _cohesionGrid;

    /// <summary>
    /// Stage 7's cell-blocked-this-tick input to <see cref="PathService.Advance"/>,
    /// allocated once and never written to after construction. Every element
    /// stays its default <see langword="false"/> for this worktree's whole
    /// lifetime — <see cref="AdvancePathService"/>'s own remarks explain why
    /// no door-driven dynamic blocker source exists here — so a fresh array
    /// every tick would only ever reproduce the same all-false content
    /// <see cref="PathService.Advance"/> already reads through a
    /// <see cref="ReadOnlySpan{T}"/> it never mutates.
    /// </summary>
    private readonly bool[] _pathBlockedCells;

    /// <summary>
    /// Stage 5's reused cell chain for <see cref="LineOfSight.IsVisible"/>,
    /// sized once from <see cref="_navGrid"/>'s fixed dimensions. Task 88:
    /// stage 5 runs that query once per living observer-candidate pair — 4,684
    /// times per tick at 200 operators — and each call used to allocate its
    /// own chain, which made it the largest single allocator in the whole
    /// tick. Contents never outlive one call: <c>LineOfSight</c> writes the
    /// whole prefix it then reads and stores no reference to the buffer, so
    /// nothing from one pair's query can reach another's answer.
    /// </summary>
    private readonly int[] _sightCellBuffer;

    /// <summary>
    /// Stage 5's reused merge buffer for <see cref="ContactMemory.Update"/>,
    /// which needs one slot per remembered contact plus one per observation.
    /// Grown on demand and never shrunk, the same shape
    /// <see cref="PathService"/> uses for its own smoothing scratch, because
    /// the roster can grow through stage 2 after this simulation was
    /// constructed. Like <see cref="_sightCellBuffer"/> it is capacity and
    /// never content: <c>ContactMemory.Update</c> copies its result out
    /// before returning, so no entry survives into the next operator's
    /// memory.
    /// </summary>
    private ContactMemoryEntry[] _contactMergeBuffer = [];

    /// <summary>
    /// Stage 11's write-only record of which operators fired this tick, and
    /// at which contact, for <see cref="ProposeFire"/> to consume. Empty
    /// before the first call to <see cref="RunTick"/>.
    /// </summary>
    private ImmutableArray<FiredShot> _pendingFiredShots = ImmutableArray<FiredShot>.Empty;

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
        MissionState initialState,
        ImmutableArray<CoverRecord> coverRecords)
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
        _coverRecords = coverRecords.IsDefault ? ImmutableArray<CoverRecord>.Empty : coverRecords;

        // Call-site obligation for stage 7: PathService takes its latency as
        // a constructor parameter and deliberately does not read the
        // ruleset itself, so this caller passes ruleset.PathLatencyTicks.
        _pathService = new PathService(ruleset.PathLatencyTicks);

        // NavCellFlags is byte-backed with literal values 0 (Blocked), 1
        // (Open), 2 (Door) — the same "0 blocked, nonzero open" convention
        // ClearanceField.Build documents, so this reinterpret cast carries
        // no narrowing and no semantic mismatch. Built once, here, since the
        // grid never changes after construction (see _clearanceField).
        _clearanceField = new int[navGrid.CellCount];
        ClearanceField.Build(
            MemoryMarshal.Cast<NavCellFlags, byte>(navGrid.Passability),
            _clearanceField,
            navGrid.Width,
            navGrid.Height);

        // See _cohesionGrid's own remarks: sized once here from the ruleset
        // this simulation is bound to for its whole lifetime.
        var cohesionRadiusRaw = RawFromWorldUnits(ruleset.GroupCohesionRadiusWu);
        _cohesionGrid = new SandataCollisionGrid(CohesionCollisionCellSizeRaw(cohesionRadiusRaw));

        // See _pathBlockedCells's own remarks: never written to after this.
        _pathBlockedCells = new bool[navGrid.CellCount];

        // A NavGrid's dimensions are fixed for its lifetime, so stage 5's
        // cell chain can be sized once here rather than grown.
        _sightCellBuffer = new int[LineOfSight.RequiredCellBufferLength(navGrid)];

        State = initialState;

        RecomputePublishedPaths();
    }

    /// <summary>
    /// Design section 4's resume rule, run here so that it happens "before
    /// the first tick executes": every group whose request has already been
    /// published has its polyline recomputed from the request record
    /// <see cref="MissionState.Groups"/> carries, because that polyline is
    /// derived and is deliberately absent from both the snapshot and both
    /// hashes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="GroupPathState"/> with <see cref="GroupPathState.HasOutstandingRequest"/>
    /// still set needs nothing here and gets nothing: stage 7 re-submits it
    /// on the first tick and <see cref="PathService.Advance"/> publishes it
    /// on the tick its stored <see cref="GroupPathState.RequestTick"/> always
    /// implied. Only a cleared flag means "already published", since
    /// <see cref="AdvancePathService"/> is the one place that clears it.
    /// </para>
    /// <para>
    /// On a fresh mission this loop does nothing at all, because every group
    /// a mission starts with has its request outstanding — which is why the
    /// seed-1 workload's hashes are untouched by it. Task 90 measured what
    /// its absence cost: an operator resumed mid-walk lost its path entirely
    /// and stood still from the resume onward, while the run that never
    /// stopped kept walking.
    /// </para>
    /// </remarks>
    private void RecomputePublishedPaths()
    {
        var groups = State.Groups;
        if (groups.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var group in groups)
        {
            if (group.HasOutstandingRequest)
            {
                continue;
            }

            _pathService.RestorePublishedPath(
                group.GroupId, group.StartCellIndex, group.GoalCellIndex, group.RequestTick,
                _navGrid, _pathBlockedCells, _wallBuckets);
        }
    }

    /// <summary>The mission this simulation is ticking.</summary>
    public Mission Mission => _mission;

    /// <summary>The ruleset this simulation was constructed with.</summary>
    public SandataRuleset Ruleset => _ruleset;

    /// <summary>The current authoritative state, as of the last completed stage.</summary>
    public MissionState State { get; private set; }

    /// <summary>
    /// The only production-facing door onto the private <see cref="_pathService"/>
    /// field: <paramref name="groupId"/>'s current published, line-of-sight-
    /// smoothed path polyline, delegating verbatim to
    /// <see cref="PathService.GetCurrentPath"/>. Returns
    /// <see cref="ImmutableArray{T}.Empty"/> — never <see langword="null"/>,
    /// never a throw — for a group id with no outstanding or published
    /// request, exactly as <see cref="PathService.GetCurrentPath"/> already
    /// does for one.
    /// </summary>
    /// <remarks>
    /// <b>DERIVED — never hashed, never snapshotted.</b> The returned
    /// polyline is design section 4's "published path polylines" under "what
    /// is derived and never hashed": a pure function of the nav data, the
    /// group's start cell, and its goal cell. Only the request that produced
    /// it — <see cref="MissionState.Groups"/>' <see cref="GroupPathState"/>
    /// entries — is authoritative, hashed, and snapshotted. On resume this
    /// polyline is recomputed from that stored request by
    /// <see cref="RecomputePublishedPaths"/> before the first tick executes,
    /// so a caller must never persist a value read from this accessor: doing
    /// so would create a second, driftable copy of state this simulation
    /// already reconstructs on its own.
    /// </remarks>
    public ImmutableArray<PathPoint> GetPublishedPath(ulong groupId) => _pathService.GetCurrentPath(groupId);

    /// <summary>
    /// Companion to <see cref="GetPublishedPath"/>: why
    /// <paramref name="groupId"/>'s published path currently looks the way it
    /// does, delegating verbatim to <see cref="PathService.GetReasonCode"/>.
    /// Costs nothing beyond that call — no extra state to maintain, no extra
    /// query to run — and is exactly as derived as <see cref="GetPublishedPath"/>
    /// itself: never hashed, never snapshotted, and safe to read every tick.
    /// </summary>
    public PathReasonCode GetPublishedPathReasonCode(ulong groupId) => _pathService.GetReasonCode(groupId);

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
    /// Stage 14's scheduled state hash, from the most recent tick whose
    /// <c>currentTick % Mission.TickPolicy.StateHashCadenceTicks == 0</c> —
    /// see <see cref="ComputeStateHash"/>. <see langword="null"/> until the
    /// first scheduled tick has run.
    /// </summary>
    public ulong? LastStateHash { get; private set; }

    /// <summary>
    /// The only production submission door into <see cref="OrderQueue"/> this
    /// simulation exposes — call-site obligation for stage 1: wraps
    /// <see cref="OrderQueue.SubmitValidated"/>, the only public submission
    /// door on that type (<c>OrderQueue.Submit</c> is deliberately private).
    /// The submitted order, if accepted, becomes visible to stage 1's
    /// <see cref="ApplyOrders"/> starting on the tick <see cref="RunTick"/>
    /// is next called for a tick equal to <paramref name="targetTick"/>.
    /// </summary>
    /// <remarks>
    /// Task 76 (docs/plans/2026-08-07-sandata-scaffold.md): when
    /// <see cref="OrderQueue.SubmitValidated"/> reports a rejection, this
    /// method emits a <see cref="MissionEventKind.OrderRejected"/> event
    /// through <see cref="EmitOrderRejectedEvent"/> before returning — design
    /// section 16, "An order is validated when it is submitted, not when it
    /// is applied, so the player learns immediately." The event is emitted
    /// here, at submission, not deferred to stage 14.
    /// </remarks>
    public (OrderQueue Queue, Order? Submitted, OrderRejection? Rejection) SubmitOrder(
        long targetTick,
        int factionId,
        ImmutableArray<ulong> addressees,
        OrderKind kind,
        ImmutableArray<OrderPathNode> pathNodes = default)
    {
        var result = State.OrderQueue.SubmitValidated(
            targetTick, factionId, addressees, kind, _navGrid, _wallBuckets, pathNodes);

        var state = State with { OrderQueue = result.Queue };
        if (result.Rejection is { } rejection)
        {
            state = EmitOrderRejectedEvent(state, rejection);
        }

        State = state;
        return result;
    }

    /// <summary>
    /// Task 76's event-emission call site for a rejected order — design
    /// section 16: "A rejected order emits an authoritative event carrying
    /// the order id and a reason code. It is not silently dropped." Assigns
    /// the event <paramref name="state"/>'s current
    /// <see cref="MissionState.NextEventSequence"/> and advances that
    /// counter by one, the same "assign then advance" shape every other
    /// authoritative counter in this class already follows.
    /// </summary>
    private static MissionState EmitOrderRejectedEvent(MissionState state, OrderRejection rejection)
    {
        var missionEvent = MissionEvent.OrderRejected(
            state.NextEventSequence, state.Tick, rejection.OrderId, rejection.Reason);

        return state with
        {
            NextEventSequence = state.NextEventSequence + 1,
            EventFeed = state.EventFeed.Append(missionEvent),
        };
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
    /// Runs all fourteen stages of <see cref="TickStage"/>'s table, in that
    /// exact numeric order, for <paramref name="currentTick"/>. See this
    /// class's per-stage method remarks for exactly which stages are
    /// complete, partial, or blocked on a missing callee.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why <see cref="MissionState.Tick"/> is written before stage 1 and
    /// not after stage 14.</b> Authoritative time is an integer tick
    /// (<c>CLAUDE.md</c> section 5), and the tick a running mission is on is
    /// authoritative state that design section 4 lists as hashed and
    /// snapshotted — it is the first field
    /// <see cref="Determinism.SandataStateHasher"/> folds and the first field
    /// <see cref="MissionSnapshot"/> carries. Every stage below that stamps a
    /// <see cref="Events.MissionEvent"/> reads <c>State.Tick</c> rather than
    /// taking <paramref name="currentTick"/> as a parameter, so the write has
    /// to land before the first stage that can emit one. Stage 1 emits
    /// <see cref="Events.MissionEvent.OrderRejected"/>, so "before stage 1" is
    /// the only placement under which an event's own tick stamp is the tick it
    /// happened on. After <see cref="RunTick"/> returns,
    /// <c>State.Tick == currentTick</c>: the tick just executed, not the tick
    /// to execute next.
    /// </para>
    /// <para>
    /// This assignment is what makes that field non-constant. Until
    /// 2026-08-11 nothing in this assembly ever wrote it, so it stayed 0 for
    /// the whole of every run: the hasher folded a constant, every event
    /// carried tick 0 regardless of when it fired, and
    /// <c>Sandata.Headless.HeadlessRunner</c>'s per-tick divergence check
    /// compared 0 against 0. Fixing it moves every recorded Sandata state
    /// hash, which is why <c>Fixtures/seed-1-baseline.json</c> and the seed-1
    /// baseline in <c>docs/development/testing.md</c> were re-measured in the
    /// same change. It is deliberately <em>not</em> a new
    /// <see cref="Rules.SandataPresetId"/>: design section 4's trigger list is
    /// an enum value or order, a roster order, a weapon weight, the tick rate,
    /// the millisecond conversion rule, or a hash mixer, and a defect in this
    /// method is none of them — <see cref="Rules.SandataRuleset.ContentHash"/>
    /// is unchanged by it.
    /// </para>
    /// </remarks>
    public void RunTick(long currentTick)
    {
        // Before stage 1 — see this method's remarks. Stage 1 can emit an
        // event, and every event stamps itself from State.Tick.
        State = State with { Tick = currentTick };

        // Stage 1.
        State = ApplyOrders(State, currentTick);

        // Stage 2.
        State = ApplySpawnAndDespawn(State);

        // Stage 3.
        var bodies = BuildCollisionBodies(State);
        _contactGrid.Rebuild(bodies, CollisionBodyRadiusRaw);
        var cohesionRadiusRaw = RawFromWorldUnits(_ruleset.GroupCohesionRadiusWu);
        _cohesionGrid.RebuildWithinRange(bodies, cohesionRadiusRaw);
        var view = CaptureTickStartView(State, _contactGrid, _cohesionGrid);

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

        // Stage 9. Takes stage 8's intents and stage 5's evaluated sensing so
        // an operator that is in a position to shoot stands still to do it —
        // see ComputeMovementProposals and IsHaltedToEngage.
        PendingMovementProposals = ComputeMovementProposals(view, slots, State, PendingIntents, sensing);

        view.Release();

        // Stage 5's deferred commit: safe now that no stage 5-through-9
        // reader can observe it mid-range.
        State = CommitSensing(State, sensing);

        // Stage 10. Reads only PendingMovementProposals and State — the
        // frozen view above was already released before this line runs, and
        // none of stages 10 through 14 below ever takes a TickStartView
        // parameter, so there is structurally nothing for them to read from
        // it even by mistake.
        ResolveLocalAvoidanceAndCollision();

        // Stage 11.
        AdvanceWeaponChain();

        // Stage 12.
        var damageInstances = ProposeFire();

        // Stage 13.
        ResolveDamage(damageInstances);

        // Stage 14.
        ComputeStateHash(currentTick);
    }

    // ---- Stage 10 through 14. ----------------------------------------------

    /// <summary>
    /// Stage 10, <see cref="TickStage.LocalAvoidanceAndCollision"/>. Resolves
    /// and commits stage 9's <see cref="PendingMovementProposals"/> in one
    /// call to <see cref="Movement.LocalAvoidance.Commit"/> — propose,
    /// prioritise, and commit (including the one sidestep retry) are all
    /// inside that call, per its own remarks; this method only writes the
    /// settled positions back into <see cref="State"/>. Takes no
    /// <see cref="TickStartView"/> parameter — every position this method
    /// needs already lives in <see cref="PendingMovementProposals"/>,
    /// captured before the view was released.
    /// </summary>
    internal void ResolveLocalAvoidanceAndCollision()
    {
        var proposals = PendingMovementProposals;
        if (proposals.IsDefaultOrEmpty)
        {
            return;
        }

        _localAvoidance ??= new LocalAvoidance(CollisionCellSizeRaw, CollisionBodyRadiusRaw);
        var results = _localAvoidance.Commit(proposals);

        var operators = State.Operators;
        var builder = ImmutableArray.CreateBuilder<OperatorState>(operators.Length);

        foreach (var op in operators)
        {
            var committed = op;
            foreach (var result in results)
            {
                if (result.EntityId == op.EntityId)
                {
                    committed = op with
                    {
                        PositionX = FixedPoint.FromRaw(result.CommittedXRaw),
                        PositionY = FixedPoint.FromRaw(result.CommittedYRaw),
                    };
                    break;
                }
            }

            builder.Add(committed);
        }

        State = State with { Operators = builder.MoveToImmutable() };
    }

    /// <summary>
    /// Stage 11's per-tick record of one operator's completed shot: who fired
    /// and at whom, for <see cref="ProposeFire"/> to resolve.
    /// </summary>
    /// <param name="Mode">
    /// The single mode <see cref="FireModeSelection.SelectMode"/> resolved for
    /// this round, carried through to <see cref="MissionEvent.ShotFired"/>'s
    /// reason code — design section 9: "the simulation picks the mode, and the
    /// mode picks the sound slot."
    /// </param>
    private readonly record struct FiredShot(
        ulong ShooterEntityId, ulong TargetEntityId, FireModeSet Mode);

    /// <summary>
    /// Stage 11, <see cref="TickStage.WeaponChain"/>. Advances every living
    /// operator's weapon chain by exactly one tick via
    /// <see cref="WeaponChain.Advance"/>, writes the updated phase, remaining
    /// ticks, and aim angle back into <see cref="State"/>, and records every
    /// operator whose shot completed this tick into
    /// <see cref="_pendingFiredShots"/> for <see cref="ProposeFire"/>. Reads
    /// only <see cref="State"/> (this tick's already-committed positions,
    /// health, and contact memory) and <see cref="PendingIntents"/> (stage
    /// 8's output, index-aligned to <see cref="MissionState.Operators"/>
    /// since both are built from the same tick-start-view order with no
    /// liveness filter — see <see cref="SelectIntents"/>); it never takes a
    /// <see cref="TickStartView"/> parameter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Call-site obligations.</b> <see cref="WeaponLoweredRules.IsForcedLowered"/>
    /// is passed <see cref="SandataRuleset.LoweredWallDistanceWu"/>. The
    /// <c>arcWithinTolerance</c> argument to <see cref="WeaponChain.Advance"/>
    /// is computed by <see cref="WeaponChain.IsArcWithinTolerance"/> against
    /// <see cref="SandataRuleset.AimToleranceBam"/> — closing the "read by
    /// nothing" finding wave 9 left on that field.
    /// </para>
    /// <para>
    /// <b>Target bearing.</b> <c>raiseRequested</c> is this operator's stage-8
    /// <see cref="OperatorIntent.Engage"/> selection. When raising, this
    /// method reads the operator's own committed
    /// <see cref="OperatorState.ContactMemory"/> for its highest-tier entry
    /// (ties break on the lower <see cref="ContactMemoryEntry.EnemyEntityId"/>),
    /// resolves that contact's live committed position from
    /// <see cref="State"/> — not the remembered cell, which is all
    /// <see cref="ContactMemoryEntry"/> itself carries — and turns the aim
    /// point toward it by at most <see cref="FirearmDefinition.TurnBamPerTick"/>
    /// of raw <see cref="Bam16"/> magnitude. <see cref="WeaponChain.Advance"/>
    /// only counts phases; its own remarks are explicit that nothing inside
    /// it rotates anything, so this call site is the one place that has to
    /// perform that per-tick turn during <see cref="WeaponChainPhase.Turning"/>.
    /// </para>
    /// <para>
    /// If the operator is raising but its best remembered contact has since
    /// died or left the roster, <c>arcWithinTolerance</c> stays
    /// <see langword="false"/> for the whole tick — the chain holds at
    /// <see cref="WeaponChainPhase.Turning"/> rather than firing at nothing.
    /// </para>
    /// <para>
    /// <b>Per-operator loadout.</b> Task 79c
    /// (docs/plans/2026-08-07-sandata-scaffold.md, the wave-12 audit's
    /// corrected obligation): each operator's own
    /// <see cref="OperatorState.Firearm"/> selects its
    /// <see cref="FirearmCatalog"/> row, looked up once per operator inside
    /// the loop below rather than once for the whole tick, so two operators
    /// carrying different firearms advance different timing chains.
    /// </para>
    /// </remarks>
    internal void AdvanceWeaponChain()
    {
        var operators = State.Operators;
        if (operators.IsDefaultOrEmpty)
        {
            _pendingFiredShots = ImmutableArray<FiredShot>.Empty;
            return;
        }

        var intents = PendingIntents;

        var updated = ImmutableArray.CreateBuilder<OperatorState>(operators.Length);
        var fired = ImmutableArray.CreateBuilder<FiredShot>();
        var loweredTransitions = ImmutableArray.CreateBuilder<(ulong EntityId, bool Lowered)>();

        for (var i = 0; i < operators.Length; i++)
        {
            var op = operators[i];

            if (!DamageResolution.IsAlive(op.Health))
            {
                updated.Add(op);
                continue;
            }

            var definition = FirearmCatalog.Rows[(int)op.Firearm];
            var readyTicks = TickConversion.ToTicks(definition.ReadyMs, _ruleset.TickRate);
            var resetTicks = TickConversion.ToTicks(definition.ResetMs, _ruleset.TickRate);

            var positionXWu = WorldUnits.FromFixedPoint(op.PositionX);
            var positionYWu = WorldUnits.FromFixedPoint(op.PositionY);

            var forceLowered = WeaponLoweredRules.IsForcedLowered(
                positionXWu, positionYWu, _navGrid, _wallBuckets,
                _ruleset.LoweredWallDistanceWu, definition.ExemptFromLoweredRule);

            var raiseRequested = i < intents.Length && intents[i].Intent == OperatorIntent.Engage;

            var aimAngle = op.AimAngle;
            var arcWithinTolerance = false;
            var offCentreBam = 0;
            ulong? targetEntityId = null;
            var targetRangeWu = 0;

            if (raiseRequested &&
                TryFindBestContact(op.ContactMemory, out var contactId) &&
                TryFindOperatorIndex(operators, contactId, out var targetIndex))
            {
                var target = operators[targetIndex];
                var targetXWu = WorldUnits.FromFixedPoint(target.PositionX);
                var targetYWu = WorldUnits.FromFixedPoint(target.PositionY);
                var bearing = new Bam16(Cordic.Atan2(targetYWu - positionYWu, targetXWu - positionXWu));

                // The engagement range design section 9's band rule selects a
                // fire mode from. Measured here, from the same committed
                // positions the bearing above is measured from, rather than
                // re-measured in stage 12 — a mode chosen from one tick's
                // range and a shot resolved against another's would disagree
                // with itself on the tick a target crosses a band boundary.
                var rangeDeltaXWu = targetXWu - positionXWu;
                var rangeDeltaYWu = targetYWu - positionYWu;
                targetRangeWu = IntegerSqrt(
                    checked((rangeDeltaXWu * rangeDeltaXWu) + (rangeDeltaYWu * rangeDeltaYWu)));

                var arc = Bam16.ShortestArc(aimAngle, bearing);
                offCentreBam = Math.Abs((int)arc);

                var step = Math.Clamp((int)arc, -definition.TurnBamPerTick, definition.TurnBamPerTick);
                aimAngle = new Bam16(unchecked((ushort)(aimAngle.Raw + step)));

                arcWithinTolerance = WeaponChain.IsArcWithinTolerance(
                    aimAngle, bearing, _ruleset.AimToleranceBam);
                targetEntityId = contactId;
            }

            var aimMs = definition.AimBaseMs + checked((definition.AimPerBamMs * offCentreBam) / 1024);
            var aimTicks = TickConversion.ToTicks(Math.Max(aimMs, 0), _ruleset.TickRate);

            var result = WeaponChain.Advance(
                (WeaponChainPhase)op.WeaponChainPhase,
                op.WeaponChainRemainingTicks,
                forceLowered,
                raiseRequested,
                arcWithinTolerance,
                readyTicks,
                aimTicks,
                resetTicks);

            // Design section 9: the lowered flag is hashed state. Until
            // 2026-08-12 this loop computed forceLowered, handed it to the
            // chain, and threw it away: OperatorState.WeaponLowered was folded
            // into the state hash on every tick of every run while never once
            // being assigned, so it was a constant false and no renderer could
            // have drawn it even if one had tried to.
            //
            // It is the rule's own output, not the resolved chain phase.
            // Deriving it from the phase was tried first and is wrong for this
            // field: WeaponChainPhase.Lowered is also the phase of an operator
            // who simply has not raised yet, which is every operator with no
            // contact, so the flag would read true for almost everybody almost
            // always and the doorway transition smoke row SD-4 asks a person
            // to watch would be invisible inside it. This field means "the
            // weapon-lowered rule is forcing this weapon down", which is
            // exactly what design section 9 makes observable, and it is false
            // for a pistol at all times because that rule exempts one.
            var lowered = forceLowered;
            if (lowered != op.WeaponLowered)
            {
                loweredTransitions.Add((op.EntityId, lowered));
            }

            var (shotCount, cyclicAccumulator, selectedMode) = ResolveShotsThisTick(
                op, definition, result, targetEntityId, targetRangeWu, arcWithinTolerance, lowered);

            updated.Add(op with
            {
                WeaponChainPhase = (int)result.Phase,
                WeaponChainRemainingTicks = result.RemainingTicks,
                AimAngle = aimAngle,
                WeaponLowered = lowered,
                CyclicFireAccumulator = cyclicAccumulator,
            });

            if (targetEntityId is ulong firedTargetId && selectedMode is { } mode)
            {
                for (var shot = 0; shot < shotCount; shot++)
                {
                    fired.Add(new FiredShot(op.EntityId, firedTargetId, mode));
                }
            }
        }

        State = State with { Operators = updated.MoveToImmutable() };
        _pendingFiredShots = fired.ToImmutable();

        // Emitted after the loop, in ascending operator order, because the
        // loop walks MissionState.Operators (already ascending by EntityId)
        // and because an event's sequence number must not depend on which
        // half of a tick a state write happened in.
        var state = State;
        foreach (var (entityId, lowered) in loweredTransitions)
        {
            state = EmitWeaponLoweredEvent(state, entityId, lowered);
        }

        State = state;
    }

    /// <summary>
    /// Stage 11's fire-mode and cadence decision for one operator on one tick:
    /// how many rounds leave the barrel, what the operator's cyclic-fire
    /// accumulator holds going into the next tick, and which
    /// <see cref="FireModeSet"/> mode those rounds were fired under.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Mode selection.</b> Design section 9's ordered band rule, through
    /// <see cref="FireModeSelection.SelectMode"/>, against the firearm's own
    /// bands and the range measured this tick. A <see langword="null"/> result
    /// is the rule's "no engagement" outcome — the target is beyond the
    /// weapon's single-fire band — and produces no round even on a tick the
    /// chain itself resolved one. Until 2026-08-12 this rule had no production
    /// caller at all: every shot in the game came out of the weapon chain's
    /// own <c>Aiming → Firing → Resetting</c> cycle, one round per cycle, for
    /// every weapon at every range, so no weapon in the roster had ever fired
    /// automatically.
    /// </para>
    /// <para>
    /// <b>Automatic cadence.</b> For <see cref="FireModeSet.Auto"/> the chain
    /// still owns ready, turn, and aim, and its own first resolved shot is the
    /// burst's first round. From then on
    /// <see cref="CyclicFireAccumulator.Advance"/> — design section 9's
    /// driftless per-round scheduler — produces the subsequent rounds at the
    /// firearm's <see cref="FirearmDefinition.CyclicRpm"/>, for as long as the
    /// operator is still aimed within tolerance and not lowered. Section 9:
    /// "automatic fire stops when the magazine empties, the target leaves the
    /// cone, or the intent changes; there is no burst length random draw."
    /// Magazine depletion is the one of those three not tested here, because
    /// nothing in this worktree consumes
    /// <see cref="OperatorState.MagazineRounds"/> yet.
    /// </para>
    /// <para>
    /// <b>The accumulator is the burst latch.</b> It resets to zero on any
    /// tick the operator is not sustaining automatic fire, and
    /// <see cref="CyclicFireAccumulator.Advance"/> never returns zero once
    /// started at this tick rate, so a non-zero accumulator means "a burst is
    /// in progress" without a second stored flag to keep in step with it.
    /// </para>
    /// </remarks>
    private (int ShotCount, int CyclicAccumulator, FireModeSet? Mode) ResolveShotsThisTick(
        OperatorState op,
        FirearmDefinition definition,
        WeaponChainAdvanceResult result,
        ulong? targetEntityId,
        int targetRangeWu,
        bool arcWithinTolerance,
        bool lowered)
    {
        if (targetEntityId is null)
        {
            return (0, 0, null);
        }

        var mode = FireModeSelection.SelectMode(
            definition.Modes,
            targetRangeWu,
            definition.AutoBandMaxWu,
            definition.BurstBandMaxWu,
            definition.SingleBandMaxWu);

        if (mode is not { } selectedMode)
        {
            return (0, 0, null);
        }

        var shotCount = result.Fired ? 1 : 0;

        if (selectedMode != FireModeSet.Auto || lowered || !arcWithinTolerance)
        {
            return (shotCount, 0, selectedMode);
        }

        if (!result.Fired && op.CyclicFireAccumulator == 0)
        {
            // Aimed, in an automatic band, but the chain has not yet resolved
            // the burst's first round. Nothing to sustain.
            return (shotCount, 0, selectedMode);
        }

        var advance = CyclicFireAccumulator.Advance(
            op.CyclicFireAccumulator, _ruleset.TickRate, definition.CyclicRpm);

        if (!result.Fired)
        {
            shotCount = advance.ShotsFired;
        }

        return (shotCount, advance.Accumulator, selectedMode);
    }

    /// <summary>
    /// Finds <paramref name="memory"/>'s highest-<see cref="ContactTier"/>
    /// entry, ties broken toward the lower
    /// <see cref="ContactMemoryEntry.EnemyEntityId"/> for a stable, total
    /// order. Returns <see langword="false"/> when every entry is
    /// <see cref="ContactTier.Unknown"/> or <paramref name="memory"/> is
    /// empty.
    /// </summary>
    private static bool TryFindBestContact(ImmutableArray<ContactMemoryEntry> memory, out ulong contactId)
    {
        contactId = 0;
        if (memory.IsDefaultOrEmpty)
        {
            return false;
        }

        var found = false;
        var bestTier = ContactTier.Unknown;

        foreach (var entry in memory)
        {
            var tier = (ContactTier)entry.ContactTier;
            if (tier == ContactTier.Unknown)
            {
                continue;
            }

            if (!found || tier > bestTier || (tier == bestTier && entry.EnemyEntityId < contactId))
            {
                found = true;
                bestTier = tier;
                contactId = entry.EnemyEntityId;
            }
        }

        return found;
    }

    /// <summary>
    /// Binary search for <paramref name="entityId"/> within
    /// <paramref name="operators"/>, which <see cref="MissionState.Operators"/>'s
    /// own remarks guarantee stays ascending by
    /// <see cref="OperatorState.EntityId"/> — the same flat-sorted-array
    /// convention <see cref="FindAssignment"/> already uses, in place of a
    /// banned <c>Dictionary&lt;&gt;</c>.
    /// </summary>
    private static bool TryFindOperatorIndex(
        ImmutableArray<OperatorState> operators, ulong entityId, out int index)
    {
        var low = 0;
        var high = operators.Length - 1;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var candidateId = operators[mid].EntityId;

            if (candidateId == entityId)
            {
                index = mid;
                return true;
            }

            if (candidateId < entityId)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        index = -1;
        return false;
    }

    /// <summary>
    /// Stage 12, <see cref="TickStage.FireProposal"/>. Resolves each of stage
    /// 11's <see cref="_pendingFiredShots"/> into a <see cref="DamageInstance"/>,
    /// using the shooter's and target's committed positions from
    /// <see cref="State"/>. Takes no <see cref="TickStartView"/> parameter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is real here.</b> Range is measured from committed positions;
    /// <see cref="AccuracyRules.Dispersion"/> and
    /// <see cref="AccuracyRules.DrawAngularErrorBam"/> are both called with
    /// real arguments, from the real <c>Accuracy</c> RNG stream keyed on
    /// <see cref="Mission.Seed"/> and the shooter's entity id. Task 79d-1
    /// (docs/plans/2026-08-07-sandata-scaffold.md, the wave-12 audit's
    /// corrected obligation) makes that draw decide hit or miss: it is
    /// compared against the target's subtended half-angle at the measured
    /// range, computed by <see cref="SubtendedHalfAngleBam"/> from
    /// <see cref="CollisionBodyRadiusRaw"/> and <paramref name="rangeWu"/> —
    /// see that method's remarks for the exact unit conversion. A miss skips
    /// the <see cref="DamageInstance"/> entirely; a hit produces the same one
    /// this method always produced before this task.
    /// <see cref="CoverRules.ApplyToDamage"/> is called for real, against the
    /// target's real <see cref="CoverState"/> resolved by
    /// <see cref="ResolveCoverState"/> (see next), only for a hit. Task
    /// 79d-2a (docs/plans/2026-08-07-sandata-scaffold.md) resolves
    /// <see cref="FirearmDefinition"/> from each shot's own shooter's
    /// <see cref="OperatorState.Firearm"/>, inside this loop, the same shape
    /// stage 11's <see cref="AdvanceWeaponChain"/> already used — before that
    /// task every shot in the game used <see cref="FirearmId.Ak47"/>'s
    /// dispersion regardless of what the shooter actually carried, which made
    /// a miss mathematically unreachable within
    /// <see cref="Sensing.ContactMemory.DetectRangeWu"/> (see
    /// <see cref="SubtendedHalfAngle_AlwaysAtLeast_AkDispersion_WithinDetectRange"/>
    /// in <c>TickPipelineTests</c>).
    /// </para>
    /// <para>
    /// <b>Task 79d-2b, cover half.</b> The target's <see cref="CoverState"/>
    /// now comes from <see cref="ResolveCoverState"/>, which looks up the
    /// target's position against this simulation's real
    /// <see cref="_coverRecords"/> rather than the constant
    /// <see cref="CoverState.NotInCover"/> every shot resolved against
    /// before this task — exact map geometry, not an invented placeholder.
    /// The damage a hit deals comes from
    /// <see cref="CaliberDamage.RawDamage"/>, keyed on the shooter's own
    /// <see cref="FirearmDefinition.Caliber"/> — resolved from
    /// <see cref="OperatorState.Firearm"/> by task 79d-2a — rather than the
    /// single flat constant every hit dealt before this task. Those eight
    /// values are provisional and say so at their own declaration; what is
    /// no longer provisional is that the weapon an operator carries decides
    /// how hard its round lands.
    /// </para>
    /// <para>
    /// <see cref="AccuracyRules.DrawAngularErrorBam"/> takes a
    /// <see langword="ulong"/> entity id, matching
    /// <see cref="OperatorState.EntityId"/> exactly — task 78 widened it, so
    /// this call site no longer narrows.
    /// </para>
    /// <para>
    /// <b>Events.</b> Every shot this method resolves — hit or miss — emits
    /// <see cref="MissionEventKind.ShotFired"/>, immediately followed by
    /// <see cref="MissionEventKind.ShotHit"/> or
    /// <see cref="MissionEventKind.ShotMissed"/>, through the same "assign
    /// then advance <see cref="MissionState.NextEventSequence"/>" shape
    /// <see cref="EmitOrderRejectedEvent"/> already uses. A shot skipped by
    /// the continues above — shooter or target not found, or target already
    /// dead this tick — emits nothing, matching this method's pre-79d-1
    /// behaviour of producing no <see cref="DamageInstance"/> for those
    /// cases either.
    /// </para>
    /// </remarks>
    internal ImmutableArray<DamageInstance> ProposeFire()
    {
        var firedShots = _pendingFiredShots;
        if (firedShots.IsDefaultOrEmpty)
        {
            return ImmutableArray<DamageInstance>.Empty;
        }

        var operators = State.Operators;
        var damageBuilder = ImmutableArray.CreateBuilder<DamageInstance>(firedShots.Length);
        var state = State;

        foreach (var shot in firedShots)
        {
            if (!TryFindOperatorIndex(operators, shot.ShooterEntityId, out var shooterIndex) ||
                !TryFindOperatorIndex(operators, shot.TargetEntityId, out var targetIndex))
            {
                continue;
            }

            var shooter = operators[shooterIndex];
            var target = operators[targetIndex];

            if (!DamageResolution.IsAlive(target.Health))
            {
                continue;
            }

            var definition = FirearmCatalog.Rows[(int)shooter.Firearm];

            var shooterXWu = WorldUnits.FromFixedPoint(shooter.PositionX);
            var shooterYWu = WorldUnits.FromFixedPoint(shooter.PositionY);
            var targetXWu = WorldUnits.FromFixedPoint(target.PositionX);
            var targetYWu = WorldUnits.FromFixedPoint(target.PositionY);

            var dx = targetXWu - shooterXWu;
            var dy = targetYWu - shooterYWu;
            var rangeSquaredWu = checked((dx * dx) + (dy * dy));
            var rangeWu = IntegerSqrt(rangeSquaredWu);

            var dispersionBam = AccuracyRules.Dispersion(
                rangeWu, definition.DispersionAtZeroWu, definition.DispersionAtMaxWu, definition.MaxEffectiveWu);

            var angularErrorBam = AccuracyRules.DrawAngularErrorBam(
                _mission.Seed, shooter.EntityId, dispersionBam);

            var halfAngleBam = SubtendedHalfAngleBam(rangeWu);
            var isHit = Math.Abs(angularErrorBam) <= halfAngleBam;

            state = EmitShotFiredEvent(state, shooter.EntityId, shot.Mode);

            if (isHit)
            {
                var targetCover = ResolveCoverState(target, targetXWu, targetYWu);
                var damage = CoverRules.ApplyToDamage(
                    CaliberDamage.RawDamage(definition.Caliber), targetCover,
                    shooterXWu, shooterYWu, targetXWu, targetYWu);

                damageBuilder.Add(new DamageInstance(shooter.EntityId, target.EntityId, damage));
                state = EmitShotHitEvent(state, shooter.EntityId);
            }
            else
            {
                state = EmitShotMissedEvent(state, shooter.EntityId);
            }
        }

        State = state;
        return damageBuilder.ToImmutable();
    }

    /// <summary>
    /// Task 79d-2b: the real <see cref="CoverState"/> a shot at
    /// <paramref name="target"/> resolves against, looked up from this
    /// simulation's <see cref="_coverRecords"/> rather than the constant
    /// <see cref="CoverState.NotInCover"/> every shot resolved against
    /// before this task.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Which record.</b> A <see cref="CoverRecord"/> contains
    /// <paramref name="targetXWu"/>, <paramref name="targetYWu"/> when both
    /// fall within its closed rectangle
    /// <c>[MinX, MaxX] x [MinY, MaxY]</c> — the same inclusive-bounds
    /// convention <c>MapValidator.IsInsideClosedBox</c> already uses
    /// elsewhere in this codebase for the same "is this point inside this
    /// map rectangle" question. When more than one record contains the
    /// target, this is a multi-result query, and design section 4's total-
    /// order rule (<c>CLAUDE.md</c> section 5) applies: the record with the
    /// lowest <see cref="CoverRecord.LineNumber"/> wins, an arbitrary but
    /// total and stable tie-break drawn directly from the map file's own
    /// line order, never from iteration order over <see cref="_coverRecords"/>.
    /// </para>
    /// <para>
    /// <b>No containing record.</b> Returns <see cref="CoverState.NotInCover"/>
    /// unchanged — a target standing outside every cover rectangle is
    /// exactly as exposed as it was before this task.
    /// </para>
    /// <para>
    /// <b>Posture.</b> A containing record's
    /// <see cref="CoverRecord.ArcCentreBam"/> and
    /// <see cref="CoverRecord.ArcHalfBam"/> copy unchanged into
    /// <see cref="CoverState.ArcCentreBam"/> and
    /// <see cref="CoverState.ArcHalfBam"/>; <see cref="CoverState.Posture"/>
    /// reads <paramref name="target"/>'s own
    /// <see cref="OperatorState.IsCrouched"/> — design section 4's "posture
    /// (standing or crouched)" is per-operator authoritative state, not
    /// per-cover-object, exactly as <see cref="CoverState"/>'s own remarks
    /// describe two operators behind the same object each holding their own
    /// state.
    /// </para>
    /// </remarks>
    private CoverState ResolveCoverState(OperatorState target, long targetXWu, long targetYWu)
    {
        CoverRecord? containing = null;

        foreach (var record in _coverRecords)
        {
            var isInside =
                targetXWu >= record.MinX && targetXWu <= record.MaxX &&
                targetYWu >= record.MinY && targetYWu <= record.MaxY;

            if (!isInside)
            {
                continue;
            }

            if (containing is null || record.LineNumber < containing.LineNumber)
            {
                containing = record;
            }
        }

        if (containing is null)
        {
            return CoverState.NotInCover;
        }

        return new CoverState(
            InCover: true,
            ArcCentreBam: new Bam16((ushort)containing.ArcCentreBam),
            ArcHalfBam: (ushort)containing.ArcHalfBam,
            Posture: target.IsCrouched ? CoverPosture.Crouched : CoverPosture.Standing);
    }

    /// <summary>
    /// Task 79d-1's hit-resolution geometry: the half-angle, in raw
    /// <see cref="Bam16"/> units, that the target's collision body subtends
    /// as seen from the shooter at <paramref name="rangeWu"/> — the standard
    /// small-body approximation <c>atan(radius / range)</c>, computed exactly
    /// by <see cref="Cordic.Atan2"/> rather than a banned floating-point
    /// arctangent. <see cref="ProposeFire"/> compares this against the
    /// magnitude of the drawn angular error: within it is a hit, beyond it a
    /// miss.
    /// </summary>
    /// <remarks>
    /// <b>The unit trap this method exists to avoid.</b>
    /// <see cref="CollisionBodyRadiusRaw"/> is already a raw
    /// <see cref="FixedPoint"/> value (scale 1,024 per world unit) — it is
    /// the numerator as-is, never divided down. <paramref name="rangeWu"/>,
    /// by contrast, is a whole world-unit integer (the same one
    /// <see cref="AccuracyRules.Dispersion"/> takes), so it is the one that
    /// must be scaled up to match, via <see cref="RawFromWorldUnits"/> —
    /// exactly the inverse of the direction <see cref="WorldUnits.FromFixedPoint"/>
    /// converts positions in, a few lines above this call site. Converting
    /// <see cref="CollisionBodyRadiusRaw"/> down to world units instead would
    /// floor a 32-raw radius (already well under one world unit) straight to
    /// zero, making every shot at any nonzero range miss regardless of the
    /// drawn error — silently off by exactly the 1,024 this remark warns
    /// about.
    /// </remarks>
    /// <param name="rangeWu">
    /// The measured engagement range, in whole world units, as
    /// <see cref="IntegerSqrt"/> already produces it for this method's only
    /// caller. Never negative.
    /// </param>
    private static int SubtendedHalfAngleBam(int rangeWu) =>
        Cordic.Atan2(CollisionBodyRadiusRaw, RawFromWorldUnits(rangeWu));

    /// <summary>
    /// Task 79d-1's event-emission call site for a fired shot — the same
    /// "assign then advance <see cref="MissionState.NextEventSequence"/>"
    /// shape <see cref="EmitOrderRejectedEvent"/> already uses. Emitted once
    /// per shot <see cref="ProposeFire"/> resolves, before the hit-or-miss
    /// outcome event.
    /// </summary>
    private static MissionState EmitShotFiredEvent(MissionState state, ulong shooterEntityId, FireModeSet mode)
    {
        var missionEvent = MissionEvent.ShotFired(state.NextEventSequence, state.Tick, shooterEntityId, mode);

        return state with
        {
            NextEventSequence = state.NextEventSequence + 1,
            EventFeed = state.EventFeed.Append(missionEvent),
        };
    }

    /// <summary>
    /// Stage 11's event-emission call site for a weapon-lowered transition —
    /// the same "assign then advance
    /// <see cref="MissionState.NextEventSequence"/>" shape every other emitter
    /// in this class uses. Emitted only on the tick the stored flag actually
    /// changes, so a weapon held lowered for a hundred ticks emits one event
    /// rather than a hundred.
    /// </summary>
    private static MissionState EmitWeaponLoweredEvent(
        MissionState state, ulong operatorEntityId, bool lowered)
    {
        var missionEvent = MissionEvent.WeaponLoweredChanged(
            state.NextEventSequence, state.Tick, operatorEntityId, lowered);

        return state with
        {
            NextEventSequence = state.NextEventSequence + 1,
            EventFeed = state.EventFeed.Append(missionEvent),
        };
    }

    /// <summary>
    /// Task 79d-1's event-emission call site for a shot that connects — the
    /// same "assign then advance <see cref="MissionState.NextEventSequence"/>"
    /// shape <see cref="EmitOrderRejectedEvent"/> already uses. Emitted
    /// immediately after <see cref="EmitShotFiredEvent"/>, for the same shot.
    /// </summary>
    private static MissionState EmitShotHitEvent(MissionState state, ulong shooterEntityId)
    {
        var missionEvent = MissionEvent.ShotHit(state.NextEventSequence, state.Tick, shooterEntityId);

        return state with
        {
            NextEventSequence = state.NextEventSequence + 1,
            EventFeed = state.EventFeed.Append(missionEvent),
        };
    }

    /// <summary>
    /// Task 79d-1's event-emission call site for a shot that goes wide — the
    /// same "assign then advance <see cref="MissionState.NextEventSequence"/>"
    /// shape <see cref="EmitOrderRejectedEvent"/> already uses. Emitted
    /// immediately after <see cref="EmitShotFiredEvent"/>, for the same shot.
    /// </summary>
    private static MissionState EmitShotMissedEvent(MissionState state, ulong shooterEntityId)
    {
        var missionEvent = MissionEvent.ShotMissed(state.NextEventSequence, state.Tick, shooterEntityId);

        return state with
        {
            NextEventSequence = state.NextEventSequence + 1,
            EventFeed = state.EventFeed.Append(missionEvent),
        };
    }

    /// <summary>
    /// Deterministic pure-integer square root by binary search —
    /// <c>Math.Sqrt</c> is banned in this project (see, for example,
    /// <c>Sensing/Shadowcast.cs</c>'s own remarks on the same ban). Used only
    /// by <see cref="ProposeFire"/>, to turn a squared range into
    /// <see cref="AccuracyRules.Dispersion"/>'s linear <c>rangeWu</c> input.
    /// </summary>
    private static int IntegerSqrt(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var low = 0L;
        var high = value;

        while (low < high)
        {
            var mid = low + ((high - low + 1) / 2);
            if (mid <= value / mid)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return checked((int)low);
    }

    /// <summary>
    /// Stage 13, <see cref="TickStage.DamageResolution"/>. Applies stage 12's
    /// <paramref name="damageInstances"/> to every operator simultaneously,
    /// resolves this tick's deaths, and resolves the mission outcome — all
    /// three already-existing calls, used in the order
    /// <see cref="DamageResolution.ApplyDamage"/> (accumulate-then-apply
    /// internally) then <see cref="DamageResolution.ResolveDeaths"/> then
    /// <see cref="OutcomeRules.Resolve"/>.
    /// </summary>
    /// <remarks>
    /// <b>Simultaneity.</b> <see cref="DamageResolution.ApplyDamage"/> computes
    /// every operator's new health from that same operator's health in the
    /// <paramref name="damageInstances"/> array's <em>input</em>
    /// <c>operators</c> snapshot — never from another operator's already-
    /// updated value in the same call — so an operator whose shot this method
    /// resolves still fires this tick even if a different shot proposed the
    /// same tick kills it: both shots are scored against the tick-start
    /// roster, per <see cref="DamageResolution.ApplyDamage"/>'s own remarks.
    /// <see cref="DamageResolution.ResolveDeaths"/> is called only on the
    /// resulting fully-resolved array, per its own remarks, so two operators
    /// who mutually kill each other this tick both appear in the result.
    /// There is no <c>Events</c>-shaped type anywhere in <c>Sandata.Core</c>
    /// (confirmed by a full-project search) to publish those deaths into —
    /// see <see cref="ComputeStateHash"/>'s remarks for the same gap.
    /// </remarks>
    internal void ResolveDamage(ImmutableArray<DamageInstance> damageInstances)
    {
        var beforeDamage = State.Operators;
        if (beforeDamage.IsDefaultOrEmpty)
        {
            return;
        }

        var afterDamage = DamageResolution.ApplyDamage(beforeDamage, damageInstances);
        var deaths = DamageResolution.ResolveDeaths(beforeDamage, afterDamage);
        _ = deaths; // No Events-shaped type exists in Sandata.Core to publish these into.

        var outcome = OutcomeRules.Resolve(afterDamage);

        State = State with { Operators = afterDamage, Winner = (int)outcome };
    }

    /// <summary>
    /// Stage 14, <see cref="TickStage.StateHash"/>. Computes and stores
    /// <see cref="LastStateHash"/> via <see cref="SandataStateHasher.Compute"/>,
    /// but only on ticks the mission actually schedules — every
    /// <paramref name="currentTick"/> that is an exact multiple of
    /// <see cref="MissionTickPolicy.StateHashCadenceTicks"/>, per
    /// <see cref="TickStage.StateHash"/>'s own remarks. On every other tick
    /// this method does nothing, leaving <see cref="LastStateHash"/> holding
    /// whichever scheduled tick's value it last computed.
    /// </summary>
    /// <remarks>
    /// <b>Event emission does not happen here.</b> Task 76
    /// (docs/plans/2026-08-07-sandata-scaffold.md) adds
    /// <see cref="Events.MissionEventFeed"/> and its first producer, a
    /// rejected-order event emitted by <see cref="SubmitOrder"/> at
    /// submission time — design section 16: "An order is validated when it
    /// is submitted, not when it is applied." No stage currently emits an
    /// event during <see cref="RunTick"/> itself; a later task (79d) is
    /// expected to add stage 12/13 event emission for shot and hit outcomes
    /// once fire resolution is real. This method's own job stays exactly
    /// what it was: compute and store <see cref="LastStateHash"/> on a
    /// scheduled tick.
    /// </remarks>
    internal void ComputeStateHash(long currentTick)
    {
        var cadence = _mission.TickPolicy.StateHashCadenceTicks;
        if (cadence > 0 && currentTick % cadence != 0)
        {
            return;
        }

        LastStateHash = SandataStateHasher.Compute(_mission, State, _ruleset);
    }

    // ---- Stages 1 through 9: implemented below, one commit group at a time. ----

    /// <summary>
    /// The uniform collision grid's cell edge length, in raw fixed-point
    /// units: one body diameter, the tightest cell that still keeps
    /// <see cref="SandataCollisionGrid"/>'s fixed three-by-three neighbour
    /// scan complete. That rule is not this task's invention — it is
    /// <see cref="SandataCollisionGrid"/>'s own enforced precondition:
    /// <c>ValidateBodyRadius</c> throws whenever twice the body radius
    /// exceeds the cell size, and <c>RebuildWithinRange</c>'s remarks state
    /// the same requirement generalised to a plain range. This value is
    /// therefore <c>2 * CollisionBodyRadiusRaw</c> exactly, 8,704 raw
    /// (8.5 wu), not an independently chosen number.
    /// </summary>
    private const int CollisionCellSizeRaw = 2 * CollisionBodyRadiusRaw;

    /// <summary>
    /// The operator body radius, in raw fixed-point units, used to rebuild
    /// this tick's collision broad phase. Design section 4's unit table
    /// (docs/plans/2026-08-07-sandata-scaffold-design.md, "Units, and why
    /// they are chosen") names this value as
    /// <c>Hukbo.Core/Simulation/CollisionRules.cs:72</c>'s
    /// <c>DefaultBodyRadiusRaw</c>, <c>(17 * FixedPoint.Scale) / 4</c> =
    /// 4,352 raw = 4.25 wu = 0.266 m, unchanged from Hukbo. That file's own
    /// <c>&lt;remarks&gt;</c> record it as measured, not guessed: 4.5 wu
    /// deadlocked Hukbo's collision resolver, 4.25 and 4.125 did not, and
    /// 4.25 was kept. <c>Sandata.Core</c> may not take a
    /// <c>ProjectReference</c> on <c>Hukbo.Core</c> (design section 3), so
    /// the value is restated here rather than shared; see the wave-12 audit
    /// for the open tier-2 extraction question that duplication raises.
    /// </summary>
    private const int CollisionBodyRadiusRaw = 4352;

    /// <summary>
    /// The cell edge length, in raw fixed-point units, for the second
    /// collision grid stage 3 builds solely to source
    /// <see cref="ComputeSquadGrouping"/>'s candidate pairs. Unlike
    /// <see cref="CollisionCellSizeRaw"/> this is not a constant: task 77
    /// found that a fixed 256-raw-unit cell (roughly a quarter world unit)
    /// cannot host a cohesion-radius query, because
    /// <see cref="SandataRuleset.GroupCohesionRadiusWu"/> defaults to 96
    /// world units — 98,304 raw, four orders of magnitude past that cell
    /// edge — and <see cref="SandataCollisionGrid.RebuildWithinRange"/>'s own
    /// remarks require a cell at least as wide as the range being queried, or
    /// its 3-by-3 neighbour scan silently misses pairs sitting in a cell
    /// beyond that ring. This helper sizes the second grid's cell to the
    /// actual per-tick radius instead, so the query is complete at any
    /// configured <see cref="SandataRuleset.GroupCohesionRadiusWu"/>, not
    /// only the shipped default. <see cref="Math.Max(int, int)"/> floors the
    /// result at 1 raw unit so a (nonsensical but not rejected) zero radius
    /// never produces a zero-or-negative cell size.
    /// </summary>
    private static int CohesionCollisionCellSizeRaw(int cohesionRadiusRaw) =>
        Math.Max(cohesionRadiusRaw, 1);

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
    private MissionState ApplyOrders(MissionState state, long currentTick)
    {
        var assignments = AdvanceOrderAssignments(state);

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
    /// How close an ordered operator's committed position must come to its
    /// current authored node, in world units, before that node counts as
    /// reached. <b>PROVISIONAL:</b> design section 16 names no arrival radius,
    /// and this value is not derived from one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why it is this large rather than as tight as possible.</b> One
    /// authored order addresses several operators at once, and
    /// <see cref="ComputeMovementProposals"/>' ordered branch sends every one
    /// of them at the node itself rather than at a formation slot around it —
    /// design section 16: "An authored polyline is authoritative, not
    /// derived... never re-smoothed". <see cref="CollisionBodyRadiusRaw"/> is
    /// 4,352 raw, which is 4.25 world units, so two bodies can never approach
    /// closer than 8.5 world units centre to centre. An arrival radius under
    /// that would let the first operator of a pair arrive while the second is
    /// held off it by collision forever: never arriving, never advancing its
    /// node index, and never clearing its assignment. That permanent stall
    /// would read on screen exactly like the defect this whole sub-step exists
    /// to remove, so the radius clears two body radii with margin instead.
    /// </para>
    /// <para>
    /// Sixteen world units is one metre at design section 4's scale, and is
    /// far below any plausible spacing between two hand-drawn nodes, so a
    /// radius this wide cannot make one node swallow the next.
    /// </para>
    /// </remarks>
    private const int NodeArrivalRadiusWu = 16;

    /// <summary>
    /// Stage 1's first half: advances and clears the assignments already in
    /// flight, before <see cref="ApplyOrders"/> applies any order targeting
    /// this tick. Returns the surviving assignments, ascending by
    /// <see cref="OrderAssignment.EntityId"/> exactly as
    /// <see cref="MissionState.OrderAssignments"/> requires.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Until 2026-08-12 nothing in <c>src/</c> ever called
    /// <see cref="MovementSource.Evaluate"/> and nothing ever advanced
    /// <see cref="OrderAssignment.CurrentNodeIndex"/>, so an operator handed a
    /// drawn polyline walked to its first node and stood on it for the rest of
    /// the run — it never reached node 1, never reached the final node, and
    /// never returned to its squad's autonomous route. That is what a tester
    /// meant on 2026-08-12 by "they don't follow the key points created".
    /// </para>
    /// <para>
    /// <b>Why this runs before application rather than after it.</b> An order
    /// applied this tick creates an assignment at
    /// <see cref="OrderAssignment.CurrentNodeIndex"/> 0. Testing arrival
    /// immediately afterwards would clear a brand-new assignment on the very
    /// tick it was given whenever the operator already happens to stand within
    /// <see cref="NodeArrivalRadiusWu"/> of its own first node — a player who
    /// draws the first node under an operator's feet would watch the order
    /// evaporate. Evaluating the previous tick's assignments first removes that
    /// case entirely.
    /// </para>
    /// <para>
    /// <b>Why <c>cancelOrderApplied</c> is <see langword="false"/>.</b> Design
    /// section 16's condition 2 is handled by application rather than by
    /// evaluation: <see cref="ApplyOrders"/> clears an addressee's assignment
    /// for every <see cref="OrderKind"/> other than
    /// <see cref="OrderKind.MoveAlongPath"/>, which covers both
    /// <see cref="OrderKind.Cancel"/> and <see cref="OrderKind.Hold"/>, and it
    /// runs after this method inside the same stage. A cancel submitted for
    /// this tick is therefore honoured on this tick regardless of what this
    /// method reports.
    /// </para>
    /// <para>
    /// <b>Positions are last tick's committed positions.</b> This runs before
    /// <see cref="CaptureTickStartView"/>, reads only
    /// <see cref="MissionState.Operators"/>, and recomputes nothing.
    /// </para>
    /// </remarks>
    private ImmutableArray<OrderAssignment> AdvanceOrderAssignments(MissionState state)
    {
        var assignments = state.OrderAssignments;
        if (assignments.IsDefaultOrEmpty)
        {
            return ImmutableArray<OrderAssignment>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<OrderAssignment>(assignments.Length);

        foreach (var assignment in assignments)
        {
            var isAlive = TryFindOperatorIndex(state.Operators, assignment.EntityId, out var index) &&
                DamageResolution.IsAlive(state.Operators[index].Health);

            var advanced = assignment;
            var reachedFinalNode = false;

            if (isAlive)
            {
                advanced = AdvanceNodeIndex(assignment, state.Operators[index], out reachedFinalNode);
            }

            var evaluation = MovementSource.Evaluate(
                advanced,
                operatorIsAlive: isAlive,
                cancelOrderApplied: false,
                reachedFinalNode: reachedFinalNode,
                navGrid: _navGrid);

            if (evaluation.Assignment is { } surviving)
            {
                builder.Add(surviving);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Returns <paramref name="assignment"/> with
    /// <see cref="OrderAssignment.CurrentNodeIndex"/> advanced past every node
    /// <paramref name="operatorState"/> has already reached, and reports
    /// through <paramref name="reachedFinalNode"/> whether the polyline's last
    /// node is one of them — design section 16's condition 1, which
    /// <see cref="MovementSource.Evaluate"/> consumes as a caller-supplied
    /// fact.
    /// </summary>
    /// <remarks>
    /// The loop advances rather than stepping once so that a polyline whose
    /// consecutive nodes sit closer together than
    /// <see cref="NodeArrivalRadiusWu"/> cannot leave the operator standing
    /// between two nodes it has both already reached. It is bounded by the
    /// node count, which <see cref="Order.MaxAuthoredPathNodeCount"/> caps at
    /// submission.
    /// </remarks>
    private static OrderAssignment AdvanceNodeIndex(
        OrderAssignment assignment, OperatorState operatorState, out bool reachedFinalNode)
    {
        reachedFinalNode = false;

        var nodes = assignment.PathNodes;
        if (nodes.IsDefaultOrEmpty)
        {
            return assignment;
        }

        var lastIndex = nodes.Length - 1;
        var nodeIndex = Math.Clamp(assignment.CurrentNodeIndex, 0, lastIndex);

        while (IsAtNode(operatorState, nodes[nodeIndex]))
        {
            if (nodeIndex == lastIndex)
            {
                reachedFinalNode = true;
                break;
            }

            nodeIndex++;
        }

        return nodeIndex == assignment.CurrentNodeIndex
            ? assignment
            : assignment with { CurrentNodeIndex = nodeIndex };
    }

    /// <summary>
    /// Whether <paramref name="operatorState"/>'s committed position lies
    /// within <see cref="NodeArrivalRadiusWu"/> of <paramref name="node"/>.
    /// Compared squared, in raw fixed-point units, so no square root is taken
    /// and no world-unit truncation happens before the comparison.
    /// </summary>
    private static bool IsAtNode(OperatorState operatorState, OrderPathNode node)
    {
        var dxRaw = (long)operatorState.PositionX.RawValue - RawFromWorldUnits(node.X);
        var dyRaw = (long)operatorState.PositionY.RawValue - RawFromWorldUnits(node.Y);

        var radiusRaw = (long)NodeArrivalRadiusWu * FixedPoint.Scale;

        return checked((dxRaw * dxRaw) + (dyRaw * dyRaw)) <= checked(radiusRaw * radiusRaw);
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
    /// Stage 3. Rebuilds this tick's two collision uniform grids (already
    /// done by <see cref="RunTick"/> before calling this method — physical
    /// contact via <paramref name="grid"/>, squad cohesion range via
    /// <paramref name="cohesionGrid"/>) and freezes the tick-start view
    /// stages 5 through 9 read.
    /// </summary>
    private static TickStartView CaptureTickStartView(
        MissionState state, SandataCollisionGrid grid, SandataCollisionGrid cohesionGrid) =>
        new(state, grid.Pairs, cohesionGrid.Pairs);

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
    /// <c>GroupCohesionRadiusWu</c>, <c>LoweredWallDistanceWu</c>,
    /// <c>AimToleranceBam</c>), and design section 4 does not supply one
    /// either — section 6's row for <see cref="VisionCone.Contains"/>
    /// specifies the predicate's shape and never a half-width — so this
    /// remains a placeholder pending a real tuning pass.
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

        // One slot per remembered contact plus one per observation is
        // ContactMemory.Update's own worst case, and neither can exceed the
        // roster; sized once here rather than per operator, and grown only
        // when the roster itself has grown since the last tick.
        var requiredMergeLength = checked(count * 2);
        if (_contactMergeBuffer.Length < requiredMergeLength)
        {
            _contactMergeBuffer = new ContactMemoryEntry[requiredMergeLength];
        }

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

                if (!LineOfSight.IsVisible(
                        originX, originY, targetX, targetY, _navGrid, _wallBuckets, _sightCellBuffer))
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
                view.ContactMemory(i), observationBuffer.AsSpan(0, observationCount), currentTick,
                _contactMergeBuffer);
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
    /// overload, gated by <see cref="SandataRuleset.GroupCohesionRadiusWu"/>
    /// converted to raw fixed-point via <see cref="RawFromWorldUnits"/> (the
    /// ruleset field is documented in world units; <c>SquadGrouping</c>'s
    /// parameter is raw — task 77 found stage 6 passing the world-unit value
    /// straight through with no conversion, silently shrinking the default
    /// 96-world-unit radius to about 0.094 world units). Candidates come from
    /// <see cref="TickStartView.CohesionPairs"/>, the range query stage 3
    /// built specifically for this radius — never
    /// <see cref="TickStartView.Pairs"/>, which is filtered to physical body
    /// contact and can only narrow a candidate list, never widen it, so it
    /// can never surface two operators standing world units apart. Purely
    /// derived — nothing here is written back into <see cref="MissionState"/>,
    /// per that method's own remarks.
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

        var groupCohesionRadiusRaw = RawFromWorldUnits(_ruleset.GroupCohesionRadiusWu);

        SquadGrouping.Compute(
            view.EntityIds, isAlive, factions, xRaw, yRaw,
            groupCohesionRadiusRaw, view.CohesionPairs, slots);
    }

    /// <summary>
    /// Stage 7. Call-site obligation: for every <see cref="MissionState.Groups"/>
    /// entry whose <see cref="GroupPathState.HasOutstandingRequest"/> is
    /// <see langword="true"/>, submits it to <see cref="_pathService"/> via
    /// <see cref="PathService.RequestPath"/> (a no-op if that group already has
    /// an outstanding request in flight there — <see cref="PathService.RequestPath"/>'s
    /// own remarks), then advances <see cref="_pathService"/> by one tick
    /// against <see cref="_navGrid"/> and <see cref="_wallBuckets"/>. After
    /// <see cref="PathService.Advance"/> returns, any such group whose request
    /// published this tick (<see cref="PathService.HasOutstandingRequest"/> now
    /// <see langword="false"/>) has its <see cref="State"/> entry rewritten
    /// with <see cref="GroupPathState.HasOutstandingRequest"/> cleared, so a
    /// later tick's loop does not resubmit an already-published request.
    /// <b>This method does not decide what sets a destination</b> — it only
    /// drains and reconciles whatever <see cref="MissionState.Groups"/>
    /// already holds; no autonomous destination-request source exists in this
    /// worktree, so a fixture that never populates <see cref="MissionState.Groups"/>
    /// sees this stage's search-and-publish machinery run every tick with
    /// nothing to act on, exactly as before this task — see
    /// <see cref="TickStage.PathService"/>'s own remarks.
    /// <b>PROVISIONAL</b> <paramref name="currentTick"/>'s <c>blocked</c> span
    /// is all-<see langword="false"/> — no door-driven dynamic blocker source
    /// exists in this worktree (stage 4 is the same honest pass-through), so
    /// every cell reports passable to the search this call may run.
    /// </summary>
    private void AdvancePathService(long currentTick)
    {
        var groups = State.Groups;

        foreach (var group in groups)
        {
            if (group.HasOutstandingRequest)
            {
                _pathService.RequestPath(
                    group.GroupId, group.StartCellIndex, group.GoalCellIndex, group.RequestTick);
            }
        }

        _pathService.Advance(currentTick, _navGrid, _pathBlockedCells, _wallBuckets);

        if (groups.IsDefaultOrEmpty)
        {
            return;
        }

        var updatedGroups = ImmutableArray.CreateBuilder<GroupPathState>(groups.Length);
        foreach (var group in groups)
        {
            var justPublished = group.HasOutstandingRequest && !_pathService.HasOutstandingRequest(group.GroupId);
            updatedGroups.Add(justPublished ? group with { HasOutstandingRequest = false } : group);
        }

        State = State with { Groups = updatedGroups.MoveToImmutable() };
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
    /// section 8's <see langword="ulong"/> "minimum entity id") now passes to
    /// <see cref="PathService.GetReasonCode"/> unchanged — task 78 widened
    /// <c>PathService</c>'s whole group-identity surface to
    /// <see langword="ulong"/>, so no narrowing happens here.
    /// <see cref="AdvancePathService"/> now drains every
    /// <see cref="MissionState.Groups"/> entry whose
    /// <see cref="GroupPathState.HasOutstandingRequest"/> is
    /// <see langword="true"/> into <see cref="PathService.RequestPath"/> each
    /// tick (task 79a), so <see cref="PathService.GetReasonCode"/> now
    /// answers whatever that group's live pathfinding state actually is.
    /// <see cref="PathReasonCode.NoDestinationRequested"/> is the answer only
    /// for a group id <see cref="MissionState.Groups"/> never named at all —
    /// no autonomous destination-request source populates that array in this
    /// worktree, so a fixture that never sets it still sees this stage
    /// select every unassigned operator's intent as if nothing had been
    /// requested, which remains this stage's only honest behavior absent
    /// that source.
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

            var groupId = slots[i].GroupId;
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
    /// Sprint speed per design section 4 ("Units, and why they are
    /// chosen"): 5 m/s doubles to 80 world units per second. Not itself a
    /// per-tick raw step - <see cref="SandataRuleset.TickRate"/> is a
    /// per-instance property, not a compile-time constant (line 145 of
    /// that file), so the actual per-tick cap is derived fresh from
    /// <see cref="_ruleset"/> inside <see cref="ComputeMovementProposals"/>,
    /// the same shape stage 6 already uses for
    /// <c>readyTicks</c>/<c>resetTicks</c>/<c>aimTicks</c> at lines 447,
    /// 448, and 485.
    /// </summary>
    private const int SprintSpeedWuPerSecond = 80;

    /// <summary>
    /// Clamps a stage 9 desired point to at most
    /// <paramref name="movementSpeedRaw"/> raw units of displacement from
    /// the start point - design section 4's per-tick speed cap. Applied
    /// once, in <see cref="ComputeMovementProposals"/>, after both the
    /// ordered and autonomous branches have already picked a target, never
    /// per-branch, so an ordered operator's authored waypoint and an
    /// autonomous operator's formation slot are both walked toward across
    /// ticks rather than reached in one -
    /// <see cref="Movement.LocalAvoidance.Commit"/> applies no speed cap of
    /// its own, moving a proposal straight to its desired point subject
    /// only to collision blocking, so this is the only place stage 9
    /// enforces a per-tick distance limit. Uses <see cref="IntegerSqrt"/>
    /// for the displacement's magnitude; <c>Math.Sqrt</c> is banned in this
    /// project (see <see cref="IntegerSqrt"/>'s own remarks).
    /// </summary>
    private static (int X, int Y) ClampToMovementSpeed(
        int startXRaw, int startYRaw, int desiredXRaw, int desiredYRaw, int movementSpeedRaw)
    {
        var dx = (long)desiredXRaw - startXRaw;
        var dy = (long)desiredYRaw - startYRaw;
        var distanceSq = (dx * dx) + (dy * dy);

        if (distanceSq <= (long)movementSpeedRaw * movementSpeedRaw)
        {
            return (desiredXRaw, desiredYRaw);
        }

        // IntegerSqrt truncates, so it reports a distance no larger than the
        // true one. Scaling by movementSpeedRaw over a distance that is too
        // small produces a step that is too large, and the resulting
        // displacement can land just past the cap this method exists to
        // enforce — measured at 1,638.06 raw against a cap of 1,638 on a
        // (-1554, -518) step. Rounding the divisor up instead puts the error
        // on the undershoot side, where a tick occasionally travels one raw
        // unit less than it could and the bound is never broken.
        var distance = IntegerSqrt(distanceSq);
        if (distance <= 0)
        {
            return (desiredXRaw, desiredYRaw);
        }

        if ((long)distance * distance < distanceSq)
        {
            distance++;
        }

        var clampedX = startXRaw + checked((int)((dx * movementSpeedRaw) / distance));
        var clampedY = startYRaw + checked((int)((dy * movementSpeedRaw) / distance));
        return (clampedX, clampedY);
    }

    /// <summary>
    /// The squad's shared formation half-width, in world units, that
    /// <see cref="Squads.FormationCollapse.IsCollapsed"/> compares against
    /// the leader's clearance. <b>PROVISIONAL</b> — no
    /// <see cref="SandataRuleset"/> field carries this value: adding one
    /// would move that type's pinned <see cref="SandataRuleset.ContentHash"/>
    /// literal, an explicitly reviewed change this task does not make, so
    /// this is a placeholder pending a real tuning pass and a ruleset field
    /// of its own.
    /// </summary>
    private const long FormationHalfWidthWu = 6;

    /// <summary>
    /// How far behind the leader, in world units, each row of trailing
    /// slots marches — <see cref="FormationSlotOffsetsWu"/>'s per-row trail
    /// offset. <b>PROVISIONAL</b> — <see cref="SquadSlot"/> carries only a
    /// <see cref="SquadSlot.SlotIndex"/>, no stored per-operator offset
    /// (design section 8: "Stored per operator: ... Nothing else."), so
    /// this task derives one deterministically from that index rather than
    /// inventing per-operator state; the step size itself is an
    /// unvalidated placeholder.
    /// </summary>
    private const long FormationTrailStepWu = 8;

    /// <summary>
    /// Each row's sideways displacement from the leader's own path, in
    /// world units, before <see cref="Squads.FormationCollapse"/> may zero
    /// it. <b>PROVISIONAL</b> for the same reason as
    /// <see cref="FormationTrailStepWu"/>.
    /// </summary>
    private const long FormationLateralStepWu = 4;

    // The leader's sample point sits exactly one per-tick step ahead of its
    // own projection onto the path. There is no separate lookahead constant
    // any more, and the reason is worth keeping.
    //
    // A lookahead exists at all because a leader whose target is its own
    // projection never moves. Anything larger than one step is absorbed by
    // ClampToMovementSpeed and buys nothing; anything smaller throttles the
    // leader below the sprint speed design section 4 sets. One step is
    // therefore the only value that is neither wasteful nor limiting, and it
    // needs no tuning pass because it is not a tuning parameter — it is
    // SprintSpeedWuPerSecond divided by the tick rate, and it moves when
    // either of those does.
    //
    // Task 79b's provisional 8 world units could not be reduced to this
    // before task 87. PolylineArclength stored each segment's length as a
    // truncated integer square root of a *world-unit* square — an (8, 8)
    // segment measured 11 rather than 8·√2 ≈ 11.31 — and ProjectArclength and
    // SampleAt then divided by that truncated length in opposite directions,
    // so a position turned into an arclength and back lost up to about two
    // world units on a diagonal. Task 84 set the lookahead to 2 on the
    // reasoning that the clamp absorbs anything at or above the step, did not
    // check that reasoning against the arclength arithmetic the value
    // actually feeds, and froze a leader at (4, 4) permanently: it projected
    // to arclength 2, sampled arclength 4, and landed back on its own
    // position with nothing for the clamp to move toward. The lookahead went
    // back to 8 with the floor written at its declaration.
    //
    // Task 87 removed the floor rather than respecting it. Every length in
    // PolylineArclength is now raw fixed point, so the round trip loses a raw
    // unit or two instead of a world unit or two, and a lookahead of 1,638
    // raw clears that by three orders of magnitude.

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
    /// <b>Autonomous branch.</b> An operator with no
    /// <see cref="OrderAssignment"/> asks its own group's
    /// <see cref="_pathService"/> for <see cref="PathService.GetCurrentPath"/>
    /// — empty until <see cref="AdvancePathService"/> has actually published
    /// one for that group, in which case this method falls back to holding
    /// at the operator's own current position, exactly as it did before this
    /// task. Once a path is published, this method builds a
    /// <see cref="PolylineArclength"/> over it and derives the shared
    /// "leader arclength" fresh every tick by projecting the slot's own
    /// <see cref="SquadSlot.LeaderEntityId"/> current position onto that
    /// polyline through <see cref="ProjectArclength"/> — a pure geometric
    /// projection, not a stored per-tick tracker, so design section 8's
    /// "Stored per group: nothing" still holds. <see cref="SlotTargets.ComputeTarget"/>'s
    /// own remarks say that value is "not required to be
    /// <c>path.TotalLength</c>", only whatever arclength corresponds to the
    /// leader's current progress along the path, which is exactly what the
    /// projection recomputes. Because <see cref="Movement.LocalAvoidance.Commit"/>
    /// moves an entity straight to its proposal's desired point every tick
    /// with no speed cap of its own, projecting the leader's own current
    /// position back onto its own path would leave the leader's target
    /// pinned to wherever it already stands, so the leader's sample sits one
    /// per-tick step past that projection — see the comment above
    /// <see cref="FormationHalfWidthWu"/>'s neighbours for why exactly one
    /// step and not a separate tunable — giving the leader
    /// (slot 0, whose trail and lateral offsets are both zero)
    /// somewhere ahead of it to walk toward each tick, clamped to the
    /// path's own <see cref="PolylineArclength.TotalLength"/> so it never
    /// overshoots the goal. Each slot's trail
    /// and lateral offset come from <see cref="FormationSlotOffsetsWu"/>, a
    /// pure function of <see cref="SquadSlot.SlotIndex"/> — the only
    /// per-operator formation-shape input this worktree stores (design
    /// section 8: "Stored per operator: ... Nothing else."). The lateral
    /// component is then gated through <see cref="FormationCollapse.LateralOffset"/>
    /// using the clearance this method looks up at the leader's own cell via
    /// <see cref="FindLeaderClearance"/>, so a leader standing where the
    /// clearance field reads below <see cref="FormationHalfWidthWu"/> forces
    /// every slot in the group to single file for that tick, per design
    /// section 8's "Doorway collapse falls out of the clearance field."
    /// </para>
    /// <para>
    /// <b>Speed clamp.</b> Both branches above only choose a desired point;
    /// neither one is speed-limited on its own, and <see cref="Movement.LocalAvoidance.Commit"/>
    /// applies no cap of its own either. This method converts design
    /// section 4's sprint speed into a per-tick raw step once, from
    /// <see cref="_ruleset"/>, then calls <see cref="ClampToMovementSpeed"/>
    /// once per operator after the ordered/autonomous branch above has run,
    /// never inside either branch, so an ordered operator walks toward a
    /// far waypoint across many ticks and a non-leader slot walks into its
    /// formation position rather than starting there.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Whether operator <paramref name="index"/> should stand still this tick
    /// because it is in a position to shoot. True when stage 8 selected
    /// <see cref="OperatorIntent.Engage"/> for it <em>and</em> its best
    /// remembered contact is a living operator inside its own firearm's
    /// <see cref="FirearmDefinition.MaxEffectiveWu"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The range half does not discriminate today, and that is measured
    /// rather than assumed.</b> The binding number is
    /// <see cref="ContactMemory.IdentifyRangeWu"/>, which is 96 — not
    /// <see cref="ContactMemory.DetectRangeWu"/>'s 256 — because
    /// <see cref="OperatorIntent.Engage"/> requires
    /// <see cref="ContactTier.Identified"/> and nothing less.
    /// <c>PistolMaxEffectiveWu</c> is 320 and <c>RifleMaxEffectiveWu</c> is
    /// 800, so 96 &lt; 320 &lt; 800: a contact identified at all is already
    /// well inside the effective range of every weapon in the catalog. On
    /// today's numbers this method returns exactly what a bare
    /// <c>intents[index].Intent == Engage</c> test would return, and no test
    /// in this repository can currently tell the two apart.
    /// </para>
    /// <para>
    /// The range test is kept regardless, because the alternative is a rule
    /// that is only accidentally correct. The moment detection outruns a
    /// weapon — a longer <see cref="ContactMemory.DetectRangeWu"/>, a spotter,
    /// a shorter-ranged weapon than the 320 wu pistol — halting on the intent
    /// alone would stop an operator the instant it saw anything and leave it
    /// firing from a distance it cannot reach. Writing the rule correctly now
    /// costs one comparison per living operator per tick; discovering it later
    /// costs a session of wondering why nobody closes.
    /// </para>
    /// <para>
    /// <b>Why the contact comes from <paramref name="sensing"/> rather than
    /// from <c>state.Operators[index].ContactMemory</c>.</b> Stage 5 is
    /// evaluated before this stage but committed after it — see
    /// <see cref="RunTick"/> — so the memory on
    /// <see cref="MissionState"/> at this point is still last tick's. Stage 8
    /// chose its intent from <paramref name="sensing"/>, and a halt decision
    /// that consulted a different tick's contacts than the intent it is
    /// honouring would disagree with itself on the tick a contact appears or
    /// is lost.
    /// </para>
    /// <para>
    /// <b>Line of sight is already accounted for and is not re-tested here.</b>
    /// A contact only enters <see cref="ContactMemory"/> after passing
    /// <see cref="VisionCone.Contains"/> and <see cref="LineOfSight.IsVisible"/>
    /// in stage 5. Re-testing would duplicate stage 5's work; skipping the
    /// memory and scanning raw positions instead would halt operators behind
    /// walls, which is the mistake this note exists to prevent.
    /// </para>
    /// <para>
    /// The halt is a proposal like any other. It goes through
    /// <see cref="LocalAvoidance"/> in stage 10 exactly as a moving proposal
    /// does, so a halted operator still participates in collision rather than
    /// becoming an immovable obstacle.
    /// </para>
    /// </remarks>
    private static bool IsHaltedToEngage(
        TickStartView view,
        MissionState state,
        ImmutableArray<IntentSelectionResult> intents,
        SensingOutcome sensing,
        int index)
    {
        if (index >= intents.Length || intents[index].Intent != OperatorIntent.Engage)
        {
            return false;
        }

        var contactMemory = sensing.ContactMemoryByIndex;
        if (index >= contactMemory.Length ||
            !TryFindBestContact(contactMemory[index], out var contactId))
        {
            return false;
        }

        var contactIndex = view.IndexOf(contactId);
        if (contactIndex < 0 || !view.IsAlive(contactIndex))
        {
            return false;
        }

        // The view and state.Operators are index-aligned: CaptureTickStartView
        // builds the view by walking state.Operators in order. The firearm is
        // read from state because the view carries only what stages 5 through
        // 9 need to read positionally, and a loadout is not one of those.
        if (index >= state.Operators.Length)
        {
            return false;
        }

        var definition = FirearmCatalog.Rows[(int)state.Operators[index].Firearm];

        var dxWu = WorldUnits.FromFixedPoint(FixedPoint.FromRaw(view.PositionXRaw(contactIndex))) -
            WorldUnits.FromFixedPoint(FixedPoint.FromRaw(view.PositionXRaw(index)));
        var dyWu = WorldUnits.FromFixedPoint(FixedPoint.FromRaw(view.PositionYRaw(contactIndex))) -
            WorldUnits.FromFixedPoint(FixedPoint.FromRaw(view.PositionYRaw(index)));

        var rangeSquaredWu = checked((dxWu * dxWu) + (dyWu * dyWu));
        var maxEffectiveSquaredWu =
            checked((long)definition.MaxEffectiveWu * definition.MaxEffectiveWu);

        // Compared squared, so no integer square root is taken on a path that
        // runs once per living operator per tick.
        return rangeSquaredWu <= maxEffectiveSquaredWu;
    }

    private ImmutableArray<MovementProposal> ComputeMovementProposals(
        TickStartView view,
        ReadOnlySpan<SquadSlot> slots,
        MissionState state,
        ImmutableArray<IntentSelectionResult> intents,
        SensingOutcome sensing)
    {
        var count = view.Count;
        var assignments = state.OrderAssignments;
        // Sized to count: the loop below adds at most one proposal per
        // operator (skipping dead ones), so this is the exact upper bound
        // and avoids the unsized builder's doubling regrowth.
        var builder = ImmutableArray.CreateBuilder<MovementProposal>(count);

        // Design section 4: 5 m/s sprint = 80 wu/s. Truncating (not
        // rounding) keeps this <= CollisionBodyRadiusRaw on the safe side -
        // the game's only rounding rule (design section 4) is scoped to
        // milliseconds, not this conversion. _ruleset.TickRate is a
        // per-instance property, so this is computed here rather than as a
        // compile-time constant, once per call rather than per operator
        // since it does not vary across this tick's operators.
        var movementSpeedRaw = (SprintSpeedWuPerSecond * FixedPoint.Scale) / _ruleset.TickRate;

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

            // Halt to engage. Until 2026-08-11 this stage never read the
            // intent stage 8 had just selected, so an operator with
            // OperatorIntent.Engage kept walking its published path at full
            // sprint speed regardless. On the shipped angle-house map that
            // meant an assaulting pair advanced onto the objective cell a
            // defender was standing on and killed it from touching distance,
            // which is why the first person to watch a run reported the game
            // as melee combat. See this method's remarks.
            if (IsHaltedToEngage(view, state, intents, sensing, i))
            {
                builder.Add(new MovementProposal(
                    entityId, startXRaw, startYRaw, startXRaw, startYRaw,
                    slot.GroupId, slot.SlotIndex ?? 0));
                continue;
            }

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
                var path = _pathService.GetCurrentPath(slot.GroupId);

                if (path.IsDefaultOrEmpty || slot.LeaderEntityId is not { } leaderEntityId)
                {
                    desiredXRaw = startXRaw;
                    desiredYRaw = startYRaw;
                }
                else
                {
                    var arclength = PolylineArclength.Build(path);
                    var leaderIndex = view.IndexOf(leaderEntityId);
                    var leaderPositionArclength = leaderIndex < 0
                        ? arclength.TotalLength
                        : ProjectArclength(
                            path, arclength, view.PositionXRaw(leaderIndex), view.PositionYRaw(leaderIndex));
                    var leaderArclength = Math.Min(
                        leaderPositionArclength + movementSpeedRaw, arclength.TotalLength);
                    var (trailOffsetWu, lateralOffsetWu) = FormationSlotOffsetsWu(slot.SlotIndex ?? 0);
                    var leaderClearance = FindLeaderClearance(view, leaderEntityId);
                    var gatedLateralOffsetWu = FormationCollapse.LateralOffset(
                        leaderClearance, FormationHalfWidthWu, lateralOffsetWu);

                    // The formation-shape constants are authored in whole
                    // world units because that is the unit a person reasons
                    // about a squad's spacing in; the arclength table they
                    // index into is raw, so they are scaled here rather than
                    // being restated in raw at their declarations.
                    var target = SlotTargets.ComputeTarget(
                        arclength,
                        leaderArclength,
                        checked(trailOffsetWu * FixedPoint.Scale),
                        checked(gatedLateralOffsetWu * FixedPoint.Scale));

                    desiredXRaw = checked((int)target.X);
                    desiredYRaw = checked((int)target.Y);
                }
            }

            (desiredXRaw, desiredYRaw) = ClampToMovementSpeed(
                startXRaw, startYRaw, desiredXRaw, desiredYRaw, movementSpeedRaw);

            builder.Add(new MovementProposal(
                entityId, startXRaw, startYRaw, desiredXRaw, desiredYRaw,
                slot.GroupId, slot.SlotIndex ?? 0));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Projects a raw fixed-point world position onto the nearest point of
    /// <paramref name="path"/> and returns that point's arclength, also raw,
    /// per <paramref name="arclength"/>.
    /// Pure function of its inputs — no state is stored between calls, so
    /// this is safe to call fresh every tick for every group's leader rather
    /// than tracking leader progress incrementally (design section 8:
    /// "Stored per group: nothing").
    /// </summary>
    /// <remarks>
    /// Walks every segment of <paramref name="path"/>, clamps the
    /// dot-product projection scalar to the segment's own span so the
    /// closest point never falls outside the segment, and keeps the
    /// segment whose clamped closest point has the smallest squared
    /// distance to the query position. Ties break on the lowest segment
    /// index, matching this codebase's other total-order rules (design
    /// section 4: every multi-result query needs a total order). All
    /// arithmetic is integer and <see langword="checked"/>, following
    /// <see cref="FormationCollapse"/>'s own division-free,
    /// cross-multiplication style — no floating point anywhere in this
    /// method.
    /// </remarks>
    private static long ProjectArclength(
        ImmutableArray<PathPoint> path, in PolylineArclength arclength, long positionXRaw, long positionYRaw)
    {
        var bestDistanceSq = Int128.MaxValue;
        var bestArclength = 0L;

        for (var i = 0; i < path.Length - 1; i++)
        {
            // The published polyline's vertices are whole world units; every
            // length this method compares or returns is raw, so each vertex is
            // scaled once on the way in. Doing the geometry in world units and
            // scaling the answer afterwards is what task 87 removed: it threw
            // away the query position's own sub-world-unit precision before
            // the projection had a chance to use it.
            var ax = checked(path[i].X * FixedPoint.Scale);
            var ay = checked(path[i].Y * FixedPoint.Scale);
            var bx = checked(path[i + 1].X * FixedPoint.Scale);
            var by = checked(path[i + 1].Y * FixedPoint.Scale);
            var dx = bx - ax;
            var dy = by - ay;
            var denom = checked((dx * dx) + (dy * dy));

            var apx = positionXRaw - ax;
            var apy = positionYRaw - ay;

            long clampedNumerator;
            long closestX;
            long closestY;

            if (denom == 0)
            {
                clampedNumerator = 0;
                closestX = ax;
                closestY = ay;
            }
            else
            {
                var numerator = checked((apx * dx) + (apy * dy));
                clampedNumerator = Math.Clamp(numerator, 0, denom);

                // Int128 for the product alone. At raw scale the numerator is
                // already on the order of the squared map extent, and
                // multiplying that by a raw coordinate overflows a signed
                // 64-bit integer on a map only a few thousand world units
                // across. The quotient is back inside long by construction,
                // since clampedNumerator never exceeds denom. Hukbo.Core's
                // MovementContextQuery widens the same way for the same
                // reason; Int128 is exact integer arithmetic and carries none
                // of the cross-version hazard that bans double here.
                closestX = ax + checked((long)(((Int128)clampedNumerator * dx) / denom));
                closestY = ay + checked((long)(((Int128)clampedNumerator * dy) / denom));
            }

            var distX = positionXRaw - closestX;
            var distY = positionYRaw - closestY;
            var distanceSq = ((Int128)distX * distX) + ((Int128)distY * distY);

            if (distanceSq < bestDistanceSq)
            {
                bestDistanceSq = distanceSq;
                var segmentLength = arclength.ArclengthAtVertex(i + 1) - arclength.ArclengthAtVertex(i);
                var distanceAlongSegment = denom == 0
                    ? 0L
                    : checked((long)(((Int128)clampedNumerator * segmentLength) / denom));
                bestArclength = arclength.ArclengthAtVertex(i) + distanceAlongSegment;
            }
        }

        return bestArclength;
    }

    /// <summary>
    /// Derives one slot's trail and lateral offsets, in world units, purely
    /// from its zero-based <see cref="SquadSlot.SlotIndex"/> — the only
    /// per-operator formation-shape input this worktree stores (design
    /// section 8, quoted on <see cref="FormationTrailStepWu"/>). Slot 0 (the
    /// leader itself) rides its own path with no offset at all. Every later
    /// slot packs two to a row, alternating left and right of the leader's
    /// centreline by the index's own parity, one
    /// <see cref="FormationTrailStepWu"/> further back per row — a pure
    /// function of a value design section 8 already guarantees is a stable
    /// total order (ascending living entity id), so two evaluations of the
    /// same slot index always agree.
    /// </summary>
    private static (long TrailOffsetWu, long LateralOffsetWu) FormationSlotOffsetsWu(int slotIndex)
    {
        if (slotIndex <= 0)
        {
            return (0, 0);
        }

        var row = (slotIndex + 1) / 2;
        var trailOffsetWu = row * FormationTrailStepWu;
        var lateralOffsetWu = slotIndex % 2 == 1 ? FormationLateralStepWu : -FormationLateralStepWu;

        return (trailOffsetWu, lateralOffsetWu);
    }

    /// <summary>
    /// Looks up the group leader's own clearance-field value:
    /// <paramref name="view"/>'s frozen position for
    /// <paramref name="leaderEntityId"/>, converted to a nav cell via
    /// <see cref="NavGrid.WorldToCellCoordinate"/>, indexed into
    /// <see cref="_clearanceField"/>. A living operator's own
    /// <see cref="SquadSlot.LeaderEntityId"/> is guaranteed non-null and
    /// present in <paramref name="view"/> — the operator's own component
    /// always has at least itself alive — so the two defensive fallbacks
    /// below (<see cref="ClearanceField.BlockedClearance"/> for a missing
    /// index or an out-of-grid cell) exist only to keep this method total
    /// without throwing, not because either path is expected to run.
    /// </summary>
    private int FindLeaderClearance(TickStartView view, ulong leaderEntityId)
    {
        var leaderIndex = view.IndexOf(leaderEntityId);

        if (leaderIndex < 0)
        {
            return ClearanceField.BlockedClearance;
        }

        var cellX = NavGrid.WorldToCellCoordinate(view.PositionXWu(leaderIndex));
        var cellY = NavGrid.WorldToCellCoordinate(view.PositionYWu(leaderIndex));

        if (!_navGrid.TryGetCellIndex(cellX, cellY, out var cellIndex))
        {
            return ClearanceField.BlockedClearance;
        }

        return _clearanceField[cellIndex];
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
