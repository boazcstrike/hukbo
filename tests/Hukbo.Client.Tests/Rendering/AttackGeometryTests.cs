using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

public sealed class AttackGeometryTests
{
    [Fact]
    public void Evaluate_UsesContinuousContactAndRecoveryBoundaries()
    {
        var profile = AttackMotionCatalog.Resolve(WeaponId.Wasay);
        var latched = Animation(
            WeaponId.Wasay,
            ageSeconds: 0f,
            awaitingDrawAcknowledgement: true);
        var recoveryStart = latched with
        {
            AwaitingDrawAcknowledgement = false,
        };
        var recoveryEnd = recoveryStart with
        {
            AgeSeconds = profile.RecoverySeconds,
        };
        var justBeforeReadiness = recoveryEnd with
        {
            AgeSeconds = profile.RecoverySeconds - 0.0001f,
        };

        var contact = AttackGeometry.Evaluate(latched);
        var start = AttackGeometry.Evaluate(recoveryStart);
        var almostReady = AttackGeometry.Evaluate(justBeforeReadiness);
        var ready = AttackGeometry.Evaluate(recoveryEnd);

        Assert.Equal(contact, start);
        Assert.InRange(
            MathF.Abs(almostReady.WeaponAngleRadians - ready.WeaponAngleRadians),
            0f,
            0.0001f);
        Assert.InRange(
            MathF.Abs(almostReady.WeaponReach - ready.WeaponReach),
            0f,
            0.0001f);
        Assert.Equal(0f, ready.WeaponAngleRadians);
        Assert.Equal(1f, ready.WeaponReach);
        Assert.Equal(0f, ready.TrailStrength);
    }

    [Fact]
    public void Evaluate_KeepsReachAndTrailFiniteAcrossFamiliesAndOutcomes()
    {
        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            var profile = AttackMotionCatalog.Resolve(weapon);
            foreach (var resolution in Enum.GetValues<AttackResolution>())
            {
                foreach (var recoveryProgress in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                {
                    var animation = Animation(
                        weapon,
                        resolution,
                        ageSeconds: profile.RecoverySeconds * recoveryProgress,
                        awaitingDrawAcknowledgement: false);

                    var geometry = AttackGeometry.Evaluate(animation);

                    Assert.True(float.IsFinite(geometry.WeaponAngleRadians));
                    Assert.True(float.IsFinite(geometry.WeaponReach));
                    Assert.True(geometry.WeaponReach > 0f);
                    Assert.True(float.IsFinite(geometry.WeaponLateralOffset));
                    Assert.True(float.IsFinite(geometry.TrailAngleRadians));
                    Assert.True(float.IsFinite(geometry.TrailStrength));
                    Assert.InRange(geometry.TrailStrength, 0f, 1f);
                }
            }
        }
    }

    [Fact]
    public void Evaluate_AlternatesComboLateralBiasWithoutReversingReach()
    {
        var first = AttackGeometry.Evaluate(
            Animation(WeaponId.Itak, comboPosition: 1));
        var second = AttackGeometry.Evaluate(
            Animation(WeaponId.Itak, comboPosition: 2));

        Assert.True(first.WeaponReach > 0f);
        Assert.True(second.WeaponReach > 0f);
        Assert.Equal(
            first.WeaponLateralOffset,
            -second.WeaponLateralOffset,
            precision: 5);
        Assert.Equal(
            first.WeaponAngleRadians,
            -second.WeaponAngleRadians,
            precision: 5);
    }

    [Fact]
    public void Evaluate_GivesEveryFamilyADistinctContactCurve()
    {
        var geometries = Enum.GetValues<WeaponId>()
            .Select(weapon => AttackGeometry.Evaluate(Animation(weapon)))
            .ToArray();

        for (var first = 0; first < geometries.Length; first++)
        {
            for (var second = first + 1; second < geometries.Length; second++)
            {
                var firstGeometry = geometries[first];
                var secondGeometry = geometries[second];
                Assert.True(
                    MathF.Abs(
                        firstGeometry.WeaponAngleRadians -
                        secondGeometry.WeaponAngleRadians) > 0.01f ||
                    MathF.Abs(
                        firstGeometry.WeaponReach -
                        secondGeometry.WeaponReach) > 0.01f ||
                    MathF.Abs(
                        firstGeometry.TorsoForwardOffset -
                        secondGeometry.TorsoForwardOffset) > 0.01f);
            }
        }
    }

    internal static AttackAnimation Animation(
        WeaponId weapon,
        AttackResolution resolution = AttackResolution.Landed,
        int? comboPosition = 1,
        float ageSeconds = 0f,
        bool awaitingDrawAcknowledgement = true,
        ShieldId shield = ShieldId.None,
        float directionX = 1f,
        float directionY = 0f)
    {
        var profile = AttackMotionCatalog.Resolve(weapon);
        return new AttackAnimation(
            Sequence: 1,
            Tick: 1,
            AttackerEntityId: 2,
            DefenderEntityId: 7,
            Damage: resolution == AttackResolution.Landed ? 10 : 0,
            FactionId: 0,
            weapon,
            AttackerShield: shield,
            HitLocation: BodyPart.Chest,
            resolution,
            comboPosition,
            IsLethal: false,
            directionX,
            directionY,
            MotionIntensity.Full,
            profile,
            ageSeconds,
            awaitingDrawAcknowledgement);
    }
}
