using Hukbo.Client.Settings;

namespace Hukbo.Client.Theming;

internal sealed class UiThemeManager
{
    private readonly UiThemeCatalog _catalog;
    private readonly Func<string, bool> _persist;

    public UiThemeManager(
        UiThemeCatalog catalog,
        ClientSettingsStore settingsStore)
        : this(
            catalog,
            settingsStore.Load(catalog.DefaultThemeId).SelectedThemeId,
            themeId => TryPersistTheme(settingsStore, catalog, themeId))
    {
    }

    internal UiThemeManager(
        UiThemeCatalog catalog,
        string? initialThemeId,
        Func<string, bool> persist)
    {
        _catalog = catalog;
        _persist = persist;
        ActiveTheme = catalog.TryGet(initialThemeId, out var initial)
            ? initial
            : catalog.GetRequired(catalog.DefaultThemeId);
    }

    /// <summary>
    /// Re-reads the whole settings file at save time so that persisting a
    /// theme change carries forward the army composition and gore intensity a
    /// panel may have written since this manager was constructed, rather than
    /// overwriting them with values captured at startup.
    /// </summary>
    private static bool TryPersistTheme(
        ClientSettingsStore settingsStore,
        UiThemeCatalog catalog,
        string themeId)
    {
        var current = settingsStore.Load(catalog.DefaultThemeId);
        return settingsStore.TrySave(
            themeId,
            current.Composition,
            current.GoreIntensity);
    }

    public UiTheme ActiveTheme { get; private set; }

    public IReadOnlyList<UiTheme> Themes => _catalog.Themes;

    public UiThemeStandards Standards => _catalog.Standards;

    public bool TrySelect(string id)
    {
        if (!_catalog.TryGet(id, out var theme) ||
            theme.Id == ActiveTheme.Id)
        {
            return false;
        }

        ActiveTheme = theme;
        _persist(theme.Id);
        return true;
    }
}
