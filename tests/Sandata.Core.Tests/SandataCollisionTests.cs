using Sandata.Core.Collision;

namespace Sandata.Core.Tests;

/// <summary>
/// Sandata's own uniform grid, pair emission, and three-phase resolver
/// (plan task 16). These types are a deliberate, independent duplicate of the
/// shape already proven in <c>Hukbo.Core.Simulation</c> — see design section
/// 3 of <c>docs/plans/2026-08-07-sandata-scaffold-design.md</c> for why tier-2
/// extraction is deferred rather than shared. The three fact groups below
/// match the task's stated test bar exactly: a permuted insertion order must
/// not change the emitted pair list, two co-located bodies must resolve to a
/// deterministic separation, and a hand-built multi-body fixture must commit
/// to a pinned position list.
/// </summary>
public sealed class SandataCollisionTests
{
    private const int CellSizeRaw = 100;
    private const int BodyRadiusRaw = 10;

    /// <summary>
    /// The same four bodies, inserted in three different orders, must produce
    /// the exact same <see cref="SandataCollisionGrid.Pairs"/> sequence. This
    /// is what proves the grid's output depends only on which bodies are
    /// present, never on the order the caller happened to supply them in:
    /// <see cref="SandataCollisionGrid"/>'s traversal sorts occupied cells and
    /// its finished pair list is sorted by
    /// <see cref="SandataCollisionPair.CompareTo"/>, so insertion order cannot
    /// leak through.
    /// </summary>
    [Fact]
    public void PairListIsIdenticalUnderPermutedInsertionOrder()
    {
        var bodyA = new SandataCollisionBody(EntityId: 10, XRaw: 0, YRaw: 0, IsAlive: true);
        var bodyB = new SandataCollisionBody(EntityId: 20, XRaw: 15, YRaw: 0, IsAlive: true);
        var bodyC = new SandataCollisionBody(EntityId: 30, XRaw: 30, YRaw: 0, IsAlive: true);
        var bodyD = new SandataCollisionBody(EntityId: 40, XRaw: 10_000, YRaw: 10_000, IsAlive: true);

        // A-B touch (distance 15, contact threshold 20) and B-C touch, but A-C
        // (distance 30) do not, and D sits far enough away to contact nothing.
        var expectedPairs = new[]
        {
            SandataCollisionPair.Create(10, 20),
            SandataCollisionPair.Create(20, 30),
        };

        var orderOne = new[] { bodyA, bodyB, bodyC, bodyD };
        var orderTwo = new[] { bodyD, bodyC, bodyB, bodyA };
        var orderThree = new[] { bodyB, bodyD, bodyA, bodyC };

        var grid = new SandataCollisionGrid(CellSizeRaw);

        grid.Rebuild(orderOne, BodyRadiusRaw);
        Assert.Equal(expectedPairs, grid.Pairs);

        grid.Rebuild(orderTwo, BodyRadiusRaw);
        Assert.Equal(expectedPairs, grid.Pairs);

        grid.Rebuild(orderThree, BodyRadiusRaw);
        Assert.Equal(expectedPairs, grid.Pairs);
    }

