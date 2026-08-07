using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.UI;

/// <summary>
/// Produces a short-lived, opacity-adjusted presentation theme while a
/// surface enters. Settled surfaces reuse the original theme and allocate
/// nothing.
/// </summary>
internal static class UiMotionTheme
{
    public static UiTheme WithOpacity(UiTheme theme, float opacity)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var amount = float.IsFinite(opacity)
            ? Math.Clamp(opacity, 0f, 1f)
            : 0f;
        if (amount >= 1f)
        {
            return theme;
        }

        Color Fade(Color color) => color * amount;
        var colors = theme.Colors;
        return theme with
        {
            Colors = new UiThemeColors(
                Fade(colors.CanvasBackground),
                Fade(colors.ArenaSurface),
                Fade(colors.ArenaBorder),
                Fade(colors.StatusSurface),
                Fade(colors.OverlayScrim),
                Fade(colors.PanelSurface),
                Fade(colors.PanelAlternate),
                Fade(colors.PanelBorder),
                Fade(colors.TextPrimary),
                Fade(colors.TextSecondary),
                Fade(colors.TextDisabled),
                Fade(colors.TextInverse),
                Fade(colors.ActionDefault),
                Fade(colors.ActionHover),
                Fade(colors.ActionFocus),
                Fade(colors.ActionPressed),
                Fade(colors.ActionActive),
                Fade(colors.ActionDisabled),
                Fade(colors.StatusInfo),
                Fade(colors.StatusSuccess),
                Fade(colors.StatusWarning),
                Fade(colors.StatusDanger),
                Fade(colors.TeamA),
                Fade(colors.TeamB),
                Fade(colors.OtherFaction),
                Fade(colors.Selection),
                Fade(colors.NewEvent)),
        };
    }
}
