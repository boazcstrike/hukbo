using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Collision;
using Sandata.Core.Combat;
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

    // ---- 3. NO LEAKED VIEW ------------------------------------------------

    /// <summary>
    /// Design section 5's second binding rule is that stages 10 through 14
    /// never read <see cref="TickStartView"/> — the frozen snapshot stage 9
    /// releases before they run. Walks every one of the five stage-10-to-14
    /// internal methods' parameter types, and the type graph reachable from
    /// each through generic arguments, array element types, and — for types
    /// this solution itself declares — instance fields, asserting none ever
    /// reaches <see cref="TickStartView"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this proves, and what it does not.</b> None of the five
    /// methods below takes a <see cref="TickStartView"/> parameter at all —
    /// true by inspection before this test ever runs — so on its own that
    /// fact would be a vacuous, "check the type never appears in a place it
    /// was never going to appear" test. <see
    /// cref="TypeGraphWalk_DetectsATickStartViewCarriedInAWrappingType"/> is
    /// the positive control that keeps this one honest: it proves the exact
    /// same walker used here can detect a <see cref="TickStartView"/> that
    /// is smuggled inside a wrapping type's field rather than passed
    /// directly, so a method here passing, say, a struct that happens to
    /// carry a <see cref="TickStartView"/> field would fail this test rather
    /// than passing it by accident.
    /// </para>
    /// <para>
    /// <b>Gap, stated plainly:</b> this walk only descends into types
    /// declared in this solution's own assemblies (<c>Sandata.Core</c>,
    /// <c>Hukbo.Core</c>, <c>Hukbo.Shared.Core</c>, and this test assembly,
    /// so the positive control below is itself reachable) — not into every .NET
    /// base class library type's private fields, which would make the walk
    /// slow, noisy, and prone to false positives from framework internals no
    /// caller can ever observe. It also only proves the five methods'
    /// *parameter lists* never reach <see cref="TickStartView"/>, not that
    /// their method bodies never construct or capture one some other way —
    /// reading the five bodies directly already confirms none does, but that
    /// confirmation is a source read, not something this reflection walk
    /// itself checks.
    /// </para>
    /// </remarks>
    [Fact]
    public void StageTenThroughFourteenMethods_NeverReachTickStartViewInTheirParameterTypeGraph()
    {
        var simulationType = typeof(SandataSimulation);
        var stageMethodNames = new[]
        {
            "ResolveLocalAvoidanceAndCollision", // stage 10
            "AdvanceWeaponChain",                 // stage 11
            "ProposeFire",                        // stage 12
            "ResolveDamage",                      // stage 13
            "ComputeStateHash",                   // stage 14
        };

        foreach (var methodName in stageMethodNames)
        {
            var method = simulationType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.True(method is not null, $"Expected an internal instance method named '{methodName}' on {simulationType}.");

            foreach (var parameter in method!.GetParameters())
            {
                var visited = new HashSet<Type>();
                var reachesView = TypeGraphReachesTickStartView(parameter.ParameterType, visited);

                Assert.False(
                    reachesView,
                    $"{methodName}'s parameter '{parameter.Name}' of type {parameter.ParameterType} reaches {nameof(TickStartView)}.");
            }
        }
    }

    /// <summary>
    /// A type that deliberately carries a <see cref="TickStartView"/> field,
    /// used only as the positive control for
    /// <see cref="StageTenThroughFourteenMethods_NeverReachTickStartViewInTheirParameterTypeGraph"/>
    /// — never passed to any real stage method.
    /// </summary>
    private sealed class WrapsATickStartView
    {
        internal readonly TickStartView? View;

        internal WrapsATickStartView(TickStartView? view)
        {
            View = view;
        }
    }

    /// <summary>
    /// Positive control: proves <see cref="TypeGraphReachesTickStartView"/>
    /// actually detects a <see cref="TickStartView"/> reachable through a
    /// wrapping type's field, and returns false for an unrelated type, so
    /// the stage-method walk above cannot be passing vacuously.
    /// </summary>
    [Fact]
    public void TypeGraphWalk_DetectsATickStartViewCarriedInAWrappingType()
    {
        Assert.True(TypeGraphReachesTickStartView(typeof(WrapsATickStartView), new HashSet<Type>()));
        Assert.False(TypeGraphReachesTickStartView(typeof(ImmutableArray<DamageInstance>), new HashSet<Type>()));
    }

    /// <summary>
    /// Depth-first search for <see cref="TickStartView"/> starting from
    /// <paramref name="type"/> itself, then its generic arguments, its array
    /// element type, and — only when <paramref name="type"/> belongs to one
    /// of this solution's own assemblies — its declared instance fields.
    /// <paramref name="visited"/> prevents revisiting a type already ruled
    /// out, which also breaks any cycle a self-referential type could form.
    /// </summary>
    private static bool TypeGraphReachesTickStartView(Type type, HashSet<Type> visited)
    {
        if (!visited.Add(type))
        {
            return false;
        }

        if (type == typeof(TickStartView))
        {
            return true;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                if (TypeGraphReachesTickStartView(argument, visited))
                {
                    return true;
                }
            }
        }

        if (type.IsArray && type.GetElementType() is { } elementType &&
            TypeGraphReachesTickStartView(elementType, visited))
        {
            return true;
        }

        var assemblyName = type.Assembly.GetName().Name;
        var isOwnAssembly = assemblyName is "Sandata.Core" or "Hukbo.Core" or "Hukbo.Shared.Core" or "Sandata.Core.Tests";

        if (!isOwnAssembly)
        {
            return false;
        }

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (TypeGraphReachesTickStartView(field.FieldType, visited))
            {
                return true;
            }
        }

        return false;
    }

    // ---- 4. VIEW LIFETIME --------------------------------------------------

    /// <summary>
    /// <see cref="TickStartView"/>'s invalidation contract, proven directly.
    /// The real snapshot <see cref="SandataSimulation.RunTick"/> captures at
    /// stage 3 and releases at stage 9 is a local variable inside that
    /// method — unreachable from any test through the pipeline itself, since
    /// nothing on <see cref="SandataSimulation"/>'s public or internal
    /// surface hands it back out. This constructs one directly with its
    /// internal constructor instead, releases it, and asserts that
    /// accessors throw exactly the exception <see cref="TickStartView"/>'s
    /// own <c>EnsureNotReleased</c> promises — the property that matters,
    /// proven without needing an observation point inside <c>RunTick</c>.
    /// </summary>
    [Fact]
    public void TickStartView_AccessorsThrowInvalidOperationExceptionOnceReleased()
    {
        var state = BuildState(ImmutableArray<OperatorState>.Empty);
        var pairs = Array.Empty<SandataCollisionPair>();

        var view = new TickStartView(state, pairs);
        view.Release();

        const string expectedMessage =
            "TickStartView was released after stage 9 and may not be read by stage 10 or later.";

        var countException = Assert.Throws<InvalidOperationException>(() => { _ = view.Count; });
        Assert.Equal(expectedMessage, countException.Message);

        var indexOfException = Assert.Throws<InvalidOperationException>(() => view.IndexOf(1UL));
        Assert.Equal(expectedMessage, indexOfException.Message);
    }
}
