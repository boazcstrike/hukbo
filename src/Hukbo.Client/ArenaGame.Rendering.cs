using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Hukbo.Core.Mathematics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Hukbo.Client;

/// <summary>
/// The render path. Split out of <c>ArenaGame.cs</c> so neither file passes the
/// repository's file-size limit. Presentation only: nothing here decides
/// targeting, damage, retreat, or victory.
/// </summary>
public sealed partial class ArenaGame
{
    private const string ShortcutHintLine =
        "Click: select/inspect  |  Event arrows: navigate  |  " +
        "Wheel: zoom/log scroll  |  " +
        "Space: play/pause  |  1/2/4: speed  |  " +
        "R: next round  |  Shift+R: full reset  |  " +
        "F9: sound log  |  Esc: menu";

    protected override void Draw(GameTime gameTime)
    {
        var theme = _themeManager.ActiveTheme;
        GraphicsDevice.Clear(theme.Colors.CanvasBackground);

        if (_spriteBatch is null ||
            _arenaRasterizerState is null ||
            _pixel is null ||
            _fonts is null)
        {
            return;
        }

        var screenBounds = GraphicsDevice.Viewport.Bounds;
        var layout = GetLayout(screenBounds);
        _camera.Fit(layout.ArenaBounds);
        UpdateHoverSelection(layout.ArenaBounds);

        DrawArenaLayer(
            _spriteBatch,
            _pixel,
            _arenaRasterizerState,
            layout.ArenaBounds,
            theme);
        DrawUiLayer(
            _spriteBatch,
            _pixel,
            _fonts,
            screenBounds,
            layout,
            theme);

        base.Draw(gameTime);
    }

