using Hukbo.Client.Presentation;
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

    public bool Update(
        InputEdges input,
        bool isFocused = false,
        bool isActive = false)
    {
        IsHovered = IsEnabled && Bounds.Contains(input.MousePosition);
        IsFocused = IsEnabled && isFocused;
        IsPressed = IsHovered && input.IsLeftMouseDown;
        IsActive = IsEnabled && isActive;
        return IsHovered && input.WasLeftMousePressed();
    }

    public void ResetVisualState()
    {
        IsHovered = false;
        IsFocused = false;
        IsPressed = false;
        IsActive = false;
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
        var textColor = DrawBackgroundAndBorder(spriteBatch, pixel, theme);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            Label,
            Bounds.Center.ToVector2(),
            textColor);
    }

    private Color DrawBackgroundAndBorder(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiTheme theme)
    {
        var fillColor = GetFillColor(theme);
        var textColor = IsEnabled
            ? theme.Colors.TextInverse
            : theme.Colors.TextDisabled;

        spriteBatch.Draw(pixel, Bounds, fillColor);
        if ((IsFocused || IsActive) && IsEnabled)
        {
            UiPrimitives.DrawBorder(
                spriteBatch,
                pixel,
                Bounds,
                IsFocused
                    ? theme.Colors.ActionFocus
                    : theme.Colors.Selection,
                theme.Metrics.FocusThickness);
        }

        if (IsActive && IsEnabled)
        {
            spriteBatch.Draw(
                pixel,
                new Rectangle(Bounds.Left, Bounds.Top, 6, Bounds.Height),
                theme.Colors.Selection);
        }

        return textColor;
    }

    private Color GetFillColor(UiTheme theme)
    {
        if (!IsEnabled)
        {
            return theme.Colors.ActionDisabled;
        }

        if (IsPressed)
        {
            return theme.Colors.ActionPressed;
        }

        if (IsActive)
        {
            return theme.Colors.ActionActive;
        }

        if (IsHovered)
        {
            return theme.Colors.ActionHover;
        }

        return theme.Colors.ActionDefault;
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
