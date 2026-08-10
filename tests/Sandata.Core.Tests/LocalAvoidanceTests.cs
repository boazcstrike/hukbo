using Sandata.Core.Collision;
using Sandata.Core.Movement;

namespace Sandata.Core.Tests;

/// <summary>
/// Plan task 43's local avoidance: <see cref="SidestepRules"/>'s pure rotation
/// geometry, and <see cref="LocalAvoidance"/>'s "propose, prioritise, commit"
/// composition of it over <c>Sandata.Core.Collision.SandataCollisionResolver</c>
/// (plan task 16). See design section 8 of
/// <c>docs/plans/2026-08-07-sandata-scaffold-design.md</c>, "Local avoidance:
/// propose, prioritise, commit", for the three ordered rules every fact below
/// checks against.
/// </summary>
public sealed class LocalAvoidanceTests
{
    // A cell must be at least one body diameter wide (SandataCollisionGrid's
    // own constraint), and the funnel fixture below needs several free cells
    // of slack around each body, so these two constants are picked the same
    // way SandataCollisionTests picks its own: round numbers comfortably
    // inside that constraint, not measured from anything.
    private const int BodyRadiusRaw = 10;
    private const int CellSizeRaw = 40;

    /// <summary>
    /// <see cref="SidestepRules.Sidestep"/>'s output for two hand-picked
    /// vectors, at both entity-ID parities, pinned against values computed
    /// independently in Python from <c>Trig.cs</c>'s own published quarter-wave
    /// table (the same table <see cref="Trig"/> compiles from) rather than
    /// derived from the implementation under test — so a change to the
    /// rotation formula, the truncation direction, or the parity rule moves
    /// this test instead of silently agreeing with whatever the code now
    /// does.
    /// </summary>
    [Fact]
    public void SidestepRotatesByExactlyOneSectorAndSideIsPinnedByEntityIdParity()
    {
        // sin(4096) = 25080, cos(4096) = 60547 at Trig's 65536 scale — a
        // full-length vector (65536, 0) rotated by exactly one Facing16
        // sector (22.5 degrees) lands at (cos, sin) for an even entity id
        // (turn toward the positive/clockwise side) and (cos, -sin) for odd.
        Assert.Equal((60547, 25080), SidestepRules.Sidestep(65536, 0, entityId: 0));
        Assert.Equal((60547, -25080), SidestepRules.Sidestep(65536, 0, entityId: 1));
        Assert.Equal((60547, 25080), SidestepRules.Sidestep(65536, 0, entityId: 2));
        Assert.Equal((60547, -25080), SidestepRules.Sidestep(65536, 0, entityId: 3));

        // A shorter, non-power-of-two vector, so the test also exercises the
        // truncating-division rounding path rather than only a case where the
        // scale divides evenly.
        Assert.Equal((923, 382), SidestepRules.Sidestep(1000, 0, entityId: 4));
        Assert.Equal((923, -382), SidestepRules.Sidestep(1000, 0, entityId: 5));
    }

    /// <summary>
    /// The zero vector has no direction to turn away from. A unit proposing
    /// to hold its own position (<c>DesiredXRaw/YRaw == StartXRaw/YRaw</c>)
    /// never reaches <see cref="SidestepRules.Sidestep"/> as a blocked entity
    /// in practice — a stationary proposal cannot be <c>Blocked</c>, since
    /// holding still never overlaps anything the previous tick's committed
    /// state did not already tolerate — but the function itself is total, so
    /// this pins what it returns for that input rather than leaving it
    /// undefined.
    /// </summary>
    [Fact]
    public void SidestepOfTheZeroVectorIsTheZeroVector()
    {
        Assert.Equal((0, 0), SidestepRules.Sidestep(0, 0, entityId: 0));
        Assert.Equal((0, 0), SidestepRules.Sidestep(0, 0, entityId: 1));
    }