    /// <summary>
    /// Two entities that both start, and both propose to end up, at the exact
    /// same position must not collapse onto one point. This is a degenerate
    /// starting configuration — <see cref="SandataCollisionResolver"/>'s own
    /// contract assumes the previous tick's committed positions do not
    /// overlap — but the resolver must still resolve it deterministically and
    /// without overlap rather than trusting the assumption blindly.
    /// </summary>
    /// <remarks>
    /// Task 70 changed this fixture's pinned outcome from what an earlier
    /// version of this test asserted. Before task 70, <c>_committedGrid</c>
    /// started every <c>Resolve</c> call empty and only gained a body once
    /// that body's own turn was processed, so the lower entity ID (entity 1,
    /// processed first) saw nothing at all where it stood, "claimed" the
    /// contested point outright, and only the higher entity ID (entity 2)
    /// ever discovered the coincidence and separated away from it. Task 70
    /// seeds the resolver's working grid with every request's own start
    /// position before any request is tested — see
    /// <see cref="SandataCollisionResolver"/>'s class remarks — so entity 1
    /// now sees entity 2 genuinely standing at the exact point it itself
    /// wants to hold, just as entity 2 would have seen entity 1 there under
    /// the old, unfixed resolver. Entity 1 is processed first purely because
    /// it has the lower entity ID, and it is entity 1, not entity 2, whose
    /// own desired position now triggers the exact-coincidence separation
    /// step. This is a correct, deterministic resolution of a genuinely
    /// symmetric conflict — precisely the kind of case task 70 was written to
    /// make safe — not an arbitrary re-pin to reach green.
    /// </remarks>
    [Fact]
    public void TwoBodiesAtTheSameStartingPositionResolveToADeterministicSeparation()
    {
        var resolver = new SandataCollisionResolver(CellSizeRaw, BodyRadiusRaw);

        var requests = new[]
        {
            new SandataCollisionMoveRequest(
                EntityId: 1, StartXRaw: 0, StartYRaw: 0, DesiredXRaw: 0, DesiredYRaw: 0,
                GroupId: 0, SlotIndex: 0),
            new SandataCollisionMoveRequest(
                EntityId: 2, StartXRaw: 0, StartYRaw: 0, DesiredXRaw: 0, DesiredYRaw: 0,
                GroupId: 0, SlotIndex: 0),
        };

        var results = resolver.Resolve(requests);

        Assert.Equal(
            new[]
            {
                new SandataCollisionMoveResult(1, 20, 0, SandataMovementResolution.Separated),
                new SandataCollisionMoveResult(2, 0, 0, SandataMovementResolution.Held),
            },
            results);

        AssertNoTwoResultsOverlap(results, BodyRadiusRaw);
    }

    /// <summary>
    /// A hand-built eight-request fixture, one covering each resolution kind
    /// (<see cref="SandataMovementResolution.Held"/>,
    /// <see cref="SandataMovementResolution.Moved"/>,
    /// <see cref="SandataMovementResolution.Blocked"/>, and
    /// <see cref="SandataMovementResolution.Separated"/>), committed in
    /// ascending entity-ID order because every request shares group zero and
    /// slot zero. Every position below is pinned by hand from the resolver's
    /// documented contract, not read back from a first run, so a change to
    /// commit order, the separation step, or the overlap predicate will move
    /// this test rather than the fixture silently drifting with it.
    /// </summary>
    [Fact]
    public void EightBodyFixtureProducesAPinnedCommittedPositionList()
    {
        var resolver = new SandataCollisionResolver(CellSizeRaw, BodyRadiusRaw);

        var requests = new[]
        {
            // Five operators holding position, spaced far enough apart that
            // none of them contacts another.
            new SandataCollisionMoveRequest(1, 0, 0, 0, 0, GroupId: 0, SlotIndex: 0),
            new SandataCollisionMoveRequest(2, 500, 0, 500, 0, GroupId: 0, SlotIndex: 0),
            new SandataCollisionMoveRequest(3, 1_000, 0, 1_000, 0, GroupId: 0, SlotIndex: 0),
            new SandataCollisionMoveRequest(4, 1_500, 0, 1_500, 0, GroupId: 0, SlotIndex: 0),
            new SandataCollisionMoveRequest(5, 2_000, 0, 2_000, 0, GroupId: 0, SlotIndex: 0),
            // Moves into open ground: accepted exactly as proposed.
            new SandataCollisionMoveRequest(6, 3_000, 0, 700, 0, GroupId: 0, SlotIndex: 0),
            // Moves within five raw units of entity 2's committed position —
            // overlapping but not coincident — so it is rejected and holds.
            new SandataCollisionMoveRequest(7, 3_500, 0, 505, 0, GroupId: 0, SlotIndex: 0),
            // Moves exactly onto entity 2's committed position: coincident,
            // so it is repaired one diameter east instead.
            new SandataCollisionMoveRequest(8, 4_000, 0, 500, 0, GroupId: 0, SlotIndex: 0),
        };

        var results = resolver.Resolve(requests);

        Assert.Equal(
            new[]
            {
                new SandataCollisionMoveResult(1, 0, 0, SandataMovementResolution.Held),
                new SandataCollisionMoveResult(2, 500, 0, SandataMovementResolution.Held),
                new SandataCollisionMoveResult(3, 1_000, 0, SandataMovementResolution.Held),
                new SandataCollisionMoveResult(4, 1_500, 0, SandataMovementResolution.Held),
                new SandataCollisionMoveResult(5, 2_000, 0, SandataMovementResolution.Held),
                new SandataCollisionMoveResult(6, 700, 0, SandataMovementResolution.Moved),
                new SandataCollisionMoveResult(7, 3_500, 0, SandataMovementResolution.Blocked),
                new SandataCollisionMoveResult(8, 520, 0, SandataMovementResolution.Separated),
            },
            results);

        AssertNoTwoResultsOverlap(results, BodyRadiusRaw);
    }

