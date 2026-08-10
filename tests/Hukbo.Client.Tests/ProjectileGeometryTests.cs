using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// The in-flight projectile silhouettes
/// (docs/archives/2026-08-11/2026-08-11-projectile-props.md, task A2).
/// Pure geometry, so
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
    [InlineData(BodyPart.WeaponArm)]
    [InlineData(BodyPart.ShieldArm)]
    [InlineData(BodyPart.Shoulder)]
    [InlineData(BodyPart.Head)]
    [InlineData(BodyPart.Neck)]
    [InlineData(BodyPart.Face)]
    [InlineData(BodyPart.Chest)]
    [InlineData(BodyPart.Abdomen)]
    [InlineData(BodyPart.Thigh)]
    [InlineData(BodyPart.Knee)]
    [InlineData(BodyPart.Shin)]
    [InlineData(BodyPart.Hands)]
    [InlineData(BodyPart.Feet)]
    public void CreateEmbedded_AnchorsEveryBodyPartInsideTheHostsOwnVisualBounds(
        BodyPart hitLocation)
    {
        var layout = HostLayout();

        var prop = ProjectileGeometry.CreateEmbedded(
            PawnWeaponRole.Busog,
            layout,
            hitLocation,
            onShield: false,
            seed: 12_345UL);

        // Every one of the thirteen resolves, and none of them lands off the
        // warrior it is supposed to be standing in. A body part with no
        // rectangle of its own at this tier falls back to the torso rather
        // than to the origin, which is what an unhandled case would produce.
        Assert.True(
            layout.VisualBounds.Contains(
                (int)prop.Primary.Center.X,
                (int)prop.Primary.Center.Y),
            $"{hitLocation} anchored at {prop.Primary.Center}, outside the " +
                $"pawn's own visual bounds {layout.VisualBounds}.");
    }

    [Fact]
    public void CreateEmbedded_AnchorsAShieldBlockOnTheShield()
    {
        var layout = HostLayout(ShieldId.TallHardwood);
        Assert.False(layout.ShieldBounds.IsEmpty);

        var prop = ProjectileGeometry.CreateEmbedded(
            PawnWeaponRole.Busog,
            layout,
            // A shield block carries a hit location too. The shield flag has
            // to win, or the arrow stands in the warrior instead of the board.
            hitLocation: null,
            onShield: true,
            seed: 999UL);

        Assert.True(
            layout.ShieldBounds.Contains(
                (int)prop.Primary.Center.X,
                (int)prop.Primary.Center.Y),
            $"A shield block anchored at {prop.Primary.Center}, outside the " +
                $"shield {layout.ShieldBounds}.");
    }

    [Fact]
    public void CreateEmbedded_FallsBackToTheTorsoForAShieldBlockOnAnUnshieldedPawn()
    {
        var layout = HostLayout(ShieldId.None);
        Assert.True(layout.ShieldBounds.IsEmpty);

        var prop = ProjectileGeometry.CreateEmbedded(
            PawnWeaponRole.Busog,
            layout,
            hitLocation: null,
            onShield: true,
            seed: 7UL);

        Assert.True(
            layout.VisualBounds.Contains(
                (int)prop.Primary.Center.X,
                (int)prop.Primary.Center.Y));
    }

    [Fact]
    public void CreateEmbedded_DrawsTheSameProjectileTheSameWayEveryTime()
    {
        var layout = HostLayout();
        var seed = ProjectileGeometry.CreateEmbeddedSeed(
            sequence: 42,
            hostEntityId: 7,
            attackerEntityId: 11);

        var first = ProjectileGeometry.CreateEmbedded(
            PawnWeaponRole.Busog,
            layout,
            BodyPart.Chest,
            onShield: false,
            seed);
        var second = ProjectileGeometry.CreateEmbedded(
            PawnWeaponRole.Busog,
            layout,
            BodyPart.Chest,
            onShield: false,
            seed);

        // Stable per instance rather than per frame: a stuck arrow that
        // re-rolled its angle every frame would strobe.
        Assert.Equal(first, second);
    }

    [Fact]
    public void CreateEmbeddedSeed_SeparatesTwoHitsThatDifferOnlyBySequence()
    {
        var first = ProjectileGeometry.CreateEmbeddedSeed(1, hostEntityId: 7, attackerEntityId: 11);
        var second = ProjectileGeometry.CreateEmbeddedSeed(2, hostEntityId: 7, attackerEntityId: 11);

        // Two arrows in the same warrior from the same archer must not stack
        // into one silhouette.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void CreateEmbedded_SpreadsAnglesAcrossManyHitsRatherThanDrawingThemParallel()
    {
        var layout = HostLayout();

        var angles = Enumerable
            .Range(0, 32)
            .Select(sequence => ProjectileGeometry.CreateEmbedded(
                PawnWeaponRole.Busog,
                layout,
                BodyPart.Chest,
                onShield: false,
                ProjectileGeometry.CreateEmbeddedSeed(
                    sequence,
                    hostEntityId: 7,
                    attackerEntityId: 11)).RotationRadians)
            .Distinct()
            .Count();

        Assert.True(
            angles > 8,
            $"32 hits produced only {angles} distinct angles; a warrior full " +
                "of parallel arrows reads as a decal rather than as arrows.");
    }

    [Fact]
    public void CreateEmbedded_StandsAnArrowOutFletchFirstAndASpearOutButtFirst()
    {
        var layout = HostLayout();
        const ulong Seed = 4UL;

        var arrow = ProjectileGeometry.CreateEmbedded(
            PawnWeaponRole.Busog,
            layout,
            BodyPart.Chest,
            onShield: false,
            Seed);
        var spear = ProjectileGeometry.CreateEmbedded(
            PawnWeaponRole.Bangkaw,
            layout,
            BodyPart.Chest,
            onShield: false,
            Seed);

        // The end inside the target is the pointed one, so what a spectator
        // sees standing out is the tail in both cases.
        Assert.Equal(ProjectilePropElementKind.Fletch, arrow.Secondary.Kind);
        Assert.Equal(ProjectilePropElementKind.Head, spear.Secondary.Kind);
        Assert.True(spear.Primary.Length > arrow.Primary.Length);
    }

    [Theory]
    [InlineData(PawnWeaponRole.Arquebus)]
    [InlineData(PawnWeaponRole.Kampilan)]
    public void CreateEmbedded_RejectsAWeaponThatLeavesNothingBehind(PawnWeaponRole role)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProjectileGeometry.CreateEmbedded(
                role,
                HostLayout(),
                BodyPart.Chest,
                onShield: false,
                seed: 1UL));
    }

    [Fact]
    public void CreateEmbedded_NeverExceedsTheBudgetedTwoQuadsPerSlot()
    {
        var prop = ProjectileGeometry.CreateEmbedded(
            PawnWeaponRole.Busog,
            HostLayout(),
            BodyPart.Chest,
            onShield: false,
            seed: 3UL);

        Assert.True(
            prop.ElementCount <=
                RenderBudgetEstimate.EmbeddedProjectileQuadsPerProjectile,
            $"An embedded projectile drew {prop.ElementCount} quads against a " +
                "budget term of " +
                $"{RenderBudgetEstimate.EmbeddedProjectileQuadsPerProjectile}.");
    }

    /// <summary>
    /// A High-tier host, so every optional rectangle an anchor might want —
    /// the legs, the feet, the arms — is actually present.
    /// </summary>
    private static PawnLayout HostLayout(ShieldId shield = ShieldId.TallHardwood)
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Kampilan, shield);
        var layout = PawnGeometry.Create(new Vector2(400f, 300f), 3f, appearance);
        Assert.Equal(PawnDetailTier.High, layout.DetailTier);
        return layout;
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
