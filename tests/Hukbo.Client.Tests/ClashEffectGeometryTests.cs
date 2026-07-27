using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

/// <summary>
/// One case, RED against the Phase 0 stub, which reports an empty layout so
/// nothing is drawn.
/// </summary>
public sealed class ClashEffectGeometryTests
{
    /// <summary>
    /// RED. The cross has to grow with the camera and stop growing at the
    /// same clamped apparent scale every other pawn-sized thing uses, or it
    /// reads as a full-screen flash at high zoom and vanishes at low.
    /// </summary>
    [Fact]
    public void Create_ScalesTheCrossWithZoomAndStaysInsideItsBounds()
    {
        var effect = Effect(AttackResolution.Parried, ageSeconds: 0f);

        var minimum = ClashEffectGeometry.Create(effect, cameraZoom: 0.01f);
        var low = ClashEffectGeometry.Create(effect, cameraZoom: 0.6f);
        var medium = ClashEffectGeometry.Create(effect, cameraZoom: 1f);
        var high = ClashEffectGeometry.Create(effect, cameraZoom: 2f);
        var maximum = ClashEffectGeometry.Create(effect, cameraZoom: 12f);
        var aboveMaximum = ClashEffectGeometry.Create(effect, cameraZoom: 48f);

        Assert.True(
            high.ArmLength > low.ArmLength,
            $"High zoom gave {high.ArmLength} against {low.ArmLength}.");
        Assert.True(medium.ArmLength >= low.ArmLength);
        Assert.True(maximum.ArmLength >= high.ArmLength);
        Assert.Equal(maximum.ApparentScale, aboveMaximum.ApparentScale);
        Assert.Equal(maximum.ArmLength, aboveMaximum.ArmLength);
        Assert.True(
            minimum.ApparentScale > 0f,
            $"A near-zero zoom gave a scale of {minimum.ApparentScale}.");
        Assert.True(minimum.ApparentScale <= maximum.ApparentScale);
        Assert.True(minimum.ArmThickness >= 1f);
        Assert.True(maximum.ArmThickness >= minimum.ArmThickness);

        foreach (var zoom in new[] { 0.01f, 0.6f, 1f, 2f, 12f, 48f })
        {
            foreach (var age in new[] { 0f, 0.07f, 0.139f })
            {
                var layout = ClashEffectGeometry.Create(
                    Effect(AttackResolution.ShieldBlocked, age),
                    zoom);

                Assert.InRange(layout.Progress, 0f, 1f);
                Assert.InRange(layout.Alpha, 0f, 1f);
                Assert.True(layout.ArmLength > 0f);
            }
        }

        var fresh = ClashEffectGeometry.Create(
            Effect(AttackResolution.Parried, ageSeconds: 0f),
            cameraZoom: 1f);
        var spent = ClashEffectGeometry.Create(
            Effect(AttackResolution.Parried, ageSeconds: 0.139f),
            cameraZoom: 1f);

        Assert.True(
            spent.Alpha < fresh.Alpha,
            $"An aged cross held {spent.Alpha} against a fresh {fresh.Alpha}.");
    }

    private static ClashEffect Effect(
        AttackResolution resolution,
        float ageSeconds) =>
        new(
            Sequence: 1,
            AttackerEntityId: 2,
            TargetEntityId: 7,
            XRaw: 200,
            YRaw: 300,
            resolution,
            ageSeconds);
}
