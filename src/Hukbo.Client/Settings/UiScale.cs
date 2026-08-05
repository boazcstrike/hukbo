namespace Hukbo.Client.Settings;

/// <summary>
/// Spectator-selectable preferred scale for UI typography, chrome, and hit
/// targets. The viewport policy caps a preference at the largest tier that
/// fits the current client area.
/// Numeric values are part of the persisted settings-file contract; do not
/// renumber or reuse them, or a stored preference can silently resolve to a
/// different scale after an upgrade.
/// </summary>
public enum UiScale
{
    /// <summary>
    /// Select the largest supported tier that fits the current viewport.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Prefer the baseline UI asset and layout tier.
    /// </summary>
    Percent100 = 100,

    /// <summary>
    /// Prefer the 125 percent UI asset and layout tier.
    /// </summary>
    Percent125 = 125,

    /// <summary>
    /// Prefer the 150 percent UI asset and layout tier.
    /// </summary>
    Percent150 = 150,

    /// <summary>
    /// Prefer the 200 percent UI asset and layout tier.
    /// </summary>
    Percent200 = 200,
}
