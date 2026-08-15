using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// The weapon and shield sprite atlas's geometry and cell selection (the
/// 2026-08-15 weapon sprite design, sections 4 and 5). Every assertion here is
/// arithmetic on value types — no <c>GraphicsDevice</c>, no
/// <c>SpriteBatch</c>, no window, no GPU, no audio, no focus, no network, no
/// wall clock — per the client presentation test rules in CLAUDE.md section 5.
/// </summary>
public sealed class WeaponSpriteAtlasTests
{
    private static readonly PawnWeaponRole[] AllWeaponRoles =
    [
        PawnWeaponRole.Kampilan,
        PawnWeaponRole.Wasay,
        PawnWeaponRole.Kalis,
        PawnWeaponRole.Itak,
        PawnWeaponRole.Bangkaw,
        PawnWeaponRole.Busog,
        PawnWeaponRole.Arquebus,
    ];

    /// <summary>
    /// Pinned against literals rather than against the constants under test.
    /// A dimension read out of <c>Columns * CellWidth</c> would move with the
    /// thing it is meant to catch, so it would pass for an atlas whose grid
    /// silently changed shape underneath the shipped texture.
    /// </summary>
    [Fact]
    public void TheAtlasDimensionsMatchTheShippedTexture()
    {
        Assert.Equal(10, WeaponSpriteAtlas.Columns);
        Assert.Equal(8, WeaponSpriteAtlas.Rows);
        Assert.Equal(112, WeaponSpriteAtlas.CellWidth);
        Assert.Equal(256, WeaponSpriteAtlas.CellHeight);
        Assert.Equal(1120, WeaponSpriteAtlas.AtlasWidth);
        Assert.Equal(2048, WeaponSpriteAtlas.AtlasHeight);
    }

    /// <summary>
    /// The content box is the source rectangle the renderer must actually
    /// sample — smaller than the full cell on every side, by the gutter
    /// (design section 4).
    /// </summary>
    [Fact]
    public void TheWeaponContentBoxIsInsetFromTheCellByTheGutter()
    {
        Assert.Equal(4, WeaponSpriteAtlas.GutterPixels);
        Assert.Equal(104, WeaponSpriteAtlas.WeaponContentWidth);
        Assert.Equal(248, WeaponSpriteAtlas.WeaponContentHeight);
    }

    /// <summary>
    /// The shield row declares its own, narrower content box so the shield's
    /// authored 4:11 proportion is not stretched to the weapon-shaped box
    /// (design section 4).
    /// </summary>
    [Fact]
    public void TheShieldContentBoxIsNarrowerButTheSameHeight()
    {
        Assert.Equal(90, WeaponSpriteAtlas.ShieldContentWidth);
        Assert.Equal(248, WeaponSpriteAtlas.ShieldContentHeight);
        Assert.Equal(11, WeaponSpriteAtlas.ShieldHorizontalGutterPixels);
    }

    [Theory]
    [InlineData(PawnWeaponRole.Kampilan, 0)]
    [InlineData(PawnWeaponRole.Wasay, 1)]
    [InlineData(PawnWeaponRole.Kalis, 2)]
    [InlineData(PawnWeaponRole.Itak, 3)]
    [InlineData(PawnWeaponRole.Bangkaw, 4)]
    [InlineData(PawnWeaponRole.Busog, 5)]
    [InlineData(PawnWeaponRole.Arquebus, 6)]
    public void EveryWeaponRoleMapsToItsPinnedRow(PawnWeaponRole role, int expectedRow)
    {
        Assert.Equal(expectedRow, WeaponSpriteAtlas.GetRowForWeaponRole(role));
    }

    [Fact]
    public void TheShieldRoleMapsToTheEighthRow()
    {
        Assert.Equal(
            7, WeaponSpriteAtlas.GetRowForShieldRole(PawnShieldRole.TallHardwood));
    }

