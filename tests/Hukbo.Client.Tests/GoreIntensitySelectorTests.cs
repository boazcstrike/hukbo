using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;

namespace Hukbo.Client.Tests;

/// <summary>
/// Thin wiring test only. Wrapping, keyboard and pointer selection, the
/// undefined-value fallback, and arrow sizing are all proven once,
/// generically, in <see cref="SettingsChoiceSelectorTests"/> against
/// <see cref="SettingsChoiceSelector{T}"/>, which this type delegates to
/// entirely — see <see cref="GoreIntensitySelector"/>'s own doc comment.
/// </summary>
public sealed class GoreIntensitySelectorTests
{
    [Fact]
    public void ExposesThreeOrderedNamesAndVisibleSelectedMarker()
    {
        var selector = new GoreIntensitySelector(LoadStandards());

        Assert.Equal(
            ["Off", "Stylized", "Full"],
            selector.OptionNames);
        Assert.Equal(
            "ACTIVE  -  2 / 3",
            selector.GetSelectedMarkerText(GoreIntensity.Stylized));
    }

    private static UiThemeStandards LoadStandards()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");
        return UiThemeCatalog.Load(path).Standards;
    }
}
