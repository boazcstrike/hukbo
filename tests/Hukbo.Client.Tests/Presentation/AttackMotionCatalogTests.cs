using System.Reflection;
using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

public sealed class AttackMotionCatalogTests
{
    [Fact]
    public void Resolve_MapsEveryWeaponToItsDeclaredMotionFamily()
    {
        var expected = new Dictionary<WeaponId, AttackMotionFamily>
        {
            [WeaponId.Kampilan] = AttackMotionFamily.CommittedCleaver,
            [WeaponId.Wasay] = AttackMotionFamily.HeadWeightedChop,
            [WeaponId.Kalis] = AttackMotionFamily.LinearThrustCut,
            [WeaponId.Itak] = AttackMotionFamily.CompactChopSlash,
        };

        Assert.Equal(Enum.GetValues<WeaponId>().Length, expected.Count);
        Assert.Equal(4, Enum.GetValues<AttackMotionFamily>().Length);

        foreach (var (weapon, family) in expected)
        {
            Assert.Equal(family, AttackMotionCatalog.Resolve(weapon).Family);
        }
    }

    [Fact]
    public void Resolve_RejectsAnUnknownWeapon()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AttackMotionCatalog.Resolve((WeaponId)int.MaxValue));
    }

    [Fact]
    public void Resolve_CarriesBoundedPresentationOnlyMotionData()
    {
        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            var profile = AttackMotionCatalog.Resolve(weapon);

            Assert.True(float.IsFinite(profile.VisualExtensionEnvelope));
            Assert.InRange(profile.VisualExtensionEnvelope, 0f, 2f);
            Assert.True(float.IsFinite(profile.ArcRadians));
            Assert.InRange(profile.ArcRadians, 0f, MathF.PI);
            Assert.True(float.IsFinite(profile.LateralBias));
            Assert.InRange(profile.LateralBias, -1f, 1f);
            Assert.True(float.IsFinite(profile.RecoilStrength));
            Assert.InRange(profile.RecoilStrength, 0f, 1f);
            Assert.True(float.IsFinite(profile.RecoverySeconds));
            Assert.InRange(profile.RecoverySeconds, 0.01f, 1f);
            Assert.Contains(profile.HandCount, new[] { 1, 2 });
            Assert.True(profile.TrailEligible);
        }

        Assert.False(AttackMotionCatalog.Resolve(WeaponId.Kampilan).ShieldCompatible);
        Assert.False(AttackMotionCatalog.Resolve(WeaponId.Wasay).ShieldCompatible);
        Assert.True(AttackMotionCatalog.Resolve(WeaponId.Kalis).ShieldCompatible);
        Assert.True(AttackMotionCatalog.Resolve(WeaponId.Itak).ShieldCompatible);
    }

    [Fact]
    public void AttackMotionProfile_IsAnImmutableValue()
    {
        var type = typeof(AttackMotionProfile);
        var instanceFields = type.GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.True(type.IsValueType);
        Assert.NotEmpty(instanceFields);
        Assert.All(instanceFields, field => Assert.True(field.IsInitOnly, field.Name));
    }
}
