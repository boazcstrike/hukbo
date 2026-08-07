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
        UiFontSet fonts,
        BattleEventFeed feed,
        BattleEventPanelLayout layout,
        UiTheme theme)
    {
        var captionFont = fonts.Get(UiFontRole.Caption);
        DrawDetailsChrome(spriteBatch, pixel, captionFont, layout, theme);

        if (feed.SelectedEvent is not { } selected)
        {
            UiPrimitives.DrawCenteredText(
                spriteBatch,
                captionFont,
                "Select an event to inspect every field",
                layout.DetailsBounds.Center.ToVector2() +
                    new Vector2(0, UiScaleContext.Pixels(6)),
                theme.Colors.TextSecondary);
            return;
        }

        SynchronizeDetails(
            selected,
            layout.DetailsBounds.Width,
            feed.ScenarioSeed,
            fonts.GetApproximateAdvancePx(UiFontRole.Body));
        DrawDetailLines(spriteBatch, fonts, layout, selected, theme);
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
            UiScaleContext.Pixels(1));
        UiPrimitives.DrawText(
            spriteBatch,
            font,
            "SELECTED EVENT",
            new Vector2(
                layout.DetailsBounds.Left + UiScaleContext.Pixels(9),
                layout.DetailsBounds.Top + UiScaleContext.Pixels(7)),
            theme.Colors.TextDisabled);
    }

    private void DrawDetailLines(
        SpriteBatch spriteBatch,
        UiFontSet fonts,
        BattleEventPanelLayout layout,
        BattleEvent selected,
        UiTheme theme)
    {
        var headFont = fonts.Get(UiFontRole.Body);
        var restFont = fonts.Get(UiFontRole.Caption);
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
            UiPrimitives.DrawText(
                spriteBatch,
                index == 0 ? headFont : restFont,
                _cachedDetails[index],
                new Vector2(lineBounds.Left, lineBounds.Top),
                color);
        }
    }

    private void SynchronizeDetails(
        BattleEvent battleEvent,
        int width,
        ulong scenarioSeed,
        int approximateAdvancePx)
    {
        // The seed joins the cache key for the same reason it joins the row
        // cache's: the source line now carries a warrior name derived from it.
        if (_cachedDetailsSequence == battleEvent.Sequence &&
            _cachedDetailsWidth == width &&
            _cachedDetailsAdvancePx == approximateAdvancePx &&
            _cachedDetailsScenarioSeed == scenarioSeed)
        {
            return;
        }

        // The head row (index 0) draws at the wider Body rung while the
        // rest draw at Caption; using Body's conservative advance estimate
        // here keeps the shared wrap budget safe for the widest rung in
        // this block, at the cost of a little unused margin on the
        // narrower Caption rows.
        var maxCharacters = Math.Max(
            12,
            (width - UiScaleContext.Pixels(18)) /
                approximateAdvancePx);
        _cachedDetails =
        [
            ClipLabel(
                $"{battleEvent.Kind.ToString().ToUpperInvariant()}  " +
                $"SEQUENCE {battleEvent.Sequence}",
                maxCharacters),
            ClipLabel(
                BattleEventFormatter.GetDetailSummaryLine(battleEvent),
                maxCharacters),
            ClipLabel(
                "Source: " +
                    BattleEventFormatter.GetActorLabel(
                        battleEvent,
                        scenarioSeed),
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
        _cachedDetailsAdvancePx = approximateAdvancePx;
        _cachedDetailsScenarioSeed = scenarioSeed;
    }
}
