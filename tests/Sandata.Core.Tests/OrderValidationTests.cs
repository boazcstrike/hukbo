using System.Collections.Immutable;
using System.Linq;
using Sandata.Core.Geometry;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;

namespace Sandata.Core.Tests;

/// <summary>
/// Task 58 of docs/plans/2026-08-07-sandata-scaffold.md:
/// <see cref="OrderValidation.ValidateMoveAlongPath"/> against design
/// section 16's four rejection rules — "Validation happens at submission,
/// and rejection is observable." One fixture per rule, isolated so each
/// fires exactly the reason it names and no other; a door-cell fixture
/// proving door cells are deliberately not a rejection condition; a
/// grazing-endpoint fixture asserting the exact
/// <see cref="SegmentRelation"/> classification value, not just
/// accept/reject; and a near-miss fixture proving the wall-crossing rule
/// uses no distance tolerance.
/// </summary>
public sealed class OrderValidationTests
{
    /// <summary>
    /// A grid of <paramref name="widthCells"/> by <paramref name="heightCells"/>
    /// with every cell <see cref="NavCellFlags.Open"/>. <see cref="NavGrid"/>
    /// defaults every cell to <see cref="NavCellFlags.Blocked"/> until baked;
    /// these fixtures bake nothing, so this is the "clean floor" every test
    /// below starts from and then punches its one intended obstacle into.
    /// </summary>
    private static NavGrid NewOpenGrid(int widthCells, int heightCells)
    {
        var grid = new NavGrid(widthCells, heightCells);
        Array.Fill(grid.Passability, NavCellFlags.Open);
        return grid;
    }

    private static WallBuckets NoWalls(NavGrid grid) => WallBuckets.Build(grid, [], [], [], []);

    private static WallBuckets OneWall(NavGrid grid, long ax, long ay, long bx, long by) =>
        WallBuckets.Build(grid, [ax], [ay], [bx], [by]);

    private static AuthoredPath PathOf(params (long X, long Y)[] nodes) =>
        new(nodes.Select(n => new OrderPathNode(n.X, n.Y)).ToImmutableArray());

    [Fact]
    public void ValidateMoveAlongPath_AllRulesPass_ReturnsNull()
    {
        var grid = NewOpenGrid(10, 10);
        var wallBuckets = NoWalls(grid);
        var path = PathOf((0, 0), (36, 36));

        var reason = OrderValidation.ValidateMoveAlongPath(path, grid, wallBuckets);

        Assert.Null(reason);
    }

    [Fact]
    public void ValidateMoveAlongPath_NodeOutsideMapBounds_ReturnsNodeOutsideMapBounds()
    {
        // Grid is 10x10 cells * CellSizeWu(4) = 40x40 wu, so the closed
        // bounds interval is [0, 40] x [0, 40]. x = 41 is one past it.
        var grid = NewOpenGrid(10, 10);
        var wallBuckets = NoWalls(grid);
        var path = PathOf((0, 0), (41, 0));

        var reason = OrderValidation.ValidateMoveAlongPath(path, grid, wallBuckets);

        Assert.Equal(OrderRejectReason.NodeOutsideMapBounds, reason);
    }

    [Fact]
    public void ValidateMoveAlongPath_NodeExactlyOnFarBoundary_IsNotOutsideMapBounds()
    {
        // x = 40 is exactly the closed interval's upper bound — the "zero
        // width outer boundary" edge case NavBake.RasterizeSegment documents
        // as a legitimate point, not a violation. No wall or blocked cell is
        // anywhere near it, so this must be accepted outright.
        var grid = NewOpenGrid(10, 10);
        var wallBuckets = NoWalls(grid);
        var path = PathOf((0, 0), (40, 40));

        var reason = OrderValidation.ValidateMoveAlongPath(path, grid, wallBuckets);

        Assert.Null(reason);
    }

