using Hukbo.Client.Presentation;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

internal sealed class MatchSummaryPanel
{
    private const int PreferredWidth = 500;

    // 310 plus the full-width battle-report button's own ButtonHeight (44)
    // and its ButtonGap (14) above the existing two-button row: 310 + 58 =
    // 368.
    private const int PreferredHeight = 368;
    private const int MinimumWidth = 360;
    private const int Margin = 20;
    private const int ButtonWidth = 198;

    // Carries the Label rung (measured 29px real line spacing).
    private const int ButtonHeight = 44;
    private const int ButtonGap = 14;

    // _buttons holds the two side-by-side buttons (NextRound, OpenMenu)
    // followed by the full-width battle-report button. Layout positions the
    // first PairButtonCount entries with the original paired formula and the
    // remaining entry separately, so the paired buttons' geometry stays
    // byte-for-byte what it was before this button was added.
    private const int PairButtonCount = 2;

    // Summary detail lines draw at the Body rung (measured 24px real line
    // spacing); 25 clears it with a 1px margin.
    private const int DetailLineHeight = 25;

    // Vertical center offsets (from Bounds.Top) for the two centered header
    // lines. DrawCenteredText measures a single-line string's height as the
    // font's full real vertical line spacing, so the winner line (Subtitle,
    // measured 34px) occupies WinnerLineTopOffset +/- 17, and the header
    // (Title, measured 35px) occupies HeaderTopOffset +/- 17.5. The previous
    // pair (42, 72) was tuned before these rungs carried their real bakes:
    // the winner line's box bottom (59) fell below the header's box top
    // (54.5), a 4.5px overlap. HeaderTopOffset is raised to 80 to clear it
    // with a small margin.
    private const int WinnerLineTopOffset = 42;
    private const int HeaderTopOffset = 80;

    private readonly UiButton[] _buttons =
    [
        new("Next Round", ClientCommand.NextRound),
        new("Menu", ClientCommand.OpenMenu),
        new("Battle Report", ClientCommand.ToggleBattleReport),
    ];
    private readonly UiEntranceMotion _entrance = new();
    private bool _wasVisible;

    public Rectangle Bounds { get; private set; }

    internal float EntranceOpacity => _entrance.PanelOpacity;

    internal IReadOnlyList<Rectangle> ButtonBounds =>
        Array.ConvertAll(_buttons, button => button.Bounds);

    /// <summary>
    /// Pure hit test over the buttons' already-computed <see cref="UiButton.Bounds"/>.
    /// Exists so tests can assert which command a pixel maps to without
    /// constructing a hardware-backed <see cref="InputEdges"/> or a graphics
    /// device. <see cref="Update"/> is still the runtime input path and does
    /// not call this method.
    /// </summary>
    internal ClientCommand? GetCommandAt(Point position) =>
        Array.Find(_buttons, button => button.Bounds.Contains(position))?.Command;

    public UiInteraction Update(
        InputEdges input,
        MatchSummary? summary,
        Rectangle arenaContentBounds) =>
        Update(
            input,
            summary,
            arenaContentBounds,
            TimeSpan.Zero,
            MotionIntensity.Off);

    public UiInteraction Update(
        InputEdges input,
        MatchSummary? summary,
        Rectangle arenaContentBounds,
        TimeSpan elapsed,
        MotionIntensity motionIntensity)
    {
        if (summary is null)
        {
            Bounds = Rectangle.Empty;
            ResetVisualState();
            return UiInteraction.None;
        }

        if (!_wasVisible)
        {
            _wasVisible = true;
            _entrance.Begin();
        }

        _entrance.Advance(
            elapsed,
            motionIntensity,
            UiEntranceMotion.SummaryPanelDuration,
            hasScrim: false);
        Layout(arenaContentBounds);
        foreach (var button in _buttons)
        {
            if (button.Update(input, elapsed, motionIntensity))
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
        MatchSummary? summary,
        Rectangle arenaContentBounds,
        UiTheme theme)
    {
        if (summary is null)
        {
            Bounds = Rectangle.Empty;
            return;
        }

        Layout(arenaContentBounds);
        theme = UiMotionTheme.WithOpacity(theme, EntranceOpacity);
        spriteBatch.Draw(pixel, Bounds, theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            Bounds,
            theme.Colors.PanelBorder,
            Math.Max(
                UiScaleContext.Pixels(3),
                UiScaleContext.Pixels(theme.Metrics.BorderThickness)));

        var subtitleFont = fonts.Get(UiFontRole.Subtitle);
        var titleFont = fonts.Get(UiFontRole.Title);
        var labelFont = fonts.Get(UiFontRole.Label);
        var bodyFont = fonts.Get(UiFontRole.Body);

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            subtitleFont,
            summary.WinnerLabel == "Draw"
                ? "Draw"
                : $"{summary.WinnerLabel} wins",
            new Vector2(
                Bounds.Center.X,
                Bounds.Top + UiScaleContext.Pixels(WinnerLineTopOffset)),
            theme.Colors.TextPrimary);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            titleFont,
            "MATCH COMPLETE",
            new Vector2(
                Bounds.Center.X,
                Bounds.Top + UiScaleContext.Pixels(HeaderTopOffset)),
            theme.Colors.TextSecondary);

