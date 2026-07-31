namespace Hukbo.Client.Settings;

/// <summary>
/// Spectator-selected display mode applied when the client next starts.
/// Numeric values are part of the persisted settings-file contract; do not
/// renumber or reorder them, or a stored preference can silently resolve to a
/// different mode after an upgrade.
/// </summary>
public enum StartupDisplayMode
{
    /// <summary>
    /// Start with the existing resizable borderless window behavior.
    /// </summary>
    Windowed = 0,

    /// <summary>
    /// Start in soft fullscreen without a hardware display-mode switch.
    /// </summary>
    Fullscreen = 1,
}