    /// <summary>
    /// Eight units start on one side of a single doorway point, all propose
    /// toward it, and all must pass through to their own lane on the far
    /// side. This is the plan's exact "eight units funnelling into one
    /// doorway all pass through with no overlap and no deadlock" bar. Every
    /// tick's committed positions are checked for pairwise overlap with the
    /// same <see cref="SandataCollisionGrid.Overlaps"/> predicate the
    /// resolver itself enforces, and the simulation must finish inside a
    /// pinned tick budget.
    /// </summary>
    /// <remarks>
    /// <see cref="LocalAvoidance"/> only arbitrates unit-against-unit
    /// contact; there is no wall or obstacle geometry system in
    /// <c>Sandata.Core</c> yet for a literal narrow gap to be built from. The
    /// "doorway" here is a single shared waypoint every unit's own proposal
    /// converges on before diverging back to its own lane — which reproduces
    /// exactly the property a physical doorway would: eight bodies whose
    /// desired positions collide near one point, in one tick window, must
    /// still each end up somewhere legal.
    /// </remarks>
    [Fact]
    public void EightUnitsFunnellingIntoOneDoorwayAllPassThroughWithNoOverlapAndNoDeadlock()
    {
        // A tick budget pinned by hand, not measured from a passing run:
        // eight units 350 raw units from the doorway and 300 more beyond it,
        // stepping at most 15 raw units per axis per tick, need at least
        // ceil(650 / 15) = 44 ticks of unobstructed travel; 200 leaves
        // generous room for the funnel's own sidestep-and-wait contention
        // without hiding a genuine deadlock. This is exactly the kind of
        // invented constant the task's PROVISIONAL reporting requirement
        // means — recorded here, not silently assumed.
        const int TickBudget = 200;
        const int MaxStepRaw = 15;
        const int StartYRaw = -350;
        const int FinishYRaw = 300;
        const int DoorHalfWidthRaw = 15;

        var originalXRaw = new[] { -175, -125, -75, -25, 25, 75, 125, 175 };
        var count = originalXRaw.Length;

        var xs = new int[count];
        var ys = new int[count];
        for (var i = 0; i < count; i++)
        {
            xs[i] = originalXRaw[i];
            ys[i] = StartYRaw;
        }

        var avoidance = new LocalAvoidance(CellSizeRaw, BodyRadiusRaw);
        var proposals = new MovementProposal[count];

        var tick = 0;
        for (; tick < TickBudget; tick++)
        {
            var allArrived = true;

            for (var i = 0; i < count; i++)
            {
                var (targetXRaw, targetYRaw) = WaypointFor(xs[i], ys[i], originalXRaw[i], FinishYRaw, DoorHalfWidthRaw);
                var (stepXRaw, stepYRaw) = ClampedStepToward(xs[i], ys[i], targetXRaw, targetYRaw, MaxStepRaw);

                // Stage 9 legitimately reads the frozen tick-start view — the
                // positions every entity committed to at the end of the
                // previous tick — because that is already-committed state,
                // not another entity's same-tick proposal (design section 5's
                // "no unit sees another's proposal" bans the second, not the
                // first). Task 70 closed the resolver's own blind spot where a
                // higher-priority entity could walk into a lower-priority
                // entity's not-yet-processed current position undetected —
                // see SandataCollisionResolver's class remarks — so this test
                // no longer needs to pre-filter that case itself before
                // handing a proposal to LocalAvoidance; ordinary contact now
                // reaches LocalAvoidance's own Blocked-then-sidestep path
                // exactly as design section 8 intends, in every priority
                // direction.
                proposals[i] = new MovementProposal(
                    EntityId: (ulong)(i + 1),
                    StartXRaw: xs[i],
                    StartYRaw: ys[i],
                    DesiredXRaw: xs[i] + stepXRaw,
                    DesiredYRaw: ys[i] + stepYRaw,
                    GroupId: 0,
                    SlotIndex: i);

                if (ys[i] < FinishYRaw)
                {
                    allArrived = false;
                }
            }

            if (allArrived)
            {
                break;
            }

            var results = avoidance.Commit(proposals);
            Assert.Equal(count, results.Count);

            var previousXs = (int[])xs.Clone();
            var previousYs = (int[])ys.Clone();

            foreach (var result in results)
            {
                var index = (int)result.EntityId - 1;
                xs[index] = result.CommittedXRaw;
                ys[index] = result.CommittedYRaw;
            }

            AssertNoTwoCommittedBodiesOverlap(results, tick, proposals, previousXs, previousYs);
        }

        Assert.True(
            tick < TickBudget,
            $"Eight-unit doorway funnel did not settle within the {TickBudget}-tick budget; that is a deadlock, not a slow pass-through.");

        for (var i = 0; i < count; i++)
        {
            Assert.Equal(originalXRaw[i], xs[i]);
            Assert.Equal(FinishYRaw, ys[i]);
        }
    }

