using Hukbo.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// The pawn body atlas's geometry and cell selection (the 2026-08-15 pawn
/// sprite body design, sections 3 and 4). Every assertion here is arithmetic
/// on value types — no <c>GraphicsDevice</c>, no <c>SpriteBatch</c>, no
/// window, per the client presentation test rules in CLAUDE.md section 5.
/// </summary>
public sealed class PawnSpriteAtlasTests
{
    [Fact]
    public void TheAtlasGridAccountsForEveryAuthoredCell()
    {
        Assert.Equal(
            PawnSpriteAtlas.VariantCount,
            PawnSpriteAtlas.Columns * PawnSpriteAtlas.Rows);
    }

    /// <summary>
    /// Pinned against literals rather than against the constants under test.
    /// A dimension read out of <c>Columns * CellWidth</c> would move with the
    /// thing it is meant to catch, so it would pass for an atlas whose grid
    /// silently changed shape underneath the shipped texture.
    /// </summary>
    [Fact]
    public void TheAtlasDimensionsMatchTheShippedTexture()
    {
        Assert.Equal(1120, PawnSpriteAtlas.AtlasWidth);
        Assert.Equal(1170, PawnSpriteAtlas.AtlasHeight);
        Assert.Equal(112, PawnSpriteAtlas.CellWidth);
        Assert.Equal(234, PawnSpriteAtlas.CellHeight);
    }

    [Fact]
    public void EveryCellLiesInsideTheAtlasAndNoTwoCellsOverlap()
    {
        var seen = new List<Rectangle>();

        for (var index = 0; index < PawnSpriteAtlas.VariantCount; index++)
        {
            var cell = PawnSpriteAtlas.GetCellBounds(index);

            Assert.True(cell.Left >= 0);
            Assert.True(cell.Top >= 0);
            Assert.True(cell.Right <= PawnSpriteAtlas.AtlasWidth);
            Assert.True(cell.Bottom <= PawnSpriteAtlas.AtlasHeight);

            foreach (var other in seen)
            {
                Assert.False(
                    cell.Intersects(other),
                    $"Cell {index} overlaps an earlier cell.");
            }

            seen.Add(cell);
        }

        Assert.Equal(PawnSpriteAtlas.VariantCount, seen.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(PawnSpriteAtlas.VariantCount)]
    public void AnOutOfRangeCellIndexThrowsRatherThanClamping(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PawnSpriteAtlas.GetCellBounds(index));
    }

    [Fact]
    public void TheSameWarriorAlwaysDrawsTheSameCell()
    {
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Equal(
                PawnSpriteAtlas.GetVariantIndex(entityId),
                PawnSpriteAtlas.GetVariantIndex(entityId));
        }
    }

    [Fact]
    public void EverySelectedCellIsInRange()
    {
        for (ulong entityId = 0; entityId < 5_000; entityId++)
        {
            var index = PawnSpriteAtlas.GetVariantIndex(entityId);

            Assert.InRange(index, 0, PawnSpriteAtlas.VariantCount - 1);
        }
    }

    /// <summary>
    /// The reason selection mixes through <c>PresentationHash</c> instead of
    /// taking the identifier modulo the count: entity identifiers are handed
    /// out in roster order, so a raw modulo would march bodies through the
    /// atlas in lockstep with the roster and put identical warriors at a fixed
    /// stride along the line. Consecutive identifiers must not produce
    /// consecutive cells.
    /// </summary>
    [Fact]
    public void ConsecutiveWarriorsDoNotWalkTheAtlasInOrder()
    {
        var consecutive = 0;

        for (ulong entityId = 0; entityId < 500; entityId++)
        {
            var current = PawnSpriteAtlas.GetVariantIndex(entityId);
            var next = PawnSpriteAtlas.GetVariantIndex(entityId + 1);

            if (next == (current + 1) % PawnSpriteAtlas.VariantCount)
            {
                consecutive++;
            }
        }

        // A uniform mixer lands on the next cell about 1/50 of the time. Ten
        // per cent of 500 is five times that, so this fails an in-order walk
        // outright while staying clear of ordinary chance.
        Assert.True(
            consecutive < 50,
            $"{consecutive} of 500 consecutive identifiers advanced by one " +
            "cell, which suggests selection is not being mixed.");
    }

    /// <summary>
    /// Every authored cell must be reachable. A mixer that never selects some
    /// of them would waste authoring effort silently — the art would be in the
    /// atlas and simply never drawn.
    /// </summary>
    [Fact]
    public void EveryAuthoredCellIsReachable()
    {
        var used = new bool[PawnSpriteAtlas.VariantCount];

        for (ulong entityId = 0; entityId < 5_000; entityId++)
        {
            used[PawnSpriteAtlas.GetVariantIndex(entityId)] = true;
        }

        Assert.DoesNotContain(false, used);
    }

    [Fact]
    public void TheDestinationSpansTheHeadAndTorsoTogether()
    {
        var head = new Rectangle(100, 40, 20, 24);
        var torso = new Rectangle(96, 64, 28, 46);

        var destination = PawnSpriteAtlas.GetDestinationBounds(head, torso);

        Assert.Equal(head.Top, destination.Top);
        Assert.Equal(torso.Bottom, destination.Bottom);
    }

    /// <summary>
    /// The cell keeps its authored aspect instead of being stretched to the
    /// union's width, which is what stops a broad warrior smearing and a
    /// slight one squashing (design section 10).
    /// </summary>
    [Fact]
    public void TheDestinationKeepsTheAuthoredAspectRegardlessOfBuild()
    {
        var head = new Rectangle(100, 40, 20, 24);
        var slight = PawnSpriteAtlas.GetDestinationBounds(
            head,
            new Rectangle(104, 64, 12, 46));
        var broad = PawnSpriteAtlas.GetDestinationBounds(
            head,
            new Rectangle(90, 64, 40, 46));

        Assert.Equal(slight.Width, broad.Width);
        Assert.Equal(slight.Height, broad.Height);

        var expectedWidth = (int)MathF.Round(
            slight.Height *
            ((float)PawnSpriteAtlas.CellWidth / PawnSpriteAtlas.CellHeight));
        Assert.Equal(expectedWidth, slight.Width);
    }

    [Fact]
    public void TheDestinationCentresOnTheTorso()
    {
        var destination = PawnSpriteAtlas.GetDestinationBounds(
            new Rectangle(100, 40, 20, 24),
            new Rectangle(96, 64, 28, 46));

        // Integer division in the centring step can leave the drawn cell half
        // a pixel left of the torso's centre; anything wider than that is a
        // real misalignment.
        Assert.InRange(destination.Center.X, 109, 110);
    }

    [Fact]
    public void AnEmptyLayoutDrawsNothing()
    {
        Assert.Equal(
            Rectangle.Empty,
            PawnSpriteAtlas.GetDestinationBounds(
                Rectangle.Empty,
                Rectangle.Empty));
    }
}