    /// <summary>
    /// Task 70's defect in one sentence: before this task, <c>_committedGrid</c>
    /// started every <see cref="SandataCollisionResolver.Resolve"/> call empty
    /// and only gained a body once that body's own turn was processed, so a
    /// higher-priority entity could move onto a not-yet-processed
    /// lower-priority entity's real start position undetected. This fixture
    /// pins that failure at its narrowest: entity 1 (processed first) proposes
    /// to move exactly onto entity 2's start position, and no other body
    /// exists to mask the result. Ran against the unmodified, pre-task-70
    /// resolver, this fails: entity 1 committed <c>Moved</c> to (200, 0)
    /// against an empty grid, and entity 2, rejected at its own desired
    /// position for an unrelated, non-exact overlap, fell through to an
    /// unconditional commit at its own start (200, 0) — the same point,
    /// exactly, entity 1 had already taken. Every position pinned below is
    /// hand-derived from the fixed resolver's own documented contract (see
    /// <see cref="SandataCollisionResolver"/>'s class remarks and the written
    /// rule on <see cref="SandataCollisionResolver.CommitOne"/>), not read
    /// back from a first run.
    /// </summary>
    [Fact]
    public void ExactCoincidenceOnStartPositionDoesNotCollideWithTheEntityThatMovedThere()
    {
        var resolver = new SandataCollisionResolver(CellSizeRaw, BodyRadiusRaw);

        var requests = new[]
        {
            // Proposes to move exactly onto entity 2's start position.
            new SandataCollisionMoveRequest(1, 0, 0, 200, 0, GroupId: 0, SlotIndex: 0),
            // Proposes a small, non-exact step that overlaps wherever entity 1
            // ends up near (200, 0), so its desired position is rejected for a
            // reason unrelated to the exact coincidence at its own start.
            new SandataCollisionMoveRequest(2, 200, 0, 190, 0, GroupId: 0, SlotIndex: 0),
        };

        var results = resolver.Resolve(requests);

        Assert.Equal(
            new[]
            {
                new SandataCollisionMoveResult(1, 220, 0, SandataMovementResolution.Separated),
                new SandataCollisionMoveResult(2, 190, 0, SandataMovementResolution.Moved),
            },
            results);

        AssertNoTwoResultsOverlap(results, BodyRadiusRaw);
    }

