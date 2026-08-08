using System;
using Microsoft.Xna.Framework;
using Sandata.Core.Weapons;

namespace Sandata.Client.UI;

/// <summary>
/// Bottom-left operator roster strip, design section 11's HUD element list:
/// one tile per operator, showing health, weapon, magazine, and weapon-chain
/// phase. Pure layout and content helpers only — no <c>SpriteBatch</c>, no
/// font, no dependency on <c>Sandata.Core.Simulation.OperatorState</c> so
/// this file never breaks on a field rename in a state record it does not
/// own.
/// </summary>
internal static class RosterStrip
{
    /// <summary>Distance from the window edge to the strip.</summary>
    internal const int Margin = 12;

    /// <summary>One tile's fixed width.</summary>
    internal const int TileWidth = 96;

    /// <summary>One tile's fixed height.</summary>
    internal const int TileHeight = 64;

    /// <summary>Horizontal gap between adjacent tiles.</summary>
    internal const int TileGap = 6;

    /// <summary>
    /// The plain content one roster tile shows. Deliberately built from
    /// primitives and the already-declared <see cref="WeaponChainPhase"/>
    /// enum rather than from <c>OperatorState</c> directly, so this file has
    /// no compile-time dependency on a record another task still owns.
    /// </summary>
    internal readonly record struct TileContent(
        int Health,
        int MaxHealth,
        string WeaponLabel,
        int MagazineRounds,
        int MagazineCapacity,
        WeaponChainPhase ChainPhase,
        bool IsAlive);

    /// <summary>
    /// How many tiles fit inside <paramref name="windowBounds"/> at the fixed
    /// tile width and gap. Overflow operators are simply not drawn in v0.1 —
    /// there is no scroll or pagination for the roster strip.
    /// </summary>
    internal static int CountVisibleTiles(Rectangle windowBounds, int operatorCount)
    {
        if (operatorCount <= 0)
        {
            return 0;
        }

        var available = Math.Max(0, windowBounds.Width - (Margin * 2));
        var maxThatFit = (available + TileGap) / (TileWidth + TileGap);
        return Math.Min(operatorCount, Math.Max(0, maxThatFit));
    }

    /// <summary>
    /// The strip's own bounding rectangle, anchored to the bottom-left corner
    /// of <paramref name="windowBounds"/>. Width grows with
    /// <paramref name="operatorCount"/> up to however many tiles fit; a
    /// window too narrow for even one tile yields a zero-width rectangle.
    /// </summary>
    internal static Rectangle CalculateBounds(Rectangle windowBounds, int operatorCount)
    {
        var visibleTileCount = CountVisibleTiles(windowBounds, operatorCount);
        var width = visibleTileCount <= 0
            ? 0
            : (visibleTileCount * TileWidth) + ((visibleTileCount - 1) * TileGap);

        return new Rectangle(
            windowBounds.Left + Margin,
            windowBounds.Bottom - Margin - TileHeight,
            width,
            TileHeight);
    }

    /// <summary>
    /// The rectangle for tile <paramref name="index"/> (zero-based) within a
    /// strip already positioned at <paramref name="stripBounds"/>.
    /// </summary>
    internal static Rectangle CalculateTileBounds(Rectangle stripBounds, int index) =>
        new(
            stripBounds.Left + (index * (TileWidth + TileGap)),
            stripBounds.Top,
            TileWidth,
            TileHeight);

    /// <summary>Formats the health line, for example "84/100".</summary>
    internal static string FormatHealthLine(TileContent content) =>
        $"{content.Health}/{content.MaxHealth}";

    /// <summary>Formats the magazine line, for example "18/30".</summary>
    internal static string FormatMagazineLine(TileContent content) =>
        $"{content.MagazineRounds}/{content.MagazineCapacity}";

    /// <summary>Formats the weapon-chain phase line, for example "Aiming".</summary>
    internal static string FormatChainPhaseLine(TileContent content) =>
        content.ChainPhase.ToString();
}
