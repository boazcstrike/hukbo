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
    /// Two entities that both propose the exact same position must not
    /// collapse onto one point. The lower entity ID commits first (the
    /// (GroupId, SlotIndex, EntityId) order, with both groups and slots at
    /// zero today), claims the position outright, and the second entity's
    /// exact coincidence with that already-committed body is repaired by
    /// <see cref="SandataCollisionResolver"/>'s fixed east-first separation
    /// step — never a force, never an impulse, never a push-apart.
    /// </summary>
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
                new SandataCollisionMoveResult(1, 0, 0, SandataMovementResolution.Held),
                new SandataCollisionMoveResult(2, 20, 0, SandataMovementResolution.Separated),
            },
            results);
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
