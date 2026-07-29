using Hukbo.Client.Theming;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// Shared faction-to-color lookups used by the inspector, the event log,
/// and pawn rendering. Theme colors read the active <see cref="UiTheme"/>
/// semantic roles and vary per theme; pawn colors are fixed regardless of
/// theme because pawn silhouettes are painted directly on the arena canvas,
/// not through a themed panel surface. <see cref="GetContingentGroundTint"/>
/// is the one exception: it derives a per-contingent lightness step from
/// whatever color its caller supplies, so it varies exactly when that color
/// is itself a theme role — <c>TeamA</c> or <c>TeamB</c> — rather than one of
/// the fixed pawn constants below.
/// </summary>
internal static class FactionColorPalette
{
    private static readonly Color PawnFactionAColor = new(64, 164, 255);
    private static readonly Color PawnFactionBColor = new(255, 91, 105);
    private static readonly Color PawnOtherFactionColor = new(231, 199, 84);

    /// <summary>
    /// The number of contingent tint steps <see cref="GetContingentGroundTint"/>
    /// derives per faction. Mirrors
    /// <c>Hukbo.Core.Simulation.FormationPlanner.MaximumContingents</c>,
    /// restated here because that constant is <c>internal</c> to
    /// <c>Hukbo.Core</c> and not visible across the assembly boundary.
    /// </summary>
    internal const int MaximumContingentTints = 8;

    /// <summary>
    /// Game-design choice, not a historical measurement: the fraction of the
    /// distance toward white each contingent tint step moves. <c>0.08f</c>
    /// was chosen empirically so that, for every one of the five shipped
    /// themes' <c>TeamA</c> and <c>TeamB</c> role, the eight per-faction
    /// steps this produces stay pairwise distinct and none of them collides
    /// with the other faction's unmodified base color — see
    /// <c>FactionColorPaletteTests</c>, which checks both properties against
    /// every shipped theme rather than trusting the arithmetic alone.
    /// </summary>
    private const float ContingentLightnessStepFraction = 0.08f;

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

    /// <summary>
    /// Derives the pawn ground-base tint for one contingent, presentation
    /// only. <paramref name="baseColor"/> is the color the caller would
    /// otherwise draw unmodified — ordinarily a faction's <c>TeamA</c> or
    /// <c>TeamB</c> role from the active <see cref="UiTheme"/> — and
    /// contingent 0 always returns it unchanged, matching
    /// <paramref name="contingentId"/>'s meaning as "the contingent this
    /// warrior was dealt into at spawn" (<see cref="AgentView.ContingentId"/>).
    /// Every later contingent steps one fixed increment of lightness toward
    /// white via <see cref="ContingentLightnessStepFraction"/>.
    /// </summary>
    /// <remarks>
    /// Under <see cref="ContingentState.None"/> — the value every agent's
    /// <see cref="AgentView.ContingentState"/> carries under
    /// <c>IndependentPursuitV1</c> — this returns
    /// <paramref name="baseColor"/> unmodified regardless of
    /// <paramref name="contingentId"/>, which is otherwise nonzero even under
    /// the frozen preset (contingent membership is dealt at spawn by
    /// <c>FormationPlanner</c> whether or not a preset ever reads it). That
    /// gate is what keeps a run under the frozen preset looking exactly as it
    /// looks today, for every theme, regardless of which contingent a pawn
    /// was dealt into.
    /// </remarks>
    internal static Color GetContingentGroundTint(
        Color baseColor,
        int contingentId,
        ContingentState contingentState) =>
        contingentState == ContingentState.None
            ? baseColor
            : Color.Lerp(
                baseColor,
                Color.White,
                contingentId * ContingentLightnessStepFraction);
}
