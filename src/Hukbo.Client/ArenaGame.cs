using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Hukbo.Client.Audio;
using Hukbo.Client.Diagnostics;
using Hukbo.Client.Presentation;
using Hukbo.Client.Presentation.Catalogs;
using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Hukbo.Core.Combat;
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
    // The default window is 1600x900, not the more common 1280x720, because
    // the smaller size draws no pawn legs at all. ComputeLayout gives a
    // 1600x900 window an arena panel of 1146x820; SpectatorCamera.Fit takes
    // the smaller-axis fit against a 1280x720 map, horizontalZoom =
    // 1146 * 0.88 / 1280 = 0.7879, which wins over the vertical axis;
    // PawnGeometry.ResolveApparentScale then multiplies by ZoomScale = 1.35
    // to reach apparentScale = 0.7879 * 1.35 = 1.0637. That clears
    // MediumDetailScale = 0.95, so the default view resolves
    // PawnDetailTier.Medium and CreateLegsAndFeet draws legs and feet
    // instead of the four empty rectangles it returns at PawnDetailTier.Low.
    private const int InitialWindowWidth = 1600;
    private const int InitialWindowHeight = 900;
    private const int MinimumWindowWidth = 1024;
    private const int MinimumWindowHeight = 720;

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
    private static int InspectorHeight =>
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

    /// <summary>
    /// Surface E — status-badge emphasis (UI-52). Observed once per frame in
    /// <see cref="Update"/>, unconditionally, so a frame where the pointer is
    /// consumed elsewhere still decays the pulse; read by <see cref="DrawStatus"/>.
    /// </summary>
    private readonly UiStatusBadgeMotion _statusBadgeMotion = new();
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
    private readonly string _defaultThemeId;
    private UiScale _configuredUiScale;
    private StartupDisplayMode _startupDisplayMode;
    private UiChromeStyle _configuredUiChromeStyle;
    private PawnVisualStyle _configuredPawnVisualStyle;

    /// <summary>
    /// Reused each frame so the draw path allocates nothing. Contacts are
    /// resolved after simulation ingestion, so the pose consumed by Draw is
    /// from the same frame as its authoritative event.
    /// </summary>
    private readonly Dictionary<ulong, AttackPose> _attackPoses = [];

    /// <summary>
    /// Reused each frame, mirroring <see cref="_attackPoses"/> exactly. The
    /// mapping into it lives in <see cref="GaitPoseResolver"/>; unlike the
    /// swing poses it is never scaled by playback speed, because
    /// <see cref="GaitAnimationSystem"/>'s phase already advances by distance
    /// travelled per ingested tick rather than by elapsed seconds
    /// (movement-gait-animation-design.md section 4).
    /// </summary>
    private readonly Dictionary<ulong, GaitPose> _gaitPoses = [];

    /// <summary>
    /// Reused each frame, mirroring <see cref="_swingPoses"/> and
    /// <see cref="_gaitPoses"/>. The mapping into it lives in
    /// <see cref="RangedPoseResolver"/>, which needs no animation store of its
    /// own: <see cref="Hukbo.Core.Simulation.AgentView.RangedPhase"/> and
    /// <see cref="Hukbo.Core.Simulation.AgentView.RangedPhaseTicksRemaining"/>
    /// already arrive derived on every tick's agent views.
    /// </summary>
    private readonly Dictionary<ulong, RangedPose> _rangedPoses = [];
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
    private Texture2D? _chromeAtlas;
    private Texture2D? _pawnBodyAtlas;
    private UiFontSet? _fonts;
    private MonoGameSoundPlayer? _soundPlayer;
    private Settings.ArmyComposition _activeComposition;
    private MovementPresetId _activeMovementPreset;
    private bool _isSoundLogVisible;
    private bool _isEventLogVisible;
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
        _defaultThemeId = catalog.DefaultThemeId;
        _settingsStore = ClientSettingsStore.CreateDefault(_log);
        var initialSettings = _settingsStore.Load(catalog.DefaultThemeId);
        _themeManager = new UiThemeManager(catalog, _settingsStore);
        _goreManager = new GoreIntensityManager(
            initialSettings.GoreIntensity,
            value => TryPersistGoreIntensity(catalog.DefaultThemeId, value));
        _motionManager = new MotionIntensityManager(
            initialSettings.MotionIntensity,
            value => TryPersistMotionIntensity(catalog.DefaultThemeId, value));
        _autoCameraManager = new AutoCameraModeManager(
            initialSettings.AutoCameraMode,
            value => TryPersistAutoCameraMode(catalog.DefaultThemeId, value));
        _configuredUiScale = initialSettings.UiScale;
        _startupDisplayMode = initialSettings.StartupDisplayMode;
        _configuredUiChromeStyle = initialSettings.UiChromeStyle;
        _configuredPawnVisualStyle = initialSettings.PawnVisualStyle;

        // Resolved here, ahead of the coordinator below, because the
        // coordinator's appearance cache reports through it. _renderProbeEnabled
        // is a field initializer, so it is already settled by this point, and
        // moving this assignment earlier in the same constructor changes
        // nothing about what a normal run gets: NullRenderMetricsRecorder,
        // whose every call is a no-op.
        _renderMetricsRecorder = _renderProbeEnabled
            ? new SpriteBatchRenderMetricsRecorder()
            : NullRenderMetricsRecorder.Instance;

        // Computed ahead of the scenario field below, purely to size the
        // coordinator's projectile store from the real
        // Scenario.MaximumProjectilesInFlight rather than a duplicated
        // literal. initialSettings.Composition is exactly what
        // _activeComposition becomes two statements below, so this mirrors
        // the same BuildScenario call that produces _scenario itself; the
        // repeated call is a pure, allocation-cheap read, not a second
        // source of truth.
        var startupScenarioForCapacity = scenarioOverride ??
            BuildScenario(
                _matchSeries.CurrentSeed,
                initialSettings.Composition,
                initialSettings.MovementPreset);
        _presentation = new PresentationCoordinator(
            EventHistoryCapacity,
            projectileCapacity: startupScenarioForCapacity.MaximumProjectilesInFlight,
            renderMetricsRecorder: _renderMetricsRecorder);

        // A restored preference takes effect from tick zero, so the spectator
        // never has to reopen the menu after a relaunch.
        _presentation.Blood.Intensity = _goreManager.Value;
        _presentation.Dust.MotionIntensity = _motionManager.Value;
        _menu = new MenuOverlay(catalog.Themes, catalog.Standards);
        _activeComposition = initialSettings.Composition;
        _activeMovementPreset = initialSettings.MovementPreset;
        _armyCompositionPanel = new ArmyCompositionPanel(
            ToPanelComposition(_activeComposition),
            _activeMovementPreset,
            catalog.Standards.Shared.ArmyComposition,
            catalog.Standards);

        var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var startupGraphics = StartupDisplayPolicy.Resolve(
            _startupDisplayMode,
            InitialWindowWidth,
            InitialWindowHeight,
            displayMode.Width,
            displayMode.Height,
            _renderProbeEnabled,
            RenderProbeWindowWidth,
            RenderProbeWindowHeight);
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = startupGraphics.BackBufferWidth,
            PreferredBackBufferHeight = startupGraphics.BackBufferHeight,
            SynchronizeWithVerticalRetrace = true,
            HardwareModeSwitch = startupGraphics.HardwareModeSwitch,
            IsFullScreen = startupGraphics.IsFullScreen,
        };

        Window.AllowUserResizing = true;
        Window.IsBorderless = true;
        Window.ClientSizeChanged += OnClientSizeChanged;
        Window.Title = "Hukbo";
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = false;

        _scenario = scenarioOverride ??
            BuildScenario(
                _matchSeries.CurrentSeed,
                _activeComposition,
                _activeMovementPreset);
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
    /// Starts battle playback directly, bypassing the spectator's own play
    /// control. No-op unless the render-probe opt-in is active, so normal
    /// startup is unchanged: a launched client still opens paused, exactly as
    /// it does today.
    /// </summary>
    /// <remarks>
    /// Attack-animation-v2, task 10. The probe used to measure a paused
    /// battle, where no warrior ever reaches another and no attack pose is
    /// ever held, so every station's window described the neutral pawn path
    /// and none of them described the articulated attack path this task exists
    /// to bound. This starts the same authoritative simulation the spectator
    /// would start by pressing play; it synthesizes no Core event, alters no
    /// cadence, and touches nothing outside the probe's own opt-in.
    /// </remarks>
    public void SetProbePlaybackStarted()
    {
        if (_renderProbeEnabled)
        {
            _presentation.Playback.Play();
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
        GoreIntensity value) =>
        _settingsStore.TryUpdate(
            defaultThemeId,
            current => current with
            {
                SelectedThemeId = _themeManager.ActiveTheme.Id,
                GoreIntensity = value,
            });

    /// <summary>
    /// Mirrors <see cref="TryPersistGoreIntensity"/> for the motion setting:
    /// re-reads the whole settings file at save time so a motion-level write
    /// carries forward the theme, composition, and gore level unchanged.
    /// </summary>
    private bool TryPersistMotionIntensity(
        string defaultThemeId,
        MotionIntensity value) =>
        _settingsStore.TryUpdate(
            defaultThemeId,
            current => current with
            {
                SelectedThemeId = _themeManager.ActiveTheme.Id,
                MotionIntensity = value,
            });

    /// <summary>
    /// Mirrors <see cref="TryPersistGoreIntensity"/> for the camera-assistant
    /// setting: re-reads the whole settings file at save time so a mode write
    /// carries forward every sibling field unchanged.
    /// </summary>
    private bool TryPersistAutoCameraMode(
        string defaultThemeId,
        AutoCameraMode value) =>
        _settingsStore.TryUpdate(
            defaultThemeId,
            current => current with
            {
                SelectedThemeId = _themeManager.ActiveTheme.Id,
                AutoCameraMode = value,
            });

    private bool TryPersistUiScale(string defaultThemeId, UiScale value) =>
        _settingsStore.TryUpdate(
            defaultThemeId,
            current => current with
            {
                SelectedThemeId = _themeManager.ActiveTheme.Id,
                UiScale = value,
            });

    private bool TryPersistStartupDisplayMode(
        string defaultThemeId,
        StartupDisplayMode value) =>
        _settingsStore.TryUpdate(
            defaultThemeId,
            current => current with
            {
                SelectedThemeId = _themeManager.ActiveTheme.Id,
                StartupDisplayMode = value,
            });

    private bool TryPersistUiChromeStyle(
        string defaultThemeId,
        UiChromeStyle value) =>
        _settingsStore.TryUpdate(
            defaultThemeId,
            current => current with
            {
                SelectedThemeId = _themeManager.ActiveTheme.Id,
                UiChromeStyle = value,
            });

    /// <summary>
    /// Flips the warrior body between the procedural quads and the authored
    /// sprite cells, and persists the result so the choice survives a restart.
    /// </summary>
    /// <remarks>
    /// Takes effect on the very next frame, because
    /// <c>ArenaGame.Rendering</c> reads the field on every pawn it submits.
    /// That is the point of a live toggle rather than a startup switch: the
    /// two styles can be compared against the same battle, at the same tick,
    /// without relaunching.
    /// </remarks>
    private void ToggleWarriorBodyStyle()
    {
        var previous = _configuredPawnVisualStyle;
        _configuredPawnVisualStyle =
            previous == PawnVisualStyle.SpriteBody
                ? PawnVisualStyle.Procedural
                : PawnVisualStyle.SpriteBody;
        TryPersistPawnVisualStyle(
            _defaultThemeId,
            _configuredPawnVisualStyle);
        LogSettingChanged(
            "pawnVisualStyle",
            previous.ToString(),
            _configuredPawnVisualStyle.ToString());
    }

    private bool TryPersistPawnVisualStyle(
        string defaultThemeId,
        PawnVisualStyle value) =>
        _settingsStore.TryUpdate(
            defaultThemeId,
            current => current with
            {
                SelectedThemeId = _themeManager.ActiveTheme.Id,
                PawnVisualStyle = value,
            });

    protected override void Initialize()
    {
        base.Initialize();
        if (!_graphics.IsFullScreen && !_renderProbeEnabled)
        {
            SDL_SetWindowMinimumSize(
                Window.Handle,
                MinimumWindowWidth,
                MinimumWindowHeight);
        }
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
            _fonts.SelectScale(
                _configuredUiScale,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height);
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

        // Loaded once here, never per frame: the nine-slice chrome atlas
        // described on UiNineSlice, holding the surface and border regions
        // every panel-style call site tints at draw time.
        _chromeAtlas = Content.Load<Texture2D>("Textures/UiChrome");

        // Loaded once here on the same terms as the chrome atlas above: the
        // 50 authored head-and-torso cells PawnSpriteAtlas indexes, drawn only
        // under PawnVisualStyle.SpriteBody and tinted per warrior at draw time.
        _pawnBodyAtlas = Content.Load<Texture2D>("Textures/PawnBodies");

        _soundPlayer = MonoGameSoundPlayer.Load(
            SoundLibrary.GetDefaultDirectoryPath());
        _soundDirector.AttachPlayer(_soundPlayer);

        LogViewport(LogEvents.BootWindowCreated, LogChannel.Boot);

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
        var screenBounds = GraphicsDevice.Viewport.Bounds;
        _fonts?.SelectScale(
            _configuredUiScale,
            screenBounds.Width,
            screenBounds.Height);
        var layout = GetLayout(screenBounds);
        if (_isEventLogVisible)
        {
            _eventLogPanel.ReleaseKeyboardFocusIfPointerLeaves(
                _input,
                layout.EventBounds);
        }

        // A hidden panel never held keyboard focus in the first place, so it
        // must never claim to have consumed Escape either.
        var eventEscapeConsumed =
            _isEventLogVisible &&
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
            var promptInteraction = _quitPrompt.Update(
                _input,
                screenBounds,
                gameTime.ElapsedGameTime,
                _motionManager.Value);
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
                screenBounds,
                gameTime.ElapsedGameTime,
                _motionManager.Value);
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
                _autoCameraManager.Value,
                _configuredUiScale,
                _startupDisplayMode,
                _configuredUiChromeStyle,
                gameTime.ElapsedGameTime);
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

            if (menuInteraction.SelectedUiScale is { } selectedUiScale &&
                selectedUiScale != _configuredUiScale)
            {
                var previousUiScale = _configuredUiScale;
                _configuredUiScale = selectedUiScale;
                TryPersistUiScale(_defaultThemeId, selectedUiScale);
                _fonts?.SelectScale(
                    _configuredUiScale,
                    screenBounds.Width,
                    screenBounds.Height);
                LogSettingChanged(
                    "uiScale",
                    previousUiScale.ToString(),
                    _configuredUiScale.ToString());
            }

            if (menuInteraction.SelectedStartupDisplayMode is
                { } selectedDisplayMode &&
                selectedDisplayMode != _startupDisplayMode)
            {
                var previousDisplayMode = _startupDisplayMode;
                _startupDisplayMode = selectedDisplayMode;
                TryPersistStartupDisplayMode(
                    _defaultThemeId,
                    selectedDisplayMode);
                LogSettingChanged(
                    "startupDisplay",
                    previousDisplayMode.ToString(),
                    _startupDisplayMode.ToString());
            }

            if (menuInteraction.SelectedUiChromeStyle is
                { } selectedUiChromeStyle &&
                selectedUiChromeStyle != _configuredUiChromeStyle)
            {
                var previousUiChromeStyle = _configuredUiChromeStyle;
                _configuredUiChromeStyle = selectedUiChromeStyle;
                TryPersistUiChromeStyle(_defaultThemeId, selectedUiChromeStyle);
                LogSettingChanged(
                    "uiChromeStyle",
                    previousUiChromeStyle.ToString(),
                    _configuredUiChromeStyle.ToString());
            }

            ApplyClientCommand(menuInteraction.Command);
        }
        else
        {
            var interaction = _battleReportPanel.Update(
                _input,
                _isBattleReportVisible ? _presentation.Report : null,
                layout.ArenaBounds,
                gameTime.ElapsedGameTime,
                _motionManager.Value);
            pointerConsumed = interaction.PointerConsumed;
            consumedBy = pointerConsumed ? "battleReport" : consumedBy;

            if (!pointerConsumed)
            {
                interaction = _summaryPanel.Update(
                    _input,
                    _presentation.Summary,
                    layout.ArenaBounds,
                    gameTime.ElapsedGameTime,
                    _motionManager.Value);
                pointerConsumed = interaction.PointerConsumed;
                consumedBy = pointerConsumed ? "matchSummary" : consumedBy;
            }

            if (!pointerConsumed)
            {
                interaction = _controlBar.Update(
                    _input,
                    screenBounds,
                    _presentation.Playback.IsPlaying,
                    _isSoundLogVisible,
                    _isEventLogVisible,
                    gameTime.ElapsedGameTime,
                    _motionManager.Value);
                pointerConsumed = interaction.PointerConsumed;
                consumedBy = pointerConsumed ? "controlBar" : consumedBy;
            }

            if (!pointerConsumed && _isEventLogVisible)
            {
                interaction = _eventLogPanel.Update(
                    _input,
                    _presentation.EventFeed,
                    layout.EventBounds,
                    gameTime.ElapsedGameTime,
                    _motionManager.Value);
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
                    layout.InspectorBounds,
                    gameTime.ElapsedGameTime,
                    _motionManager.Value);
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

        // Unconditional: must decay every frame even when a higher-priority
        // surface consumed the pointer above, so the badge never stalls
        // mid-pulse (UI-52 surface E).
        _statusBadgeMotion.Observe(
            _simulation.Outcome,
            _presentation.Playback.IsPlaying,
            gameTime.ElapsedGameTime,
            _motionManager.Value);

        LogPointer(consumedBy);
        LogFocusChange();

        _presentation.AdvanceEffects(
            (float)gameTime.ElapsedGameTime.TotalSeconds,
            _speedMultiplier,
            advanceContacts: _presentation.Playback.IsPlaying);
        AdvanceSimulation(gameTime.ElapsedGameTime.TotalSeconds);
        _presentation.ReleaseAttackContactsForDraw(
            _simulation.Agents,
            _motionManager.Value,
            _soundDirector,
            allowRelease: _presentation.Playback.IsPlaying);

        // After the release above, never before it: a lethal contact released
        // this frame registers its defender reaction there, and the collapse
        // reads that reaction for the direction the killing blow pushed the
        // body (the 2026-08-14 death-collapse design, section 7).
        _presentation.ObserveDeaths(_simulation.Agents);

        _attackPoses.Clear();
        var activeAttacks = _presentation.AttackAnimations.ActiveAnimations;
        for (var index = 0; index < activeAttacks.Length; index++)
        {
            var attack = activeAttacks[index];
            _attackPoses[attack.AttackerEntityId] =
                AttackPoseResolver.Resolve(attack);
        }

        GaitPoseResolver.Resolve(
            _presentation.Gait,
            _simulation.Agents,
            _motionManager.Value,
            _gaitPoses);

        // RU-25. Moved here by the 2026-08-09 merge: this call used to sit
        // beside SwingPoseResolver in the block the attack-animation-v2
        // migration relocated. Swings became AttackFrames and their resolver
        // is gone, but the ranged pose is a separate channel that migration
        // never touched, so it follows the block rather than the resolver.
        RangedPoseResolver.Resolve(_simulation.Agents, _rangedPoses);

        if (_presentation.Playback.IsPlaying &&
            _simulation.Outcome != BattleOutcome.Ongoing &&
            !_presentation.HasTerminalAttackPresentation)
        {
            CompleteMatch();
        }

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

        if (_input.WasPressed(Keys.F8))
        {
            return LogKeyCommand("F8", ClientCommand.ToggleEventLog);
        }

        if (_input.WasPressed(Keys.B))
        {
            return LogKeyCommand("B", ClientCommand.ToggleWarriorBody);
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
            case ClientCommand.ToggleEventLog:
                _isEventLogVisible = !_isEventLogVisible;
                return;
            case ClientCommand.ToggleBattleReport:
                _isBattleReportVisible = !_isBattleReportVisible;
                return;
            case ClientCommand.ToggleWarriorBody:
                ToggleWarriorBodyStyle();
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
        var saved = _settingsStore.Load(_themeManager.ActiveTheme.Id);
        _armyCompositionPanel.Open(
            ToPanelComposition(saved.Composition),
            saved.MovementPreset);
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
                _settingsStore.TryUpdate(
                    _themeManager.ActiveTheme.Id,
                    current => current with
                    {
                        SelectedThemeId = _themeManager.ActiveTheme.Id,
                        Composition = ToSettingsComposition(
                            _armyCompositionPanel.Saved),
                        MovementPreset = _armyCompositionPanel.SavedMovementPreset,
                    });
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

    // RU-25: the client's own scenario, not Scenario.CreateDefault's shipped
    // default, is what carries the ranged package: PrecolonialPhilippinesV5
    // is the only combat preset with ranged attack rules registered. It is
    // now paired with BattlefieldRealismV10, which inherits the V8 ranged
    // standoff rule that keeps a holding archer from being walked in on by
    // its own melee comrades and adds weapon-cohort deployment, shield
    // bearers placed at the forward-most slots of their own contingent, and
    // a ranged warrior backing away from closing melee. All three are a
    // labelled gameplay model, not a historical claim — see
    // the battlefield realism design. CreateDefault
    // stays V4/V4 so the headless determinism baseline and every other
    // caller are unaffected.
    //
    // RU-43: RosterCounts is filled again, through
    // ExpandCompositionToRosterCounts rather than the flat
    // ToRosterCounts(composition) RU-25 could no longer use. V5's roster
    // (now nine rows — RU-45 appended two shielded melee rows after RU-25
    // was written) fields more than one row for Timawa and for Aliping
    // Namamahay, while Settings.ArmyComposition still carries exactly one
    // slider per rank; ExpandCompositionToRosterCounts spreads each rank's
    // slider count across every roster row that carries that rank, so the
    // sliders move real warriors again instead of being read and discarded.
    //
    // The movement preset moved on again, to LastStandEngagementV11,
    // which restates every one of V10's registered field values and adds the
    // two last-stand regroup yields. The tester's finding was made against the
    // shipped build, so the shipped build is what has to change: a follower
    // whose rally agent is already fighting, or who has an enemy inside its own
    // weapon reach, now closes on that enemy rather than parking 51 world units
    // behind its leader. Every preset from V1 to V10 stays registered and
    // byte-identical for a replay that names one of them. See the archived
    // document titled "Last-stand engagement — plan".
    //
    // The combat preset moved again, to PrecolonialPhilippinesV7, the shield
    // size against projectile size design's combat preset: V6 carried across
    // unchanged, plus the NarrowBreastHigh shield roster entries, its
    // weapon-intercept and void table rows, its target-weight profile, and
    // the per-shield interception, span, and per-weapon bulk tables. Without
    // this move the new shield is registered in Hukbo.Core but never fielded
    // by the only build a spectator ever runs, which fails design section
    // 8's "shipped defaults" requirement outright. V1 through V6 stay
    // registered and byte-identical for a replay that names one of them.
    private static Scenario BuildScenario(
        ulong seed,
        Settings.ArmyComposition composition,
        MovementPresetId movementPreset)
    {
        var scenario = Scenario.CreateDefault(seed, composition.UnitsPerTeam * 2) with
        {
            CombatPreset = CombatPresetId.PrecolonialPhilippinesV7,
            MovementPreset = movementPreset,
        };

        var rules = CombatPresetRegistry.Get(scenario.CombatPreset);
        return scenario with
        {
            RosterCounts = ExpandCompositionToRosterCounts(
                rules.Roster,
                composition),
        };
    }

    /// <summary>
    /// RU-43's fix for the inert composition sliders: expands the
    /// spectator's four rank-count sliders (<see cref="RankId.Datu"/>,
    /// <see cref="RankId.Maharlika"/>, <see cref="RankId.Timawa"/>,
    /// <see cref="RankId.AlipingNamamahay"/>) into one count per
    /// <paramref name="roster"/> row, in <paramref name="roster"/> order, so
    /// the result can always be assigned straight to
    /// <see cref="Scenario.RosterCounts"/>: its length always equals
    /// <paramref name="roster"/>.Count, and its sum always equals
    /// <paramref name="composition"/>.UnitsPerTeam, which is what
    /// <see cref="Scenario.Validate"/> requires.
    /// <para>
    /// Combat preset V4 fields exactly one roster row per rank, so every
    /// rank's row group has exactly one member and its slider count passes
    /// through unchanged — today's behavior, preserved exactly. Combat
    /// preset V7 restates V6's four-row roster unchanged (one row each for
    /// Datu, Maharlika, Timawa, and Aliping Namamahay, none of them
    /// shielded or ranged) and adds two narrow-breast-high-shield rows:
    /// Kalis under Timawa and Itak under Aliping Namamahay. Timawa's row
    /// group therefore splits Kalis (solo) against Kalis + narrow shield,
    /// and Aliping Namamahay's splits Itak (solo) against Itak + narrow
    /// shield; those rows split by
    /// <see cref="CalibratedRosterEntryWeights"/> using the same
    /// largest-remainder apportionment
    /// <c>RangedCalibrationHarness.BuildRosterCounts</c> uses for the RU-24
    /// and RU-45 calibration matrix, restated in
    /// <see cref="ApportionByLargestRemainder"/> because Hukbo.Client cannot
    /// reference the Core test project. Splitting evenly instead of by that
    /// calibrated weight would change V5's measured ranged share, which is
    /// exactly the failure this method exists to avoid.
    /// </para>
    /// </summary>
    internal static ImmutableArray<int> ExpandCompositionToRosterCounts(
        IReadOnlyList<CombatLoadout> roster,
        Settings.ArmyComposition composition)
    {
        var rankTargets = new Dictionary<RankId, int>
        {
            [RankId.Datu] = composition.DatuCount,
            [RankId.Maharlika] = composition.MaharlikaCount,
            [RankId.Timawa] = composition.TimawaCount,
            [RankId.AlipingNamamahay] = composition.AlipingNamamahayCount,
        };

        var result = new int[roster.Count];
        foreach (var (rank, targetCount) in rankTargets)
        {
            var rankIndices = new List<int>();
            var rankWeights = new List<int>();
            for (var index = 0; index < roster.Count; index++)
            {
                if (roster[index].Rank != rank)
                {
                    continue;
                }

                rankIndices.Add(index);
                rankWeights.Add(ResolveRosterEntryWeight(roster[index]));
            }

            if (rankIndices.Count == 0)
            {
                continue;
            }

            var apportioned = ApportionByLargestRemainder(
                targetCount,
                rankWeights);
            for (var slot = 0; slot < rankIndices.Count; slot++)
            {
                result[rankIndices[slot]] = apportioned[slot];
            }
        }

        for (var index = 0; index < roster.Count; index++)
        {
            if (!rankTargets.ContainsKey(roster[index].Rank))
            {
                throw new InvalidOperationException(
                    $"Roster row {index} carries {roster[index].Rank}, " +
                    $"which has no {nameof(Settings.ArmyComposition)} " +
                    "slider. Widen the composition record before rostering " +
                    "that rank.");
            }
        }

        return [.. result];
    }

    /// <summary>
    /// RU-24/RU-45's calibrated share weights, keyed by weapon and shield
    /// since rank alone does not distinguish Bangkaw from Busog from
    /// Arquebus, or a solo Kalis from a shielded one. This table was
    /// originally calibrated against combat preset V5's nine-row roster,
    /// which fields ranged weapons and tall-hardwood-shield melee rows that
    /// V6 and V7 do not carry: V6's and V7's roster is a separate, shorter
    /// lineage descended from V4, not V5, so the Bangkaw, Busog, Arquebus,
    /// and tall-hardwood-shield entries below are not looked up by
    /// <see cref="ExpandCompositionToRosterCounts"/> under the shipped V7
    /// preset — that method only apportions across
    /// <c>rules.Roster</c>'s actual rows, and V7's roster has none of those
    /// four kinds. They are kept, inert, for a build still naming V5, and a
    /// (weapon, shield) pair not listed here falls back to a weight of 1 in
    /// <see cref="ResolveRosterEntryWeight"/>.
    /// <para>
    /// The shield size against projectile size design's section 8 added the
    /// <see cref="ShieldId.NarrowBreastHigh"/> rows for Kalis and Itak so the
    /// new shield is actually fielded by the only build a spectator ever
    /// runs — a shield nobody carries cannot be discovered by watching.
    /// Under V7 those two rows are the only shielded rows the roster
    /// carries at all, since V7 has no tall-hardwood-shield row to draw
    /// from; the weight below still subtracts 3 from each of Kalis's and
    /// Itak's tall-shield entries (9 to 6) to seed the corresponding
    /// narrow-shield entry, preserving this table's V5-calibrated total of
    /// 100 across its eleven listed (weapon, shield) pairs even though only
    /// six of those pairs are reachable under the roster the client
    /// actually ships. Design section 8's "shipped defaults" narrative
    /// assumes V5's ranged-and-tall-shield roster carries into V7; the
    /// registered <see cref="PhilippineCombatPresetV7"/> roster does not
    /// support that, and correcting it is outside this file's scope.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<(WeaponId Weapon, ShieldId Shield), int>
        CalibratedRosterEntryWeights =
            new Dictionary<(WeaponId Weapon, ShieldId Shield), int>
            {
                [(WeaponId.Kampilan, ShieldId.None)] = 19,
                [(WeaponId.Wasay, ShieldId.None)] = 19,
                [(WeaponId.Kalis, ShieldId.None)] = 10,
                [(WeaponId.Itak, ShieldId.None)] = 9,
                [(WeaponId.Bangkaw, ShieldId.None)] = 11,
                [(WeaponId.Busog, ShieldId.None)] = 8,
                [(WeaponId.Arquebus, ShieldId.None)] = 6,
                [(WeaponId.Kalis, ShieldId.TallHardwood)] = 6,
                [(WeaponId.Itak, ShieldId.TallHardwood)] = 6,
                [(WeaponId.Kalis, ShieldId.NarrowBreastHigh)] = 3,
                [(WeaponId.Itak, ShieldId.NarrowBreastHigh)] = 3,
            };

    private static int ResolveRosterEntryWeight(CombatLoadout entry) =>
        CalibratedRosterEntryWeights.TryGetValue(
            (entry.Weapon, entry.Shield),
            out var weight)
            ? weight
            : 1;

    /// <summary>
    /// Largest-remainder apportionment of <paramref name="total"/> across
    /// <paramref name="weights"/>: the result always sums to exactly
    /// <paramref name="total"/> regardless of rounding, which
    /// <see cref="Scenario.Validate"/> requires of
    /// <see cref="Scenario.RosterCounts"/>. Same method
    /// <c>tests/Hukbo.Core.Tests/RangedCalibrationHarness.cs</c>'s
    /// <c>BuildRosterCounts</c> uses for the RU-24 calibration matrix,
    /// restated here because Hukbo.Client cannot reference the Core test
    /// project.
    /// </summary>
    private static ImmutableArray<int> ApportionByLargestRemainder(
        int total,
        IReadOnlyList<int> weights)
    {
        var totalWeight = weights.Sum();
        if (totalWeight <= 0)
        {
            throw new ArgumentException(
                "weights must sum to a positive total.",
                nameof(weights));
        }

        var counts = new int[weights.Count];
        var remainders = new double[weights.Count];
        var assigned = 0;

        for (var index = 0; index < weights.Count; index++)
        {
            var exact = (double)total * weights[index] / totalWeight;
            counts[index] = (int)Math.Floor(exact);
            remainders[index] = exact - counts[index];
            assigned += counts[index];
        }

        var remaining = total - assigned;
        // Largest-remainder-first, ties broken on ascending index for a
        // stable, deterministic apportionment.
        foreach (var index in Enumerable.Range(0, weights.Count)
                     .OrderByDescending(index => remainders[index])
                     .ThenBy(index => index))
        {
            if (remaining <= 0)
            {
                break;
            }

            counts[index]++;
            remaining--;
        }

        return [.. counts];
    }

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

    [LibraryImport("SDL2")]
    private static partial void SDL_SetWindowMinimumSize(
        nint window,
        int minimumWidth,
        int minimumHeight);

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
        if (_graphics.IsFullScreen)
        {
            return;
        }

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

    private void OnClientSizeChanged(object? sender, EventArgs eventArgs)
    {
        if (GraphicsDevice is null)
        {
            return;
        }

        _fonts?.SelectScale(
            _configuredUiScale,
            GraphicsDevice.Viewport.Width,
            GraphicsDevice.Viewport.Height);
        LogViewport(LogEvents.RenderViewportChanged, LogChannel.Render);
    }

    private void LogViewport(string eventId, LogChannel channel)
    {
        if (!_log.IsEnabledFor(LogLevel.Information, channel))
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        var client = Window.ClientBounds;
        var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        var presentation = GraphicsDevice.PresentationParameters;
        var snapshot = CreateViewportDiagnosticSnapshot(
            client.Width,
            client.Height,
            viewport.Width,
            viewport.Height,
            presentation.BackBufferWidth,
            presentation.BackBufferHeight,
            displayMode.Width,
            displayMode.Height,
            _fonts?.ActiveScale.ToString() ?? _configuredUiScale.ToString(),
            _graphics.IsFullScreen ? "Fullscreen" : "Windowed");
        _log.Write(
            LogLevel.Information,
            channel,
            eventId,
            "client",
            snapshot.ClientDimensions,
            "viewport",
            snapshot.ViewportDimensions,
            "backBufferWidth",
            snapshot.BackBufferWidth,
            "backBufferHeight",
            snapshot.BackBufferHeight,
            "display",
            snapshot.DisplayDimensions,
            "uiScale",
            snapshot.UiScale,
            "windowMode",
            snapshot.WindowMode);
    }

    internal static ViewportDiagnosticSnapshot CreateViewportDiagnosticSnapshot(
        int clientWidth,
        int clientHeight,
        int viewportWidth,
        int viewportHeight,
        int backBufferWidth,
        int backBufferHeight,
        int displayWidth,
        int displayHeight,
        string uiScale,
        string windowMode) =>
        new(
            $"{clientWidth}x{clientHeight}",
            $"{viewportWidth}x{viewportHeight}",
            backBufferWidth,
            backBufferHeight,
            $"{displayWidth}x{displayHeight}",
            uiScale,
            windowMode);

    internal readonly record struct ViewportDiagnosticSnapshot(
        string ClientDimensions,
        string ViewportDimensions,
        int BackBufferWidth,
        int BackBufferHeight,
        string DisplayDimensions,
        string UiScale,
        string WindowMode);

    private void AdvanceSimulation(double elapsedSeconds)
    {
        if (!_presentation.Playback.IsPlaying)
        {
            return;
        }

        if (_simulation.Outcome != BattleOutcome.Ongoing)
        {
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
                _simulation.LastTickCombatByFaction,
                _simulation.Tick);

            // IngestImmediate rather than Ingest, because attack and death
            // cues now travel through AttackContactDispatcher and would sound
            // twice if this route mapped them too. The agent views are passed
            // because the ranged Release cue cannot name its own weapon and
            // reads it from the launcher's loadout — drop them and every
            // release, and every standalone miss, goes silent with nothing
            // failing anywhere.
            _soundDirector.IngestImmediate(
                _simulation.LastEvents,
                _simulation.Agents);
            LogTick();
            _simulationAccumulator -= secondsPerTick;
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
            var reloaded = _settingsStore.Load(_themeManager.ActiveTheme.Id);
            _activeComposition = reloaded.Composition;
            _activeMovementPreset = reloaded.MovementPreset;
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

        _scenario = BuildScenario(
            _matchSeries.CurrentSeed,
            _activeComposition,
            _activeMovementPreset);
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

        // V2-3: the pointer is sampled at the warrior's foot anchor rather
        // than where the cursor is, and the radius covers the drawn body
        // rather than a flat five pixels. Both come from the geometry the
        // renderer actually draws — see AgentPickTarget.
        var samplePoint = AgentPickTarget.SamplePoint(mouseWorld, _camera.Zoom);
        var pointerXRaw = ToRawCoordinate(samplePoint.X);
        var pointerYRaw = ToRawCoordinate(samplePoint.Y);
        var pickRadius = AgentPickTarget.RadiusWorldUnits(_camera.Zoom);
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
        ComputeLayout(screenBounds, _isEventLogVisible, _isSoundLogVisible);

    /// <summary>
    /// Screen partitioning. The right column's split between the battle event
    /// log and the sound log is delegated to <see cref="RightColumnSplit"/>,
    /// which the client tests exercise directly.
    /// </summary>
    private static ClientLayout ComputeLayout(
        Rectangle screenBounds,
        bool isEventLogVisible,
        bool isSoundLogVisible)
    {
        var statusBarHeight = UiScaleContext.Pixels(StatusBarHeight);
        var eventPanelWidth = UiScaleContext.Pixels(EventPanelWidth);
        var layoutMargin = UiScaleContext.Pixels(LayoutMargin);
        var layoutGap = UiScaleContext.Pixels(LayoutGap);
        var inspectorWidthLimit = UiScaleContext.Pixels(InspectorWidth);
        var soundLogMinimumHeight =
            UiScaleContext.Pixels(SoundLogMinimumHeight);
        var contentTop = Math.Min(
            screenBounds.Bottom,
            screenBounds.Top + statusBarHeight);
        var contentHeight = Math.Max(
            0,
            screenBounds.Bottom - contentTop - layoutMargin);
        var eventWidth = Math.Min(
            eventPanelWidth,
            Math.Max(0, screenBounds.Width / 3));
        var columnWidth = isEventLogVisible || isSoundLogVisible
            ? eventWidth
            : 0;
        var columnRect = new Rectangle(
            Math.Max(
                screenBounds.Left,
                screenBounds.Right - columnWidth - layoutMargin),
            contentTop,
            columnWidth,
            contentHeight);
        var column = RightColumnSplit.Split(
            columnRect,
            isEventLogVisible,
            isSoundLogVisible,
            soundLogMinimumHeight,
            SoundLogHeightPercent,
            layoutGap);
        var eventBounds = column.EventBounds;
        var soundLogBounds = column.SoundLogBounds;
        var arenaRight = Math.Max(
            screenBounds.Left + layoutMargin,
            columnWidth == 0
                ? screenBounds.Right - layoutMargin
                : columnRect.Left - layoutGap);
        var arenaBounds = new Rectangle(
            screenBounds.Left + layoutMargin,
            contentTop,
            Math.Max(
                0,
                arenaRight - screenBounds.Left - layoutMargin),
            contentHeight);
        var inspectorWidth = Math.Min(
            inspectorWidthLimit,
            Math.Max(0, arenaBounds.Width - (layoutMargin * 2)));
        var inspectorHeight = Math.Min(
            InspectorHeight,
            Math.Max(0, arenaBounds.Height - (layoutMargin * 2)));
        var inspectorBounds = new Rectangle(
            arenaBounds.Left + layoutMargin,
            Math.Max(
                arenaBounds.Top + layoutMargin,
                arenaBounds.Bottom - inspectorHeight - layoutMargin),
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
