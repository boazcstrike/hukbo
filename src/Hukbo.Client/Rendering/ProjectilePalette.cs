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
    /// rather than a historical measurement (CLAUDE.md section 7).
    /// </summary>
    internal static readonly Color ProjectileShaftColor = new(214, 178, 122);

    /// <summary>
    /// The metal tint of a spear head and of a lead ball — darker and cooler
    /// than <see cref="ProjectileShaftColor"/> so the two parts of a spear
    /// read as two parts. Provisional gameplay presentation rather than a
    /// historical measurement (CLAUDE.md section 7).
    /// </summary>
    internal static readonly Color ProjectileHeadColor = new(168, 172, 178);

    /// <summary>
    /// The tint of an arrow's fletching. Pale enough to separate from
    /// <see cref="ProjectileShaftColor"/> at the tail. Provisional gameplay
    /// presentation rather than a historical measurement (CLAUDE.md section 7).
    /// </summary>
    internal static readonly Color ProjectileFletchColor = new(236, 228, 208);

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
