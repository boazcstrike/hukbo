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
        // Provisional reconstruction derived from documented Cebu 1521
        // material and color classes. Exact digital values are presentation
        // choices, not historical claims.
        var datuCourtColors = new UiThemeColors(
            new Color(18, 13, 10),
            new Color(74, 81, 56),
            new Color(195, 163, 90),
            new Color(29, 20, 16),
            new Color(18, 13, 10, 230),
            new Color(37, 25, 20),
            new Color(50, 35, 28),
            new Color(181, 138, 61),
            new Color(243, 230, 200),
            new Color(217, 199, 167),
            new Color(199, 183, 158),
            new Color(26, 17, 13),
            new Color(208, 166, 74),
            new Color(226, 190, 104),
            new Color(227, 188, 98),
            new Color(184, 137, 57),
            new Color(167, 181, 140),
            new Color(73, 58, 49),
            new Color(132, 175, 194),
            new Color(145, 178, 122),
            new Color(227, 188, 98),
            new Color(230, 120, 100),
            new Color(118, 184, 218),
            new Color(239, 119, 104),
            new Color(227, 188, 98),
            new Color(208, 166, 74),
            new Color(242, 205, 117));
        var metrics = new UiThemeMetrics(2, 3, 2);
        var names = new[]
        {
            "Command",
            "Field Manual",
            "Signal",
            "Broadcast",
            "High Contrast",
            "Cebu 1521 — Provisional",
        };
        var themeIds = new[]
        {
            "command",
            "field-manual",
            "signal",
            "broadcast",
            "high-contrast",
            "datu-court",
        };
        var themes = themeIds
            .Select((id, index) =>
                new UiTheme(
                    id,
                    names[index],
                    id == "datu-court" ? datuCourtColors : commandColors,
                    metrics))
            .ToArray();
        var standards = new UiThemeStandards(
            6,
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
                    // menu.panelHeight: 660 -> 688 -> 800 -> 912. Each step
                    // after 688 makes room for one more settings selector
                    // stacked below the gore selector: one selector height
                    // (96) plus one selector gap (14) apiece, plus a small
                    // margin. The 800 -> 912 step is the auto-camera mode
                    // selector. See ui-theme-standards.json for the full
                    // derivation.
                    360,
                    912,
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
                    640,
                    648,
                    44,
                    8,
                    148,
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
        return new UiThemeCatalog("datu-court", themes, standards);
    }
}
