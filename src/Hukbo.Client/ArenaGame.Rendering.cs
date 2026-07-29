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

    /// <summary>
    /// GPU-004. Converts a raw <see cref="Stopwatch"/> tick count to
    /// microseconds. The arena spans accumulate raw ticks across a frame and
    /// convert once, so a per-pawn boundary costs one timestamp read and one
    /// integer subtraction rather than a <c>TimeSpan</c> construction and a
    /// recorder call.
    /// </summary>
    private static readonly double MicrosecondsPerStopwatchTick =
        1_000_000.0 / Stopwatch.Frequency;

    /// <summary>
    /// GPU-004. The instant the current arena span opened, moved forward by
    /// every boundary crossing inside <see cref="DrawArenaLayer"/>. Meaningful
    /// only while the render probe is enabled and only for the duration of one
    /// <see cref="DrawArenaLayer"/> call.
    /// </summary>
    private long _arenaSpanBoundaryTimestamp;

    /// <summary>
    /// GPU-004. Ticks charged this frame to real per-pawn geometry
    /// construction: the appearance resolution, the pose-blind bounds, the
    /// cull test, and the posed layout the renderer actually draws from.
    /// </summary>
    private long _arenaGeometryTicks;

    /// <summary>
    /// GPU-004. Ticks charged this frame to arena submission: every
    /// <c>SpriteBatch.Draw</c> call and the batch's own
    /// <c>Begin</c>/<c>End</c>, with the per-pawn geometry above fenced out.
    /// </summary>
    private long _arenaSubmitTicks;

    /// <summary>
    /// GPU-005. Pawn-layout constructions made by the probe's own duplicate
    /// counting pass this frame — the call site inside
    /// <see cref="RecordPawnQuads"/>. Written once per frame by that method
    /// rather than incremented per agent. Meaningful only while the render
    /// probe is enabled, because nothing else ever runs that pass.
    /// </summary>
    private int _frameProbePassPawnGeometryInvocations;

    /// <summary>
    /// GPU-005. Pawn-layout constructions made by the renderer's own draw path
    /// this frame — the call site inside <see cref="DrawPawns"/>, which a
    /// normal unprobed run also reaches. Written once per frame rather than
    /// incremented per agent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GPU-014 changed what one counted construction is worth without changing
    /// how many are counted for a culled agent. Before it, both counts were
    /// literal <c>PawnGeometry.Create</c> calls: one per living agent for the
    /// cull rectangle, plus one more for each agent that survived the cull.
    /// Since it, a living agent begins exactly one construction, stopping
    /// after the pose-invariant stage if the cull rejects it and finishing the
    /// same construction if it does not. So a visible pawn's count halves from
    /// two to one alongside its work, while a culled agent's count stays at
    /// one although its work shrank to the cheap stage. The count therefore
    /// understates the saving rather than overstating it, which is the
    /// conservative direction for a go/no-go figure.
    /// </para>
    /// <para>
    /// One further <c>PawnGeometry.Create</c> call exists outside this file,
    /// the inspector portrait's, reached through <c>AgentInspectorPanel</c>'s
    /// <c>PawnRenderer.Draw</c> call, and it is deliberately not counted by
    /// either field. It is a single still rather than per-agent work, it runs
    /// only while an agent is selected, and it is already timed inside
    /// <c>uiLayerMicroseconds</c> rather than inside either arena span. The
    /// duplication factor these counts feed is a statement about the arena
    /// render path, so folding a user-interface still into it would misstate
    /// that path.
    /// </para>
    /// </remarks>
    private int _frameDrawPathPawnGeometryInvocations;

    /// <summary>
    /// GPU-005. Run total of <see cref="_frameProbePassPawnGeometryInvocations"/>
    /// across every probe-enabled frame, accumulated rather than sampled
    /// because <see cref="ProbePawnGeometryDuplicationFactor"/> describes the
    /// run as a whole rather than any one frame.
    /// </summary>
    private long _probePassPawnGeometryInvocations;

    /// <summary>
    /// GPU-005. Run total of <see cref="_frameDrawPathPawnGeometryInvocations"/>
    /// across every probe-enabled frame: the invocations a normal, unprobed
    /// run would also have made, and the denominator of
    /// <see cref="ProbePawnGeometryDuplicationFactor"/>.
    /// </summary>
    private long _drawPathPawnGeometryInvocations;

    /// <summary>
    /// GPU-005. How many times this run evaluated the pure pawn-geometry
    /// helper for each one time the draw path alone needed it: the ratio of
    /// every counted invocation to the draw path's own. Derived from the two
    /// recorded run totals rather than asserted, so removing a duplicate
    /// construction moves it instead of leaving a stale constant behind. A
    /// value near 2 means the probe's counting pass rebuilt every living
    /// agent's geometry a second time; a value of 1 would mean the probe added
    /// no duplicate work at all. Zero when the draw path recorded nothing,
    /// which is the honest reading of "this run measured no duplication" and
    /// is what an unprobed run reports.
    /// </summary>
    public double ProbePawnGeometryDuplicationFactor =>
        _drawPathPawnGeometryInvocations == 0L
            ? 0d
            : (double)(_probePassPawnGeometryInvocations +
                _drawPathPawnGeometryInvocations) /
                _drawPathPawnGeometryInvocations;

    /// <summary>
    /// GPU-004. Closes the open submission span and opens a geometry span.
    /// Probe-only: a normal run reads no timestamp here, exactly as the
    /// surrounding <c>_renderProbeEnabled</c> guards in <see cref="Draw"/>
    /// intend.
    /// </summary>
    private void OpenArenaGeometrySpan()
    {
        if (!_renderProbeEnabled)
        {
            return;
        }

        var boundary = Stopwatch.GetTimestamp();
        _arenaSubmitTicks += boundary - _arenaSpanBoundaryTimestamp;
        _arenaSpanBoundaryTimestamp = boundary;
    }

    /// <summary>
    /// GPU-004. Closes the open geometry span and reopens the submission
    /// span. The mirror of <see cref="OpenArenaGeometrySpan"/>: between them
    /// the two accumulators partition the whole
    /// <see cref="DrawArenaLayer"/> call.
    /// </summary>
    private void CloseArenaGeometrySpan()
    {
        if (!_renderProbeEnabled)
        {
            return;
        }

        var boundary = Stopwatch.GetTimestamp();
        _arenaGeometryTicks += boundary - _arenaSpanBoundaryTimestamp;
        _arenaSpanBoundaryTimestamp = boundary;
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_renderProbeEnabled)
        {
            _renderProbeFrameStartTimestamp = Stopwatch.GetTimestamp();
            _renderMetricsRecorder.Reset();

            // GPU-005. Cleared rather than left to be overwritten, because
            // DrawArenaLayer returns without reaching DrawPawns whenever the
            // arena rectangle is degenerate. Without this, such a frame would
            // report the previous frame's draw-path count as its own.
            _frameProbePassPawnGeometryInvocations = 0;
            _frameDrawPathPawnGeometryInvocations = 0;
        }

        var theme = _themeManager.ActiveTheme;

        var clearStartTimestamp =
            _renderProbeEnabled ? Stopwatch.GetTimestamp() : 0L;

        GraphicsDevice.Clear(theme.Colors.CanvasBackground);

        if (_renderProbeEnabled)
        {
            _renderMetricsRecorder.AddClearMicroseconds(
                Stopwatch.GetElapsedTime(clearStartTimestamp)
                    .TotalMicroseconds);
        }

        if (_spriteBatch is null ||
            _arenaRasterizerState is null ||
            _pixel is null ||
            _fonts is null)
        {
            return;
        }

        var screenBounds = GraphicsDevice.Viewport.Bounds;

        var layoutStartTimestamp =
            _renderProbeEnabled ? Stopwatch.GetTimestamp() : 0L;

        var layout = GetLayout(screenBounds);
        _camera.Fit(layout.ArenaBounds);

        if (_renderProbeEnabled)
        {
            _renderMetricsRecorder.AddLayoutMicroseconds(
                Stopwatch.GetElapsedTime(layoutStartTimestamp)
                    .TotalMicroseconds);
        }

        var hoverSelectionStartTimestamp =
            _renderProbeEnabled ? Stopwatch.GetTimestamp() : 0L;

        UpdateHoverSelection(layout.ArenaBounds);

        if (_renderProbeEnabled)
        {
            _renderMetricsRecorder.AddHoverSelectionMicroseconds(
                Stopwatch.GetElapsedTime(hoverSelectionStartTimestamp)
                    .TotalMicroseconds);
        }

        if (_renderProbeEnabled)
        {
            // Tier 1 quads/triangles: evaluated over the exact same
            // appearance/layout/cull inputs DrawArenaLayer's own draw calls
            // resolve this frame, via the pure counting functions the
            // design's quad budgets are pinned against — never inside a
            // renderer's own per-frame path (VIS-034/VIS-035R).
            //
            // GPU-005. This pass belongs to the probe and to nothing else: a
            // normal run never evaluates it, and under the probe it walks the
            // agent list a second time to rebuild appearance, bounds, and
            // layout that DrawPawns rebuilds again moments later. Its cost is
            // therefore reported as probeOverheadMicroseconds rather than as
            // geometryBuildMicroseconds, which is the field it used to be
            // written to and the reason every figure recorded against that
            // field before GPU-005 was the instrument timing itself. The
            // renderer's real per-pawn geometry cost is
            // arenaGeometryMicroseconds (GPU-004). Nothing writes
            // geometryBuildMicroseconds now, so its zero is that absence
            // rather than a measured zero, and the two spans a Phase 3
            // go/no-go trigger divides by no longer include the instrument.
            //
            // Neither boundary read is charged to the span it bounds: the
            // span closes on the timestamp GetElapsedTime takes on entry, so
            // the microsecond conversion and the recorder call below both
            // land after it.
            var probeOverheadStartTimestamp = Stopwatch.GetTimestamp();
            RecordArenaRenderMetrics(layout.ArenaBounds);
            _renderMetricsRecorder.AddProbeOverheadMicroseconds(
                Stopwatch.GetElapsedTime(probeOverheadStartTimestamp)
                    .TotalMicroseconds);
        }

        if (_renderProbeEnabled)
        {
            // GPU-004. The arena layer is measured as two disjoint spans
            // rather than one. A rolling boundary timestamp walks the whole
            // DrawArenaLayer call, and every tick between two boundaries is
            // charged to exactly one of the two accumulators below, so the
            // spans can neither overlap nor leave a gap and their sum is the
            // single figure this call used to report on its own.
            _arenaGeometryTicks = 0L;
            _arenaSubmitTicks = 0L;
            _arenaSpanBoundaryTimestamp = Stopwatch.GetTimestamp();
        }

        DrawArenaLayer(
            _spriteBatch,
            _pixel,
            _arenaRasterizerState,
            layout.ArenaBounds,
            theme);

        if (_renderProbeEnabled)
        {
            _arenaSubmitTicks +=
                Stopwatch.GetTimestamp() - _arenaSpanBoundaryTimestamp;
            _renderMetricsRecorder.AddArenaGeometryMicroseconds(
                _arenaGeometryTicks * MicrosecondsPerStopwatchTick);
            _renderMetricsRecorder.AddSubmitMicroseconds(
                _arenaSubmitTicks * MicrosecondsPerStopwatchTick);

            // GPU-005. Every span that could be inflated by this work has
            // already closed — the submission span on the timestamp read at
            // the top of this block, the probe-overhead span further up — so
            // the accumulation and the recorder call here are charged to
            // nothing they describe. Only the two per-frame field writes that
            // feed them sit inside a measured span, and each is a single
            // integer store rather than a per-agent one.
            _probePassPawnGeometryInvocations +=
                _frameProbePassPawnGeometryInvocations;
            _drawPathPawnGeometryInvocations +=
                _frameDrawPathPawnGeometryInvocations;
            _renderMetricsRecorder.AddPawnGeometryInvocations(
                _frameProbePassPawnGeometryInvocations +
                _frameDrawPathPawnGeometryInvocations);
        }

        var uiLayerStartTimestamp =
            _renderProbeEnabled ? Stopwatch.GetTimestamp() : 0L;

        DrawUiLayer(
            _spriteBatch,
            _pixel,
            _fonts,
            screenBounds,
            layout,
            theme);

        if (_renderProbeEnabled)
        {
            _renderMetricsRecorder.AddUiLayerMicroseconds(
                Stopwatch.GetElapsedTime(uiLayerStartTimestamp)
                    .TotalMicroseconds);
        }

        var baseDrawStartTimestamp =
            _renderProbeEnabled ? Stopwatch.GetTimestamp() : 0L;

        base.Draw(gameTime);

        if (_renderProbeEnabled)
        {
            _renderMetricsRecorder.AddBaseDrawMicroseconds(
                Stopwatch.GetElapsedTime(baseDrawStartTimestamp)
                    .TotalMicroseconds);
        }

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
    /// appearance, swing pose, and every other pawn-geometry parameter left at
    /// <see cref="DrawPawns"/>'s own implicit defaults — so
    /// <c>PawnQuadCount.Count</c>'s result matches what
    /// <c>PawnRenderer.Draw</c> actually emits for that pawn this frame.
    /// Mirrors <see cref="DrawPawns"/>'s two-stage geometry path element for
    /// element, so the two passes cull the same agents.
    /// </summary>
    private void RecordPawnQuads(Rectangle arenaBounds)
    {
        var selectedEntityId = _presentation.Selection.SelectedEntityId;
        var hoveredEntityId = _hoverSelection.SelectedEntityId;

        // GPU-005. Counted in a local and stored once below rather than
        // incremented through a field per agent. This method is probe-only, so
        // no gate is needed here and the counting is charged to probe
        // overhead, which is exactly where it belongs.
        var pawnGeometryInvocations = 0;

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

            // GPU-018, deliberately not routed through the appearance cache
            // that DrawPawns now reads. Two reasons, and the first is the one
            // that matters. Draw calls RecordArenaRenderMetrics, and so this
            // pass, before it calls DrawArenaLayer and so DrawPawns, so if this
            // read the cache it would warm every slot before the render path
            // ever looked, and appearanceCacheHits/Misses/Fills would describe the
            // probe rather than the renderer — the very frame the criterion
            // asks about, a battle's first, would report a full set of hits on
            // the draw path and no visible cold start at all. Keeping this call
            // on the factory leaves those three counters meaning exactly one
            // read per living agent per frame, taken by the render path alone.
            // Second, GPU-005 put this pass's cost in probeOverheadMicroseconds,
            // and letting it consume a cache paid for by the renderer would
            // move cost across that boundary in the wrong direction. The value
            // is identical either way, so the two passes still cull the same
            // agents and count the same quads.
            var appearance = PawnAppearanceFactory.Create(
                agent.EntityId,
                agent.Loadout.Weapon,
                agent.Loadout.Shield);
            var pawnPrefix = PawnGeometry.PoseBlindPrefix.Create(
                footAnchor,
                _camera.Zoom,
                appearance);
            pawnGeometryInvocations++;

            if (!arenaBounds.Intersects(pawnPrefix.PoseBlindVisualBounds))
            {
                continue;
            }

            var swingPose = SwingPoseResolver.TryGetPose(
                _swingPoses,
                agent.EntityId,
                out var pose)
                ? pose
                : (SwingPose?)null;
            var layout = pawnPrefix.CompletePosedLayout(swingPose);
            var state = GetPawnVisualState(
                agent.EntityId,
                selectedEntityId,
                hoveredEntityId);

            RecordQuads(PawnQuadCount.Count(layout, appearance, state));
        }

        // GPU-005, as amended by GPU-014. One pawn-layout construction begun
        // per living agent, and no second one for the agents that survived the
        // cull: since GPU-014 those finish the construction stage one started
        // rather than starting another. Counted at the call site rather than
        // inside the helper so that this file's own per-agent work stays
        // visible in the file that causes it.
        _frameProbePassPawnGeometryInvocations = pawnGeometryInvocations;
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
            _motionManager.Value,
            _autoCameraManager.Value);

        // Last, and above the menu: the prompt is modal, so nothing may paint
        // over it. It scrims the whole area itself, which is what makes the
        // modality visible rather than only enforced in the input chain.
        _quitPrompt.Draw(
            spriteBatch,
            pixel,
            fonts,
            layout.ArenaBounds,
            theme);
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

        // GPU-004. Everything in this loop other than the PawnRenderer call
        // itself is per-agent CPU work: walking the agent list, projecting to
        // screen space, resolving appearance, building the pose-blind bounds,
        // testing the cull, and building the layout the renderer draws from.
        // So the geometry span opens once for the whole loop and closes only
        // around each drawn pawn's submission, rather than opening and closing
        // per agent. That choice is what keeps the instrumentation affordable
        // at a thousand units — see the boundary-count note on
        // OpenArenaGeometrySpan's callers below — and it errs toward charging
        // ambiguous work to geometry rather than to submission, which is the
        // conservative direction for the Phase 3 go/no-go trigger.
        OpenArenaGeometrySpan();

        // GPU-005. The renderer's own pawn-geometry invocation count for this
        // frame. A local, incremented only inside the _renderProbeEnabled
        // gates below, so a normal run executes a predicted-not-taken branch
        // per agent and performs no increment and no store at all. Stored to
        // its field once after the loop rather than per agent.
        var pawnGeometryInvocations = 0;

        // GPU-018. Indexed rather than enumerated, because the appearance cache
        // addresses its slots by the agent's ordinal position in this roster
        // and the ordinal has to be the position in the whole list, not a count
        // of the living. BattleSimulation.Agents is a ReadOnlyCollection over
        // an AgentView[] sized once at scenario creation
        // (BattleSimulation.cs lines 77 to 78) and refilled element for element
        // by UpdateViews (line 1866), so index i names the same warrior for the
        // whole battle: death clears IsAlive in place and never removes,
        // compacts, or reorders an entry. A loop counter that skipped the dead
        // would shift every later agent's ordinal the moment somebody fell,
        // which the stored-key check would catch as a miss rather than as a
        // wrong appearance — correct, but with the hit rate destroyed and this
        // task pointless.
        var agents = _simulation.Agents;

        // GPU-020. One pass over the at-most-256 live effects for the whole
        // frame, in place of the full scan of that same buffer that ran once
        // per drawn pawn below. The values are identical: HitEffectSystemTests
        // pins the lookup against GetPulseStrength, which stays in place as the
        // reference, over both edge behaviours — lethal exclusion and
        // maximum-over-effects — and over a full 256-effect buffer.
        //
        // This is a snapshot, not a cache. It is a ref struct so the compiler
        // forbids storing it in a field or capturing it, and BuildPulseLookup
        // clears its backing storage before every pass, so nothing survives the
        // frame and there is no invalidation rule to get wrong. Correctness
        // therefore rests entirely on nothing mutating the effect buffer
        // between this line and the last read of the lookup in the loop, which
        // holds: the only two mutators, PresentationCoordinator.IngestTick
        // (line 126) and AdvanceEffects (line 149), are both driven from
        // Update — ArenaGame.cs lines 1212 and 536 — which MonoGame runs to
        // completion before Draw, and nothing on the Draw path between here and
        // the loop's end touches HitEffects. HitEffectRenderer reads
        // ActiveEffects after DrawPawns has returned (line 682) and only reads.
        //
        // Built inside the geometry span deliberately. The per-pawn scan it
        // replaces was charged to geometry, so charging its replacement
        // anywhere else would make the span's improvement look larger than the
        // work actually saved.
        var hitPulses = _presentation.HitEffects.BuildPulseLookup();

        for (var ordinal = 0; ordinal < agents.Count; ordinal++)
        {
            var agent = agents[ordinal];

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

            // GPU-018. Same value PawnAppearanceFactory.Create returns for
            // these three arguments — the cache never computes an appearance
            // itself, it only remembers one the factory produced, and it
            // compares the stored (entity, weapon, shield) key before returning
            // anything — so the layout, the cull, and the drawn set below are
            // unchanged.
            var appearance = _presentation.PawnAppearances.Resolve(
                ordinal,
                agent.EntityId,
                agent.Loadout.Weapon,
                agent.Loadout.Shield);

            // GPU-014, stage one. The cull rectangle without the posed layout
            // behind it. Pose-blind on purpose: a pose-aware cull would make
            // the set of drawn pawns a function of presentation animation
            // phase, so the same tick would render a different draw list
            // depending on where each swing clock sat. The rectangle is the
            // same one PawnRenderer.GetBounds returns for these arguments —
            // PawnGeometryTests pins that over the full input grid — so the
            // drawn set below is unchanged by construction.
            var pawnPrefix = PawnGeometry.PoseBlindPrefix.Create(
                footAnchor,
                _camera.Zoom,
                appearance);

            if (_renderProbeEnabled)
            {
                // GPU-005, as amended by GPU-014. One pawn-layout construction
                // begun for this agent. An agent the cull rejects stops here,
                // having paid only the pose-invariant stage, which is strictly
                // less than the full GetBounds construction it used to pay and
                // is why this count now understates rather than overstates the
                // saving. An agent that survives finishes that same
                // construction below and is not counted a second time, because
                // the two stages together do the work of one, not two.
                pawnGeometryInvocations++;
            }

            if (!arenaBounds.Intersects(pawnPrefix.PoseBlindVisualBounds))
            {
                continue;
            }

            // The four values below were argument expressions on the
            // PawnRenderer.Draw call this replaces, evaluated left to right in
            // exactly this order. Each is a side-effect-free read, so naming
            // them as locals changes nothing about what is drawn; it only puts
            // them on the geometry side of the boundary, where per-agent CPU
            // work belongs.
            var factionColor = FactionColorPalette.GetPawnColor(agent.FactionId);
            var state = GetPawnVisualState(
                agent.EntityId,
                selectedEntityId,
                hoveredEntityId);
            // GPU-020. One hashed read against the frame's snapshot, in place
            // of a full scan of the live-effect buffer per pawn. Same value,
            // same position in the same left-to-right argument order.
            var hitPulseStrength = hitPulses.GetPulseStrength(agent.EntityId);
            var swingPose = SwingPoseResolver.TryGetPose(
                _swingPoses,
                agent.EntityId,
                out var pose)
                ? pose
                : (SwingPose?)null;

            // GPU-014, stage two. Redundancy R1 is gone: this finishes the
            // construction stage one began, reading the proportions no swing
            // pose can change rather than deriving them a second time. Same
            // inputs, same layout, same pixels as the PawnGeometry.Create call
            // this replaces — PawnGeometryTests pins that too. Deliberately
            // not counted as a second invocation; see the note at stage one.
            var pawnLayout = pawnPrefix.CompletePosedLayout(swingPose);

            CloseArenaGeometrySpan();

            PawnRenderer.DrawLayout(
                spriteBatch,
                pixel,
                pawnLayout,
                appearance,
                factionColor,
                state,
                hitPulseStrength,
                contingentId: agent.ContingentId,
                contingentState: agent.ContingentState);

            OpenArenaGeometrySpan();
        }

        CloseArenaGeometrySpan();

        if (_renderProbeEnabled)
        {
            // GPU-005. One integer store per frame. The geometry span has
            // already closed on the line above, so this store lands in the
            // submission span, and the two local increments above land in the
            // geometry span — a register increment and a single store against
            // spans reported in hundreds of microseconds. Both are stated here
            // rather than left for a reader to discover. Everything costlier
            // — the run-total accumulation and the recorder call — happens in
            // Draw, after both spans have closed, so nothing that could
            // meaningfully move a figure is charged to the figure it explains.
            _frameDrawPathPawnGeometryInvocations = pawnGeometryInvocations;
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
