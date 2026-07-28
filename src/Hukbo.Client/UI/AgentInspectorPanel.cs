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
        UiFontSet fonts,
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

        var titleFont = fonts.Get(UiFontRole.Title);
        var bodyFont = fonts.Get(UiFontRole.Body);
        var textX = Bounds.Left + Padding + AccentWidth;
        var textY = Bounds.Top + Padding;
        UiPrimitives.DrawText(
            spriteBatch,
            titleFont,
            "AGENT INSPECTOR",
            new Vector2(textX, textY),
            theme.Colors.TextPrimary);

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
            selected.Loadout.Weapon,
            selected.Loadout.Shield);
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
        // The third PawnRenderer.Draw call site, and the one that deliberately
        // passes no swing pose: a portrait is a still. It compiles unchanged
        // only because the pose parameter is optional.
        //
        // Also deliberately omits contingentId/contingentState: this is a
        // fixed, close-up portrait of one agent, not a battlefield read, so
        // it has no ground plane for an ambient formation tint to sit on and
        // no neighboring pawns for the tint to distinguish it from. Leaving
        // the parameters at their default (0, ContingentState.None) keeps
        // the portrait exactly as it already reads today.
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
        // Built as an ordered list rather than fixed row indices because the
        // grip line is absent for a two-handed weapon, and hard-coded indices
        // would leave a blank row where it would have been.
        var lowerLines = AgentInspectorContent.BuildLowerLines(
            selected,
            appearance.WeaponLabel,
            appearance.EvidenceTierLabel);
        for (var row = 0; row < lowerLines.Count; row++)
        {
            DrawLine(lowerLines[row], textX, lowerTextY, row);
        }

        var contentWidthBudget = AgentInspectorContent.ComputeContentWidthBudget(
            Bounds.Width);
        var evidenceLines = AgentInspectorContent.WrapText(
            appearance.EvidenceNote,
            contentWidthBudget,
            candidate => bodyFont.MeasureString(candidate).X);

        // VIS-012: the weapon-variant lines (resolved tint evidence tier and
        // note, plus any later-or-provisional-form entries the weapon
        // catalogs) follow the same wrapped evidence text immediately below
        // — each raw line word-wrapped exactly like the evidence note above,
        // through the same measure delegate and content-width budget.
        var variantLines = AgentInspectorContent.BuildWeaponVariantLines(
            appearance.WeaponRole,
            appearance.WeaponTintId);
        var wrappedVariantLines = variantLines.SelectMany(
            line => AgentInspectorContent.WrapText(
                line,
                contentWidthBudget,
                candidate => bodyFont.MeasureString(candidate).X));

        // VIS-016: the shield-variant lines (the resolved skin's own label,
        // evidence tier, and note, plus the standalone palisay research
        // note) follow the weapon-variant lines immediately below, wrapped
        // through the same measure delegate and content-width budget.
        var shieldVariantLines = AgentInspectorContent.BuildShieldVariantLines(
            appearance.ShieldRole,
            appearance.ShieldSkinId);
        var wrappedShieldVariantLines = shieldVariantLines.SelectMany(
            line => AgentInspectorContent.WrapText(
                line,
                contentWidthBudget,
                candidate => bodyFont.MeasureString(candidate).X));

        // VIS-024: the appearance-preset lines (the preset's own name, scope
        // tag, evidence tier, per-component tier/note lines, any pending-
        // term flags, and the standalone non-renderable-adornment research
        // note) follow the shield-variant lines immediately below, wrapped
        // through the same measure delegate and content-width budget.
        var appearancePresetLines = AgentInspectorContent.BuildAppearancePresetLines(
            appearance.AppearancePresetId);
        var wrappedAppearancePresetLines = appearancePresetLines.SelectMany(
            line => AgentInspectorContent.WrapText(
                line,
                contentWidthBudget,
                candidate => bodyFont.MeasureString(candidate).X));
        var extraLines = evidenceLines
            .Concat(wrappedVariantLines)
            .Concat(wrappedShieldVariantLines)
            .Concat(wrappedAppearancePresetLines)
            .ToArray();

        var maxRowBottom = Bounds.Bottom - Padding;
        for (var i = 0; i < extraLines.Length; i++)
        {
            var row = lowerLines.Count + i;
            var rowBottom = lowerTextY + (row * LineHeight) + LineHeight;
            if (rowBottom > maxRowBottom)
            {
                break;
            }

            DrawLine(extraLines[i], textX, lowerTextY, row);
        }

        void DrawLine(string text, int xPosition, int yPosition, int row)
        {
            UiPrimitives.DrawText(
                spriteBatch,
                bodyFont,
                text,
                new Vector2(
                    xPosition,
                    yPosition + (row * LineHeight)),
                theme.Colors.TextPrimary);
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
