using Hukbo.Client.Settings;

namespace Hukbo.Client.UI;

/// <summary>
/// Bounded entrance feedback for client-only panels and modal overlays.
/// Interaction geometry is never derived from these values.
/// </summary>
internal sealed class UiEntranceMotion
{
    internal static readonly TimeSpan ScrimDuration =
        TimeSpan.FromMilliseconds(110);
    internal static readonly TimeSpan ModalPanelDuration =
        TimeSpan.FromMilliseconds(160);
    internal static readonly TimeSpan SummaryPanelDuration =
        TimeSpan.FromMilliseconds(200);
    internal static readonly TimeSpan ReportPanelDuration =
        TimeSpan.FromMilliseconds(150);

    private UiTransition _scrim;
    private UiTransition _panel;

    public float ScrimOpacity => _scrim.Value;

    public float PanelOpacity => _panel.Value;

    public void Begin()
    {
        _scrim = default;
        _panel = default;
    }

    public void Advance(
        TimeSpan elapsed,
        MotionIntensity intensity,
        TimeSpan panelDuration,
        bool hasScrim)
    {
        var normalizedIntensity = Enum.IsDefined(intensity)
            ? intensity
            : MotionIntensity.Off;
        var isMotionEnabled = normalizedIntensity != MotionIntensity.Off;
        _panel.AdvanceTo(
            1f,
            elapsed,
            panelDuration,
            isMotionEnabled);
        _scrim.AdvanceTo(
            hasScrim ? 1f : 0f,
            elapsed,
            ScrimDuration,
            isMotionEnabled);
    }

    public void Reset()
    {
        _scrim = default;
        _panel = default;
    }
}
