using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

/// <summary>
/// Task 10. The spectator's motion setting reaches attack resolution, and it
/// may only remove motion that carries no combat meaning.
/// </summary>
public sealed class AttackRenderPolicyTests
{
    [Fact]
    public void ResolveMotionPolicy_RejectsAnUnknownIntensity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AttackGeometry.ResolveMotionPolicy((MotionIntensity)99));
    }

    /// <summary>
    /// Full keeps everything, Reduced keeps less of it, and Off removes the
    /// trail entirely while still leaning enough to read as a strike.
    /// </summary>
    [Fact]
    public void ResolveMotionPolicy_OrdersTheThreeSettings()
    {
        var full = AttackGeometry.ResolveMotionPolicy(MotionIntensity.Full);
        var reduced = AttackGeometry.ResolveMotionPolicy(MotionIntensity.Reduced);
        var off = AttackGeometry.ResolveMotionPolicy(MotionIntensity.Off);

        Assert.Equal(1f, full.BodyScale);
        Assert.Equal(1f, full.TrailScale);
        Assert.True(reduced.BodyScale < full.BodyScale);
        Assert.True(reduced.TrailScale < full.TrailScale);
        Assert.True(off.BodyScale < reduced.BodyScale);
        Assert.True(off.BodyScale > 0f);
        Assert.Equal(0f, off.TrailScale);
    }

    /// <summary>
    /// The accessibility guarantee, stated as the property the design states:
    /// no setting may hide the direction a blow was aimed, how far it reached,
    /// or which of the five outcomes resolved it. Only the body exaggeration
    /// and the trail are allowed to move.
    /// </summary>
    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    public void MotionIntensity_NeverChangesDirectionReachOrOutcome(WeaponId weapon)
    {
        foreach (var resolution in Enum.GetValues<AttackResolution>())
        {
            var full = Evaluate(weapon, resolution, MotionIntensity.Full);
            var reduced = Evaluate(weapon, resolution, MotionIntensity.Reduced);
            var off = Evaluate(weapon, resolution, MotionIntensity.Off);

            foreach (var sample in new[] { reduced, off })
            {
                Assert.Equal(full.WeaponAngleRadians, sample.WeaponAngleRadians);
                Assert.Equal(full.WeaponReach, sample.WeaponReach);
                Assert.Equal(full.WeaponLateralOffset, sample.WeaponLateralOffset);
                Assert.Equal(full.StanceWeight, sample.StanceWeight);
            }
        }
    }

    /// <summary>
    /// What the setting does change, it changes monotonically.
    /// </summary>
    [Fact]
    public void MotionIntensity_DampensBodyMotionAndRemovesTheTrailWhenOff()
    {
        var full = Evaluate(WeaponId.Kampilan, AttackResolution.Landed, MotionIntensity.Full);
        var reduced = Evaluate(WeaponId.Kampilan, AttackResolution.Landed, MotionIntensity.Reduced);
        var off = Evaluate(WeaponId.Kampilan, AttackResolution.Landed, MotionIntensity.Off);

        Assert.True(full.TorsoForwardOffset > reduced.TorsoForwardOffset);
        Assert.True(reduced.TorsoForwardOffset > off.TorsoForwardOffset);
        Assert.True(off.TorsoForwardOffset > 0f);

        Assert.True(full.TrailStrength > reduced.TrailStrength);
        Assert.True(reduced.TrailStrength > 0f);
        Assert.Equal(0f, off.TrailStrength);
    }

    /// <summary>
    /// A motion setting is an accessibility preference, not a persisted
    /// contract change: the three numeric values are what a saved settings
    /// file already holds.
    /// </summary>
    [Fact]
    public void MotionIntensity_KeepsItsPersistedNumericValues()
    {
        Assert.Equal(0, (int)MotionIntensity.Off);
        Assert.Equal(1, (int)MotionIntensity.Reduced);
        Assert.Equal(2, (int)MotionIntensity.Full);
    }

    private static AttackGeometrySample Evaluate(
        WeaponId weapon,
        AttackResolution resolution,
        MotionIntensity intensity)
    {
        var animation = AttackGeometryTests.Animation(weapon, resolution) with
        {
            MotionIntensity = intensity,
        };

        return AttackGeometry.Evaluate(animation);
    }
}
