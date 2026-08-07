namespace Hukbo.Client.Settings;

/// <summary>
/// Spectator-selectable governor for the camera assistant that drifts the view
/// toward fighting the spectator cannot see. Numeric values are part of the
/// persisted settings-file contract; do not renumber or reorder, or a stored
/// preference will silently resolve to a different mode after an upgrade.
/// </summary>
public enum AutoCameraMode
{
    /// <summary>
    /// The camera never moves on its own. Only spectator input pans it.
    /// </summary>
    Off = 0,

    /// <summary>
    /// The default. The camera holds still while any fighting is on screen and
    /// travels only once the screen has been empty of fighting long enough
    /// that a spectator would have gone looking themselves.
    /// </summary>
    Assisted = 1,

    /// <summary>
    /// The camera keeps the nearest melee near the middle of the screen,
    /// re-centring sooner and settling for less time than
    /// <see cref="Assisted"/>. Frequent motion is the point of this mode.
    /// </summary>
    Follow = 2,
}
