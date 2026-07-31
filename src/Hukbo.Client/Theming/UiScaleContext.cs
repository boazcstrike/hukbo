using Hukbo.Client.Settings;

namespace Hukbo.Client.Theming;

/// <summary>
/// The client thread's resolved presentation scale. Hukbo's UI is
/// immediate-mode and single-threaded, so one bounded context lets every
/// layout surface use the same integer geometry without threading scale
/// arguments through simulation-facing APIs. It never enters Core state.
/// </summary>
internal static class UiScaleContext
{
    private static UiScale _activeScale = UiScale.Percent100;

    public static UiScale ActiveScale => _activeScale;

    public static void Set(UiScale resolved)
    {
        _ = UiScalePolicy.GetPercent(resolved);
        _activeScale = resolved;
    }

    public static int Pixels(int baseline)
    {
        var scaled = baseline * (UiScalePolicy.GetPercent(_activeScale) / 100d);
        return (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
    }
}
