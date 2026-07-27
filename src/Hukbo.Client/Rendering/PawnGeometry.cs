using Hukbo.Client.Presentation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client.Rendering;

internal enum PawnDetailTier
{
    Low,
    Medium,
    High,
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
    Rectangle SelectionBounds,
    Rectangle VisualBounds);

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
            selectionBounds,
            visualBounds);
    }

    private static WeaponLayout CreateWeaponLayout(
        Vector2 footAnchor,
        float scale,
        PawnWeaponRole role,
        PawnDetailTier detailTier,
        SwingPose pose)
    {
        // Chopper is broad and forward-weighted (heavy tip, short grip);
        // thrusting blade is narrow with a long reach; bolo reuses the
        // short broad-dagger silhouette; great blade is unchanged.
        var start = role switch
        {
            PawnWeaponRole.Bolo => Offset(footAnchor, 1f, -7f, scale),
            PawnWeaponRole.GreatBlade => Offset(footAnchor, 1f, -6f, scale),
            PawnWeaponRole.HeavyChopper => Offset(footAnchor, 1f, -6f, scale),
            PawnWeaponRole.ThrustingBlade =>
                Offset(footAnchor, 1f, -7f, scale),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
        var end = role switch
        {
            PawnWeaponRole.Bolo => Offset(footAnchor, 9f, -15f, scale),
            PawnWeaponRole.GreatBlade => Offset(footAnchor, 15f, -19f, scale),
            PawnWeaponRole.HeavyChopper =>
                Offset(footAnchor, 13f, -16f, scale),
            PawnWeaponRole.ThrustingBlade =>
                Offset(footAnchor, 14f, -21f, scale),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
        var thickness = MathF.Max(
            1f,
            role switch
            {
                PawnWeaponRole.Bolo => 2.2f * scale,
                PawnWeaponRole.GreatBlade => 2.8f * scale,
                PawnWeaponRole.HeavyChopper => 3.1f * scale,
                PawnWeaponRole.ThrustingBlade => 1.6f * scale,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(role),
                    role,
                    null),
            });
        var weaponPadding = role switch
        {
            PawnWeaponRole.Bolo => 2.8f * scale,
            PawnWeaponRole.GreatBlade => 4.2f * scale,
            PawnWeaponRole.HeavyChopper => 4.4f * scale,
            PawnWeaponRole.ThrustingBlade => 3.2f * scale,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
        end = ApplySwing(start, end, pose);
        var bounds = BoundsFromLine(start, end, weaponPadding);
        var secondaryBounds = detailTier == PawnDetailTier.Low
            ? Rectangle.Empty
            : CreateSecondaryBounds(footAnchor, scale, role);

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
        PawnWeaponRole role) =>
        role switch
        {
            PawnWeaponRole.Bolo => BoundsFromLine(
                Offset(footAnchor, -2f, -4f, scale),
                Offset(footAnchor, -6f, -11f, scale),
                2f * scale),
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
