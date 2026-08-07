using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client.UI;

/// <summary>
/// The geometry of a <see cref="ConfirmationPrompt"/>, computed by a pure
/// function so a test can assert every rectangle without a graphics device.
/// </summary>
internal readonly record struct ConfirmationPromptLayout(
    Rectangle PanelBounds,
    Rectangle MessageBounds,
    Rectangle CancelBounds,
    Rectangle ConfirmBounds);

/// <summary>
/// A modal prompt that asks the spectator to confirm a destructive action
/// before it happens.
/// </summary>
/// <remarks>
/// <para>
/// This is the first modal in the client, so it is deliberately parameterised
/// by its message and by the command it issues on confirmation rather than
/// being written specifically for quitting. The next destructive action that
/// needs a prompt reuses this type instead of inventing a second pattern.
/// </para>
/// <para>
/// <b>Cancel is the initially focused control.</b> For an unrecoverable action
/// the safe option is what a reflexive <c>Enter</c> should reach: the costs are
/// asymmetric, since a wrong cancel costs one extra click while a wrong confirm
/// destroys an in-progress battle that was never saved. Defaulting to confirm
/// would reintroduce the single-keystroke hazard this prompt exists to remove.
/// </para>
/// <para>
/// While open, the prompt sits at the top of the pointer priority chain and
/// reports every click as consumed, including clicks that miss both buttons, so
/// nothing can fall through to the control bar, the arena, or agent selection
/// underneath. It also owns <c>Escape</c>, which must not additionally close an
/// overlay behind it.
/// </para>
/// </remarks>
internal sealed class ConfirmationPrompt
{
    internal const int PanelWidth = 460;
    internal const int PanelHeight = 190;
    internal const int Margin = 24;
    internal const int ButtonWidth = 186;
    internal const int ButtonHeight = 44;
    internal const int ButtonGap = 16;
    internal const int MessageTopOffset = 34;
    internal const int MessageHeight = 60;

    private readonly UiButton _cancel = new("Cancel", ClientCommand.None);
    private readonly UiButton _confirm;
    private readonly UiEntranceMotion _entrance = new();

