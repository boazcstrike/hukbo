using Hukbo.Client.Presentation;
using Hukbo.Client.Theming;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

internal sealed class MatchSummaryPanel
{
    private const int PreferredWidth = 500;
    private const int PreferredHeight = 310;
    private const int MinimumWidth = 360;
    private const int Margin = 20;
    private const int ButtonWidth = 198;

    // Carries the Label rung (measured 29px real line spacing).
    private const int ButtonHeight = 44;
    private const int ButtonGap = 14;

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
    ];

    public Rectangle Bounds { get; private set; }

    public UiInteraction Update(
        InputEdges input,
        MatchSummary? summary,
        Rectangle arenaContentBounds)
    {
        if (summary is null)
        {
            Bounds = Rectangle.Empty;
            ResetVisualState();
            return UiInteraction.None;
        }

        Layout(arenaContentBounds);
        foreach (var button in _buttons)
        {
            if (button.Update(input))
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
        spriteBatch.Draw(pixel, Bounds, theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            Bounds,
            theme.Colors.PanelBorder,
            Math.Max(3, theme.Metrics.BorderThickness));

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
            new Vector2(Bounds.Center.X, Bounds.Top + WinnerLineTopOffset),
            theme.Colors.TextPrimary);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            titleFont,
            "MATCH COMPLETE",
            new Vector2(Bounds.Center.X, Bounds.Top + HeaderTopOffset),
            theme.Colors.TextSecondary);

        var detailsLeft = Bounds.Left + 45;
        var detailsTop = Bounds.Top + 105;
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
            UiPrimitives.DrawText(
                spriteBatch,
                bodyFont,
                text,
                new Vector2(detailsLeft, detailsTop + (row * DetailLineHeight)),
                theme.Colors.TextPrimary);
        }
    }

    private void Layout(Rectangle arenaContentBounds)
    {
        var width = Math.Min(
            PreferredWidth,
            Math.Max(
                MinimumWidth,
                arenaContentBounds.Width - (Margin * 2)));
        width = Math.Min(width, Math.Max(0, arenaContentBounds.Width));
        var height = Math.Min(
            PreferredHeight,
            Math.Max(0, arenaContentBounds.Height - (Margin * 2)));
        Bounds = new Rectangle(
            arenaContentBounds.Center.X - (width / 2),
            arenaContentBounds.Center.Y - (height / 2),
            width,
            height);

        var buttonTop = Bounds.Bottom - ButtonHeight - 18;
        var totalButtonWidth = (ButtonWidth * 2) + ButtonGap;
        var buttonLeft = Bounds.Center.X - (totalButtonWidth / 2);
        for (var index = 0; index < _buttons.Length; index++)
        {
            _buttons[index].Bounds = new Rectangle(
                buttonLeft + (index * (ButtonWidth + ButtonGap)),
                buttonTop,
                ButtonWidth,
                ButtonHeight);
        }
    }

    private void ResetVisualState()
    {
        foreach (var button in _buttons)
        {
            button.ResetVisualState();
        }
    }
}
