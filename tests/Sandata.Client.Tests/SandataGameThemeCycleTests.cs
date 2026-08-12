using Hukbo.Diagnostics;
using Sandata.Client;
using Sandata.Client.Theming;

namespace Sandata.Client.Tests;

/// <summary>
/// Task 9's F6 cycle helper, <c>SandataGame.NextThemeId</c>: a pure function
/// over the catalog's own order and the currently displayed id, with no
/// <c>GraphicsDevice</c> and no keyboard state, so it is pinned directly here
/// rather than through a window-opening probe. Reuses one real shipped
/// theme's colors and metrics for every synthetic entry — only <c>Id</c>
/// varies — because this helper only ever looks at <see cref="SandataTheme.Id"/>.
/// </summary>
public sealed class SandataGameThemeCycleTests
{
    private static readonly SandataTheme Template = LoadTemplate();

    private static SandataTheme LoadTemplate()
    {
        var root = LogPaths.FindRepositoryRoot(AppContext.BaseDirectory) ??
            throw new InvalidOperationException(
                "Could not find the Hukbo.slnx repository root from the test output directory.");
        var path = Path.Combine(
            root, "src", "Sandata.Client", "Content", "Themes", "sandata-theme-standards.json");
        return SandataThemeCatalog.Load(path).Themes[0];
    }

    private static SandataTheme MakeTheme(string id) =>
        Template with { Id = id, DisplayName = id };

    [Fact]
    public void WrapsFromTheLastThemeBackToTheFirst()
    {
        var themes = new[] { MakeTheme("a"), MakeTheme("b"), MakeTheme("c") };

        var nextId = SandataGame.NextThemeId(themes, "c");

        Assert.Equal("a", nextId);
    }

    [Fact]
    public void AdvancesToTheNextThemeInCatalogOrder()
    {
        var themes = new[] { MakeTheme("a"), MakeTheme("b"), MakeTheme("c") };

        var nextId = SandataGame.NextThemeId(themes, "a");

        Assert.Equal("b", nextId);
    }

    [Fact]
    public void SingleEntryCatalogReturnsTheCurrentIdUnchanged()
    {
        var themes = new[] { MakeTheme("only") };

        var nextId = SandataGame.NextThemeId(themes, "only");

        Assert.Equal("only", nextId);
    }

    [Fact]
    public void EmptyCatalogReturnsTheCurrentIdUnchanged()
    {
        var nextId = SandataGame.NextThemeId(Array.Empty<SandataTheme>(), "anything");

        Assert.Equal("anything", nextId);
    }

    [Fact]
    public void UnknownCurrentIdDefensivelyReturnsTheFirstTheme()
    {
        var themes = new[] { MakeTheme("a"), MakeTheme("b") };

        var nextId = SandataGame.NextThemeId(themes, "not-in-the-list");

        Assert.Equal("a", nextId);
    }
}