    /// <summary>
    /// The same eight proposals, fed to fresh <see cref="LocalAvoidance"/>
    /// instances in three different array orders, must produce the exact
    /// same committed result list. This is what proves the "prioritise"
    /// step's total order — <c>(groupId, slotIndex, entityId)</c> — actually
    /// decides commit order, rather than the caller's incidental array
    /// position leaking through: design section 5's stage-9 rule is that
    /// "no unit sees another's proposal", and this is the observable
    /// consequence of that rule holding — the buffer is a set, not a
    /// sequence, from the resolver's point of view.
    /// </summary>
    [Fact]
    public void PermutingTheProposalInsertionOrderChangesNothing()
    {
        var baseProposals = new[]
        {
            new MovementProposal(1, 0, 0, 15, 0, GroupId: 0, SlotIndex: 0),
            new MovementProposal(2, 60, 0, 45, 0, GroupId: 0, SlotIndex: 1),
            new MovementProposal(3, 120, 0, 105, 0, GroupId: 0, SlotIndex: 2),
            new MovementProposal(4, 180, 0, 165, 0, GroupId: 0, SlotIndex: 3),
        };

        var forwardOrder = baseProposals;
        var reverseOrder = baseProposals.Reverse().ToArray();
        var shuffledOrder = new[] { baseProposals[2], baseProposals[0], baseProposals[3], baseProposals[1] };

        var forwardResults = new LocalAvoidance(CellSizeRaw, BodyRadiusRaw).Commit(forwardOrder);
        var reverseResults = new LocalAvoidance(CellSizeRaw, BodyRadiusRaw).Commit(reverseOrder);
        var shuffledResults = new LocalAvoidance(CellSizeRaw, BodyRadiusRaw).Commit(shuffledOrder);

        Assert.Equal(forwardResults, reverseResults);
        Assert.Equal(forwardResults, shuffledResults);
    }

