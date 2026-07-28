namespace Hukbo.Client.Settings;

/// <summary>
/// Spectator-selectable governor for ambient presentation motion (grass sway
/// today; dust and any future ambient motion fall under the same switch per
/// OD-8). Numeric values are part of the persisted settings-file contract; do
/// not renumber or reorder, or a stored preference will silently resolve to a
/// different level after an upgrade.
/// </summary>
public enum MotionIntensity
{
    /// <summary>
    /// All ambient presentation motion this setting governs is suppressed.
    /// Swings and hit effects are gameplay communication and stay exempt.
    /// </summary>
    Off = 0,

    /// <summary>
    /// A reduced level of ambient presentation motion. Per OD-9, if dust
    /// later ships, <see cref="Reduced"/> leaves dust spawning unchanged.
    /// </summary>
    Reduced = 1,

    /// <summary>
    /// The default: full ambient presentation motion.
    /// </summary>
    Full = 2,
}
