using Hukbo.Client.Presentation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

internal enum PawnDetailTier
{
    Low,
    Medium,
    High,
}

/// <summary>
/// The arc a swinging weapon has just travelled, expressed as a pivot, a
/// radius, and the two angles it spans, so that a renderer walks the arc
/// without knowing how it was derived.
/// </summary>
/// <remarks>
/// It is computed once from the pose, with no position history, and it lives
/// on <see cref="PawnLayout"/> rather than in the renderer. The
/// plains-backdrop finding recorded in <c>docs/development/testing.md</c> is
/// what fixes this shape: a duplicated ground-cell formula left the shipped
/// render loop uncovered while the tests constrained a method with no
/// production caller.
/// </remarks>
/// <param name="Pivot">The grip the arc turns about.</param>
/// <param name="Radius">Distance from the grip to the weapon tip.</param>
/// <param name="StartAngleRadians">The trailing end of the arc.</param>
/// <param name="EndAngleRadians">The current weapon tip.</param>
/// <param name="Strength">
/// Trail opacity, zero when no trail is drawn at all.
/// </param>
/// <param name="Thickness">Stroke thickness in pixels.</param>
internal readonly record struct SwingTrail(
    Vector2 Pivot,
    float Radius,
    float StartAngleRadians,
    float EndAngleRadians,
    float Strength,
    float Thickness)
{
    public bool IsEmpty => Strength <= 0f || Radius <= 0f;
}

internal readonly record struct PawnLayout(
    Vector2 FootAnchor,
    float ApparentScale,
    PawnDetailTier DetailTier,
    Rectangle GroundRingBounds,
    Rectangle TorsoBounds,
    Rectangle HeadBounds,
    Rectangle HeadTreatmentBounds,
    Vector2 WeaponStart,
    Vector2 WeaponEnd,
    float WeaponThickness,
    Rectangle WeaponBounds,
    Rectangle SecondaryEquipmentBounds,
    Rectangle ShieldBounds,
    Rectangle SelectionBounds,
    Rectangle VisualBounds,
    SwingTrail SwingTrail);

internal static class PawnGeometry
{
    private const float MinimumApparentScale = 0.72f;
    private const float MaximumApparentScale = 2.40f;
    private const float ZoomScale = 1.35f;
    private const float MediumDetailScale = 0.95f;
    private const float HighDetailScale = 1.80f;

    /// <summary>
    /// PROVISIONAL. Share of the neutral reach added to the weapon line at an
    /// extension ratio of one, which is where a blow makes contact.
    /// </summary>
    private const float ExtensionReach = 0.35f;

    /// <summary>
    /// PROVISIONAL. Angular span of the arc trail at full trail strength.
    /// </summary>
    private const float TrailSweepRadians = 0.85f;

    /// <summary>PROVISIONAL. Trail stroke thickness in pawn units.</summary>
    private const float TrailThickness = 1.2f;