    /// <summary>
    /// The property this file must actually check, per the task's own
    /// criterion-discipline instruction: no code path in
    /// <c>src/Sandata.Core/Movement</c> sums two or more direction vectors
    /// into an accumulator whose result then determines a proposed position.
    /// A grep for the identifier "force" or "velocity" would be the wrong
    /// test — a file could rename a summed-repulsion accumulator to
    /// <c>avoidanceBias</c> and pass such a grep while still being exactly
    /// the rigid-body shortcut design section 8 and <c>CLAUDE.md</c> section
    /// 9 forbid. A real force/impulse scheme's signature is observable
    /// behaviour, not spelling: a blocked unit's escape displacement would
    /// grow, or its direction would keep changing, as more obstacles
    /// contribute their own repulsion term to the sum. This test blocks the
    /// same entity's identical original proposal against one nearby obstacle
    /// and then against three (one at the same position, two more placed so
    /// that they too individually overlap the blocked entity's desired
    /// point, but do not overlap each other or the entity's eventual
    /// sidestep candidate), and asserts the committed outcome is bit-for-bit
    /// identical in both cases. <see cref="SidestepRules.Sidestep"/> and
    /// <see cref="LocalAvoidance"/> only ever look at the blocked entity's
    /// own already-proposed displacement and its own entity ID — never at an
    /// obstacle's position or count — so the crowd size cannot move the
    /// answer; a summed-repulsion scheme's answer would move with it by
    /// construction.
    /// </summary>
    [Fact]
    public void BlockedEntitysCommittedDisplacementDoesNotDependOnHowManyObstaclesBlockedIt()
    {
        var blockedEntity = new MovementProposal(
            EntityId: 100, StartXRaw: 0, StartYRaw: 0, DesiredXRaw: 1000, DesiredYRaw: 0,
            GroupId: 0, SlotIndex: 1);

        // One obstacle, close enough to (1000, 0) to overlap it (distance 15,
        // diameter 20) but not coincide with it.
        var oneObstacle = new[]
        {
            new MovementProposal(1, 1000, 15, 1000, 15, GroupId: 0, SlotIndex: 0),
            blockedEntity,
        };

        // Three obstacles: the same one as above, plus two more that also
        // individually overlap (1000, 0) from other directions, spaced at
        // least one diameter from each other and from the entity's rotated
        // sidestep candidate so none of them changes whether the sidestep
        // itself succeeds.
        var threeObstacles = new[]
        {
            new MovementProposal(1, 1000, 15, 1000, 15, GroupId: 0, SlotIndex: 0),
            new MovementProposal(2, 1000, -15, 1000, -15, GroupId: 0, SlotIndex: 0),
            new MovementProposal(3, 985, 0, 985, 0, GroupId: 0, SlotIndex: 0),
            blockedEntity,
        };

        var oneObstacleResults = new LocalAvoidance(CellSizeRaw, BodyRadiusRaw).Commit(oneObstacle);
        var threeObstaclesResults = new LocalAvoidance(CellSizeRaw, BodyRadiusRaw).Commit(threeObstacles);

        var oneObstacleOutcome = Single(oneObstacleResults, entityId: 100);
        var threeObstaclesOutcome = Single(threeObstaclesResults, entityId: 100);

        Assert.Equal(oneObstacleOutcome, threeObstaclesOutcome);

        // The blocked entity did get somewhere other than its own start —
        // the sidestep engaged rather than every scenario collapsing to the
        // same trivial "held at start" answer, which would make the equality
        // assertion above vacuous.
        Assert.NotEqual(SandataMovementResolution.Blocked, oneObstacleOutcome.Resolution);
        Assert.NotEqual(0, oneObstacleOutcome.CommittedXRaw);

        // And the magnitude of that displacement is bounded by the original
        // proposed step (rotation cannot lengthen a vector), not scaled up by
        // the extra obstacles a summed-repulsion scheme would have added in:
        // squared distance from start must stay at or under 1000^2, with the
        // small shortfall coming only from the rotation's own bounded
        // integer-division rounding documented on SidestepRules.Sidestep.
        var squaredDisplacement = SandataCollisionGrid.SquaredDistance(
            0, 0, oneObstacleOutcome.CommittedXRaw, oneObstacleOutcome.CommittedYRaw);
        Assert.InRange(squaredDisplacement, 900_000, 1_000_000);
    }

    private static SandataCollisionMoveResult Single(IReadOnlyList<SandataCollisionMoveResult> results, ulong entityId)
    {
        foreach (var result in results)
        {
            if (result.EntityId == entityId)
            {
                return result;
            }
        }

        throw new InvalidOperationException($"No committed result for entity {entityId}.");
    }

