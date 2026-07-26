using Microsoft.Xna.Framework;

namespace Hukbo.Client.Theming;

/// <summary>
/// The built-in catalog <see cref="UiThemeCatalog.LoadOrFallback"/> returns
/// when the content-shipped JSON cannot be read or fails validation. Split
/// out of the main validator file to keep it under the file-size cap.
/// </summary>
internal sealed partial class UiThemeCatalog
{
    private static UiThemeCatalog CreateFallback()
    {
        var commandColors = new UiThemeColors(
            new Color(8, 13, 22),
            new Color(19, 29, 43),
            new Color(116, 145, 178),
            new Color(12, 19, 30),
            new Color(4, 8, 16, 190),
            new Color(22, 31, 46),
            new Color(30, 42, 59),
            new Color(115, 144, 178),
            Color.White,
            new Color(190, 208, 226),
            new Color(142, 157, 173),
            new Color(6, 16, 26),
            new Color(98, 184, 255),
            new Color(124, 202, 255),
            new Color(255, 212, 90),
            new Color(70, 146, 199),
            new Color(82, 211, 168),
            new Color(52, 67, 84),
            new Color(91, 190, 255),
            new Color(82, 211, 168),
            new Color(255, 212, 90),
            new Color(255, 107, 120),
            new Color(64, 164, 255),
            new Color(255, 91, 105),
            new Color(231, 199, 84),
            new Color(255, 212, 90),
            new Color(255, 212, 90));
        var metrics = new UiThemeMetrics(2, 3, 2);
        var names = new[]
        {
            "Command",
            "Field Manual",
            "Signal",
            "Broadcast",
            "High Contrast",
        };
        var themeIds = new[]
        {
            "command",
            "field-manual",
            "signal",
            "broadcast",
            "high-contrast",
        };
        var themes = themeIds
            .Select((id, index) =>
                new UiTheme(id, names[index], commandColors, metrics))
            .ToArray();
        var standards = new UiThemeStandards(
            5,
            themeIds,
            RuntimeColorRoles,
            RuntimeInteractionStates,
            new UiMetricRanges(
                new UiIntegerRange(1, 4),
                new UiIntegerRange(2, 5),
                new UiIntegerRange(0, 6),
                new UiIntegerRange(44, 64),
                new UiNumberRange(0.45d, 1.25d)),
            ["Default"],
            new UiSharedStandards(
                "Default",
                new UiMenuLayout(
                    360,
                    590,
                    280,
                    44,
                    8,
                    42,
                    72,
                    94,
                    14,
                    23),
                new UiThemeSelectorLayout(
                    96,
                    44,
                    10,
                    44,
                    18,
                    43,
                    68,
                    22,
                    7,
                    5),
                new UiTextScales(
                    1f,
                    1f,
                    0.78f,
                    0.58f,
                    1.15f,
                    0.58f,
                    0.82f,
                    0.56f)));
        return new UiThemeCatalog("command", themes, standards);
    }
}
