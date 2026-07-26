using System.Text.Json.Nodes;
using Hukbo.Client.Theming;

namespace Hukbo.Client.Tests;

public sealed class UiThemeCatalogTests
{
    private static string BuiltInCatalogPath =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");

    [Fact]
    public void BuiltInCatalogContainsExactlyFiveUniqueThemes()
    {
        var catalog = UiThemeCatalog.Load(BuiltInCatalogPath);

        Assert.Equal(5, catalog.Themes.Count);
        Assert.Equal(
            5,
            catalog.Themes
                .Select(theme => theme.Id)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Contains(
            catalog.Themes,
            theme => theme.Id == catalog.DefaultThemeId);
        Assert.Equal(5, catalog.Standards.RequiredThemeCount);
        Assert.Equal("Default", catalog.Standards.Shared.FontAssetId);
        Assert.Equal(44, catalog.Standards.Shared.Selector.MinimumTargetSize);
    }

    [Theory]
    [InlineData("command")]
    [InlineData("field-manual")]
    [InlineData("signal")]
    [InlineData("broadcast")]
    [InlineData("high-contrast")]
    public void EveryBuiltInThemeIsCompleteAndAccessible(string id)
    {
        var catalog = UiThemeCatalog.Load(BuiltInCatalogPath);

        var errors = catalog.ValidateTheme(catalog.GetRequired(id));

        Assert.Empty(errors);
    }

    [Fact]
    public void RejectsDuplicateThemeIds()
    {
        var json = ReadCatalog()
            .Replace(
                "\"id\": \"signal\"",
                "\"id\": \"command\"",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => UiThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void RejectsMissingSemanticColor()
    {
        var json = ReadCatalog()
            .Replace(
                "        \"selection\": \"#FFD45A\",\r\n" +
                "        \"newEvent\": \"#FFD45A\"",
                "        \"selection\": \"#FFD45A\"",
                StringComparison.Ordinal)
            .Replace(
                "        \"selection\": \"#FFD45A\",\n" +
                "        \"newEvent\": \"#FFD45A\"",
                "        \"selection\": \"#FFD45A\"",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => UiThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void RejectsMalformedColor()
    {
        var json = ReadCatalog()
            .Replace(
                "\"canvasBackground\": \"#080D16\"",
                "\"canvasBackground\": \"navy\"",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => UiThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void RejectsUnsupportedSchemaVersion()
    {
        var json = ReadCatalog()
            .Replace(
                "\"schemaVersion\": 1",
                "\"schemaVersion\": 2",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => UiThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void RejectsFailedTextContrast()
    {
        var json = ReadCatalog()
            .Replace(
                "\"textPrimary\": \"#FFFFFF\"",
                "\"textPrimary\": \"#161F2E\"",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => UiThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void RejectsOmittedLowContrastInteractionPair()
    {
        var json = ReadCatalog()
            .Replace(
                "    { \"foreground\": \"textInverse\", " +
                "\"background\": \"actionPressed\", " +
                "\"minimumRatio\": 4.5 },\r\n",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                "    { \"foreground\": \"textInverse\", " +
                "\"background\": \"actionPressed\", " +
                "\"minimumRatio\": 4.5 },\n",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                "\"actionPressed\": \"#4692C7\"",
                "\"actionPressed\": \"#06101A\"",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => UiThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void RejectsOmittedEventLogContrastPair()
    {
        var json = ReadCatalog()
            .Replace(
                "    { \"foreground\": \"teamA\", " +
                "\"background\": \"panelAlternate\", " +
                "\"minimumRatio\": 4.5 },\r\n",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                "    { \"foreground\": \"teamA\", " +
                "\"background\": \"panelAlternate\", " +
                "\"minimumRatio\": 4.5 },\n",
                string.Empty,
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => UiThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void EventLogContrastPairsAreConfiguredAndPass()
    {
        _ = UiThemeCatalog.Load(BuiltInCatalogPath);
        using var document = System.Text.Json.JsonDocument.Parse(
            ReadCatalog());
        var pairs = document.RootElement
            .GetProperty("contrastPairs")
            .EnumerateArray()
            .Select(pair => (
                pair.GetProperty("foreground").GetString(),
                pair.GetProperty("background").GetString()))
            .ToHashSet();
        var expected = new[]
        {
            ("textInverse", "selection"),
            ("textInverse", "actionHover"),
            ("textDisabled", "panelAlternate"),
            ("teamA", "panelAlternate"),
            ("teamB", "panelAlternate"),
            ("otherFaction", "panelAlternate"),
            ("statusSuccess", "panelAlternate"),
            ("newEvent", "panelAlternate"),
            ("textDisabled", "panelSurface"),
            ("statusInfo", "panelSurface"),
            ("statusWarning", "panelSurface"),
            ("statusDanger", "panelSurface"),
            ("otherFaction", "panelSurface"),
            ("statusSuccess", "panelSurface"),
        };

        foreach (var pair in expected)
        {
            Assert.Contains(pair, pairs);
        }
    }

    [Fact]
    public void RejectsDuplicateContrastPair()
    {
        var json = ReadCatalog()
            .Replace(
                "  \"contrastPairs\": [",
                "  \"contrastPairs\": [\n" +
                "    { \"foreground\": \"textPrimary\", " +
                "\"background\": \"panelSurface\", " +
                "\"minimumRatio\": 4.5 },",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => UiThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void RejectsUnknownContrastPairRole()
    {
        var json = ReadCatalog()
            .Replace(
                "\"foreground\": \"textPrimary\"",
                "\"foreground\": \"unknownText\"",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => UiThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void RejectsStandardsThatDivergeFromTypedContract()
    {
        var json = ReadCatalog()
            .Replace(
                "      \"newEvent\"\r\n",
                "      \"notRuntimeColor\"\r\n",
                StringComparison.Ordinal)
            .Replace(
                "      \"newEvent\"\n",
                "      \"notRuntimeColor\"\n",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => UiThemeCatalog.LoadFromJson(json));
    }

    [Fact]
    public void InvalidShippedCatalogUsesCompleteCommandFallback()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"missing-theme-catalog-{Guid.NewGuid():N}.json");

        var catalog = UiThemeCatalog.LoadOrFallback(missingPath);

        Assert.Equal("command", catalog.DefaultThemeId);
        Assert.Equal(5, catalog.Themes.Count);
        Assert.Equal(
            catalog.GetRequired("command").Colors,
            catalog.GetRequired("signal").Colors);
    }

    [Fact]
    public void StandardsExposeTheArmyCompositionLayout()
    {
        var catalog = UiThemeCatalog.Load(BuiltInCatalogPath);

        var layout = catalog.Standards.Shared.ArmyComposition;

        Assert.Equal(420, layout.PanelWidth);
        Assert.Equal(560, layout.PanelHeight);
        Assert.Equal(44, layout.RowHeight);
        Assert.Equal(8, layout.RowGap);
        Assert.Equal(260, layout.StepperWidth);
        Assert.Equal(44, layout.ArrowWidth);
    }

    [Fact]
    public void ValidationRejectsAMissingArmyCompositionLayout()
    {
        var document = JsonNode.Parse(ReadCatalog())!.AsObject();
        var shared = document["standards"]!["shared"]!.AsObject();
        shared.Remove("armyComposition");

        Assert.Throws<InvalidDataException>(
            () => UiThemeCatalog.LoadFromJson(document.ToJsonString()));
    }

    [Fact]
    public void TheFallbackCatalogIncludesTheArmyCompositionLayout()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"missing-theme-catalog-{Guid.NewGuid():N}.json");

        var catalog = UiThemeCatalog.LoadOrFallback(missingPath);

        var layout = catalog.Standards.Shared.ArmyComposition;
        Assert.True(layout.PanelWidth > 0);
        Assert.True(layout.PanelHeight > 0);
        Assert.True(layout.RowHeight > 0);
        Assert.True(
            layout.ArrowWidth >= catalog.Standards.MetricRanges.TargetSize.Minimum &&
            layout.ArrowWidth <= catalog.Standards.MetricRanges.TargetSize.Maximum);
        Assert.True(layout.ArrowWidth <= layout.StepperWidth);
    }

    private static string ReadCatalog() =>
        File.ReadAllText(BuiltInCatalogPath);
}
