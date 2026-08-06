using Hukbo.Client.Settings;

namespace Hukbo.Client.UI;

/// <summary>
/// Shared duration constants for the UI-52 secondary-feedback surfaces —
/// new-event row emphasis, selected-agent accent, selector arrow and marker
/// interpolation, control-bar active strip, and status-badge emphasis. Each
/// value is the midpoint of its published band in
/// <c>docs/plans/2026-08-07-ui-52-secondary-feedback-design.md</c> §4.4,
/// rounded to a round number of milliseconds. Kept in one class so every
/// UI-52 duration can be asserted inside its band with a single test.
/// </summary>
internal static class UiSecondaryMotion
{
    /// <summary>Surface A — new-event row emphasis. Band 160-220 ms.</summary>
    internal static readonly TimeSpan NewEventDuration =
        TimeSpan.FromMilliseconds(200);

    /// <summary>Surface B — selected-agent accent. Band 140-180 ms.</summary>
    internal static readonly TimeSpan SelectionAccentDuration =
        TimeSpan.FromMilliseconds(160);

    /// <summary>Surface C — selector arrow and marker. Band 90-140 ms.</summary>
    internal static readonly TimeSpan SelectorMarkerDuration =
        TimeSpan.FromMilliseconds(120);

    /// <summary>Surface D — control-bar active strip. Band 100-140 ms.</summary>
    internal static readonly TimeSpan ActiveStripDuration =
        TimeSpan.FromMilliseconds(120);

    /// <summary>Surface E — status-badge emphasis. Band 450-650 ms.</summary>
    internal static readonly TimeSpan StatusBadgeDuration =
        TimeSpan.FromMilliseconds(550);

    /// <summary>
    /// Normalizes an out-of-range <see cref="MotionIntensity"/> value to
    /// <see cref="MotionIntensity.Off"/>, matching the normalization already
    /// performed by <see cref="UiButtonMotion.Advance"/> and
    /// <see cref="UiEntranceMotion.Advance"/>.
    /// </summary>
    internal static bool IsEnabled(MotionIntensity intensity)
    {
        var normalized = Enum.IsDefined(intensity)
            ? intensity
            : MotionIntensity.Off;
        return normalized != MotionIntensity.Off;
    }
}
