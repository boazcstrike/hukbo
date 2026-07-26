using Microsoft.Xna.Framework;

namespace Hukbo.Client.Theming;

/// <summary>
/// Color parsing and contrast math for <see cref="UiThemeCatalog"/>. Split
/// out of the main validator file to keep it under the file-size cap.
/// </summary>
internal sealed partial class UiThemeCatalog
{
    private static Color GetColor(UiThemeColors colors, string role) =>
        role switch
        {
            "canvasBackground" => colors.CanvasBackground,
            "arenaSurface" => colors.ArenaSurface,
            "arenaBorder" => colors.ArenaBorder,
            "statusSurface" => colors.StatusSurface,
            "overlayScrim" => colors.OverlayScrim,
            "panelSurface" => colors.PanelSurface,
            "panelAlternate" => colors.PanelAlternate,
            "panelBorder" => colors.PanelBorder,
            "textPrimary" => colors.TextPrimary,
            "textSecondary" => colors.TextSecondary,
            "textDisabled" => colors.TextDisabled,
            "textInverse" => colors.TextInverse,
            "actionDefault" => colors.ActionDefault,
            "actionHover" => colors.ActionHover,
            "actionFocus" => colors.ActionFocus,
            "actionPressed" => colors.ActionPressed,
            "actionActive" => colors.ActionActive,
            "actionDisabled" => colors.ActionDisabled,
            "statusInfo" => colors.StatusInfo,
            "statusSuccess" => colors.StatusSuccess,
            "statusWarning" => colors.StatusWarning,
            "statusDanger" => colors.StatusDanger,
            "teamA" => colors.TeamA,
            "teamB" => colors.TeamB,
            "otherFaction" => colors.OtherFaction,
            "selection" => colors.Selection,
            "newEvent" => colors.NewEvent,
            _ => throw new InvalidDataException($"Unknown color role '{role}'."),
        };

    private static Color ParseColor(string value) =>
        TryParseColor(value, out var color)
            ? color
            : throw new InvalidDataException($"Invalid color '{value}'.");

    private static bool TryParseColor(string? value, out Color color)
    {
        color = default;
        if (value is null ||
            value.Length is not (7 or 9) ||
            value[0] != '#')
        {
            return false;
        }

        if (!uint.TryParse(
            value.AsSpan(1),
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture,
            out var rgba))
        {
            return false;
        }

        if (value.Length == 7)
        {
            rgba = (rgba << 8) | 0xFF;
        }

        color = new Color(
            (byte)(rgba >> 24),
            (byte)(rgba >> 16),
            (byte)(rgba >> 8),
            (byte)rgba);
        return true;
    }

    private static Color Composite(Color foreground, Color background)
    {
        var alpha = foreground.A / 255d;
        var backgroundAlpha = background.A / 255d;
        var outputAlpha = alpha + (backgroundAlpha * (1d - alpha));
        if (outputAlpha <= 0d)
        {
            return Color.Transparent;
        }

        byte Blend(byte foregroundChannel, byte backgroundChannel) =>
            (byte)Math.Round(
                ((foregroundChannel * alpha) +
                 (backgroundChannel * backgroundAlpha * (1d - alpha))) /
                outputAlpha);

        return new Color(
            Blend(foreground.R, background.R),
            Blend(foreground.G, background.G),
            Blend(foreground.B, background.B),
            (byte)Math.Round(outputAlpha * 255d));
    }

    private static double GetContrastRatio(Color first, Color second)
    {
        var lighter = Math.Max(GetLuminance(first), GetLuminance(second));
        var darker = Math.Min(GetLuminance(first), GetLuminance(second));
        return (lighter + 0.05d) / (darker + 0.05d);
    }

    private static double GetLuminance(Color color)
    {
        static double Linearize(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045d
                ? value / 12.92d
                : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
        }

        return (0.2126d * Linearize(color.R)) +
            (0.7152d * Linearize(color.G)) +
            (0.0722d * Linearize(color.B));
    }
}
