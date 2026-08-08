using Microsoft.Xna.Framework;

namespace Sandata.Client.Theming;

/// <summary>
/// The three theme-independent faction colors: friendly, hostile, and
/// unknown contact. Mirrors the discipline in
/// <c>Hukbo.Client.UI.FactionColorPalette</c>, whose pawn colors are fixed
/// regardless of theme because a spectator's ability to tell friend from foe
/// must never depend on which theme happens to be active.
/// </summary>
/// <remarks>
/// Hukbo's <c>UiThemeColors.TeamA</c>/<c>TeamB</c>/<c>OtherFaction</c> do
/// vary per theme, because those roles paint themed UI surfaces (the event
/// log, the roster), not the pawn itself; <c>FactionColorPalette</c> keeps a
/// separate, fixed set of pawn colors for exactly that reason.
/// <see cref="SandataThemeColors.Friendly"/>,
/// <see cref="SandataThemeColors.Hostile"/>, and
/// <see cref="SandataThemeColors.UnknownContact"/> take the opposite
/// approach: design section 11 declares them theme-independent constants
/// outright, so every shipped theme document is required to assign them
/// exactly these three values (enforced by
/// <see cref="SandataThemeCatalog.LoadFromJson"/>) rather than each theme
/// choosing its own. The typed constants below are what the operator
/// renderer, the contact list, and the fire cone overlay read directly, with
/// no theme lookup involved.
/// </remarks>
internal static class SandataFactionPalette
{
    internal static readonly Color Friendly = new(64, 164, 255);

    internal static readonly Color Hostile = new(255, 91, 105);

    internal static readonly Color UnknownContact = new(231, 199, 84);
}