    [Fact]
    public void ValidateMoveAlongPath_NodeInBlockedCell_ReturnsNodeInBlockedCell()
    {
        var grid = NewOpenGrid(10, 10);
        grid.Passability[grid.CellIndex(5, 5)] = NavCellFlags.Blocked;
        var wallBuckets = NoWalls(grid);

        // (22, 22) converts to cell (22 >> 2, 22 >> 2) = (5, 5), the one
        // cell this fixture blocked. No wall exists to confuse the result
        // with SegmentCrossesWall.
        var path = PathOf((0, 0), (22, 22));

        var reason = OrderValidation.ValidateMoveAlongPath(path, grid, wallBuckets);

        Assert.Equal(OrderRejectReason.NodeInBlockedCell, reason);
    }

    [Fact]
    public void ValidateMoveAlongPath_PathThroughDoorCell_IsAccepted()
    {
        // Same geometry as the blocked-cell fixture above, except cell
        // (5, 5) is a Door, not Blocked — design section 16: "Door cells are
        // deliberately not a rejection condition."
        var grid = NewOpenGrid(10, 10);
        grid.Passability[grid.CellIndex(5, 5)] = NavCellFlags.Door;
        var wallBuckets = NoWalls(grid);
        var path = PathOf((0, 0), (22, 22));

        var reason = OrderValidation.ValidateMoveAlongPath(path, grid, wallBuckets);

        Assert.Null(reason);
    }

    [Fact]
    public void ValidateMoveAlongPath_SegmentCrossesWall_ReturnsSegmentCrossesWall()
    {
        // A vertical wall at x = 20 spanning the whole grid height. The path
        // runs horizontally through x = 20 at y = 20, a classic crossing.
        var grid = NewOpenGrid(10, 10);
        var wallBuckets = OneWall(grid, 20, 0, 20, 40);
        var path = PathOf((10, 20), (30, 20));

        var reason = OrderValidation.ValidateMoveAlongPath(path, grid, wallBuckets);

        Assert.Equal(OrderRejectReason.SegmentCrossesWall, reason);
    }

    [Fact]
    public void ValidateMoveAlongPath_SegmentEndpointTouchesWallEndpointExactly_ClassifiedAsTouchingAndRejected()
    {
        // The wall is (0,0)-(10,0). The path's own segment starts exactly at
        // the wall's B endpoint, (10,0), then runs away from it. This is the
        // same coordinate shape as
        // ExactPredicatesTests.ClassifySegments_SharedEndpoint_ReturnsTouching.
        var grid = NewOpenGrid(4, 4);
        var wallBuckets = OneWall(grid, 0, 0, 10, 0);
        var path = PathOf((10, 0), (10, 10));

        // Assert the specific classification value the exact predicate
        // reaches for this pair — not just that OrderValidation rejects it.
        var relation = ExactPredicates.ClassifySegments(10, 0, 10, 10, 0, 0, 10, 0);
        Assert.Equal(SegmentRelation.Touching, relation);

        var reason = OrderValidation.ValidateMoveAlongPath(path, grid, wallBuckets);

        Assert.Equal(OrderRejectReason.SegmentCrossesWall, reason);
    }

