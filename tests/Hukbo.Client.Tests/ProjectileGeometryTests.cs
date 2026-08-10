using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// The in-flight projectile silhouettes
/// (docs/plans/2026-08-11-projectile-props.md, task A2). Pure geometry, so
/// every assertion here runs without a graphics device, a sprite batch, or a
/// window.
/// </summary>
public sealed class ProjectileGeometryTests
{
    private static readonly Vector2 Anywhere = new(400f, 300f);
    private static readonly Vector2 East = new(1f, 0f);

    [Theory]
    [InlineData(PawnWeaponRole.Bangkaw)]
    [InlineData(PawnWeaponRole.Busog)]
    [InlineData(PawnWeaponRole.Arquebus)]
    public void Create_CentresThePropOnTheCurrentPositionRatherThanTrailingBehindIt(
        PawnWeaponRole role)
    {
        var layout = ProjectileGeometry.Create(role, Anywhere, East, cameraZoom: 1f);

        // The whole defect projectile-props exists to fix: RU-25 anchored the
        // quad at the launch point, so the drawn thing grew out of the thrower
        // and was longest at impact. The primary element must sit on the shot.
        Assert.Equal(Anywhere, layout.Primary.Center);
    }

    [Fact]
    public void Create_GivesTheThreeRangedWeaponsMutuallyDistinctSilhouettes()
    {
        var spear = ProjectileGeometry.Create(
            PawnWeaponRole.Bangkaw,
            Anywhere,
            East,
            cameraZoom: 1f);
        var arrow = ProjectileGeometry.Create(
            PawnWeaponRole.Busog,
            Anywhere,
            East,
            cameraZoom: 1f);
        var ball = ProjectileGeometry.Create(
            PawnWeaponRole.Arquebus,
            Anywhere,
            East,
            cameraZoom: 1f);

        // A spectator has to be able to tell the three apart at a glance, and
        // the only channels available are length and element count.
        Assert.True(
            spear.Primary.Length > arrow.Primary.Length,
            $"A spear ({spear.Primary.Length}) must draw longer than an " +
                $"arrow ({arrow.Primary.Length}).");
        Assert.True(
            arrow.Primary.Length > ball.Primary.Length,
            $"An arrow ({arrow.Primary.Length}) must draw longer than a " +
                $"ball ({ball.Primary.Length}).");

        Assert.Equal(ProjectilePropElementKind.Head, spear.Secondary.Kind);
        Assert.Equal(ProjectilePropElementKind.Fletch, arrow.Secondary.Kind);
        Assert.Equal(ProjectilePropElementKind.Ball, ball.Primary.Kind);
    }

    [Fact]
    public void Create_PutsASpearHeadInFrontAndAnArrowFletchBehind()
    {
        var spear = ProjectileGeometry.Create(
            PawnWeaponRole.Bangkaw,
            Anywhere,
            East,
            cameraZoom: 1f);
        var arrow = ProjectileGeometry.Create(
            PawnWeaponRole.Busog,
            Anywhere,
            East,
            cameraZoom: 1f);

        // Travelling east, "in front" is a greater x than the shot's own.
        Assert.True(
            spear.Secondary.Center.X > Anywhere.X,
            "A spear's head is at the end that arrives first.");
        Assert.True(
            arrow.Secondary.Center.X < Anywhere.X,
            "An arrow's fletching is at the end that arrives last.");
    }

    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(0f, 1f)]
    [InlineData(-1f, 0f)]
    [InlineData(0f, -1f)]
    [InlineData(1f, 1f)]
    [InlineData(-1f, 1f)]
    public void Create_RotatesToTheDirectionOfTravel(float directionX, float directionY)
    {
        var direction = new Vector2(directionX, directionY);

        var layout = ProjectileGeometry.Create(
            PawnWeaponRole.Busog,
            Anywhere,
            direction,
            cameraZoom: 1f);

        Assert.Equal(
            MathF.Atan2(directionY, directionX),
            layout.RotationRadians,
            precision: 5);
    }

    [Fact]
    public void Create_DrawsAnUnrotatedPropForAShotThatResolvedNoTarget()
    {
        // A Release with no resolved target puts the destination on the origin,
        // so the shot never moves and has no direction to face. It still left
        // the bow, so it still draws — RU-25's own reasoning, unchanged.
        var layout = ProjectileGeometry.Create(
            PawnWeaponRole.Busog,
            Anywhere,
            Vector2.Zero,
            cameraZoom: 1f);

        Assert.Equal(0f, layout.RotationRadians);
        Assert.False(layout.Primary.IsEmpty);
    }

    [Theory]
    [InlineData(PawnWeaponRole.Bangkaw, 2)]
    [InlineData(PawnWeaponRole.Busog, 2)]
    [InlineData(PawnWeaponRole.Arquebus, 1)]
    public void Create_NeverExceedsTheBudgetedTwoQuadsPerFlight(
        PawnWeaponRole role,
        int expectedElementCount)
    {
        var layout = ProjectileGeometry.Create(role, Anywhere, East, cameraZoom: 1f);

        Assert.Equal(expectedElementCount, layout.ElementCount);
        Assert.True(
            layout.ElementCount <= RenderBudgetEstimate.ProjectileQuadsPerProjectile,
            $"{role} draws {layout.ElementCount} quads against a budget term " +
                $"of {RenderBudgetEstimate.ProjectileQuadsPerProjectile}.");
    }

    [Theory]
    [InlineData(0.01f)]
    [InlineData(0.2f)]
    public void Create_KeepsAProjectileVisibleAtAPulledOutCamera(float cameraZoom)
    {
        var layout = ProjectileGeometry.Create(
            PawnWeaponRole.Busog,
            Anywhere,
            East,
            cameraZoom);

        // Floored rather than allowed to reach zero: the in-flight prop is
        // never detail-gated, because at low detail it may be the only thing
        // telling a spectator a ranged unit exists. A prop that scaled to
        // nothing would gate it in every way but name.
        Assert.False(layout.Primary.IsEmpty);
        Assert.True(layout.Primary.Length >= 1f);
        Assert.True(layout.Primary.Thickness >= 1f);
    }

    [Fact]
    public void Create_ScalesWithTheCamera()
    {
        var near = ProjectileGeometry.Create(
            PawnWeaponRole.Bangkaw,
            Anywhere,
            East,
            cameraZoom: 3f);
        var far = ProjectileGeometry.Create(
            PawnWeaponRole.Bangkaw,
            Anywhere,
            East,
            cameraZoom: 1f);

        Assert.True(near.Primary.Length > far.Primary.Length);
    }

    [Theory]
    [InlineData(PawnWeaponRole.Kampilan)]
    [InlineData(PawnWeaponRole.Wasay)]
    [InlineData(PawnWeaponRole.Kalis)]
    [InlineData(PawnWeaponRole.Itak)]
    public void Create_RejectsAMeleeRoleRatherThanDrawingSomethingArbitrary(
        PawnWeaponRole role)
    {
        // Unreachable in a real battle — a melee weapon emits no Release — but
        // exhaustive rather than falling through, matching
        // PawnAppearanceFactory.ToWeaponRole. A silent fall-through here is how
        // the 2026-08-09 Arquebus crashes got to be four separate crashes.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProjectileGeometry.Create(role, Anywhere, East, cameraZoom: 1f));
    }
}