    private void DrawArenaLayer(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        RasterizerState rasterizerState,
        Rectangle arenaBounds,
        UiTheme theme)
    {
        if (arenaBounds.Width <= 0 || arenaBounds.Height <= 0)
        {
            return;
        }

        GraphicsDevice.ScissorRectangle = arenaBounds;
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            rasterizerState);
        DrawArena(spriteBatch, pixel, arenaBounds, theme);
        spriteBatch.End();
    }

    private void DrawUiLayer(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        Rectangle screenBounds,
        ClientLayout layout,
        UiTheme theme)
    {
        var selectedAgent =
            _presentation.Selection.Resolve(_simulation.Agents);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp);
        DrawStatus(spriteBatch, pixel, fonts, screenBounds, theme);
        _controlBar.Draw(
            spriteBatch,
            pixel,
            fonts,
            screenBounds,
            _presentation.Playback.IsPlaying,
            _isSoundLogVisible,
            theme);
        _inspectorPanel.Draw(
            spriteBatch,
            pixel,
            fonts,
            selectedAgent,
            layout.InspectorBounds,
            theme);
        _eventLogPanel.Draw(
            spriteBatch,
            pixel,
            fonts,
            _presentation.EventFeed,
            layout.EventBounds,
            theme);
        if (_isSoundLogVisible)
        {
            _soundLogPanel.Draw(
                spriteBatch,
                pixel,
                fonts,
                _soundDirector,
                layout.SoundLogBounds,
                theme);
        }

        _summaryPanel.Draw(
            spriteBatch,
            pixel,
            fonts,
            _presentation.Summary,
            layout.ArenaBounds,
            theme);
        _menu.Draw(
            spriteBatch,
            pixel,
            fonts,
            screenBounds,
            theme,
            _goreManager.Value);
        if (_isArmyCompositionPanelVisible)
        {
            _armyCompositionPanel.Draw(
                spriteBatch,
                pixel,
                fonts,
                screenBounds,
                theme);
        }

        spriteBatch.End();
    }

    private void DrawArena(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle arenaBounds,
        UiTheme theme)
    {
        DrawMapSurface(spriteBatch, pixel, arenaBounds, theme);
        BloodRenderer.DrawGroundMarks(
            _presentation.Blood.ActiveGroundMarks,
            _camera,
            arenaBounds,
            _camera.Zoom,
            spriteBatch,
            pixel);
        DrawPawns(spriteBatch, pixel, arenaBounds);
        BloodRenderer.DrawBursts(
            _presentation.Blood.ActiveBursts,
            _presentation.Blood.ActiveSpurts,
            _camera,
            arenaBounds,
            _camera.Zoom,
            spriteBatch,
            pixel);

        HitEffectRenderer.Draw(
            _presentation.HitEffects.ActiveEffects,
            _camera,
            arenaBounds,
            _camera.Zoom,
            spriteBatch,
            pixel);
        ClashEffectRenderer.Draw(
            _presentation.ClashEffects.ActiveEffects,
            _camera,
            arenaBounds,
            _camera.Zoom,
            spriteBatch,
            pixel);
    }

    private void DrawMapSurface(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle arenaBounds,
        UiTheme theme)
    {
        var topLeft = _camera.WorldToScreen(Vector2.Zero, arenaBounds);
        var bottomRight = _camera.WorldToScreen(
            new Vector2(_scenario.MapWidth, _scenario.MapHeight),
            arenaBounds);
        var mapBounds = RectangleFromPoints(topLeft, bottomRight);
        var visibleMapBounds = Rectangle.Intersect(mapBounds, arenaBounds);

        if (visibleMapBounds.Width <= 0 || visibleMapBounds.Height <= 0)
        {
            return;
        }

        var backdropFrame = new PlainsBackdropFrame(
            mapBounds,
            arenaBounds,
            _scenario.MapWidth,
            _scenario.MapHeight,
            _scenario.Seed);
        PlainsBackdropRenderer.Draw(
            spriteBatch,
            pixel,
            backdropFrame,
            _plainsDecals,
            _camera,
            theme);
        DrawBorder(
            spriteBatch,
            pixel,
            visibleMapBounds,
            theme.Colors.ArenaBorder,
            theme.Metrics.BorderThickness);
    }

    private void DrawPawns(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle arenaBounds)
    {
        var selectedEntityId = _presentation.Selection.SelectedEntityId;
        var hoveredEntityId = _hoverSelection.SelectedEntityId;

        foreach (var agent in _simulation.Agents)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            var worldPosition = new Vector2(
                agent.XRaw / (float)FixedPoint.Scale,
                agent.YRaw / (float)FixedPoint.Scale);
            var footAnchor = _camera.WorldToScreen(
                worldPosition,
                arenaBounds);
            var appearance = PawnAppearanceFactory.Create(
                agent.EntityId,
                agent.Loadout.Weapon,
                agent.Loadout.Shield);

            // Pose-blind on purpose. A pose-aware cull would make the set of
            // drawn pawns a function of presentation animation phase, so the
            // same tick would render a different draw list depending on where
            // each swing clock sat. See PawnRenderer.GetBounds.
            var visualBounds = PawnRenderer.GetBounds(
                footAnchor,
                _camera.Zoom,
                appearance);

            if (!arenaBounds.Intersects(visualBounds))
            {
                continue;
            }

            PawnRenderer.Draw(
                spriteBatch,
                pixel,
                footAnchor,
                _camera.Zoom,
                appearance,
                FactionColorPalette.GetPawnColor(agent.FactionId),
                GetPawnVisualState(
                    agent.EntityId,
                    selectedEntityId,
                    hoveredEntityId),
                hitPulseStrength:
                    _presentation.HitEffects.GetPulseStrength(
                        agent.EntityId),
                swingPose: SwingPoseResolver.TryGetPose(
                    _swingPoses,
                    agent.EntityId,
                    out var swingPose)
                    ? swingPose
                    : null);
        }
    }

    private static PawnVisualState GetPawnVisualState(
        ulong entityId,
        ulong? selectedEntityId,
        ulong? hoveredEntityId) =>
        entityId == selectedEntityId
            ? PawnVisualState.Selected
            : entityId == hoveredEntityId
                ? PawnVisualState.Hovered
                : PawnVisualState.Normal;

    private void DrawStatus(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        UiFontSet fonts,
        Rectangle screenBounds,
        UiTheme theme)
    {
        var statusBounds = new Rectangle(
            screenBounds.Left,
            screenBounds.Top,
            screenBounds.Width,
            Math.Min(StatusBarHeight, screenBounds.Height));
        spriteBatch.Draw(pixel, statusBounds, theme.Colors.StatusSurface);

        UiPrimitives.DrawText(
            spriteBatch,
            fonts.Get(UiFontRole.Label),
            BuildStatusLine(),
            new Vector2(18, 12),
            theme.Colors.TextPrimary);
        UiPrimitives.DrawText(
            spriteBatch,
            fonts.Get(UiFontRole.Body),
            ShortcutHintLine,
            new Vector2(18, 39),
            theme.Colors.TextSecondary);
    }

    private string BuildStatusLine()
    {
        var factionZeroAlive = 0;
        var factionOneAlive = 0;

        foreach (var agent in _simulation.Agents)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            if (agent.FactionId == 0)
            {
                factionZeroAlive++;
            }
            else if (agent.FactionId == 1)
            {
                factionOneAlive++;
            }
        }

        var state = _presentation.Playback.IsPlaying ? "PLAYING" : "PAUSED";
        var stagedSuffix = _isCompositionStaged
            ? $"  |  {CompositionStagedNotice}"
            : string.Empty;
        return
            $"{state}  |  Tick {_simulation.Tick:N0}  |  {_speedMultiplier}x  |  " +
            $"Team A (Blue) {_matchSeries.TeamAWins}W/{factionZeroAlive} alive  |  " +
            $"Team B (Red) {_matchSeries.TeamBWins}W/{factionOneAlive} alive  |  " +
            $"{_simulation.Outcome}" +
            stagedSuffix;
    }

    private static Rectangle RectangleFromPoints(
        Vector2 first,
        Vector2 second)
    {
        var left = (int)MathF.Floor(MathF.Min(first.X, second.X));
        var top = (int)MathF.Floor(MathF.Min(first.Y, second.Y));
        var right = (int)MathF.Ceiling(MathF.Max(first.X, second.X));
        var bottom = (int)MathF.Ceiling(MathF.Max(first.Y, second.Y));
        return new Rectangle(left, top, right - left, bottom - top);
    }

    private static void DrawBorder(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color color,
        int thickness)
    {
        spriteBatch.Draw(
            pixel,
            new Rectangle(bounds.Left, bounds.Top, bounds.Width, thickness),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Left,
                bounds.Bottom - thickness,
                bounds.Width,
                thickness),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(bounds.Left, bounds.Top, thickness, bounds.Height),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                bounds.Right - thickness,
                bounds.Top,
                thickness,
                bounds.Height),
            color);
    }
}
