using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Hukbo.Client.Audio;
using Hukbo.Client.Diagnostics;
using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Core.Simulation;
using Hukbo.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client;

public sealed partial class ArenaGame : Game
{
    private const int InitialWindowWidth = 1280;
    private const int InitialWindowHeight = 720;

    // The integration design's measurement matrix (VIS-035, section 11) is
    // fixed at 1080p; the render probe forces the window to this size
    // instead of the normal client's smaller default so a captured report
    // is comparable across runs and hardware.
    private const int RenderProbeWindowWidth = 1920;
    private const int RenderProbeWindowHeight = 1080;
    private const int StatusBarHeight = 68;
    private const int EventPanelWidth = 420;
    private const int LayoutMargin = 12;
    private const int LayoutGap = 10;
    private const int InspectorWidth = 310;

    // Derived, not guessed: tall enough for the base detail rows plus
    // AgentInspectorContent.EvidenceReservedLineCount wrapped evidence
    // lines, per AgentInspectorContent.ComputeRequiredHeight — the exact
    // row math AgentInspectorPanel.Draw uses. AgentInspectorPanel also
    // refuses to draw any row that would still fall past its own bounds,
    // so this cannot overflow even if a future evidence string needs
    // more lines than reserved.
    private static readonly int InspectorHeight =
        AgentInspectorContent.ComputeRequiredHeight(
            AgentInspectorContent.EvidenceReservedLineCount);
    private const int EventHistoryCapacity = 200;

    // Both derived from the sound log's real row math after the font
    // overhaul grew its row, header, and section heights to clear the
    // Caption (20px real line spacing) and Title (35px real line spacing)
    // rungs — see the derivation comment above
    // `SoundLogPanel.Layout.cs:CaptionLineSpacing`.
    //
    // `SoundLogHeightPercent` buys a ten-row viewport onto the sound log's
    // expected-files list. It does not buy a view of the whole list, and
    // no percentage could: the list runs to thirty-seven rows at the
    // thirteen slots the catalog now carries, being one row per slot plus
    // one indented hit-class row for each of the four location-driven
    // weapon slots, while `SoundLogPanel.CalculateLayout` deliberately caps
    // the section at one section header plus `SoundCatalog.AllSounds.Count`
    // binding rows. The rows past that cap are reached by scrolling the
    // section with the mouse wheel, which the panel implements.
    //
    // Ten of those rows need a real panel height of 416px. The panel spends
    // 196px of any height on its vertical padding, its header, its path
    // line, the gap between its two sections, and the cue log's own section
    // header plus the three minimum-reserved cue rows; the remaining 220px
    // is exactly one 20px section header and ten 20px binding rows, with
    // zero slack. At the default 1280x720 window the right column is
    // 720 - 68 - 12 == 640px tall, that being the window height less the
    // status bar and the layout margin, and 640 * 65 / 100 == 416 exactly
    // under integer division.
    //
    // `SoundLogMinimumHeight` is the analogous floor for a shorter window —
    // header, path, one binding row, and the three reserved cue rows — and
    // the slot count does not move it, because reserving a single binding
    // row is independent of how many slots exist. It therefore stays at 236
    // across this change, and it stays comfortably below the percentage
    // figure at the default window, so the percentage branch keeps deciding
    // there.
    private const int SoundLogMinimumHeight = 236;
    private const int SoundLogHeightPercent = 65;
    private const int MaximumSafeRawCoordinate =
        Scenario.MaximumMapDimension * FixedPoint.Scale;
    private const ulong DefaultSeed = 1;
    private const double MaximumAccumulatedSeconds = 0.5;
    private const string CompositionStagedNotice =
        "Army composition staged — takes effect on next Full Reset";

    private readonly GraphicsDeviceManager _graphics;
    private readonly InputEdges _input = new();
    private readonly MenuOverlay _menu;
    private readonly UiThemeManager _themeManager;
    private readonly ControlBar _controlBar = new();
    private readonly AgentInspectorPanel _inspectorPanel = new();
    private readonly BattleEventLogPanel _eventLogPanel = new();
    private readonly SoundLogPanel _soundLogPanel = new();
    private readonly SoundDirector _soundDirector;
    private readonly MatchSummaryPanel _summaryPanel = new();
    private readonly BattleReportPanel _battleReportPanel = new();

    /// <summary>
    /// Guards the unrecoverable action. There is no save, so a stray click on
    /// the control bar — which the spectator uses constantly — must not end a
    /// battle outright. Cancel is the focused control when it opens.
    /// </summary>
    private readonly ConfirmationPrompt _quitPrompt = new(
        "Quit Hukbo? The battle in progress will be lost.",
        "Quit",
        ClientCommand.Exit);
    /// <summary>
    /// Assigned in the constructor rather than here because it now needs
    /// <see cref="_renderMetricsRecorder"/>: its appearance cache (GPU-017,
    /// adopted by GPU-018) reports hits, misses, and fills through that seam,
    /// and a field initializer cannot read another instance field.
    /// </summary>
    private readonly PresentationCoordinator _presentation;
    private readonly AgentSelection _hoverSelection = new();
    private readonly ArenaAutoPanController _autoPan = new();
    private readonly MatchSeries _matchSeries = new(DefaultSeed);
    private readonly ClientSettingsStore _settingsStore;
    private readonly GoreIntensityManager _goreManager;
    private readonly MotionIntensityManager _motionManager;
    private readonly AutoCameraModeManager _autoCameraManager;
    private readonly ArmyCompositionPanel _armyCompositionPanel;

    /// <summary>
    /// Reused each frame so the draw path allocates nothing. The mapping into
    /// it lives in <see cref="SwingPoseResolver"/> rather than here, because
    /// this file is banned from tests and anything in it is untestable by
    /// construction.
    /// </summary>
    private readonly Dictionary<ulong, SwingPose> _swingPoses = [];
    private readonly DiagnosticLog _log;

    /// <summary>
    /// Reduces the per-frame timings to one line per second of wall time. Held
    /// unconditionally — it is eight doubles and costs nothing when nothing
    /// feeds it — but only fed when <see cref="_isFrameTimingLogged"/> says a
    /// window would actually be written.
    /// </summary>
    private readonly FrameTimingAggregator _frameTiming = new();

    /// <summary>
    /// Whether frames are measured at all, resolved once from the log rather
    /// than tested per frame. The frame loop reads this before taking a single
    /// timestamp, so a run with the render channel filtered out — and every
    /// <c>Release</c> run, where the log is off entirely — pays one bool test
    /// per frame and nothing else.
    /// </summary>
    /// <remarks>
    /// Resolved against <see cref="LogLevel.Warning"/>, not
    /// <see cref="LogLevel.Debug"/>, because a window that starved reports at
    /// warn: a run filtered down to warnings must still get the finding, and
    /// finding it requires having measured. The routine summary is written at
    /// debug and filters itself out on such a run.
    /// </remarks>
    private readonly bool _isFrameTimingMeasured;

    /// <summary>
    /// Whether the per-frame <c>render.frame</c> line is enabled. Separate from
    /// <see cref="_isFrameTimingLogged"/> because it is a <c>trc</c> line: an
    /// ordinary <c>dbg</c> run gets the one-a-second summary, and only a run
    /// asked for trace pays a line per frame.
    /// </summary>
    private readonly bool _isFrameTraceLogged;

