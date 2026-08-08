using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Collision;
using Sandata.Core.Determinism;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;
using Sandata.Core.Weapons;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 49c of docs/plans/2026-08-07-sandata-scaffold.md: proves the
/// fourteen-stage tick pipeline (<see cref="SandataSimulation.RunTick"/>,
/// <see cref="TickStage"/>, <see cref="TickStartView"/>) behaves the way its
/// own doc comments claim, using only the observable surface — <see
/// cref="SandataSimulation.State"/>, <see
/// cref="SandataSimulation.PendingIntents"/>, <see
/// cref="SandataSimulation.LastStateHash"/>, and the internal <see
/// cref="SandataSimulation.PendingMovementProposals"/> buffer that <see
/// cref="InternalsVisibleToAttribute"/> exposes to this assembly. This file
/// never edits a production type; every gap it hits is reported in its own
/// remarks instead of being patched around.
/// </summary>
public sealed class TickPipelineTests
{
    // ---- Shared fixture builders --------------------------------------

    private static Mission BuildMission(ulong seed = 1UL) => new(
        formatVersion: Mission.CurrentFormatVersion,
        seed: seed,
        mapContentHash: 1UL,
        // StateHashCadenceTicks: 1 so every tick this file runs computes a
        // fresh LastStateHash — several tests below depend on that.
        tickPolicy: new MissionTickPolicy(TickLimit: 10_000, StateHashCadenceTicks: 1),
        factionSetups: ImmutableArray.Create(
            new MissionFactionSetup(FactionId: 0, OperatorCount: 4),
            new MissionFactionSetup(FactionId: 1, OperatorCount: 4)),
        rulesetId: SandataPresetId.ModernTacticalV1);

    private static OperatorState BuildOperator(
        int entityId,
        int faction,
        int positionXWu,
        int positionYWu,
        Facing16? facing = null,
        Bam16? aimAngle = null,
        int weaponChainPhase = 0,
        int weaponChainRemainingTicks = 0) => new(
            EntityId: (ulong)entityId,
            PositionX: FixedPoint.FromWhole(positionXWu),
            PositionY: FixedPoint.FromWhole(positionYWu),
            Facing: facing ?? Facing16.East,
            AimAngle: aimAngle ?? Bam16.FromFacing16(facing ?? Facing16.East),
            Health: 100,
            Faction: faction,
            Intent: 0,
            IsCrouched: false,
            WeaponLowered: false,
            WeaponChainPhase: weaponChainPhase,
            WeaponChainRemainingTicks: weaponChainRemainingTicks,
            MagazineRounds: 30,
            CyclicFireAccumulator: 0,
            SuppressionCounter: 0);

    private static MissionState BuildState(ImmutableArray<OperatorState> operators, long tick = 0) => new(
        Tick: tick, Phase: 1, Winner: -1, NextEntityId: (ulong)(operators.Length + 1), NextEventSequence: 0)
    {
        Operators = operators,
        FactionAlerts = ImmutableArray.Create(new FactionAlertState(0, 0), new FactionAlertState(1, 0)),
        Doors = ImmutableArray<DoorState>.Empty,
        Groups = ImmutableArray<GroupPathState>.Empty,
        RngStreams = ImmutableArray<RngStreamState>.Empty,
    };

    /// <summary>
    /// A fresh <see cref="NavGrid"/> with every cell marked <see
    /// cref="NavCellFlags.Open"/>. <see cref="NavGrid"/>'s own constructor
    /// leaves every cell at the enum's zero value, <see
    /// cref="NavCellFlags.Blocked"/> — realistic only once <see
    /// cref="NavBake.Bake"/> has run, which no test in this file
    /// does. Without this, any path node submitted against the grid this
    /// method used to hand back is rejected with
    /// <see cref="OrderRejectReason.NodeInBlockedCell"/> before stage 1 ever
    /// applies it — not a pipeline defect, a fixture gap this file corrects
    /// explicitly rather than silently.
    /// </summary>
    private static NavGrid BuildGrid()
    {
        var grid = new NavGrid(width: 32, height: 32);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        return grid;
    }

    private static WallBuckets NoWalls(NavGrid grid) => WallBuckets.Build(grid, [], [], [], []);

    // ---- 1. STAGE ORDER --------------------------------------------------

