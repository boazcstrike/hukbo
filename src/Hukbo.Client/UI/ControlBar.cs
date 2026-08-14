using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
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
    private const int LeftPadding = 10;
    private const int RightPadding = 14;
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
        new("Events", ClientCommand.ToggleEventLog),
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

    internal Rectangle GetBounds(Rectangle availableBounds)
    {
        Layout(availableBounds);
        return Bounds;
    }

    internal IReadOnlyList<float> ButtonHoverAmounts =>
        Array.ConvertAll(_buttons, button => button.HoverAmount);

    /// <summary>
    /// The already-computed active state of every button, in the same order
    /// they are laid out. Exists purely for tests — asserting that a toggle
    /// button (for example "Events") reflects its bound visibility flag
    /// without a graphics device. <see cref="Update"/> or <see cref="Draw"/>
    /// must have run at least once before this reflects real state.
    /// </summary>
    internal IReadOnlyList<bool> ButtonActiveStates =>
        Array.ConvertAll(_buttons, button => button.IsActive);

    public UiInteraction Update(
        InputEdges input,
        Rectangle availableBounds,
        bool isPlaying,
        bool isSoundLogVisible,
        bool isEventLogVisible) =>
        Update(
            input,
            availableBounds,
            isPlaying,
            isSoundLogVisible,
            isEventLogVisible,
            TimeSpan.Zero,
            MotionIntensity.Off);

    public UiInteraction Update(
        InputEdges input,
        Rectangle availableBounds,
        bool isPlaying,
        bool isSoundLogVisible,
        bool isEventLogVisible,
        TimeSpan elapsed,
        MotionIntensity motionIntensity)
    {
        Layout(availableBounds);

        foreach (var button in _buttons)
        {
            var isActive = IsButtonActive(
                button.Command,
                isPlaying,
                isSoundLogVisible,
                isEventLogVisible);
            if (button.Update(
                    input,
                    elapsed,
                    motionIntensity,
                    isActive: isActive))
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
        bool isEventLogVisible,
        UiTheme theme)
    {
        Layout(availableBounds);
        SynchronizeVisualState(isPlaying, isSoundLogVisible, isEventLogVisible);

        spriteBatch.Draw(pixel, Bounds, theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            Bounds,
            theme.Colors.PanelBorder,
            UiScaleContext.Pixels(theme.Metrics.BorderThickness));

        var labelFont = fonts.Get(UiFontRole.Label);
        foreach (var button in _buttons)
        {
            button.Draw(spriteBatch, pixel, labelFont, theme);
        }
    }

    private void Layout(Rectangle availableBounds)
    {
        var margin = UiScaleContext.Pixels(Margin);
        var width = Math.Min(
            UiScaleContext.Pixels(BarWidth),
            Math.Max(0, availableBounds.Width));
        var height = Math.Min(
            UiScaleContext.Pixels(BarHeight),
            Math.Max(0, availableBounds.Height));
        Bounds = new Rectangle(
            Math.Max(
                availableBounds.Left,
                availableBounds.Right - width - margin),
            Math.Min(
                availableBounds.Bottom - height,
                availableBounds.Top + margin),
            width,
            height);

        var desiredButtonHeight = UiScaleContext.Pixels(ButtonHeight);
        if (UiScaleContext.ActiveScale != UiScale.Percent100)
        {
            // The baseline's established 34px geometry remains byte-identical
            // at 100%. Larger accessibility tiers must not round the 125%
            // target down below a 44px physical hit target.
            desiredButtonHeight = Math.Max(44, desiredButtonHeight);
        }

        var buttonHeight = Math.Min(desiredButtonHeight, Bounds.Height);
        var buttonTop = Bounds.Top + ((Bounds.Height - buttonHeight) / 2);
        var leftPadding = Math.Min(
            UiScaleContext.Pixels(LeftPadding),
            Bounds.Width);
        var rightPadding = Math.Min(
            UiScaleContext.Pixels(RightPadding),
            Math.Max(0, Bounds.Width - leftPadding));
        var contentWidth = Math.Max(
            0,
            Bounds.Width - leftPadding - rightPadding);
        var gapCount = Math.Max(0, _buttons.Length - 1);
        var buttonGap = gapCount == 0
            ? 0
            : Math.Min(
                UiScaleContext.Pixels(ButtonGap),
                contentWidth / gapCount);
        var maximumButtonWidth = _buttons.Length == 0
            ? 0
            : Math.Max(
                0,
                (contentWidth - (buttonGap * gapCount)) / _buttons.Length);
        var buttonWidth = Math.Min(
            UiScaleContext.Pixels(ButtonWidth),
            maximumButtonWidth);
        var buttonLeft = Bounds.Left + leftPadding;

        for (var index = 0; index < _buttons.Length; index++)
        {
            _buttons[index].Bounds = new Rectangle(
                buttonLeft + (index * (buttonWidth + buttonGap)),
                buttonTop,
                buttonWidth,
                buttonHeight);
        }
    }

    private void SynchronizeVisualState(
        bool isPlaying,
        bool isSoundLogVisible,
        bool isEventLogVisible)
    {
        foreach (var button in _buttons)
        {
            button.UpdateVisualState(
                IsButtonActive(
                    button.Command,
                    isPlaying,
                    isSoundLogVisible,
                    isEventLogVisible));
        }
    }

    private static bool IsButtonActive(
        ClientCommand command,
        bool isPlaying,
        bool isSoundLogVisible,
        bool isEventLogVisible) =>
        command switch
        {
            ClientCommand.Play => isPlaying,
            ClientCommand.Pause => !isPlaying,
            ClientCommand.ToggleSoundLog => isSoundLogVisible,
            ClientCommand.ToggleEventLog => isEventLogVisible,
            _ => false,
        };
}
