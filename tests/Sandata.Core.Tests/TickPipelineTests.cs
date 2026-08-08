using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Sandata.Core.Collision;
using Sandata.Core.Combat;
using Sandata.Core.Determinism;
using Sandata.Core.Events;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Sensing;
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

        var simA = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildFixture());
        var simB = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildFixture());

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

        var sim = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state);
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
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildFixture(96));
        simAtRadius.RunTick(0);
        var atRadiusOne = simAtRadius.PendingMovementProposals.Single(p => p.EntityId == 1UL);
        var atRadiusTwo = simAtRadius.PendingMovementProposals.Single(p => p.EntityId == 2UL);
        Assert.Equal(atRadiusOne.GroupId, atRadiusTwo.GroupId);

        var simBeyondRadius = new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildFixture(97));
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

        var sim = new SandataSimulation(mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, state);
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

        var simNarrow = new SandataSimulation(mission, rulesetNarrow, grid, wallBuckets, BuildFixture());
        simNarrow.RunTick(0);
        var narrowOne = simNarrow.PendingMovementProposals.Single(p => p.EntityId == 1UL);
        var narrowTwo = simNarrow.PendingMovementProposals.Single(p => p.EntityId == 2UL);
        Assert.NotEqual(narrowOne.GroupId, narrowTwo.GroupId);

        var simWide = new SandataSimulation(mission, rulesetWide, grid, wallBuckets, BuildFixture());
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

        var sim = new SandataSimulation(mission, rulesetWideCohesion, grid, wallBuckets, state);
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

        var simLow = new SandataSimulation(mission, rulesetLow, grid, wallBuckets, BuildFixture());
        var simHigh = new SandataSimulation(mission, rulesetHigh, grid, wallBuckets, BuildFixture());

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

        var simInclusive = new SandataSimulation(mission, rulesetInclusive, grid, wallBuckets, BuildFixture());
        simInclusive.RunTick(0);
        var forcedOperator = Assert.Single(simInclusive.State.Operators);
        Assert.Equal((int)WeaponChainPhase.Lowered, forcedOperator.WeaponChainPhase);
        Assert.Equal(0, forcedOperator.WeaponChainRemainingTicks);

        var simJustOutside = new SandataSimulation(mission, rulesetJustOutside, grid, wallBuckets, BuildFixture());
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

        var simWide = new SandataSimulation(mission, rulesetWide, grid, wallBuckets, BuildFixture());
        simWide.RunTick(0);
        var completedOperator = simWide.State.Operators.Single(op => op.EntityId == 1UL);
        Assert.Equal((int)WeaponChainPhase.Aiming, completedOperator.WeaponChainPhase);

        var simNarrow = new SandataSimulation(mission, rulesetNarrow, grid, wallBuckets, BuildFixture());
        simNarrow.RunTick(0);
        var stillTurningOperator = simNarrow.State.Operators.Single(op => op.EntityId == 1UL);
        Assert.Equal((int)WeaponChainPhase.Turning, stillTurningOperator.WeaponChainPhase);
    }

    /// <summary>
    /// Task 79c (docs/plans/2026-08-07-sandata-scaffold.md, the wave-12
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

        var simRifle = new SandataSimulation(mission, ruleset, grid, wallBuckets, BuildFixture(FirearmId.Ak47));
        simRifle.RunTick(0);
        var rifleOperator = simRifle.State.Operators.Single(op => op.EntityId == 1UL);

        var simPistol = new SandataSimulation(mission, ruleset, grid, wallBuckets, BuildFixture(FirearmId.Beretta92Fs));
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
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildFixture(resumedQueue));
        var simFresh = new SandataSimulation(
            mission, SandataRuleset.ModernTacticalV1, grid, wallBuckets, BuildFixture(OrderQueue.Empty));

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

        var sim = new SandataSimulation(mission, ruleset, grid, wallBuckets, state);

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
    /// Task 79b (second pass, after coordinator rejection): closes the gap
    /// task 79a opened — an unassigned operator (no <see
    /// cref="OrderAssignment"/>) whose group now has a published path (<see
    /// cref="RunTick_OutstandingGroupPathRequest_PublishesAtExactlyRequestTickPlusLatencyAndIsNotReissued"/>
    /// proves that publish transition on its own) must actually walk along
    /// that path, not toward the path's own end. The rejected first pass
    /// used a straight spawn-to-goal path, on which "walk toward the
    /// polyline" and "walk toward the goal" are the same motion and so
    /// could not tell <c>leaderArclength</c>'s two candidate values apart;
    /// this fixture instead forces a genuinely bent, multi-vertex smoothed
    /// polyline — a non-axis-aligned start/goal cell pair (so raw A* zigzags
    /// rather than reducing to one segment) plus a real <see
    /// cref="WallBuckets"/> wall segment placed to block the funnel
    /// smoother's line-of-sight shortcut back to a straight line — so the
    /// two candidates diverge sharply on the very first tick after publish.
    /// Reached only through <see cref="SandataSimulation.RunTick"/>,
    /// observed only through the committed <see
    /// cref="SandataSimulation.State"/> position — never
    /// <c>ComputeMovementProposals</c> directly.
    /// </summary>
    /// <remarks>
    /// With cell size 4 (<see cref="NavGrid.CellSizeWu"/>), start cell
    /// (0, 0) and goal cell (6, 3) sit at world-unit centres (2, 2) and
    /// (26, 14); a wall segment running the full grid height at x = 14
    /// forces the funnel-smoothed path through the vertices (2, 2),
    /// (10, 10), (14, 14), (18, 14), (26, 14) instead of a straight line
    /// (confirmed empirically against the real <see
    /// cref="Sandata.Core.Navigation.PathService"/> output before this test
    /// was written). <see cref="Movement.LocalAvoidance.Commit"/> moves an
    /// unblocked proposal straight to its desired point every tick, with no
    /// speed cap of its own, so the very first published tick's committed
    /// position <i>is</i> that tick's <c>ComputeMovementProposals</c>
    /// target. If <c>leaderArclength</c> were pinned to
    /// <see cref="PolylineArclength.TotalLength"/> (the rejected value),
    /// that target would be the polyline's final vertex — the goal itself,
    /// raw (26,624, 14,336) — on this very first tick, regardless of the
    /// operator's own position. This test's first assertion, raw
    /// (7,168, 7,168), is a point on the first segment toward (10, 10), far
    /// short of the goal and off the straight spawn-to-goal line entirely;
    /// it fails under the rejected pinned value, and passes only when
    /// <c>leaderArclength</c> is genuinely derived from the leader's own
    /// projected position. This was confirmed directly: temporarily pinning
    /// <c>leaderArclength</c> back to <c>arclength.TotalLength</c> and
    /// rerunning this fixture's own tick sequence made every tick from the
    /// first published one onward land on raw (26,624, 14,336) — an instant
    /// jump straight to the goal — before the pin was reverted.
    /// </remarks>
    [Fact]
    public void RunTick_UnassignedOperatorInGroupWithPublishedPath_FollowsTheBentPolylineNotTheGoal()
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

        var sim = new SandataSimulation(mission, ruleset, grid, wallBuckets, state);

        // Request tick: path not yet published, so stage 9 still holds.
        sim.RunTick(0);
        var stillAtSpawn = Assert.Single(sim.State.Operators);
        Assert.Equal(2 * FixedPoint.Scale, stillAtSpawn.PositionX.RawValue);
        Assert.Equal(2 * FixedPoint.Scale, stillAtSpawn.PositionY.RawValue);

        // Publish tick: stage 7 publishes the path before stage 9 runs, so
        // this same tick's proposal already targets a point on it. Exact
        // raw (7,168, 7,168) — see remarks above for why this value, and
        // not the goal, is what a correct projection-based leaderArclength
        // produces here.
        sim.RunTick(pathLatencyTicks);
        var afterFirstMove = Assert.Single(sim.State.Operators);
        Assert.Equal(7 * FixedPoint.Scale, afterFirstMove.PositionX.RawValue);
        Assert.Equal(7 * FixedPoint.Scale, afterFirstMove.PositionY.RawValue);

        // Two ticks further on (each RunTick call performs exactly one
        // movement step, so the intermediate tick must actually be run, not
        // skipped over), the leader has walked past the polyline's (14, 14)
        // corner and onto the final, horizontal segment toward the goal —
        // Y pinned at 14 while X still trails the goal's 26. A straight
        // spawn-to-goal beeline would read Y = 10 (not 14) at X = 19 (slope
        // 12/24 from (2, 2) to (26, 14)); landing on the corridor's own Y
        // instead is this fixture's second, independent confirmation that
        // motion follows the polyline's actual shape.
        sim.RunTick(pathLatencyTicks + 1);
        sim.RunTick(pathLatencyTicks + 2);
        var afterCorner = Assert.Single(sim.State.Operators);
        Assert.Equal(19 * FixedPoint.Scale, afterCorner.PositionX.RawValue);
        Assert.Equal(14 * FixedPoint.Scale, afterCorner.PositionY.RawValue);
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

        var sim = new SandataSimulation(mission, ruleset, grid, wallBuckets, state);

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
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(25)]
    public void RunTick_SameGeometryAnyShooterEntityId_AlwaysHits(int shooterEntityId)
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
    /// Task 86 finding replacing the removed
    /// <c>RunTick_Miss_EmitsExactlyOneShotFiredAndOneShotMissedEvent</c>: at
    /// the designed <c>CollisionBodyRadiusRaw</c> (4,352 raw), a miss is
    /// mathematically unreachable for the AK-47 loadout through
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
    /// miss for this weapon. This is a genuine contradiction of the
    /// original task-79d-1 assumption that shooter-id tuning alone
    /// preserves both a hit and a miss path; it is reported, not hidden,
    /// per the task-86 brief's evidence-contradicts-brief rule. This test
    /// pins the impossibility as a regression check: if a future change to
    /// <c>CollisionBodyRadiusRaw</c>, the AK-47's dispersion constants, or
    /// <c>DetectRangeWu</c> ever reopens a reachable miss, this test fails
    /// and the removed <c>RunTick</c>-level miss coverage must return.
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

        return new SandataSimulation(mission, ruleset, grid, wallBuckets, state);
    }
}