    /// <summary>
    /// Pins the closest real evidence available for stage order, given that
    /// <see cref="SandataSimulation.RunTick"/> exposes no stage-observation
    /// hook at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this proves, and what it cannot.</b> <see cref="SandataSimulation.RunTick"/>
    /// carries no stage-observation hook anywhere — no event, no callback, no
    /// recorded stage log, confirmed by reading the method and
    /// <see cref="TickStage"/> in full. There is therefore no way, from
    /// outside this type, to observe the actual order the fourteen
    /// private/internal stage calls execute in; the only thing an outside
    /// caller can see is the state before the call and the state after it
    /// returns.
    /// </para>
    /// <para>
    /// This test submits an order whose effect can only appear in
    /// <see cref="MissionState.OrderAssignments"/> by way of stage 1's
    /// <c>ApplyOrders</c>, then reads <see cref="SandataSimulation.LastStateHash"/>,
    /// which stage 14's <c>ComputeStateHash</c> computes from
    /// <see cref="SandataSimulation.State"/> as it stands when that stage
    /// runs. Recomputing the same hash independently, from the exact
    /// post-tick <see cref="MissionState"/> this test can already see, and
    /// getting the same value proves stage 14 hashed a state that already
    /// carries stage 1's effect — the two could only agree if stage 1 ran
    /// before stage 14 within the same <see cref="SandataSimulation.RunTick"/>
    /// call, matching <see cref="TickStage"/>'s declared 1-before-14 order.
    /// </para>
    /// <para>
    /// <b>Gap, stated plainly:</b> this does not, and cannot, establish the
    /// relative order of any other pair among the fourteen stages (2 through
    /// 13). Proving those would need an observation point <see
    /// cref="SandataSimulation"/> does not expose today.
    /// </para>
    /// </remarks>
    [Fact]
    public void RunTick_OrderAppliedAtStage1_ReachesTheStateStage14Hashes()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();
        var op = BuildOperator(entityId: 1, faction: 0, positionXWu: 0, positionYWu: 0);
        var state = BuildState(ImmutableArray.Create(op));

