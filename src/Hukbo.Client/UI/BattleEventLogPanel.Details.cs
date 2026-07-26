using Hukbo.Client.Presentation;
using Hukbo.Client.Theming;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

/// <summary>
/// Selected-event detail panel drawing for <see cref="BattleEventLogPanel"/>.
/// </summary>
internal sealed partial class BattleEventLogPanel
{
    private void DrawDetails(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        UiTheme theme)
    {
        DrawDetailsChrome(spriteBatch, pixel, font, layout, theme);

        if (feed.SelectedEvent is not { } selected)
        {
            UiPrimitives.DrawCenteredText(
                spriteBatch,
                font,
                "Select an event to inspect every field",
                layout.DetailsBounds.Center.ToVector2() + new Vector2(0, 6),
                theme.Colors.TextSecondary,
                0.54f);
            return;
        }

        SynchronizeDetails(selected, layout.DetailsBounds.Width);
        DrawDetailLines(spriteBatch, font, layout, selected, theme);
    }

    private static void DrawDetailsChrome(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        BattleEventPanelLayout layout,
        UiTheme theme)
    {
        spriteBatch.Draw(
            pixel,
            layout.DetailsBounds,
            theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            layout.DetailsBounds,
            theme.Colors.PanelBorder,
            1);
        spriteBatch.DrawString(
            font,
            "SELECTED EVENT",
            new Vector2(
                layout.DetailsBounds.Left + 9,
                layout.DetailsBounds.Top + 7),
            theme.Colors.TextDisabled,
            0f,
            Vector2.Zero,
            0.53f,
            SpriteEffects.None,
            0f);
    }

    private void DrawDetailLines(
        SpriteBatch spriteBatch,
        SpriteFont font,
        BattleEventPanelLayout layout,
        BattleEvent selected,
        UiTheme theme)
    {
        for (var index = 0; index < _cachedDetails.Length; index++)
        {
            var lineBounds = GetDetailLineBounds(layout, index);
            if (lineBounds.Bottom > layout.DetailsBounds.Bottom)
            {
                break;
            }

            var color = index == 0
                ? GetKindColor(selected.Kind, theme)
                : index == _cachedDetails.Length - 1
                    ? theme.Colors.TextPrimary
                    : theme.Colors.TextSecondary;
            spriteBatch.DrawString(
                font,
                _cachedDetails[index],
                new Vector2(lineBounds.Left, lineBounds.Top),
                color,
                0f,
                Vector2.Zero,
                index == 0 ? 0.61f : 0.55f,
                SpriteEffects.None,
                0f);
        }
    }

    private void SynchronizeDetails(BattleEvent battleEvent, int width)
    {
        if (_cachedDetailsSequence == battleEvent.Sequence &&
            _cachedDetailsWidth == width)
        {
            return;
        }

        var maxCharacters = Math.Max(12, (width - 18) / 7);
        _cachedDetails =
        [
            ClipLabel(
                $"{battleEvent.Kind.ToString().ToUpperInvariant()}  " +
                $"SEQUENCE {battleEvent.Sequence}",
                maxCharacters),
            ClipLabel(
                $"Tick: {battleEvent.Tick}    Value: {battleEvent.Value}",
                maxCharacters),
            ClipLabel(
                $"Source: {BattleEventFormatter.GetActorLabel(battleEvent)}",
                maxCharacters),
            ClipLabel(
                $"Target: {battleEvent.TargetEntityId?.ToString() ?? "none"}",
                maxCharacters),
            ClipLabel(
                $"Faction: {BattleEventFormatter.GetFactionLabel(battleEvent.FactionId)}",
                maxCharacters),
            ClipLabel(
                $"Action: {BattleEventFormatter.GetActionLabel(battleEvent)}",
                maxCharacters),
        ];
        _cachedDetailsSequence = battleEvent.Sequence;
        _cachedDetailsWidth = width;
    }
}
