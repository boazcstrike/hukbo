using Microsoft.Xna.Framework;

namespace Sandata.Client.Theming;

/// <summary>
/// One shipped Sandata theme: an identifier, a display name, its semantic
/// colors, and its visual metrics. Mirrors the shape of
/// <c>Hukbo.Client.Theming.UiTheme</c>, but declares its own record rather
/// than reusing Hukbo's — see <see cref="SandataThemeColors"/>.
/// </summary>
internal sealed record SandataTheme(
    string Id,
    string DisplayName,
    SandataThemeColors Colors,
    SandataThemeMetrics Metrics);

/// <summary>
/// Sandata's own 39-role semantic color record
/// (<c>docs/plans/2026-08-07-sandata-scaffold-design.md</c> section 11).
/// Deliberately independent of
/// <c>Hukbo.Client.Theming.UiThemeColors</c>'s 27 roles: bolting tactical
/// roles onto the shared record would force every melee theme to author
/// meaningless colors or break that catalog's exact-role-count invariant.
/// </summary>
/// <remarks>
/// The 39 roles break down as the design document specifies: 23 kept with
/// the same name and meaning as the corresponding Hukbo role, 4 repurposed
/// under a tactical name (<c>TeamA</c> to <see cref="Friendly"/>,
/// <c>TeamB</c> to <see cref="Hostile"/>, <c>OtherFaction</c> to
/// <see cref="UnknownContact"/>, <c>Selection</c> to
/// <see cref="SelectedTrooper"/>), and 12 newly added for tactical
/// presentation. <see cref="Friendly"/>, <see cref="Hostile"/>, and
/// <see cref="UnknownContact"/> are still declared here so every shipped
/// theme document carries a value for them and the catalog's missing/unknown
/// role check covers them, but <see cref="SandataThemeCatalog"/> additionally
/// requires their value to be identical in every shipped theme and to match
/// <see cref="SandataFactionPalette"/> — see that type's remarks for why a
/// theme is not allowed to vary friend-or-foe recognition.
/// </remarks>
internal sealed record SandataThemeColors(
    // 23 kept unchanged from Hukbo.Client.Theming.UiThemeColors.
    Color CanvasBackground,
    Color ArenaSurface,
    Color ArenaBorder,
    Color StatusSurface,
    Color OverlayScrim,
    Color PanelSurface,
    Color PanelAlternate,
    Color PanelBorder,
    Color TextPrimary,
    Color TextSecondary,
    Color TextDisabled,
    Color TextInverse,
    Color ActionDefault,
    Color ActionHover,
    Color ActionFocus,
    Color ActionPressed,
    Color ActionActive,
    Color ActionDisabled,
    Color StatusInfo,
    Color StatusSuccess,
    Color StatusWarning,
    Color StatusDanger,
    Color NewEvent,
    // 4 repurposed from Hukbo's TeamA, TeamB, OtherFaction, Selection.
    Color Friendly,
    Color Hostile,
    Color UnknownContact,
    Color SelectedTrooper,
    // 12 added for tactical presentation.
    Color Suppressed,
    Color Downed,
    Color OrderPath,
    Color Waypoint,
    Color CoverGood,
    Color CoverNone,
    Color BreachPoint,
    Color FireConeFill,
    Color FireConeEdge,
    Color AlertCalm,
    Color AlertRaised,
    Color AlertBreach);

/// <summary>
/// Visual metrics for one theme. Mirrors
/// <c>Hukbo.Client.Theming.UiThemeMetrics</c> field for field; Sandata's
/// design does not call for a different metric shape.
/// </summary>
internal sealed record SandataThemeMetrics(
    int BorderThickness,
    int FocusThickness,
    int ShadowOffset);
