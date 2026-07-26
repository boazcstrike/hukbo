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
            settingsStore.TrySave)
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
