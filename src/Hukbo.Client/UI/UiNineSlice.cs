using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

/// <summary>
/// One tile of a nine-slice draw: a source rectangle read from the chrome
/// atlas paired with the destination rectangle it is drawn into.
/// </summary>
internal readonly record struct UiNineSliceTile(Rectangle Source, Rectangle Destination);

/// <summary>
/// Nine-slice chrome geometry and drawing for panel-style UI chrome. The
/// chrome atlas is a 128x64 RGBA texture holding two 48x48 regions, both
/// carved with a 12-pixel margin: the surface region at atlas origin (0, 0)
/// and the border region at atlas origin (64, 0). Every pixel in the atlas
/// is white with alpha 0, 140, or 255, so a caller's tint colour decides the
/// drawn hue; the border region's centre cell is fully transparent and is
/// never drawn.
/// </summary>
internal static class UiNineSlice
{
    /// <summary>
    /// Atlas origin and size of the surface region, drawn as a single
    /// stretched quad by <see cref="DrawPanel"/>.
    /// </summary>
    internal static readonly Rectangle SurfaceRegion = new(0, 0, 48, 48);

    /// <summary>
    /// Atlas origin and size of the border region, sliced into the nine
    /// tiles <see cref="BuildTiles"/> produces.
    /// </summary>
    internal static readonly Rectangle BorderRegion = new(64, 0, 48, 48);

    /// <summary>
    /// Index into <see cref="BuildTiles"/>'s result of the centre tile. The
    /// border region's centre cell is empty, so <see cref="DrawPanel"/>
    /// skips this index rather than drawing a transparent quad.
    /// </summary>
    internal const int CentreTileIndex = 4;

    /// <summary>
    /// Splits <paramref name="bounds"/> into the nine destination rectangles
    /// of a nine-slice draw, each paired with the matching rectangle read
    /// from <paramref name="sourceRegion"/>, in the fixed order top-left,
    /// top, top-right, left, centre, right, bottom-left, bottom,
    /// bottom-right.
    /// </summary>
    /// <remarks>
    /// This is a pure function: no <c>SpriteBatch</c>, no <c>Texture2D</c>,
    /// no <c>GraphicsDevice</c>. It is the tested seam behind
    /// <see cref="DrawPanel"/>.
    ///
    /// <para>
    /// Degenerate-bounds rule: when <paramref name="bounds"/> is smaller
    /// than <c>2 * marginPixels</c> along an axis, the margin used on that
    /// axis is clamped to half of that axis's length (integer division,
    /// rounded down), independently for the destination axis and for the
    /// matching source-region axis. Both corners on that axis therefore
    /// shrink together rather than overlapping, the centre band on that axis
    /// degrades to zero width or height instead of going negative, and every
    /// produced rectangle stays within <paramref name="bounds"/> and within
    /// <paramref name="sourceRegion"/>. The clamp is applied per axis and per
    /// rectangle (destination vs. source), so a source region and a
    /// destination of different sizes each get their own, correctly
    /// tiling, margin.
    /// </para>
    /// </remarks>
    /// <param name="bounds">The destination rectangle to fill.</param>
    /// <param name="marginPixels">
    /// The corner size in pixels, applied identically to the destination and
    /// to <paramref name="sourceRegion"/> before the degenerate-bounds clamp
    /// described above.
    /// </param>
    /// <param name="sourceRegion">
    /// The atlas region to slice, given as an origin and a size.
    /// </param>
    internal static UiNineSliceTile[] BuildTiles(
        Rectangle bounds,
        int marginPixels,
        Rectangle sourceRegion)
    {
        if (marginPixels < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(marginPixels),
                marginPixels,
                "Margin must be zero or positive.");
        }

        var destX = SliceAxis(bounds.X, bounds.Width, marginPixels);
        var destY = SliceAxis(bounds.Y, bounds.Height, marginPixels);
        var srcX = SliceAxis(sourceRegion.X, sourceRegion.Width, marginPixels);
        var srcY = SliceAxis(sourceRegion.Y, sourceRegion.Height, marginPixels);