        var detailLineHeight = UiScaleContext.Pixels(DetailLineHeight);
        var detailsLeft = Bounds.Left + UiScaleContext.Pixels(45);
        var detailsTop = Bounds.Top + UiScaleContext.Pixels(105);
        DrawDetail(
            $"Survivors: Blue {summary.BlueSurvivors} / Red {summary.RedSurvivors}",
            0);
        DrawDetail($"Terminal tick: {summary.TerminalTick:N0}", 1);
        DrawDetail(
            $"Simulated duration: {summary.SimulatedDurationSeconds:0.00} s",
            2);
        DrawDetail($"Seed: {summary.Seed}", 3);

        foreach (var button in _buttons)
        {
            button.Draw(spriteBatch, pixel, labelFont, theme);
        }

        void DrawDetail(string text, int row)
        {
            var rowTop = detailsTop + (row * detailLineHeight);
            if (rowTop + detailLineHeight > _buttons[PairButtonCount].Bounds.Top)
            {
                return;
            }

            UiPrimitives.DrawText(
                spriteBatch,
                bodyFont,
                text,
                new Vector2(detailsLeft, rowTop),
                theme.Colors.TextPrimary);
        }
    }

    private void Layout(Rectangle arenaContentBounds)
    {
        var margin = UiScaleContext.Pixels(Margin);
        var width = Math.Min(
            UiScaleContext.Pixels(PreferredWidth),
            Math.Max(
                UiScaleContext.Pixels(MinimumWidth),
                arenaContentBounds.Width - (margin * 2)));
        width = Math.Min(width, Math.Max(0, arenaContentBounds.Width));
        var height = Math.Min(
            UiScaleContext.Pixels(PreferredHeight),
            Math.Max(0, arenaContentBounds.Height - (margin * 2)));
        Bounds = new Rectangle(
            arenaContentBounds.Center.X - (width / 2),
            arenaContentBounds.Center.Y - (height / 2),
            width,
            height);

        var horizontalGap = Math.Min(
            UiScaleContext.Pixels(ButtonGap),
            Bounds.Width);
        var buttonWidth = Math.Min(
            UiScaleContext.Pixels(ButtonWidth),
            Math.Max(0, (Bounds.Width - horizontalGap) / 2));
        var totalButtonWidth = (buttonWidth * 2) + horizontalGap;
        var buttonLeft = Bounds.Center.X - (totalButtonWidth / 2);
        var bottomPadding = Math.Min(
            UiScaleContext.Pixels(18),
            Bounds.Height);
        var availableButtonHeight = Math.Max(
            0,
            Bounds.Height - bottomPadding);
        var verticalGap = Math.Min(
            UiScaleContext.Pixels(ButtonGap),
            availableButtonHeight);
        var buttonHeight = Math.Min(
            UiScaleContext.Pixels(ButtonHeight),
            Math.Max(0, (availableButtonHeight - verticalGap) / 2));
        var buttonTop = Bounds.Bottom - bottomPadding - buttonHeight;
        for (var index = 0; index < PairButtonCount; index++)
        {
            _buttons[index].Bounds = new Rectangle(
                buttonLeft + (index * (buttonWidth + horizontalGap)),
                buttonTop,
                buttonWidth,
                buttonHeight);
        }

        // The full-width battle-report button sits directly above the
        // NextRound/OpenMenu row, spanning the same left edge and combined
        // width those two buttons occupy together, separated from the row
        // by the same ButtonGap used between them.
        _buttons[PairButtonCount].Bounds = new Rectangle(
            buttonLeft,
            buttonTop - verticalGap - buttonHeight,
            totalButtonWidth,
            buttonHeight);
    }

    private void ResetVisualState()
    {
        _wasVisible = false;
        _entrance.Reset();
        foreach (var button in _buttons)
        {
            button.ResetVisualState();
        }
    }
}
