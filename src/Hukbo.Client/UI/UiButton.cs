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

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        UiTheme theme,
        float textScale = 1f)
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

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            Label,
            Bounds.Center.ToVector2(),
            textColor,
            textScale);
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
