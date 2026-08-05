using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

internal readonly record struct UiInteraction(
    ClientCommand Command,
    bool PointerConsumed)
{
    public static UiInteraction None => new(ClientCommand.None, false);
}

internal sealed class UiButton
{
    private readonly UiButtonMotion _motion = new();

    public UiButton(string label, ClientCommand command)
    {
        Label = label;
        Command = command;
    }

    public string Label { get; }

    public ClientCommand Command { get; }

    public Rectangle Bounds { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsHovered { get; private set; }

    public bool IsFocused { get; private set; }

    public bool IsPressed { get; private set; }

    public bool IsActive { get; private set; }

    internal float HoverAmount => _motion.HoverAmount;

    public bool Update(
        InputEdges input,
        bool isFocused = false,
        bool isActive = false) =>
        Update(
            input,
            TimeSpan.Zero,
            MotionIntensity.Off,
            isFocused,
            isActive);

    /// <summary>
    /// Updates interaction state immediately, then advances decorative
    /// feedback with unscaled client time. The hit target remains
    /// <see cref="Bounds"/> throughout the transition.
    /// </summary>
    public bool Update(
        InputEdges input,
        TimeSpan elapsed,
        MotionIntensity motionIntensity,
        bool isFocused = false,
        bool isActive = false)
    {
        IsHovered = IsEnabled && Bounds.Contains(input.MousePosition);
        IsFocused = IsEnabled && isFocused;
        IsPressed = IsHovered && input.IsLeftMouseDown;
        IsActive = IsEnabled && isActive;
        _motion.Advance(
            IsHovered,
            IsFocused,
            IsPressed,
            elapsed,
            motionIntensity);
        return IsHovered && input.WasLeftMousePressed();
    }

    public void ResetVisualState()
    {
        IsHovered = false;
        IsFocused = false;
        IsPressed = false;
        IsActive = false;
        _motion.Reset();
    }

    public void UpdateVisualState(bool isActive)
    {
        IsActive = IsEnabled && isActive;
    }

    /// <summary>
    /// Draws the button at scale 1.0 against a bake the caller already chose
    /// for the size it wants to draw. The caller resolves a
    /// <c>UiFontRole</c> to a <c>SpriteFont</c> through <c>UiFontSet</c>
    /// before calling this method; there is no scaled overload.
    /// </summary>
    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        UiTheme theme)
    {
        var (textColor, visualBounds) =
            DrawBackgroundAndBorder(spriteBatch, pixel, theme);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            Label,
            visualBounds.Center.ToVector2(),
            textColor);
    }

    private (Color TextColor, Rectangle VisualBounds) DrawBackgroundAndBorder(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiTheme theme)
    {
        var fillColor = GetFillColor(theme);
        var visualBounds = _motion.GetVisualBounds(Bounds);
        var textColor = IsEnabled
            ? theme.Colors.TextInverse
            : theme.Colors.TextDisabled;

        spriteBatch.Draw(pixel, visualBounds, fillColor);
        if ((IsFocused || IsActive) && IsEnabled)
        {
            UiPrimitives.DrawBorder(
                spriteBatch,
                pixel,
                Bounds,
                IsFocused
                    ? Color.Lerp(
                        theme.Colors.Selection,
                        theme.Colors.ActionFocus,
                        _motion.FocusAmount)
                    : theme.Colors.Selection,
                UiScaleContext.Pixels(theme.Metrics.FocusThickness));
        }

        if (IsActive && IsEnabled)
        {
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    visualBounds.Left,
                    visualBounds.Top,
                    UiScaleContext.Pixels(6),
                    visualBounds.Height),
                theme.Colors.Selection);
        }

        return (textColor, visualBounds);
    }

    private Color GetFillColor(UiTheme theme)
    {
        if (!IsEnabled)
        {
            return theme.Colors.ActionDisabled;
        }

        var hoverColor = IsActive
            ? theme.Colors.ActionActive
            : Color.Lerp(
                theme.Colors.ActionDefault,
                theme.Colors.ActionHover,
                _motion.HoverAmount);
        return Color.Lerp(
            hoverColor,
            theme.Colors.ActionPressed,
            _motion.PressAmount);
    }

}

internal static class UiPrimitives
{
    public static void DrawBorder(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color color,
        int thickness = 2)
    {
        spriteBatch.Draw(
            pixel,
            new Rectangle(bounds.Left, bounds.Top, bounds.Width, thickness),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Left,
                bounds.Bottom - thickness,
                bounds.Width,
                thickness),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(bounds.Left, bounds.Top, thickness, bounds.Height),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Right - thickness,
                bounds.Top,
                thickness,
                bounds.Height),
            color);
    }

    /// <summary>
    /// Draws text at a whole-pixel top-left origin, at scale 1.0 against a
    /// bake taken at the size the caller intends to draw. There is no float
    /// resampling here: the caller is expected to have already chosen a
    /// <see cref="Theming.UiFontRole"/>-sized font for the string it wants to
    /// draw.
    /// </summary>
    public static void DrawText(
        SpriteBatch spriteBatch,
        SpriteFont font,
        string text,
        Vector2 position,
        Color color)
    {
        var snapped = UiTextGeometry.SnapToPixel(position);
        spriteBatch.DrawString(
            font,
            text,
            snapped,
            color,
            0f,
            Vector2.Zero,
            1f,
            SpriteEffects.None,
            0f);
    }

    /// <summary>
    /// Draws text centred on <paramref name="center"/> at scale 1.0, with the
    /// top-left origin snapped to a whole pixel by
    /// <see cref="UiTextGeometry.GetCenteredTopLeft"/>. There is no scaled
    /// overload; every caller draws against a bake taken at the size it
    /// intends to show.
    /// </summary>
    public static void DrawCenteredText(
        SpriteBatch spriteBatch,
        SpriteFont font,
        string text,
        Vector2 center,
        Color color)
    {
        var measuredSize = font.MeasureString(text);
        var position = UiTextGeometry.GetCenteredTopLeft(measuredSize, center);
        spriteBatch.DrawString(
            font,
            text,
            position,
            color,
            0f,
            Vector2.Zero,
            1f,
            SpriteEffects.None,
            0f);
    }
}
