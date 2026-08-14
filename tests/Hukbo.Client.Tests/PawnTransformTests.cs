using Hukbo.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

/// <summary>
/// Covers <see cref="PawnTransform"/>: the rigid plane transform every quad on
/// a pawn is carried by (the 2026-08-14 death-collapse design, section 4). Pure
/// value arithmetic, so nothing here constructs a graphics device, a sprite
/// batch, or a window.
/// </summary>
public sealed class PawnTransformTests
{
    /// <summary>
    /// Float comparison tolerance. Generous by the standards of exact
    /// arithmetic and tight by the standards of a screen: a tenth of a pixel is
    /// two orders of magnitude below anything a spectator can see, and the
    /// rotations below run through a sine and a cosine.
    /// </summary>
    private const float Tolerance = 1e-3f;

    [Fact]
    public void Identity_MovesNothing()
    {
        var transform = PawnTransform.Identity;

        Assert.True(transform.IsIdentity);
        Assert.Equal(new Vector2(17f, -4f), transform.Apply(new Vector2(17f, -4f)));
    }

    /// <summary>
    /// The property the renderer's axis-aligned fast path rests on: a zero
    /// angle is not "a rotation of zero", it is the identity value, so
    /// <c>DrawQuad</c> takes the plain overload and a living pawn's pixels are
    /// unchanged rather than merely close.
    /// </summary>
    [Fact]
    public void AboutPivot_IsExactlyTheIdentityForAZeroAngle()
    {
        Assert.True(PawnTransform.AboutPivot(new Vector2(200f, 350f), 0f).IsIdentity);
    }

    [Fact]
    public void AboutPivot_LeavesThePivotWhereItIs()
    {
        var pivot = new Vector2(120f, -37f);

        var moved = PawnTransform
            .AboutPivot(pivot, MathF.PI / 2f)
            .Apply(pivot);

        Assert.Equal(pivot.X, moved.X, Tolerance);
        Assert.Equal(pivot.Y, moved.Y, Tolerance);
    }

    /// <summary>
    /// A quarter turn about the origin in MonoGame's screen axes — X right, Y
    /// down — takes the point one unit to the right onto the point one unit
    /// below. This is the sense of rotation everything else here inherits.
    /// </summary>
    [Fact]
    public void AboutPivot_TurnsAQuarterTurnInScreenAxes()
    {
        var turned = PawnTransform
            .AboutPivot(Vector2.Zero, MathF.PI / 2f)
            .Apply(new Vector2(1f, 0f));

        Assert.Equal(0f, turned.X, Tolerance);
        Assert.Equal(1f, turned.Y, Tolerance);
    }

    /// <summary>
    /// The whole reason a body lies on the ground rather than leaning: a head
    /// standing a body's height above the foot anchor ends a body's length
    /// sideways from it, at the anchor's own height — which is the ground.
    /// </summary>
    [Fact]
    public void AQuarterTurnAboutTheFootAnchorPutsTheHeadOnTheGroundPlane()
    {
        var footAnchor = new Vector2(400f, 300f);
        var head = footAnchor + new Vector2(0f, -40f);

        var fallen = PawnTransform
            .AboutPivot(footAnchor, MathF.PI / 2f)
            .Apply(head);

        Assert.Equal(footAnchor.X + 40f, fallen.X, Tolerance);
        Assert.Equal(footAnchor.Y, fallen.Y, Tolerance);
    }

    [Fact]
    public void Then_ComposingWithTheIdentityInEitherPositionReturnsTheOther()
    {
        var rotation = PawnTransform.AboutPivot(new Vector2(5f, 9f), 0.4f);

        Assert.Equal(rotation, rotation.Then(PawnTransform.Identity));
        Assert.Equal(rotation, PawnTransform.Identity.Then(rotation));
    }

    /// <summary>
    /// The composition the shield needs: its own posture rotation about its
    /// block centre, then the collapse about the foot anchor. One transform
    /// must do what the two do in sequence, because
    /// <c>SpriteBatch.Draw</c> takes one angle and one position.
    /// </summary>
    [Theory]
    [InlineData(0.35f, 1.57f)]
    [InlineData(-0.35f, -1.57f)]
    [InlineData(1.2f, 0.05f)]
    public void Then_AgreesWithApplyingBothRotationsInOrder(
        float innerRadians,
        float outerRadians)
    {
        var innerPivot = new Vector2(210f, 140f);
        var outerPivot = new Vector2(200f, 300f);
        var inner = PawnTransform.AboutPivot(innerPivot, innerRadians);
        var outer = PawnTransform.AboutPivot(outerPivot, outerRadians);
        var point = new Vector2(233f, 118f);

        var composed = inner.Then(outer).Apply(point);
        var sequential = outer.Apply(inner.Apply(point));

        Assert.Equal(sequential.X, composed.X, Tolerance);
        Assert.Equal(sequential.Y, composed.Y, Tolerance);
    }

    [Fact]
    public void Then_AddsTheTwoAngles()
    {
        var composed = PawnTransform
            .AboutPivot(new Vector2(3f, 4f), 0.5f)
            .Then(PawnTransform.AboutPivot(new Vector2(70f, 12f), 0.25f));

        Assert.Equal(0.75f, composed.Radians, Tolerance);
    }

    /// <summary>
    /// A rigid transform preserves distance, which is what lets
    /// <c>DrawLine</c> transform both endpoints and then recompute the stroke's
    /// own length and angle from them without knowing anything was moved.
    /// </summary>
    [Fact]
    public void Apply_PreservesTheDistanceBetweenTwoPoints()
    {
        var transform = PawnTransform.AboutPivot(new Vector2(-8f, 60f), 2.1f);
        var from = new Vector2(14f, 5f);
        var to = new Vector2(90f, -33f);

        Assert.Equal(
            Vector2.Distance(from, to),
            Vector2.Distance(transform.Apply(from), transform.Apply(to)),
            Tolerance);
    }
}
