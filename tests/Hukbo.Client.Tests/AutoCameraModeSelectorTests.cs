using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;

namespace Hukbo.Client.Tests;

/// <summary>
/// Thin wiring test only. Wrapping, keyboard and pointer selection, the
/// undefined-value fallback, and arrow sizing are all proven once,
/// generically, in <see cref="SettingsChoiceSelectorTests"/> against
/// <see cref="SettingsChoiceSelector{T}"/>, which this type delegates to
/// entirely — see <see cref="AutoCameraModeSelector"/>'s own doc comment.
/// </summary>
public sealed class AutoCameraModeSelectorTests
{
    [Fact]
    public void ExposesThreeOrderedNamesAndVisibleSelectedMarker()
    {
        var selector = new AutoCameraModeSelector(LoadStandards());

        Assert.Equal(
            ["Off", "Assisted", "Follow"],
            selector.OptionNames);
        Assert.Equal(
            "ACTIVE  -  2 / 3",
            selector.GetSelectedMarkerText(AutoCameraMode.Assisted));
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
