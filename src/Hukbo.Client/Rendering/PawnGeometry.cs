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
    Rectangle ShieldBounds,
    Rectangle SelectionBounds,
    Rectangle VisualBounds);

internal static class PawnGeometry
{
    private const float MinimumApparentScale = 0.72f;
    private const float MaximumApparentScale = 2.40f;
    private const float ZoomScale = 1.35f;
    private const float MediumDetailScale = 0.95f;
    private const float HighDetailScale = 1.80f;

    public static PawnLayout Create(
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        float scaleMultiplier = 1f)
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

        var torsoHeight = ToSize(
            12f * appearance.StatureMultiplier * apparentScale);
        var torsoWidth = ToSize(
            7f * appearance.BuildMultiplier * apparentScale);
        var torsoBottom = (int)MathF.Round(
            footAnchor.Y - MathF.Max(1f, apparentScale));
        var torsoBounds = new Rectangle(
            (int)MathF.Round(footAnchor.X - (torsoWidth / 2f)),
            torsoBottom - torsoHeight,
            torsoWidth,
            torsoHeight);

        var headSize = ToSize(7f * apparentScale);
        var headGap = ToSize(apparentScale);
        var headBounds = new Rectangle(
            (int)MathF.Round(footAnchor.X - (headSize / 2f)),
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
            footAnchor,
            apparentScale,
            appearance.WeaponRole,
            detailTier);
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
            visualBounds);
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
        PawnDetailTier detailTier)
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
