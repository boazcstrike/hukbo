using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Covers the cull rectangle a corpse is tested against (the 2026-08-14
/// death-collapse design, section 5). The claim being pinned is containment: a
/// body rotated by any angle about its foot anchor stays inside the envelope,
/// so a corpse near the arena panel's edge is never culled while part of it is
/// still on screen.
/// </summary>
public sealed class ProneEnvelopeTests
{
    /// <summary>
    /// Every rotation, not only the quarter turn. The envelope has to cover the
    /// whole collapse as well as its resting pose, and it has to do so without
    /// depending on the fall direction or the per-warrior jitter — which is
    /// exactly what "contains it at every angle" means.
    /// </summary>
    [Theory]
    [InlineData(0, 0, 200f, 300f)]
    [InlineData(-6, -40, 400f, 260f)]
    [InlineData(-14, -52, 0f, 0f)]
    [InlineData(3, -9, -120f, 640f)]
    public void ProneEnvelope_ContainsTheStandingBoundsAtEveryRotation(
        int offsetX,
        int offsetY,
        float anchorX,
        float anchorY)
    {
        var footAnchor = new Vector2(anchorX, anchorY);
        var standing = new Rectangle(
            (int)anchorX + offsetX,
            (int)anchorY + offsetY,
            14,
            48);
        var envelope = PawnGeometry.CreateProneEnvelope(standing, footAnchor);

        for (var step = 0; step < 180; step++)
        {
            var transform = PawnTransform.AboutPivot(
                footAnchor,
                MathF.Tau * (step / 180f));

            foreach (var corner in Corners(standing))
            {
                var moved = transform.Apply(corner);
                Assert.True(
                    moved.X >= envelope.Left - 1f &&
                    moved.X <= envelope.Right + 1f &&
                    moved.Y >= envelope.Top - 1f &&
                    moved.Y <= envelope.Bottom + 1f,
                    $"step {step}: {moved} escaped {envelope}");
            }
        }
    }

    /// <summary>
    /// It is a superset, never a replacement. A living pawn is still culled
    /// against the standing rectangle, and nothing that rectangle admits may be
    /// lost by widening it for a corpse.
    /// </summary>
    [Fact]
    public void ProneEnvelope_ContainsTheStandingBoundsItself()
    {
        var footAnchor = new Vector2(320f, 240f);
        var standing = new Rectangle(313, 190, 15, 51);

        var envelope = PawnGeometry.CreateProneEnvelope(standing, footAnchor);

        Assert.Equal(envelope, Rectangle.Union(envelope, standing));
    }

    /// <summary>
    /// The envelope does not depend on the collapse clock, so the drawn set
    /// still does not vary with animation phase — the property the pose-blind
    /// cull exists to protect. Nothing about this value can change while a body
    /// is falling.
    /// </summary>
    [Fact]
    public void ProneEnvelope_IsAPureFunctionOfTheStandingBoundsAndTheAnchor()
    {
        var footAnchor = new Vector2(88f, 512f);
        var standing = new Rectangle(80, 460, 16, 52);

        Assert.Equal(
            PawnGeometry.CreateProneEnvelope(standing, footAnchor),
            PawnGeometry.CreateProneEnvelope(standing, footAnchor));
    }

    /// <summary>
    /// The same claim against a real appearance rather than a hand-built
    /// rectangle: whatever the catalogs produce, the fallen body of that
    /// warrior stays inside the rectangle the draw loop culls it against.
    /// </summary>
    [Theory]
    [InlineData(WeaponId.Kalis, ShieldId.TallHardwood)]
    [InlineData(WeaponId.Kampilan, ShieldId.None)]
    [InlineData(WeaponId.Busog, ShieldId.None)]
    [InlineData(WeaponId.Wasay, ShieldId.TallHardwood)]
    public void ProneEnvelope_ContainsARealPawnLaidOnItsSide(
        WeaponId weapon,
        ShieldId shield)
    {
        var footAnchor = new Vector2(500f, 400f);
        var appearance = PawnAppearanceFactory.Create(7, weapon, shield);
        var prefix = PawnGeometry.PoseBlindPrefix.Create(
            footAnchor,
            cameraZoom: 3f,
            appearance);
        var envelope = prefix.ProneEnvelopeVisualBounds;
        var fallen = PawnGeometry.Create(
            footAnchor,
            cameraZoom: 3f,
            appearance,
            collapseRotationRadians: CollapsePose.ResolveFinalRotation(true, 7));

        foreach (var corner in Corners(fallen.VisualBounds))
        {
            var moved = fallen.Collapse.Apply(corner);
            Assert.True(
                moved.X >= envelope.Left - 1f &&
                moved.X <= envelope.Right + 1f &&
                moved.Y >= envelope.Top - 1f &&
                moved.Y <= envelope.Bottom + 1f,
                $"{weapon}/{shield}: {moved} escaped {envelope}");
        }
    }

    private static Vector2[] Corners(Rectangle bounds) =>
    [
        new(bounds.Left, bounds.Top),
        new(bounds.Right, bounds.Top),
        new(bounds.Left, bounds.Bottom),
        new(bounds.Right, bounds.Bottom),
    ];
}
