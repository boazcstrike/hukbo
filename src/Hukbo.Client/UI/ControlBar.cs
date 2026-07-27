using Hukbo.Client.Presentation;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

internal sealed class ControlBar
{
    private const int BarWidth = 384;
    private const int BarHeight = 48;
    private const int Margin = 10;
    private const int ButtonGap = 8;
    private const int ButtonWidth = 84;

    // Button labels draw at the Label rung (measured 29px real line
    // spacing); 34 clears it.
    private const int ButtonHeight = 34;

    private readonly UiButton[] _buttons =
    [
        new("Play", ClientCommand.Play),
        new("Pause", ClientCommand.Pause),
        new("Menu", ClientCommand.OpenMenu),
        new("Sounds", ClientCommand.ToggleSoundLog),
    ];

    public Rectangle Bounds { get; private set; }

    public UiInteraction Update(
        InputEdges input,
        Rectangle availableBounds,
        bool isPlaying,
        bool isSoundLogVisible)
    {
        Layout(availableBounds);

        foreach (var button in _buttons)
        {
            var isActive = IsButtonActive(
                button.Command,
                isPlaying,
                isSoundLogVisible);
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
        UiFontSet fonts,
        Rectangle availableBounds,
        bool isPlaying,
        bool isSoundLogVisible,
        UiTheme theme)
    {
        Layout(availableBounds);
        SynchronizeVisualState(isPlaying, isSoundLogVisible);

        spriteBatch.Draw(pixel, Bounds, theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            Bounds,
            theme.Colors.PanelBorder,
            theme.Metrics.BorderThickness);

        var labelFont = fonts.Get(UiFontRole.Label);
        foreach (var button in _buttons)
        {
            button.Draw(spriteBatch, pixel, labelFont, theme);
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

    private void SynchronizeVisualState(
        bool isPlaying,
        bool isSoundLogVisible)
    {
        foreach (var button in _buttons)
        {
            button.UpdateVisualState(
                IsButtonActive(
                    button.Command,
                    isPlaying,
                    isSoundLogVisible));
        }
    }

    private static bool IsButtonActive(
        ClientCommand command,
        bool isPlaying,
        bool isSoundLogVisible) =>
        command switch
        {
            ClientCommand.Play => isPlaying,
            ClientCommand.Pause => !isPlaying,
            ClientCommand.ToggleSoundLog => isSoundLogVisible,
            _ => false,
        };
}