    /// <param name="message">
    /// The question put to the spectator. Kept short enough to read at a
    /// glance; this prompt does not wrap or scroll.
    /// </param>
    /// <param name="confirmLabel">The affirmative button's label.</param>
    /// <param name="confirmCommand">
    /// The command issued when the spectator confirms. Cancelling never issues
    /// a command.
    /// </param>
    public ConfirmationPrompt(
        string message,
        string confirmLabel,
        ClientCommand confirmCommand)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmLabel);

        if (confirmCommand == ClientCommand.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confirmCommand),
                "A confirmation prompt whose confirm button issues no command " +
                "could never complete the action it is guarding.");
        }

        Message = message;
        _confirm = new UiButton(confirmLabel, confirmCommand);
    }

    public string Message { get; }

    public bool IsVisible { get; private set; }

    public Rectangle Bounds { get; private set; }

    internal float ScrimOpacity => _entrance.ScrimOpacity;

    internal float EntranceOpacity => _entrance.PanelOpacity;

    /// <summary>
    /// True when the confirm button holds focus. Cancel holds it otherwise, and
    /// cancel is where focus starts every time the prompt opens.
    /// </summary>
    public bool IsConfirmFocused { get; private set; }

    /// <summary>
    /// Computes every rectangle from the available bounds. Pure: no graphics
    /// device, no sprite batch, no window.
    /// </summary>
    /// <remarks>
    /// The panel is centred and clamped to the available bounds the same way
    /// <see cref="MatchSummaryPanel"/> clamps, so it cannot overflow at the
    /// smallest supported viewport. Confirm sits to the right of cancel, which
    /// puts the destructive option furthest from the cursor's resting position
    /// after the click that opened the prompt.
    /// </remarks>
    internal static ConfirmationPromptLayout CalculateLayout(
        Rectangle availableBounds)
    {
        var width = Math.Min(
            UiScaleContext.Pixels(PanelWidth),
            Math.Max(0, availableBounds.Width));
        var height = Math.Min(
            UiScaleContext.Pixels(PanelHeight),
            Math.Max(0, availableBounds.Height));
        var panel = new Rectangle(
            availableBounds.Left + ((availableBounds.Width - width) / 2),
            availableBounds.Top + ((availableBounds.Height - height) / 2),
            width,
            height);

        var horizontalMargin = Math.Min(
            UiScaleContext.Pixels(Margin),
            panel.Width / 2);
        var bottomMargin = Math.Min(
            UiScaleContext.Pixels(Margin),
            panel.Height);
        var desiredButtonGap = UiScaleContext.Pixels(ButtonGap);
        var buttonGap = Math.Min(desiredButtonGap, panel.Width);
        var buttonWidth = Math.Min(
            UiScaleContext.Pixels(ButtonWidth),
            Math.Max(0, (panel.Width - buttonGap) / 2));
        var buttonHeight = Math.Min(
            UiScaleContext.Pixels(ButtonHeight),
            Math.Max(0, panel.Height - bottomMargin));
        var buttonTop = panel.Bottom - bottomMargin - buttonHeight;
        var pairWidth = (buttonWidth * 2) + buttonGap;
        var pairLeft = panel.Left + ((panel.Width - pairWidth) / 2);
        var messageTopOffset = Math.Min(
            UiScaleContext.Pixels(MessageTopOffset),
            panel.Height);
        var messageTop = panel.Top + messageTopOffset;
        var message = new Rectangle(
            panel.Left + horizontalMargin,
            messageTop,
            Math.Max(0, panel.Width - (horizontalMargin * 2)),
            Math.Min(
                UiScaleContext.Pixels(MessageHeight),
                Math.Max(0, buttonTop - messageTop)));

        return new ConfirmationPromptLayout(
            panel,
            message,
            new Rectangle(pairLeft, buttonTop, buttonWidth, buttonHeight),
            new Rectangle(
                pairLeft + buttonWidth + buttonGap,
                buttonTop,
                buttonWidth,
                buttonHeight));
    }

    /// <summary>
    /// Opens the prompt with cancel focused.
    /// </summary>
    public void Open()
    {
        IsVisible = true;
        IsConfirmFocused = false;
        _entrance.Begin();
    }

    /// <summary>
    /// Closes the prompt without issuing a command.
    /// </summary>
    public void Close()
    {
        IsVisible = false;
        IsConfirmFocused = false;
        _cancel.ResetVisualState();
        _confirm.ResetVisualState();
        _entrance.Reset();
    }

    /// <summary>
    /// Hit-tests the two buttons without touching input state, for tests and
    /// for the same reason <see cref="MatchSummaryPanel.GetCommandAt"/> exists:
    /// geometry can be asserted without a graphics device.
    /// </summary>
    /// <returns>
    /// The confirm command when <paramref name="position"/> is over the confirm
    /// button, <see cref="ClientCommand.None"/> when it is over cancel, and
    /// <c>null</c> when it is over neither.
    /// </returns>
    internal ClientCommand? GetCommandAt(Point position)
    {
        if (_confirm.Bounds.Contains(position))
        {
            return _confirm.Command;
        }

        return _cancel.Bounds.Contains(position)
            ? ClientCommand.None
            : null;
    }

    /// <summary>
    /// Runs one frame of input. Returns the confirm command when the spectator
    /// confirms, and <see cref="ClientCommand.None"/> otherwise.
    /// </summary>
    /// <remarks>
    /// <see cref="UiInteraction.PointerConsumed"/> is true for every frame the
    /// prompt is open, not only when the pointer is inside the panel. A modal
    /// that let a stray click through to the battle underneath would be worse
    /// than no modal, because the spectator would believe the click was
    /// swallowed.
    /// </remarks>
    public UiInteraction Update(
        InputEdges input,
        Rectangle availableBounds) =>
        Update(
            input,
            availableBounds,
            TimeSpan.Zero,
            MotionIntensity.Off);

    public UiInteraction Update(
        InputEdges input,
        Rectangle availableBounds,
        TimeSpan elapsed,
        MotionIntensity motionIntensity)
    {
        if (!IsVisible)
        {
            Bounds = Rectangle.Empty;
            return UiInteraction.None;
        }

        _entrance.Advance(
            elapsed,
            motionIntensity,
            UiEntranceMotion.ModalPanelDuration,
            hasScrim: true);
        var layout = CalculateLayout(availableBounds);
        Bounds = layout.PanelBounds;
        _cancel.Bounds = layout.CancelBounds;
        _confirm.Bounds = layout.ConfirmBounds;

        if (input.WasPressed(Keys.Left) ||
            input.WasPressed(Keys.Right) ||
            input.WasPressed(Keys.Tab))
        {
            IsConfirmFocused = !IsConfirmFocused;
        }

        if (input.WasPressed(Keys.Escape))
        {
            Close();
            return new UiInteraction(ClientCommand.None, true);
        }

        if (input.WasPressed(Keys.Enter))
        {
            var confirmed = IsConfirmFocused;
            Close();
            return new UiInteraction(
                confirmed ? _confirm.Command : ClientCommand.None,
                true);
        }

        var cancelClicked = _cancel.Update(
            input,
            elapsed,
            motionIntensity,
            isFocused: !IsConfirmFocused);
        var confirmClicked = _confirm.Update(
            input,
            elapsed,
            motionIntensity,
            isFocused: IsConfirmFocused);

        if (cancelClicked)
        {
            Close();
            return new UiInteraction(ClientCommand.None, true);
        }

        if (confirmClicked)
        {
            var command = _confirm.Command;
            Close();
            return new UiInteraction(command, true);
        }

        return new UiInteraction(ClientCommand.None, true);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        Rectangle availableBounds,
        UiTheme theme)
    {
        if (!IsVisible)
        {
            return;
        }

        var layout = CalculateLayout(availableBounds);
        Bounds = layout.PanelBounds;
        _cancel.Bounds = layout.CancelBounds;
        _confirm.Bounds = layout.ConfirmBounds;

        // Scrim the whole area first: it dims the battle behind the prompt and
        // makes the modality visible rather than merely enforced in code.
        var scrimTheme = UiMotionTheme.WithOpacity(theme, ScrimOpacity);
        spriteBatch.Draw(
            pixel,
            availableBounds,
            scrimTheme.Colors.OverlayScrim);
        theme = UiMotionTheme.WithOpacity(theme, EntranceOpacity);
        spriteBatch.Draw(pixel, layout.PanelBounds, theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            layout.PanelBounds,
            theme.Colors.StatusWarning,
            Math.Max(
                UiScaleContext.Pixels(3),
                UiScaleContext.Pixels(theme.Metrics.BorderThickness)));

        if (layout.MessageBounds.Width > 0 &&
            layout.MessageBounds.Height > 0)
        {
            UiPrimitives.DrawCenteredText(
                spriteBatch,
                fonts.Get(UiFontRole.Body),
                Message,
                new Vector2(
                    layout.PanelBounds.Center.X,
                    layout.MessageBounds.Top),
                theme.Colors.TextPrimary);
        }

        var labelFont = fonts.Get(UiFontRole.Label);
        if (layout.CancelBounds.Width > 0 && layout.CancelBounds.Height > 0)
        {
            _cancel.Draw(spriteBatch, pixel, labelFont, theme);
        }

        if (layout.ConfirmBounds.Width > 0 && layout.ConfirmBounds.Height > 0)
        {
            _confirm.Draw(spriteBatch, pixel, labelFont, theme);
        }
    }
}
