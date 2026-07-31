using Hukbo.Client.Theming;

namespace Hukbo.Client.Tests;

public sealed class UiThemeManagerTests
{
    [Fact]
    public void SelectChangesThemeImmediatelyAndPersistsStableId()
    {
        string? savedId = null;
        var manager = CreateManager(
            "command",
            id =>
            {
                savedId = id;
                return true;
            });

        var changed = manager.TrySelect("signal");

        Assert.True(changed);
        Assert.Equal("signal", manager.ActiveTheme.Id);
        Assert.Equal("signal", savedId);
    }

    [Fact]
    public void CurrentOrUnknownThemeDoesNotChangeOrPersist()
    {
        var saveCount = 0;
        var manager = CreateManager(
            "command",
            _ =>
            {
                saveCount++;
                return true;
            });

        Assert.False(manager.TrySelect("command"));
        Assert.False(manager.TrySelect("not-a-theme"));
        Assert.Equal("command", manager.ActiveTheme.Id);
        Assert.Equal(0, saveCount);
    }

    [Fact]
    public void UnknownInitialSettingFallsBackToCatalogDefault()
    {
        var manager = CreateManager("not-a-theme", _ => true);

        Assert.Equal("datu-court", manager.ActiveTheme.Id);
    }

    private static UiThemeManager CreateManager(
        string initialId,
        Func<string, bool> persist)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");
        return new UiThemeManager(
            UiThemeCatalog.Load(path),
            initialId,
            persist);
    }
}
