using Hukbo.Client.Presentation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

/// <summary>
/// Which part of a projectile silhouette an element draws. The renderer picks
/// a color from this rather than the geometry carrying one, keeping this file
/// free of theme concerns exactly as <see cref="PawnGeometry"/> is.
/// </summary>
internal enum ProjectilePropElementKind
{
    /// <summary>The wooden body of a spear or an arrow.</summary>
    Shaft,

    /// <summary>The metal point at a spear's leading end.</summary>
    Head,

    /// <summary>The feathered tail at an arrow's trailing end.</summary>
    Fletch,

    /// <summary>A lead ball, which has no shaft and no tail.</summary>
    Ball,
}

/// <summary>
/// One stroked quad of a projectile silhouette, centred on
/// <see cref="Center"/> and rotated by the layout's shared
/// <see cref="ProjectilePropLayout.RotationRadians"/>.
/// </summary>
internal readonly record struct ProjectilePropElement(
    ProjectilePropElementKind Kind,
    Vector2 Center,
    float Length,
    float Thickness)
{
    public bool IsEmpty => Length <= 0f || Thickness <= 0f;
}

/// <summary>
/// The drawable form of one in-flight shot: at most two stroked quads, both
/// rotated to the shot's direction of travel and positioned relative to the
/// shot's <em>current</em> location rather than to where it was launched from.
/// </summary>
/// <remarks>
/// That centring is the whole point of the type. The draw this replaces
/// stretched one quad from the launch point to the current position, so what a
/// spectator saw was a line anchored at the thrower that grew longer every
/// tick and was at its longest the instant the shot landed — the opposite of
/// how a missile reads. A projectile is small, and the eye tracks where it is.
/// </remarks>
internal readonly record struct ProjectilePropLayout(
    float RotationRadians,
    ProjectilePropElement Primary,
    ProjectilePropElement Secondary)
{
    /// <summary>
    /// How many quads this layout draws. Never more than two — that bound is
    /// what <see cref="RenderBudgetEstimate.ProjectileQuadsPerProjectile"/> is
    /// stated against, and <c>ProjectileGeometryTests</c> pins it.
    /// </summary>
    public int ElementCount =>
        (Primary.IsEmpty ? 0 : 1) + (Secondary.IsEmpty ? 0 : 1);
}

/// <summary>
/// Builds the silhouette of one in-flight shot. Pure: no
/// <c>GraphicsDevice</c>, no <c>SpriteBatch</c>, no window, so the shapes are
/// testable without a GPU, following the same split
/// <see cref="PawnGeometry"/> and <see cref="BloodGeometry"/> already use.
/// </summary>
/// <remarks>
/// <para>
/// Every proportion below is a <strong>Provisional reconstruction</strong>
/// under <c>CLAUDE.md</c> section 7. The weapon classes themselves carry their
/// own evidence tiers in <see cref="PawnAppearance"/> — Bangkaw and Busog have
/// a zero-year-gap 1521 attestation — but no source records the drawn
/// proportions of a projectile in flight, and nothing here may be presented as
/// a measurement. These are numbers chosen so that three shot types are
/// distinguishable at a glance, and that is their entire justification.
/// </para>
/// </remarks>
internal static class ProjectileGeometry
{
    // Provisional reconstruction, all of it. World units, scaled by camera
    // zoom at the call. The three lengths are deliberately well separated
    // rather than finely graded: the spectator-facing question this answers is
    // "which of the three ranged weapons was that", and a 13-unit spear next to
    // a 7-unit arrow next to a 2-unit ball answers it at a glance, where three
    // similar lengths would not.
    private const float BangkawShaftUnits = 13f;
    private const float BangkawShaftThicknessUnits = 1.6f;
    private const float BangkawHeadUnits = 3.5f;
    private const float BangkawHeadThicknessUnits = 2.6f;

    private const float BusogShaftUnits = 7f;
    private const float BusogShaftThicknessUnits = 1.2f;
    private const float BusogFletchUnits = 2.2f;
    private const float BusogFletchThicknessUnits = 2.4f;

    private const float ArquebusBallUnits = 2.2f;

    /// <summary>
    /// Below this, a stroked quad stops being visible at all. Every dimension
    /// is floored here rather than allowed to reach zero, the same way
    /// <see cref="BloodGeometry"/> floors its droplet thickness, so a shot
    /// stays on screen at a pulled-out camera instead of silently vanishing.
    /// </summary>
    private const float MinimumDimension = 1f;

