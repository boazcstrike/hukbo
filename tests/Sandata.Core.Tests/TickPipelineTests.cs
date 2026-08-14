using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Collision;
using Sandata.Core.Combat;
using Sandata.Core.Determinism;
using Sandata.Core.Events;
using Sandata.Core.Maps;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Sensing;
using Sandata.Core.Simulation;
using Sandata.Core.Squads;
using Sandata.Core.Weapons;
using Sandata.Headless;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 49c of Sandata's scaffold plan: proves the
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
    private static NavGrid BuildGrid(int width = 32, int height = 32)
    {
        var grid = new NavGrid(width: width, height: height);
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

        var sim = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);

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
    /// <b>Gap, stated plainly:</b> no order is submitted in this fixture, so
    /// <c>MovementProposal.DesiredXRaw/DesiredYRaw</c> stay equal to
    /// <c>StartXRaw/StartYRaw</c> for every operator regardless of squad
    /// membership — <c>ComputeMovementProposals</c>'s autonomous-movement
    /// branch (no <see cref="Sandata.Core.Orders.OrderAssignment"/>) holds
    /// position, and grouping alone never drives a delta. <b>Corrected,
    /// task 77:</b> this comment previously claimed the four bodies (0, 20,
    /// 40, 60 world units apart, all faction 0) stood "far enough apart" to
    /// never group, reading <c>GroupCohesionRadius</c>'s default 96 as if it
    /// were already the correct raw-unit interpretation that task 77 found
    /// broken. Under the fixed world-unit conversion every pair here is well
    /// inside the true 96-world-unit radius and does union into one squad in
    /// both arrangements — this test does not assert <c>GroupId</c> and does
    /// not need to, since the claim it makes is about the movement delta,
    /// which is genuinely zero either way. <c>IntentSelection</c> selects no
    /// engagement for any of them (no opposing-faction contact in range), so
    /// every proposal's delta is zero in both arrangements. This proves the
    /// pipeline does not crash, misassign, or otherwise diverge under a
    /// genuinely permuted id-to-body mapping, but it is a structural check —
    /// presence, count, and a zero-delta value — not a proof that a
    /// *non-trivial* movement outcome is order independent. That stronger
    /// claim would need a fixture that drives at least one operator into
    /// <c>Engage</c> or <c>Advance</c>, which Test 6's <c>AimToleranceBam</c>
    /// case below finds is itself harder than it looks.
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

        var simA = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildState(leftToRight), ImmutableArray<CoverRecord>.Empty);
        var simB = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildState(rightToLeft), ImmutableArray<CoverRecord>.Empty);

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
        var cohesionPairs = Array.Empty<SandataCollisionPair>();

        var view = new TickStartView(state, pairs, cohesionPairs);
        view.Release();

        const string expectedMessage =
            "TickStartView was released after stage 9 and may not be read by stage 10 or later.";

        var countException = Assert.Throws<InvalidOperationException>(() => { _ = view.Count; });
        Assert.Equal(expectedMessage, countException.Message);

        var indexOfException = Assert.Throws<InvalidOperationException>(() => view.IndexOf(1UL));
        Assert.Equal(expectedMessage, indexOfException.Message);
    }

    // ---- 5. DETERMINISM -----------------------------------------------

    /// <summary>
    /// Two independently constructed <see cref="SandataSimulation"/>
    /// instances, built from the same <see cref="Mission"/> and the same
    /// starting fixture (an identified cross-faction contact, so the
    /// weapon chain actually advances through several phases rather than
    /// sitting idle), run twenty identical ticks each and must land on the
    /// same <see cref="MissionState.Operators"/> and the same
    /// <see cref="SandataSimulation.LastStateHash"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Gap, stated plainly.</b> <see cref="SandataSimulation"/> exposes no
    /// ordered event stream the way <c>Hukbo.Core.Simulation.BattleOutcome</c>
    /// does — <see cref="SandataSimulation.PendingIntents"/> and
    /// <see cref="SandataSimulation.PendingMovementProposals"/> are each only
    /// the most recently completed tick's buffer, overwritten on the next
    /// <see cref="SandataSimulation.RunTick"/> call, not an accumulated
    /// history. This test can therefore only compare the two runs'
    /// end-of-run <see cref="SandataSimulation.State"/> and
    /// <see cref="SandataSimulation.LastStateHash"/>, not an ordered event
    /// stream across all twenty ticks the way a full determinism contract
    /// (design section 4's "identical state hash, event hash, winner, and
    /// ordered event stream") would ask for. There is no event hash and no
    /// winner concept in this worktree's <see cref="SandataSimulation"/>
    /// today either.
    /// </para>
    /// </remarks>
    [Fact]
    public void RunTick_TwentyIdenticalTicksAcrossTwoIndependentInstances_ProduceIdenticalStateAndHash()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        MissionState BuildFixture() => BuildState(ImmutableArray.Create(
            BuildOperator(1, faction: 0, positionXWu: 0, positionYWu: 0, aimAngle: new Bam16(2548)),
            BuildOperator(2, faction: 1, positionXWu: 90, positionYWu: 0)));

        var simA = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildFixture(), ImmutableArray<CoverRecord>.Empty);
        var simB = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildFixture(), ImmutableArray<CoverRecord>.Empty);

        for (var tick = 0; tick < 20; tick++)
        {
            simA.RunTick(tick);
            simB.RunTick(tick);
        }

        Assert.True(simA.State.Operators.SequenceEqual(simB.State.Operators));
        Assert.NotNull(simA.LastStateHash);
        Assert.Equal(simA.LastStateHash, simB.LastStateHash);
    }

    // ---- 6. THE FOUR RULESET CONSTANTS ---------------------------------

    /// <summary>
    /// <see cref="SandataRuleset.GroupCohesionRadiusWu"/>: design section 8
    /// documents it as a world-unit radius — "operators within
    /// <c>GroupCohesionRadius</c> world units of each other in the same
    /// faction are unioned" — so two same-faction operators 50 world units
    /// apart, well inside the default 96-world-unit radius, union into one
    /// squad. <b>Task 77, superseding the test this replaces.</b> The
    /// previous version of this test, named
    /// <c>RunTick_TwoSameFactionOperatorsFiftyWorldUnitsApart_AreNotGroupedDespiteDocumentedRadius</c>,
    /// asserted the opposite and documented two compounding defects as the
    /// reason: <c>SandataSimulation.ComputeSquadGrouping</c> passed
    /// <c>_ruleset.GroupCohesionRadius</c> straight into
    /// <c>SquadGrouping.Compute</c>'s raw-fixed-point parameter with no
    /// world-unit-to-raw conversion (so the effective radius was about 0.094
    /// world units, not 96), and its candidate pair list came from
    /// <c>TickStartView.Pairs</c>, the physical-contact broad phase, which
    /// can never surface two operators standing world units apart regardless
    /// of any radius value. Both are fixed: <see cref="SandataRuleset.GroupCohesionRadiusWu"/>
    /// states its own unit and <c>ComputeSquadGrouping</c> now converts it
    /// via <c>RawFromWorldUnits</c> before comparing, and stage 3 builds a
    /// second <see cref="Sandata.Core.Collision.SandataCollisionGrid"/> sized
    /// to that radius via <c>RebuildWithinRange</c>, exposed as
    /// <see cref="TickStartView.CohesionPairs"/>, which
    /// <c>ComputeSquadGrouping</c> now reads instead. This test proves the
    /// fixed behaviour through <see cref="SandataSimulation.RunTick"/>, not
    /// by calling <see cref="Sandata.Core.Squads.SquadGrouping.Compute"/>
    /// directly with a hand-fed pair list — see this file's own remarks on
    /// why a fixture-supplied candidate list cannot prove the production call
    /// chain.
    /// </summary>
    [Fact]
    public void RunTick_TwoSameFactionOperatorsFiftyWorldUnitsApart_AreGroupedNowThatTheRadiusIsHonoured()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var state = BuildState(ImmutableArray.Create(
            BuildOperator(1, faction: 0, positionXWu: 0, positionYWu: 0),
            BuildOperator(2, faction: 0, positionXWu: 50, positionYWu: 0)));

        var sim = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);
        sim.RunTick(0);

        var proposalOne = sim.PendingMovementProposals.Single(p => p.EntityId == 1UL);
        var proposalTwo = sim.PendingMovementProposals.Single(p => p.EntityId == 2UL);

        Assert.Equal(proposalOne.GroupId, proposalTwo.GroupId);
        Assert.Equal(1UL, proposalOne.GroupId);
    }

    /// <summary>
    /// Boundary of <see cref="SandataRuleset.GroupCohesionRadiusWu"/>, proven
    /// through <see cref="SandataSimulation.RunTick"/> at the ruleset's own
    /// world-unit granularity: exactly at the default 96-world-unit radius
    /// the two operators union (inclusive — matching
    /// <see cref="Sandata.Core.Squads.SquadGrouping.Compute"/>'s own
    /// documented inclusive comparison and
    /// <see cref="Sandata.Core.Collision.SandataCollisionGrid.IsContact"/>'s
    /// same convention for physical contact), and one world unit further out
    /// — 97 world units — they do not.
    /// </summary>
    [Fact]
    public void RunTick_AtTheCohesionRadiusOperatorsGroup_OneWorldUnitBeyondTheyDoNot()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        MissionState BuildFixture(int separationWu) => BuildState(ImmutableArray.Create(
            BuildOperator(1, faction: 0, positionXWu: 0, positionYWu: 0),
            BuildOperator(2, faction: 0, positionXWu: separationWu, positionYWu: 0)));

        var simAtRadius = new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildFixture(96), ImmutableArray<CoverRecord>.Empty);
        simAtRadius.RunTick(0);
        var atRadiusOne = simAtRadius.PendingMovementProposals.Single(p => p.EntityId == 1UL);
        var atRadiusTwo = simAtRadius.PendingMovementProposals.Single(p => p.EntityId == 2UL);
        Assert.Equal(atRadiusOne.GroupId, atRadiusTwo.GroupId);

        var simBeyondRadius = new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildFixture(97), ImmutableArray<CoverRecord>.Empty);
        simBeyondRadius.RunTick(0);
        var beyondOne = simBeyondRadius.PendingMovementProposals.Single(p => p.EntityId == 1UL);
        var beyondTwo = simBeyondRadius.PendingMovementProposals.Single(p => p.EntityId == 2UL);
        Assert.NotEqual(beyondOne.GroupId, beyondTwo.GroupId);
        Assert.Equal(1UL, beyondOne.GroupId);
        Assert.Equal(2UL, beyondTwo.GroupId);
    }

    /// <summary>
    /// Two operators within <see cref="SandataRuleset.GroupCohesionRadiusWu"/>
    /// of each other but in different factions never union — the candidate
    /// pair exists (<see cref="TickStartView.CohesionPairs"/> is a plain
    /// distance query with no faction filter of its own), but
    /// <see cref="Sandata.Core.Squads.SquadGrouping.ComputeCore"/> skips a
    /// candidate whose two operators disagree on faction before ever
    /// comparing distance, so this proves the faction gate survives the
    /// fixed candidate source and is not merely bypassed by it.
    /// </summary>
    [Fact]
    public void RunTick_TwoDifferentFactionOperatorsWithinRadius_AreNeverGrouped()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var state = BuildState(ImmutableArray.Create(
            BuildOperator(1, faction: 0, positionXWu: 0, positionYWu: 0),
            BuildOperator(2, faction: 1, positionXWu: 20, positionYWu: 0)));

        var sim = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);
        sim.RunTick(0);

        var proposalOne = sim.PendingMovementProposals.Single(p => p.EntityId == 1UL);
        var proposalTwo = sim.PendingMovementProposals.Single(p => p.EntityId == 2UL);

        Assert.NotEqual(proposalOne.GroupId, proposalTwo.GroupId);
        Assert.Equal(1UL, proposalOne.GroupId);
        Assert.Equal(2UL, proposalTwo.GroupId);
    }

    /// <summary>
    /// Changing only <see cref="SandataRuleset.GroupCohesionRadiusWu"/> — no
    /// other input, same fixture, same tick — changes whether two
    /// same-faction operators 50 world units apart group, proven through
    /// <see cref="SandataSimulation.RunTick"/> with two independently
    /// constructed <see cref="SandataRuleset"/> instances.
    /// </summary>
    [Fact]
    public void RunTick_ChangingTheRulesetCohesionRadiusAlone_ChangesWhetherOperatorsGroup()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        MissionState BuildFixture() => BuildState(ImmutableArray.Create(
            BuildOperator(1, faction: 0, positionXWu: 0, positionYWu: 0),
            BuildOperator(2, faction: 0, positionXWu: 50, positionYWu: 0)));

        var rulesetNarrow = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: 10,
            groupCohesionRadiusWu: 10,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 1024);

        var rulesetWide = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: 10,
            groupCohesionRadiusWu: 60,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 1024);

        var simNarrow = new SandataSimulation(mission, rulesetNarrow, grid, wallBuckets, BuildFixture(), ImmutableArray<CoverRecord>.Empty);
        simNarrow.RunTick(0);
        var narrowOne = simNarrow.PendingMovementProposals.Single(p => p.EntityId == 1UL);
        var narrowTwo = simNarrow.PendingMovementProposals.Single(p => p.EntityId == 2UL);
        Assert.NotEqual(narrowOne.GroupId, narrowTwo.GroupId);

        var simWide = new SandataSimulation(mission, rulesetWide, grid, wallBuckets, BuildFixture(), ImmutableArray<CoverRecord>.Empty);
        simWide.RunTick(0);
        var wideOne = simWide.PendingMovementProposals.Single(p => p.EntityId == 1UL);
        var wideTwo = simWide.PendingMovementProposals.Single(p => p.EntityId == 2UL);
        Assert.Equal(wideOne.GroupId, wideTwo.GroupId);
    }

    /// <summary>
    /// Proves the cohesion candidate source is correct beyond one physical
    /// collision cell — <c>CollisionCellSizeRaw</c> is 256 raw units, about a
    /// quarter of one world unit, so any world-unit-scale separation already
    /// clears it, but this fixture makes the margin explicit: two operators
    /// 200 world units apart (204,800 raw units, roughly 800 physical
    /// collision cells) under a custom 250-world-unit cohesion radius still
    /// group. A candidate source built by reusing the physical-contact grid
    /// (fixed 256-raw-unit cell, per <see cref="SandataCollisionGrid.RebuildWithinRange"/>'s
    /// own remarks on why that reuse is unsafe) would silently miss this
    /// pair; the per-tick cohesion grid, whose cell size stage 3 derives from
    /// the radius itself, does not.
    /// </summary>
    [Fact]
    public void RunTick_OperatorsSeparatedByManyPhysicalCollisionCells_StillGroupWithinACustomWideCohesionRadius()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var state = BuildState(ImmutableArray.Create(
            BuildOperator(1, faction: 0, positionXWu: 0, positionYWu: 0),
            BuildOperator(2, faction: 0, positionXWu: 200, positionYWu: 0)));

        var rulesetWideCohesion = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: 10,
            groupCohesionRadiusWu: 250,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 1024);

        var sim = new SandataSimulation(mission, rulesetWideCohesion, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);
        sim.RunTick(0);

        var proposalOne = sim.PendingMovementProposals.Single(p => p.EntityId == 1UL);
        var proposalTwo = sim.PendingMovementProposals.Single(p => p.EntityId == 2UL);

        Assert.Equal(proposalOne.GroupId, proposalTwo.GroupId);
    }

    /// <summary>
    /// <see cref="SandataRuleset.PathLatencyTicks"/>, for a fixture whose
    /// <see cref="MissionState.Groups"/> stays empty throughout — the fixture
    /// this test recorded before task 79a, when <c>AdvancePathService</c>
    /// never called <c>PathService.RequestPath</c> for any group at all. Task
    /// 79a's edit only submits a request for a group that actually appears in
    /// <see cref="MissionState.Groups"/> with <see cref="GroupPathState.HasOutstandingRequest"/>
    /// set; this fixture never populates that array, so
    /// <c>AdvancePathService</c>'s per-group loop still has nothing to act on
    /// and this test's original claim — <c>PathLatencyTicks</c> alone cannot
    /// move this particular fixture's outcome — still holds. See
    /// <c>RunTick_OutstandingGroupPathRequest_PublishesAtExactlyRequestTickPlusLatencyAndIsNotReissued</c>
    /// below for the fixture that now does put an outstanding request into
    /// <see cref="MissionState.Groups"/> and proves <c>PathLatencyTicks</c> is
    /// load-bearing.
    /// </summary>
    /// <remarks>
    /// What this test proves: two <see cref="SandataRuleset"/> instances
    /// differing only in <c>PathLatencyTicks</c> produce byte-for-byte
    /// identical <see cref="SandataSimulation.State"/> after several ticks of
    /// an otherwise ordinary, group-less fixture — full record equality, not
    /// merely the state hash, because <see cref="SandataRuleset.ContentHash"/>
    /// folds <c>PathLatencyTicks</c> directly (<see cref="SandataStateHasher.Compute"/>
    /// folds <c>ruleset.ContentHash</c> last), so comparing
    /// <see cref="SandataSimulation.LastStateHash"/> instead would report a
    /// difference this constant itself never causes.
    /// </remarks>
    [Fact]
    public void RunTick_PathLatencyTicksDifference_LeavesStateIdentical()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var rulesetLow = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: 10,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 1024);

        var rulesetHigh = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: 9999,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 1024);

        MissionState BuildFixture() => BuildState(ImmutableArray.Create(
            BuildOperator(1, faction: 0, positionXWu: 0, positionYWu: 0),
            BuildOperator(2, faction: 1, positionXWu: 90, positionYWu: 0)));

        var simLow = new SandataSimulation(mission, rulesetLow, grid, wallBuckets, BuildFixture(), ImmutableArray<CoverRecord>.Empty);
        var simHigh = new SandataSimulation(mission, rulesetHigh, grid, wallBuckets, BuildFixture(), ImmutableArray<CoverRecord>.Empty);

        for (var tick = 0; tick < 5; tick++)
        {
            simLow.RunTick(tick);
            simHigh.RunTick(tick);
        }

        Assert.Equal(simLow.State, simHigh.State);
    }

    /// <summary>
    /// <see cref="SandataRuleset.LoweredWallDistanceWu"/>: an operator
    /// standing exactly 8 world units from a wall — <see cref="WeaponChainPhase.Raising"/>,
    /// mid-raise, with a provisional 5 ticks left — is forced back to
    /// <see cref="WeaponChainPhase.Lowered"/> in one tick under a
    /// threshold of 8 (inclusive, per <see cref="WeaponLoweredRules"/>'s own
    /// contract), but is not forced under a threshold of 7, where the raise
    /// simply keeps counting down. Wall geometry mirrors
    /// <c>WeaponLoweredRulesTests</c>'s own fixture: a vertical wall at
    /// x = 50 spanning y in [0, 100], queried at y = 60 so the perpendicular
    /// distance is exactly <c>|x - 50|</c>.
    /// </summary>
    [Fact]
    public void RunTick_LoweredWallDistanceWuThreshold_ForcesLoweredOnlyWhenInclusive()
    {
        var grid = BuildGrid();
        var wallBuckets = WallBuckets.Build(grid, [50], [0], [50], [100]);
        var mission = BuildMission();

        // x = 42: distance to the wall at x = 50 is exactly 8.
        MissionState BuildFixture() => BuildState(ImmutableArray.Create(
            BuildOperator(
                1, faction: 0, positionXWu: 42, positionYWu: 60,
                weaponChainPhase: (int)WeaponChainPhase.Raising,
                // PROVISIONAL: any positive remaining-ticks value works to
                // distinguish "forced to Lowered" from "still Raising"; 5 is
                // an arbitrary pick with no historical or tuning meaning.
                weaponChainRemainingTicks: 5)));

        var rulesetInclusive = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: 10,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 8,
            aimToleranceBam: 1024);

        var rulesetJustOutside = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: 10,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 7,
            aimToleranceBam: 1024);

        var simInclusive = new SandataSimulation(mission, rulesetInclusive, grid, wallBuckets, BuildFixture(), ImmutableArray<CoverRecord>.Empty);
        simInclusive.RunTick(0);
        var forcedOperator = Assert.Single(simInclusive.State.Operators);
        Assert.Equal((int)WeaponChainPhase.Lowered, forcedOperator.WeaponChainPhase);
        Assert.Equal(0, forcedOperator.WeaponChainRemainingTicks);

        var simJustOutside = new SandataSimulation(mission, rulesetJustOutside, grid, wallBuckets, BuildFixture(), ImmutableArray<CoverRecord>.Empty);
        simJustOutside.RunTick(0);
        var raisingOperator = Assert.Single(simJustOutside.State.Operators);
        Assert.Equal((int)WeaponChainPhase.Raising, raisingOperator.WeaponChainPhase);
        Assert.Equal(4, raisingOperator.WeaponChainRemainingTicks);
    }

    /// <summary>
    /// <see cref="SandataRuleset.AimToleranceBam"/>: reachable only through a
    /// narrow gate — <c>raiseRequested</c> true (stage 8 selected
    /// <see cref="Sandata.Core.Simulation.OperatorIntent.Engage"/>), a
    /// remembered contact stage 9's own-tick sensing resolves to a live
    /// operator, and <see cref="WeaponChainPhase.Turning"/> already reached.
    /// This fixture opens that gate: an operator at the origin facing
    /// <see cref="Facing16.East"/> with <see cref="Bam16"/> raw aim 2,548, an
    /// opposing-faction operator 90 world units due east (inside
    /// <c>ContactMemory.IdentifyRangeWu</c> = 96, so this tick's fresh
    /// sensing classifies it <see cref="ContactTier.Identified"/> and stage 8
    /// selects <c>Engage</c>). With the rifle's
    /// <c>TurnBamPerTick</c> = 2,048, the bearing to the target is
    /// <see cref="Bam16"/> raw 0 (due east), so the shortest arc from 2,548 is
    /// -2,548, magnitude past the per-tick turn cap; the one-tick turn lands
    /// at raw 500, clamped short of the target by exactly 500 raw units —
    /// worked by hand from <see cref="Bam16.ShortestArc"/> and
    /// <see cref="WeaponChain.IsArcWithinTolerance"/> exactly as
    /// <c>AdvanceWeaponChain</c> computes them, not asserted by fiat. A
    /// tolerance of 600 admits that 500-unit residual into
    /// <see cref="WeaponChainPhase.Aiming"/> in the same tick; a tolerance of
    /// 400 does not, and the chain stays <see cref="WeaponChainPhase.Turning"/>.
    /// </summary>
    [Fact]
    public void RunTick_AimToleranceBamThreshold_CompletesTurningOnlyWhenResidualArcFitsInside()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        MissionState BuildFixture() => BuildState(ImmutableArray.Create(
            BuildOperator(
                1, faction: 0, positionXWu: 0, positionYWu: 0,
                facing: Facing16.East, aimAngle: new Bam16(2548),
                weaponChainPhase: (int)WeaponChainPhase.Turning, weaponChainRemainingTicks: 0),
            BuildOperator(2, faction: 1, positionXWu: 90, positionYWu: 0)));

        var rulesetWide = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: 10,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 600);

        var rulesetNarrow = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: 10,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 400);

        var simWide = new SandataSimulation(mission, rulesetWide, grid, wallBuckets, BuildFixture(), ImmutableArray<CoverRecord>.Empty);
        simWide.RunTick(0);
        var completedOperator = simWide.State.Operators.Single(op => op.EntityId == 1UL);
        Assert.Equal((int)WeaponChainPhase.Aiming, completedOperator.WeaponChainPhase);

        var simNarrow = new SandataSimulation(mission, rulesetNarrow, grid, wallBuckets, BuildFixture(), ImmutableArray<CoverRecord>.Empty);
        simNarrow.RunTick(0);
        var stillTurningOperator = simNarrow.State.Operators.Single(op => op.EntityId == 1UL);
        Assert.Equal((int)WeaponChainPhase.Turning, stillTurningOperator.WeaponChainPhase);
    }

    /// <summary>
    /// Task 79c (Sandata's scaffold plan, the wave-12
    /// audit's corrected obligation): <see cref="OperatorState.Firearm"/>
    /// genuinely drives stage 11 through <see
    /// cref="SandataSimulation.RunTick"/> — not by calling <see
    /// cref="SandataSimulation.AdvanceWeaponChain"/> directly. Two otherwise
    /// identical single-operator fixtures differ only in <c>Firearm</c>: an
    /// <see cref="FirearmId.Ak47"/> rifle and a <see
    /// cref="FirearmId.Beretta92Fs"/> pistol. Chosen deliberately — <see
    /// cref="FirearmCatalog"/>'s <c>Rifle(...)</c>/<c>Pistol(...)</c> factory
    /// methods give every rifle row and every pistol row identical timing
    /// fields within its own class, so any two rifles (or any two pistols)
    /// would tie on every field stage 11 reads. Only a rifle-versus-pistol
    /// pair genuinely differs in <c>ReadyMs</c> (405 vs 80), <c>AimBaseMs</c>
    /// (335 vs 165), <c>AimPerBamMs</c> (5 vs 3), <c>ResetMs</c> (150 vs
    /// 120), and <c>TurnBamPerTick</c> (2,048 vs 4,096).
    /// <para>
    /// The opposing-faction operator at 90 world units is inside <see
    /// cref="ContactMemory.IdentifyRangeWu"/> (96), exactly mirroring <see
    /// cref="RunTick_AimToleranceBamThreshold_CompletesTurningOnlyWhenResidualArcFitsInside"/>'s
    /// own fixture, so stage 8 selects <see cref="OperatorIntent.Engage"/>
    /// and <c>raiseRequested</c> is <see langword="true"/> on the very first
    /// tick. Operator 1 starts <see cref="WeaponChainPhase.Lowered"/>
    /// (<see cref="BuildOperator"/>'s default), so <see
    /// cref="WeaponChain.Advance"/> walks it straight into <see
    /// cref="WeaponChainPhase.Raising"/> within the same call, seeding
    /// <c>WeaponChainRemainingTicks</c> fresh from <c>readyTicks</c> — <see
    /// cref="TickConversion.ToTicks"/>'s pinned <c>(ms * TickRate + 500) /
    /// 1000</c> rule at the ruleset's 50 Hz gives 20 ticks for the rifle's
    /// 405 ms and 4 ticks for the pistol's 80 ms. That is the observed
    /// divergence: two operators, identical in every other field, land in
    /// the same phase with different remaining-tick counts after the same
    /// single <see cref="SandataSimulation.RunTick"/> call, solely because
    /// their <see cref="OperatorState.Firearm"/> differs.
    /// </para>
    /// </summary>
    [Fact]
    public void RunTick_OperatorsWithDifferentFirearms_AdvanceDifferentWeaponChainTiming()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();
        var ruleset = SandataRuleset.ModernTacticalV1;

        MissionState BuildFixture(FirearmId firearm) => BuildState(ImmutableArray.Create(
            BuildOperator(1, faction: 0, positionXWu: 0, positionYWu: 0) with { Firearm = firearm },
            BuildOperator(2, faction: 1, positionXWu: 90, positionYWu: 0)));

        var simRifle = new SandataSimulation(mission, ruleset, grid, wallBuckets, BuildFixture(FirearmId.Ak47), ImmutableArray<CoverRecord>.Empty);
        simRifle.RunTick(0);
        var rifleOperator = simRifle.State.Operators.Single(op => op.EntityId == 1UL);

        var simPistol = new SandataSimulation(mission, ruleset, grid, wallBuckets, BuildFixture(FirearmId.Beretta92Fs), ImmutableArray<CoverRecord>.Empty);
        simPistol.RunTick(0);
        var pistolOperator = simPistol.State.Operators.Single(op => op.EntityId == 1UL);

        Assert.Equal((int)WeaponChainPhase.Raising, rifleOperator.WeaponChainPhase);
        Assert.Equal((int)WeaponChainPhase.Raising, pistolOperator.WeaponChainPhase);
        Assert.Equal(20, rifleOperator.WeaponChainRemainingTicks);
        Assert.Equal(4, pistolOperator.WeaponChainRemainingTicks);
        Assert.NotEqual(rifleOperator.WeaponChainRemainingTicks, pistolOperator.WeaponChainRemainingTicks);
    }

    // ---- 7. ADDITIVE ORDER LAYER ---------------------------------------

    /// <summary>
    /// A resumed <see cref="OrderQueue"/> whose counters have advanced away
    /// from zero but which carries no orders (<see cref="SandataSimulation.RestoreOrderQueue"/>
    /// with <c>nextOrderId: 1, nextOrderSequence: 1</c> and an empty order
    /// array) must behave identically, tick for tick, to the fresh
    /// <see cref="OrderQueue.Empty"/> a new mission starts from: stage 1's
    /// <c>ApplyOrders</c> reads only <see cref="OrderQueue.InApplicationOrder"/>,
    /// never the counters, so an empty order list produces the same
    /// <see cref="MissionState.OrderAssignments"/>, the same
    /// <see cref="MissionState.Operators"/>, and the same
    /// <see cref="SandataSimulation.PendingMovementProposals"/> either way.
    /// </summary>
    /// <remarks>
    /// <b>Not a defect, and deliberately not asserted as hash-equal.</b>
    /// <see cref="SandataStateHasher"/>'s own <c>FoldOrderQueue</c> folds
    /// <c>NextOrderId</c> and <c>NextOrderSequence</c> into the state hash
    /// whenever the queue is not exactly equal to <see cref="OrderQueue.Empty"/>
    /// — its own remarks name this precise resumed-but-empty shape and cite
    /// <c>OrderStateHashTests</c> as the file that already pins it. A queue
    /// restored at counters (1, 1) is therefore not record-equal to
    /// <see cref="OrderQueue.Empty"/>, so its counters are folded and the
    /// resulting <see cref="SandataSimulation.LastStateHash"/> differs from
    /// the fresh-queue run's, by design, even though every operator-visible
    /// outcome below is identical. This test asserts that documented
    /// divergence explicitly, rather than silently avoiding it, so a reader
    /// does not mistake it for a determinism defect this file failed to
    /// catch.
    /// </remarks>
    [Fact]
    public void RunTick_ResumedEmptyOrderQueueVersusFreshEmptyOrderQueue_ProduceIdenticalOperatorsButDivergentHash()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        MissionState BuildFixture(OrderQueue queue) => BuildState(ImmutableArray.Create(
            BuildOperator(1, faction: 0, positionXWu: 0, positionYWu: 0),
            BuildOperator(2, faction: 1, positionXWu: 90, positionYWu: 0)))
            with
        { OrderQueue = queue };

        var resumedQueue = SandataSimulation.RestoreOrderQueue(
            nextOrderId: 1, nextOrderSequence: 1, ImmutableArray<Order>.Empty);

        var simResumed = new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildFixture(resumedQueue), ImmutableArray<CoverRecord>.Empty);
        var simFresh = new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildFixture(OrderQueue.Empty), ImmutableArray<CoverRecord>.Empty);

        for (var tick = 0; tick < 5; tick++)
        {
            simResumed.RunTick(tick);
            simFresh.RunTick(tick);
        }

        Assert.True(simResumed.State.Operators.SequenceEqual(simFresh.State.Operators));
        Assert.True(simResumed.State.OrderAssignments.SequenceEqual(simFresh.State.OrderAssignments));
        Assert.True(simResumed.PendingMovementProposals.SequenceEqual(simFresh.PendingMovementProposals));

        Assert.NotEqual(resumedQueue, OrderQueue.Empty);
        Assert.NotEqual(simResumed.State, simFresh.State);
        Assert.NotNull(simResumed.LastStateHash);
        Assert.NotNull(simFresh.LastStateHash);
        Assert.NotEqual(simResumed.LastStateHash, simFresh.LastStateHash);
    }

    // ---- 7. STAGE 7, PATH REQUEST DRAIN -----------------------------------

    /// <summary>
    /// Task 79a: <see cref="SandataSimulation"/>'s stage 7 call site
    /// (<c>AdvancePathService</c>) now drains <see cref="MissionState.Groups"/>
    /// into <see cref="PathService.RequestPath"/> instead of never calling it
    /// (wave 9's <c>RunTick_PathLatencyTicksDifference_LeavesStateIdentical</c>
    /// recorded that gap; its own doc comment is corrected alongside this
    /// test). This is the first fixture to make
    /// <see cref="SandataRuleset.PathLatencyTicks"/> observably change
    /// <see cref="SandataSimulation.RunTick"/>'s output for a group that
    /// actually has a destination.
    /// </summary>
    /// <remarks>
    /// <b>Why <see cref="OperatorIntent"/>, not <c>PathService.GetCurrentPath</c>
    /// directly.</b> <see cref="SandataSimulation"/> exposes no accessor to its
    /// private <c>_pathService</c> field, and this task's edit is scoped to
    /// <c>AdvancePathService</c>'s own body only — adding one would touch a
    /// different part of the file. <see cref="SandataSimulation.PendingIntents"/>
    /// is instead the public, <see cref="SandataSimulation.RunTick"/>-driven
    /// proxy: with a solo operator (no contact, no suppression, no breach
    /// point — every higher-priority branch in <c>IntentSelection.Select</c>'s
    /// cascade stays false), stage 8 selects <see cref="OperatorIntent.Advance"/>
    /// exactly when stage 7 published <see cref="PathReasonCode.PathValid"/>
    /// for that operator's group, and <see cref="OperatorIntent.Hold"/>
    /// exactly when it is still <see cref="PathReasonCode.AwaitingLatency"/> —
    /// the same empty-then-non-empty transition
    /// <c>PathService.GetCurrentPath</c> would show, one layer further out.
    /// <see cref="MissionState.Groups"/> itself, directly on the public
    /// <see cref="SandataSimulation.State"/>, is what proves the second half:
    /// the request is cleared on the exact publish tick and never re-issued.
    /// </remarks>
    [Fact]
    public void RunTick_OutstandingGroupPathRequest_PublishesAtExactlyRequestTickPlusLatencyAndIsNotReissued()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var startCell = grid.CellIndex(0, 0);
        var goalCell = grid.CellIndex(5, 0);

        const int pathLatencyTicks = 3;
        var ruleset = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: pathLatencyTicks,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 1024);

        var op = BuildOperator(entityId: 1, faction: 0, positionXWu: 0, positionYWu: 0);
        var groupState = new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: goalCell,
            HasOutstandingRequest: true,
            StartCellIndex: startCell,
            GoalCellIndex: goalCell,
            RequestTick: 0);
        var state = BuildState(ImmutableArray.Create(op)) with { Groups = ImmutableArray.Create(groupState) };

        var sim = new SandataSimulation(mission, ruleset, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);

        for (var tick = 0; tick < pathLatencyTicks; tick++)
        {
            sim.RunTick(tick);

            var group = Assert.Single(sim.State.Groups);
            Assert.True(group.HasOutstandingRequest, $"tick {tick}: must not publish before RequestTick + PathLatencyTicks");

            var intent = Assert.Single(sim.PendingIntents);
            Assert.Equal(OperatorIntent.Hold, intent.Intent);
        }

        sim.RunTick(pathLatencyTicks);

        var publishedGroup = Assert.Single(sim.State.Groups);
        Assert.False(publishedGroup.HasOutstandingRequest, "request must clear on the exact publish tick");

        var publishedIntent = Assert.Single(sim.PendingIntents);
        Assert.Equal(OperatorIntent.Advance, publishedIntent.Intent);

        // Not re-issued: a later tick leaves the already-published request
        // cleared rather than resubmitting it.
        sim.RunTick(pathLatencyTicks + 1);
        var laterGroup = Assert.Single(sim.State.Groups);
        Assert.False(laterGroup.HasOutstandingRequest);
        var laterIntent = Assert.Single(sim.PendingIntents);
        Assert.Equal(OperatorIntent.Advance, laterIntent.Intent);
    }

    // ---- 8. STAGE 9, AUTONOMOUS BRANCH -------------------------------------

    /// <summary>
    /// Task 79b, amended by task 84: an unassigned operator in a group with a
    /// published path follows that path's actual bent shape rather than
    /// heading for the goal, and — since task 84 — walks it at the designed
    /// sprint speed instead of arriving in a single stride. Reached only
    /// through <see cref="SandataSimulation.RunTick"/> and observed only
    /// through the committed <see cref="SandataSimulation.State"/> position,
    /// never through <c>ComputeMovementProposals</c> directly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Geometry.</b> With cell size 4 (<see cref="NavGrid.CellSizeWu"/>),
    /// start cell (0, 0) and goal cell (6, 3) sit at world-unit centres
    /// (2, 2) and (26, 14); a wall segment running the full grid height at
    /// x = 14 forces the funnel-smoothed path through the vertices (2, 2),
    /// (10, 10), (14, 14), (18, 14), (26, 14) instead of a straight line
    /// (confirmed empirically against the real
    /// <see cref="Sandata.Core.Navigation.PathService"/> output before this
    /// test's original, task 79b version was written).
    /// </para>
    /// <para>
    /// <b>The first published tick, derived rather than read back from a
    /// run.</b> The operator spawns exactly on the path's own start vertex at
    /// raw (2,048, 2,048), so its projected arclength is zero and the leader's
    /// sample arclength is one per-tick step, 1,638 raw. Since task 87 the
    /// first segment's stored length is the integer square root of a *raw*
    /// square: (8·1,024)² · 2 = 134,217,728, whose root truncates to 11,585
    /// against a true 8,192·√2 ≈ 11,585.24. <see cref="PolylineArclength.SampleAt"/>
    /// at arclength 1,638 therefore returns
    /// 2,048 + 8,192·1,638/11,585 = 2,048 + 1,158 = <b>3,206 raw</b> on both
    /// axes, and the displacement of (1,158, 1,158) has a magnitude of about
    /// 1,637.4 raw, just inside the 1,638 cap, so the clamp does not bind on
    /// this tick at all.
    /// </para>
    /// <para>
    /// <b>That the pinned value is the same 3,206 it was before task 87 is a
    /// coincidence worth naming, not evidence that nothing changed.</b> Under
    /// the old world-unit table the sample landed on (7, 7) — raw
    /// (7,168, 7,168) — a displacement of 5,120 raw per axis, and stage 9's
    /// clamp then scaled it down by 1,638/7,240 to 1,158. The new arithmetic
    /// reaches 1,158 by walking exactly one step along the segment instead.
    /// Both round to the same integer; only one of them is a leader that moves
    /// at the speed the design specifies rather than one whose target
    /// overshoots by four and a half strides and is reeled back in.
    /// </para>
    /// <para>
    /// <b>The two properties this test has always existed to prove, both
    /// preserved.</b> First, the operator's opening move lands on the first
    /// segment toward (10, 10) rather than on the goal at (26, 14) — under
    /// the rejected task 79b pin of <c>leaderArclength</c> to
    /// <see cref="PolylineArclength.TotalLength"/> the target would have been
    /// the goal itself from the first published tick onward. Second, once the
    /// operator has walked past the polyline's (14, 14) corner it sits on the
    /// corridor's own Y of 14, whereas a straight spawn-to-goal beeline would
    /// read Y = 10 at X = 19 (slope 12/24 from (2, 2) to (26, 14)); the
    /// assertion allows for the fixed-point residue of the diagonal approach
    /// and only requires the operator to be clear of the beeline by two whole
    /// world units, which no beeline can satisfy.
    /// </para>
    /// <para>
    /// <b>What task 84 adds.</b> No single tick may displace the operator by
    /// more than the per-tick cap — the assertion that makes a teleport
    /// impossible to pass — and the operator must still arrive at the goal,
    /// which is what separates a slowed walk from a stalled one. That second
    /// half is not decoration: task 84's first implementation set the
    /// lookahead to the per-tick step rounded up to 2 world units, and at that
    /// lookahead the round-trip arclength quantization on a diagonal segment
    /// froze this very operator at (4, 4) permanently. An arrival assertion is
    /// what catches that; a "the position changed" assertion is not.
    /// </para>
    /// <para>
    /// <b>What task 87 adds.</b> The lookahead this test exercises *is* the
    /// per-tick step now — the reduction task 84 could not make — because the
    /// arclength round trip loses a raw unit or two rather than a world unit
    /// or two. The arrival assertion below is therefore the direct proof that
    /// the deadlock task 84 documented no longer exists at that value.
    /// </para>
    /// </remarks>
    [Fact]
    public void RunTick_UnassignedOperatorInGroupWithPublishedPath_WalksTheBentPolylineAtTheDesignedSpeed()
    {
        var grid = BuildGrid();
        var wallBuckets = WallBuckets.Build(grid, [14L], [-100L], [14L], [100L]);
        var mission = BuildMission();

        var startCell = grid.CellIndex(0, 0);
        var goalCell = grid.CellIndex(6, 3);

        const int pathLatencyTicks = 1;
        var ruleset = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: pathLatencyTicks,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 1024);

        // Spawns on the path's own start vertex (2, 2) — the world-unit
        // centre of cell (0, 0) — so the very first projection has a known,
        // exact starting arclength of zero.
        var op = BuildOperator(entityId: 1, faction: 0, positionXWu: 2, positionYWu: 2);
        var groupState = new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: goalCell,
            HasOutstandingRequest: true,
            StartCellIndex: startCell,
            GoalCellIndex: goalCell,
            RequestTick: 0);
        var state = BuildState(ImmutableArray.Create(op)) with { Groups = ImmutableArray.Create(groupState) };

        var sim = new SandataSimulation(mission, ruleset, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);

        // Request tick: path not yet published, so stage 9 still holds.
        sim.RunTick(0);
        var stillAtSpawn = Assert.Single(sim.State.Operators);
        Assert.Equal(2 * FixedPoint.Scale, stillAtSpawn.PositionX.RawValue);
        Assert.Equal(2 * FixedPoint.Scale, stillAtSpawn.PositionY.RawValue);

        // Publish tick: stage 7 publishes the path before stage 9 runs, so
        // this same tick's proposal already targets a point on it. The exact
        // raw 3,206 is derived in the remarks above, not copied from a run.
        sim.RunTick(pathLatencyTicks);
        var afterFirstMove = Assert.Single(sim.State.Operators);
        Assert.Equal(3206, afterFirstMove.PositionX.RawValue);
        Assert.Equal(3206, afterFirstMove.PositionY.RawValue);

        // First property: that opening move is onto the first segment toward
        // (10, 10) and nowhere near the goal's (26, 14).
        Assert.True(afterFirstMove.PositionX.RawValue > 2 * FixedPoint.Scale);
        Assert.True(afterFirstMove.PositionX.RawValue < 10 * FixedPoint.Scale);

        // Design section 4's sprint of 80 world units per second at the
        // fixture's tick rate of 50, in raw fixed-point units.
        const int movementSpeedRaw = 80 * FixedPoint.Scale / 50;

        var previousX = afterFirstMove.PositionX.RawValue;
        var previousY = afterFirstMove.PositionY.RawValue;
        var reachedTheCorridor = false;
        var yPastTheCorner = 0;

        // Sixty ticks is more than three times what the polyline's roughly 29
        // world units of length needs at 1.6 world units per tick, so a run
        // that has not arrived by then has stalled rather than merely been
        // slowed.
        for (var tick = pathLatencyTicks + 1; tick <= pathLatencyTicks + 60; tick++)
        {
            sim.RunTick(tick);
            var current = Assert.Single(sim.State.Operators);
            var x = current.PositionX.RawValue;
            var y = current.PositionY.RawValue;

            var dx = (long)x - previousX;
            var dy = (long)y - previousY;
            Assert.True(
                (dx * dx) + (dy * dy) <= (long)movementSpeedRaw * movementSpeedRaw,
                $"tick {tick} displaced the operator further than the per-tick cap of {movementSpeedRaw} raw");

            if (!reachedTheCorridor && x >= 19 * FixedPoint.Scale)
            {
                reachedTheCorridor = true;
                yPastTheCorner = y;
            }

            previousX = x;
            previousY = y;
        }

        // Second property: past the (14, 14) corner the operator is on the
        // corridor, not on the spawn-to-goal beeline, which would read
        // Y = 10 at X = 19.
        Assert.True(reachedTheCorridor, "the operator never reached X = 19");
        Assert.True(
            yPastTheCorner > 12 * FixedPoint.Scale,
            $"at X = 19 the operator's Y was {yPastTheCorner} raw, which is the beeline's Y rather than the corridor's");

        // Task 84: the clamp slows the walk; it must not stall it.
        var arrived = Assert.Single(sim.State.Operators);
        Assert.Equal(26 * FixedPoint.Scale, arrived.PositionX.RawValue);
        Assert.Equal(14 * FixedPoint.Scale, arrived.PositionY.RawValue);
    }

    // ---- 9. TASK 84, MOVEMENT SPEED CLAMP ----------------------------------

    /// <summary>
    /// Task 84: an ordered operator (an <see cref="OrderAssignment"/> whose
    /// single path node sits far from the operator's own spawn point) must
    /// take more than one <see cref="SandataSimulation.RunTick"/> call to
    /// arrive, and no single tick's displacement may exceed the per-tick
    /// speed cap <see cref="SandataSimulation.ComputeMovementProposals"/>
    /// derives from design section 4's 5 m/s sprint. This asserts the
    /// displacement magnitude every tick, not merely that the position
    /// changed, so an unclamped implementation (one <c>RunTick</c> jumping
    /// straight to the waypoint) fails it immediately.
    /// </summary>
    [Fact]
    public void RunTick_OrderedOperatorFarFromWaypoint_ClampsPerTickDisplacementToDesignedSprintSpeed()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();
        var op = BuildOperator(entityId: 1, faction: 0, positionXWu: 0, positionYWu: 0);
        var state = BuildState(ImmutableArray.Create(op));

        var sim = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);

        // Two far nodes, neither at the spawn point: an authored path needs
        // at least two nodes (Order.MaxAuthoredPathNodeCount's own lower
        // bound, OrderValidation.ValidateMoveAlongPath).
        //
        // Amended 2026-08-12. This test used to assert that the operator's X
        // came to rest exactly on node 0, at 40 wu, and its own comment
        // explained why that was safe: "CurrentNodeIndex is never advanced
        // anywhere in production code today (confirmed by search)". That was
        // true and it was the defect — an operator handed a polyline walked to
        // its first node and stood there for the rest of the run. Stage 1 now
        // advances the index and clears the assignment at the final node, so
        // the operator passes node 0 without stopping on it and comes to rest
        // near node 1 instead. What this test measures — that the per-tick
        // displacement never exceeds the sprint cap, and that a far waypoint
        // takes more than one tick — is unchanged.
        var pathNodes = ImmutableArray.Create(new OrderPathNode(40, 0), new OrderPathNode(60, 0));
        var (_, _, rejection) = sim.SubmitOrder(
            targetTick: 0,
            factionId: 0,
            addressees: ImmutableArray.Create(1UL),
            kind: OrderKind.MoveAlongPath,
            pathNodes: pathNodes);
        Assert.Null(rejection);

        // Design section 4: 5 m/s sprint = 80 wu/s, truncated to raw at
        // ModernTacticalV1's own TickRate - independently re-derived here
        // from public building blocks, not copied from the production
        // constant this test assembly cannot see.
        var movementSpeedRaw = (80L * FixedPoint.Scale) / SandataRuleset.ModernTacticalV1.TickRate;

        var previousXRaw = 0L;
        var reachedFinalWaypoint = false;
        var ticksRun = 0;

        // The assignment clears once the final node is reached, so the loop
        // watches the assignment rather than an exact coordinate: an operator
        // is "arrived" when stage 1 has stopped giving it somewhere to be.
        for (var tick = 0; tick < 80 && !reachedFinalWaypoint; tick++)
        {
            sim.RunTick(tick);
            ticksRun++;

            var current = Assert.Single(sim.State.Operators).PositionX.RawValue;
            var deltaX = current - previousXRaw;

            Assert.True(deltaX >= 0, $"tick {tick}: operator moved backward away from a forward waypoint");
            Assert.True(
                deltaX * deltaX <= movementSpeedRaw * movementSpeedRaw,
                $"tick {tick}: displacement {deltaX} raw exceeds the per-tick cap {movementSpeedRaw} raw");

            previousXRaw = current;
            reachedFinalWaypoint = sim.State.OrderAssignments.IsEmpty;
        }

        Assert.True(reachedFinalWaypoint, "operator must eventually reach the authored polyline's final node");
        Assert.True(ticksRun > 1, "a 40 wu waypoint must take more than one tick at the designed sprint speed");

        // It walked to the far end of the polyline rather than stopping on the
        // first node it met.
        var arrivedX = Assert.Single(sim.State.Operators).PositionX.RawValue;
        Assert.True(
            arrivedX > 40L * FixedPoint.Scale,
            $"operator came to rest at {arrivedX} raw, at or before node 0 rather than past it");
    }

    /// <summary>
    /// Task 84: the same displacement bound as the ordered-branch test
    /// above, proven for the autonomous branch - a group leader following a
    /// published path with no <see cref="OrderAssignment"/> of its own. The
    /// published path here is a straight, axis-aligned line (open grid, no
    /// walls) so segment length and arclength projection are both exact
    /// integer divisions with no truncation loss; this test is about the
    /// speed clamp, not <see cref="PolylineArclength"/>'s own quantization
    /// behaviour on a diagonal segment (see the rewritten bent-polyline
    /// test's remarks for that separate, reported defect).
    /// </summary>
    [Fact]
    public void RunTick_AutonomousLeaderFarAlongPublishedPath_ClampsPerTickDisplacementToDesignedSprintSpeed()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var startCell = grid.CellIndex(0, 0);
        var goalCell = grid.CellIndex(25, 0);

        const int pathLatencyTicks = 1;
        var ruleset = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: pathLatencyTicks,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 1024);

        var op = BuildOperator(entityId: 1, faction: 0, positionXWu: 2, positionYWu: 2);
        var groupState = new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: goalCell,
            HasOutstandingRequest: true,
            StartCellIndex: startCell,
            GoalCellIndex: goalCell,
            RequestTick: 0);
        var state = BuildState(ImmutableArray.Create(op)) with { Groups = ImmutableArray.Create(groupState) };

        var sim = new SandataSimulation(mission, ruleset, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);

        var movementSpeedRaw = (80L * FixedPoint.Scale) / ruleset.TickRate;

        var previousXRaw = 2L * FixedPoint.Scale;
        var previousYRaw = 2L * FixedPoint.Scale;

        for (var tick = 0; tick <= pathLatencyTicks + 20; tick++)
        {
            sim.RunTick(tick);

            var current = Assert.Single(sim.State.Operators);
            var deltaX = current.PositionX.RawValue - previousXRaw;
            var deltaY = current.PositionY.RawValue - previousYRaw;

            Assert.True(
                (deltaX * deltaX) + (deltaY * deltaY) <= movementSpeedRaw * movementSpeedRaw,
                $"tick {tick}: displacement ({deltaX}, {deltaY}) raw exceeds the per-tick cap {movementSpeedRaw} raw");

            previousXRaw = current.PositionX.RawValue;
            previousYRaw = current.PositionY.RawValue;
        }

        // 21 ticks at <= 1,638 raw/tick cannot cover the 100 wu (102,400
        // raw) gap to the goal - the bound above is not vacuously true.
        var final = Assert.Single(sim.State.Operators);
        Assert.True(
            final.PositionX.RawValue < 102 * FixedPoint.Scale,
            "leader must not have already reached a 100 wu goal after 21 clamped ticks");
        Assert.True(
            final.PositionX.RawValue > 2 * FixedPoint.Scale,
            "leader must actually have moved from its spawn point across 21 ticks");
    }

    /// <summary>
    /// Task 89, the healthy half. An operator whose group has a published
    /// path and whose route is clear of other bodies walks that path from end
    /// to end and <b>arrives at the goal</b>, one designed sprint step per
    /// tick. Arrival is the assertion a stall cannot satisfy, and it is what
    /// separates this test from
    /// <see cref="RunTick_AutonomousLeaderFarAlongPublishedPath_ClampsPerTickDisplacementToDesignedSprintSpeed"/>
    /// above, which deliberately stops short and pins the per-tick bound
    /// instead.
    /// <para>
    /// This is also the fixture task 90 needs: a mover that keeps moving
    /// across a stated window, rather than one that looks active and is inert
    /// inside the window an assertion actually covers.
    /// </para>
    /// </summary>
    [Fact]
    public void RunTick_AutonomousLeaderWithAClearRoute_WalksThePublishedPathToItsGoal()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var startCell = grid.CellIndex(0, 0);
        var goalCell = grid.CellIndex(25, 0);

        const int pathLatencyTicks = 1;
        var ruleset = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: pathLatencyTicks,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 1024);

        // (2, 2) world units is nav cell (0, 0)'s own centre, and the goal
        // cell (25, 0)'s centre is (102, 2) - both derived from
        // NavGrid.CellSizeWu 4 rather than pinned as bare literals, so the
        // 100 wu of travel between them is the grid's arithmetic and not this
        // test's assumption.
        const int goalXWu = (25 * NavGrid.CellSizeWu) + (NavGrid.CellSizeWu / 2);
        const int startYWu = (0 * NavGrid.CellSizeWu) + (NavGrid.CellSizeWu / 2);

        var op = BuildOperator(entityId: 1, faction: 0, positionXWu: 2, positionYWu: startYWu);
        var groupState = new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: goalCell,
            HasOutstandingRequest: true,
            StartCellIndex: startCell,
            GoalCellIndex: goalCell,
            RequestTick: 0);
        var state = BuildState(ImmutableArray.Create(op)) with { Groups = ImmutableArray.Create(groupState) };

        var sim = new SandataSimulation(mission, ruleset, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);

        var movementSpeedRaw = (80L * FixedPoint.Scale) / ruleset.TickRate;

        // 100 wu at 1,638 raw/tick is 63 whole steps; the latency ticks and
        // one partial final step sit on top of that, and the cap below leaves
        // room for both without ever being reachable by a mover that stalls.
        const int tickBudget = 100;

        var previousXRaw = 2L * FixedPoint.Scale;
        var previousYRaw = (long)startYWu * FixedPoint.Scale;
        var arrivedAtTick = -1;

        for (var tick = 0; tick < tickBudget && arrivedAtTick < 0; tick++)
        {
            sim.RunTick(tick);

            var current = Assert.Single(sim.State.Operators);
            var deltaX = current.PositionX.RawValue - previousXRaw;
            var deltaY = current.PositionY.RawValue - previousYRaw;

            Assert.True(
                (deltaX * deltaX) + (deltaY * deltaY) <= movementSpeedRaw * movementSpeedRaw,
                $"tick {tick}: displacement ({deltaX}, {deltaY}) raw exceeds the per-tick cap {movementSpeedRaw} raw");

            previousXRaw = current.PositionX.RawValue;
            previousYRaw = current.PositionY.RawValue;

            if (current.PositionX.RawValue == goalXWu * FixedPoint.Scale &&
                current.PositionY.RawValue == startYWu * FixedPoint.Scale)
            {
                arrivedAtTick = tick;
            }
        }

        Assert.True(
            arrivedAtTick >= 0,
            $"leader must reach the goal ({goalXWu}, {startYWu}) wu within {tickBudget} ticks; " +
            $"it stopped at ({previousXRaw}, {previousYRaw}) raw");

        // Arrival that took one stride would mean the clamp was not applied
        // at all, which would satisfy the assertion above for the wrong
        // reason.
        Assert.True(
            arrivedAtTick > 60,
            $"100 wu at {movementSpeedRaw} raw per tick cannot be covered in {arrivedAtTick + 1} ticks");
    }

    /// <summary>
    /// Task 89, the finding. An operator with a freshly published group path
    /// takes one step and then holds position forever when another body
    /// stands on its route. The cause is <b>not</b> any of the four the task
    /// row proposed: the derived <see cref="SquadSlot.GroupId"/> and
    /// <see cref="SquadSlot.SlotIndex"/> never change, the leader never
    /// reaches the end of the polyline, no lateral offset is gated, and
    /// <see cref="RunTick_AutonomousLeaderWithAClearRoute_WalksThePublishedPathToItsGoal"/>
    /// above proves the arclength arithmetic walks the identical path to its
    /// goal once the route is clear.
    /// <para>
    /// The cause is stage 10, and it is two correct components composing into
    /// a permanent stall. <c>LocalAvoidance.Commit</c> refuses a step whose
    /// destination would overlap another body, and then offers exactly one
    /// retry: <c>SidestepRules.Sidestep</c>'s single 22.5-degree rotation of
    /// that same delta, to the side <c>entityId</c> parity picks. Design
    /// section 8 states that rule in full - "if that is also blocked, it
    /// waits a tick" - and says nothing about what happens when the blocker
    /// never moves. Here it never does, so every input to both candidates is
    /// identical on every subsequent tick and both are rejected forever. Head
    /// on, a 22.5-degree turn does not clear a body whose radius is larger
    /// than the step that turns.
    /// </para>
    /// <para>
    /// <b>Rewritten 2026-08-11, and the paragraphs above are now history
    /// rather than current behaviour.</b> This fixture can no longer reach the
    /// stage 10 stall, because the blocker it uses is an <em>opposing
    /// faction</em> body 28 wu away — and stage 9 now halts an operator that
    /// stage 8 gave <see cref="OperatorIntent.Engage"/> when its best contact
    /// is inside its firearm's effective range. The leader stops to shoot the
    /// blocker before it can ever walk into it, so what this fixture now
    /// demonstrates is the halt, not the stall. The old assertions failed
    /// exactly as they were written to: "stage 9 stopped proposing a move, so
    /// this is not a stage 10 stall".
    /// </para>
    /// <para>
    /// <b>Task 89's stall is not fixed and has not been lost.</b> Nothing in
    /// <c>LocalAvoidance</c> changed. The stall is a stage 10 property, and it
    /// is now pinned where it actually lives, against
    /// <c>LocalAvoidance.Commit</c> directly, by
    /// <c>LocalAvoidanceTests.CommitAgainstAStaticBody_StallsForeverBecauseTheOneSidestepIsBlockedToo</c>.
    /// That is a better home for it than this one: at stage 10 there is no
    /// sensing, no intent, and no faction, so no future combat rule can
    /// silently stop the fixture from reaching the defect the way this one
    /// just did.
    /// </para>
    /// </summary>
    [Fact]
    public void RunTick_HostileBodyOnThePublishedPath_HaltsTheLeaderToEngageBeforeItWalksIntoTheBody()
    {
        // 96 cells at NavGrid.CellSizeWu 4 is 384 wu across, deliberately
        // wider than this file's 32-cell default. ContactMemory.DetectRangeWu
        // is 256, so on a 128 wu grid every hostile is already inside
        // detection range at tick 0 and the leader could never be seen to walk
        // and then stop — a correct halt would be indistinguishable from a
        // leader that never moved at all.
        var grid = BuildGrid(width: 96, height: 32);
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var startCell = grid.CellIndex(0, 0);
        var goalCell = grid.CellIndex(90, 0);

        const int pathLatencyTicks = 1;
        var ruleset = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: pathLatencyTicks,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 1024);

        // The leader walks east along the cell-centre line y = 2 wu; the
        // blocker stands on that same line 28 wu ahead of it. Opposing
        // faction, so SquadGrouping never unions the two and the blocker
        // never acquires a slot in the leader's formation; no group of its
        // own has a path, so its every proposal is its own position. Both
        // carry health far above anything stage 13 can remove inside this
        // window, so the blocker cannot be shot out of the way and turn a
        // movement finding into a combat one - asserted below rather than
        // assumed.
        const int blockerXWu = 300;
        var leader = BuildOperator(entityId: 1, faction: 0, positionXWu: 2, positionYWu: 2)
            with
        { Health = 1_000_000 };
        var blocker = BuildOperator(entityId: 3, faction: 1, positionXWu: blockerXWu, positionYWu: 2)
            with
        { Health = 1_000_000 };

        var groupState = new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: goalCell,
            HasOutstandingRequest: true,
            StartCellIndex: startCell,
            GoalCellIndex: goalCell,
            RequestTick: 0);
        var state = BuildState(ImmutableArray.Create(leader, blocker)) with
        {
            Groups = ImmutableArray.Create(groupState),
        };

        var sim = new SandataSimulation(mission, ruleset, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);

        var movementSpeedRaw = (80L * FixedPoint.Scale) / ruleset.TickRate;

        // Long enough that the leader covers the ~19 wu of clear ground
        // before the blocker's body and then sits against it for many times
        // as many ticks as it took to get there.
        // The leader halts when the blocker reaches ContactMemory
        // .IdentifyRangeWu (96), which from 2 wu against a blocker at 300 wu
        // is about 202 wu of walking at 1.6 wu per tick — roughly 126 ticks.
        // The window opens well after that so the halt has settled.
        const int tickCount = 200;
        const int stallWindowStart = 160;

        long stalledXRaw = 0;
        long stalledYRaw = 0;

        for (var tick = 0; tick < tickCount; tick++)
        {
            sim.RunTick(tick);

            var leaderNow = sim.State.Operators.Single(o => o.EntityId == 1UL);
            var blockerNow = sim.State.Operators.Single(o => o.EntityId == 3UL);

            Assert.True(
                blockerNow.PositionX.RawValue == blockerXWu * FixedPoint.Scale &&
                blockerNow.PositionY.RawValue == 2 * FixedPoint.Scale,
                $"tick {tick}: the blocker must stand still for this fixture to mean anything");
            Assert.True(blockerNow.Health > 0, $"tick {tick}: the blocker must not be shot out of the way");
            Assert.True(leaderNow.Health > 0, $"tick {tick}: the leader must survive the whole window");

            if (tick < stallWindowStart)
            {
                continue;
            }

            var proposal = sim.PendingMovementProposals.Single(p => p.EntityId == 1UL);

            // Stage 9 itself is what stops the leader now: it proposes the
            // leader's own current position rather than a stride toward the
            // path. This is the assertion that inverted on 2026-08-11 — it
            // used to require a full-magnitude proposal, because the stall it
            // pinned lived in stage 10 rejecting live proposals.
            var desiredDeltaX = (long)proposal.DesiredXRaw - proposal.StartXRaw;
            var desiredDeltaY = (long)proposal.DesiredYRaw - proposal.StartYRaw;
            var desiredMagnitudeSq = (desiredDeltaX * desiredDeltaX) + (desiredDeltaY * desiredDeltaY);
            Assert.True(
                desiredMagnitudeSq == 0,
                $"tick {tick}: stage 9 proposed a step of {desiredMagnitudeSq} raw squared toward a " +
                "hostile it should have halted to engage");
            Assert.Equal(1UL, proposal.GroupId);
            Assert.Equal(0, proposal.SlotIndex);

            // The whole point of the halt: the leader keeps its distance
            // rather than closing to touching range. Asserted as a real gap in
            // world units, not as "the positions differ" — two bodies resting
            // against each other also differ.
            var gapXWu =
                (blockerNow.PositionX.RawValue - leaderNow.PositionX.RawValue) / FixedPoint.Scale;
            Assert.True(
                gapXWu >= 80,
                $"tick {tick}: the leader closed to {gapXWu} wu of the hostile. It should have " +
                "stopped at about ContactMemory.IdentifyRangeWu (96), which is where Engage " +
                "becomes available — closing to touching distance is what this halt prevents");

            if (tick == stallWindowStart)
            {
                stalledXRaw = leaderNow.PositionX.RawValue;
                stalledYRaw = leaderNow.PositionY.RawValue;
                continue;
            }

            Assert.True(
                leaderNow.PositionX.RawValue == stalledXRaw && leaderNow.PositionY.RawValue == stalledYRaw,
                $"tick {tick}: the leader moved from ({stalledXRaw}, {stalledYRaw}) to " +
                $"({leaderNow.PositionX.RawValue}, {leaderNow.PositionY.RawValue}) while halted to engage");
        }

        // The stall is short of the goal and past the start, so neither
        // "never left" nor "already arrived" can produce it.
        Assert.True(
            stalledXRaw > 2 * FixedPoint.Scale,
            "the leader must have walked the clear ground before the blocker");
        Assert.True(
            stalledXRaw < (blockerXWu - 8) * FixedPoint.Scale,
            "the leader must be stalled against the blocker's body, not standing on top of it");
    }

    /// <summary>
    /// Task 84: the second, distinct case its brief names - a non-leader
    /// squad slot (entity 2, <see cref="SquadSlot.SlotIndex"/> 1) starting
    /// away from its own formation position must walk into it across
    /// multiple ticks, never teleport there in one. The leader (entity 1,
    /// the lowest id, per this file's own <see
    /// cref="RunTick_GroupLeaderInNarrowCorridor_CollapsesFollowerLateralOffsetToZero"/>
    /// convention) and follower both start on a straight, axis-aligned
    /// published path, 12 world units apart - close enough to share one
    /// cohesion group (<see cref="SandataRuleset.GroupCohesionRadiusWu"/> 96),
    /// far enough apart that their collision bodies (<c>CollisionBodyRadiusRaw</c>
    /// 4,352 raw, 4.25 wu each) do not already overlap and confound this
    /// test's own read of <see cref="SandataSimulation.State"/> with
    /// collision-resolution effects unrelated to task 84's clamp. The
    /// slot's target depends only on the leader's own position, not the
    /// follower's, so this spacing does not change it: trail 8 wu behind,
    /// lateral 4 wu offset (<see cref="SandataSimulation.FormationSlotOffsetsWu"/>)
    /// places the target well outside the one-tick (1,638 raw, about 1.6 wu)
    /// cap regardless of where the follower itself starts.
    /// </summary>
    [Fact]
    public void RunTick_NonLeaderSlotFarFromFormationPosition_DoesNotTeleportIntoPlace()
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var startCell = grid.CellIndex(0, 0);
        var goalCell = grid.CellIndex(25, 0);

        const int pathLatencyTicks = 1;
        var ruleset = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: pathLatencyTicks,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 1024);

        var leader = BuildOperator(entityId: 1, faction: 0, positionXWu: 22, positionYWu: 2);
        var follower = BuildOperator(entityId: 2, faction: 0, positionXWu: 34, positionYWu: 2);
        var groupState = new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: goalCell,
            HasOutstandingRequest: true,
            StartCellIndex: startCell,
            GoalCellIndex: goalCell,
            RequestTick: 0);
        var state = BuildState(ImmutableArray.Create(leader, follower))
            with
        { Groups = ImmutableArray.Create(groupState) };

        var sim = new SandataSimulation(mission, ruleset, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);

        var movementSpeedRaw = (80L * FixedPoint.Scale) / ruleset.TickRate;

        sim.RunTick(0); // request tick: path not yet published, both hold.

        var beforeFollower = sim.State.Operators.Single(o => o.EntityId == 2UL);
        Assert.Equal(34 * FixedPoint.Scale, beforeFollower.PositionX.RawValue);
        Assert.Equal(2 * FixedPoint.Scale, beforeFollower.PositionY.RawValue);

        sim.RunTick(pathLatencyTicks); // publish tick: slot target is now real.

        var afterFirstMove = sim.State.Operators.Single(o => o.EntityId == 2UL);
        var deltaX = afterFirstMove.PositionX.RawValue - beforeFollower.PositionX.RawValue;
        var deltaY = afterFirstMove.PositionY.RawValue - beforeFollower.PositionY.RawValue;

        Assert.True(
            (deltaX * deltaX) + (deltaY * deltaY) <= movementSpeedRaw * movementSpeedRaw,
            $"first published tick displacement ({deltaX}, {deltaY}) raw exceeds the per-tick cap {movementSpeedRaw} raw");
        Assert.True(deltaX != 0 || deltaY != 0, "follower must actually start moving toward its formation slot");

        // The proposal itself (before any collision resolution downstream
        // of stage 9 - out of task 84's grant, and irrelevant to whether
        // the clamp did its job) already shows the target is still far off:
        // the trail (8 wu behind the leader's own arclength) and lateral
        // (4 wu) offsets place the slot's true target tens of world units
        // from the follower's spawn point, so one clamped ~1.6 wu step
        // could not have reached it. A second tick's proposal must show the
        // same bounded, nonzero step, proving the clamp - not a one-tick
        // snap - governs every tick this walk takes, not only the first.
        sim.RunTick(pathLatencyTicks + 1);
        var followerProposal2 = sim.PendingMovementProposals.Single(p => p.EntityId == 2UL);
        var deltaX2 = followerProposal2.DesiredXRaw - followerProposal2.StartXRaw;
        var deltaY2 = followerProposal2.DesiredYRaw - followerProposal2.StartYRaw;

        Assert.True(
            ((long)deltaX2 * deltaX2) + ((long)deltaY2 * deltaY2) <= movementSpeedRaw * movementSpeedRaw,
            $"second tick's proposed displacement ({deltaX2}, {deltaY2}) raw exceeds the per-tick cap {movementSpeedRaw} raw");
        Assert.True(deltaX2 != 0 || deltaY2 != 0, "follower must still be closing on its formation slot on the second tick");
    }

    /// <summary>
    /// Task 87: a point sampled from an arclength projects back to that same
    /// arclength, over a polyline carrying an axis-aligned, an exact
    /// 45-degree, and an oblique segment. This is the property that decides
    /// whether a leader aiming a short distance ahead of its own projection
    /// actually gets there, and before task 87 it lost up to about two world
    /// units on a diagonal.
    /// </summary>
    /// <remarks>
    /// <b>What this test does not bind, established by breaking it.</b> The
    /// round trip is insensitive to the stored segment length being wrong,
    /// because <see cref="PolylineArclength.SampleAt"/> and
    /// <c>ProjectArclength</c> divide by the same length in opposite
    /// directions and the error cancels. Reintroducing task 87's world-unit
    /// truncation leaves this test passing. What it does bind is the
    /// coordinate precision: sampling in raw rather than in whole world units,
    /// and projecting a raw query position rather than one already rounded to
    /// a world unit. The segment length is pinned separately by
    /// <c>SlotTargetsTests.Build_DiagonalSegmentLength_IsTheRawRootNotTheScaledWorldUnitRoot</c>.
    /// </remarks>
    [Fact]
    public void ProjectArclength_RoundTripsEverySampledPointToWithinTwoRawUnits()
    {
        // Two raw units, not one, and the difference is arithmetic rather
        // than slack. Three truncating divisions sit on this round trip: the
        // segment length is a truncated integer square root, SampleAt then
        // truncates the interpolated coordinate, and ProjectArclength
        // truncates the projection back onto the segment. Each can lose up to
        // a raw unit and they do not cancel. Task 87's row asked for "within
        // one raw unit"; that figure was written without checking it against
        // the three roundings it has to survive, and one is not reachable
        // without rounding-to-nearest at every step. Two raw units is
        // 2/1024 of a world unit, against a per-tick step of 1,638 raw — the
        // property that matters is that the loss is now a rounding error
        // rather than a stride, and it is measured here rather than assumed.
        const long MaxRoundTripDriftRaw = 2;

        // ProjectArclength is private, and it is read here by reflection for
        // the same reason the invariant test below reads its constants that
        // way: re-deriving the projection locally would test a copy of the
        // arithmetic rather than the arithmetic stage 9 actually runs.
        var projectArclength = typeof(SandataSimulation).GetMethod(
            "ProjectArclength", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "SandataSimulation.ProjectArclength not found by reflection.");

        // One axis-aligned segment, one exact 45-degree diagonal, and one
        // oblique segment at roughly 18.4 degrees — the angle of the angle
        // -house fixture's own wall, and the case task 87 was written for.
        ImmutableArray<PathPoint> polyline =
        [
            new PathPoint(0, 0),
            new PathPoint(100, 0),
            new PathPoint(140, 40),
            new PathPoint(200, 60),
        ];

        var arclength = PolylineArclength.Build(polyline);

        long RoundTrip(long queryArclength)
        {
            var sample = arclength.SampleAt(queryArclength);
            return (long)projectArclength.Invoke(
                null, [polyline, arclength, sample.X, sample.Y])!;
        }

        var probed = 0;
        var worstDrift = 0L;
        var worstDescription = "none";

        void Probe(string description, long query)
        {
            var drift = Math.Abs(RoundTrip(query) - query);
            probed++;

            if (drift > worstDrift)
            {
                worstDrift = drift;
                worstDescription = $"{description} at arclength {query}";
            }
        }

        for (var vertexIndex = 0; vertexIndex < polyline.Length; vertexIndex++)
        {
            Probe($"vertex {vertexIndex}", arclength.ArclengthAtVertex(vertexIndex));
        }

        for (var segment = 0; segment + 1 < polyline.Length; segment++)
        {
            var segmentStart = arclength.ArclengthAtVertex(segment);
            var segmentLength = arclength.ArclengthAtVertex(segment + 1) - segmentStart;

            for (var step = 1; step < 32; step++)
            {
                Probe($"segment {segment} step {step}", segmentStart + (segmentLength * step / 32));
            }
        }

        // Both loops must actually have run. Without this the whole test
        // passes on an empty polyline, which is the shape of vacuous pass this
        // wave has thrown away three measurements over.
        Assert.Equal(97, probed);

        Assert.True(
            worstDrift <= MaxRoundTripDriftRaw,
            $"worst round-trip drift was {worstDrift} raw units at {worstDescription}, " +
            $"above the {MaxRoundTripDriftRaw} this arithmetic is allowed");
    }

    /// <summary>
    /// Task 84: pins the two constants the clamp's own safety property
    /// depends on - per-tick movement must never exceed the collision body
    /// radius, or two operators closing on each other could pass through
    /// in a single tick. Both constants are read out of the production type
    /// by reflection rather than re-derived locally, so the invariant is
    /// asserted against what the simulation actually uses.
    /// </summary>
    [Fact]
    public void MovementSpeedRaw_NeverExceedsTheCollisionBodyRadius()
    {
        // Both constants are read out of the production type by reflection,
        // the way this file's task 86 constants test already reads them.
        // Re-deriving either one locally would leave an invariant test that
        // passes no matter what the simulation actually uses, which is the
        // opposite of what this test is for.
        var sprintSpeedField = typeof(SandataSimulation).GetField(
            "SprintSpeedWuPerSecond", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "SandataSimulation.SprintSpeedWuPerSecond not found by reflection.");
        var radiusField = typeof(SandataSimulation).GetField(
            "CollisionBodyRadiusRaw", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "SandataSimulation.CollisionBodyRadiusRaw not found by reflection.");

        var sprintSpeedWuPerSecond = (int)sprintSpeedField.GetValue(null)!;
        var collisionBodyRadiusRaw = (long)(int)radiusField.GetValue(null)!;
        var movementSpeedRaw =
            ((long)sprintSpeedWuPerSecond * FixedPoint.Scale) / SandataRuleset.ModernTacticalV1.TickRate;

        Assert.Equal(80, sprintSpeedWuPerSecond);
        Assert.Equal(1_638L, movementSpeedRaw);
        Assert.Equal(4_352L, collisionBodyRadiusRaw);
        Assert.True(
            movementSpeedRaw <= collisionBodyRadiusRaw,
            "per-tick movement must never exceed the collision body radius");
    }

    /// <summary>
    /// Task 79b: a group whose leader's actual position sits in a real
    /// one-cell-wide corridor — <see cref="NavGrid.Passability"/> is baked
    /// with the corridor's flanking rows blocked, so the clearance field
    /// this fixture's <see cref="SandataSimulation"/> instance bakes in its
    /// own constructor genuinely drops below the production
    /// <c>FormationHalfWidthWu</c> (6 world units: a leader clearance of 10
    /// chamfer units converts to 4 world units, strictly under 6) at the
    /// leader's own cell — not a fixture that reaches the collapsed
    /// assertion without an actual clearance drop. The follower's lateral
    /// offset (nonzero, design section 8's arclength formula, when
    /// expanded) must therefore land on zero.
    /// </summary>
    [Fact]
    public void RunTick_GroupLeaderInNarrowCorridor_CollapsesFollowerLateralOffsetToZero()
    {
        var grid = BuildGrid();

        // A one-cell-wide corridor along row y = 5, columns x = 0 through 9:
        // both flanking rows blocked across the same span, so every corridor
        // cell in that span sits exactly one orthogonal chamfer step (10)
        // from the nearest blocked cell — the pinned collapsing value
        // FormationCollapseTests already exercises for the same
        // FormationHalfWidthWu-scale threshold.
        for (var x = 0; x < 10; x++)
        {
            grid.Passability[grid.CellIndex(x, 4)] = NavCellFlags.Blocked;
            grid.Passability[grid.CellIndex(x, 6)] = NavCellFlags.Blocked;
        }

        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();

        var startCell = grid.CellIndex(0, 5);
        var goalCell = grid.CellIndex(9, 5);

        const int pathLatencyTicks = 1;
        var ruleset = new SandataRuleset(
            tickRate: 50,
            msToTickConversionRuleId: MsToTickConversionRule.HalfAwayFromZero,
            pathLatencyTicks: pathLatencyTicks,
            groupCohesionRadiusWu: 96,
            loweredWallDistanceWu: 24,
            aimToleranceBam: 1024);

        // Leader (entity 1, lowest id) sits at the corridor cell's own
        // centre, (10, 22) world units — cell (2, 5) — well inside the
        // blocked-flank span above. The follower (entity 2) stands right
        // beside it, inside the same cohesion radius, so both derive into
        // one group with entity 1 as leader and slot 0, entity 2 as slot 1.
        var leader = BuildOperator(entityId: 1, faction: 0, positionXWu: 10, positionYWu: 22);
        var follower = BuildOperator(entityId: 2, faction: 0, positionXWu: 12, positionYWu: 22);

        var groupState = new GroupPathState(
            GroupId: 1UL,
            DestinationCellIndex: goalCell,
            HasOutstandingRequest: true,
            StartCellIndex: startCell,
            GoalCellIndex: goalCell,
            RequestTick: 0);
        var state = BuildState(ImmutableArray.Create(leader, follower)) with
        {
            Groups = ImmutableArray.Create(groupState),
        };

        var sim = new SandataSimulation(mission, ruleset, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);

        sim.RunTick(0);
        sim.RunTick(pathLatencyTicks);

        var followerProposal = Assert.Single(
            sim.PendingMovementProposals.Where(p => p.EntityId == 2));

        // The published path is a straight horizontal segment at Y = 22
        // world units (cell row 5's own centre) end to end, so its local
        // direction is purely along X; any nonzero lateral offset would
        // move the follower's target off that exact Y. Landing back on it
        // is what "every slot's lateral offset is zero" means here.
        const int corridorCentreYRaw = 22 * FixedPoint.Scale;
        Assert.Equal(corridorCentreYRaw, followerProposal.DesiredYRaw);
    }

    /// <summary>
    /// Task 86 finding, superseding task 79d-1's original claim for this
    /// fixture. Before task 86, <c>CollisionBodyRadiusRaw</c> was an
    /// invented 32 raw (0.03 wu); at that size the target's subtended
    /// half-angle at 90 wu was comparable to the AK-47's drawn dispersion,
    /// so shooter id 2 drew a miss and id 25 drew a hit. Task 86 corrected
    /// the radius to the designed 4,352 raw (4.25 wu,
    /// <c>Hukbo.Core/Simulation/CollisionRules.cs:72</c>'s
    /// <c>DefaultBodyRadiusRaw</c>), which grows the half-angle roughly
    /// 136x. Both ids now hit — verified here directly, and proved to hold
    /// for every reachable id by
    /// <see cref="SubtendedHalfAngle_AlwaysAtLeast_AkDispersion_WithinDetectRange"/>
    /// below. See the task-86 report for the full derivation.
    /// <para>
    /// The name says "while stage 12 hardcodes the rifle" for history, not
    /// current behavior: task 79d-2a moved <c>SandataSimulation.ProposeFire</c>'s
    /// <see cref="FirearmDefinition"/> resolution inside the per-shooter loop,
    /// keyed on each shot's own <see cref="OperatorState.Firearm"/>, the same
    /// shape stage 11 already used. Neither fixture below sets
    /// <see cref="OperatorState.Firearm"/>, so both still default to
    /// <see cref="FirearmId.Ak47"/> and this test's outcome is unchanged by
    /// that fix — it is named for the geometry finding it pins, not for the
    /// now-closed loadout gap.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(25)]
    public void RunTick_SameGeometryAnyShooterEntityId_AlwaysHitsWhileStage12HardcodesTheRifle(
        int shooterEntityId)
    {
        var sim = BuildFiringFixture(shooterEntityId);

        sim.RunTick(0);

        var events = sim.State.EventFeed.Events;
        Assert.Single(events, e => e.Kind == MissionEventKind.ShotFired);
        Assert.Single(events, e => e.Kind == MissionEventKind.ShotHit);
        Assert.DoesNotContain(events, e => e.Kind == MissionEventKind.ShotMissed);
    }

    /// <summary>Task 79d-1, done-when criterion 2, restated as an explicit count check.</summary>
    [Fact]
    public void RunTick_Hit_EmitsExactlyOneShotFiredAndOneShotHitEvent()
    {
        var sim = BuildFiringFixture(shooterEntityId: 25);

        sim.RunTick(0);

        var events = sim.State.EventFeed.Events;
        Assert.Equal(2, events.Length);
        Assert.Equal(1, events.Count(e => e.Kind == MissionEventKind.ShotFired));
        Assert.Equal(1, events.Count(e => e.Kind == MissionEventKind.ShotHit));
        Assert.Equal(0, events.Count(e => e.Kind == MissionEventKind.ShotMissed));
    }

    /// <summary>
    /// Task 86 finding, still true after task 79d-2a: at the designed
    /// <c>CollisionBodyRadiusRaw</c> (4,352 raw), a miss is mathematically
    /// unreachable for an <see cref="FirearmId.Ak47"/> loadout through
    /// <see cref="SandataSimulation.RunTick"/>, because
    /// <see cref="AccuracyRules.DrawAngularErrorBam"/>'s drawn magnitude
    /// never exceeds the private <c>SubtendedHalfAngleBam(rangeWu)</c>
    /// (reflected below — same reflection convention this file already uses
    /// for other private members) for any whole range the sensing pipeline
    /// can reach. <see cref="ContactMemory.DetectRangeWu"/> (256) is the
    /// outer bound: <c>AdvanceWeaponChain</c> only proposes a shot once a
    /// contact clears <see cref="ContactTier.Unknown"/>, which requires a
    /// range within <c>DetectRangeWu</c>. Solving dispersion(R) =
    /// half-angle(R) continuously puts the crossover at roughly 345 wu —
    /// past <c>DetectRangeWu</c> — so no in-range geometry can ever draw a
    /// miss for this weapon. This pins the impossibility as a regression
    /// check: if a future change to <c>CollisionBodyRadiusRaw</c>, the
    /// AK-47's dispersion constants, or <c>DetectRangeWu</c> ever reopens a
    /// reachable rifle miss, this test fails.
    /// <para>
    /// Task 86 found two separate things blocking a reachable miss, only one
    /// of them geometric. The first is the crossover above, and it still
    /// holds. The second was that <c>ProposeFire</c> resolved every shot's
    /// <see cref="FirearmDefinition"/> from a hardcoded rifle default rather
    /// than the shooter's own <see cref="OperatorState.Firearm"/>, so a
    /// pistol loadout changed nothing in stage 12 — <see cref="FirearmId.Beretta92Fs"/>'s
    /// far wider dispersion curve (crossover roughly 157 wu, comfortably
    /// inside <see cref="ContactMemory.DetectRangeWu"/>) was unreachable from
    /// any <see cref="SandataSimulation.RunTick"/> call. Task 79d-2a closed
    /// that second gap: <c>ProposeFire</c> now resolves
    /// <see cref="FirearmDefinition"/> per shot from
    /// <see cref="OperatorState.Firearm"/>, the same shape stage 11 already
    /// used, so a pistol shooter's dispersion is real in stage 12. That makes
    /// the pistol-miss path reachable, and
    /// <see cref="RunTick_PistolMissesAndRifleHitsAtTheSameTwoHundredWorldUnitRange"/>
    /// and <see cref="RunTick_Miss_EmitsExactlyOneShotFiredAndOneShotMissedEvent"/>
    /// below restore that <c>RunTick</c>-level miss coverage — the exact
    /// obligation this doc comment used to describe as still open.
    /// <c>EmitShotMissedEvent</c> is no longer a production path with zero
    /// test coverage.
    /// </para>
    /// </summary>
    [Fact]
    public void SubtendedHalfAngle_AlwaysAtLeast_AkDispersion_WithinDetectRange()
    {
        var method = typeof(SandataSimulation).GetMethod(
            "SubtendedHalfAngleBam", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "SandataSimulation.SubtendedHalfAngleBam not found by reflection; " +
                "task 86's regression check for the miss-impossibility finding cannot run.");

        var definition = FirearmCatalog.Rows[(int)FirearmId.Ak47];

        for (var rangeWu = 1; rangeWu <= ContactMemory.DetectRangeWu; rangeWu++)
        {
            var maxDrawMagnitudeBam = AccuracyRules.Dispersion(
                rangeWu, definition.DispersionAtZeroWu, definition.DispersionAtMaxWu, definition.MaxEffectiveWu);
            var halfAngleBam = (int)method.Invoke(null, new object[] { rangeWu })!;

            Assert.True(
                maxDrawMagnitudeBam <= halfAngleBam,
                $"range {rangeWu} wu: max drawn magnitude {maxDrawMagnitudeBam} exceeds half-angle " +
                $"{halfAngleBam} bam — a miss is reachable within DetectRangeWu, so the task-86 finding " +
                "no longer holds and RunTick-level miss coverage must be restored.");
        }
    }

    /// <summary>
    /// Task 86 regression pin: <c>CollisionBodyRadiusRaw</c> must stay the
    /// designed value —
    /// <c>Hukbo.Core/Simulation/CollisionRules.cs:72</c>'s
    /// <c>DefaultBodyRadiusRaw</c>
    /// (4,352 raw = 4.25 wu), restated in <see cref="SandataSimulation"/>
    /// per design section 4 of
    /// docs/plans/2026-08-07-sandata-scaffold-design.md, not the invented
    /// 32 raw task 86 replaced. <c>CollisionCellSizeRaw</c> must stay
    /// exactly twice that (8,704 raw), the tightest cell
    /// <see cref="SandataCollisionGrid"/>'s own three-by-three neighbour
    /// scan tolerates. Reflection is required — both are <c>private
    /// const</c>, same convention this file already uses for other private
    /// members.
    /// </summary>
    [Fact]
    public void CollisionBodyRadiusAndCellSize_MatchTheDesignedValues()
    {
        var radiusField = typeof(SandataSimulation).GetField(
            "CollisionBodyRadiusRaw", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "SandataSimulation.CollisionBodyRadiusRaw not found by reflection.");
        var cellSizeField = typeof(SandataSimulation).GetField(
            "CollisionCellSizeRaw", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "SandataSimulation.CollisionCellSizeRaw not found by reflection.");

        var bodyRadiusRaw = (int)radiusField.GetValue(null)!;
        var cellSizeRaw = (int)cellSizeField.GetValue(null)!;

        Assert.Equal(4352, bodyRadiusRaw);
        Assert.Equal(2 * bodyRadiusRaw, cellSizeRaw);
        Assert.Equal(8704, cellSizeRaw);
    }

    /// <summary>
    /// Task 86's own required proof: the re-spaced seed-1 headless fixture's
    /// operators must clear one designed body diameter (8,704 raw = 2 *
    /// <c>CollisionBodyRadiusRaw</c>) at minimum pairwise separation, so
    /// operators no longer start overlapping now that the collision body
    /// radius is the designed size rather than the old invented one. Calls
    /// the real <see cref="HeadlessRunner.BuildOpenGrid"/> and
    /// <see cref="HeadlessRunner.BuildInitialState"/> directly — both
    /// promoted from <see langword="private"/> to <see langword="internal"/>
    /// for exactly this proof, reachable here through
    /// <c>Sandata.Headless.csproj</c>'s existing
    /// <c>InternalsVisibleTo("Sandata.Core.Tests")</c> grant — rather than
    /// reimplementing the placement formula in the test. Uses the same
    /// operator count and seed (200, 1) the canonical gate's headless
    /// determinism workload and this repo's default benchmark both run.
    /// </summary>
    [Fact]
    public void HeadlessFixture_MinimumPairwiseSeparation_ClearsOneBodyDiameter()
    {
        const int operatorCount = 200;
        const ulong seed = 1UL;
        const int bodyDiameterRaw = 8704; // 2 * CollisionBodyRadiusRaw (4,352 raw), task 86

        var (_, _, packingSide) = HeadlessRunner.BuildOpenGrid(operatorCount);
        var state = HeadlessRunner.BuildInitialState(operatorCount, seed, packingSide);
        var operators = state.Operators;

        var minSquaredRaw = long.MaxValue;
        for (var i = 0; i < operators.Length; i++)
        {
            for (var j = i + 1; j < operators.Length; j++)
            {
                var dx = (long)(operators[i].PositionX.RawValue - operators[j].PositionX.RawValue);
                var dy = (long)(operators[i].PositionY.RawValue - operators[j].PositionY.RawValue);
                var squaredRaw = (dx * dx) + (dy * dy);
                if (squaredRaw < minSquaredRaw)
                {
                    minSquaredRaw = squaredRaw;
                }
            }
        }

        Assert.True(
            minSquaredRaw >= (long)bodyDiameterRaw * bodyDiameterRaw,
            $"minimum pairwise separation squared {minSquaredRaw} raw is under the body-diameter-squared " +
            $"threshold {(long)bodyDiameterRaw * bodyDiameterRaw} raw — operators would start overlapping.");

        // Pinned exact measurement at this operator count and seed: the
        // worst-case jitter bound (12 wu pitch minus the jitter's 2 wu
        // worst-case shrink) is actually reached, 10 wu (10,240 raw).
        Assert.Equal(10_240L * 10_240L, minSquaredRaw);
    }

    /// <summary>
    /// Task 79d-1, done-when criterion 3: once a shot is emitted the event
    /// hash must move off <see cref="SandataHash.Begin"/>'s bare FNV-1a
    /// offset basis — the value the feed starts at before any event folds
    /// in (see <see cref="MissionEventFeed.Empty"/>).
    /// </summary>
    [Fact]
    public void RunTick_ShotEmitted_EventHashMovesOffTheFnv1aOffsetBasis()
    {
        var sim = BuildFiringFixture(shooterEntityId: 25);

        sim.RunTick(0);

        Assert.NotEmpty(sim.State.EventFeed.Events);
        Assert.NotEqual(SandataHash.Begin(), sim.State.EventFeed.Hash);
    }

    /// <summary>
    /// Shared fixture for the 79d-1 hit/miss tests: an opposing-faction pair
    /// 90 world units apart on the x axis, with the shooter's weapon chain
    /// seeded directly into <see cref="WeaponChainPhase.Aiming"/> with one
    /// remaining tick so stage 11 fires on tick 0 without simulating the
    /// full ready/turn/aim sequence. Sensing is still real: the 90 wu
    /// separation is within <c>ContactMemory.IdentifyRangeWu</c> (96), so
    /// stage 5 commits a genuine <c>ContactMemory</c> entry stage 11 reads
    /// to populate the real target id consumed by stage 12.
    /// </summary>
    private static SandataSimulation BuildFiringFixture(int shooterEntityId)
    {
        var grid = BuildGrid();
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();
        var ruleset = SandataRuleset.ModernTacticalV1;

        var shooter = BuildOperator(
            entityId: shooterEntityId, faction: 0, positionXWu: 0, positionYWu: 0,
            weaponChainPhase: (int)WeaponChainPhase.Aiming, weaponChainRemainingTicks: 1);
        var target = BuildOperator(entityId: 100_000, faction: 1, positionXWu: 90, positionYWu: 0);
        var state = BuildState(ImmutableArray.Create(shooter, target));

        return new SandataSimulation(
            mission, ruleset, grid, wallBuckets, state, ImmutableArray<CoverRecord>.Empty);
    }

    /// <summary>
    /// Task 79d-2a, deliverable B: the same range and shooter entity id, run
    /// twice with different <see cref="OperatorState.Firearm"/> loadouts,
    /// prove <c>ProposeFire</c> now reads the per-shot loadout rather than a
    /// hardcoded rifle default — a <see cref="FirearmId.Beretta92Fs"/>
    /// shooter misses where an otherwise-identical
    /// <see cref="FirearmId.Ak47"/> shooter hits, reached only through
    /// <see cref="SandataSimulation.RunTick"/>, never by calling
    /// <c>ProposeFire</c> directly. 200 wu and shooter entity id 25 are a
    /// concrete deterministic pair found by probing
    /// <c>AccuracyRules.DrawAngularErrorBam</c> against both weapons'
    /// dispersion curves at that range: the pistol's wider curve draws past
    /// its target's subtended half-angle there while the rifle's narrower
    /// curve does not. Both outcomes are asserted on the observable event
    /// kinds and on the target's <see cref="OperatorState.Health"/>, not on
    /// "the two runs differ" — see
    /// <see cref="RunTick_Miss_EmitsExactlyOneShotFiredAndOneShotMissedEvent"/>
    /// for the exact-count restatement of the miss half alone.
    /// </summary>
    [Fact]
    public void RunTick_PistolMissesAndRifleHitsAtTheSameTwoHundredWorldUnitRange()
    {
        var pistolSim = BuildRangedFiringFixture(FirearmId.Beretta92Fs, rangeWu: 200, shooterEntityId: 25);
        var rifleSim = BuildRangedFiringFixture(FirearmId.Ak47, rangeWu: 200, shooterEntityId: 25);

        pistolSim.RunTick(0);
        rifleSim.RunTick(0);

        var pistolEvents = pistolSim.State.EventFeed.Events;
        Assert.Equal(1, pistolEvents.Count(e => e.Kind == MissionEventKind.ShotFired));
        Assert.Equal(0, pistolEvents.Count(e => e.Kind == MissionEventKind.ShotHit));
        Assert.Equal(1, pistolEvents.Count(e => e.Kind == MissionEventKind.ShotMissed));
        var pistolTarget = pistolSim.State.Operators.Single(o => o.EntityId == RangedFixtureTargetEntityId);
        Assert.Equal(100, pistolTarget.Health);

        var rifleEvents = rifleSim.State.EventFeed.Events;
        Assert.Equal(1, rifleEvents.Count(e => e.Kind == MissionEventKind.ShotFired));
        Assert.Equal(1, rifleEvents.Count(e => e.Kind == MissionEventKind.ShotHit));
        Assert.Equal(0, rifleEvents.Count(e => e.Kind == MissionEventKind.ShotMissed));
        var rifleTarget = rifleSim.State.Operators.Single(o => o.EntityId == RangedFixtureTargetEntityId);
        Assert.True(rifleTarget.Health < 100, $"rifle hit should reduce target health below 100, was {rifleTarget.Health}");
    }

    /// <summary>
    /// Task 79d-1, done-when criterion 2's miss half, restored per task
    /// 79d-2a's obligation (see
    /// <see cref="SubtendedHalfAngle_AlwaysAtLeast_AkDispersion_WithinDetectRange"/>'s
    /// remarks): the same exact-count shape as
    /// <see cref="RunTick_Hit_EmitsExactlyOneShotFiredAndOneShotHitEvent"/>,
    /// using the pistol half of
    /// <see cref="RunTick_PistolMissesAndRifleHitsAtTheSameTwoHundredWorldUnitRange"/>'s
    /// fixture.
    /// </summary>
    [Fact]
    public void RunTick_Miss_EmitsExactlyOneShotFiredAndOneShotMissedEvent()
    {
        var sim = BuildRangedFiringFixture(FirearmId.Beretta92Fs, rangeWu: 200, shooterEntityId: 25);

        sim.RunTick(0);

        var events = sim.State.EventFeed.Events;
        Assert.Equal(2, events.Length);
        Assert.Equal(1, events.Count(e => e.Kind == MissionEventKind.ShotFired));
        Assert.Equal(0, events.Count(e => e.Kind == MissionEventKind.ShotHit));
        Assert.Equal(1, events.Count(e => e.Kind == MissionEventKind.ShotMissed));
    }

    private const int RangedFixtureTargetEntityId = 200_000;

    /// <summary>
    /// Shared fixture for the task 79d-2a pistol-miss/rifle-hit pair: the
    /// shooter sits <paramref name="rangeWu"/> world units <b>east</b> of the
    /// target, at (<paramref name="rangeWu"/>, 0), so the target is directly
    /// opposite the shooter's default <see cref="Facing16.East"/> facing.
    /// Both positions stay non-negative and inside a grid widened to fit
    /// <paramref name="rangeWu"/> — <see cref="NavGrid.CellSizeWu"/> is 4
    /// world units per cell, and <see cref="BuildGrid"/>'s default 32-cell
    /// width only covers 128 wu, short of the 200 wu this fixture needs. The
    /// shooter's <see cref="OperatorState.ContactMemory"/> is pre-seeded with
    /// a stale <see cref="ContactTier.Identified"/> entry for the target from
    /// tick 0.
    /// <para>
    /// This is deliberate, not an oversight: <see cref="IntentSelection.Select"/>
    /// only selects <see cref="OperatorIntent.Engage"/> when
    /// <c>BestContactTier == ContactTier.Identified</c>, which real sensing
    /// only ever grants within <see cref="ContactMemory.IdentifyRangeWu"/>
    /// (96 wu) — too close for the 200 wu range this fixture needs. Placing
    /// the target behind the shooter's vision cone
    /// (<c>VisionConeHalfWidthBam</c>, 90° half-width) means stage 5's
    /// <see cref="ContactMemory.Update"/> never observes it from the
    /// shooter's side this tick, so the pre-seeded entry survives unchanged
    /// as a ghost (that method's documented "carried forward unchanged"
    /// rule) and stage 9 still selects <see cref="OperatorIntent.Engage"/>
    /// from it. The target's own sensing of the shooter is real and mutual —
    /// the shooter sits inside the target's East-facing cone — the same
    /// harmless mutual-visibility shape <see cref="BuildFiringFixture"/>
    /// already relies on at 90 wu; the target's own weapon chain starts
    /// <see cref="WeaponChainPhase.Lowered"/>, so it never fires back within
    /// one tick. Stage 12's actual shot geometry is unaffected by the ghost:
    /// <c>ProposeFire</c> always reads the target's real, live committed
    /// position from <see cref="MissionState.Operators"/>, never the contact
    /// memory, so the resolved range is the true <paramref name="rangeWu"/>
    /// this fixture places the target at. The shooter's weapon chain is
    /// seeded directly into <see cref="WeaponChainPhase.Aiming"/> with one
    /// remaining tick, the same shortcut <see cref="BuildFiringFixture"/>
    /// uses, so stage 11 fires on tick 0 without simulating the full
    /// ready/turn/aim sequence.
    /// </para>
    /// </summary>
    private static SandataSimulation BuildRangedFiringFixture(
        FirearmId firearm,
        int rangeWu,
        int shooterEntityId,
        ImmutableArray<CoverRecord> coverRecords = default,
        bool targetIsCrouched = false)
    {
        var gridWidthCells = (rangeWu / NavGrid.CellSizeWu) + 8;
        var grid = BuildGrid(width: gridWidthCells, height: 8);
        var wallBuckets = NoWalls(grid);
        var mission = BuildMission();
        var ruleset = SandataRuleset.ModernTacticalV1;

        var shooter = BuildOperator(
            entityId: shooterEntityId, faction: 0, positionXWu: rangeWu, positionYWu: 0,
            weaponChainPhase: (int)WeaponChainPhase.Aiming, weaponChainRemainingTicks: 1) with
        {
            Firearm = firearm,
            ContactMemory = ImmutableArray.Create(new ContactMemoryEntry(
                EnemyEntityId: (ulong)RangedFixtureTargetEntityId,
                LastKnownCellIndex: 0,
                ContactTier: (int)ContactTier.Identified,
                LastSeenTick: 0)),
        };
        var target = BuildOperator(
            entityId: RangedFixtureTargetEntityId, faction: 1, positionXWu: 0, positionYWu: 0) with
        {
            IsCrouched = targetIsCrouched,
        };
        var state = BuildState(ImmutableArray.Create(shooter, target));

        return new SandataSimulation(
            mission, ruleset, grid, wallBuckets, state,
            coverRecords.IsDefault ? ImmutableArray<CoverRecord>.Empty : coverRecords);
    }

    /// <summary>
    /// Task 81's reuse-proof: <see cref="SandataSimulation"/> now holds one
    /// <see cref="SandataCollisionGrid"/> instance for its whole lifetime
    /// instead of constructing a fresh one every tick (the change that cut
    /// stage 3's measured per-tick allocation from roughly 382,000 bytes to
    /// roughly 21,000 bytes). That reuse is only safe if a later
    /// <see cref="SandataCollisionGrid.Rebuild"/> call fully discards the
    /// previous tick's <see cref="SandataCollisionGrid.Pairs"/> rather than
    /// leaking stale entries into an observably reused buffer. This asserts
    /// the discard on content, not on "it reused something": a first
    /// <see cref="SandataCollisionGrid.Rebuild"/> call produces one
    /// contact pair between two co-located bodies, and a second call on the
    /// very same instance — with entirely different entity ids placed far
    /// apart — must report zero pairs and, specifically, must not still
    /// report the first call's pair.
    /// </summary>
    [Fact]
    public void SandataCollisionGrid_Rebuild_DiscardsThePreviousCallsPairs()
    {
        const int cellSizeRaw = 200;
        const int bodyRadiusRaw = 50; // diameter 100 <= cellSizeRaw, satisfies ValidateBodyRadius.
        var grid = new SandataCollisionGrid(cellSizeRaw);

        var firstTickBodies = new[]
        {
            new SandataCollisionBody(EntityId: 1, XRaw: 0, YRaw: 0, IsAlive: true),
            new SandataCollisionBody(EntityId: 2, XRaw: 10, YRaw: 0, IsAlive: true), // within sum-of-radii contact.
        };
        grid.Rebuild(firstTickBodies, bodyRadiusRaw);

        Assert.Equal(
            new SandataCollisionPair(LowEntityId: 1, HighEntityId: 2),
            Assert.Single(grid.Pairs));

        var secondTickBodies = new[]
        {
            new SandataCollisionBody(EntityId: 3, XRaw: 5_000, YRaw: 5_000, IsAlive: true),
            new SandataCollisionBody(EntityId: 4, XRaw: -5_000, YRaw: -5_000, IsAlive: true),
        };
        grid.Rebuild(secondTickBodies, bodyRadiusRaw);

        Assert.Empty(grid.Pairs);
        Assert.DoesNotContain(
            grid.Pairs, pair => pair.LowEntityId == 1 || pair.HighEntityId == 2);
    }

    /// <summary>
    /// Task 79d-2b: the damage a hit deals is keyed on the shooter's own
    /// <see cref="FirearmDefinition.Caliber"/>, so two shooters whose
    /// firearms belong to different caliber families deal different damage
    /// on an otherwise identical hit. Everything else about the two fixtures
    /// is the same — the same range, the same geometry, the same shooter
    /// entity id and therefore the same <c>Accuracy</c> draw, the same
    /// target — so the loadout is the only variable, which is what makes
    /// this a test of the caliber table rather than of the geometry. Before
    /// this task every hit dealt one flat constant regardless of loadout,
    /// and this test could not have distinguished the two.
    /// </summary>
    /// <remarks>
    /// The two expected health values are computed from
    /// <see cref="CaliberDamage.RawDamage"/> itself rather than written as
    /// literals, so the test follows the table if a future tuning pass moves
    /// it, and still fails if stage 12 stops reading the table at all. What
    /// is pinned as a literal is the relation the table's own remarks
    /// promise: 7.62x39 does strictly more damage than 5.56x45. Both
    /// shooters are rifles at 100 world units, inside
    /// <see cref="ContactMemory.DetectRangeWu"/>, where
    /// <see cref="SubtendedHalfAngle_AlwaysAtLeast_AkDispersion_WithinDetectRange"/>
    /// establishes that a rifle cannot miss, so both shots land and the only
    /// difference reaching the target's health is the caliber.
    /// </remarks>
    [Fact]
    public void RunTick_TwoShootersOfDifferentCaliberFamilies_DealDifferentDamageOnAnIdenticalHit()
    {
        var softerCaliberDamage = CaliberDamage.RawDamage(CaliberFamily.Cal556X45);
        var harderCaliberDamage = CaliberDamage.RawDamage(CaliberFamily.Cal762X39);

        Assert.True(
            harderCaliberDamage > softerCaliberDamage,
            "the caliber table's own remarks promise 7.62x39 above 5.56x45");

        var harderSim = BuildRangedFiringFixture(FirearmId.Ak47, rangeWu: 100, shooterEntityId: 25);
        var softerSim = BuildRangedFiringFixture(FirearmId.M4, rangeWu: 100, shooterEntityId: 25);

        var fullHealth = harderSim.State.Operators
            .Single(o => o.EntityId == RangedFixtureTargetEntityId).Health;

        harderSim.RunTick(0);
        softerSim.RunTick(0);

        var harderTarget = harderSim.State.Operators.Single(o => o.EntityId == RangedFixtureTargetEntityId);
        var softerTarget = softerSim.State.Operators.Single(o => o.EntityId == RangedFixtureTargetEntityId);

        Assert.Equal(fullHealth - harderCaliberDamage, harderTarget.Health);
        Assert.Equal(fullHealth - softerCaliberDamage, softerTarget.Health);
    }

    /// <summary>
    /// Task 79d-2b: a target standing inside a cover record's protected arc
    /// takes the cover-modified damage, and a target inside the same
    /// rectangle but with the shot arriving from outside the arc takes the
    /// unmodified value — design section 9's flank-and-rear bypass, reached
    /// through <see cref="SandataSimulation.RunTick"/> rather than by calling
    /// <see cref="CoverRules"/> directly, which is the whole point: before
    /// this task the map's `COVER` records never reached the simulation at
    /// all and stage 12 resolved every shot against
    /// <see cref="CoverState.NotInCover"/>.
    /// </summary>
    /// <remarks>
    /// Both fixtures place the same cover rectangle over the target's
    /// position and differ only in the record's arc. The protecting record
    /// uses an <c>ArcHalfBam</c> of 32,768, which
    /// <see cref="Sandata.Core.Maps.CoverRecord"/>'s own documentation
    /// defines as covering "from every direction", so it protects whatever
    /// bearing the shooter occupies without this test having to encode a
    /// bearing convention. The bypassing record uses a half-width of one BAM
    /// centred on 16,384, a quarter turn away from either bearing a shooter
    /// due east of the target can occupy under any convention, so the shot
    /// arrives from outside the arc no matter how the cone measures its
    /// angles. The expected damage values come from
    /// <see cref="CaliberDamage.RawDamage"/> and
    /// <see cref="CoverRules.ApplyPercentageReduction"/>'s stated arithmetic
    /// rather than from a run.
    /// </remarks>
    [Fact]
    public void RunTick_TargetInsideACoverArc_TakesReducedDamageWhileAFlankingShotIgnoresTheCover()
    {
        var rawDamage = CaliberDamage.RawDamage(CaliberFamily.Cal762X39);

        var protectingCover = ImmutableArray.Create(new CoverRecord(
            LineNumber: 1, MinX: 0, MinY: 0, MaxX: 8, MaxY: 8,
            ArcCentreBam: 0, ArcHalfBam: 32768, Height: 1));
        var bypassedCover = ImmutableArray.Create(new CoverRecord(
            LineNumber: 1, MinX: 0, MinY: 0, MaxX: 8, MaxY: 8,
            ArcCentreBam: 16384, ArcHalfBam: 1, Height: 1));

        var coveredSim = BuildRangedFiringFixture(
            FirearmId.Ak47, rangeWu: 100, shooterEntityId: 25, coverRecords: protectingCover);
        var flankedSim = BuildRangedFiringFixture(
            FirearmId.Ak47, rangeWu: 100, shooterEntityId: 25, coverRecords: bypassedCover);

        var fullHealth = coveredSim.State.Operators
            .Single(o => o.EntityId == RangedFixtureTargetEntityId).Health;

        coveredSim.RunTick(0);
        flankedSim.RunTick(0);

        var coveredTarget = coveredSim.State.Operators.Single(o => o.EntityId == RangedFixtureTargetEntityId);
        var flankedTarget = flankedSim.State.Operators.Single(o => o.EntityId == RangedFixtureTargetEntityId);

        // Standing in cover: the raw damage loses
        // CoverRules.StandingCoverReductionPercent, truncating toward zero.
        var expectedCoveredDamage =
            (rawDamage * (100 - CoverRules.StandingCoverReductionPercent)) / 100;

        Assert.Equal(fullHealth - expectedCoveredDamage, coveredTarget.Health);
        Assert.Equal(fullHealth - rawDamage, flankedTarget.Health);
        Assert.True(
            coveredTarget.Health > flankedTarget.Health,
            "cover inside its own arc must leave the target better off than a flanking shot");
    }

    /// <summary>
    /// Task 79d-2b: the posture <see cref="SandataSimulation"/> passes into
    /// the target's <see cref="CoverState"/> comes from that operator's own
    /// <see cref="OperatorState.IsCrouched"/> flag, so a crouched target in
    /// the same cover takes
    /// <see cref="CoverRules.CrouchedCoverReductionPercent"/> rather than
    /// <see cref="CoverRules.StandingCoverReductionPercent"/>. Without this
    /// the posture half of the cover lookup could be hardcoded to standing
    /// and every other cover assertion in this file would still pass.
    /// </summary>
    [Fact]
    public void RunTick_CrouchedTargetInCover_TakesTheCrouchedReductionRatherThanTheStandingOne()
    {
        var rawDamage = CaliberDamage.RawDamage(CaliberFamily.Cal762X39);

        var cover = ImmutableArray.Create(new CoverRecord(
            LineNumber: 1, MinX: 0, MinY: 0, MaxX: 8, MaxY: 8,
            ArcCentreBam: 0, ArcHalfBam: 32768, Height: 1));

        var crouchedSim = BuildRangedFiringFixture(
            FirearmId.Ak47, rangeWu: 100, shooterEntityId: 25,
            coverRecords: cover, targetIsCrouched: true);

        var fullHealth = crouchedSim.State.Operators
            .Single(o => o.EntityId == RangedFixtureTargetEntityId).Health;

        crouchedSim.RunTick(0);

        var crouchedTarget = crouchedSim.State.Operators.Single(o => o.EntityId == RangedFixtureTargetEntityId);

        var expectedCrouchedDamage =
            (rawDamage * (100 - CoverRules.CrouchedCoverReductionPercent)) / 100;
        var expectedStandingDamage =
            (rawDamage * (100 - CoverRules.StandingCoverReductionPercent)) / 100;

        Assert.Equal(fullHealth - expectedCrouchedDamage, crouchedTarget.Health);
        Assert.True(
            expectedCrouchedDamage < expectedStandingDamage,
            "the crouched reduction must be the stronger of the two for this test to mean anything");
    }

    // ---- MissionState.Tick advances (2026-08-11) ------------------------

    // Until 2026-08-11 nothing in Sandata.Core ever wrote MissionState.Tick.
    // It stayed 0 for the whole of every run, so SandataStateHasher folded a
    // constant, every emitted event carried tick 0 regardless of when it
    // fired, and HeadlessRunner's per-tick divergence check compared 0
    // against 0. The four tests below are the ones that would have caught
    // that, and each of them fails against the pre-fix code.
    //
    // What this group does NOT bind: it says nothing about the *value* of any
    // state hash. That is GoldenReplayTests' job through
    // Fixtures/seed-1-baseline.json, and those eighty recorded hashes were
    // re-measured in the same change that added these tests. A test here that
    // pinned a hash literal would also violate the one-literal rule
    // CLAUDE.md section 5 states for this assembly.

    /// <summary>
    /// After <see cref="SandataSimulation.RunTick"/> returns,
    /// <see cref="MissionState.Tick"/> is the tick just executed. Asserts the
    /// value at each of four consecutive ticks rather than "it changed", and
    /// deliberately does not start at 0 for the first call — a fixture built
    /// at tick 0 that then runs tick 0 cannot tell a real assignment from the
    /// initial value it already had.
    /// </summary>
    [Fact]
    public void RunTick_LeavesMissionStateTickAtTheTickJustExecuted()
    {
        var sim = BuildFiringFixture(shooterEntityId: 1);

        Assert.Equal(0L, sim.State.Tick);

        foreach (var tick in new long[] { 3, 4, 5, 6 })
        {
            sim.RunTick(tick);
            Assert.Equal(tick, sim.State.Tick);
        }
    }

    /// <summary>
    /// The tick a run is on survives a gap in the caller's tick numbers.
    /// <see cref="SandataSimulation.RunTick"/> takes the tick as a parameter
    /// and does not increment a counter of its own, so a caller that jumps is
    /// followed rather than corrected. This is the assertion that would fail
    /// if someone later replaced the assignment with <c>Tick + 1</c>.
    /// </summary>
    [Fact]
    public void RunTick_FollowsTheCallersTickNumberRatherThanCountingItsOwn()
    {
        var sim = BuildFiringFixture(shooterEntityId: 1);

        sim.RunTick(41);
        Assert.Equal(41L, sim.State.Tick);

        sim.RunTick(9_000);
        Assert.Equal(9_000L, sim.State.Tick);
    }

    /// <summary>
    /// An event emitted from inside <see cref="SandataSimulation.RunTick"/>
    /// stamps itself with the tick it fired on, not with 0. This is the
    /// consequence that mattered: all four
    /// <see cref="MissionEvent"/> constructions in
    /// <c>SandataSimulation</c> read <c>State.Tick</c> rather than taking the
    /// tick as a parameter, so a never-written field silently backdated every
    /// one of them. 61 is an arbitrary non-zero tick; the fixture's weapon
    /// chain has one tick remaining, so it fires on whichever tick is run
    /// first.
    /// </summary>
    [Fact]
    public void RunTick_ShotFiredEventCarriesTheTickItFiredOn()
    {
        var sim = BuildFiringFixture(shooterEntityId: 1);

        sim.RunTick(61);

        var fired = sim.State.EventFeed.Events
            .Where(e => e.Kind == MissionEventKind.ShotFired)
            .ToArray();

        Assert.Single(fired);
        Assert.Equal(61L, fired[0].Tick);
    }

    /// <summary>
    /// The same property on the submission path.
    /// <see cref="SandataSimulation.SubmitOrder"/> emits its rejection event
    /// immediately rather than deferring it to a stage (design section 16),
    /// and stamps it from <see cref="MissionState.Tick"/> — so a rejection
    /// raised after the run has advanced must carry the advanced tick. The
    /// order is rejected for carrying a single path node, which
    /// <see cref="OrderRejectReason.InvalidNodeCount"/> names.
    /// </summary>
    [Fact]
    public void SubmitOrder_RejectionEmittedAfterTheRunAdvanced_CarriesTheAdvancedTick()
    {
        var sim = BuildFiringFixture(shooterEntityId: 1);
        sim.RunTick(23);

        var (_, _, rejection) = sim.SubmitOrder(
            targetTick: 24,
            factionId: 0,
            addressees: ImmutableArray.Create(1UL),
            kind: OrderKind.MoveAlongPath,
            pathNodes: ImmutableArray.Create(new OrderPathNode(4, 4)));

        Assert.NotNull(rejection);

        var rejected = sim.State.EventFeed.Events
            .Where(e => e.Kind == MissionEventKind.OrderRejected)
            .ToArray();

        Assert.Single(rejected);
        Assert.Equal(23L, rejected[0].Tick);
    }

    /// <summary>
    /// <see cref="MissionSnapshot"/> already carried
    /// <see cref="MissionState.Tick"/> through its round trip, but until the
    /// field advanced there was no run from which a non-zero value could be
    /// captured, so the round trip was only ever exercised at 0. Runs the
    /// simulation, snapshots it, and proves the captured tick is the one the
    /// run reached.
    /// </summary>
    [Fact]
    public void ToSnapshot_AfterARunAdvanced_CapturesTheAdvancedTick()
    {
        var sim = BuildFiringFixture(shooterEntityId: 1);
        sim.RunTick(17);

        var snapshot = sim.State.ToSnapshot();

        Assert.Equal(17L, snapshot.Tick);
    }

}
