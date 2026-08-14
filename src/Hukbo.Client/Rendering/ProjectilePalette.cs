using Hukbo.Client.Presentation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Projectile element colours, moved off <c>ArenaGame</c> (PV-1) so a Client
/// test can read them without constructing a game, a graphics device, or a
/// window. Pure data; carries no rendering behaviour of its own.
/// </summary>
internal static class ProjectilePalette
{
    /// <summary>
    /// RU-25. The wooden-shaft tint drawn for every in-flight
    /// <see cref="ProjectileFlight"/>, provisional gameplay presentation
    /// rather than a historical measurement (CLAUDE.md section 7). PV-7
    /// retuned this from (214, 178, 122) to clear
    /// <see cref="ContrastEnvelope.MinimumGroundDistance"/> against every
    /// shipped ground shade — the prior value sat only 28.2 from the Field
    /// Manual ground at its 0.22 backdrop ceiling; this one clears every
    /// shade by at least 64.5 (see <c>ProjectilePaletteContrastTests</c>).
    /// </summary>
    internal static readonly Color ProjectileShaftColor = new(235, 165, 95);

    /// <summary>
    /// The metal tint of a spear head and of a lead ball — darker and cooler
    /// than <see cref="ProjectileShaftColor"/> so the two parts of a spear
    /// read as two parts. Provisional gameplay presentation rather than a
    /// historical measurement (CLAUDE.md section 7). PV-7 retuned this from
    /// (168, 172, 178) to clear <see cref="ContrastEnvelope.MinimumGroundDistance"/>
    /// against every shipped ground shade — the prior value sat only 47.8
    /// from the Field Manual ground; this one clears every shade by at
    /// least 66.9 (see <c>ProjectilePaletteContrastTests</c>).
    /// </summary>
    internal static readonly Color ProjectileHeadColor = new(160, 165, 195);

    /// <summary>
    /// The tint of an arrow's fletching. Pale enough to separate from
    /// <see cref="ProjectileShaftColor"/> at the tail. Provisional gameplay
    /// presentation rather than a historical measurement (CLAUDE.md section 7).
    /// PV-7 retuned this from (236, 228, 208) to clear
    /// <see cref="ContrastEnvelope.MinimumGroundDistance"/> against every
    /// shipped ground shade — the prior value sat only 29.9 from the
    /// Broadcast ground; this one clears every shade by at least 62.9 (see
    /// <c>ProjectilePaletteContrastTests</c>).
    /// </summary>
    internal static readonly Color ProjectileFletchColor = new(255, 250, 190);

    /// <summary>
    /// Resolves the element colour for a projectile prop kind, matching
    /// <see cref="ProjectilePropElementKind"/> to the palette above.
    /// </summary>
    internal static Color GetColor(ProjectilePropElementKind kind) =>
        kind switch
        {
            ProjectilePropElementKind.Shaft => ProjectileShaftColor,
            ProjectilePropElementKind.Head => ProjectileHeadColor,
            ProjectilePropElementKind.Fletch => ProjectileFletchColor,
            ProjectilePropElementKind.Ball => ProjectileHeadColor,
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                null),
        };
}
