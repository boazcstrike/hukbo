using Hukbo.Client.Presentation;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

internal sealed class ControlBar
{
    // 10 + 544 + 14: Layout places the first button at Bounds.Left + 10, six
    // buttons at ButtonWidth 84 plus five gaps at ButtonGap 8 is 544 of
    // content, and the bar keeps its existing 14 pixels of right padding.
    // Seven buttons at ButtonWidth 84 is 588, six gaps at ButtonGap 8 is 48,
    // so the content is 636 wide. Layout places the first button at
    // Bounds.Left + 10 and the bar keeps 14 pixels of right padding, giving
    // 10 + 636 + 14. Getting this wrong clips the rightmost button, which is
    // why ControlBarTests asserts every button lies inside the bar.
    private const int BarWidth = 660;
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
        new("Min", ClientCommand.Minimize),
        new("Max", ClientCommand.ToggleMaximize),

        // RequestExit, not Exit: quitting is unrecoverable and this bar is
        // clicked constantly, so it raises the confirmation prompt rather than
        // destroying the battle on one stray click.
        new("Close", ClientCommand.RequestExit),
    ];

    public Rectangle Bounds { get; private set; }

    /// <summary>
    /// Pure hit test over the buttons' already-computed <see cref="UiButton.Bounds"/>.
    /// Exists so tests can assert which command a pixel maps to without
    /// constructing a hardware-backed <see cref="InputEdges"/>. <see cref="Update"/>
    /// is still the runtime input path and does not call this method.
    /// </summary>
    internal ClientCommand? GetCommandAt(Point position) =>
        Array.Find(_buttons, button => button.Bounds.Contains(position))?.Command;

    /// <summary>
    /// The already-computed bounds of every button, in the same order they
    /// are laid out. Exists purely for layout tests — asserting each
    /// button's rectangle sits inside <see cref="Bounds"/> without a
    /// graphics device. <see cref="Layout"/> must have run at least once
    /// (via <see cref="Update"/> or <see cref="Draw"/>) before this reflects
    /// real geometry.
    /// </summary>
    internal IReadOnlyList<Rectangle> ButtonBounds =>
        Array.ConvertAll(_buttons, button => button.Bounds);

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
