using Hukbo.Client.Presentation;

namespace Hukbo.Client.Tests;

/// <summary>
/// "Never reuse a salt" (R-W6.2) is only a real rule if a reused value fails
/// the build. These assertions are that failure, plus a pin on the four
/// pre-existing salts so a registry edit can never silently reshuffle
/// shipped pawn appearance or plains backdrop decals.
/// </summary>
public sealed class PresentationSaltsTests
{
    [Fact]
    public void AllRegisteredSaltsArePairwiseDistinct()
    {
        var values = PresentationSalts.All
            .Select(entry => entry.Value)
            .ToArray();

        var distinctValues = values.Distinct().ToArray();

        Assert.Equal(values.Length, distinctValues.Length);
    }

    [Fact]
    public void AllRegisteredNamesArePairwiseDistinct()
    {
        var names = PresentationSalts.All
            .Select(entry => entry.Name)
            .ToArray();

        var distinctNames = names.Distinct(StringComparer.Ordinal).ToArray();

        Assert.Equal(names.Length, distinctNames.Length);
    }

    [Fact]
    public void EveryRegisteredSaltCarriesANonEmptyPurpose()
    {
        Assert.All(
            PresentationSalts.All,
            entry => Assert.False(string.IsNullOrWhiteSpace(entry.Purpose)));
    }

    [Fact]
    public void PawnBodySaltMatchesThePawnAppearanceFactoryValue()
    {
        Assert.Equal(0xA0761D6478BD642FUL, PresentationSalts.PawnBodySalt);
    }

    [Fact]
    public void PawnClothingSaltMatchesThePawnAppearanceFactoryValue()
    {
        Assert.Equal(0xE7037ED1A0B428DBUL, PresentationSalts.PawnClothingSalt);
    }

    [Fact]
    public void PawnDetailSaltMatchesThePawnAppearanceFactoryValue()
    {
        Assert.Equal(0x8EBC6AF09C88C6E3UL, PresentationSalts.PawnDetailSalt);
    }

    [Fact]
    public void PlainsBackdropSaltMatchesThePlainsBackdropGeometryValue()
    {
        Assert.Equal(0x504C41494E530001UL, PresentationSalts.PlainsBackdropSalt);
    }

    [Fact]
    public void RegistryListsAllThirteenSalts()
    {
        Assert.Equal(13, PresentationSalts.All.Count);
    }
}
