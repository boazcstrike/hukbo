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
                new UiIntegerRange(44, 64)),
            [
                "Fonts/UiCaption",
                "Fonts/UiBody",
                "Fonts/UiLabel",
                "Fonts/UiSubtitle",
                "Fonts/UiTitle",
                "Fonts/UiDisplay",
            ],
            new UiSharedStandards(
                new UiMenuLayout(
                    // menu.panelHeight: 660 -> 688. Grows by the same 28px
                    // delta as subtitleTopOffset below, so the gore selector
                    // and the helper line keep their original clearance from
                    // the panel bottom. See ui-theme-standards.json for the
                    // full derivation.
                    360,
                    688,
                    280,
                    44,
                    8,
                    // menu.titleTopOffset unchanged; Display bake (rung
                    // Display, line spacing 61) still centres at y=42, giving
                    // a title box of y=12..73.
                    42,
                    // menu.subtitleTopOffset: 72 -> 100. Subtitle bake (rung
                    // Subtitle, line spacing 34) centred at y=100 gives a box
                    // of y=83..117, clearing the title box bottom (73) by a
                    // visible 10px gap instead of overlapping it by 18px.
                    100,
                    // menu.selectorTopOffset: 94 -> 122. Cascaded by the same
                    // 28px delta as subtitleTopOffset so the theme selector
                    // keeps its original 5px clearance below the subtitle box
                    // bottom (117 + 5 = 122).
                    122,
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
                new UiArmyCompositionLayout(
                    420,
                    648,
                    44,
                    8,
                    260,
                    44),
                new UiFontAssignments(
                    "Fonts/UiCaption",
                    "Fonts/UiBody",
                    "Fonts/UiLabel",
                    "Fonts/UiSubtitle",
                    "Fonts/UiTitle",
                    "Fonts/UiDisplay"),
                new UiTextRoles(
                    UiFontRole.Display,
                    UiFontRole.Subtitle,
                    UiFontRole.Label,
                    UiFontRole.Caption,
                    UiFontRole.Subtitle,
                    UiFontRole.Caption,
                    UiFontRole.Label,
                    UiFontRole.Caption)));
        return new UiThemeCatalog("command", themes, standards);
    }
}
