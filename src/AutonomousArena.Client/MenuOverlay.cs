using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AutonomousArena.Client;

internal sealed class MenuOverlay
{
    private const int PanelWidth = 360;
    private const int PanelHeight = 326;
    private const int ButtonWidth = 280;
    private const int ButtonHeight = 54;
    private const int ButtonGap = 14;

    private static readonly Color BackdropColor = new(4, 8, 16, 190);
    private static readonly Color PanelColor = new(22, 31, 46, 248);
    private static readonly Color BorderColor = new(85, 111, 145);
    private static readonly Color ButtonColor = new(46, 62, 82);
    private static readonly Color HoverColor = new(62, 98, 132);
    private static readonly Color FocusColor = new(54, 78, 104);
    private static readonly Color PressedColor = new(35, 152, 123);
    private static readonly Color DisabledColor = new(34, 42, 52);
    private static readonly Color DisabledTextColor = new(112, 121, 132);

    private readonly MenuButton[] _buttons =
    [
        new("Play", MenuAction.Play),
        new("Pause", MenuAction.Pause),
        new("Exit Game", MenuAction.Exit),
    ];

    private int _focusedButtonIndex;

    public bool IsVisible { get; private set; }

    public void Open()
    {
        IsVisible = true;
        _focusedButtonIndex = 0;
    }

    public void Close()
    {
        IsVisible = false;
        ResetVisualState();
    }

    public MenuAction Update(InputEdges input, Rectangle screenBounds)
    {
        if (!IsVisible)
        {
            return MenuAction.None;
        }

        Layout(screenBounds);

        if (input.WasPressed(Keys.Down) ||
            input.WasPressed(Keys.S) ||
            input.WasPressed(Keys.Tab))
        {
            MoveFocus(1);
        }
        else if (input.WasPressed(Keys.Up) || input.WasPressed(Keys.W))
        {
            MoveFocus(-1);
        }

        var hoveredButtonIndex = -1;
        for (var index = 0; index < _buttons.Length; index++)
        {
            var button = _buttons[index];
            button.IsHovered =
                button.IsEnabled && button.Bounds.Contains(input.MousePosition);
            button.IsFocused =
                button.IsEnabled && index == _focusedButtonIndex;
            button.IsPressed = button.IsHovered && input.IsLeftMouseDown;

            if (button.IsHovered)
            {
                hoveredButtonIndex = index;
            }
        }

        if (hoveredButtonIndex >= 0)
        {
            _focusedButtonIndex = hoveredButtonIndex;
            for (var index = 0; index < _buttons.Length; index++)
            {
                _buttons[index].IsFocused =
                    _buttons[index].IsEnabled &&
                    index == _focusedButtonIndex;
            }
        }

        if (input.WasLeftMousePressed() && hoveredButtonIndex >= 0)
        {
            return _buttons[hoveredButtonIndex].Action;
        }

        if (input.WasPressed(Keys.Enter) || input.WasPressed(Keys.Space))
        {
            var focusedButton = _buttons[_focusedButtonIndex];
            return focusedButton.IsEnabled
                ? focusedButton.Action
                : MenuAction.None;
        }

        return MenuAction.None;
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        Rectangle screenBounds)
    {
        if (!IsVisible)
        {
            return;
        }

        Layout(screenBounds);

        spriteBatch.Draw(pixel, screenBounds, BackdropColor);

        var panelBounds = GetPanelBounds(screenBounds);
        spriteBatch.Draw(pixel, panelBounds, PanelColor);
        DrawBorder(spriteBatch, pixel, panelBounds, BorderColor, 2);

        DrawCenteredText(
            spriteBatch,
            font,
            "AUTONOMOUS ARENA",
            new Vector2(panelBounds.Center.X, panelBounds.Top + 42),
            Color.White);
        DrawCenteredText(
            spriteBatch,
            font,
            "Simulation controls",
            new Vector2(panelBounds.Center.X, panelBounds.Top + 72),
            new Color(160, 181, 204));

        foreach (var button in _buttons)
        {
            var fillColor = GetButtonColor(button);
            var textColor = button.IsEnabled ? Color.White : DisabledTextColor;

            spriteBatch.Draw(pixel, button.Bounds, fillColor);
            if (button.IsFocused && button.IsEnabled)
            {
                DrawBorder(spriteBatch, pixel, button.Bounds, Color.White, 2);
            }

            DrawCenteredText(
                spriteBatch,
                font,
                button.Label,
                button.Bounds.Center.ToVector2(),
                textColor);
        }

        DrawCenteredText(
            spriteBatch,
            font,
            "Esc closes  |  Up/Down selects  |  Enter activates",
            new Vector2(panelBounds.Center.X, panelBounds.Bottom - 23),
            new Color(134, 151, 170),
            0.72f);
    }

    private void Layout(Rectangle screenBounds)
    {
        var panel = GetPanelBounds(screenBounds);
        var buttonLeft = panel.Center.X - (ButtonWidth / 2);
        var buttonTop = panel.Top + 102;

        for (var index = 0; index < _buttons.Length; index++)
        {
            _buttons[index].Bounds = new Rectangle(
                buttonLeft,
                buttonTop + (index * (ButtonHeight + ButtonGap)),
                ButtonWidth,
                ButtonHeight);
        }
    }

    private void MoveFocus(int direction)
    {
        for (var count = 0; count < _buttons.Length; count++)
        {
            _focusedButtonIndex =
                (_focusedButtonIndex + direction + _buttons.Length) %
                _buttons.Length;

            if (_buttons[_focusedButtonIndex].IsEnabled)
            {
                return;
            }
        }
    }

    private static Rectangle GetPanelBounds(Rectangle screenBounds) =>
        new(
            screenBounds.Center.X - (PanelWidth / 2),
            screenBounds.Center.Y - (PanelHeight / 2),
            PanelWidth,
            PanelHeight);

    private static Color GetButtonColor(MenuButton button)
    {
        if (!button.IsEnabled)
        {
            return DisabledColor;
        }

        if (button.IsPressed)
        {
            return PressedColor;
        }

        if (button.IsHovered)
        {
            return HoverColor;
        }

        return button.IsFocused ? FocusColor : ButtonColor;
    }

    private static void DrawBorder(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color color,
        int thickness)
    {
        spriteBatch.Draw(
            pixel,
            new Rectangle(bounds.Left, bounds.Top, bounds.Width, thickness),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(bounds.Left, bounds.Bottom - thickness, bounds.Width, thickness),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(bounds.Left, bounds.Top, thickness, bounds.Height),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(bounds.Right - thickness, bounds.Top, thickness, bounds.Height),
            color);
    }

    private static void DrawCenteredText(
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

    private void ResetVisualState()
    {
        foreach (var button in _buttons)
        {
            button.IsHovered = false;
            button.IsFocused = false;
            button.IsPressed = false;
        }
    }
}
