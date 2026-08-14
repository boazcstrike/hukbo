using Hukbo.Client.UI;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Pins the pure nine-slice tiling geometry behind <c>UiNineSlice</c>.
/// </summary>
/// <remarks>
/// Nothing here constructs a <c>SpriteBatch</c>, a <c>Texture2D</c>, a
/// <c>GraphicsDevice</c>, or a window. Every assertion runs against
/// <see cref="UiNineSlice.BuildTiles"/>, the same pure geometry helper
/// <see cref="UiNineSlice.DrawPanel"/> calls to place its eight border
/// slices, matching the GPU-independent convention set by
/// <c>PawnRendererTests</c>.
/// </remarks>
public sealed class UiNineSliceTests
{
    private static readonly Rectangle SourceRegion = new(64, 0, 48, 48);

    private const int TopLeftIndex = 0;
    private const int TopIndex = 1;
    private const int TopRightIndex = 2;
    private const int LeftIndex = 3;
    private const int CentreIndex = 4;
    private const int RightIndex = 5;
    private const int BottomLeftIndex = 6;
    private const int BottomIndex = 7;
    private const int BottomRightIndex = 8;

    public static TheoryData<int> Margins()
    {
        var data = new TheoryData<int>();
        for (var margin = 1; margin <= 16; margin++)
        {
            data.Add(margin);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Margins))]
    public void BuildTiles_NineDestinationRectsTileBoundsExactlyForGenerousBounds(
        int margin)
    {
        var bounds = new Rectangle(10, 20, 300, 220);

        var tiles = UiNineSlice.BuildTiles(bounds, margin, SourceRegion);

        AssertTilesPartitionBounds(tiles, bounds);
    }

    [Theory]
    [MemberData(nameof(Margins))]
    public void BuildTiles_NineDestinationRectsTileBoundsExactlyForTightBounds(
        int margin)
    {
        // A bounds rectangle exactly 2*margin square is the boundary case
        // between the ordinary path and the degenerate clamp: corners meet
        // with no middle band left over, and the tiles must still partition
        // bounds with zero gap and zero overlap.
        var bounds = new Rectangle(0, 0, 2 * margin, 2 * margin);

        var tiles = UiNineSlice.BuildTiles(bounds, margin, SourceRegion);

        AssertTilesPartitionBounds(tiles, bounds);
    }

    [Theory]
    [MemberData(nameof(Margins))]
    public void BuildTiles_CornersAreExactlyMarginPixelsSquareAndNeverScale(
        int margin)
    {
        var bounds = new Rectangle(0, 0, 300, 220);

        var tiles = UiNineSlice.BuildTiles(bounds, margin, SourceRegion);

        foreach (var index in new[]
                 {
                     TopLeftIndex, TopRightIndex, BottomLeftIndex, BottomRightIndex,
                 })
        {
            Assert.Equal(margin, tiles[index].Destination.Width);
            Assert.Equal(margin, tiles[index].Destination.Height);
            Assert.Equal(margin, tiles[index].Source.Width);
            Assert.Equal(margin, tiles[index].Source.Height);
        }
    }

    [Fact]
    public void BuildTiles_EdgeRectsVaryInOneAxisOnlyRelativeToTheAdjacentCorner()
    {
        var bounds = new Rectangle(0, 0, 300, 220);
        const int Margin = 12;

        var tiles = UiNineSlice.BuildTiles(bounds, Margin, SourceRegion);

        // The top edge shares its height with the top-left corner (the
        // fixed axis) but takes its width from the centre column (the
        // varying axis), and symmetrically for the other three edges.
        Assert.Equal(tiles[TopLeftIndex].Destination.Height, tiles[TopIndex].Destination.Height);
        Assert.Equal(tiles[CentreIndex].Destination.Width, tiles[TopIndex].Destination.Width);
        Assert.NotEqual(tiles[TopLeftIndex].Destination.Width, tiles[TopIndex].Destination.Width);

        Assert.Equal(tiles[BottomLeftIndex].Destination.Height, tiles[BottomIndex].Destination.Height);
        Assert.Equal(tiles[CentreIndex].Destination.Width, tiles[BottomIndex].Destination.Width);

        Assert.Equal(tiles[TopLeftIndex].Destination.Width, tiles[LeftIndex].Destination.Width);
        Assert.Equal(tiles[CentreIndex].Destination.Height, tiles[LeftIndex].Destination.Height);
        Assert.NotEqual(tiles[TopLeftIndex].Destination.Height, tiles[LeftIndex].Destination.Height);

        Assert.Equal(tiles[TopRightIndex].Destination.Width, tiles[RightIndex].Destination.Width);
        Assert.Equal(tiles[CentreIndex].Destination.Height, tiles[RightIndex].Destination.Height);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(23, 5)]
    [InlineData(5, 23)]
    [InlineData(0, 40)]
    [InlineData(40, 0)]
    public void BuildTiles_DegenerateBoundsSmallerThanTwiceMarginNeitherOverlapsNorEscapesBounds(
        int width,
        int height)
    {
        var bounds = new Rectangle(3, 4, width, height);
        const int Margin = 12;

        var tiles = UiNineSlice.BuildTiles(bounds, Margin, SourceRegion);

        AssertTilesPartitionBounds(tiles, bounds);
    }

    [Theory]
    [MemberData(nameof(Margins))]
    public void BuildTiles_SourceRectsStayInsideTheDeclaredFortyEightPixelRegion(
        int margin)
    {
        var bounds = new Rectangle(0, 0, 300, 220);

        var tiles = UiNineSlice.BuildTiles(bounds, margin, SourceRegion);

        foreach (var tile in tiles)
        {
            Assert.True(tile.Source.Left >= SourceRegion.Left);
            Assert.True(tile.Source.Top >= SourceRegion.Top);
            Assert.True(tile.Source.Right <= SourceRegion.Right);
            Assert.True(tile.Source.Bottom <= SourceRegion.Bottom);
        }
    }

    [Fact]
    public void BuildTiles_NegativeMarginThrows()
    {
        var bounds = new Rectangle(0, 0, 100, 100);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => UiNineSlice.BuildTiles(bounds, -1, SourceRegion));
    }

    [Fact]
    public void SurfaceRegion_MatchesTheDocumentedAtlasOriginAndSize()
    {
        Assert.Equal(new Rectangle(0, 0, 48, 48), UiNineSlice.SurfaceRegion);
    }

    [Fact]
    public void BorderRegion_MatchesTheDocumentedAtlasOriginAndSize()
    {
        Assert.Equal(new Rectangle(64, 0, 48, 48), UiNineSlice.BorderRegion);
    }

    [Fact]
    public void CentreTileIndex_MatchesTheCentrePositionInBuildTilesOutput()
    {
        var bounds = new Rectangle(0, 0, 300, 220);

        var tiles = UiNineSlice.BuildTiles(bounds, 12, SourceRegion);
        var centre = tiles[UiNineSlice.CentreTileIndex];

        Assert.Equal(
            new Rectangle(12, 12, bounds.Width - 24, bounds.Height - 24),
            centre.Destination);
    }

    /// <summary>
    /// Asserts the nine destination rects in <paramref name="tiles"/> fully
    /// contain <paramref name="bounds"/>, never draw outside it, sum to
    /// exactly its area, and pairwise never overlap — together this proves
    /// zero gap and zero overlap, i.e. an exact tiling.
    /// </summary>
    private static void AssertTilesPartitionBounds(
        UiNineSliceTile[] tiles,
        Rectangle bounds)
    {
        Assert.Equal(9, tiles.Length);

        long totalArea = 0;
        foreach (var tile in tiles)
        {
            var destination = tile.Destination;
            Assert.True(destination.Left >= bounds.Left);
            Assert.True(destination.Top >= bounds.Top);
            Assert.True(destination.Right <= bounds.Right);
            Assert.True(destination.Bottom <= bounds.Bottom);
            totalArea += (long)destination.Width * destination.Height;
        }

        Assert.Equal((long)bounds.Width * bounds.Height, totalArea);

        for (var i = 0; i < tiles.Length; i++)
        {
            for (var j = i + 1; j < tiles.Length; j++)
            {
                Assert.False(
                    tiles[i].Destination.Intersects(tiles[j].Destination),
                    $"Tile {i} overlaps tile {j}.");
            }
        }
    }
}
