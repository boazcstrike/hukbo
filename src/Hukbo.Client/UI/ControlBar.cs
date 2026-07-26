using Hukbo.Client.Presentation;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

internal sealed class ControlBar
{
    private const int BarWidth = 292;
    private const int BarHeight = 48;
    private const int Margin = 10;
    private const int ButtonGap = 8;
    private const int ButtonWidth = 84;
    private const int ButtonHeight = 34;

    private readonly UiButton[] _buttons =
    [
        new("Play", ClientCommand.Play),
        new("Pause", ClientCommand.Pause),
        new("Menu", ClientCommand.OpenMenu),
    ];

    public Rectangle Bounds { get; private set; }

    public UiInteraction Update(
        InputEdges input,
        Rectangle availableBounds,
        bool isPlaying)
    {
        Layout(availableBounds);

        foreach (var button in _buttons)
        {
            var isActive =
                (button.Command == ClientCommand.Play && isPlaying) ||
                (button.Command == ClientCommand.Pause && !isPlaying);
            if (button.Update(input, isActive: isActive))
            {
                return new UiInteraction(button.Command, true);
            }
        }

        return new UiInteraction(
            ClientCommand.None,
            Bounds.Contains(input.MousePosition));
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        Rectangle availableBounds,
        bool isPlaying,
        UiTheme theme)
    {
        Layout(availableBounds);
        SynchronizeVisualState(isPlaying);

        spriteBatch.Draw(pixel, Bounds, theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            Bounds,
            theme.Colors.PanelBorder,
            theme.Metrics.BorderThickness);

        foreach (var button in _buttons)
        {
            button.Draw(spriteBatch, pixel, font, theme, 0.78f);
        }
    }

    private void Layout(Rectangle availableBounds)
    {
        var width = Math.Min(BarWidth, Math.Max(0, availableBounds.Width));
        var height = Math.Min(BarHeight, Math.Max(0, availableBounds.Height));
        Bounds = new Rectangle(
            Math.Max(availableBounds.Left, availableBounds.Right - width - Margin),
            Math.Min(
                availableBounds.Bottom - height,
                availableBounds.Top + Margin),
            width,
            height);

        var buttonTop = Bounds.Top + ((Bounds.Height - ButtonHeight) / 2);
        var buttonLeft = Bounds.Left + 10;

        for (var index = 0; index < _buttons.Length; index++)
        {
            _buttons[index].Bounds = new Rectangle(
                buttonLeft + (index * (ButtonWidth + ButtonGap)),
                buttonTop,
                ButtonWidth,
                ButtonHeight);
        }
    }

    private void SynchronizeVisualState(bool isPlaying)
    {
        foreach (var button in _buttons)
        {
            var isActive =
                (button.Command == ClientCommand.Play && isPlaying) ||
                (button.Command == ClientCommand.Pause && !isPlaying);
            button.UpdateVisualState(isActive);
        }
    }
}
