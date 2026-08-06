using Hukbo.Client.Settings;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// Surface E — status-badge emphasis. Tracks the last observed
/// <see cref="BattleOutcome"/> and playing/paused state and triggers a
/// one-shot, non-looping <see cref="UiEmphasisPulse"/> only when one of
/// those two facts changes. A tick-count or alive-count change does not
/// trigger it, so the badge never becomes an idle animation. The very
/// first <see cref="Observe"/> call seeds the recorded state without
/// pulsing, matching the "colour and opacity only" decision for UI-52
/// secondary feedback.
/// </summary>
internal sealed class UiStatusBadgeMotion
{
    private UiEmphasisPulse _pulse;
    private BattleOutcome? _lastOutcome;
    private bool? _lastIsPlaying;

    public float Amount => _pulse.Amount;

    /// <summary>
    /// Observes the current outcome and playing state. Call once per frame
    /// with unscaled elapsed time and the active <see cref="MotionIntensity"/>.
    /// </summary>
    public void Observe(
        BattleOutcome outcome,
        bool isPlaying,
        TimeSpan elapsed,
        MotionIntensity intensity)
    {
        var isMotionEnabled = UiSecondaryMotion.IsEnabled(intensity);
        var hasChanged = _lastOutcome.HasValue
            && (_lastOutcome != outcome || _lastIsPlaying != isPlaying);

        if (hasChanged)
        {
            _pulse.Trigger(isMotionEnabled);
        }

        _lastOutcome = outcome;
        _lastIsPlaying = isPlaying;

        _pulse.Advance(elapsed, UiSecondaryMotion.StatusBadgeDuration, isMotionEnabled);
    }

    /// <summary>
    /// Resolves the status bar's fill colour by amount alone, with no
    /// positional change, matching the colour-and-opacity-only decision for
    /// UI-52 secondary feedback.
    /// </summary>
    internal static Color GetBarColor(
        Color statusSurface,
        Color statusInfo,
        float pulseAmount) =>
        Color.Lerp(statusSurface, statusInfo, pulseAmount);
}
