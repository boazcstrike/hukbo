namespace Hukbo.Client.Settings;

/// <summary>
/// Spectator-selectable governor for nonessential presentation motion,
/// including ambient battlefield effects and decorative UI transitions.
/// Numeric values are part of the persisted settings-file contract; do not
/// renumber or reorder, or a stored preference will silently resolve to a
/// different level after an upgrade.
/// </summary>
public enum MotionIntensity
{
    /// <summary>
    /// All nonessential presentation motion this setting governs is
    /// suppressed. UI states snap immediately. Swings and hit effects are
    /// gameplay communication and stay exempt.
    /// </summary>
    Off = 0,

    /// <summary>
    /// A reduced level of ambient presentation motion. UI feedback uses only
    /// color and opacity, with no positional movement. Per OD-9, dust spawning
    /// remains unchanged.
    /// </summary>
    Reduced = 1,

    /// <summary>
    /// The default: full ambient presentation motion.
    /// </summary>
    Full = 2,
}
