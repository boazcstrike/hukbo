using System.Text.Json;
using Hukbo.Diagnostics;
using Microsoft.Xna.Framework;
using Sandata.Client.Theming;

namespace Sandata.Client.Tests;

/// <summary>
/// Covers the task 13 test bar: the role contract (count and name set),
/// unknown/missing role rejection, per-theme contrast, and the
/// theme-independent faction constants. Mirrors
/// <c>Hukbo.Client.Tests.UiThemeCatalogTests</c>'s intent for Sandata's own
/// catalog and record.
/// </summary>
public sealed class SandataThemeTests
{
    // design/2026-08-07-sandata-scaffold-design.md section 11: 23 kept + 4
    // repurposed + 12 added = 39 roles, in the same order as
    // SandataThemeColors's properties. A fortieth, "weapon", was added on
    // 2026-08-12: the weapon was previously drawn in the operator's own
    // faction colour, which made a gun and the body it is held by one
    // undifferentiated blob at the zoom a spectator plays at.
    private static readonly string[] ExpectedRoles =
    [
        "canvasBackground",
        "arenaSurface",
        "arenaBorder",
        "statusSurface",
        "overlayScrim",
        "panelSurface",
        "panelAlternate",
        "panelBorder",
        "textPrimary",
        "textSecondary",
        "textDisabled",
        "textInverse",
        "actionDefault",
        "actionHover",
        "actionFocus",
        "actionPressed",
        "actionActive",
        "actionDisabled",
        "statusInfo",
        "statusSuccess",
        "statusWarning",
        "statusDanger",
        "newEvent",
        "friendly",
        "hostile",
        "unknownContact",
        "selectedTrooper",
        "suppressed",
        "downed",
        "orderPath",
        "waypoint",
        "coverGood",
        "coverNone",
        "breachPoint",
        "fireConeFill",
        "fireConeEdge",
        "weapon",
        "alertCalm",
        "alertRaised",
        "alertBreach",
    ];

    private static string BuiltInCatalogPath
    {
        get
        {
            var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory) ??
                throw new InvalidOperationException(
                    "Could not find the Hukbo.slnx repository root from " +
                    "the test output directory.");
            return Path.Combine(
                root,
                "src",
                "Sandata.Client",
                "Content",
                "Themes",
                "sandata-theme-standards.json");
        }
    }

    private static string ReadCatalog() => File.ReadAllText(BuiltInCatalogPath);

    [Fact]
    public void TypedContractDeclaresExactlyFortyRoles()
    {
        var roles = typeof(SandataThemeColors)
            .GetProperties()
            .Select(property =>
                char.ToLowerInvariant(property.Name[0]) + property.Name[1..])
            .ToArray();

        Assert.Equal(40, roles.Length);
        Assert.Equal(
            ExpectedRoles.Order(StringComparer.Ordinal),
            roles.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void BuiltInCatalogContainsAtLeastTwoUniqueThemes()
    {
        var catalog = SandataThemeCatalog.Load(BuiltInCatalogPath);

        Assert.True(catalog.Themes.Count >= 2);
        Assert.Equal(
            catalog.Themes.Count,
            catalog.Themes
                .Select(theme => theme.Id)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Contains(
            catalog.Themes,
            theme => theme.Id == catalog.DefaultThemeId);
    }

    [Fact]
    public void EveryShippedThemeCarriesAllFortyRoles()
    {
        var catalog = SandataThemeCatalog.Load(BuiltInCatalogPath);

        Assert.All(
            catalog.Themes,
            theme =>
            {
                var colors = theme.Colors;
                var values = new[]
                {
                    colors.CanvasBackground,
                    colors.ArenaSurface,
                    colors.ArenaBorder,
                    colors.StatusSurface,
                    colors.OverlayScrim,
                    colors.PanelSurface,
                    colors.PanelAlternate,
                    colors.PanelBorder,
                    colors.TextPrimary,
                    colors.TextSecondary,
                    colors.TextDisabled,
                    colors.TextInverse,
                    colors.ActionDefault,
                    colors.ActionHover,
                    colors.ActionFocus,
                    colors.ActionPressed,
                    colors.ActionActive,
                    colors.ActionDisabled,
                    colors.StatusInfo,
                    colors.StatusSuccess,
                    colors.StatusWarning,
                    colors.StatusDanger,
                    colors.NewEvent,
                    colors.Friendly,
                    colors.Hostile,
                    colors.UnknownContact,
                    colors.SelectedTrooper,
                    colors.Suppressed,
                    colors.Downed,
                    colors.OrderPath,
                    colors.Waypoint,
                    colors.CoverGood,
                    colors.CoverNone,
                    colors.BreachPoint,
                    colors.FireConeFill,
                    colors.FireConeEdge,
                    colors.Weapon,
                    colors.AlertCalm,
                    colors.AlertRaised,
                    colors.AlertBreach,
                };
                Assert.Equal(40, values.Length);
            });
    }

    [Fact]
    public void RejectsAThemeMissingARequiredRole()
    {
        var json = ReadCatalog()
            .Replace(
                "\"breachPoint\": \"#FF7A45\",",
                string.Empty,
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => SandataThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void RejectsAThemeWithAnUnknownRole()
    {
        var json = ReadCatalog()
            .Replace(
                "\"breachPoint\": \"#FF7A45\",",
                "\"breachPoint\": \"#FF7A45\", \"notARole\": \"#000000\",",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => SandataThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void RejectsStandardsThatDivergeFromTheTypedContract()
    {
        var json = ReadCatalog()
            .Replace(
                "\n      \"breachPoint\",\n",
                "\n",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => SandataThemeCatalog.LoadFromJson(json));
    }

    [Theory]
    [InlineData("night-ops")]
    [InlineData("daylight-ops")]
    public void EveryBuiltInThemeLoadsWithoutContrastFailure(string id)
    {
        var catalog = SandataThemeCatalog.Load(BuiltInCatalogPath);

        Assert.True(catalog.TryGet(id, out var theme));
        Assert.Equal(id, theme.Id);
    }

    [Fact]
    public void FriendlyHostileAndUnknownContactAreIdenticalAcrossThemes()
    {
        var catalog = SandataThemeCatalog.Load(BuiltInCatalogPath);

        Assert.All(
            catalog.Themes,
            theme =>
            {
                Assert.Equal(SandataFactionPalette.Friendly, theme.Colors.Friendly);
                Assert.Equal(SandataFactionPalette.Hostile, theme.Colors.Hostile);
                Assert.Equal(
                    SandataFactionPalette.UnknownContact,
                    theme.Colors.UnknownContact);
            });
    }

    [Fact]
    public void RejectsAThemeWhoseFriendlyColorDiverges()
    {
        var json = ReadCatalog()
            .Replace(
                "\"friendly\": \"#40A4FF\",\n        \"hostile\": \"#FF5B69\",\n        \"unknownContact\": \"#E7C754\",\n        \"selectedTrooper\": \"#FF9F1C\",",
                "\"friendly\": \"#123456\",\n        \"hostile\": \"#FF5B69\",\n        \"unknownContact\": \"#E7C754\",\n        \"selectedTrooper\": \"#FF9F1C\",",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => SandataThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void CatalogJsonParsesAsValidJson()
    {
        using var document = JsonDocument.Parse(ReadCatalog());

        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }
}