    /// <summary>
    /// While south of the doorway, the target is the nearest point inside
    /// the doorway's own finite gap — <c>Clamp(currentXRaw, -DoorHalfWidthRaw,
    /// DoorHalfWidthRaw)</c> at the doorway's Y — not literally the single
    /// point <c>(0, 0)</c>. A single point is a mathematically zero-width
    /// gap eight 20-raw-unit-diameter bodies cannot occupy at once no matter
    /// how they arbitrate, so aiming every lane at exactly the same column
    /// would make the fixture unwinnable by construction rather than
    /// exercising <see cref="LocalAvoidance"/>'s actual "propose, prioritise,
    /// commit" contention. A finite gap, wider than one body but far
    /// narrower than the eight lanes' own 350-raw-unit spread, is what a
    /// "doorway" the plan's DONE WHEN criterion means physically: a
    /// bottleneck units must take turns through, not a single coordinate.
    /// Once north of the doorway the target is the unit's own starting lane;
    /// once arrived, its own current position, so
    /// <see cref="ClampedStepToward"/> proposes zero further movement.
    /// </summary>
    private static (int TargetXRaw, int TargetYRaw) WaypointFor(
        int currentXRaw, int currentYRaw, int laneXRaw, int finishYRaw, int doorHalfWidthRaw)
    {
        if (currentYRaw < 0)
        {
            return (Math.Clamp(currentXRaw, -doorHalfWidthRaw, doorHalfWidthRaw), 0);
        }

        return currentYRaw < finishYRaw ? (laneXRaw, finishYRaw) : (currentXRaw, currentYRaw);
    }

    /// <summary>
    /// Steps toward a target by clamping each axis independently to
    /// <paramref name="maxStepRaw"/> — a Chebyshev step, not a Euclidean one,
    /// so this fixture needs no square root anywhere in its own movement
    /// model, matching the determinism contract's ban on <c>Math.Sqrt</c> in
    /// spirit even though this test file is not itself in the scanned
    /// <c>src/Sandata.Core</c> tree.
    /// </summary>
    private static (int DeltaXRaw, int DeltaYRaw) ClampedStepToward(
        int currentXRaw, int currentYRaw, int targetXRaw, int targetYRaw, int maxStepRaw)
    {
        var deltaXRaw = Math.Clamp(targetXRaw - currentXRaw, -maxStepRaw, maxStepRaw);
        var deltaYRaw = Math.Clamp(targetYRaw - currentYRaw, -maxStepRaw, maxStepRaw);
        return (deltaXRaw, deltaYRaw);
    }

    private static void AssertNoTwoCommittedBodiesOverlap(
        IReadOnlyList<SandataCollisionMoveResult> results,
        int tick,
        MovementProposal[] proposals,
        int[] previousXs,
        int[] previousYs)
    {
        for (var i = 0; i < results.Count; i++)
        {
            for (var j = i + 1; j < results.Count; j++)
            {
                if (SandataCollisionGrid.Overlaps(
                        results[i].CommittedXRaw, results[i].CommittedYRaw,
                        results[j].CommittedXRaw, results[j].CommittedYRaw,
                        BodyRadiusRaw))
                {
                    var detail = string.Join(
                        " | ",
                        proposals.Select((p, idx) =>
                            $"e{p.EntityId} prevpos=({previousXs[idx]},{previousYs[idx]}) desired=({p.DesiredXRaw},{p.DesiredYRaw})"));
                    throw new Xunit.Sdk.XunitException(
                        $"tick={tick} Entities {results[i].EntityId} and {results[j].EntityId} overlap after commit. {detail}");
                }
            }
        }
    }