    [Fact]
    public void ValidateMoveAlongPath_SegmentPassesOneWorldUnitFromAWall_ExactTestAcceptsWhereADistanceToleranceWouldReject()
    {
        // The wall is the horizontal segment (0,0)-(10,0). The path segment
        // (5,1)-(5,10) runs vertically, starting exactly 1 world unit above
        // the wall's line and moving away from it — it never reaches y = 0,
        // so the two segments never meet.
        //
        // By hand: orient(A,B,C) = cross((0,9),(-5,-1)) = 45 (positive);
        // orient(A,B,D) = cross((0,9),(5,-1)) = -45 (negative) — C and D
        // straddle line AB. But orient(C,D,A) = cross((10,0),(5,1)) = 10
        // (positive) and orient(C,D,B) = cross((10,0),(5,10)) = 100
        // (positive) — A and B do NOT straddle line CD, both sitting on the
        // same side of it. A proper crossing needs both pairs to straddle,
        // so this is Disjoint; the four orientation values are also all
        // non-zero, so no endpoint lands on the other segment's interior
        // either.
        var grid = NewOpenGrid(4, 4);
        var wallBuckets = OneWall(grid, 0, 0, 10, 0);
        var path = PathOf((5, 1), (5, 10));

        // Prove a distance-tolerance check would disagree with the exact
        // predicate: the path's near endpoint sits exactly 1.0 world unit
        // from the wall's infinite line (perpendicular distance, computed
        // here as |cross((D-C),(P-C))| / |D-C|, the standard point-to-line
        // formula), and a plausible "reject anything within 1.5 wu of a
        // wall" tolerance — the kind of check an epsilon-based validator
        // might use instead of an exact test — would flag this path as
        // grazing the wall even though the segments never actually meet.
        const double plausibleEpsilon = 1.5;
        var crossMagnitude = Math.Abs(((10.0 - 0.0) * (1.0 - 0.0)) - ((0.0 - 0.0) * (5.0 - 0.0)));
        var wallLength = Math.Sqrt(((10.0 - 0.0) * (10.0 - 0.0)) + ((0.0 - 0.0) * (0.0 - 0.0)));
        var perpendicularDistance = crossMagnitude / wallLength;

        Assert.Equal(1.0, perpendicularDistance, precision: 10);
        Assert.True(
            perpendicularDistance < plausibleEpsilon,
            "This fixture only proves anything if a plausible tolerance would have flagged it.");

        var relation = ExactPredicates.ClassifySegments(5, 1, 5, 10, 0, 0, 10, 0);
        Assert.Equal(SegmentRelation.Disjoint, relation);

        var reason = OrderValidation.ValidateMoveAlongPath(path, grid, wallBuckets);

        Assert.Null(reason);
    }

    [Fact]
    public void ValidateMoveAlongPath_OneNode_ReturnsInvalidNodeCount()
    {
        var grid = NewOpenGrid(10, 10);
        var wallBuckets = NoWalls(grid);
        var path = PathOf((0, 0));

        var reason = OrderValidation.ValidateMoveAlongPath(path, grid, wallBuckets);

        Assert.Equal(OrderRejectReason.InvalidNodeCount, reason);
    }

    [Fact]
    public void ValidateMoveAlongPath_TwoNodes_ReturnsNull()
    {
        var grid = NewOpenGrid(10, 10);
        var wallBuckets = NoWalls(grid);
        var path = PathOf((0, 0), (4, 0));

        var reason = OrderValidation.ValidateMoveAlongPath(path, grid, wallBuckets);

        Assert.Null(reason);
    }

    [Fact]
    public void ValidateMoveAlongPath_NodeCountExactlyAtCap_ReturnsNull()
    {
        // A wide, open, single-row corridor with room to spare for the cap
        // node count laid out one world unit apart in a straight line, so no
        // other rule can fire.
        var grid = NewOpenGrid(64, 4);
        var wallBuckets = NoWalls(grid);
        var path = new AuthoredPath(BuildStraightLine(Order.MaxAuthoredPathNodeCount));

        var reason = OrderValidation.ValidateMoveAlongPath(path, grid, wallBuckets);

        Assert.Null(reason);
    }

    [Fact]
    public void ValidateMoveAlongPath_NodeCountOneOverCap_ReturnsInvalidNodeCount()
    {
        var grid = NewOpenGrid(64, 4);
        var wallBuckets = NoWalls(grid);
        var path = new AuthoredPath(BuildStraightLine(Order.MaxAuthoredPathNodeCount + 1));

        var reason = OrderValidation.ValidateMoveAlongPath(path, grid, wallBuckets);

        Assert.Equal(OrderRejectReason.InvalidNodeCount, reason);
    }

    private static ImmutableArray<OrderPathNode> BuildStraightLine(int nodeCount)
    {
        var builder = ImmutableArray.CreateBuilder<OrderPathNode>(nodeCount);
        for (var index = 0; index < nodeCount; index++)
        {
            builder.Add(new OrderPathNode(index, 2));
        }

        return builder.MoveToImmutable();
    }
}
