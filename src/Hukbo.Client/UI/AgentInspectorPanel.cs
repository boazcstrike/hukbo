using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

internal sealed class AgentInspectorPanel
{
    private const int Padding = 14;
    private const int AccentWidth = 5;
    private const int PortraitSize = 56;
    private const int PortraitGap = 10;
    private const int LineHeight = 19;

    private static readonly Color PanelColor = new(17, 25, 38, 238);
    private static readonly Color PortraitColor = new(9, 15, 24);
    private static readonly Color BorderColor = new(76, 96, 121);
    private static readonly Color BlueColor = new(64, 164, 255);
    private static readonly Color RedColor = new(255, 91, 105);
    private static readonly Color OtherFactionColor = new(231, 199, 84);

    public Rectangle Bounds { get; private set; }

    public UiInteraction Update(
        InputEdges input,
        AgentView? agent,
        Rectangle bounds)
    {
        if (agent is null)
        {
            Bounds = Rectangle.Empty;
            return UiInteraction.None;
        }

        Bounds = bounds;
        return new UiInteraction(
            ClientCommand.None,
            Bounds.Contains(input.MousePosition));
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        AgentView? agent,
        Rectangle bounds)
    {
        if (agent is not { } selected)
        {
            Bounds = Rectangle.Empty;
            return;
        }

        Bounds = bounds;
        spriteBatch.Draw(pixel, Bounds, PanelColor);
        UiPrimitives.DrawBorder(spriteBatch, pixel, Bounds, BorderColor);

        var textX = Bounds.Left + Padding + AccentWidth;
        var textY = Bounds.Top + Padding;
        spriteBatch.DrawString(
            font,
            "AGENT INSPECTOR",
            new Vector2(textX, textY),
            Color.White,
            0f,
            Vector2.Zero,
            0.78f,
            SpriteEffects.None,
            0f);

        textY += 31;
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                Bounds.Left + 2,
                Bounds.Top + 2,
                AccentWidth,
                Math.Max(0, Bounds.Height - 4)),
            GetFactionColor(selected.FactionId));

        var appearance = PawnAppearanceFactory.Create(selected.EntityId);
        var factionLabel = GetFactionLabel(selected.FactionId);
        var stateLabel = selected.IsAlive ? "ALIVE" : "DEAD";
        var targetLabel = selected.TargetEntityId?.ToString() ?? "none";
        var x = selected.XRaw / (double)FixedPoint.Scale;
        var y = selected.YRaw / (double)FixedPoint.Scale;
        var portraitBounds = new Rectangle(
            textX,
            textY,
            PortraitSize,
            PortraitSize);
        spriteBatch.Draw(pixel, portraitBounds, PortraitColor);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            portraitBounds,
            GetFactionColor(selected.FactionId),
            1);
        PawnRenderer.Draw(
            spriteBatch,
            pixel,
            new Vector2(
                portraitBounds.Center.X,
                portraitBounds.Bottom - 7),
            cameraZoom: 1f,
            appearance,
            GetFactionColor(selected.FactionId),
            selected.IsAlive
                ? PawnVisualState.Normal
                : PawnVisualState.Dead,
            scaleMultiplier: 1f);

        var detailX = portraitBounds.Right + PortraitGap;
        DrawLine($"ID: {selected.EntityId}", detailX, textY, 0);
        DrawLine($"Faction: {factionLabel}", detailX, textY, 1);
        DrawLine($"State: {stateLabel}", detailX, textY, 2);
        DrawLine(
            $"HP: {selected.HitPoints}/{selected.MaximumHitPoints}",
            detailX,
            textY,
            3);

        var lowerTextY = Math.Max(
            portraitBounds.Bottom + 5,
            textY + (4 * LineHeight) + 2);
        DrawLine($"Intent: {selected.Intent}", textX, lowerTextY, 0);
        DrawLine($"Target: {targetLabel}", textX, lowerTextY, 1);
        DrawLine($"Position: {x:0.00}, {y:0.00}", textX, lowerTextY, 2);
        DrawLine(
            $"Visual role: {appearance.WeaponLabel}",
            textX,
            lowerTextY,
            3);

        void DrawLine(string text, int xPosition, int yPosition, int row)
        {
            spriteBatch.DrawString(
                font,
                text,
                new Vector2(
                    xPosition,
                    yPosition + (row * LineHeight)),
                Color.White,
                0f,
                Vector2.Zero,
                0.64f,
                SpriteEffects.None,
                0f);
        }
    }

    private static string GetFactionLabel(int factionId) =>
        factionId switch
        {
            0 => "Blue",
            1 => "Red",
            _ => $"Faction {factionId}",
        };

    private static Color GetFactionColor(int factionId) =>
        factionId switch
        {
            0 => BlueColor,
            1 => RedColor,
            _ => OtherFactionColor,
        };
}
