using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Client.Theming;
using Hukbo.Core.Combat;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client.UI;

internal sealed class AgentInspectorPanel
{
    private const int Padding = AgentInspectorContent.Padding;
    private const int AccentWidth = AgentInspectorContent.AccentWidth;
    private const int PortraitSize = AgentInspectorContent.PortraitSize;
    private const int PortraitGap = AgentInspectorContent.PortraitGap;
    private const int LineHeight = AgentInspectorContent.LineHeight;
    private const float DetailTextScale = 0.64f;

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
        Rectangle bounds,
        UiTheme theme)
    {
        if (agent is not { } selected)
        {
            Bounds = Rectangle.Empty;
            return;
        }

        Bounds = bounds;
        spriteBatch.Draw(pixel, Bounds, theme.Colors.PanelSurface);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            Bounds,
            theme.Colors.PanelBorder,
            theme.Metrics.BorderThickness);

        var textX = Bounds.Left + Padding + AccentWidth;
        var textY = Bounds.Top + Padding;
        spriteBatch.DrawString(
            font,
            "AGENT INSPECTOR",
            new Vector2(textX, textY),
            theme.Colors.TextPrimary,
            0f,
            Vector2.Zero,
            0.78f,
            SpriteEffects.None,
            0f);

        textY += AgentInspectorContent.TitleHeight;
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                Bounds.Left + 2,
                Bounds.Top + 2,
                AccentWidth,
                Math.Max(0, Bounds.Height - 4)),
            GetUiFactionColor(selected.FactionId, theme));

        var appearance = PawnAppearanceFactory.Create(
            selected.EntityId,
            selected.Loadout.Weapon);
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
        spriteBatch.Draw(
            pixel,
            portraitBounds,
            theme.Colors.PanelAlternate);
        UiPrimitives.DrawBorder(
            spriteBatch,
            pixel,
            portraitBounds,
            GetUiFactionColor(selected.FactionId, theme),
            1);
        PawnRenderer.Draw(
            spriteBatch,
            pixel,
            new Vector2(
                portraitBounds.Center.X,
                portraitBounds.Bottom - 7),
            cameraZoom: 1f,
            appearance,
            FactionColorPalette.GetPawnColor(selected.FactionId),
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
            portraitBounds.Bottom + AgentInspectorContent.PortraitBottomGap,
            textY
                + (AgentInspectorContent.TopDetailRowCount * LineHeight)
                + AgentInspectorContent.TopDetailBottomGap);
        DrawLine($"Intent: {selected.Intent}", textX, lowerTextY, 0);
        DrawLine($"Target: {targetLabel}", textX, lowerTextY, 1);
        DrawLine($"Position: {x:0.00}, {y:0.00}", textX, lowerTextY, 2);
        DrawLine(
            AgentInspectorContent.FormatWeaponLine(appearance.WeaponLabel),
            textX,
            lowerTextY,
            3);
        DrawLine(
            AgentInspectorContent.FormatArmorLine(selected.Loadout.Armor),
            textX,
            lowerTextY,
            4);
        DrawLine(
            AgentInspectorContent.FormatShieldLine(selected.Loadout.Shield),
            textX,
            lowerTextY,
            5);

        var contentWidthBudget = AgentInspectorContent.ComputeContentWidthBudget(
            Bounds.Width);
        var evidenceLines = AgentInspectorContent.WrapText(
            appearance.EvidenceNote,
            contentWidthBudget,
            candidate => font.MeasureString(candidate).X * DetailTextScale);
        var maxRowBottom = Bounds.Bottom - Padding;
        for (var i = 0; i < evidenceLines.Count; i++)
        {
            var row = AgentInspectorContent.LowerRowCount + i;
            var rowBottom = lowerTextY + (row * LineHeight) + LineHeight;
            if (rowBottom > maxRowBottom)
            {
                break;
            }

            DrawLine(evidenceLines[i], textX, lowerTextY, row);
        }

        void DrawLine(string text, int xPosition, int yPosition, int row)
        {
            spriteBatch.DrawString(
                font,
                text,
                new Vector2(
                    xPosition,
                    yPosition + (row * LineHeight)),
                theme.Colors.TextPrimary,
                0f,
                Vector2.Zero,
                DetailTextScale,
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

    private static Color GetUiFactionColor(
        int factionId,
        UiTheme theme) =>
        FactionColorPalette.GetThemeColor(
            factionId,
            theme,
            theme.Colors.OtherFaction);
}
