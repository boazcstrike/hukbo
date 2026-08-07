using Hukbo.Client.Settings;

namespace Hukbo.Client.Theming;

/// <summary>
/// Resolves the persisted UI-scale preference into one of the pre-baked font
/// tiers. Auto uses both viewport axes so an ultrawide-but-short window never
/// selects a tier whose text cannot fit vertically.
/// </summary>
internal static class UiScalePolicy
{
    public static UiScale Resolve(UiScale configured, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        UiScale maximum;
        if (width >= 3840 && height >= 2160)
        {
            maximum = UiScale.Percent200;
        }
        else if (width >= 2560 && height >= 1440)
        {
            maximum = UiScale.Percent150;
        }
        else
        {
            maximum = width >= 1920 && height >= 1080
                ? UiScale.Percent125
                : UiScale.Percent100;
        }

        if (configured == UiScale.Auto)
        {
            return maximum;
        }

        return GetPercent(configured) <= GetPercent(maximum)
            ? configured
            : maximum;
    }

    public static int GetPercent(UiScale resolved) => resolved switch
    {
        UiScale.Percent100 => 100,
        UiScale.Percent125 => 125,
        UiScale.Percent150 => 150,
        UiScale.Percent200 => 200,
        _ => throw new ArgumentOutOfRangeException(
            nameof(resolved),
            resolved,
            "Auto must be resolved against a viewport before use."),
    };
}
