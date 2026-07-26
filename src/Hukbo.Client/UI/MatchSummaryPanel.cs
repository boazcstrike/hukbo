using Hukbo.Client.Presentation;
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
    private const int ButtonHeight = 44;
    private const int ButtonGap = 14;

    private static readonly Color PanelColor = new(22, 31, 46, 250);
    private static readonly Color BorderColor = new(103, 132, 166);
    private static readonly Color MutedTextColor = new(162, 178, 196);

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
        SpriteFont font,
        MatchSummary? summary,
        Rectangle arenaContentBounds)
    {
        if (summary is null)
        {
            Bounds = Rectangle.Empty;
            return;
        }

        Layout(arenaContentBounds);
        spriteBatch.Draw(pixel, Bounds, PanelColor);
        UiPrimitives.DrawBorder(spriteBatch, pixel, Bounds, BorderColor, 3);

        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            summary.WinnerLabel == "Draw"
                ? "Draw"
                : $"{summary.WinnerLabel} wins",
            new Vector2(Bounds.Center.X, Bounds.Top + 42),
            Color.White,
            1.05f);
        UiPrimitives.DrawCenteredText(
            spriteBatch,
            font,
            "MATCH COMPLETE",
            new Vector2(Bounds.Center.X, Bounds.Top + 72),
            MutedTextColor,
            0.72f);

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
            button.Draw(spriteBatch, pixel, font, 0.72f);
        }

        void DrawDetail(string text, int row)
        {
            spriteBatch.DrawString(
                font,
                text,
                new Vector2(detailsLeft, detailsTop + (row * 25)),
                Color.White,
                0f,
                Vector2.Zero,
                0.76f,
                SpriteEffects.None,
                0f);
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
