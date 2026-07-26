using Hukbo.Client.Presentation;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

internal sealed class BattleEventLogPanel
{
    private const int Padding = 12;
    private const int HeaderHeight = 43;
    private const int FooterHeight = 24;
    private const int RowHeight = 22;
    private const int RowsPerWheelDetent = 3;

    private static readonly Color PanelColor = new(14, 21, 33, 246);
    private static readonly Color BorderColor = new(76, 96, 121);
    private static readonly Color MutedTextColor = new(144, 162, 183);
    private static readonly Color NewEventColor = new(231, 199, 84);

    private readonly List<FormattedEvent> _formattedRows = [];

    public Rectangle Bounds { get; private set; }

    public UiInteraction Update(
        InputEdges input,
        BattleEventFeed feed,
        Rectangle bounds)
    {
        Bounds = bounds;
        var pointerConsumed = Bounds.Contains(input.MousePosition);
        if (!pointerConsumed || input.ScrollWheelDelta == 0)
        {
            return new UiInteraction(ClientCommand.None, pointerConsumed);
        }

        var detents = Math.Max(
            1,
            Math.Abs(input.ScrollWheelDelta) /
            MouseWheelDeltaPerDetent);
        var direction = input.ScrollWheelDelta > 0 ? -1 : 1;
        feed.Scroll(
            direction * detents * RowsPerWheelDetent,
            GetVisibleRowCount());
        return new UiInteraction(ClientCommand.None, true);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        BattleEventFeed feed,
        Rectangle bounds)
    {
        Bounds = bounds;
        SynchronizeCache(feed.Entries);

        spriteBatch.Draw(pixel, Bounds, PanelColor);
        UiPrimitives.DrawBorder(spriteBatch, pixel, Bounds, BorderColor);
        spriteBatch.DrawString(
            font,
            $"BATTLE EVENTS  {feed.Entries.Count}",
            new Vector2(Bounds.Left + Padding, Bounds.Top + Padding),
            Color.White,
            0f,
            Vector2.Zero,
            0.76f,
            SpriteEffects.None,
            0f);

        var visibleRows = feed.GetVisibleEntries(GetVisibleRowCount());
        var rowY = Bounds.Top + HeaderHeight;
        for (var index = 0; index < visibleRows.Length; index++)
        {
            var battleEvent = visibleRows[index];
            spriteBatch.DrawString(
                font,
                GetFormattedRow(battleEvent),
                new Vector2(Bounds.Left + Padding, rowY + (index * RowHeight)),
                MutedTextColor,
                0f,
                Vector2.Zero,
                0.62f,
                SpriteEffects.None,
                0f);
        }

        var footer = feed.IsPinnedToBottom
            ? "Wheel: scroll history"
            : "New events below";
        spriteBatch.DrawString(
            font,
            footer,
            new Vector2(
                Bounds.Left + Padding,
                Bounds.Bottom - FooterHeight + 3),
            feed.IsPinnedToBottom ? MutedTextColor : NewEventColor,
            0f,
            Vector2.Zero,
            0.62f,
            SpriteEffects.None,
            0f);
    }

    private const int MouseWheelDeltaPerDetent = 120;

    private int GetVisibleRowCount()
    {
        var availableHeight =
            Math.Max(0, Bounds.Height - HeaderHeight - FooterHeight);
        return Math.Max(1, availableHeight / RowHeight);
    }

    private string GetFormattedRow(BattleEvent battleEvent)
    {
        for (var index = 0; index < _formattedRows.Count; index++)
        {
            var formattedRow = _formattedRows[index];
            if (formattedRow.BattleEvent == battleEvent)
            {
                return formattedRow.Text;
            }

            if (formattedRow.BattleEvent.Sequence == battleEvent.Sequence)
            {
                _formattedRows.RemoveAt(index);
                break;
            }
        }

        var text = FormatEvent(battleEvent);
        _formattedRows.Add(new FormattedEvent(battleEvent, text));
        return text;
    }

    private void SynchronizeCache(IReadOnlyList<BattleEvent> entries)
    {
        if (entries.Count == 0)
        {
            _formattedRows.Clear();
            return;
        }

        var oldestSequence = entries[0].Sequence;
        var newestSequence = entries[^1].Sequence;
        for (var index = _formattedRows.Count - 1; index >= 0; index--)
        {
            var sequence = _formattedRows[index].BattleEvent.Sequence;
            if (sequence < oldestSequence || sequence > newestSequence)
            {
                _formattedRows.RemoveAt(index);
            }
        }
    }

    private static string FormatEvent(BattleEvent battleEvent)
    {
        var prefix = $"T{battleEvent.Tick:00000}  ";
        var actor = $"{GetFactionLabel(battleEvent.FactionId)} #{battleEvent.SourceEntityId}";
        var target = battleEvent.TargetEntityId is { } targetId
            ? $"#{targetId}"
            : "none";

        return battleEvent.Kind switch
        {
            BattleEventKind.Move =>
                $"{prefix}{actor} moved toward {target}",
            BattleEventKind.Attack =>
                $"{prefix}{actor} hit {target} for {battleEvent.Value}",
            BattleEventKind.Damage =>
                $"{prefix}{actor} took {battleEvent.Value} damage",
            BattleEventKind.Death =>
                $"{prefix}{actor} died",
            BattleEventKind.Outcome =>
                $"{prefix}{GetOutcomeLabel(battleEvent.FactionId)}",
            _ =>
                $"{prefix}Unknown event",
        };
    }

    private static string GetFactionLabel(int? factionId) =>
        factionId switch
        {
            0 => "Blue",
            1 => "Red",
            int value => $"Faction {value}",
            null => "Agent",
        };

    private static string GetOutcomeLabel(int? factionId) =>
        factionId switch
        {
            0 => "Blue wins",
            1 => "Red wins",
            _ => "Draw",
        };

    private readonly record struct FormattedEvent(
        BattleEvent BattleEvent,
        string Text);
}
