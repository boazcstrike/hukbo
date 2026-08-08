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
        var weaponForward = pose.WeaponTip - pose.WeaponHand;

        Assert.Equal(1f, pose.Forward.Length(), precision: 5);
        Assert.True(Vector2.Dot(weaponForward, pose.Forward) > 0f);
        AssertFinite(pose.WeaponTip);
        AssertFinite(pose.TrailStart);
        AssertFinite(pose.TrailEnd);
    }

    [Fact]
    public void Resolve_MirrorsComboAcrossTargetLocalLateralAxis()
    {
        var first = AttackPoseResolver.Resolve(
            AttackGeometryTests.Animation(
                WeaponId.Itak,
                comboPosition: 1,
                directionX: 0.6f,
                directionY: 0.8f));
        var second = AttackPoseResolver.Resolve(
            AttackGeometryTests.Animation(
                WeaponId.Itak,
                comboPosition: 2,
                directionX: 0.6f,
                directionY: 0.8f));
        var firstWeapon = first.WeaponTip - first.WeaponHand;
        var secondWeapon = second.WeaponTip - second.WeaponHand;

        Assert.True(Vector2.Dot(firstWeapon, first.Forward) > 0f);
        Assert.True(Vector2.Dot(secondWeapon, second.Forward) > 0f);
        Assert.Equal(
            Vector2.Dot(firstWeapon, first.Right),
            -Vector2.Dot(secondWeapon, second.Right),
            precision: 5);
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
        Assert.True(Vector2.Dot(pose.WeaponHand, pose.Right) > 0f);
        Assert.True(Vector2.Dot(pose.ShieldHand, pose.Right) < 0f);
        Assert.NotEqual(pose.WeaponHand, pose.ShieldHand);
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
