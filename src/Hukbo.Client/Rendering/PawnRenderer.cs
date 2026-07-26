using Hukbo.Client.Presentation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.Rendering;

internal enum PawnVisualState
{
    Normal,
    Hovered,
    Selected,
    Dead,
}

internal static class PawnRenderer
{
    private static readonly Color ShadowColor = new(30, 40, 48);
    private static readonly Color OutlineColor = new(10, 15, 21);
    private static readonly Color CharredWood = new(48, 40, 33);
    private static readonly Color Iron = new(56, 66, 73);
    private static readonly Color IronHighlight = new(130, 142, 145);
    private static readonly Color HoverColor = new(231, 199, 84);
    private static readonly Color DeadColor = new(91, 98, 105);
    private static readonly Color HitPulseColor = new(255, 244, 214);

    public static Rectangle GetBounds(
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        float scaleMultiplier = 1f) =>
        PawnGeometry.Create(
            footAnchor,
            cameraZoom,
            appearance,
            scaleMultiplier).VisualBounds;

    public static void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 footAnchor,
        float cameraZoom,
        PawnAppearance appearance,
        Color factionColor,
        PawnVisualState state,
        float scaleMultiplier = 1f,
        float hitPulseStrength = 0f)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(pixel);
        if (!float.IsFinite(hitPulseStrength) ||
            hitPulseStrength is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(hitPulseStrength));
        }

        var layout = PawnGeometry.Create(
            footAnchor,
            cameraZoom,
            appearance,
            scaleMultiplier);
        var isDead = state == PawnVisualState.Dead;
        var clothingColor = ApplyHitPulse(
            ApplyState(appearance.ClothingColor, isDead),
            hitPulseStrength);
        var accentColor = ApplyHitPulse(
            ApplyState(appearance.AccentColor, isDead),
            hitPulseStrength);
        var skinColor = ApplyHitPulse(
            ApplyState(appearance.SkinColor, isDead),
            hitPulseStrength);
        var headTreatmentColor = ApplyHitPulse(
            ApplyState(appearance.HeadTreatmentColor, isDead),
            hitPulseStrength);
        var displayedFactionColor = ApplyHitPulse(
            ApplyState(factionColor, isDead),
            hitPulseStrength);

        DrawGroundBase(
            spriteBatch,
            pixel,
            layout.GroundRingBounds,
            displayedFactionColor);
        DrawSecondaryEquipment(
            spriteBatch,
            pixel,
            layout,
            appearance.WeaponRole,
            isDead);
        DrawTorso(
            spriteBatch,
            pixel,
            layout,
            clothingColor,
            accentColor);
        DrawHead(spriteBatch, pixel, layout.HeadBounds, skinColor);

        if (layout.DetailTier != PawnDetailTier.Low)
        {
            DrawHeadTreatment(
                spriteBatch,
                pixel,
                layout,
                appearance.HeadTreatment,
                headTreatmentColor);
        }

        DrawWeapon(
            spriteBatch,
            pixel,
            layout,
            appearance.WeaponRole,
            isDead);

        if (state is PawnVisualState.Hovered or PawnVisualState.Selected)
        {
            DrawSelectionMark(
                spriteBatch,
                pixel,
                layout.SelectionBounds,
                state == PawnVisualState.Selected
                    ? Color.White
                    : HoverColor,
                state == PawnVisualState.Selected ? 2 : 1);
        }
        else if (isDead)
        {
            DrawDeadMark(
                spriteBatch,
                pixel,
                Rectangle.Union(
                    layout.TorsoBounds,
                    layout.HeadBounds));
        }
    }

    private static void DrawGroundBase(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color factionColor)
    {
        spriteBatch.Draw(pixel, bounds, factionColor);

        var inner = Inset(bounds, 1);
        if (!inner.IsEmpty)
        {
            spriteBatch.Draw(pixel, inner, ShadowColor);
        }
    }

    private static void DrawTorso(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        Color clothingColor,
        Color accentColor)
    {
        DrawSteppedCapsule(
            spriteBatch,
            pixel,
            layout.TorsoBounds,
            OutlineColor);
        DrawSteppedCapsule(
            spriteBatch,
            pixel,
            Inset(layout.TorsoBounds, 1),
            clothingColor);

        if (layout.DetailTier == PawnDetailTier.High)
        {
            var beltHeight = Math.Max(
                1,
                (int)MathF.Round(layout.ApparentScale));
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    layout.TorsoBounds.Left + 1,
                    layout.TorsoBounds.Bottom -
                        Math.Max(2, layout.TorsoBounds.Height / 3),
                    Math.Max(1, layout.TorsoBounds.Width - 2),
                    beltHeight),
                accentColor);
        }
    }

    private static void DrawHead(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color skinColor)
    {
        DrawDisk(spriteBatch, pixel, bounds, OutlineColor);
        DrawDisk(spriteBatch, pixel, Inset(bounds, 1), skinColor);
    }

    private static void DrawHeadTreatment(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        PawnHeadTreatment treatment,
        Color color)
    {
        var bounds = layout.HeadTreatmentBounds;

        switch (treatment)
        {
            case PawnHeadTreatment.CroppedHair:
                spriteBatch.Draw(pixel, bounds, color);
                spriteBatch.Draw(
                    pixel,
                    new Rectangle(
                        bounds.Left,
                        bounds.Bottom,
                        Math.Max(1, bounds.Width / 4),
                        Math.Max(1, bounds.Height / 2)),
                    color);
                break;
            case PawnHeadTreatment.Headcloth:
                spriteBatch.Draw(pixel, bounds, color);
                var tailWidth = Math.Max(
                    1,
                    (int)MathF.Round(layout.ApparentScale));
                var tailTop = Math.Clamp(
                    bounds.Bottom - 1,
                    layout.HeadBounds.Top,
                    layout.HeadBounds.Bottom - 1);
                spriteBatch.Draw(
                    pixel,
                    new Rectangle(
                        layout.HeadBounds.Right - tailWidth,
                        tailTop,
                        tailWidth,
                        layout.HeadBounds.Bottom - tailTop),
                    color);
                break;
            case PawnHeadTreatment.WrappedCloth:
                spriteBatch.Draw(pixel, bounds, color);
                spriteBatch.Draw(
                    pixel,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top + Math.Max(1, bounds.Height / 2),
                        bounds.Width,
                        1),
                    color);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(treatment),
                    treatment,
                    null);
        }
    }

    private static void DrawSecondaryEquipment(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        PawnWeaponRole role,
        bool isDead)
    {
        if (layout.DetailTier == PawnDetailTier.Low ||
            layout.SecondaryEquipmentBounds.IsEmpty ||
            role != PawnWeaponRole.Bolo)
        {
            return;
        }

        var scale = layout.ApparentScale;
        DrawLine(
            spriteBatch,
            pixel,
            layout.FootAnchor + new Vector2(-2f * scale, -4f * scale),
            layout.FootAnchor + new Vector2(-6f * scale, -11f * scale),
            ApplyState(CharredWood, isDead),
            MathF.Max(2f, 2f * scale));
    }

    private static void DrawWeapon(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        PawnWeaponRole role,
        bool isDead)
    {
        var darkWood = ApplyState(CharredWood, isDead);
        var iron = ApplyState(Iron, isDead);
        var ironHighlight = ApplyState(IronHighlight, isDead);

        switch (role)
        {
            case PawnWeaponRole.Bolo:
                DrawBlade(
                    spriteBatch,
                    pixel,
                    layout,
                    darkWood,
                    iron,
                    ironHighlight,
                    gripEnd: 0.30f,
                    widthMultiplier: 2.1f);
                break;
            case PawnWeaponRole.GreatBlade:
                DrawBlade(
                    spriteBatch,
                    pixel,
                    layout,
                    darkWood,
                    iron,
                    ironHighlight,
                    gripEnd: 0.22f,
                    widthMultiplier: 2.45f);
                break;
            case PawnWeaponRole.HeavyChopper:
                DrawBlade(
                    spriteBatch,
                    pixel,
                    layout,
                    darkWood,
                    iron,
                    ironHighlight,
                    gripEnd: 0.28f,
                    widthMultiplier: 2.9f);
                break;
            case PawnWeaponRole.ThrustingBlade:
                DrawBlade(
                    spriteBatch,
                    pixel,
                    layout,
                    darkWood,
                    iron,
                    ironHighlight,
                    gripEnd: 0.16f,
                    widthMultiplier: 1.5f);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(role), role, null);
        }
    }

    private static void DrawBlade(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        PawnLayout layout,
        Color gripColor,
        Color bladeColor,
        Color highlightColor,
        float gripEnd,
        float widthMultiplier)
    {
        var bladeStart = Vector2.Lerp(
            layout.WeaponStart,
            layout.WeaponEnd,
            gripEnd);
        DrawLine(
            spriteBatch,
            pixel,
            layout.WeaponStart,
            bladeStart,
            gripColor,
            MathF.Max(2f, layout.WeaponThickness * 0.85f));
        DrawLine(
            spriteBatch,
            pixel,
            bladeStart,
            layout.WeaponEnd,
            bladeColor,
            MathF.Max(2f, layout.WeaponThickness * widthMultiplier));
        DrawLine(
            spriteBatch,
            pixel,
            bladeStart,
            layout.WeaponEnd,
            highlightColor,
            MathF.Max(1f, layout.WeaponThickness * 0.55f));
    }

    private static void DrawSteppedCapsule(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color color)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        var step = Math.Min(
            Math.Max(1, bounds.Width / 4),
            Math.Max(1, bounds.Height / 4));
        var middle = new Rectangle(
            bounds.Left,
            bounds.Top + step,
            bounds.Width,
            Math.Max(1, bounds.Height - (step * 2)));
        var capWidth = Math.Max(1, bounds.Width - (step * 2));
        var top = new Rectangle(
            bounds.Left + step,
            bounds.Top,
            capWidth,
            Math.Max(1, step));
        var bottom = new Rectangle(
            bounds.Left + step,
            Math.Max(bounds.Top, bounds.Bottom - step),
            capWidth,
            Math.Max(1, step));

        spriteBatch.Draw(pixel, middle, color);
        spriteBatch.Draw(pixel, top, color);
        spriteBatch.Draw(pixel, bottom, color);
    }

    private static void DrawDisk(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color color)
    {
        if (bounds.IsEmpty)
        {
            return;
        }

        var bandHeight = Math.Max(1, bounds.Height / 4);
        var inset = Math.Max(1, bounds.Width / 5);
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Left,
                bounds.Top + bandHeight,
                bounds.Width,
                Math.Max(1, bounds.Height - (bandHeight * 2))),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Left + inset,
                bounds.Top,
                Math.Max(1, bounds.Width - (inset * 2)),
                bandHeight),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Left + inset,
                bounds.Bottom - bandHeight,
                Math.Max(1, bounds.Width - (inset * 2)),
                bandHeight),
            color);
    }

    private static void DrawSelectionMark(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color color,
        int thickness)
    {
        var cornerLength = Math.Clamp(
            Math.Min(bounds.Width, bounds.Height) / 4,
            4,
            10);
        var right = bounds.Right - 1;
        var bottom = bounds.Bottom - 1;

        DrawCorner(bounds.Left, bounds.Top, 1, 1);
        DrawCorner(right, bounds.Top, -1, 1);
        DrawCorner(bounds.Left, bottom, 1, -1);
        DrawCorner(right, bottom, -1, -1);

        void DrawCorner(int x, int y, int horizontal, int vertical)
        {
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    horizontal > 0 ? x : x - cornerLength + 1,
                    vertical > 0 ? y : y - thickness + 1,
                    cornerLength,
                    thickness),
                color);
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    horizontal > 0 ? x : x - thickness + 1,
                    vertical > 0 ? y : y - cornerLength + 1,
                    thickness,
                    cornerLength),
                color);
        }
    }

    private static void DrawDeadMark(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds)
    {
        DrawLine(
            spriteBatch,
            pixel,
            new Vector2(bounds.Left, bounds.Top),
            new Vector2(bounds.Right, bounds.Bottom),
            DeadColor,
            2f);
        DrawLine(
            spriteBatch,
            pixel,
            new Vector2(bounds.Right, bounds.Top),
            new Vector2(bounds.Left, bounds.Bottom),
            DeadColor,
            2f);
    }

    private static void DrawLine(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 start,
        Vector2 end,
        Color color,
        float thickness)
    {
        var delta = end - start;
        var length = delta.Length();
        if (length <= float.Epsilon)
        {
            return;
        }

        spriteBatch.Draw(
            pixel,
            start,
            sourceRectangle: null,
            color,
            MathF.Atan2(delta.Y, delta.X),
            new Vector2(0f, 0.5f),
            new Vector2(length, MathF.Max(1f, thickness)),
            SpriteEffects.None,
            layerDepth: 0f);
    }

    private static Rectangle Inset(Rectangle bounds, int amount)
    {
        var width = bounds.Width - (amount * 2);
        var height = bounds.Height - (amount * 2);
        return width <= 0 || height <= 0
            ? Rectangle.Empty
            : new Rectangle(
                bounds.X + amount,
                bounds.Y + amount,
                width,
                height);
    }

    private static Color ApplyState(Color color, bool isDead) =>
        isDead ? Color.Lerp(color, DeadColor, 0.68f) : color;

    private static Color ApplyHitPulse(Color color, float strength) =>
        Color.Lerp(color, HitPulseColor, strength * 0.55f);
}
