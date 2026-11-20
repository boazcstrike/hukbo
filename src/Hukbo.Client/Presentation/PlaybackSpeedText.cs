using System.Globalization;

namespace Hukbo.Client.Presentation;

/// <summary>
/// The playback speed as the status line and window title show it. While the
/// play-rate governor holds the battle below the requested speed, both numbers
/// appear so a battle that quietly plays slower is not a surprise (play-rate
/// governor design, section 3.5).
/// </summary>
internal static class PlaybackSpeedText
{
    /// <summary>
    /// <c>4x</c>, or <c>4x (holding 2.6x)</c> while <paramref name="isGoverned"/>.
    /// </summary>
    public static string Format(
        int requestedSpeed,
        double effectiveSpeed,
        bool isGoverned) =>
        isGoverned
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{requestedSpeed}x (holding {effectiveSpeed:0.0}x)")
            : string.Create(CultureInfo.InvariantCulture, $"{requestedSpeed}x");
}
