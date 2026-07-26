using Hukbo.Client.Presentation;
using Hukbo.Client.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client;

internal sealed class MenuOverlay
{
    private const int PanelWidth = 360;
    private const int PanelHeight = 500;
    private const int ButtonWidth = 280;
    private const int ButtonHeight = 54;
    private const int ButtonGap = 14;

    private static readonly Color BackdropColor = new(4, 8, 16, 190);
    private static readonly Color PanelColor = new(22, 31, 46, 248);
    private static readonly Color BorderColor = new(85, 111, 145);
    private readonly UiButton[] _buttons =
    [
        new("Play", ClientCommand.Play),
        new("Pause", ClientCommand.Pause),
        new("Next Round", ClientCommand.NextRound),
        new("Full Reset", ClientCommand.FullReset),
        new("Exit Game", ClientCommand.Exit),
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

    public UiInteraction Update(InputEdges input, Rectangle screenBounds)
    {
        if (!IsVisible)
        {
            return UiInteraction.None;
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
            button.Update(input, index == _focusedButtonIndex);

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
                _buttons[index].Update(
                    input,
                    index == _focusedButtonIndex);
            }
        }

        if (input.WasLeftMousePressed() && hoveredButtonIndex >= 0)
        {
            return new UiInteraction(
                _buttons[hoveredButtonIndex].Command,
                true);
        }

        if (input.WasPressed(Keys.Enter) || input.WasPressed(Keys.Space))
        {
            var focusedButton = _buttons[_focusedButtonIndex];
            return new UiInteraction(
                focusedButton.IsEnabled
                    ? focusedButton.Command
                    : ClientCommand.None,
                true);
        }

        return new UiInteraction(ClientCommand.None, true);
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
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            panelBounds,
            BorderColor,
            2);

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            "HUKBO",
            new Vector2(panelBounds.Center.X, panelBounds.Top + 42),
            Color.White);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            "Simulation controls",
            new Vector2(panelBounds.Center.X, panelBounds.Top + 72),
            new Color(160, 181, 204));

        foreach (var button in _buttons)
        {
            button.Draw(spriteBatch, pixel, font);
        }

        UiPrimitives.DrawCenteredText(
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

    private void ResetVisualState()
    {
        foreach (var button in _buttons)
        {
            button.ResetVisualState();
        }
    }
}