    private Scenario _scenario;
    private BattleSimulation _simulation;
    private SpectatorCamera _camera;
    private ImmutableArray<PlainsDecal> _plainsDecals;
    private ImmutableArray<GrassCluster> _grassClusters;
    private SpriteBatch? _spriteBatch;
    private RasterizerState? _arenaRasterizerState;
    private Texture2D? _pixel;
    private UiFontSet? _fonts;
    private MonoGameSoundPlayer? _soundPlayer;
    private Settings.ArmyComposition _activeComposition;
    private bool _isSoundLogVisible;
    private bool _isBattleReportVisible;
    private bool _isArmyCompositionPanelVisible;
    private bool _isCompositionStaged;
    private bool _exitRequested;
    private int _speedMultiplier = 1;
    private double _simulationAccumulator;

    // The frame's own measurements, written by Update and AdvanceSimulation
    // and read at the end of Update. Fields rather than arguments because the
    // producer and the consumer are separated by the whole input chain.
    private double _frameDrawMilliseconds;
    private int _frameSimulationTicks;
    private bool _frameSimulationStarved;

    // CompleteMatch runs on every frame that follows a decided match, so the
    // outcome line needs its own guard or the log fills with one identical row
    // per frame until the spectator resets.
    private long _loggedOutcomeTick = -1;
    private string _lastFocusTarget = string.Empty;

    /// <summary>
    /// Debug-time opt-in for <c>tools/Hukbo.Tools.RenderProbe</c> (VIS-035):
    /// when the <c>HUKBO_RENDER_PROBE</c> environment variable is exactly
    /// <c>"1"</c>, every <see cref="Draw"/> call publishes a
    /// <see cref="RenderProbeSample"/> through <see cref="RenderProbeSampled"/>
    /// and <see cref="SetProbeCameraZoom"/> becomes live. Read once here,
    /// never per frame, so a default run (the variable unset, which is every
    /// Release run) pays a single cached bool check and nothing else — the
    /// render path's cost is unaffected.
    /// </summary>
    private readonly bool _renderProbeEnabled =
        Environment.GetEnvironmentVariable("HUKBO_RENDER_PROBE") == "1";

    /// <summary>
    /// The arena batch's Tier 1/Tier 2 measurement seam (VIS-034/VIS-035R,
    /// amendment A-1): <see cref="SpriteBatchRenderMetricsRecorder"/> when
    /// the render-probe opt-in is active, <see cref="NullRenderMetricsRecorder"/>
    /// otherwise, so a normal run's every call through this field is the
    /// disabled no-op the debug-logging standard requires of a disabled
    /// call. Assigned once in the constructor, never reassigned.
    /// </summary>
    private readonly IRenderMetricsRecorder _renderMetricsRecorder;

    private long _renderProbeFrameStartTimestamp;

    /// <summary>
    /// <see cref="System.GC.GetAllocatedBytesForCurrentThread"/> as of the
    /// previous probe-enabled <see cref="Draw"/> call, so this frame's
    /// <see cref="RenderMetricsSnapshot.ManagedBytesAllocated"/> (R-W4.10)
    /// can be set from a genuine frame-to-frame delta rather than the
    /// cumulative-since-process-start counter. Unused, and never read, when
    /// the render-probe opt-in is off.
    /// </summary>
    private long _renderProbePreviousAllocatedBytes;

    /// <summary>
    /// Fires once per <see cref="Draw"/> call while the render-probe opt-in
    /// is active; never raised otherwise. <c>Hukbo.Tools.RenderProbe</c> is
    /// the only subscriber today (VIS-035).
    /// </summary>
    public event Action<RenderProbeSample>? RenderProbeSampled;

    /// <param name="log">
    /// The debug log every subsystem in the client writes through. Optional so
    /// nothing outside <c>Program</c> has to supply one; defaults to the
    /// no-op log, which is also what a <c>Release</c> build resolves to.
    /// </param>
    /// <param name="scenarioOverride">
    /// Replaces the startup scenario that <c>BuildScenario</c> would
    /// otherwise construct from the persisted army composition. Null in
    /// every normal run; <c>Hukbo.Tools.RenderProbe</c> (VIS-035) is the
    /// only caller that supplies one, so it can launch the real client
    /// against a scripted seed and unit count instead of a spectator's
    /// saved settings.
    /// </param>
    public ArenaGame(DiagnosticLog? log = null, Scenario? scenarioOverride = null)
    {
        _log = log ?? DiagnosticLog.Disabled;
        _isFrameTimingMeasured =
            _log.IsEnabledFor(LogLevel.Warning, LogChannel.Render);
        _isFrameTraceLogged =
            _log.IsEnabledFor(LogLevel.Trace, LogChannel.Render);
        _soundDirector = new SoundDirector(
            EventHistoryCapacity,
            new SilentSoundPlayer(SoundLibrary.GetDefaultDirectoryPath()),
            budget: null,
            _log);

        var catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "ui-theme-standards.json");
        var catalog = UiThemeCatalog.LoadOrFallback(catalogPath, _log);
        _settingsStore = ClientSettingsStore.CreateDefault(_log);
        _themeManager = new UiThemeManager(catalog, _settingsStore);
        _goreManager = new GoreIntensityManager(
            _settingsStore.Load(catalog.DefaultThemeId).GoreIntensity,
            value => TryPersistGoreIntensity(catalog.DefaultThemeId, value));
        _motionManager = new MotionIntensityManager(
            _settingsStore.Load(catalog.DefaultThemeId).MotionIntensity,
            value => TryPersistMotionIntensity(catalog.DefaultThemeId, value));
        _autoCameraManager = new AutoCameraModeManager(
            _settingsStore.Load(catalog.DefaultThemeId).AutoCameraMode,
            value => TryPersistAutoCameraMode(catalog.DefaultThemeId, value));

        // Resolved here, ahead of the coordinator below, because the
        // coordinator's appearance cache reports through it. _renderProbeEnabled
        // is a field initializer, so it is already settled by this point, and
        // moving this assignment earlier in the same constructor changes
        // nothing about what a normal run gets: NullRenderMetricsRecorder,
        // whose every call is a no-op.
        _renderMetricsRecorder = _renderProbeEnabled
            ? new SpriteBatchRenderMetricsRecorder()
            : NullRenderMetricsRecorder.Instance;
        _presentation = new PresentationCoordinator(
            EventHistoryCapacity,
            renderMetricsRecorder: _renderMetricsRecorder);

