namespace Hukbo.Client.Settings;

internal sealed record ClientSettings(
    int SchemaVersion,
    string SelectedThemeId);
