using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Combat;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Tests;

public sealed class AttackPoseResolverTests
{
    public static TheoryData<float, float> TargetHeadings =>
        new()
        {
            { 1f, 0f },
            { 1f, 1f },
            { 0f, 1f },
            { -1f, 1f },
            { -1f, 0f },
            { -1f, -1f },
            { 0f, -1f },
            { 1f, -1f },
        };

    [Theory]
    [MemberData(nameof(TargetHeadings))]
    public void Resolve_PointsWeaponTowardTargetAtEveryHeading(
        float directionX,
        float directionY)
    {
        var animation = AttackGeometryTests.Animation(
            WeaponId.Kampilan,
            directionX: directionX,
            directionY: directionY);

        var pose = AttackPoseResolver.Resolve(animation);

        AssertFinite(pose.WeaponTip);
    }

    [Fact]
    public void Resolve_PutsLegalShieldOverlayOnOffHand()
    {
        var pose = AttackPoseResolver.Resolve(
            AttackGeometryTests.Animation(
                WeaponId.Kalis,
                shield: ShieldId.TallHardwood,
                directionX: -0.8f,
                directionY: 0.6f));

        Assert.True(pose.HasShield);
        Assert.False(pose.HasSupportHand);
    }

    [Fact]
    public void Resolve_SuppressesIllegalTwoHandedShieldOverlay()
    {
        var pose = AttackPoseResolver.Resolve(
            AttackGeometryTests.Animation(
                WeaponId.Wasay,
                shield: ShieldId.TallHardwood));

        Assert.False(pose.HasShield);
        Assert.True(pose.HasSupportHand);
    }

    [Fact]
    public void Resolve_IsAllocationFreeAfterWarmup()
    {
        var animation = AttackGeometryTests.Animation(
            WeaponId.Kalis,
            shield: ShieldId.TallHardwood,
            directionX: 0.6f,
            directionY: 0.8f);
        _ = AttackPoseResolver.Resolve(animation);
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 10_000; index++)
        {
            _ = AttackPoseResolver.Resolve(animation);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    private static void AssertFinite(Vector2 value)
    {
        Assert.True(float.IsFinite(value.X));
        Assert.True(float.IsFinite(value.Y));
    }
}