        var sim = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state);

        var pathNodes = ImmutableArray.Create(
            new OrderPathNode(0, 0),
            new OrderPathNode(40, 0));

        var (_, _, rejection) = sim.SubmitOrder(
            targetTick: 0,
            factionId: 0,
            addressees: ImmutableArray.Create(1UL),
            kind: OrderKind.MoveAlongPath,
            pathNodes: pathNodes);

        Assert.Null(rejection);

        sim.RunTick(currentTick: 0);

        var assignment = Assert.Single(sim.State.OrderAssignments);
        Assert.Equal(1UL, assignment.EntityId);
        Assert.Equal(0, assignment.CurrentNodeIndex);
        Assert.Equal(2, assignment.PathNodes.Length);

        Assert.NotNull(sim.LastStateHash);
        var recomputed = SandataStateHasher.Compute(mission, sim.State, SandataRuleset.ModernTacticalV1);
        Assert.Equal(recomputed, sim.LastStateHash);
    }

    // ---- 2. ORDER INDEPENDENCE -------------------------------------------

    /// <summary>
    /// Design section 5's write-only discipline for stages 5 through 9 means
    /// a physical operator's outcome must depend only on its own state and
    /// its faction and neighbours — never on which array slot happened to
    /// hold it. This runs the same four physical bodies through two
    /// <see cref="SandataSimulation"/> instances that assign the ascending
    /// entity-id sequence 1-2-3-4 to those bodies in two different spatial
    /// arrangements — <c>id1..id4</c> left-to-right in one, right-to-left in
    /// the other, a genuine multi-operator permutation of "which body a
    /// processing-order position belongs to" — then compares
    /// <see cref="SandataSimulation.PendingMovementProposals"/> matched by
    /// physical starting position rather than by entity id, since the two
    /// runs deliberately give the same physical body a different id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why not a raw array permutation.</b> The original design for this
    /// test permuted <see cref="MissionState.Operators"/> directly (e.g.
    /// <c>[op4, op2, op1, op3]</c>) and ran that permuted array through a
    /// second <see cref="SandataSimulation"/>. That is not valid input: stage
    /// 6's <c>ComputeSquadGrouping</c> passes <see cref="TickStartView"/>'s
    /// entity-id array straight into <c>SquadGrouping.Compute</c>, whose
    /// <c>ValidateStrictlyAscending</c> throws <see cref="ArgumentException"/>
    /// the instant two adjacent ids are out of order — confirmed empirically,
    /// not by inspection alone. <b>This is a reportable defect in its own
    /// right</b>: <see cref="TickStartView"/>'s own doc comment calls
    /// ascending order "the order <c>MissionState.Operators</c> is documented
    /// to hold," but nothing at <see cref="SandataSimulation"/>'s
    /// constructor, at <see cref="MissionState"/>'s construction, or anywhere
    /// between the two validates that invariant before stage 6 depends on it.
    /// A caller that builds or restores a <see cref="MissionState"/> with an
    /// out-of-order operator array gets an opaque <see
    /// cref="ArgumentException"/> three stages later, from a type
    /// (<c>SquadGrouping</c>) that names an argument (<c>entityIds</c>) the
    /// caller never touched directly, rather than a clear failure at the
    /// boundary that actually violated the contract.
    /// </para>
    /// <para>
    /// <b>What this test measures instead.</b> Both arrangements below keep
    /// <see cref="MissionState.Operators"/> strictly ascending by entity id —
    /// valid input either way — but swap which physical body (starting
    /// position) each id names, which swaps the order stage 5 through 9's
    /// ascending-id-order iteration visits the four bodies in. Matching
    /// proposals by starting position instead of entity id, and comparing
    /// the movement delta (<c>DesiredXRaw - StartXRaw</c>,
    /// <c>DesiredYRaw - StartYRaw</c>) rather than the id itself, proves a
    /// physical body's outcome does not depend on which id — and therefore
    /// which iteration-order slot — it was assigned.
    /// </para>
    /// <para>
    /// <b>Gap, stated plainly:</b> no order is submitted in this fixture, and
    /// the four bodies stand far enough apart (raw distance far past
    /// <c>GroupCohesionRadius</c>'s default 96) that none ever senses or
    /// groups with another. <c>IntentSelection</c> therefore selects no
    /// engagement or movement for any of them, so every proposal's delta is
    /// zero in both arrangements. This proves the pipeline does not crash,
    /// misassign, or otherwise diverge under a genuinely permuted id-to-body
    /// mapping, but it is a structural check — presence, count, and a
    /// zero-delta value — not a proof that a *non-trivial* movement outcome
    /// is order independent. That stronger claim would need a fixture that
    /// drives at least one operator into <c>Engage</c> or <c>Advance</c>,
    /// which Test 6's <c>AimToleranceBam</c> case below finds is itself
    /// harder than it looks.
    /// </para>
    /// </remarks>
    [Fact]
    public void RunTick_PermutedEntityIdToPositionMapping_ProducesIdenticalMovementProposals()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        // Arrangement A: ascending id order matches ascending spatial order.
        var leftToRight = ImmutableArray.Create(
            BuildOperator(1, faction: 0, positionXWu: 0, positionYWu: 0),
            BuildOperator(2, faction: 0, positionXWu: 20, positionYWu: 0),
            BuildOperator(3, faction: 0, positionXWu: 40, positionYWu: 0),
            BuildOperator(4, faction: 0, positionXWu: 60, positionYWu: 0));

        // Arrangement B: the same four physical positions, the same
        // strictly-ascending id sequence 1-2-3-4 — still valid input — but
        // the id-to-position mapping is reversed, so id 1 now names the
        // rightmost body and id 4 the leftmost.
        var rightToLeft = ImmutableArray.Create(
            BuildOperator(1, faction: 0, positionXWu: 60, positionYWu: 0),
            BuildOperator(2, faction: 0, positionXWu: 40, positionYWu: 0),
            BuildOperator(3, faction: 0, positionXWu: 20, positionYWu: 0),
            BuildOperator(4, faction: 0, positionXWu: 0, positionYWu: 0));

        var simA = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildState(leftToRight));
        var simB = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildState(rightToLeft));

        simA.RunTick(0);
        simB.RunTick(0);

        var proposalsA = simA.PendingMovementProposals.OrderBy(p => p.StartXRaw).ToImmutableArray();
        var proposalsB = simB.PendingMovementProposals.OrderBy(p => p.StartXRaw).ToImmutableArray();

        Assert.Equal(4, proposalsA.Length);
        Assert.Equal(proposalsA.Length, proposalsB.Length);

        for (var i = 0; i < proposalsA.Length; i++)
        {
            // Matched by physical starting position, not by entity id — the
            // two arrangements deliberately give the same body a different
            // id, so the ids themselves are expected to differ here.
            Assert.Equal(proposalsA[i].StartXRaw, proposalsB[i].StartXRaw);
            Assert.Equal(proposalsA[i].StartYRaw, proposalsB[i].StartYRaw);

            var deltaXA = proposalsA[i].DesiredXRaw - proposalsA[i].StartXRaw;
            var deltaYA = proposalsA[i].DesiredYRaw - proposalsA[i].StartYRaw;
            var deltaXB = proposalsB[i].DesiredXRaw - proposalsB[i].StartXRaw;
            var deltaYB = proposalsB[i].DesiredYRaw - proposalsB[i].StartYRaw;

            Assert.Equal(deltaXA, deltaXB);
            Assert.Equal(deltaYA, deltaYB);
            Assert.Equal(0, deltaXA);
            Assert.Equal(0, deltaYA);
        }
    }
}
