using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

public sealed class PawnAppearanceFactoryTests
{
    [Fact]
    public void Create_ReturnsIdenticalAppearanceForStableEntityIdAndWeapon()
    {
        var first = PawnAppearanceFactory.Create(42, WeaponId.Kampilan, ShieldId.None);
        var second = PawnAppearanceFactory.Create(42, WeaponId.Kampilan, ShieldId.None);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_SameEntityIdDifferentWeaponKeepsBodyButChangesRole()
    {
        var greatBlade = PawnAppearanceFactory.Create(42, WeaponId.Kampilan, ShieldId.None);
        var bolo = PawnAppearanceFactory.Create(42, WeaponId.Itak, ShieldId.None);

        Assert.Equal(greatBlade.StatureMultiplier, bolo.StatureMultiplier);
        Assert.Equal(greatBlade.BuildMultiplier, bolo.BuildMultiplier);
        Assert.Equal(greatBlade.HeadTreatment, bolo.HeadTreatment);
        Assert.Equal(greatBlade.ClothingColor, bolo.ClothingColor);
        Assert.Equal(greatBlade.AccentColor, bolo.AccentColor);
        Assert.Equal(greatBlade.SkinColor, bolo.SkinColor);
        Assert.Equal(greatBlade.HeadTreatmentColor, bolo.HeadTreatmentColor);
        Assert.NotEqual(greatBlade.WeaponRole, bolo.WeaponRole);
    }

    [Theory]
    [InlineData(WeaponId.Kampilan)]
    [InlineData(WeaponId.Wasay)]
    [InlineData(WeaponId.Kalis)]
    [InlineData(WeaponId.Itak)]
    public void Create_MapsEveryCoreWeaponIdToOneExplicitSilhouette(
        WeaponId weapon)
    {
        var appearance = PawnAppearanceFactory.Create(1, weapon, ShieldId.None);

        Assert.Equal(
            Enum.Parse<PawnWeaponRole>(weapon.ToString()),
            appearance.WeaponRole);
    }

    [Fact]
    public void Create_MapsAllFourWeaponIdsToDistinctSilhouettes()
    {
        var roles = Enum.GetValues<WeaponId>()
            .Select(weapon => PawnAppearanceFactory
                .Create(1, weapon, ShieldId.None)
                .WeaponRole)
            .ToHashSet();

        Assert.Equal(Enum.GetValues<WeaponId>().Length, roles.Count);
    }

    [Fact]
    public void Create_NeverDerivesWeaponRoleFromEntityIdAlone()
    {
        for (ulong entityId = 0; entityId < 20; entityId++)
        {
            foreach (var weapon in Enum.GetValues<WeaponId>())
            {
                var appearance = PawnAppearanceFactory.Create(entityId, weapon, ShieldId.None);

                Assert.Equal(
                    Enum.Parse<PawnWeaponRole>(weapon.ToString()),
                    appearance.WeaponRole);
            }
        }
    }

    [Fact]
    public void Create_UsesOnlyApprovedBodyMultipliers()
    {
        float[] allowedStatures = [0.90f, 1.00f, 1.10f];
        float[] allowedBuilds = [0.86f, 1.00f, 1.18f];

        for (ulong entityId = 0; entityId < 128; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(
                entityId,
                WeaponId.Kampilan, ShieldId.None);

            Assert.Contains(appearance.StatureMultiplier, allowedStatures);
            Assert.Contains(appearance.BuildMultiplier, allowedBuilds);
        }
    }

    [Fact]
    public void WeaponLabels_UseThePairForm()
    {
        string[] labels =
        [
            PawnAppearanceFactory
                .Create(0, WeaponId.Kampilan, ShieldId.None).WeaponLabel,
            PawnAppearanceFactory
                .Create(0, WeaponId.Wasay, ShieldId.None).WeaponLabel,
            PawnAppearanceFactory
                .Create(0, WeaponId.Kalis, ShieldId.None).WeaponLabel,
            PawnAppearanceFactory
                .Create(0, WeaponId.Itak, ShieldId.None).WeaponLabel,
        ];

        Assert.Equal(
            [
                "Kampilan — Great Blade",
                "Wasay — War Axe",
                "Kalis — Thrusting Blade",
                "Itak — Work Blade",
            ],
            labels);
    }

    [Fact]
    public void WeaponLabels_NeverCarryACulturalNameWithoutItsDescriptor()
    {
        // The pair form is what CLAUDE.md section 7 permits: a cultural
        // identification appears only alongside a plain English descriptor,
        // never bare. This is the assertion that catches a label regressing
        // to just "Kampilan".
        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            var label = PawnAppearanceFactory
                .Create(0, weapon, ShieldId.None)
                .WeaponLabel;

            var parts = label.Split(" — ");
            Assert.Equal(2, parts.Length);
            Assert.NotEmpty(parts[0]);
            Assert.NotEmpty(parts[1]);
        }
    }

