using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// Shared faction-to-color lookups used by the inspector, the event log,
/// and pawn rendering. Theme colors read the active <see cref="UiTheme"/>
/// semantic roles and vary per theme; pawn colors are fixed regardless of
/// theme because pawn silhouettes are painted directly on the arena canvas,
/// not through a themed panel surface.
/// </summary>
internal static class FactionColorPalette
{
    private static readonly Color PawnFactionAColor = new(64, 164, 255);
    private static readonly Color PawnFactionBColor = new(255, 91, 105);
    private static readonly Color PawnOtherFactionColor = new(231, 199, 84);

    internal static Color GetThemeColor(
        int? factionId,
        UiTheme theme,
        Color otherFactionColor) =>
        factionId switch
        {
            0 => theme.Colors.TeamA,
            1 => theme.Colors.TeamB,
            _ => otherFactionColor,
        };

    internal static Color GetPawnColor(int factionId) =>
        factionId switch
        {
            0 => PawnFactionAColor,
            1 => PawnFactionBColor,
            _ => PawnOtherFactionColor,
        };
}
