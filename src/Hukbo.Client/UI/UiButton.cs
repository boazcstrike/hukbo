using Hukbo.Client.Presentation;
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
    private static readonly Color ButtonColor = new(46, 62, 82);
    private static readonly Color HoverColor = new(62, 98, 132);
    private static readonly Color FocusColor = new(54, 78, 104);
    private static readonly Color ActiveColor = new(35, 152, 123);
    private static readonly Color DisabledColor = new(34, 42, 52);
    private static readonly Color DisabledTextColor = new(112, 121, 132);

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

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        float textScale = 1f)
    {
        var fillColor = GetFillColor();
        var textColor = IsEnabled ? Color.White : DisabledTextColor;

        spriteBatch.Draw(pixel, Bounds, fillColor);
        if ((IsFocused || IsActive) && IsEnabled)
        {
            UiPrimitives.DrawBorder(spriteBatch, pixel, Bounds, Color.White, 2);
        }

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            Label,
            Bounds.Center.ToVector2(),
            textColor,
            textScale);
    }

    private Color GetFillColor()
    {
        if (!IsEnabled)
        {
            return DisabledColor;
        }

        if (IsPressed || IsActive)
        {
            return ActiveColor;
        }

        if (IsHovered)
        {
            return HoverColor;
        }

        return IsFocused ? FocusColor : ButtonColor;
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

    public static void DrawCenteredText(
        SpriteBatch spriteBatch,
        SpriteFont font,
        string text,
        Vector2 center,
        Color color,
        float scale = 1f)
    {
        var size = font.MeasureString(text) * scale;
        var position = center - (size / 2f);
        spriteBatch.DrawString(
            font,
            text,
            position,
            color,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f);
    }
}