        return
        [
            Tile(srcX.Start, srcY.Start, destX.Start, destY.Start),
            Tile(srcX.Middle, srcY.Start, destX.Middle, destY.Start),
            Tile(srcX.End, srcY.Start, destX.End, destY.Start),
            Tile(srcX.Start, srcY.Middle, destX.Start, destY.Middle),
            Tile(srcX.Middle, srcY.Middle, destX.Middle, destY.Middle),
            Tile(srcX.End, srcY.Middle, destX.End, destY.Middle),
            Tile(srcX.Start, srcY.End, destX.Start, destY.End),
            Tile(srcX.Middle, srcY.End, destX.Middle, destY.End),
            Tile(srcX.End, srcY.End, destX.End, destY.End),
        ];

        static UiNineSliceTile Tile(
            AxisSegment sourceX,
            AxisSegment sourceY,
            AxisSegment destX,
            AxisSegment destY) =>
            new(
                new Rectangle(sourceX.Origin, sourceY.Origin, sourceX.Length, sourceY.Length),
                new Rectangle(destX.Origin, destY.Origin, destX.Length, destY.Length));
    }

    /// <summary>
    /// Draws one nine-slice chrome panel: a single stretched quad for the
    /// surface fill, tinted <paramref name="surfaceTint"/>, followed by the
    /// eight outer slices of the border region, tinted
    /// <paramref name="borderTint"/>. The border region's centre slice is
    /// skipped because that cell is empty in the atlas.
    /// </summary>
    /// <param name="spriteBatch">An already-begun sprite batch.</param>
    /// <param name="chromeAtlas">
    /// The 128x64 chrome atlas holding the surface and border regions
    /// described on <see cref="UiNineSlice"/>.
    /// </param>
    /// <param name="bounds">The destination rectangle to fill.</param>
    /// <param name="surfaceTint">Tint applied to the surface fill quad.</param>
    /// <param name="borderTint">Tint applied to the eight border slices.</param>
    /// <param name="marginPixels">
    /// The corner size in pixels; see <see cref="BuildTiles"/> for the
    /// degenerate-bounds clamp this value is subject to.
    /// </param>
    internal static void DrawPanel(
        SpriteBatch spriteBatch,
        Texture2D chromeAtlas,
        Rectangle bounds,
        Color surfaceTint,
        Color borderTint,
        int marginPixels)
    {
        spriteBatch.Draw(chromeAtlas, bounds, SurfaceRegion, surfaceTint);

        var borderTiles = BuildTiles(bounds, marginPixels, BorderRegion);
        for (var i = 0; i < borderTiles.Length; i++)
        {
            if (i == CentreTileIndex)
            {
                continue;
            }

            var tile = borderTiles[i];
            spriteBatch.Draw(chromeAtlas, tile.Destination, tile.Source, borderTint);
        }
    }

    /// <summary>
    /// One segment of a sliced axis: an origin coordinate and a length in
    /// pixels.
    /// </summary>
    private readonly record struct AxisSegment(int Origin, int Length);

    /// <summary>
    /// The three segments — start corner, middle band, end corner — that one
    /// axis of a nine-slice splits into.
    /// </summary>
    private readonly record struct AxisSlices(
        AxisSegment Start,
        AxisSegment Middle,
        AxisSegment End);

    /// <summary>
    /// Splits one axis of length <paramref name="length"/>, starting at
    /// <paramref name="origin"/>, into a start corner, a middle band, and an
    /// end corner. The corner size is <paramref name="margin"/>, clamped to
    /// half of <paramref name="length"/> (integer division) so that the two
    /// corners never overlap and the middle band never goes negative; see
    /// the degenerate-bounds rule documented on <see cref="BuildTiles"/>.
    /// The three segments always sum to exactly <paramref name="length"/>.
    /// </summary>
    private static AxisSlices SliceAxis(int origin, int length, int margin)
    {
        var clampedMargin = Math.Min(margin, length / 2);
        var middleLength = length - (2 * clampedMargin);

        return new AxisSlices(
            new AxisSegment(origin, clampedMargin),
            new AxisSegment(origin + clampedMargin, middleLength),
            new AxisSegment(origin + clampedMargin + middleLength, clampedMargin));
    }
}
