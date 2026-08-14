namespace Hukbo.Client.Settings;

/// <summary>
/// Spectator-selectable way panel chrome is drawn: the existing flat
/// rectangles, or a nine-slice sprite skin sourced from a texture atlas.
/// Numeric values are part of the persisted settings-file contract; do not
/// renumber or reuse them, or a stored preference can silently resolve to a
/// different style after an upgrade.
/// </summary>
public enum UiChromeStyle
{
    /// <summary>
    /// Draw panel chrome as a filled rectangle plus a one-pixel border, the
    /// look every panel had before this setting existed.
    /// </summary>
    Procedural = 0,

    /// <summary>
    /// Draw panel chrome as a nine-slice sprite skin sourced from the chrome
    /// atlas, tinted by the active theme's surface and border colours.
    /// </summary>
    NineSlice = 1,
}