    /// <summary>
    /// The same defect as
    /// <see cref="ExactCoincidenceOnStartPositionDoesNotCollideWithTheEntityThatMovedThere"/>,
    /// but with a third body added purely to reject entity 3's desired
    /// position for a reason that has nothing to do with entity 1's earlier
    /// move — a shape closer to an ordinary squad scene than the two-entity
    /// fixture above. Ran against the unmodified, pre-task-70 resolver, entity
    /// 1 commits <c>Moved</c> onto entity 3's start (200, 0) against an empty
    /// grid; entity 2 (stationary at (215, 0), overlapping entity 1's new
    /// position but never checked against it) commits <c>Blocked</c> onto its
    /// own start anyway; and entity 3, rejected at its desired position by
    /// both of them, falls through to an unconditional commit at its own
    /// start — the exact point entity 1 already occupies. That failure is
    /// entity 1 landing exactly on top of entity 3, distance zero, the
    /// clearest possible violation of the no-overlap invariant.
    /// </summary>
    [Fact]
    public void BlockedFallbackOntoTakenStartDoesNotCollideWithTheEntityThatTookIt()
    {
        var resolver = new SandataCollisionResolver(CellSizeRaw, BodyRadiusRaw);

        var requests = new[]
        {
            // Proposes to move exactly onto entity 3's start position.
            new SandataCollisionMoveRequest(1, 0, 0, 200, 0, GroupId: 0, SlotIndex: 0),
            // Holds a fixed position that overlaps wherever entity 1 ends up
            // near (200, 0), so it has its own, independent reason to reject
            // entity 3's desired position later.
            new SandataCollisionMoveRequest(2, 215, 0, 215, 0, GroupId: 0, SlotIndex: 0),
            // Proposes a small step that overlaps entity 2's fixed position,
            // never entity 1's, so its desired-position rejection is entirely
            // unrelated to entity 1's earlier move onto its start.
            new SandataCollisionMoveRequest(3, 200, 0, 210, 0, GroupId: 0, SlotIndex: 0),
        };

        var results = resolver.Resolve(requests);

        AssertNoTwoResultsOverlap(results, BodyRadiusRaw);
    }

    /// <summary>
    /// A partial (non-exact) overlap version of the same defect: neither
    /// body's final committed position is the other's exactly, but they are
    /// still within one body diameter of each other, which is just as much a
    /// no-overlap-invariant violation as an exact coincidence. Ran against the
    /// unmodified, pre-task-70 resolver, entity 1 commits <c>Moved</c> to
    /// (195, 0) against an empty grid — five raw units short of entity 2's
    /// start rather than exactly onto it — and entity 2, holding in place at
    /// (200, 0), is rejected by that overlap and falls through to an
    /// unconditional commit at its own start anyway, landing five raw units
    /// (distance 5, contact threshold 20) from entity 1.
    /// </summary>
    [Fact]
    public void PartialOverlapAtACommittedStartPositionIsRepairedRatherThanCommittedInto()
    {
        var resolver = new SandataCollisionResolver(CellSizeRaw, BodyRadiusRaw);

        var requests = new[]
        {
            // Proposes to move within one body diameter of entity 2's start,
            // but not exactly onto it.
            new SandataCollisionMoveRequest(1, 0, 0, 195, 0, GroupId: 0, SlotIndex: 0),
            // Holds its own start position, which entity 1's move now
            // overlaps without being exactly coincident with it.
            new SandataCollisionMoveRequest(2, 200, 0, 200, 0, GroupId: 0, SlotIndex: 0),
        };

        var results = resolver.Resolve(requests);

        AssertNoTwoResultsOverlap(results, BodyRadiusRaw);
    }

    /// <summary>
    /// The same request set, resolved by three fresh resolver instances fed
    /// the array in three different orders, must produce the exact same
    /// committed result list. This is <see cref="SandataCollisionResolver"/>'s
    /// own version of the permutation guarantee
    /// <c>LocalAvoidanceTests.PermutingTheProposalInsertionOrderChangesNothing</c>
    /// already proves one layer up, checked directly at the resolver instead
    /// of through <c>LocalAvoidance</c>: the prioritise step sorts by request
    /// value — <c>(GroupId, SlotIndex, EntityId)</c> — never by array index, so
    /// the caller's incidental array order cannot leak into either the commit
    /// order or the committed positions.
    /// </summary>
    [Fact]
    public void PermutingTheRequestArrayOrderDoesNotChangeCommittedPositions()
    {
        var baseRequests = new[]
        {
            new SandataCollisionMoveRequest(1, 0, 0, 15, 0, GroupId: 0, SlotIndex: 0),
            new SandataCollisionMoveRequest(2, 60, 0, 45, 0, GroupId: 0, SlotIndex: 0),
            new SandataCollisionMoveRequest(3, 120, 0, 105, 0, GroupId: 0, SlotIndex: 0),
            new SandataCollisionMoveRequest(4, 180, 0, 180, 0, GroupId: 0, SlotIndex: 0),
        };

        var forwardOrder = baseRequests;
        var reverseOrder = baseRequests.Reverse().ToArray();
        var shuffledOrder = new[] { baseRequests[2], baseRequests[0], baseRequests[3], baseRequests[1] };

        var forwardResults = new SandataCollisionResolver(CellSizeRaw, BodyRadiusRaw).Resolve(forwardOrder);
        var reverseResults = new SandataCollisionResolver(CellSizeRaw, BodyRadiusRaw).Resolve(reverseOrder);
        var shuffledResults = new SandataCollisionResolver(CellSizeRaw, BodyRadiusRaw).Resolve(shuffledOrder);

        Assert.Equal(forwardResults, reverseResults);
        Assert.Equal(forwardResults, shuffledResults);
    }