    /// <param name="swingPose">
    /// The pose one in-flight swing puts this pawn in, or <c>null</c> for a
    /// pawn standing still. A neutral pose produces the same layout as no pose
    /// at all, so a caller may pass either.
    /// </param>
    public static PawnLayout Create(
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        float scaleMultiplier = 1f,
        SwingPose? swingPose = null)
    {
        if (!float.IsFinite(cameraZoom) || cameraZoom < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cameraZoom));
        }

        if (!float.IsFinite(scaleMultiplier) || scaleMultiplier <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(scaleMultiplier));
        }

        var apparentScale = Math.Clamp(
            cameraZoom * ZoomScale,
            MinimumApparentScale,
            MaximumApparentScale) * scaleMultiplier;
        var detailTier = apparentScale switch
        {
            < MediumDetailScale => PawnDetailTier.Low,
            < HighDetailScale => PawnDetailTier.Medium,
            _ => PawnDetailTier.High,
        };

        var ringWidth = ToSize(13f * apparentScale);
        var ringHeight = ToSize(4f * apparentScale);
        var groundRingBounds = CenteredRectangle(
            footAnchor.X,
            footAnchor.Y - (ringHeight / 2f),
            ringWidth,
            ringHeight);

        // The feet stay planted, so the ground ring keeps the foot anchor
        // while everything the warrior can lean moves with the torso.
        var pose = swingPose ?? default;
        var bodyAnchor = footAnchor + new Vector2(
            pose.TorsoLeanX * apparentScale,
            pose.TorsoLeanY * apparentScale);

        var torsoHeight = ToSize(
            12f * appearance.StatureMultiplier * apparentScale);
        var torsoWidth = ToSize(
            7f * appearance.BuildMultiplier * apparentScale);
        var torsoBottom = (int)MathF.Round(
            bodyAnchor.Y - MathF.Max(1f, apparentScale));
        var torsoBounds = new Rectangle(
            (int)MathF.Round(bodyAnchor.X - (torsoWidth / 2f)),
            torsoBottom - torsoHeight,
            torsoWidth,
            torsoHeight);

        var headSize = ToSize(7f * apparentScale);
        var headGap = ToSize(apparentScale);
        var headBounds = new Rectangle(
            (int)MathF.Round(bodyAnchor.X - (headSize / 2f)),
            torsoBounds.Top - headGap - headSize,
            headSize,
            headSize);
        var headTreatmentHeight = Math.Max(1, ToSize(2.6f * apparentScale));
        var headTreatmentBounds = new Rectangle(
            headBounds.Left,
            headBounds.Top,
            headBounds.Width,
            headTreatmentHeight);

        var weapon = CreateWeaponLayout(
            bodyAnchor,
            apparentScale,
            appearance.WeaponRole,
            detailTier,
            pose);

        // The shield is deliberately not posed. A swing moves the weapon arm;
        // the off-hand block stays where the torso puts it, so a spectator can
        // still tell a shielded warrior from a solo one at the moment of
        // impact, which is exactly when the weapon line is least readable.
        var shieldBounds = CreateShieldBounds(
            footAnchor,
            apparentScale,
            appearance.ShieldRole,
            torsoBounds,
            detailTier);
        var renderedBounds = Rectangle.Union(groundRingBounds, torsoBounds);
        renderedBounds = Rectangle.Union(renderedBounds, headBounds);
        renderedBounds = Rectangle.Union(renderedBounds, headTreatmentBounds);
        renderedBounds = Rectangle.Union(renderedBounds, weapon.Bounds);

        if (!weapon.SecondaryBounds.IsEmpty)
        {
            renderedBounds = Rectangle.Union(
                renderedBounds,
                weapon.SecondaryBounds);
        }

        if (!shieldBounds.IsEmpty)
        {
            renderedBounds = Rectangle.Union(renderedBounds, shieldBounds);
        }

        var selectionPadding = Math.Max(
            3,
            (int)MathF.Ceiling(3f * apparentScale));
        var selectionBounds = Inflate(renderedBounds, selectionPadding);
        var visualBounds = Rectangle.Union(renderedBounds, selectionBounds);

        return new PawnLayout(
            footAnchor,
            apparentScale,
            detailTier,
            groundRingBounds,
            torsoBounds,
            headBounds,
            headTreatmentBounds,
            weapon.Start,
            weapon.End,
            weapon.Thickness,
            weapon.Bounds,
            weapon.SecondaryBounds,
            shieldBounds,
            selectionBounds,
            visualBounds,
            CreateSwingTrail(weapon, apparentScale, detailTier, pose));
    }

    /// <summary>
    /// The arc the weapon tip has just swept, derived from the pose alone. It
    /// is omitted entirely at the low detail tier, where a pawn is a handful
    /// of pixels tall and the arc would be noise.
    /// </summary>
    private static SwingTrail CreateSwingTrail(
        WeaponLayout weapon,
        float scale,
        PawnDetailTier detailTier,
        SwingPose pose)
    {
        if (detailTier == PawnDetailTier.Low || pose.TrailStrength <= 0f)
        {
            return default;
        }

        var reach = weapon.End - weapon.Start;
        var radius = reach.Length();
        if (radius <= 0f)
        {
            return default;
        }

        // The arc trails behind the direction of travel, and the sign of the
        // weapon rotation is what says which way that is. There is no position
        // history to consult and none is kept.
        var facing = pose.WeaponAngleRadians >= 0f ? 1f : -1f;
        var endAngle = MathF.Atan2(reach.Y, reach.X);
        var sweep = TrailSweepRadians * pose.TrailStrength * facing;

        return new SwingTrail(
            weapon.Start,
            radius,
            endAngle - sweep,
            endAngle,
            pose.TrailStrength,
            MathF.Max(1f, TrailThickness * scale));
    }

    /// <summary>
    /// The block a shield bearer draws beside the torso, on the side opposite
    /// the weapon, or <see cref="Rectangle.Empty"/> for a warrior carrying no
    /// shield.
    /// </summary>
    /// <remarks>
    /// This is the surface that makes grip discoverable on the battlefield
    /// rather than only in the inspector: preset V2 is the first to field a
    /// solo and a shielded warrior of the same weapon at once, and they deal
    /// different damage, so without it the two are indistinguishable.
    /// <para>
    /// Drawn at every detail tier including <see cref="PawnDetailTier.Low"/>,
    /// unlike secondary equipment. A shield changes what the warrior is, not
    /// how ornamented they are, and dropping it at distance would remove the
    /// distinction exactly when a spectator is watching whole formations
    /// rather than individuals. It is a solid block, which survives being a
    /// few pixels tall in a way a drawn line does not.
    /// </para>
    /// </remarks>
    private static Rectangle CreateShieldBounds(
        Vector2 footAnchor,
        float scale,
        PawnShieldRole role,
        Rectangle torsoBounds,
        PawnDetailTier detailTier)
    {
        if (role == PawnShieldRole.None)
        {
            return Rectangle.Empty;
        }

        // Tall enough to read as covering chest and abdomen, which is what
        // the targeting multiplier actually does.
        var width = Math.Max(2, ToSize(4f * scale));
        var height = Math.Max(
            detailTier == PawnDetailTier.Low ? 3 : 5,
            ToSize(11f * scale));
        var left = (int)MathF.Round(footAnchor.X - (7f * scale)) - width;
        var top = torsoBounds.Top + ToSize(scale);

        return new Rectangle(left, top, width, height);
    }

    private static WeaponLayout CreateWeaponLayout(
        Vector2 footAnchor,
        float scale,
        PawnWeaponRole role,
        PawnDetailTier detailTier,
        SwingPose pose)
    {
        // The Wasay is a haft, not a blade: a thinner shaft than the old
        // broad chopper carrying a distinct head at the far end, which
        // CreateSecondaryBounds supplies. The Kalis is narrow with a long
        // reach; the Itak reuses the short broad-dagger silhouette; the
        // Kampilan is unchanged.
        var start = role switch
        {
            PawnWeaponRole.Itak => Offset(footAnchor, 1f, -7f, scale),
            PawnWeaponRole.Kampilan => Offset(footAnchor, 1f, -6f, scale),
            PawnWeaponRole.Wasay => Offset(footAnchor, 1f, -5f, scale),
            PawnWeaponRole.Kalis =>
                Offset(footAnchor, 1f, -7f, scale),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
        var end = role switch
        {
            PawnWeaponRole.Itak => Offset(footAnchor, 9f, -15f, scale),
            PawnWeaponRole.Kampilan => Offset(footAnchor, 15f, -19f, scale),
            PawnWeaponRole.Wasay =>
                Offset(footAnchor, 12f, -18f, scale),
            PawnWeaponRole.Kalis =>
                Offset(footAnchor, 14f, -21f, scale),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
        var thickness = MathF.Max(
            1f,
            role switch
            {
                PawnWeaponRole.Itak => 2.2f * scale,
                PawnWeaponRole.Kampilan => 2.8f * scale,
                PawnWeaponRole.Wasay => 1.9f * scale,
                PawnWeaponRole.Kalis => 1.6f * scale,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(role),
                    role,
                    null),
            });
        var weaponPadding = role switch
        {
            PawnWeaponRole.Itak => 2.8f * scale,
            PawnWeaponRole.Kampilan => 4.2f * scale,
            PawnWeaponRole.Wasay => 4.4f * scale,
            PawnWeaponRole.Kalis => 3.2f * scale,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
        end = ApplySwing(start, end, pose);
        var bounds = BoundsFromLine(start, end, weaponPadding);

        // The Wasay's head is what distinguishes an axe from a blade, so it
        // survives the low detail tier that drops the Itak's off-hand
        // secondary. Without it the axe silhouette is just a thin Kampilan.
        var secondaryBounds =
            detailTier == PawnDetailTier.Low && role != PawnWeaponRole.Wasay
                ? Rectangle.Empty
                : CreateSecondaryBounds(footAnchor, scale, role, end);

        return new WeaponLayout(
            start,
            end,
            thickness,
            bounds,
            secondaryBounds);
    }

    /// <summary>
    /// Rotates the weapon line about the grip and lengthens it along the
    /// reach. A neutral pose rotates by nothing and lengthens by nothing, so
    /// the line is bit-for-bit the static one.
    /// </summary>
    /// <remarks>
    /// The rotation is applied to the drawn line only; the pawn silhouette is
    /// not mirrored for a warrior striking to its left, so a leftward swing
    /// reads as an overhead sweep rather than as a blade ending on the target.
    /// Mirroring the silhouette needs a facing this pose does not carry, and
    /// is outside what this task was asked to change.
    /// </remarks>
    private static Vector2 ApplySwing(Vector2 start, Vector2 end, SwingPose pose)
    {
        var reach = end - start;
        var cosine = MathF.Cos(pose.WeaponAngleRadians);
        var sine = MathF.Sin(pose.WeaponAngleRadians);
        var rotated = new Vector2(
            (reach.X * cosine) - (reach.Y * sine),
            (reach.X * sine) + (reach.Y * cosine));
        var extension = MathF.Max(
            0f,
            1f + (pose.ExtensionRatio * ExtensionReach));

        return start + (rotated * extension);
    }

    private static Rectangle CreateSecondaryBounds(
        Vector2 footAnchor,
        float scale,
        PawnWeaponRole role,
        Vector2 weaponEnd) =>
        role switch
        {
            PawnWeaponRole.Itak => BoundsFromLine(
                Offset(footAnchor, -2f, -4f, scale),
                Offset(footAnchor, -6f, -11f, scale),
                2f * scale),

            // The axe head, centred on the far end of the haft and set
            // across it so the mass reads as sitting behind the edge.
            PawnWeaponRole.Wasay => new Rectangle(
                (int)MathF.Round(weaponEnd.X - (2.6f * scale)),
                (int)MathF.Round(weaponEnd.Y - (2.6f * scale)),
                Math.Max(2, ToSize(5f * scale)),
                Math.Max(2, ToSize(5.2f * scale))),

            _ => Rectangle.Empty,
        };

    private static Vector2 Offset(
        Vector2 origin,
        float x,
        float y,
        float scale) =>
        origin + new Vector2(x * scale, y * scale);

    private static Rectangle CenteredRectangle(
        float centerX,
        float centerY,
        int width,
        int height) =>
        new(
            (int)MathF.Round(centerX - (width / 2f)),
            (int)MathF.Round(centerY - (height / 2f)),
            width,
            height);

    private static Rectangle BoundsFromLine(
        Vector2 start,
        Vector2 end,
        float padding)
    {
        var left = (int)MathF.Floor(MathF.Min(start.X, end.X) - padding);
        var top = (int)MathF.Floor(MathF.Min(start.Y, end.Y) - padding);
        var right = (int)MathF.Ceiling(MathF.Max(start.X, end.X) + padding);
        var bottom = (int)MathF.Ceiling(MathF.Max(start.Y, end.Y) + padding);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    private static Rectangle Inflate(Rectangle bounds, int amount) =>
        new(
            bounds.X - amount,
            bounds.Y - amount,
            bounds.Width + (amount * 2),
            bounds.Height + (amount * 2));

    private static int ToSize(float value) =>
        Math.Max(1, (int)MathF.Round(value));

    private readonly record struct WeaponLayout(
        Vector2 Start,
        Vector2 End,
        float Thickness,
        Rectangle Bounds,
        Rectangle SecondaryBounds);
}
