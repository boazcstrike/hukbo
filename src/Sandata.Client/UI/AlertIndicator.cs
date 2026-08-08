using System;
using Microsoft.Xna.Framework;
using Sandata.Client.Theming;

namespace Sandata.Client.UI;

/// <summary>
/// Top-centre faction alert indicator, design section 11's HUD element list.
/// Sandata's alert level is three-valued — Calm, Raised, Breach (design
/// section 4; <c>Sandata.Core.Simulation.FactionAlertState.AlertLevel</c>'s
/// remarks) — carried here as the same raw <see langword="int"/> that record
/// holds, since task 35 has not yet declared the authoritative
/// <c>AlertLevel</c> enum. By that design line's own listed order, <c>0</c> is
/// Calm, <c>1</c> is Raised, and <c>2</c> is Breach.
/// </summary>
/// <remarks>
/// The pure-helper testability pattern's rule against color-only state
/// applies at full force here: a colour-blind player must be able to read the
/// alert level from shape alone, so every level below maps to both a distinct
/// theme colour and a distinct <see cref="IndicatorShape"/>.
/// </remarks>
internal static class AlertIndicator
{
    /// <summary>Distance from the window edge to the indicator panel.</summary>
    internal const int Margin = 12;

    /// <summary>The indicator panel's fixed width, before any window clamp.</summary>
    internal const int Width = 140;

    /// <summary>The indicator panel's fixed height, before any window clamp.</summary>
    internal const int Height = 32;

    /// <summary>
    /// The distinct silhouette drawn for each alert level, so the level reads
    /// without relying on colour at all.
    /// </summary>
    internal enum IndicatorShape
    {
        Circle,
        Diamond,
        Triangle,
    }

    /// <summary>
    /// The indicator panel's bounding rectangle, anchored to the top-centre
    /// of <paramref name="windowBounds"/> and clamped to fit inside it.
    /// </summary>
    internal static Rectangle CalculateBounds(Rectangle windowBounds)
    {
        var width = Math.Min(Width, Math.Max(0, windowBounds.Width - (Margin * 2)));
        var height = Math.Min(Height, Math.Max(0, windowBounds.Height - (Margin * 2)));

        return new Rectangle(
            windowBounds.Center.X - (width / 2),
            windowBounds.Top + Margin,
            width,
            height);
    }

    /// <summary>
    /// The shape for <paramref name="alertLevel"/> (raw <c>0</c>/<c>1</c>/<c>2</c>
    /// matching <c>FactionAlertState.AlertLevel</c>). Any value outside that
    /// range is treated as Breach, the most conservative reading for state a
    /// future task's enum might still widen.
    /// </summary>
    internal static IndicatorShape GetShape(int alertLevel) => alertLevel switch
    {
        0 => IndicatorShape.Circle,
        1 => IndicatorShape.Diamond,
        _ => IndicatorShape.Triangle,
    };

    /// <summary>The theme colour for <paramref name="alertLevel"/>.</summary>
    internal static Color GetColor(int alertLevel, SandataThemeColors colors) => alertLevel switch
    {
        0 => colors.AlertCalm,
        1 => colors.AlertRaised,
        _ => colors.AlertBreach,
    };

    /// <summary>The plain-English label for <paramref name="alertLevel"/>.</summary>
    internal static string GetLabel(int alertLevel) => alertLevel switch
    {
        0 => "Calm",
        1 => "Raised",
        _ => "Breach",
    };
}