    /// <summary>
    /// Fails the assertion with a diagnostic pairing every offending entity ID
    /// with its committed position, rather than a bare boolean, when two
    /// results in the same commit end up within one body diameter of each
    /// other. Mirrors
    /// <c>LocalAvoidanceTests.AssertNoTwoCommittedBodiesOverlap</c>'s own
    /// pairwise sweep, using the same
    /// <see cref="SandataCollisionGrid.Overlaps"/> predicate the resolver
    /// itself is built on, so this assertion can never disagree with the
    /// resolver about what "overlap" means.
    /// </summary>
    private static void AssertNoTwoResultsOverlap(IReadOnlyList<SandataCollisionMoveResult> results, int bodyRadiusRaw)
    {
        for (var i = 0; i < results.Count; i++)
        {
            for (var j = i + 1; j < results.Count; j++)
            {
                if (SandataCollisionGrid.Overlaps(
                    results[i].CommittedXRaw, results[i].CommittedYRaw,
                    results[j].CommittedXRaw, results[j].CommittedYRaw,
                    bodyRadiusRaw))
                {
                    throw new Xunit.Sdk.XunitException(
                        $"Entities {results[i].EntityId} at ({results[i].CommittedXRaw}, {results[i].CommittedYRaw}) " +
                        $"and {results[j].EntityId} at ({results[j].CommittedXRaw}, {results[j].CommittedYRaw}) overlap.");
                }
            }
        }
    }

    /// <summary>
    /// Two bodies straddling a negative cell boundary (cell size 100, so the
    /// boundary at -100 separates cell -2 from cell -1) must still be found
    /// as a contact pair. <see cref="SandataCollisionGrid"/> computes cell
    /// coordinates with <see cref="Sandata.Core.Mathematics.IntegerMath.FloorDiv"/>
    /// rather than the truncating built-in <c>/</c>, precisely so a body on
    /// the negative side of the map's origin lands in its correct cell
    /// instead of being folded into cell zero.
    /// </summary>
    [Fact]
    public void NegativeCoordinatesAreIndexedAndDetectContactCorrectly()
    {
        var nearBoundaryLow = new SandataCollisionBody(EntityId: 1, XRaw: -105, YRaw: -500, IsAlive: true);
        var nearBoundaryHigh = new SandataCollisionBody(EntityId: 2, XRaw: -95, YRaw: -500, IsAlive: true);
        var deeplyNegative = new SandataCollisionBody(EntityId: 3, XRaw: -500, YRaw: -500, IsAlive: true);

        var grid = new SandataCollisionGrid(CellSizeRaw);

        grid.Rebuild([nearBoundaryLow, nearBoundaryHigh, deeplyNegative], BodyRadiusRaw);

        Assert.Equal([SandataCollisionPair.Create(1, 2)], grid.Pairs);
    }

    /// <summary>
    /// An entity never collides with itself, so asking for a pair of an
    /// entity ID with itself is an emitter defect, not an empty result.
    /// </summary>
    [Fact]
    public void CollisionPairCreateThrowsOnEqualEntityIds()
    {
        Assert.Throws<ArgumentException>(() => SandataCollisionPair.Create(7, 7));
    }
}
