namespace Hukbo.Client.Settings;

/// <summary>
/// Spectator-selectable blood rendering level. Numeric values are part of the
/// persisted settings-file contract; do not renumber or reorder, or a stored
/// preference will silently resolve to a different level after an upgrade.
/// </summary>
internal enum GoreIntensity
{
    /// <summary>
    /// No blood is drawn. Ingest and advance are no-ops and no burst, ground
    /// mark, or spurt slot is ever occupied.
    /// </summary>
    Off = 0,

    /// <summary>
    /// The default: directional impact spray plus fading ground marks.
    /// </summary>
    Stylized = 1,

    /// <summary>
    /// Adds a sustained spurt on lethal blows and longer-lived, denser ground
    /// marks on top of the <see cref="Stylized"/> presentation.
    /// </summary>
    Full = 2,
}
