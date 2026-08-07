using Sandata.Core.Navigation;

namespace Sandata.Core.Tests;

/// <summary>
/// Covers <see cref="ClearanceField"/> against plan task 19's two-part test
/// bar: a hand-derived 8 by 8 fixture checked cell by cell, and a proof that
/// <see cref="ClearanceField.RebuildLocal"/> after a door change reproduces
/// exactly what a full <see cref="ClearanceField.Build"/> from scratch would
/// produce for the same, post-change passability.
/// </summary>
public sealed class ClearanceFieldTests
{
    /// <summary>
    /// The 8 by 8 fixture is a single blocked cell at <c>(3, 3)</c> on an
    /// otherwise fully open grid — deliberately not a wall or a border,
    /// because a border's four sides are each a whole blocked line, and the
    /// cheapest way to reach a whole line is always a pure orthogonal walk
    /// toward whichever side is closest (a diagonal step costs 14 for the
    /// same one-cell reduction in distance that an orthogonal step buys for
    /// 10, so cutting toward a line never pays for itself). A single point
    /// obstacle is the smallest fixture that forces the diagonal weight to
    /// matter at all, which is the case this test exists to pin.
    ///
    /// <para>
    /// With one obstacle, the cheapest path from any cell to it is exactly
    /// the octile-optimal walk: <c>min(|dx|, |dy|)</c> diagonal steps,
    /// which close both axes at once, followed by
    /// <c>max(|dx|, |dy|) - min(|dx|, |dy|)</c> orthogonal steps to close
    /// out whichever axis had farther to go. In the same terms
    /// <see cref="Sandata.Core.Navigation.NavHeuristic.Octile"/> uses:
    /// <c>clearance(x, y) = 10 * (max - min) + 14 * min</c> where
    /// <c>min</c>/<c>max</c> are the sorted <c>(|x - 3|, |y - 3|)</c>. Every
    /// one of the 64 numbers below was computed by hand from that formula —
    /// not read back from <see cref="ClearanceField"/> — and the obstacle
    /// cell itself is <c>0</c> by the formula too (both deltas zero), which
    /// happens to coincide with, but is asserted independently of,
    /// <see cref="ClearanceField.BlockedClearance"/>.
    /// </para>
    /// </summary>
    [Fact]
    public void Build_SingleObstacleOnAnOpenEightBySEightGrid_MatchesTheHandDerivedOctileValueCellByCell()
    {
        const int width = 8;
        const int height = 8;
        const int obstacleX = 3;
        const int obstacleY = 3;

        // expected[y][x], derived by hand: 10 * (max(dx, dy) - min(dx, dy)) + 14 * min(dx, dy),
        // dx = |x - 3|, dy = |y - 3|.
        int[][] expected =
        [
            [42, 38, 34, 30, 34, 38, 42, 52],
            [38, 28, 24, 20, 24, 28, 38, 48],
            [34, 24, 14, 10, 14, 24, 34, 44],
            [30, 20, 10, 0, 10, 20, 30, 40],
            [34, 24, 14, 10, 14, 24, 34, 44],
            [38, 28, 24, 20, 24, 28, 38, 48],
            [42, 38, 34, 30, 34, 38, 42, 52],
            [52, 48, 44, 40, 44, 48, 52, 56],
        ];

        var passability = new byte[width * height];
        Array.Fill(passability, (byte)1);
        passability[(obstacleY * width) + obstacleX] = 0;

        var clearance = new int[width * height];

        ClearanceField.Build(passability, clearance, width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var actual = clearance[(y * width) + x];
                Assert.True(
                    expected[y][x] == actual,
                    $"cell ({x}, {y}): expected {expected[y][x]}, got {actual}.");
            }
        }
    }

    [Fact]
    public void Build_TheObstacleCellItself_IsBlockedClearance()
    {
        var passability = new byte[64];
        Array.Fill(passability, (byte)1);
        passability[(3 * 8) + 3] = 0;
        var clearance = new int[64];

        ClearanceField.Build(passability, clearance, 8, 8);

        Assert.Equal(ClearanceField.BlockedClearance, clearance[(3 * 8) + 3]);
    }

    /// <summary>
    /// This is the test that matters more than the hand-derived fixture
    /// above, per plan task 19: an incremental clearance update that
    /// silently drifts from a full recompute is a desync a spectator would
    /// never see coming, because nothing about it looks wrong locally.
    ///
    /// <para>
    /// The fixture is a 20 by 20 grid: a blocked border ring, and one
    /// interior wall row at <c>y = 10</c> spanning <c>x = 1..18</c>, with a
    /// single door cell at <c>(10, 10)</c>. The door starts closed (part of
    /// the solid wall row) and this test opens it — the passability flip a
    /// door opening actually is. Because the wall row's cells immediately on
    /// either side of the door (<c>(9, 10)</c> and <c>(11, 10)</c>) stay
    /// blocked regardless of the door, and the grid's border is nine cells
    /// away from the door in every direction, the door's true radius of
    /// influence is small: this test rebuilds locally with a radius of only
    /// 4 cells, a window nowhere near the border, and still expects an exact
    /// match — there is no fudging here by choosing a radius so generous
    /// that "local" quietly means "everything".
    /// </para>
    /// </summary>
    [Fact]
    public void RebuildLocal_AfterADoorOpens_MatchesAFullRebuildFromScratchForThePostChangePassability()
    {
        const int width = 20;
        const int height = 20;
        const int doorX = 10;
        const int doorY = 10;
        const int radiusOfInfluenceCells = 4;

        var passabilityBeforeDoorOpens = BuildBorderedRoomWithADoor(width, height, doorY, doorX, doorIsOpen: false);
        var clearanceBeforeDoorOpens = new int[width * height];
        ClearanceField.Build(passabilityBeforeDoorOpens, clearanceBeforeDoorOpens, width, height);

        var passabilityAfterDoorOpens = BuildBorderedRoomWithADoor(width, height, doorY, doorX, doorIsOpen: true);

        var expectedFromFullRebuild = new int[width * height];
        ClearanceField.Build(passabilityAfterDoorOpens, expectedFromFullRebuild, width, height);

        var actualFromLocalRebuild = (int[])clearanceBeforeDoorOpens.Clone();
        ClearanceField.RebuildLocal(
            passabilityAfterDoorOpens,
            actualFromLocalRebuild,
            width,
            height,
            changedMinX: doorX,
            changedMinY: doorY,
            changedMaxX: doorX,
            changedMaxY: doorY,
            radiusOfInfluenceCells: radiusOfInfluenceCells);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                Assert.True(
                    expectedFromFullRebuild[index] == actualFromLocalRebuild[index],
                    $"cell ({x}, {y}): full rebuild {expectedFromFullRebuild[index]}, local rebuild {actualFromLocalRebuild[index]}.");
            }
        }

        // The rebuild has to have actually changed something near the door —
        // otherwise this test would pass even if RebuildLocal did nothing at
        // all. The cell directly north of the door loses its closest
        // obstacle (the door cell itself, one orthogonal step away) once the
        // door opens, and falls back to the nearest still-blocked wall cell,
        // one diagonal step away: 10 before, 14 after.
        var cellNorthOfDoor = ((doorY - 1) * width) + doorX;
        Assert.Equal(10, clearanceBeforeDoorOpens[cellNorthOfDoor]);
        Assert.Equal(14, actualFromLocalRebuild[cellNorthOfDoor]);
    }

    [Fact]
    public void Build_ThrowsArgumentException_WhenPassabilityLengthDoesNotMatchWidthTimesHeight()
    {
        var passability = new byte[10];
        var clearance = new int[12];

        Assert.Throws<ArgumentException>(() => ClearanceField.Build(passability, clearance, width: 3, height: 4));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 0)]
    [InlineData(-1, 4)]
    public void Build_ThrowsArgumentOutOfRange_ForANonPositiveDimension(int width, int height)
    {
        var passability = new byte[Math.Max(width, 0) * Math.Max(height, 0)];
        var clearance = new int[passability.Length];

        Assert.Throws<ArgumentOutOfRangeException>(() => ClearanceField.Build(passability, clearance, width, height));
    }

    [Fact]
    public void RebuildLocal_ThrowsArgumentOutOfRange_WhenTheRadiusIsNegative()
    {
        var passability = new byte[16];
        Array.Fill(passability, (byte)1);
        var clearance = new int[16];

        Assert.Throws<ArgumentOutOfRangeException>(() => ClearanceField.RebuildLocal(
            passability, clearance, width: 4, height: 4,
            changedMinX: 1, changedMinY: 1, changedMaxX: 1, changedMaxY: 1,
            radiusOfInfluenceCells: -1));
    }

    [Fact]
    public void RebuildLocal_ThrowsArgumentOutOfRange_WhenTheChangedRectangleIsOutsideTheGrid()
    {
        var passability = new byte[16];
        Array.Fill(passability, (byte)1);
        var clearance = new int[16];

        Assert.Throws<ArgumentOutOfRangeException>(() => ClearanceField.RebuildLocal(
            passability, clearance, width: 4, height: 4,
            changedMinX: 4, changedMinY: 0, changedMaxX: 4, changedMaxY: 0,
            radiusOfInfluenceCells: 1));
    }

    /// <summary>
    /// A blocked ring border plus one interior wall row at <paramref name="wallY"/>
    /// spanning <c>x = 1 .. width - 2</c>, with a single door cell at
    /// <c>(doorX, wallY)</c> that is passable when <paramref name="doorIsOpen"/>
    /// and blocked, indistinguishable from the rest of the wall row,
    /// otherwise.
    /// </summary>
    private static byte[] BuildBorderedRoomWithADoor(int width, int height, int wallY, int doorX, bool doorIsOpen)
    {
        var passability = new byte[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var isBorder = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                var isWallRow = y == wallY && x >= 1 && x <= width - 2;
                var isOpenDoor = isWallRow && x == doorX && doorIsOpen;

                var isBlocked = (isBorder || isWallRow) && !isOpenDoor;
                passability[(y * width) + x] = isBlocked ? (byte)0 : (byte)1;
            }
        }

        return passability;
    }
}