    /// <summary>
    /// Builds the silhouette for one live flight.
    /// </summary>
    /// <param name="weaponRole">
    /// The launching weapon's role, mapped from the flight's own
    /// <c>WeaponId</c> through
    /// <see cref="PawnAppearanceFactory.ToWeaponRole"/> — never through a
    /// second copy of that switch, because a second copy of it is exactly what
    /// produced the 2026-08-09 <c>Arquebus</c> crashes.
    /// </param>
    /// <param name="currentScreenPosition">
    /// Where the shot is now, in screen space. The silhouette is centred here.
    /// </param>
    /// <param name="travelDirection">
    /// The shot's direction of travel in screen space, normalized internally.
    /// Taken from destination minus origin, which is fixed for the flight's
    /// whole life, so the prop holds a steady heading instead of swinging as
    /// the shot advances. A zero vector — a release that resolved no target,
    /// and so never moves — draws unrotated rather than not at all.
    /// </param>
    /// <param name="cameraZoom">
    /// The camera's zoom, applied to every world-unit dimension above.
    /// </param>
    public static ProjectilePropLayout Create(
        PawnWeaponRole weaponRole,
        Vector2 currentScreenPosition,
        Vector2 travelDirection,
        float cameraZoom)
    {
        var direction = Normalize(travelDirection);
        var rotation = MathF.Atan2(direction.Y, direction.X);
        var scale = MathF.Max(cameraZoom, 0f);

        return weaponRole switch
        {
            PawnWeaponRole.Bangkaw => CreateShafted(
                currentScreenPosition,
                direction,
                rotation,
                scale,
                BangkawShaftUnits,
                BangkawShaftThicknessUnits,
                ProjectilePropElementKind.Head,
                BangkawHeadUnits,
                BangkawHeadThicknessUnits,
                // A spear's head is at the end that arrives first.
                secondaryAtLeadingEnd: true),
            PawnWeaponRole.Busog => CreateShafted(
                currentScreenPosition,
                direction,
                rotation,
                scale,
                BusogShaftUnits,
                BusogShaftThicknessUnits,
                ProjectilePropElementKind.Fletch,
                BusogFletchUnits,
                BusogFletchThicknessUnits,
                // An arrow's fletching is at the end that arrives last.
                secondaryAtLeadingEnd: false),
            PawnWeaponRole.Arquebus => new ProjectilePropLayout(
                rotation,
                new ProjectilePropElement(
                    ProjectilePropElementKind.Ball,
                    currentScreenPosition,
                    Scaled(ArquebusBallUnits, scale),
                    Scaled(ArquebusBallUnits, scale)),
                // A ball has no second element. This is the asymmetry that
                // keeps the arquebus out of the embedded-projectile feature
                // too: a lead ball does not stand out of a wound.
                default),
            _ => throw new ArgumentOutOfRangeException(
                nameof(weaponRole),
                weaponRole,
                "Only a ranged weapon launches a projectile. A melee role " +
                "reaching here means a Release event was attributed to a " +
                "warrior who cannot produce one."),
        };
    }

    private static ProjectilePropLayout CreateShafted(
        Vector2 center,
        Vector2 direction,
        float rotation,
        float scale,
        float shaftUnits,
        float shaftThicknessUnits,
        ProjectilePropElementKind secondaryKind,
        float secondaryUnits,
        float secondaryThicknessUnits,
        bool secondaryAtLeadingEnd)
    {
        var shaftLength = Scaled(shaftUnits, scale);
        var secondaryLength = Scaled(secondaryUnits, scale);

        // Seated against the shaft's own end rather than overlapping it, so
        // the drawn head or fletch reads as a separate part at every zoom.
        var offset = (shaftLength + secondaryLength) * 0.5f;
        var secondaryCenter = center +
            (direction * (secondaryAtLeadingEnd ? offset : -offset));

        return new ProjectilePropLayout(
            rotation,
            new ProjectilePropElement(
                ProjectilePropElementKind.Shaft,
                center,
                shaftLength,
                Scaled(shaftThicknessUnits, scale)),
            new ProjectilePropElement(
                secondaryKind,
                secondaryCenter,
                secondaryLength,
                Scaled(secondaryThicknessUnits, scale)));
    }

    private static float Scaled(float units, float scale) =>
        MathF.Max(units * scale, MinimumDimension);

    /// <summary>
    /// Unit-length <paramref name="direction"/>, or the positive x axis when
    /// it has no length to normalize. Never returns a zero vector, so
    /// <see cref="CreateShafted"/>'s offset arithmetic always has a heading.
    /// </summary>
    private static Vector2 Normalize(Vector2 direction)
    {
        var length = direction.Length();
        return length <= float.Epsilon
            ? new Vector2(1f, 0f)
            : direction / length;
    }
}
