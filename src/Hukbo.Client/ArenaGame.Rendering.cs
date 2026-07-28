using System.Diagnostics;
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
        if (_renderProbeEnabled)
        {
            _renderProbeFrameStartTimestamp = Stopwatch.GetTimestamp();
            _renderMetricsRecorder.Reset();
        }

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

        if (_renderProbeEnabled)
        {
            // Tier 1 quads/triangles: evaluated over the exact same
            // appearance/layout/cull inputs DrawArenaLayer's own draw calls
            // resolve this frame, via the pure counting functions the
            // design's quad budgets are pinned against — never inside a
            // renderer's own per-frame path (VIS-034/VIS-035R). Timed
            // separately from arena submission itself, matching
            // RenderMetricsSnapshot's own Tier 1 field split.
            var geometryBuildStartTimestamp = Stopwatch.GetTimestamp();
            RecordArenaRenderMetrics(layout.ArenaBounds);
            _renderMetricsRecorder.AddGeometryBuildMicroseconds(
                Stopwatch.GetElapsedTime(geometryBuildStartTimestamp)
                    .TotalMicroseconds);
        }

        var arenaSubmitStartTimestamp =
            _renderProbeEnabled ? Stopwatch.GetTimestamp() : 0L;

        DrawArenaLayer(
            _spriteBatch,
            _pixel,
            _arenaRasterizerState,
            layout.ArenaBounds,
            theme);

        if (_renderProbeEnabled)
        {
            _renderMetricsRecorder.AddSubmitMicroseconds(
                Stopwatch.GetElapsedTime(arenaSubmitStartTimestamp)
                    .TotalMicroseconds);
        }

        DrawUiLayer(
            _spriteBatch,
            _pixel,
            _fonts,
            screenBounds,
            layout,
            theme);

        base.Draw(gameTime);

        if (_renderProbeEnabled)
        {
            var elapsed = Stopwatch.GetElapsedTime(_renderProbeFrameStartTimestamp);
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            var frameAllocatedBytes = Math.Max(
                0L,
                allocatedBytes - _renderProbePreviousAllocatedBytes);
            _renderProbePreviousAllocatedBytes = allocatedBytes;
            _renderMetricsRecorder.SetManagedBytesAllocated(frameAllocatedBytes);

            RenderProbeSampled?.Invoke(new RenderProbeSample(
                elapsed.TotalMilliseconds,
                _renderMetricsRecorder.Snapshot(),
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.CollectionCount(2),
                allocatedBytes));
        }
    }

    /// <summary>
    /// Records this frame's arena-batch Tier 1 quad/triangle counts (and
    /// their backend-derived Tier 2 diagnostic submissions) into
    /// <see cref="_renderMetricsRecorder"/>. Only called from the
    /// render-probe opt-in branch of <see cref="Draw"/>, so a normal run
    /// never evaluates this. Mirrors <see cref="DrawPawns"/> and
    /// <see cref="DrawArena"/>'s own draw-order and cull decisions
    /// element-for-element rather than reusing worst-case estimates, except
    /// for the ground grid, decals, and trample marks, which — like
    /// <c>BackdropQuadCount</c>'s own documented semantics — report the
    /// full unculled count rather than replicating
    /// <c>PlainsBackdropRenderer</c>'s per-cell/per-decal arena-bounds
    /// intersection test, an upper bound rather than an exact live figure
    /// for those three categories only.
    /// </summary>
    private void RecordArenaRenderMetrics(Rectangle arenaBounds)
    {
        RecordPawnQuads(arenaBounds);
        RecordBackdropQuads(arenaBounds);

        // One arena Begin/End pair, one shared 1x1 pixel texture — the
        // current backend's own invariant (R-W4.5, demoted to a Tier 2
        // diagnostic assertion scoped to this backend by amendment A-1).
        _renderMetricsRecorder.AddBatch();
        _renderMetricsRecorder.AddTextureBind();
    }

    /// <summary>
    /// Recomputes each visible pawn's <see cref="PawnLayout"/> with the same
    /// inputs <see cref="DrawPawns"/> resolves — footAnchor, camera zoom,
    /// appearance, swing pose, and every other <c>PawnGeometry.Create</c>
    /// parameter left at <see cref="DrawPawns"/>'s own implicit defaults —
    /// so <c>PawnQuadCount.Count</c>'s result matches what
    /// <c>PawnRenderer.Draw</c> actually emits for that pawn this frame.
    /// </summary>
    private void RecordPawnQuads(Rectangle arenaBounds)
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
            var visualBounds = PawnRenderer.GetBounds(
                footAnchor,
                _camera.Zoom,
                appearance);

            if (!arenaBounds.Intersects(visualBounds))
            {
                continue;
            }

            var swingPose = SwingPoseResolver.TryGetPose(
                _swingPoses,
                agent.EntityId,
                out var pose)
                ? pose
                : (SwingPose?)null;
            var layout = PawnGeometry.Create(
                footAnchor,
                _camera.Zoom,
                appearance,
                swingPose: swingPose);
            var state = GetPawnVisualState(
                agent.EntityId,
                selectedEntityId,
                hoveredEntityId);

            RecordQuads(PawnQuadCount.Count(layout, appearance, state));
        }
    }

    /// <summary>
    /// Records the battlefield backdrop's Tier 1 quads: the ground grid and
    /// scatter decals at their full (unculled) counts, the live trample
    /// marks at their full count, the grass clusters at the camera's current
    /// zoom band, and the live dust puffs at the camera's current zoom — the
    /// same fields <see cref="DrawMapSurface"/> and <see cref="DrawGrass"/>
    /// already read this frame.
    /// </summary>
    private void RecordBackdropQuads(Rectangle arenaBounds)
    {
        var mapBounds = GetMapBounds(arenaBounds);
        if (mapBounds.Width > 0 && mapBounds.Height > 0)
        {
            var (columns, rows) = PlainsBackdropGeometry.GetGridDimensions(
                _scenario.MapWidth,
                _scenario.MapHeight);
            RecordQuads(BackdropQuadCount.GroundGrid(columns, rows));
        }

        RecordQuads(BackdropQuadCount.Decals(_plainsDecals.Length));
        RecordQuads(
            BackdropQuadCount.TrampleMarks(_presentation.Trample.ActiveMarks.Length));

        var zoomBand = GrassGeometry.GetZoomBand(_camera.Zoom);
        RecordQuads(BackdropQuadCount.GrassClusters(_grassClusters, zoomBand));

        RecordQuads(
            BackdropQuadCount.DustPuffs(_presentation.Dust.ActivePuffs, _camera.Zoom));
    }

    /// <summary>
    /// Records <paramref name="quadCount"/> Tier 1 quads into
    /// <see cref="_renderMetricsRecorder"/>, plus the Tier 2 diagnostic
    /// counters that follow from it as a fact of today's backend: every draw
    /// call <c>PawnRenderer</c>/<c>PlainsBackdropRenderer</c>/
    /// <c>GrassRenderer</c>/<c>DustRenderer</c> issues is exactly one
    /// <c>SpriteBatch.Draw</c> submission rendering exactly one quad — two
    /// triangles — per <c>PawnQuadCount</c>'s own remarks.
    /// </summary>
    private void RecordQuads(int quadCount)
    {
        if (quadCount <= 0)
        {
            return;
        }

        _renderMetricsRecorder.AddQuads(quadCount);
        _renderMetricsRecorder.AddTriangles(quadCount * 2);
        for (var index = 0; index < quadCount; index++)
        {
            _renderMetricsRecorder.AddSubmission();
        }
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
        if (_isBattleReportVisible && _presentation.Report is not null)
        {
            _battleReportPanel.Draw(
                spriteBatch,
                pixel,
                fonts,
                _presentation.Report,
                layout.ArenaBounds,
                theme);
        }

        _menu.Draw(
            spriteBatch,
            pixel,
            fonts,
            screenBounds,
            theme,
            _goreManager.Value,
            _motionManager.Value);
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
        DrawGrass(spriteBatch, pixel, arenaBounds, theme);
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
        DustRenderer.Draw(
            _presentation.Dust.ActivePuffs,
            _camera,
            arenaBounds,
            _camera.Zoom,
            spriteBatch,
            pixel,
            theme);
    }

    private void DrawMapSurface(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle arenaBounds,
        UiTheme theme)
    {
        var mapBounds = GetMapBounds(arenaBounds);
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

    /// <summary>
    /// Draws grass clusters between the ground grid and pawns
    /// (battlefield-environment-design.md, "Grass clusters"). Recomputes the
    /// projected map rectangle rather than threading it out of
    /// <see cref="DrawMapSurface"/>: both calls are two allocation-free
    /// <c>SpectatorCamera.WorldToScreen</c> lookups, cheaper than adding a
    /// return value or a field to carry one frame's rectangle across calls.
    /// </summary>
    private void DrawGrass(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle arenaBounds,
        UiTheme theme)
    {
        var mapBounds = GetMapBounds(arenaBounds);
        GrassRenderer.Draw(
            spriteBatch,
            pixel,
            _grassClusters,
            _presentation.Trample.ActiveMarks,
            _camera,
            mapBounds,
            arenaBounds,
            theme,
            _motionManager.Value,
            _presentation.GrassSwayClockSeconds);
    }

    /// <summary>
    /// The projected map rectangle in screen space for the current camera
    /// state, shared by <see cref="DrawMapSurface"/> and
    /// <see cref="DrawGrass"/> so the two draw calls agree on exactly the
    /// same rectangle every frame.
    /// </summary>
    private Rectangle GetMapBounds(Rectangle arenaBounds)
    {
        var topLeft = _camera.WorldToScreen(Vector2.Zero, arenaBounds);
        var bottomRight = _camera.WorldToScreen(
            new Vector2(_scenario.MapWidth, _scenario.MapHeight),
            arenaBounds);
        return RectangleFromPoints(topLeft, bottomRight);
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