        // A restored preference takes effect from tick zero, so the spectator
        // never has to reopen the menu after a relaunch.
        _presentation.Blood.Intensity = _goreManager.Value;
        _presentation.Dust.MotionIntensity = _motionManager.Value;
        _menu = new MenuOverlay(catalog.Themes, catalog.Standards);
        _activeComposition =
            _settingsStore.Load(catalog.DefaultThemeId).Composition;
        _armyCompositionPanel = new ArmyCompositionPanel(
            ToPanelComposition(_activeComposition),
            catalog.Standards.Shared.ArmyComposition);

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth =
                _renderProbeEnabled ? RenderProbeWindowWidth : InitialWindowWidth,
            PreferredBackBufferHeight =
                _renderProbeEnabled ? RenderProbeWindowHeight : InitialWindowHeight,
            SynchronizeWithVerticalRetrace = true,
        };

        Window.AllowUserResizing = true;
        Window.IsBorderless = true;
        Window.Title = "Hukbo";
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = false;

        _scenario = scenarioOverride ??
            BuildScenario(_matchSeries.CurrentSeed, _activeComposition);
        _simulation = BattleSimulation.Create(_scenario);
        _camera = new SpectatorCamera(_scenario.MapWidth, _scenario.MapHeight);
        _plainsDecals = PlainsBackdropGeometry.GenerateDecals(
            _scenario.Seed,
            _scenario.MapWidth,
            _scenario.MapHeight);
        _grassClusters = GrassGeometry.GenerateClusters(
            _scenario.Seed,
            _scenario.MapWidth,
            _scenario.MapHeight);
        _presentation.EventFeed.SetScenarioSeed(_scenario.Seed);

        LogScenarioBuilt("startup");
    }

    /// <summary>
    /// Overrides the spectator camera's zoom directly, bypassing wheel-input
    /// scaling. No-op unless the render-probe opt-in is active, so a
    /// spectator's own zoom is the only path in a normal run — this exists
    /// for <c>Hukbo.Tools.RenderProbe</c> (VIS-035) to drive the three
    /// scripted camera stations (minimum zoom, default fit, maximum zoom)
    /// named in the integration design's measurement matrix.
    /// </summary>
    public void SetProbeCameraZoom(float zoom)
    {
        if (_renderProbeEnabled)
        {
            _camera.SetZoom(zoom);
        }
    }

    /// <summary>
    /// Turns the graphics device's wait for the display's vertical retrace on
    /// or off for the rest of this game's life. No-op unless the render-probe
    /// opt-in is active, so a normal run keeps the constructor's
    /// <c>SynchronizeWithVerticalRetrace = true</c> and never executes a
    /// statement in here — the same shape as <see cref="SetProbeCameraZoom"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GPU-006, integration design section 4.3. Presentation itself happens in
    /// <c>Game.EndDraw</c>, outside the probe's measured window, but driver
    /// back-pressure is not: once the driver has buffered its maximum number
    /// of in-flight frames the next graphics call blocks, and the first
    /// graphics call of the next frame is the <c>GraphicsDevice.Clear</c> at
    /// the top of <c>Draw</c> — inside the window. A blocking wait for the
    /// display is not CPU cost, so a probe whose whole purpose is to measure
    /// CPU cost per frame has to disable the wait or every percentile it
    /// reports is a floor imposed by the display rather than a measurement.
    /// </para>
    /// <para>
    /// Call this before <see cref="Microsoft.Xna.Framework.Game.Run"/>.
    /// <c>GraphicsDeviceManager</c> reads the flag when it builds the device's
    /// <c>PresentationParameters</c>, which happens during device creation, so
    /// a call made before the device exists needs no <c>ApplyChanges</c> — and
    /// must not make one, because <c>ApplyChanges</c> creates the device
    /// itself when none exists yet, which would drag device creation out of
    /// the normal startup order. The guarded call below therefore exists only
    /// for the case where the device is already live.
    /// </para>
    /// </remarks>
    public void SetProbeVerticalRetrace(bool synchronize)
    {
        if (!_renderProbeEnabled)
        {
            return;
        }

        _graphics.SynchronizeWithVerticalRetrace = synchronize;

        // A fixed-step loop would re-impose a cadence cap of its own on top of
        // the one this method just lifted, so the probe states its requirement
        // here rather than leaving it resting on a constructor line no part of
        // the probe owns. Already false for every run today; this keeps it
        // false for a probe run regardless of what the constructor decides
        // later.
        IsFixedTimeStep = false;

        if (_graphics.GraphicsDevice is not null)
        {
            _graphics.ApplyChanges();
        }
    }

    /// <summary>
    /// Whether this game is presenting synchronized to the display's vertical
    /// retrace, read from the device that actually ran rather than from the
    /// value anybody asked for.
    /// </summary>
    /// <remarks>
    /// GPU-006. <c>Hukbo.Tools.RenderProbe</c> reads this after
    /// <see cref="Microsoft.Xna.Framework.Game.Run"/> returns and records it on
    /// the report fingerprint, so a report always states the retrace setting of
    /// the run that produced it and can never be silently compared against a
    /// report captured under the other setting. The device's own
    /// <c>PresentationInterval</c> is preferred because it is what
    /// <c>GraphicsDeviceManager</c> actually resolved the flag to at device
    /// creation; the manager's flag is the fallback for the window before any
    /// device exists.
    /// </remarks>
    public bool IsVerticalRetraceSynchronized =>
        _graphics.GraphicsDevice?.PresentationParameters is { } parameters
            ? parameters.PresentationInterval != PresentInterval.Immediate
            : _graphics.SynchronizeWithVerticalRetrace;

    /// <summary>
    /// Re-reads the whole settings file at save time, mirroring
    /// <c>UiThemeManager</c>'s persist path, so writing a gore level carries
    /// forward the army composition a panel may have staged since startup. The
    /// theme comes from the in-memory manager, which is authoritative.
    /// </summary>
    private bool TryPersistGoreIntensity(
        string defaultThemeId,
        GoreIntensity value)
    {
        var current = _settingsStore.Load(defaultThemeId);
        return _settingsStore.TrySave(
            _themeManager.ActiveTheme.Id,
            current.Composition,
            value,
            current.MotionIntensity,
            current.AutoCameraMode);
    }

    /// <summary>
    /// Mirrors <see cref="TryPersistGoreIntensity"/> for the motion setting:
    /// re-reads the whole settings file at save time so a motion-level write
    /// carries forward the theme, composition, and gore level unchanged.
    /// </summary>
    private bool TryPersistMotionIntensity(
        string defaultThemeId,
        MotionIntensity value)
    {
        var current = _settingsStore.Load(defaultThemeId);
        return _settingsStore.TrySave(
            _themeManager.ActiveTheme.Id,
            current.Composition,
            current.GoreIntensity,
            value,
            current.AutoCameraMode);
    }

    /// <summary>
    /// Mirrors <see cref="TryPersistGoreIntensity"/> for the camera-assistant
    /// setting: re-reads the whole settings file at save time so a mode write
    /// carries forward every sibling field unchanged.
    /// </summary>
    private bool TryPersistAutoCameraMode(
        string defaultThemeId,
        AutoCameraMode value)
    {
        var current = _settingsStore.Load(defaultThemeId);
        return _settingsStore.TrySave(
            _themeManager.ActiveTheme.Id,
            current.Composition,
            current.GoreIntensity,
            current.MotionIntensity,
            value);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _arenaRasterizerState = new RasterizerState
        {
            CullMode = CullMode.None,
            ScissorTestEnable = true,
        };
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        try
        {
            _fonts = UiFontSet.Load(Content.Load<SpriteFont>);
            _log.Write(
                LogLevel.Information,
                LogChannel.Assets,
                LogEvents.AssetsFontLoaded);
        }
        catch (Exception exception)
        {
            // Rethrown: without fonts there is no readable UI at all. The log
            // line exists because the crash text alone does not say which
            // content root was searched.
            _log.Write(
                LogLevel.Error,
                LogChannel.Assets,
                LogEvents.AssetsFontFailed,
                "contentRoot",
                Content.RootDirectory,
                "reason",
                exception.GetType().Name,
                "msg",
                exception.Message);
            _log.Flush();
            throw;
        }

        _soundPlayer = MonoGameSoundPlayer.Load(
            SoundLibrary.GetDefaultDirectoryPath());
        _soundDirector.AttachPlayer(_soundPlayer);

        _log.Write(
            LogLevel.Information,
            LogChannel.Boot,
            LogEvents.BootWindowCreated,
            "width",
            GraphicsDevice.Viewport.Width,
            "height",
            GraphicsDevice.Viewport.Height);

        ValidateVisualCatalogs();

        _camera.Fit(GetLayout(GraphicsDevice.Viewport.Bounds).ArenaBounds);
    }

    /// <summary>
    /// The once-at-load visual catalog validation pass (VIS-006;
    /// visual-system-integration-design.md section 2), run once here for
    /// every catalog implementing the shared <see cref="VisualCatalogEntry"/>
    /// shape. A failure never crashes the game: it is logged on the
    /// <c>assets</c> channel via <see cref="VisualDiagnostics.ReportCatalogInvalid"/>
    /// and the fallback chain (section 4) already treats any entry a future
    /// resolver marks invalid the same way it treats a missing one. All
    /// wiring, no validation logic — the checks themselves live in the
    /// testable <see cref="VisualCatalogValidator"/>.
    /// </summary>
    private void ValidateVisualCatalogs()
    {
        var diagnostics = new VisualDiagnostics();
        LogVisualCatalogFailures(
            diagnostics,
            VisualCatalogValidator.Validate(
                "appearance",
                AppearanceComponentCatalog.All
                    .Select(entry => entry.Catalog)
                    .ToArray()));
        LogVisualCatalogFailures(
            diagnostics,
            VisualCatalogValidator.Validate("backdrop", BackdropVisualCatalog.All));
    }

    private void LogVisualCatalogFailures(
        VisualDiagnostics diagnostics,
        VisualCatalogValidationResult result)
    {
        foreach (var failure in result.Failures)
        {
            diagnostics.ReportCatalogInvalid(
                _log,
                result.CatalogId,
                failure.Reason,
                failure.Message is null
                    ? $"entry '{failure.EntryId}'"
                    : $"entry '{failure.EntryId}': {failure.Message}");
        }
    }

    protected override void UnloadContent()
    {
        _soundPlayer?.Dispose();
        _pixel?.Dispose();
        _arenaRasterizerState?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        var isFrameMeasured = _isFrameTimingMeasured || _isFrameTraceLogged;
        var updateStartTimestamp =
            isFrameMeasured ? Stopwatch.GetTimestamp() : 0L;
        _frameSimulationTicks = 0;
        _frameSimulationStarved = false;

        _input.Update();
        _soundDirector.BeginFrame(gameTime.ElapsedGameTime.TotalSeconds);
        _presentation.AdvanceEffects(
            (float)gameTime.ElapsedGameTime.TotalSeconds,
            _speedMultiplier);
        SwingPoseResolver.Resolve(
            _presentation.Swings,
            _simulation.Agents,
            _swingPoses);
        var screenBounds = GraphicsDevice.Viewport.Bounds;
        var layout = GetLayout(screenBounds);
        _eventLogPanel.ReleaseKeyboardFocusIfPointerLeaves(
            _input,
            layout.EventBounds);
        var eventEscapeConsumed =
            !_menu.IsVisible &&
            _eventLogPanel.HandleEscape(
                _input,
                _presentation.EventFeed);

        if (_input.WasPressed(Keys.Escape) && !eventEscapeConsumed)
        {
            if (_isArmyCompositionPanelVisible)
            {
                _armyCompositionPanel.PerformAction(
                    ArmyCompositionPanelAction.Cancel);
                _isArmyCompositionPanelVisible = false;
            }
            else
            {
                ToggleMenu();
            }
        }

        var pointerConsumed = false;

        // Which surface claimed the press. The priority chain below is
        // invisible from outside the debugger, and a click that "did nothing"
        // is almost always a click a surface above the intended one swallowed.
        var consumedBy = "none";

        // The prompt is modal, so it takes the whole chain before anything else
        // is consulted and reports every click consumed while it is open. It
        // also owns Escape for that frame, which is why it returns immediately
        // rather than falling through to the menu handling below.
        if (_quitPrompt.IsVisible)
        {
            var promptInteraction = _quitPrompt.Update(_input, screenBounds);
            LogPointer("quitPrompt");
            if (promptInteraction.Command != ClientCommand.None)
            {
                ApplyClientCommand(promptInteraction.Command);
            }

            return;
        }

        if (_menu.IsVisible && _isArmyCompositionPanelVisible)
        {
            var panelInteraction = _armyCompositionPanel.Update(
                _input,
                screenBounds);
            pointerConsumed = panelInteraction.PointerConsumed;
            consumedBy = pointerConsumed ? "armyComposition" : consumedBy;
            ApplyArmyCompositionResult(panelInteraction.Result);
        }
        else if (_menu.IsVisible)
        {
            var menuInteraction = _menu.Update(
                _input,
                screenBounds,
                _themeManager.ActiveTheme.Id,
                _goreManager.Value,
                _motionManager.Value,
                _autoCameraManager.Value);
            pointerConsumed = menuInteraction.PointerConsumed;
            consumedBy = pointerConsumed ? "menu" : consumedBy;
            if (menuInteraction.SelectedThemeId is { } selectedThemeId)
            {
                var previousThemeId = _themeManager.ActiveTheme.Id;
                if (_themeManager.TrySelect(selectedThemeId))
                {
                    LogSettingChanged(
                        "theme",
                        previousThemeId,
                        _themeManager.ActiveTheme.Id);
                }
            }

            if (menuInteraction.SelectedGoreIntensity is { } selectedGore)
            {
                var previousGore = _goreManager.Value;
                _goreManager.TrySelect(selectedGore);
                _presentation.Blood.Intensity = _goreManager.Value;
                if (_goreManager.Value != previousGore)
                {
                    LogSettingChanged(
                        "gore",
                        previousGore.ToString(),
                        _goreManager.Value.ToString());
                }
            }

            if (menuInteraction.SelectedMotionIntensity is { } selectedMotion)
            {
                var previousMotion = _motionManager.Value;
                _motionManager.TrySelect(selectedMotion);
                _presentation.Dust.MotionIntensity = _motionManager.Value;
                if (_motionManager.Value != previousMotion)
                {
                    LogSettingChanged(
                        "motion",
                        previousMotion.ToString(),
                        _motionManager.Value.ToString());
                }
            }

            if (menuInteraction.SelectedAutoCameraMode is { } selectedCamera)
            {
                var previousCamera = _autoCameraManager.Value;
                _autoCameraManager.TrySelect(selectedCamera);
                if (_autoCameraManager.Value != previousCamera)
                {
                    // The assistant keeps no state that survives a mode
                    // change: a pan in flight under the old mode has no
                    // meaning under the new one.
                    _autoPan.Reset();
                    LogSettingChanged(
                        "autoCamera",
                        previousCamera.ToString(),
                        _autoCameraManager.Value.ToString());
                }
            }

            ApplyClientCommand(menuInteraction.Command);
        }
        else
        {
            var interaction = _battleReportPanel.Update(
                _input,
                _isBattleReportVisible ? _presentation.Report : null,
                layout.ArenaBounds);
            pointerConsumed = interaction.PointerConsumed;
            consumedBy = pointerConsumed ? "battleReport" : consumedBy;

            if (!pointerConsumed)
            {
                interaction = _summaryPanel.Update(
                    _input,
                    _presentation.Summary,
                    layout.ArenaBounds);
                pointerConsumed = interaction.PointerConsumed;
                consumedBy = pointerConsumed ? "matchSummary" : consumedBy;
            }

            if (!pointerConsumed)
            {
                interaction = _controlBar.Update(
                    _input,
                    screenBounds,
                    _presentation.Playback.IsPlaying,
                    _isSoundLogVisible);
                pointerConsumed = interaction.PointerConsumed;
                consumedBy = pointerConsumed ? "controlBar" : consumedBy;
            }

            if (!pointerConsumed)
            {
                interaction = _eventLogPanel.Update(
                    _input,
                    _presentation.EventFeed,
                    layout.EventBounds);
                pointerConsumed = interaction.PointerConsumed;
                consumedBy = pointerConsumed ? "eventLog" : consumedBy;
            }

            if (!pointerConsumed && _isSoundLogVisible)
            {
                interaction = _soundLogPanel.Update(
                    _input,
                    _soundDirector,
                    layout.SoundLogBounds);
                pointerConsumed = interaction.PointerConsumed;
                consumedBy = pointerConsumed ? "soundLog" : consumedBy;
            }

            if (!pointerConsumed)
            {
                interaction = _inspectorPanel.Update(
                    _input,
                    _presentation.Selection.Resolve(_simulation.Agents),
                    layout.InspectorBounds);
                pointerConsumed = interaction.PointerConsumed;
                consumedBy = pointerConsumed ? "inspector" : consumedBy;
            }

            if (!pointerConsumed &&
                layout.ArenaBounds.Contains(_input.MousePosition))
            {
                consumedBy = "arena";
            }

            UpdateSpectatorInput(
                interaction.Command,
                layout,
                pointerConsumed,
                (float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        LogPointer(consumedBy);
        LogFocusChange();
        AdvanceSimulation(gameTime.ElapsedGameTime.TotalSeconds);
        UpdateWindowTitle();

        if (isFrameMeasured)
        {
            LogFrameTiming(
                gameTime.ElapsedGameTime.TotalMilliseconds,
                Stopwatch.GetElapsedTime(updateStartTimestamp)
                    .TotalMilliseconds);
        }

        // One flush per frame rather than one per line: a crash still keeps
        // everything up to the previous frame, and warnings and errors flush
        // themselves the moment they are written.
        _log.Flush();

        base.Update(gameTime);
    }

    /// <summary>
    /// Records a move of the event log's keyboard focus. Focus decides whether
    /// the spectator's next keystroke reaches the panel or the simulation
    /// shortcuts, which makes it the usual explanation for a shortcut that
    /// stopped working.
    /// </summary>
    private void LogFocusChange()
    {
        var target = _eventLogPanel.KeyboardFocusTarget.ToString();
        if (string.Equals(target, _lastFocusTarget, StringComparison.Ordinal))
        {
            return;
        }

        var previous = _lastFocusTarget;
        _lastFocusTarget = target;
        _log.Write(
            LogLevel.Debug,
            LogChannel.Input,
            LogEvents.InputFocusChanged,
            "from",
            previous,
            "to",
            target);
    }

    /// <summary>
    /// Records a left-button press and the surface that claimed it. Only a
    /// press is logged, never a hover or a held button, so the line count
    /// tracks what the spectator actually did.
    /// </summary>
    private void LogPointer(string consumedBy)
    {
        if (!_input.WasLeftMousePressed())
        {
            return;
        }

        // A press whose position is off the viewport happened in another window
        // or on another monitor. No surface could have claimed it, and calling
        // that "none" would read as a dead click on our own UI.
        var position = _input.MousePosition;
        var target = GraphicsDevice.Viewport.Bounds.Contains(position)
            ? consumedBy
            : "outside";

        _log.Write(
            LogLevel.Debug,
            LogChannel.Input,
            LogEvents.InputPointer,
            "button",
            "left",
            "x",
            position.X,
            "y",
            position.Y,
            "consumedBy",
            target);
    }

    private void UpdateSpectatorInput(
        ClientCommand panelCommand,
        ClientLayout layout,
        bool pointerConsumed,
        float elapsedSeconds)
    {
        var gate = SpectatorInputGate.Resolve(
            _eventLogPanel.KeyboardFocusTarget);

        var command =
            panelCommand == ClientCommand.None && gate.AllowSpectatorCommands
                ? GetSpectatorKeyboardCommand()
                : panelCommand;
        ApplyClientCommand(command);

        if (gate.AllowSpeedShortcuts)
        {
            HandleSpeedInput();
        }

        HandleArenaSelection(layout.ArenaBounds, pointerConsumed);

        var manualPanApplied = _camera.Update(
            _input,
            elapsedSeconds,
            allowZoom: !pointerConsumed,
            gate.PanInput);

        UpdateAutoPan(layout.ArenaBounds, manualPanApplied, elapsedSeconds);
    }

    /// <summary>
    /// Drifts the camera to the nearest melee once the spectator's screen holds
    /// no fighting at all. Spectator pan input always wins, and the assistant
    /// stays out of the way while the match summary is up.
    /// </summary>
    private void UpdateAutoPan(
        Rectangle arenaBounds,
        bool manualPanApplied,
        float elapsedSeconds)
    {
        var center = _autoPan.Update(
            _simulation.Agents,
            _camera.Center,
            _camera.GetVisibleHalfExtents(arenaBounds),
            _camera.Zoom,
            _autoCameraManager.Value,
            manualPanApplied,
            isSuppressed: _presentation.Summary is not null,
            elapsedSeconds);

        _camera.MoveCenterTo(center);
    }

    private ClientCommand GetSpectatorKeyboardCommand()
    {
        if (_input.WasPressed(Keys.R))
        {
            var shifted = _input.IsDown(Keys.LeftShift) ||
                _input.IsDown(Keys.RightShift);
            return LogKeyCommand(
                shifted ? "Shift+R" : "R",
                shifted ? ClientCommand.FullReset : ClientCommand.NextRound);
        }

        if (_input.WasPressed(Keys.Space))
        {
            return LogKeyCommand(
                "Space",
                _presentation.Playback.IsPlaying
                    ? ClientCommand.Pause
                    : ClientCommand.Play);
        }

        if (_input.WasPressed(Keys.F9))
        {
            return LogKeyCommand("F9", ClientCommand.ToggleSoundLog);
        }

        return ClientCommand.None;
    }

    /// <summary>
    /// Records the key that produced a command and returns the command
    /// unchanged, so the mapping is visible in the log without the caller
    /// growing a second statement per branch.
    /// </summary>
    private ClientCommand LogKeyCommand(string key, ClientCommand command)
    {
        _log.Write(
            LogLevel.Debug,
            LogChannel.Input,
            LogEvents.InputKey,
            "key",
            key,
            "command",
            command.ToString());
        return command;
    }

    private void HandleSpeedInput()
    {
        var previous = _speedMultiplier;
        if (_input.WasPressed(Keys.D1) || _input.WasPressed(Keys.NumPad1))
        {
            _speedMultiplier = 1;
        }
        else if (_input.WasPressed(Keys.D2) || _input.WasPressed(Keys.NumPad2))
        {
            _speedMultiplier = 2;
        }
        else if (_input.WasPressed(Keys.D4) || _input.WasPressed(Keys.NumPad4))
        {
            _speedMultiplier = 4;
        }

        if (_speedMultiplier != previous)
        {
            _log.Write(
                LogLevel.Information,
                LogChannel.Simulation,
                LogEvents.SimSpeedChanged,
                "from",
                previous,
                "to",
                _speedMultiplier);
        }
    }

    private void HandleArenaSelection(
        Rectangle arenaBounds,
        bool pointerConsumed)
    {
        if (pointerConsumed ||
            !_input.WasLeftMousePressed() ||
            !arenaBounds.Contains(_input.MousePosition))
        {
            return;
        }

        SelectAtPointer(_presentation.Selection, arenaBounds);
    }

    private void ToggleMenu()
    {
        if (_menu.IsVisible)
        {
            _menu.Close();
            return;
        }

        ApplyClientCommand(ClientCommand.OpenMenu);
    }

    private void LogSettingChanged(string key, string from, string to) =>
        _log.Write(
            LogLevel.Information,
            LogChannel.Settings,
            LogEvents.SettingsChanged,
            "key",
            key,
            "from",
            from,
            "to",
            to);

    private void LogPlaybackChanged() =>
        _log.Write(
            LogLevel.Information,
            LogChannel.Simulation,
            LogEvents.SimPlaybackChanged,
            "playing",
            _presentation.Playback.IsPlaying,
            "tick",
            _simulation.Tick,
            "outcome",
            _simulation.Outcome.ToString());

    private void ApplyClientCommand(ClientCommand command)
    {
        if (command != ClientCommand.None)
        {
            // Every accepted command is a click the spectator made, whether it
            // came from a button or a shortcut key.
            _soundDirector.RequestCue(GameSoundId.UiClick, _simulation.Tick);
        }

        switch (command)
        {
            case ClientCommand.None:
                return;
            case ClientCommand.ToggleSoundLog:
                _isSoundLogVisible = !_isSoundLogVisible;
                return;
            case ClientCommand.ToggleBattleReport:
                _isBattleReportVisible = !_isBattleReportVisible;
                return;
            case ClientCommand.Minimize:
                SDL_MinimizeWindow(Window.Handle);
                return;
            case ClientCommand.ToggleMaximize:
                ToggleMaximizeWindow();
                return;
            case ClientCommand.RequestExit:
                // Asks rather than acts. Only the prompt's confirm button
                // issues ClientCommand.Exit.
                _quitPrompt.Open();
                return;
            case ClientCommand.Play:
                if (_simulation.Outcome == BattleOutcome.Ongoing)
                {
                    _presentation.Playback.Play();
                }

                _simulationAccumulator = 0;
                _menu.Close();
                LogPlaybackChanged();
                return;
            case ClientCommand.Pause:
                _presentation.Playback.Pause();
                _simulationAccumulator = 0;
                LogPlaybackChanged();
                return;
            case ClientCommand.OpenMenu:
                _presentation.Playback.Pause();
                _simulationAccumulator = 0;
                _menu.Open();
                return;
            case ClientCommand.OpenArmyComposition:
                OpenArmyCompositionPanel();
                return;
            case ClientCommand.NextRound:
            case ClientCommand.FullReset:
                ResetSimulation(command);
                return;
            case ClientCommand.Exit:
                RequestExit();
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(command),
                    command,
                    null);
        }
    }

    private void OpenArmyCompositionPanel()
    {
        var saved = _settingsStore.Load(_themeManager.ActiveTheme.Id).Composition;
        _armyCompositionPanel.Open(ToPanelComposition(saved));
        _isArmyCompositionPanelVisible = true;
    }

    private void ApplyArmyCompositionResult(ArmyCompositionPanelResult result)
    {
        switch (result)
        {
            case ArmyCompositionPanelResult.Cancelled:
                _isArmyCompositionPanelVisible = false;
                return;
            case ArmyCompositionPanelResult.Applied:
                var savedForComposition = _settingsStore.Load(
                    _themeManager.ActiveTheme.Id);
                _settingsStore.TrySave(
                    _themeManager.ActiveTheme.Id,
                    ToSettingsComposition(_armyCompositionPanel.Saved),
                    savedForComposition.GoreIntensity,
                    savedForComposition.MotionIntensity,
                    savedForComposition.AutoCameraMode);
                _isCompositionStaged = true;
                _isArmyCompositionPanelVisible = false;
                return;
            default:
                return;
        }
    }

    private static ImmutableArray<int> ToRosterCounts(
        Settings.ArmyComposition composition) =>
        [.. composition.CategoryCounts];

    private static UI.ArmyComposition ToPanelComposition(
        Settings.ArmyComposition composition) =>
        new(ToRosterCounts(composition), composition.UnitsPerTeam);

    /// <summary>
    /// Rebuilds the persisted, named-field <see cref="Settings.ArmyComposition"/>
    /// from the panel's positional one. The panel's array is the source of
    /// truth for the count; the four indices below exist only because the
    /// settings record's fields are named per rank rather than array-shaped,
    /// so the constructor call itself cannot be a loop. The guard is what
    /// catches a roster-count change here loudly, in every configuration
    /// including Release, instead of silently truncating or throwing an
    /// index-out-of-range deep in a settings round-trip.
    /// </summary>
    private static Settings.ArmyComposition ToSettingsComposition(
        UI.ArmyComposition composition)
    {
        if (composition.CategoryCounts.Length !=
            Settings.ArmyComposition.CategoryCount)
        {
            throw new InvalidOperationException(
                $"Panel composition has " +
                $"{composition.CategoryCounts.Length} categories but " +
                $"{nameof(Settings.ArmyComposition)} expects " +
                $"{Settings.ArmyComposition.CategoryCount}.");
        }

        return new Settings.ArmyComposition(
            composition.UnitsPerTeam,
            composition.CategoryCounts[0],
            composition.CategoryCounts[1],
            composition.CategoryCounts[2],
            composition.CategoryCounts[3]);
    }

    private static Scenario BuildScenario(
        ulong seed,
        Settings.ArmyComposition composition) =>
        Scenario.CreateDefault(seed, composition.UnitsPerTeam * 2) with
        {
            RosterCounts = ToRosterCounts(composition),
        };

    private void RequestExit()
    {
        if (_exitRequested)
        {
            return;
        }

        _exitRequested = true;
        Exit();
    }

    /// <summary>
    /// Minimizes the window. <paramref name="window"/> must be
    /// <see cref="GameWindow.Handle"/>, which on the DesktopGL platform is
    /// the underlying <c>SDL_Window*</c> — the same handle SDL2's own
    /// window-management functions expect.
    /// </summary>
    [LibraryImport("SDL2")]
    private static partial void SDL_MinimizeWindow(nint window);

    /// <summary>
    /// Maximizes the window. Same handle contract as
    /// <see cref="SDL_MinimizeWindow"/>.
    /// </summary>
    [LibraryImport("SDL2")]
    private static partial void SDL_MaximizeWindow(nint window);

    /// <summary>
    /// Restores a maximized window to its previous size. Same handle contract
    /// as <see cref="SDL_MinimizeWindow"/>.
    /// </summary>
    [LibraryImport("SDL2")]
    private static partial void SDL_RestoreWindow(nint window);

    /// <summary>
    /// Reads SDL's own window state flags. This is what makes the Max button
    /// correct rather than merely plausible: the spectator can maximize or
    /// restore the window outside the application, through a Windows snap
    /// shortcut or the taskbar, and a boolean tracked on this class would
    /// desynchronize and invert the button. Asking SDL every time cannot go
    /// out of step with reality.
    /// </summary>
    [LibraryImport("SDL2")]
    private static partial uint SDL_GetWindowFlags(nint window);

    /// <summary>
    /// <c>SDL_WINDOW_MAXIMIZED</c> from SDL2's own <c>SDL_WindowFlags</c>
    /// enumeration. Declared here rather than imported because it is a single
    /// stable constant of SDL2's public ABI.
    /// </summary>
    private const uint SdlWindowMaximized = 0x00000080;

    /// <summary>
    /// Toggles the window between maximized and its previous size, reading the
    /// current state from SDL rather than from a tracked flag.
    /// </summary>
    private void ToggleMaximizeWindow()
    {
        var handle = Window.Handle;
        if ((SDL_GetWindowFlags(handle) & SdlWindowMaximized) != 0)
        {
            SDL_RestoreWindow(handle);
        }
        else
        {
            SDL_MaximizeWindow(handle);
        }
    }

    private void AdvanceSimulation(double elapsedSeconds)
    {
        if (!_presentation.Playback.IsPlaying)
        {
            return;
        }

        if (_simulation.Outcome != BattleOutcome.Ongoing)
        {
            CompleteMatch();
            return;
        }

        var secondsPerTick = 1d / _scenario.TickRate;
        var requestedSeconds =
            _simulationAccumulator + (elapsedSeconds * _speedMultiplier);
        _simulationAccumulator = Math.Min(
            requestedSeconds,
            MaximumAccumulatedSeconds);

        // The clamp above is the moment the simulation stops keeping pace with
        // the wall clock: whole ticks are dropped rather than run late, so the
        // battle jumps instead of playing. Recorded rather than merely
        // performed, because from a spectator's chair a dropped tick and a slow
        // frame look identical and only the log can tell them apart.
        _frameSimulationStarved = requestedSeconds > MaximumAccumulatedSeconds;

        while (_simulationAccumulator >= secondsPerTick &&
               _simulation.Outcome == BattleOutcome.Ongoing)
        {
            _simulation.AdvanceOneTick();
            _frameSimulationTicks++;
            _log.SetTick(_simulation.Tick);
            _presentation.IngestTick(
                _simulation.LastEvents,
                _simulation.Agents,
                _simulation.LastTickCombatByFaction);
            _soundDirector.Ingest(_simulation.LastEvents);
            LogTick();
            _simulationAccumulator -= secondsPerTick;
        }

        if (_simulation.Outcome != BattleOutcome.Ongoing)
        {
            CompleteMatch();
        }
    }

    /// <summary>
    /// Emits one observation of the tick that just advanced. Sampled ticks go
    /// out at debug level and every other tick at trace level, so an ordinary
    /// verbose run carries a bisectable skeleton rather than a firehose.
    /// </summary>
    /// <remarks>
    /// The state hash is computed only when the line is actually going to be
    /// written. It is a read-only query, but it is not free, and a client that
    /// pays for it every tick in a build nobody is debugging is a client that
    /// drops frames for no reason.
    /// </remarks>
    /// <summary>
    /// Records what this frame cost: the per-frame line when trace is on, and
    /// the one-a-second summary the aggregator closes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two questions a laggy session raises are how long a frame took and
    /// whether the simulation kept up, and they have different answers: the
    /// catch-up loop hides a slow frame by running several ticks in it, so a
    /// battle can hold its tick rate exactly while the picture updates twice a
    /// second. <c>starvedFrames</c> separates the two — zero means the ticks
    /// arrived on time whatever the frame rate was.
    /// </para>
    /// <para>
    /// <paramref name="updateMilliseconds"/> closes before <c>_log.Flush</c>
    /// and <c>base.Update</c>, so it measures this class's own update work and
    /// not the frame's every last microsecond. <paramref name="frameMilliseconds"/>
    /// is the whole frame — it is the elapsed time MonoGame reports, which with
    /// a variable time step is real wall time since the previous frame.
    /// </para>
    /// </remarks>
    private void LogFrameTiming(
        double frameMilliseconds,
        double updateMilliseconds)
    {
        if (_isFrameTraceLogged)
        {
            _log.Write(
                LogLevel.Trace,
                LogChannel.Render,
                LogEvents.RenderFrame,
                "frameMs",
                frameMilliseconds,
                "updateMs",
                updateMilliseconds,
                "drawMs",
                _frameDrawMilliseconds,
                "simTicks",
                _frameSimulationTicks,
                "starved",
                _frameSimulationStarved);
        }

        if (!_isFrameTimingMeasured)
        {
            return;
        }

        _frameTiming.Add(
            frameMilliseconds,
            updateMilliseconds,
            _frameDrawMilliseconds,
            _frameSimulationTicks,
            _frameSimulationStarved);

        if (!_frameTiming.TryTakeWindow(out var window))
        {
            return;
        }

        _log.Write(
            LogLevel.Debug,
            LogChannel.Render,
            LogEvents.RenderWindow,
            "frames",
            window.Frames,
            "elapsedMs",
            window.ElapsedMilliseconds,
            "meanMs",
            window.MeanMilliseconds,
            "worstMs",
            window.WorstMilliseconds,
            "worstUpdateMs",
            window.WorstUpdateMilliseconds,
            "worstDrawMs",
            window.WorstDrawMilliseconds,
            "simTicks",
            window.SimulationTicks);

        // Its own identifier rather than a field on the summary: the summary is
        // the routine reading and this is the finding, and a reader filtering
        // for trouble should not have to know which field of a healthy line to
        // test. At warn, because the simulation missing the wall clock is a
        // defect report rather than an observation, and it must survive a level
        // filter that drops the summary.
        if (window.StarvedFrames > 0)
        {
            _log.Write(
                LogLevel.Warning,
                LogChannel.Render,
                LogEvents.RenderStarved,
                "frames",
                window.Frames,
                "starvedFrames",
                window.StarvedFrames,
                "worstMs",
                window.WorstMilliseconds,
                "simTicks",
                window.SimulationTicks,
                "msg",
                "Simulation fell behind the wall clock; ticks were dropped.");
        }
    }

    private void LogTick()
    {
        var level = LogSampling.IsSampledTick(_simulation.Tick)
            ? LogLevel.Debug
            : LogLevel.Trace;
        if (!_log.IsEnabledFor(level, LogChannel.Simulation))
        {
            return;
        }

        var alive0 = 0;
        var alive1 = 0;
        var refusals = 0;
        foreach (var agent in _simulation.Agents)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            if (agent.FactionId == 0)
            {
                alive0++;
            }
            else
            {
                alive1++;
            }

            if (agent.FootworkPhase == FootworkPhase.Refuse)
            {
                refusals++;
            }
        }

        // The two movement fields ride the line only under a preset that
        // resolves footwork at all, so a legacy run's lines stay
        // byte-identical to the ones written before the fields existed.
        if (MovementPresetRegistry
            .Get(_simulation.Scenario.MovementPreset)
            .UsesEquipmentRelativeFootwork)
        {
            _log.Write(
                level,
                LogChannel.Simulation,
                LogEvents.SimTick,
                "tick",
                _simulation.Tick,
                "alive0",
                alive0,
                "alive1",
                alive1,
                "events",
                _simulation.LastEvents.Count,
                "stateHash",
                _simulation.ComputeStateHash().ToString(
                    "X16",
                    CultureInfo.InvariantCulture),
                "refusals",
                refusals,
                "conflictDenials",
                _simulation.MovementConflictDenials);
            return;
        }

        _log.Write(
            level,
            LogChannel.Simulation,
            LogEvents.SimTick,
            "tick",
            _simulation.Tick,
            "alive0",
            alive0,
            "alive1",
            alive1,
            "events",
            _simulation.LastEvents.Count,
            "stateHash",
            _simulation.ComputeStateHash().ToString(
                "X16",
                CultureInfo.InvariantCulture));
    }

    private void LogScenarioBuilt(string reason) =>
        _log.Write(
            LogLevel.Information,
            LogChannel.Simulation,
            LogEvents.SimScenarioBuilt,
            "seed",
            _scenario.Seed,
            "agents",
            _activeComposition.UnitsPerTeam * 2,
            "mapWidth",
            _scenario.MapWidth,
            "mapHeight",
            _scenario.MapHeight,
            "grassClusters",
            _grassClusters.Length,
            "reason",
            reason);

    private void CompleteMatch()
    {
        _presentation.ProcessTerminal(
            _simulation.Outcome,
            _simulation.Agents,
            _simulation.Tick,
            _scenario.TickRate,
            _scenario.Seed);
        _simulationAccumulator = 0;

        if (_loggedOutcomeTick == _simulation.Tick)
        {
            return;
        }

        _loggedOutcomeTick = _simulation.Tick;
        _log.Write(
            LogLevel.Information,
            LogChannel.Simulation,
            LogEvents.SimOutcome,
            "outcome",
            _simulation.Outcome.ToString(),
            "tick",
            _simulation.Tick,
            "seed",
            _scenario.Seed,
            "stateHash",
            _simulation.ComputeStateHash().ToString(
                "X16",
                CultureInfo.InvariantCulture));
    }

    private void ResetSimulation(ClientCommand resetCommand)
    {
        if (resetCommand == ClientCommand.FullReset)
        {
            _matchSeries.FullReset();
            _activeComposition =
                _settingsStore.Load(_themeManager.ActiveTheme.Id).Composition;
            _isCompositionStaged = false;
        }
        else if (resetCommand == ClientCommand.NextRound)
        {
            _matchSeries.StartNextRound(_simulation.Outcome);
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                nameof(resetCommand),
                resetCommand,
                "Only round reset commands can reset the simulation.");
        }

        _scenario = BuildScenario(_matchSeries.CurrentSeed, _activeComposition);
        _simulation = BattleSimulation.Create(_scenario);
        _loggedOutcomeTick = -1;

        // A round boundary is not one continuous run of frames: the frames that
        // build a scenario would otherwise be averaged into the new round's
        // first window and hide its real opening cost.
        _frameTiming.Reset();
        _log.SetTick(DiagnosticLog.NoTick);
        _log.Write(
            LogLevel.Information,
            LogChannel.Simulation,
            LogEvents.SimReset,
            "kind",
            resetCommand.ToString(),
            "seed",
            _matchSeries.CurrentSeed);
        _plainsDecals = PlainsBackdropGeometry.GenerateDecals(
            _scenario.Seed,
            _scenario.MapWidth,
            _scenario.MapHeight);
        _grassClusters = GrassGeometry.GenerateClusters(
            _scenario.Seed,
            _scenario.MapWidth,
            _scenario.MapHeight);
        LogScenarioBuilt(resetCommand.ToString());
        _presentation.ResetFor(resetCommand);
        _presentation.EventFeed.SetScenarioSeed(_scenario.Seed);
        _soundDirector.Clear();
        _hoverSelection.Clear();
        _simulationAccumulator = 0;
        _menu.Close();
        _autoPan.Reset();

        if (resetCommand == ClientCommand.FullReset)
        {
            _speedMultiplier = 1;
            _camera = new SpectatorCamera(
                _scenario.MapWidth,
                _scenario.MapHeight);
            _camera.Fit(GetLayout(GraphicsDevice.Viewport.Bounds).ArenaBounds);
        }
    }

    private void UpdateHoverSelection(Rectangle arenaBounds)
    {
        if (!arenaBounds.Contains(_input.MousePosition))
        {
            _hoverSelection.Clear();
            return;
        }

        SelectAtPointer(_hoverSelection, arenaBounds);
    }

    private void SelectAtPointer(
        AgentSelection selection,
        Rectangle arenaBounds)
    {
        var mouseWorld = _camera.ScreenToWorld(
            _input.MousePosition,
            arenaBounds);
        var pointerXRaw = ToRawCoordinate(mouseWorld.X);
        var pointerYRaw = ToRawCoordinate(mouseWorld.Y);
        var pickRadius = MathF.Max(5f / _camera.Zoom, 1.5f);
        var pickRadiusRaw = checked(
            (long)Math.Ceiling(pickRadius * FixedPoint.Scale));
        var maximumDistanceSquared = checked(pickRadiusRaw * pickRadiusRaw);

        selection.SelectNearest(
            _simulation.Agents,
            pointerXRaw,
            pointerYRaw,
            maximumDistanceSquared);
    }

    private void UpdateWindowTitle()
    {
        Window.Title =
            $"Hukbo — A {_matchSeries.TeamAWins} : {_matchSeries.TeamBWins} B — " +
            $"Seed {_matchSeries.CurrentSeed} — Tick {_simulation.Tick:N0} — " +
            $"{_speedMultiplier}x — " +
            $"{(_presentation.Playback.IsPlaying ? "Playing" : "Paused")} — " +
            _simulation.Outcome;
    }

    private ClientLayout GetLayout(Rectangle screenBounds) =>
        ComputeLayout(screenBounds, _isSoundLogVisible);

    /// <summary>
    /// Screen partitioning. The right column's split between the battle event
    /// log and the sound log is delegated to <see cref="RightColumnSplit"/>,
    /// which the client tests exercise directly.
    /// </summary>
    private static ClientLayout ComputeLayout(
        Rectangle screenBounds,
        bool isSoundLogVisible)
    {
        var contentTop = Math.Min(
            screenBounds.Bottom,
            screenBounds.Top + StatusBarHeight);
        var contentHeight = Math.Max(
            0,
            screenBounds.Bottom - contentTop - LayoutMargin);
        var eventWidth = Math.Min(
            EventPanelWidth,
            Math.Max(0, screenBounds.Width / 3));
        var column = RightColumnSplit.Split(
            new Rectangle(
                Math.Max(
                    screenBounds.Left,
                    screenBounds.Right - eventWidth - LayoutMargin),
                contentTop,
                eventWidth,
                contentHeight),
            isSoundLogVisible,
            SoundLogMinimumHeight,
            SoundLogHeightPercent,
            LayoutGap);
        var eventBounds = column.EventBounds;
        var soundLogBounds = column.SoundLogBounds;
        var arenaRight = Math.Max(
            screenBounds.Left + LayoutMargin,
            eventBounds.Left - LayoutGap);
        var arenaBounds = new Rectangle(
            screenBounds.Left + LayoutMargin,
            contentTop,
            Math.Max(
                0,
                arenaRight - screenBounds.Left - LayoutMargin),
            contentHeight);
        var inspectorWidth = Math.Min(
            InspectorWidth,
            Math.Max(0, arenaBounds.Width - (LayoutMargin * 2)));
        var inspectorHeight = Math.Min(
            InspectorHeight,
            Math.Max(0, arenaBounds.Height - (LayoutMargin * 2)));
        var inspectorBounds = new Rectangle(
            arenaBounds.Left + LayoutMargin,
            Math.Max(
                arenaBounds.Top + LayoutMargin,
                arenaBounds.Bottom - inspectorHeight - LayoutMargin),
            inspectorWidth,
            inspectorHeight);

        return new ClientLayout(
            arenaBounds,
            eventBounds,
            soundLogBounds,
            inspectorBounds);
    }

    private static int ToRawCoordinate(float worldCoordinate)
    {
        var scaled = Math.Round(
            (double)worldCoordinate * FixedPoint.Scale,
            MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(
            scaled,
            -MaximumSafeRawCoordinate,
            MaximumSafeRawCoordinate);
    }

    private readonly record struct ClientLayout(
        Rectangle ArenaBounds,
        Rectangle EventBounds,
        Rectangle SoundLogBounds,
        Rectangle InspectorBounds);
}