    /// <summary>
    /// Task 89's recorded gap, pinned here from 2026-08-11: a mover whose
    /// desired step is blocked by a body that never moves refuses the step,
    /// takes its single <see cref="SidestepRules.Sidestep"/> retry, has that
    /// refused too, and then repeats both refusals for as long as the caller
    /// keeps asking. It never re-plans and it never gets past.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This pins a known gap, not a desired outcome.</b> Design section 8
    /// states the rule in full — "if that is also blocked, it waits a tick" —
    /// and says nothing about a blocker that never moves. Head on, a
    /// 22.5-degree rotation does not clear a body whose radius exceeds the
    /// step that turns. If a future change lets a blocked mover route around a
    /// static body, this test fails, and the right response then is to delete
    /// it rather than to widen it.
    /// </para>
    /// <para>
    /// <b>Why it lives at stage 10 rather than in <c>TickPipelineTests</c>.</b>
    /// It used to be reproduced through <c>SandataSimulation.RunTick</c>
    /// against an opposing-faction blocker. On 2026-08-11 stage 9 gained the
    /// halt-to-engage rule, so that fixture's leader now stops to shoot the
    /// blocker before it can ever walk into it and the stall became
    /// unreachable from there — the old test failed on its own guard
    /// assertion, which is exactly what that assertion was for. Down here
    /// there is no sensing, no intent, and no faction, so no future combat
    /// rule can quietly stop this fixture from reaching the defect.
    /// </para>
    /// </remarks>
    [Fact]
    public void CommitAgainstAStaticBody_StallsForeverBecauseTheOneSidestepIsBlockedToo()
    {
        const ulong moverId = 1UL;
        const ulong blockerId = 3UL;

        // Head on, along +X. The mover starts clear of the blocker — 24 raw
        // apart against a body diameter of 20, so nothing overlaps at rest and
        // the resolver has no reason to push anybody anywhere. The step of 12
        // would close that to 12, well inside the diameter, so the full step
        // overlaps and so does its single 22.5-degree rotation.
        const int blockerXRaw = 200;
        const int moverStartXRaw = blockerXRaw - (BodyRadiusRaw * 2) - 4;
        const int yRaw = 500;
        const int stepRaw = 12;

        var avoidance = new LocalAvoidance(CellSizeRaw, BodyRadiusRaw);

        var moverXRaw = moverStartXRaw;
        var moverYRaw = yRaw;

        for (var tick = 0; tick < 40; tick++)
        {
            var proposals = new[]
            {
                new MovementProposal(
                    moverId, moverXRaw, moverYRaw, moverXRaw + stepRaw, moverYRaw, GroupId: 1UL, SlotIndex: 0),
                // The blocker proposes its own position every tick: it is the
                // static body the whole finding is about.
                new MovementProposal(
                    blockerId, blockerXRaw, yRaw, blockerXRaw, yRaw, GroupId: 2UL, SlotIndex: 0),
            };

            var results = avoidance.Commit(proposals);

            var mover = results.Single(r => r.EntityId == moverId);
            var blocker = results.Single(r => r.EntityId == blockerId);

            Assert.Equal(blockerXRaw, blocker.CommittedXRaw);
            Assert.Equal(yRaw, blocker.CommittedYRaw);

            Assert.True(
                mover.CommittedXRaw == moverXRaw && mover.CommittedYRaw == moverYRaw,
                $"tick {tick}: the mover advanced from ({moverXRaw}, {moverYRaw}) to " +
                $"({mover.CommittedXRaw}, {mover.CommittedYRaw}). If stage 10 now routes around a " +
                "static body, delete this test rather than widening it.");

            moverXRaw = mover.CommittedXRaw;
            moverYRaw = mover.CommittedYRaw;
        }

        // Not "it did not move": it did not move *and* it was still trying,
        // from a position that is genuinely short of the blocker rather than
        // resting on top of it.
        Assert.Equal(moverStartXRaw, moverXRaw);

        // At rest the two are clear of each other, so nothing above was the
        // resolver merely refusing to let bodies interpenetrate...
        Assert.True(
            blockerXRaw - moverXRaw >= BodyRadiusRaw * 2,
            $"the mover should be resting clear of the blocker, but the gap is " +
            $"{blockerXRaw - moverXRaw} raw against a body diameter of {BodyRadiusRaw * 2}");

        // ...and one full step would genuinely overlap, so the mover really is
        // blocked rather than having quietly arrived somewhere acceptable.
        Assert.True(
            blockerXRaw - (moverXRaw + stepRaw) < BodyRadiusRaw * 2,
            "the fixture must actually be blocked for this finding to mean anything");
    }
}
