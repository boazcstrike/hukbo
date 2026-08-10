using Hukbo.Client.Presentation;
using Hukbo.Core.Combat;
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
    /// How much of a shaft still shows once it is standing in something. The
    /// rest reads as buried. Provisional reconstruction, like every other
    /// proportion here.
    /// </summary>
    private const float EmbeddedLengthFraction = 0.55f;

    /// <summary>
    /// Half-width of the cone an embedded shaft may stand at, either side of
    /// horizontal. Wide enough that two arrows in one warrior are visibly
    /// separate, narrow enough that none of them lies flat along the body.
    /// </summary>
    private const float EmbeddedAngleSpreadRadians = 0.7f;

    /// <summary>
    /// How finely the cone above is subdivided. A discrete step count rather
    /// than a float division of the raw seed, so the angle is exactly
    /// reproducible from the seed with no rounding to argue about.
    /// </summary>
    private const ulong EmbeddedAngleSteps = 64;

    /// <summary>
    /// The seed bit deciding which side of the warrior a shaft stands out of.
    /// Taken well above the bits <see cref="EmbeddedAngleSteps"/> consumes so
    /// the side and the angle are not correlated.
    /// </summary>
    private const ulong EmbeddedMirrorBit = 1UL << 40;

    // Where the trunk's own landmarks sit as fractions of torso height.
    // Provisional presentation proportions, not anatomy.
    private const float ChestHeightFraction = 0.3f;
    private const float AbdomenHeightFraction = 0.75f;

    // Distinct per-field keys, so that mixing (sequence, host, attacker) can
    // never collapse to the same value when two of the three happen to be
    // equal. Same three-key shape BloodGeometry seeds a burst with.
    private const ulong EmbeddedSequenceKey = 0x9E3779B97F4A7C15UL;
    private const ulong EmbeddedHostKey = 0xC2B2AE3D27D4EB4FUL;
    private const ulong EmbeddedAttackerKey = 0x165667B19E3779F9UL;

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

    /// <summary>
    /// Builds the silhouette of one projectile standing in the warrior or the
    /// shield it struck, anchored to that warrior's own drawn geometry so it
    /// rides with them as they move.
    /// </summary>
    /// <param name="weaponRole">
    /// The launching weapon's role. Only <see cref="PawnWeaponRole.Bangkaw"/>
    /// and <see cref="PawnWeaponRole.Busog"/> reach here —
    /// <see cref="Presentation.EmbeddedProjectileSystem.Embeds"/> is the gate,
    /// and it refuses an arquebus ball.
    /// </param>
    /// <param name="layout">
    /// The host's posed layout for this frame. Every anchor is read off the
    /// rectangles this already computes rather than re-derived from
    /// <see cref="PawnLayout.WeaponStart"/> or
    /// <see cref="PawnLayout.ShieldBounds"/>, per the composed-layer rule in
    /// <see cref="PawnLayout"/>'s own documentation.
    /// </param>
    /// <param name="hitLocation">
    /// The body part struck, or <see langword="null"/> when
    /// <paramref name="onShield"/> is set.
    /// </param>
    /// <param name="onShield">Whether the shield took it.</param>
    /// <param name="seed">
    /// The stable per-projectile seed from
    /// <see cref="CreateEmbeddedSeed"/>, which decides the angle the shaft
    /// stands at. Same hit, same angle, every frame and every replay.
    /// </param>
    public static ProjectilePropLayout CreateEmbedded(
        PawnWeaponRole weaponRole,
        PawnLayout layout,
        BodyPart? hitLocation,
        bool onShield,
        ulong seed)
    {
        if (weaponRole is not (PawnWeaponRole.Bangkaw or PawnWeaponRole.Busog))
        {
            throw new ArgumentOutOfRangeException(
                nameof(weaponRole),
                weaponRole,
                "Only a shafted projectile embeds. An arquebus ball does not " +
                "stand out of a wound, and a melee weapon launches nothing.");
        }

        var anchor = ResolveAnchor(layout, hitLocation, onShield);
        var direction = ResolveEmbeddedDirection(seed);

        // Mostly buried, so shorter than the same shaft in flight. Scaled by
        // the host's own apparent scale rather than the camera zoom, so a
        // stuck arrow grows and shrinks with the warrior carrying it.
        var scale = layout.ApparentScale * EmbeddedLengthFraction;

        return weaponRole == PawnWeaponRole.Bangkaw
            ? CreateShafted(
                anchor,
                direction,
                MathF.Atan2(direction.Y, direction.X),
                scale,
                BangkawShaftUnits,
                BangkawShaftThicknessUnits,
                ProjectilePropElementKind.Head,
                BangkawHeadUnits,
                BangkawHeadThicknessUnits,
                // The head is the end inside the target, so it points the
                // opposite way from the visible shaft: the fletch end is what
                // stands out.
                secondaryAtLeadingEnd: false)
            : CreateShafted(
                anchor,
                direction,
                MathF.Atan2(direction.Y, direction.X),
                scale,
                BusogShaftUnits,
                BusogShaftThicknessUnits,
                ProjectilePropElementKind.Fletch,
                BusogFletchUnits,
                BusogFletchThicknessUnits,
                secondaryAtLeadingEnd: true);
    }

    /// <summary>
    /// The stable seed one embedded projectile draws its angle from, mixing
    /// the contact sequence with both entity identifiers exactly as
    /// <c>BloodGeometry</c> seeds a burst.
    /// </summary>
    public static ulong CreateEmbeddedSeed(
        long sequence,
        ulong hostEntityId,
        ulong attackerEntityId) =>
        PresentationHash.Mix((ulong)sequence + EmbeddedSequenceKey) ^
        PresentationHash.Mix(hostEntityId + EmbeddedHostKey) ^
        PresentationHash.Mix(attackerEntityId + EmbeddedAttackerKey);

    /// <summary>
    /// The point on the host that a projectile struck. Every one of the
    /// thirteen <see cref="BodyPart"/> values resolves, and each falls back to
    /// the torso when the rectangle it would rather use is empty — the leg and
    /// foot rectangles are <see cref="Rectangle.Empty"/> at
    /// <see cref="PawnDetailTier.Low"/>, and an unshielded warrior has no
    /// shield block at any tier.
    /// </summary>
    private static Vector2 ResolveAnchor(
        PawnLayout layout,
        BodyPart? hitLocation,
        bool onShield)
    {
        if (onShield && !layout.ShieldBounds.IsEmpty)
        {
            return Center(layout.ShieldBounds);
        }

        var torso = Center(layout.TorsoBounds);
        if (hitLocation is not { } part)
        {
            return torso;
        }

        return part switch
        {
            BodyPart.Head or BodyPart.Face => CenterOrFallback(layout.HeadBounds, torso),
            // Between the head and the trunk, so the top edge of the torso
            // rather than either rectangle's own middle.
            BodyPart.Neck => layout.TorsoBounds.IsEmpty
                ? torso
                : new Vector2(
                    layout.TorsoBounds.Center.X,
                    layout.TorsoBounds.Top),
            BodyPart.Shoulder or BodyPart.Chest => layout.TorsoBounds.IsEmpty
                ? torso
                : new Vector2(
                    layout.TorsoBounds.Center.X,
                    layout.TorsoBounds.Top +
                        (layout.TorsoBounds.Height * ChestHeightFraction)),
            BodyPart.Abdomen => layout.TorsoBounds.IsEmpty
                ? torso
                : new Vector2(
                    layout.TorsoBounds.Center.X,
                    layout.TorsoBounds.Top +
                        (layout.TorsoBounds.Height * AbdomenHeightFraction)),
            BodyPart.WeaponArm or BodyPart.Hands =>
                CenterOrFallback(layout.Arms.WeaponForearm.Bounds, torso),
            BodyPart.ShieldArm =>
                CenterOrFallback(layout.Arms.SupportForearm.Bounds, torso),
            BodyPart.Thigh or BodyPart.Knee =>
                CenterOrFallback(layout.LeftLegBounds, torso),
            BodyPart.Shin => CenterOrFallback(layout.RightLegBounds, torso),
            BodyPart.Feet => CenterOrFallback(layout.LeftFootBounds, torso),
            _ => throw new ArgumentOutOfRangeException(
                nameof(hitLocation),
                hitLocation,
                null),
        };
    }

    /// <summary>
    /// The angle the shaft stands at, spread across a cone either side of
    /// horizontal so a warrior struck twice does not draw two parallel
    /// arrows. Derived from the seed alone, so it never changes for a given
    /// hit.
    /// </summary>
    private static Vector2 ResolveEmbeddedDirection(ulong seed)
    {
        // The low bits of a mixed value are as well distributed as the high
        // ones, so a plain modulus is enough here and needs no rejection step.
        var steps = (int)(seed % EmbeddedAngleSteps);
        var fraction = steps / (float)(EmbeddedAngleSteps - 1);
        var angle = -EmbeddedAngleSpreadRadians +
            (fraction * 2f * EmbeddedAngleSpreadRadians);

        // Half of them lean the other way, so arrows stand out of both sides
        // of a warrior rather than all pointing right.
        var mirrored = (seed & EmbeddedMirrorBit) != 0;
        return mirrored
            ? new Vector2(-MathF.Cos(angle), MathF.Sin(angle))
            : new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private static Vector2 CenterOrFallback(Rectangle bounds, Vector2 fallback) =>
        bounds.IsEmpty ? fallback : Center(bounds);

    private static Vector2 Center(Rectangle bounds) =>
        new(bounds.Center.X, bounds.Center.Y);

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