    [Fact]
    public void TheShieldNoneRoleHasNoRow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeaponSpriteAtlas.GetRowForShieldRole(PawnShieldRole.None));
    }

    /// <summary>
    /// Every one of the eighty authored content boxes — seventy weapon cells
    /// and ten shield cells — lies inside the atlas and does not overlap any
    /// other, including across role boundaries. The full cell (gutter
    /// included) is checked too, since a shape bug in the row math could
    /// place a whole cell out of bounds while its narrower content box still
    /// happened to land inside the atlas.
    /// </summary>
    [Fact]
    public void EveryContentBoxLiesInsideTheAtlasAndNoTwoOverlap()
    {
        var contentBoxes = new List<(string Label, Rectangle Box)>();
        var cellBoxes = new List<(string Label, Rectangle Box)>();

        foreach (var role in AllWeaponRoles)
        {
            for (var variant = 0; variant < 10; variant++)
            {
                var label = $"{role}[{variant}]";
                contentBoxes.Add((label, WeaponSpriteAtlas.GetSourceBounds(role, variant)));
                cellBoxes.Add((
                    label,
                    WeaponSpriteAtlas.GetCellBounds(
                        WeaponSpriteAtlas.GetRowForWeaponRole(role), variant)));
            }
        }

        for (var variant = 0; variant < 10; variant++)
        {
            var label = $"Shield[{variant}]";
            contentBoxes.Add((
                label,
                WeaponSpriteAtlas.GetShieldSourceBounds(
                    PawnShieldRole.TallHardwood, variant)));
            cellBoxes.Add((
                label,
                WeaponSpriteAtlas.GetCellBounds(WeaponSpriteAtlas.ShieldRow, variant)));
        }

        Assert.Equal(80, contentBoxes.Count);
        Assert.Equal(80, cellBoxes.Count);

        AssertInBoundsAndNonOverlapping(contentBoxes);
        AssertInBoundsAndNonOverlapping(cellBoxes);
    }

    private static void AssertInBoundsAndNonOverlapping(
        List<(string Label, Rectangle Box)> boxes)
    {
        for (var index = 0; index < boxes.Count; index++)
        {
            var (label, box) = boxes[index];

            Assert.True(box.Left >= 0, $"{label} starts left of the atlas.");
            Assert.True(box.Top >= 0, $"{label} starts above the atlas.");
            Assert.True(
                box.Right <= 1120,
                $"{label} extends past the atlas's 1120px width.");
            Assert.True(
                box.Bottom <= 2048,
                $"{label} extends past the atlas's 2048px height.");

            for (var other = 0; other < index; other++)
            {
                Assert.False(
                    box.Intersects(boxes[other].Box),
                    $"{label} overlaps {boxes[other].Label}.");
            }
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void AnOutOfRangeVariantThrowsRatherThanClamping(int variantIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeaponSpriteAtlas.GetSourceBounds(
                PawnWeaponRole.Kampilan, variantIndex));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeaponSpriteAtlas.GetShieldSourceBounds(
                PawnShieldRole.TallHardwood, variantIndex));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeaponSpriteAtlas.GetCellBounds(0, variantIndex));
    }

    [Fact]
    public void AnOutOfRangeRowThrowsRatherThanClamping()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeaponSpriteAtlas.GetCellBounds(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeaponSpriteAtlas.GetCellBounds(8, 0));
    }

    [Fact]
    public void TheSameWarriorAlwaysDrawsTheSameVariant()
    {
        for (ulong entityId = 0; entityId < 200; entityId++)
        {
            Assert.Equal(
                WeaponSpriteAtlas.GetVariantIndex(entityId),
                WeaponSpriteAtlas.GetVariantIndex(entityId));
        }
    }

    [Fact]
    public void EverySelectedVariantIsInRange()
    {
        for (ulong entityId = 0; entityId < 5_000; entityId++)
        {
            var index = WeaponSpriteAtlas.GetVariantIndex(entityId);

            Assert.InRange(index, 0, 9);
        }
    }

    /// <summary>
    /// Every authored variant must be reachable across a spread of entity
    /// identifiers. A mixer that never selects some of them would waste
    /// authoring effort silently — the art would be in the atlas and simply
    /// never drawn.
    /// </summary>
    [Fact]
    public void EveryVariantIsReachableAcrossASpreadOfEntityIds()
    {
        var used = new bool[10];

        for (ulong entityId = 0; entityId < 5_000; entityId++)
        {
            used[WeaponSpriteAtlas.GetVariantIndex(entityId)] = true;
        }

        Assert.DoesNotContain(false, used);
    }

    /// <summary>
    /// The reason selection mixes through <c>PresentationHash</c> instead of
    /// taking the identifier modulo the count: entity identifiers are handed
    /// out in roster order, so a raw modulo would march weapons through the
    /// atlas in lockstep with the roster and put visibly identical weapons at
    /// a fixed stride across the line. Consecutive identifiers must not
    /// produce consecutive variants.
    /// </summary>
    [Fact]
    public void ConsecutiveWarriorsDoNotWalkTheRowInOrder()
    {
        var consecutive = 0;

        for (ulong entityId = 0; entityId < 500; entityId++)
        {
            var current = WeaponSpriteAtlas.GetVariantIndex(entityId);
            var next = WeaponSpriteAtlas.GetVariantIndex(entityId + 1);

            if (next == (current + 1) % 10)
            {
                consecutive++;
            }
        }

        // A uniform mixer lands on the next variant about 1/10 of the time.
        // Fifty per cent of 500 is five times that, so this fails an in-order
        // walk outright while staying clear of ordinary chance.
        Assert.True(
            consecutive < 250,
            $"{consecutive} of 500 consecutive identifiers advanced by one " +
            "variant, which suggests selection is not being mixed.");
    }

    /// <summary>
    /// Worked example proving the rotation constant's sign, mirroring the
    /// derivation in <see cref="WeaponSpriteAtlas.WeaponSpriteRotationOffsetRadians"/>'s
    /// own doc comment: a weapon already pointing straight up on screen needs
    /// zero added rotation, because the cell is authored pointing up.
    /// </summary>
    [Fact]
    public void TheRotationOffsetLeavesAnAlreadyUpwardWeaponUnrotated()
    {
        var delta = new Vector2(0f, -100f);
        var lineAngle = MathF.Atan2(delta.Y, delta.X);

        var totalRotation = lineAngle + WeaponSpriteAtlas.WeaponSpriteRotationOffsetRadians;

        Assert.True(
            MathF.Abs(WrapToPi(totalRotation)) < 0.0001f,
            $"Expected zero rotation for an upward weapon; got {totalRotation}.");
    }

    /// <summary>
    /// A second worked example at a different angle, so the first case cannot
    /// pass by coincidence of both terms cancelling to zero: a weapon
    /// pointing straight right on screen must be rotated a quarter turn
    /// clockwise from its authored upward orientation.
    /// </summary>
    [Fact]
    public void TheRotationOffsetTurnsAnUpwardWeaponAQuarterTurnClockwiseToPointRight()
    {
        var delta = new Vector2(100f, 0f);
        var lineAngle = MathF.Atan2(delta.Y, delta.X);

        var totalRotation = lineAngle + WeaponSpriteAtlas.WeaponSpriteRotationOffsetRadians;

        Assert.True(
            MathF.Abs(WrapToPi(totalRotation) - (MathF.PI / 2f)) < 0.0001f,
            $"Expected a quarter turn clockwise (PI/2); got {totalRotation}.");
    }

    private static float WrapToPi(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= 2f * MathF.PI;
        }

        while (angle < -MathF.PI)
        {
            angle += 2f * MathF.PI;
        }

        return angle;
    }
}
