using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;

namespace Hukbo.Client.Tests;

public sealed class PawnAppearanceFactoryTests
{
    [Fact]
    public void Create_ReturnsIdenticalAppearanceForStableEntityIdAndWeapon()
    {
        var first = PawnAppearanceFactory.Create(42, WeaponId.GreatBlade);
        var second = PawnAppearanceFactory.Create(42, WeaponId.GreatBlade);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_SameEntityIdDifferentWeaponKeepsBodyButChangesRole()
    {
        var greatBlade = PawnAppearanceFactory.Create(42, WeaponId.GreatBlade);
        var bolo = PawnAppearanceFactory.Create(42, WeaponId.Bolo);

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
    [InlineData(WeaponId.GreatBlade)]
    [InlineData(WeaponId.HeavyChopper)]
    [InlineData(WeaponId.ThrustingBlade)]
    [InlineData(WeaponId.Bolo)]
    public void Create_MapsEveryCoreWeaponIdToOneExplicitSilhouette(
        WeaponId weapon)
    {
        var appearance = PawnAppearanceFactory.Create(1, weapon);

        Assert.Equal(
            Enum.Parse<PawnWeaponRole>(weapon.ToString()),
            appearance.WeaponRole);
    }

    [Fact]
    public void Create_MapsAllFourWeaponIdsToDistinctSilhouettes()
    {
        var roles = Enum.GetValues<WeaponId>()
            .Select(weapon => PawnAppearanceFactory.Create(1, weapon).WeaponRole)
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
                var appearance = PawnAppearanceFactory.Create(entityId, weapon);

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
                WeaponId.GreatBlade);

            Assert.Contains(appearance.StatureMultiplier, allowedStatures);
            Assert.Contains(appearance.BuildMultiplier, allowedBuilds);
        }
    }

    [Fact]
    public void WeaponLabels_MatchApprovedPlayerFacingLabels()
    {
        string[] labels =
        [
            PawnAppearanceFactory.Create(0, WeaponId.GreatBlade).WeaponLabel,
            PawnAppearanceFactory.Create(0, WeaponId.HeavyChopper).WeaponLabel,
            PawnAppearanceFactory.Create(0, WeaponId.ThrustingBlade)
                .WeaponLabel,
            PawnAppearanceFactory.Create(0, WeaponId.Bolo).WeaponLabel,
        ];

        Assert.Equal(
            [
                "Great Blade",
                "Heavy Chopper",
                "Thrusting Blade",
                "Work Blade",
            ],
            labels);

        foreach (var label in labels)
        {
            Assert.DoesNotContain(
                "kampilan",
                label,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "panabas",
                label,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "kris",
                label,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(WeaponId.GreatBlade)]
    [InlineData(WeaponId.HeavyChopper)]
    [InlineData(WeaponId.ThrustingBlade)]
    public void EvidenceNote_IsMarkedProvisionalWhenPresent(WeaponId weapon)
    {
        var appearance = PawnAppearanceFactory.Create(0, weapon);

        Assert.NotNull(appearance.EvidenceNote);
        Assert.StartsWith("PROVISIONAL", appearance.EvidenceNote);
    }

    [Fact]
    public void EvidenceNote_IsNullForBolo()
    {
        var appearance = PawnAppearanceFactory.Create(0, WeaponId.Bolo);

        Assert.Null(appearance.EvidenceNote);
    }

    [Fact]
    public void Create_IsIndependentOfPresentationContext()
    {
        const ulong entityId = 73;

        var first = PawnAppearanceFactory.Create(entityId, WeaponId.Bolo);
        var second = PawnAppearanceFactory.Create(entityId, WeaponId.Bolo);
        var third = PawnAppearanceFactory.Create(entityId, WeaponId.Bolo);

        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }
}