    [Fact]
    public void WeaponLabels_NeverUseTheRejectedPanabasName()
    {
        // The panabas is first documented in nineteenth-century accounts,
        // roughly three centuries after the depicted period. The hundred-year
        // attestation rule excludes it outright rather than badging it
        // provisional, and this test is what keeps that rule load-bearing.
        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            Assert.DoesNotContain(
                "panabas",
                PawnAppearanceFactory
                    .Create(0, weapon, ShieldId.None)
                    .WeaponLabel,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void EvidenceTier_MatchesTheResearchDocument()
    {
        // Kalis is the only one a contemporary source names directly:
        // Pigafetta recorded calis in 1521. Kampilan and Wasay have an
        // attested weapon class but no period attestation of the name, and
        // Itak's is a reasoned reconstruction.
        AssertTier(WeaponId.Kalis, WeaponEvidenceTier.Documented);
        AssertTier(
            WeaponId.Kampilan,
            WeaponEvidenceTier.DocumentedFormUncertain);
        AssertTier(WeaponId.Wasay, WeaponEvidenceTier.DocumentedFormUncertain);
        AssertTier(
            WeaponId.Itak,
            WeaponEvidenceTier.ProvisionalReconstruction);

        static void AssertTier(WeaponId weapon, WeaponEvidenceTier expected)
        {
            var appearance = PawnAppearanceFactory.Create(
                0,
                weapon,
                ShieldId.None);

            Assert.Equal(expected, appearance.EvidenceTier);
            Assert.NotEmpty(appearance.EvidenceTierLabel);
        }
    }

    [Fact]
    public void EveryWeaponCarriesAnEvidenceNote()
    {
        // A name shown to a spectator always says how far its evidence
        // reaches. There is no weapon whose note may be blank.
        foreach (var weapon in Enum.GetValues<WeaponId>())
        {
            Assert.NotEmpty(
                PawnAppearanceFactory
                    .Create(0, weapon, ShieldId.None)
                    .EvidenceNote);
        }
    }

    [Fact]
    public void ShieldRoleComesFromTheAuthoritativeLoadout()
    {
        AssertRole(ShieldId.None, PawnShieldRole.None, carriesShield: false);
        AssertRole(
            ShieldId.TallHardwood,
            PawnShieldRole.TallHardwood,
            carriesShield: true);

        static void AssertRole(
            ShieldId shield,
            PawnShieldRole expectedRole,
            bool carriesShield)
        {
            var appearance = PawnAppearanceFactory.Create(
                7,
                WeaponId.Kalis,
                shield);

            Assert.Equal(expectedRole, appearance.ShieldRole);
            Assert.Equal(carriesShield, appearance.CarriesShield);
        }
    }

    [Fact]
    public void ShieldRoleIsNeverDerivedFromTheEntityId()
    {
        // Same reasoning as the weapon role: equipment identity is
        // authoritative Core state, and only stature, build, clothing, skin,
        // and head treatment may vary with the entity ID.
        for (ulong entityId = 1; entityId <= 64; entityId++)
        {
            Assert.Equal(
                PawnShieldRole.None,
                PawnAppearanceFactory
                    .Create(entityId, WeaponId.Kalis, ShieldId.None)
                    .ShieldRole);
        }
    }

    [Fact]
    public void Create_IsIndependentOfPresentationContext()
    {
        const ulong entityId = 73;

        var first = PawnAppearanceFactory.Create(entityId, WeaponId.Itak, ShieldId.None);
        var second = PawnAppearanceFactory.Create(entityId, WeaponId.Itak, ShieldId.None);
        var third = PawnAppearanceFactory.Create(entityId, WeaponId.Itak, ShieldId.None);

        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }
}
