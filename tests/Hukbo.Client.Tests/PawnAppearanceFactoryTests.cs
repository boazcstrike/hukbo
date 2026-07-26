using Hukbo.Client.Presentation;

namespace Hukbo.Client.Tests;

public sealed class PawnAppearanceFactoryTests
{
    [Fact]
    public void Create_ReturnsIdenticalAppearanceForStableEntityId()
    {
        var first = PawnAppearanceFactory.Create(42);
        var second = PawnAppearanceFactory.Create(42);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_MapsFirstFiveEntityIdsToEveryWeaponRole()
    {
        var roles = Enumerable.Range(0, 5)
            .Select(id => PawnAppearanceFactory.Create((ulong)id).WeaponRole)
            .ToHashSet();

        Assert.Equal(Enum.GetValues<PawnWeaponRole>().Length, roles.Count);
        Assert.All(Enum.GetValues<PawnWeaponRole>(), role => Assert.Contains(role, roles));
    }

    [Fact]
    public void Create_UsesOnlyApprovedBodyMultipliers()
    {
        float[] allowedStatures = [0.90f, 1.00f, 1.10f];
        float[] allowedBuilds = [0.86f, 1.00f, 1.18f];

        for (ulong entityId = 0; entityId < 128; entityId++)
        {
            var appearance = PawnAppearanceFactory.Create(entityId);

            Assert.Contains(appearance.StatureMultiplier, allowedStatures);
            Assert.Contains(appearance.BuildMultiplier, allowedBuilds);
        }
    }

    [Fact]
    public void WeaponLabels_MatchApprovedPlayerFacingLabels()
    {
        string[] labels =
        [
            PawnAppearanceFactory.Create(0).WeaponLabel,
            PawnAppearanceFactory.Create(1).WeaponLabel,
            PawnAppearanceFactory.Create(2).WeaponLabel,
            PawnAppearanceFactory.Create(3).WeaponLabel,
            PawnAppearanceFactory.Create(4).WeaponLabel,
        ];

        Assert.Equal(
            [
                "Bangkaw - Long Spear",
                "Hardened Javelin",
                "Busog - War Bow",
                "Broad Dagger",
                "Great Blade",
            ],
            labels);
        Assert.All(
            labels,
            label => Assert.DoesNotContain(
                "kampilan",
                label,
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Create_IsIndependentOfPresentationContext()
    {
        const ulong entityId = 73;

        var arenaAppearance = PawnAppearanceFactory.Create(entityId);
        var inspectorAppearance = PawnAppearanceFactory.Create(entityId);
        var resetEquivalentAppearance = PawnAppearanceFactory.Create(entityId);

        Assert.Equal(arenaAppearance, inspectorAppearance);
        Assert.Equal(arenaAppearance, resetEquivalentAppearance);
    }
}
