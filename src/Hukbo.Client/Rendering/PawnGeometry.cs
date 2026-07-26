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
        PawnDetailTier detailTier)
    {
        var start = role switch
        {
            PawnWeaponRole.LongSpear => Offset(footAnchor, -5f, -2f, scale),
            PawnWeaponRole.HardenedJavelin =>
                Offset(footAnchor, -4f, -3f, scale),
            PawnWeaponRole.WarBow => Offset(footAnchor, -7f, -2f, scale),
            PawnWeaponRole.BroadDagger => Offset(footAnchor, 1f, -7f, scale),
            PawnWeaponRole.GreatBlade => Offset(footAnchor, 1f, -6f, scale),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
        var end = role switch
        {
            PawnWeaponRole.LongSpear => Offset(footAnchor, 13f, -26f, scale),
            PawnWeaponRole.HardenedJavelin =>
                Offset(footAnchor, 11f, -20f, scale),
            PawnWeaponRole.WarBow => Offset(footAnchor, -7f, -22f, scale),
            PawnWeaponRole.BroadDagger => Offset(footAnchor, 9f, -15f, scale),
            PawnWeaponRole.GreatBlade => Offset(footAnchor, 15f, -19f, scale),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        };
        var thickness = MathF.Max(
            1f,
            role switch
            {
                PawnWeaponRole.BroadDagger => 2.2f * scale,
                PawnWeaponRole.GreatBlade => 2.8f * scale,
                _ => 1.2f * scale,
            });
        var weaponPadding = role switch
        {
            PawnWeaponRole.LongSpear => 2.8f * scale,
            PawnWeaponRole.HardenedJavelin => 1.8f * scale,
            PawnWeaponRole.WarBow => 5.2f * scale,
            PawnWeaponRole.BroadDagger => 2.8f * scale,
            PawnWeaponRole.GreatBlade => 4.2f * scale,
            _ => 0f,
        };
        var bounds = role == PawnWeaponRole.WarBow
            ? CreateBowBounds(start, end, scale)
            : BoundsFromLine(start, end, weaponPadding);
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

    private static Rectangle CreateSecondaryBounds(
        Vector2 footAnchor,
        float scale,
        PawnWeaponRole role) =>
        role switch
        {
            PawnWeaponRole.HardenedJavelin => BoundsFromLine(
                Offset(footAnchor, -6f, -4f, scale),
                Offset(footAnchor, 5f, -18f, scale),
                2.5f * scale),
            PawnWeaponRole.WarBow => BoundsFromLine(
                Offset(footAnchor, 4f, -4f, scale),
                Offset(footAnchor, 7f, -17f, scale),
                2.5f * scale),
            PawnWeaponRole.BroadDagger => BoundsFromLine(
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

    private static Rectangle CreateBowBounds(
        Vector2 start,
        Vector2 end,
        float scale)
    {
        var verticalPadding = MathF.Max(1f, 1.8f * scale);
        var left = (int)MathF.Floor(
            MathF.Min(start.X, end.X) - (5.2f * scale));
        var top = (int)MathF.Floor(
            MathF.Min(start.Y, end.Y) - verticalPadding);
        var right = (int)MathF.Ceiling(
            MathF.Max(start.X, end.X) + verticalPadding);
        var bottom = (int)MathF.Ceiling(
            MathF.Max(start.Y, end.Y) + verticalPadding);
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
