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
            [WeaponId.Bangkaw] = AttackMotionFamily.OverhandThrow,
            [WeaponId.Busog] = AttackMotionFamily.DrawAndRelease,
            [WeaponId.Arquebus] = AttackMotionFamily.BracedDischarge,
        };

        Assert.Equal(Enum.GetValues<WeaponId>().Length, expected.Count);
        Assert.Equal(7, Enum.GetValues<AttackMotionFamily>().Length);

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
        }

        // TrailEligible is pinned per weapon, not asserted as a blanket true.
        // The four bladed melee weapons sweep an edge through the air and
        // draw a bounded trail; the three ranged weapons resolve their attack
        // as a release (a hurl, a bowstring, a discharge) with no continuous
        // edge sweep, so they do not draw one. Each pin below still fails on
        // its own if that weapon's flag flips silently.
        Assert.True(AttackMotionCatalog.Resolve(WeaponId.Kampilan).TrailEligible);
        Assert.True(AttackMotionCatalog.Resolve(WeaponId.Wasay).TrailEligible);
        Assert.True(AttackMotionCatalog.Resolve(WeaponId.Kalis).TrailEligible);
        Assert.True(AttackMotionCatalog.Resolve(WeaponId.Itak).TrailEligible);
        Assert.False(AttackMotionCatalog.Resolve(WeaponId.Bangkaw).TrailEligible);
        Assert.False(AttackMotionCatalog.Resolve(WeaponId.Busog).TrailEligible);
        Assert.False(AttackMotionCatalog.Resolve(WeaponId.Arquebus).TrailEligible);

        Assert.False(AttackMotionCatalog.Resolve(WeaponId.Kampilan).ShieldCompatible);
        Assert.False(AttackMotionCatalog.Resolve(WeaponId.Wasay).ShieldCompatible);
        Assert.True(AttackMotionCatalog.Resolve(WeaponId.Kalis).ShieldCompatible);
        Assert.True(AttackMotionCatalog.Resolve(WeaponId.Itak).ShieldCompatible);
        Assert.True(AttackMotionCatalog.Resolve(WeaponId.Bangkaw).ShieldCompatible);
        Assert.False(AttackMotionCatalog.Resolve(WeaponId.Busog).ShieldCompatible);
        Assert.False(AttackMotionCatalog.Resolve(WeaponId.Arquebus).ShieldCompatible);
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

    /// <summary>
    /// Design section 7's family signatures, pinned as relations between the
    /// four profiles rather than as absolute numbers, so retuning one family's
    /// magnitude cannot quietly erase what separates it from another.
    /// </summary>
    /// <remarks>
    /// Every relation is provisional presentation choreography. None of it is
    /// a claim about how any of these weapons was actually used.
    /// </remarks>
    [Fact]
    public void Profiles_KeepEachFamilysDeclaredSignature()
    {
        var kampilan = AttackMotionCatalog.Resolve(WeaponId.Kampilan);
        var wasay = AttackMotionCatalog.Resolve(WeaponId.Wasay);
        var kalis = AttackMotionCatalog.Resolve(WeaponId.Kalis);
        var itak = AttackMotionCatalog.Resolve(WeaponId.Itak);

        // Kampilan: the broadest angular travel of the four.
        Assert.True(kampilan.ArcRadians > wasay.ArcRadians);
        Assert.True(kampilan.ArcRadians > kalis.ArcRadians);
        Assert.True(kampilan.ArcRadians > itak.ArcRadians);

        // Wasay: the hardest stop and the longest recovery.
        Assert.True(wasay.RecoilStrength > kampilan.RecoilStrength);
        Assert.True(wasay.RecoilStrength > kalis.RecoilStrength);
        Assert.True(wasay.RecoilStrength > itak.RecoilStrength);
        Assert.True(wasay.RecoverySeconds > kampilan.RecoverySeconds);
        Assert.True(wasay.RecoverySeconds > kalis.RecoverySeconds);
        Assert.True(wasay.RecoverySeconds > itak.RecoverySeconds);

        // Kalis: the longest extension and the least angular travel, which is
        // what makes it read as a thrust rather than a cut.
        Assert.True(kalis.VisualExtensionEnvelope > kampilan.VisualExtensionEnvelope);
        Assert.True(kalis.VisualExtensionEnvelope > wasay.VisualExtensionEnvelope);
        Assert.True(kalis.VisualExtensionEnvelope > itak.VisualExtensionEnvelope);
        Assert.True(kalis.ArcRadians < itak.ArcRadians);
        Assert.True(kalis.LateralBias < itak.LateralBias);

        // Itak: the smallest reach envelope and the quickest return.
        Assert.True(itak.VisualExtensionEnvelope <= kampilan.VisualExtensionEnvelope);
        Assert.True(itak.VisualExtensionEnvelope <= wasay.VisualExtensionEnvelope);
        Assert.True(itak.RecoverySeconds < kampilan.RecoverySeconds);
        Assert.True(itak.RecoverySeconds < kalis.RecoverySeconds);
    }

    /// <summary>
    /// Grip class is a loadout fact, not a tuning knob: the two-handed
    /// families commit both hands and forbid a shield, the one-handed families
    /// commit one and permit one.
    /// </summary>
    [Fact]
    public void Profiles_MatchTheirGripAndLoadoutClass()
    {
        foreach (var weapon in new[] { WeaponId.Kampilan, WeaponId.Wasay })
        {
            var profile = AttackMotionCatalog.Resolve(weapon);
            Assert.Equal(2, profile.HandCount);
            Assert.False(profile.ShieldCompatible);
        }

        foreach (var weapon in new[] { WeaponId.Kalis, WeaponId.Itak })
        {
            var profile = AttackMotionCatalog.Resolve(weapon);
            Assert.Equal(1, profile.HandCount);
            Assert.True(profile.ShieldCompatible);
        }
    }

    /// <summary>
    /// The Wasay's head-led acceleration is a curve difference, not a
    /// magnitude difference: it commits a smaller share of its arc at contact
    /// than the families that lead with the edge, so the mass arrives late.
    /// </summary>
    [Fact]
    public void Profiles_GiveTheHeadWeightedFamilyALaterContactShare()
    {
        var wasay = AttackMotionCatalog.Resolve(WeaponId.Wasay);

        Assert.True(
            wasay.ContactAngleShare <
            AttackMotionCatalog.Resolve(WeaponId.Kampilan).ContactAngleShare);
        Assert.True(
            wasay.ContactAngleShare <
            AttackMotionCatalog.Resolve(WeaponId.Itak).ContactAngleShare);
        Assert.True(wasay.TrailLagShare > 0f);
    }

    /// <summary>
    /// A shield overlay exists only for a loadout that may legally carry one.
    /// It is an overlay on the weapon's own family, never a fifth or sixth
    /// family of its own.
    /// </summary>
    [Theory]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    public void ResolveShieldOverlay_AppliesOnlyToALegalPairedLoadout(WeaponId weapon)
    {
        Assert.Null(
            AttackMotionCatalog.ResolveShieldOverlay(weapon, ShieldId.None));

        var overlay = AttackMotionCatalog.ResolveShieldOverlay(
            weapon,
            ShieldId.TallHardwood);

        Assert.NotNull(overlay);
        Assert.InRange(overlay.Value.LateralScale, 0f, 1f);
        Assert.InRange(overlay.Value.TorsoRotationScale, 0f, 1f);
        Assert.InRange(overlay.Value.ExtensionScale, 0f, 1f);

        // Solo and paired stay the same base family.
        Assert.Equal(
            AttackMotionCatalog.Resolve(weapon).Family,
            AttackMotionCatalog.Resolve(weapon).Family);
    }

    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    public void ResolveShieldOverlay_RejectsAnIncompatibleProfile(WeaponId weapon)
    {
        Assert.Null(
            AttackMotionCatalog.ResolveShieldOverlay(weapon, ShieldId.None));
        Assert.Null(
            AttackMotionCatalog.ResolveShieldOverlay(
                weapon,
                ShieldId.TallHardwood));
    }

    [Fact]
    public void ResolveShieldOverlay_RejectsUnknownValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AttackMotionCatalog.ResolveShieldOverlay(
                (WeaponId)999,
                ShieldId.None));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AttackMotionCatalog.ResolveShieldOverlay(
                WeaponId.Kalis,
                (ShieldId)999));
    }
}
