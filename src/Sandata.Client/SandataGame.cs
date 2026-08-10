using System.Collections.Immutable;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Sandata.Client.Audio;
using Sandata.Client.Rendering;
using Sandata.Client.Simulation;
using Sandata.Client.Theming;
using Sandata.Client.UI;
using Sandata.Core.Combat;
using Sandata.Core.Maps;
using Sandata.Core.Mathematics;
using Sandata.Core.Navigation;
using Sandata.Core.Orders;
using Sandata.Core.Rules;
using Sandata.Core.Simulation;
using Sandata.Core.Weapons;

namespace Sandata.Client;

/// <summary>
/// Sandata's MonoGame window: the game loop, the spectator camera, the world
/// renderer for walls, doors, cover, and objectives (plan task 33 of
/// docs/plans/2026-08-07-sandata-scaffold.md), and — task 69 — the composed
/// HUD, the operator pawns, and every overlay design section 11 names.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> This type makes no simulation decision. It reads a
/// caller-supplied, already-parsed and already-validated set of
/// <see cref="MapRecord"/> values (design section 12's `.hkmap` grammar,
/// `Sandata.Core.Maps.MapTokenizer`/<c>MapValidator</c>) and paints what they
/// already say — never targeting, damage, retreat, or victory, matching the
/// same boundary <c>Hukbo.Client.ArenaGame</c> holds for the melee game
/// (CLAUDE.md section 3, the <c>hukbo-client-ui</c> skill). No task before
/// this one wires a scenario or mission loader into the Sandata client, so
/// <see cref="SandataGame"/> accepts its map as a plain constructor
/// parameter — never a hardcoded fixture path, never a
/// <c>LogPaths.FindRepositoryRoot</c>-style lookup, both of which are test-only
/// techniques that would silently stop working the moment this game is run
/// from a packaged build instead of a repository checkout.
/// </para>
/// <para>
/// All the geometry this type draws is produced by
/// <see cref="WorldRenderer"/>'s pure, unit-tested helpers, plus task 37's
/// <see cref="OperatorGeometry"/>, task 45's <see cref="FireConeOverlay"/>/
/// <see cref="OrderPathOverlay"/>/<see cref="BreachMarkerOverlay"/>, and task
/// 38/46's window-layout helpers composed by <see cref="HudComposer"/>. This
/// class's own job is only to own the <see cref="GraphicsDeviceManager"/>,
/// the <see cref="SpriteBatch"/>, the one shared 1x1 pixel texture, and the
/// calls that turn each of those pure results into a <c>spriteBatch.Draw</c>
/// call — none of which a unit test can or should exercise (the
/// <c>hukbo-client-ui</c> skill's pure-helper/impure-Draw split).
/// </para>
/// <para>
/// <b>Placeholders.</b> Several values below stand in for simulation state
/// that does not exist yet — no task before this one wires an
/// <c>OperatorState</c>, a <c>FactionAlertState</c>, a shot-event feed, or an
/// order layer into the Sandata client. Every one of those stand-ins is named
/// with a <c>Placeholder</c> prefix at its declaration and named again, with
/// the task expected to supply the real value, in this task's own final
/// report — never invented silently.
/// </para>
/// </remarks>
internal sealed class SandataGame : Game
{
    // Fallback map extent used only when no GridRecord is present in the
    // supplied map records (for example, an empty map passed by a caller
    // that has not loaded one yet). Matches angle-house.hkmap's own GRID so
    // a camera built against no map still frames a plausible tactical space
    // instead of an arbitrary tiny or huge one.
    private const int DefaultMapWidthWu = 640;
    private const int DefaultMapHeightWu = 720;

    private const int InitialWindowWidth = 1280;
    private const int InitialWindowHeight = 720;

    // ---- Task 69 placeholders: every one stands in for a simulation value
    // no earlier task has wired into the Sandata client yet. See this task's
    // own PLACEHOLDERS report section for the task expected to supply each
    // real value.

    /// <summary>
    /// The body radius <see cref="NavBake.Bake"/> inflates every blocked cell
    /// by. <c>Sandata.Core</c> has no ruleset-level body-radius field yet, so
    /// this reuses the exact value already established as this repository's
    /// own convention at every other <c>NavBake.Bake</c> call site
    /// (<c>tests/Sandata.Core.Tests/NavBakeTests.cs</c>,
    /// <c>FormationCollapseTests.cs</c>), rather than inventing a new one.
    /// </summary>
    private const int PlaceholderBodyRadiusWu = 5;

    /// <summary>
    /// Per-operator sprite scale before <see cref="SandataCamera.Zoom"/> is
    /// applied. No <c>OperatorState</c> exists yet to carry a real value.
    /// </summary>
    private const float PlaceholderOperatorApparentScale = 1f;

    /// <summary>
    /// The detail tier every placeholder operator pawn draws at. No
    /// <c>OperatorState</c> exists yet to carry a real value; <c>Medium</c> is
    /// the middle of <see cref="OperatorDetailTier"/>'s three values, chosen
    /// so both the gear-gated and optics-gated layers get exercised on screen
    /// without picking either extreme arbitrarily.
    /// </summary>
    private const OperatorDetailTier PlaceholderOperatorDetailTier = OperatorDetailTier.Medium;

    /// <summary>
    /// Fire-cone half-width, in <see cref="Bam16"/> raw units, applied to
    /// every placeholder operator. 4,096 raw units is one-sixteenth of a full
    /// turn (22.5 degrees) each side of facing, a 45-degree cone — no
    /// per-weapon field-of-view data exists yet to replace it.
    /// </summary>
    private const ushort PlaceholderFireConeHalfWidthBam = 4096;

    /// <summary>
    /// Fire-cone range, in world units, applied to every placeholder
    /// operator. No per-weapon range data exists yet to replace it.
    /// </summary>
    private const float PlaceholderFireConeRangeWu = 200f;

    /// <summary>
    /// The alert level every draw reads for <see cref="UI.AlertIndicator"/>.
    /// <c>0</c> is Calm (<see cref="UI.AlertIndicator.GetShape"/>'s own
    /// documented mapping) — no <c>FactionAlertState</c> is wired into the
    /// Sandata client yet to replace it.
    /// </summary>
    private const int PlaceholderAlertLevel = 0;

    /// <summary>
    /// Screen-pixel thickness for every line this class draws (fire-cone
    /// edges, the order-path polyline, the multi-select marquee border).
    /// <see cref="WorldRenderer"/> defines no line primitive at all — only an
    /// axis-aligned box and a rotated block — so this is this task's own
    /// drawing decision, not a value read from any helper.
    /// </summary>
    private const float LineThicknessPixels = 2f;

    // ---- Task 71 placeholders. The tick-pipeline one is retired: this class
    // now drives SandataSimulation.RunTick and therefore has a real clock. The
    // per-code roster stand-in below is still a stand-in.

    /// <summary>
    /// The <see cref="Order.FactionId"/> every order this class submits
    /// addresses. Matches this constructor's own established "faction 0 is
    /// the spectator's own squad" convention (see the
    /// <c>spawn.Faction == 0</c> check above).
    /// </summary>
    private const int PlaceholderOrderFactionId = 0;

    /// <summary>
    /// The faction whose squads seek an objective on their own. Faction 0 is
    /// already this class's "the spectator's own squad" convention, and on the
    /// shipped <c>angle-house</c> map both <c>OBJECTIVE</c> records sit
    /// exactly on faction 1's two spawn positions — so the map itself already
    /// reads as "faction 0 assaults, faction 1 holds two rooms". Every other
    /// faction holds, which is what <c>PathReasonCode.NoDestinationRequested</c>
    /// already means for a group no <c>MissionState.Groups</c> entry names.
    /// </summary>
    private const int AssaultingFaction = 0;

    /// <summary>
    /// <see cref="Mission.Seed"/> for the <see cref="Mission"/> this class
    /// builds so it can construct a <see cref="SandataSimulation"/>. No task
    /// before this one wires a real seed source (menu, launch argument, save
    /// file) into the Sandata client, and this task's own file list does not
    /// add one, so every client-hosted run uses this fixed value until a
    /// later task supplies a real one.
    /// </summary>
    private const ulong PlaceholderMissionSeed = 1UL;

    /// <summary>
    /// <see cref="MissionTickPolicy.TickLimit"/> for the client's
    /// <see cref="Mission"/>. This task never calls
    /// <see cref="SandataSimulation.RunTick"/> — it only submits orders
    /// through <see cref="SandataSimulation.SubmitOrder"/> — so this value is
    /// never actually reached by this class; it exists only because
    /// <see cref="MissionTickPolicy"/> requires a positive value.
    /// </summary>
    private const int PlaceholderTickLimit = 36_000;

    /// <summary>
    /// <see cref="MissionTickPolicy.StateHashCadenceTicks"/> for the client's
    /// <see cref="Mission"/>. Unreached for the same reason as
    /// <see cref="PlaceholderTickLimit"/>.
    /// </summary>
    private const int PlaceholderStateHashCadenceTicks = 1;

    /// <summary>
    /// <see cref="MissionFactionSetup.OperatorCount"/> must be strictly
    /// positive for both factions (<see cref="Mission"/>'s own constructor
    /// validation), but this class's own constructor doc comment treats an
    /// empty map-records array as "a valid, empty world, not an error." This
    /// floor keeps that promise: when a faction's real spawn count from the
    /// map is zero, the <see cref="Mission"/> this class builds still reports
    /// one seat for that faction, purely so the constructor does not throw.
    /// Nothing in <c>Sandata.Core</c> reads <see cref="Mission.FactionSetups"/>
    /// anywhere else (confirmed by a repository-wide search before this
    /// task's implementation), so this floor never inflates
    /// <see cref="MissionState.Operators"/> — that list stays exactly as long
    /// as the real, possibly-empty, spawn list the map supplied.
    /// </summary>
    private const int MinimumMissionFactionOperatorCount = 1;

    /// <summary>
    /// A theme used only if the shipped catalog at
    /// <c>Content/Themes/sandata-theme-standards.json</c> cannot be read or
    /// fails validation. <see cref="SandataThemeCatalog"/> (task 13) exposes
    /// no fallback-loading convenience of its own — unlike
    /// <c>Hukbo.Client.Theming.UiThemeCatalog.LoadOrFallback</c> — and this
    /// task's file list forbids adding one, so the fallback lives here
    /// instead, hardcoded and never read from JSON.
    /// </summary>
    private static readonly SandataThemeColors FallbackThemeColors = new(
        CanvasBackground: new Color(18, 18, 22),
        ArenaSurface: new Color(28, 30, 34),
        ArenaBorder: new Color(90, 96, 104),
        StatusSurface: new Color(22, 24, 28),
        OverlayScrim: new Color(0, 0, 0, 160),
        PanelSurface: new Color(32, 34, 40),
        PanelAlternate: new Color(38, 40, 46),
        PanelBorder: new Color(70, 74, 82),
        TextPrimary: Color.White,
        TextSecondary: new Color(200, 200, 205),
        TextDisabled: new Color(120, 120, 125),
        TextInverse: Color.Black,
        ActionDefault: new Color(60, 120, 200),
        ActionHover: new Color(80, 140, 220),
        ActionFocus: new Color(100, 160, 240),
        ActionPressed: new Color(40, 100, 180),
        ActionActive: new Color(60, 120, 200),
        ActionDisabled: new Color(80, 80, 85),
        StatusInfo: new Color(70, 130, 200),
        StatusSuccess: new Color(70, 180, 100),
        StatusWarning: new Color(220, 170, 60),
        StatusDanger: new Color(210, 70, 70),
        NewEvent: new Color(230, 200, 90),
        Friendly: SandataFactionPalette.Friendly,
        Hostile: SandataFactionPalette.Hostile,
        UnknownContact: SandataFactionPalette.UnknownContact,
        SelectedTrooper: new Color(255, 159, 28),
        Suppressed: new Color(150, 150, 60),
        Downed: new Color(120, 40, 40),
        OrderPath: new Color(90, 200, 220),
        Waypoint: new Color(230, 190, 60),
        CoverGood: new Color(70, 160, 90),
        CoverNone: new Color(120, 70, 70),
        BreachPoint: new Color(255, 122, 69),
        FireConeFill: new Color(220, 60, 60, 60),
        FireConeEdge: new Color(220, 60, 60),
        AlertCalm: new Color(70, 130, 200),
        AlertRaised: new Color(220, 170, 60),
        AlertBreach: new Color(210, 70, 70));

    private static readonly SandataThemeMetrics FallbackThemeMetrics = new(
        BorderThickness: 2,
        FocusThickness: 3,
        ShadowOffset: 2);

    private static readonly SandataControlBar.Button[] ControlBarButtons =
    [
        SandataControlBar.Button.Pause,
        SandataControlBar.Button.StepOneTick,
        SandataControlBar.Button.Speed,
        SandataControlBar.Button.Restart,
    ];

    /// <summary>
    /// The speed fractions the <see cref="SandataControlBar.Button.Speed"/>
    /// button cycles through, as (numerator, denominator) pairs handed
    /// straight to <see cref="TickPacing.Advance"/>. Half speed exists because
    /// a room entry at 50 ticks a second is roughly a fifth of a second of
    /// real time and a spectator cannot see what happened inside it; the fast
    /// steps exist because crossing the map takes about twenty seconds at
    /// normal speed.
    /// </summary>
    private static readonly (int Numerator, int Denominator)[] SpeedSteps =
    [
        (1, 2),
        (1, 1),
        (2, 1),
        (4, 1),
    ];

    /// <summary>
    /// The index into <see cref="SpeedSteps"/> a run starts at — normal
    /// speed, one simulation tick per 20 milliseconds of real time.
    /// </summary>
    private const int DefaultSpeedIndex = 1;

    private readonly DiagnosticLog _log;
    private readonly GraphicsDeviceManager _graphics;
    private readonly ImmutableArray<MapRecord> _mapRecords;
    private readonly ImmutableArray<WallRecord> _wallRecords;
    private readonly ImmutableArray<DoorRecord> _doorRecords;
    private readonly ImmutableArray<SpawnRecord> _spawnRecords;
    private readonly ImmutableArray<CoverRecord> _coverRecords;
    private readonly ImmutableArray<WorldRenderer.DrawShape> _breachMarkerWorldShapes;
    private readonly SandataCamera _camera;
    private readonly SandataTheme _theme;
    private readonly NavGrid _navGrid;
    private readonly WallBuckets _wallBuckets;
    private readonly ImmutableArray<ObjectiveRecord> _objectiveRecords;
    private readonly Mission _mission;
    private SandataSimulation _simulation;
    private readonly SandataSoundPlayer _soundPlayer;
    private readonly UndoStack<int> _undoStack = new();
    private readonly int _operatorCount;
    private readonly int _contactCount;

    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private int _previousScrollWheelValue;
    private MouseState _previousMouseState;
    private KeyboardState _previousKeyboardState;
    private DragCapture _dragCapture = DragCapture.Inactive;
    private MultiSelectState _multiSelect = MultiSelectState.Empty;
    private PathDrawState _pathDrawState = PathDrawState.CreateEmpty();
    private ImmutableArray<GoCodePanel.GoCodeEntry> _goCodeEntries = ImmutableArray<GoCodePanel.GoCodeEntry>.Empty;
    private ImmutableArray<OrderQueueView.Entry> _orderQueueEntries = ImmutableArray<OrderQueueView.Entry>.Empty;
    private long _accumulatedMicroseconds;
    private long _nextTick;
    private bool _isPaused;
    private int _speedIndex = DefaultSpeedIndex;
    private int _pendingSingleSteps;

    // -1 rather than the real starting counts, so the first tick of a run
    // always writes one baseline roster line before any casualty can move it.
    private int _lastLoggedAssaultingAlive = -1;
    private int _lastLoggedDefendingAlive = -1;

    /// <summary>
    /// Builds the game window. <paramref name="mapRecords"/> is the already
    /// validated map to draw — an empty array is a valid, empty world, not
    /// an error, since no earlier task wires a real map source in yet.
    /// </summary>
    public SandataGame(DiagnosticLog? log = null, ImmutableArray<MapRecord> mapRecords = default)
    {
        _log = log ?? DiagnosticLog.Disabled;
        _mapRecords = mapRecords.IsDefault ? ImmutableArray<MapRecord>.Empty : mapRecords;
        _theme = LoadTheme();

        _wallRecords = FindWalls(_mapRecords);
        _doorRecords = FindDoors(_mapRecords);
        _spawnRecords = FindSpawns(_mapRecords);
        _coverRecords = FindCovers(_mapRecords);
        _objectiveRecords = FindObjectives(_mapRecords);
        _breachMarkerWorldShapes = BreachMarkerOverlay.CreateWorldShapes(_mapRecords);

        var operatorCount = 0;
        var contactCount = 0;
        foreach (var spawn in _spawnRecords)
        {
            // Faction 0 is this task's own decision for "the spectator's own
            // squad" — SpawnRecord.Faction is real map data (design section
            // 12), but the design doc never states which of its two values
            // reads as friendly, so this follows the same convention
            // Hukbo.Client's own melee game uses for faction 0.
            if (spawn.Faction == 0)
            {
                operatorCount++;
            }
            else
            {
                contactCount++;
            }
        }

        _operatorCount = operatorCount;
        _contactCount = contactCount;

        var grid = FindGrid(_mapRecords);
        var mapWidthWu = grid?.WidthWu ?? DefaultMapWidthWu;
        var mapHeightWu = grid?.HeightWu ?? DefaultMapHeightWu;
        _camera = new SandataCamera(mapWidthWu, mapHeightWu);

        // NavGrid's own dimensions are in cells, not world units (NavGrid's
        // remarks); CellSizeWu is the fixed conversion factor. Clamped to
        // NavGrid's own [1, MaxDimensionCells] range so an unusually large or
        // small GRID record never throws out of this constructor.
        var widthCells = Math.Clamp(mapWidthWu / NavGrid.CellSizeWu, 1, NavGrid.MaxDimensionCells);
        var heightCells = Math.Clamp(mapHeightWu / NavGrid.CellSizeWu, 1, NavGrid.MaxDimensionCells);
        _navGrid = new NavGrid(widthCells, heightCells);
        NavBake.Bake(_navGrid, _wallRecords, _doorRecords, PlaceholderBodyRadiusWu);

        // OrderQueue.SubmitValidated requires a non-null WallBuckets for every
        // order kind, including OrderKind.GoCodeRelease (task 71 build item
        // 5), so this task's own order-submission call sites need one built
        // over the same wall data _navGrid was already baked from — no
        // earlier task builds one for the Sandata client.
        _wallBuckets = BuildWallBuckets(_navGrid, _wallRecords);

        // Task 80: build a real Mission/MissionState pair from the caller's
        // own already-validated map, and construct the one production
        // SandataSimulation this class ever hosts. SandataSimulation.SubmitOrder
        // is the only door into OrderQueue that also emits
        // MissionEventKind.OrderRejected, so a real simulation instance is
        // required before either ReleaseGoCode or SubmitDrawnPath can call it.
        var canonicalMapRecords = MapCanonicalizer.Canonicalize(_mapRecords);
        var mapContentHash = MapContentHash.Compute(canonicalMapRecords);

        _mission = new Mission(
            formatVersion: Mission.CurrentFormatVersion,
            seed: PlaceholderMissionSeed,
            mapContentHash: mapContentHash,
            tickPolicy: new MissionTickPolicy(PlaceholderTickLimit, PlaceholderStateHashCadenceTicks),
            factionSetups: ImmutableArray.Create(
                new MissionFactionSetup(FactionId: 0, OperatorCount: Math.Max(MinimumMissionFactionOperatorCount, _operatorCount)),
                new MissionFactionSetup(FactionId: 1, OperatorCount: Math.Max(MinimumMissionFactionOperatorCount, _contactCount))),
            rulesetId: SandataPresetId.ModernTacticalV1);

        _simulation = CreateSimulation();

        // Task 39 built SandataSoundPlayer and SandataSoundBudget but
        // deliberately left the MonoGame-backed ISandataSoundOutput to a
        // later task — no SoundEffectInstance-backed implementation exists
        // anywhere in this codebase yet, and this task's file list forbids
        // adding a new file for one. NullSandataSoundOutput below is
        // constructed here so the player's budget bookkeeping exists; nothing
        // in this class calls HandleShotFired or HandleAutomaticFireStopped,
        // since no shot-event source exists yet either.
        _soundPlayer = new SandataSoundPlayer(new NullSandataSoundOutput(), new SandataSoundBudget());

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = InitialWindowWidth,
            PreferredBackBufferHeight = InitialWindowHeight,
            SynchronizeWithVerticalRetrace = true,
        };

        Window.AllowUserResizing = true;
        Window.Title = "Sandata";
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();

        // Fits the camera to the window exactly once, on the first real
        // viewport this process ever has; SandataCamera.Fit is itself a
        // no-op on every call after the first.
        _camera.Fit(GraphicsDevice.Viewport.Bounds);

        _log.Write(
            LogLevel.Information,
            LogChannel.Boot,
            LogEvents.BootWindowCreated,
            "width",
            GraphicsDevice.Viewport.Width,
            "height",
            GraphicsDevice.Viewport.Height);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
    }

    protected override void UnloadContent()
    {
        _pixel?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        var mouseState = Mouse.GetState();
        var keyboardState = Keyboard.GetState();
        _camera.Update(
            keyboardState,
            mouseState.ScrollWheelValue,
            _previousScrollWheelValue,
            (float)gameTime.ElapsedGameTime.TotalSeconds);
        _previousScrollWheelValue = mouseState.ScrollWheelValue;

        // Transport first: a click that lands on the control bar is consumed
        // there and must not also begin a marquee drag, and design section
        // 11's pointer-priority chain puts the in-world layer last.
        var transportConsumedClick = UpdateTransportControls(mouseState, keyboardState);

        if (!transportConsumedClick)
        {
            UpdateDragCapture(mouseState);
        }

        UpdatePathDrawing(mouseState);
        UpdatePathSubmission(keyboardState);
        UpdateGoCodeReleases(keyboardState);

        AdvanceSimulation(gameTime);

        _previousMouseState = mouseState;
        _previousKeyboardState = keyboardState;

        base.Update(gameTime);
    }

    /// <summary>
    /// Runs the simulation forward by however many whole 20-millisecond ticks
    /// this frame earned, per <see cref="TickPacing.Advance"/>. A paused run
    /// executes only the single steps
    /// <see cref="SandataControlBar.Button.StepOneTick"/> has queued, and
    /// banks no time at all while paused — so unpausing resumes rather than
    /// fast-forwarding through however long the pause lasted.
    /// </summary>
    /// <remarks>
    /// The mission's own <see cref="MissionTickPolicy.TickLimit"/> is the hard
    /// stop. Nothing here inspects the simulation's outcome: deciding a
    /// mission is over is a simulation decision and this class makes none.
    /// </remarks>
    private void AdvanceSimulation(GameTime gameTime)
    {
        if (_nextTick >= _mission.TickPolicy.TickLimit)
        {
            return;
        }

        int ticksToRun;

        if (_isPaused)
        {
            _accumulatedMicroseconds = 0;
            ticksToRun = _pendingSingleSteps;
            _pendingSingleSteps = 0;
        }
        else
        {
            var (numerator, denominator) = SpeedSteps[_speedIndex];
            var elapsedMicroseconds = (long)gameTime.ElapsedGameTime.TotalMilliseconds * 1_000;

            (_accumulatedMicroseconds, ticksToRun) = TickPacing.Advance(
                _accumulatedMicroseconds,
                elapsedMicroseconds,
                numerator,
                denominator,
                TickPacing.DefaultMaxTicksPerFrame);
        }

        for (var step = 0; step < ticksToRun && _nextTick < _mission.TickPolicy.TickLimit; step++)
        {
            _simulation.RunTick(_nextTick);
            _nextTick++;
            LogRosterIfChanged();
        }
    }

    /// <summary>
    /// Writes one <see cref="LogEvents.SimSandataRoster"/> line on each tick
    /// where a faction's living count changed, so a run leaves behind a record
    /// of when its casualties happened. Nothing is written on a tick where
    /// nobody died, which in a whole mission is almost every tick.
    /// </summary>
    /// <remarks>
    /// The counting loop runs only when the line would actually be emitted —
    /// the level and channel are tested first, so a <c>Release</c> run, or a
    /// run with the <c>sim</c> channel filtered out, does no work and
    /// allocates nothing. That is the debug-logging standard's allocation
    /// rule, and it is why this method reads state the caller already holds
    /// rather than asking the simulation a question the frame would not
    /// otherwise ask.
    /// </remarks>
    private void LogRosterIfChanged()
    {
        if (!_log.IsEnabledFor(LogLevel.Debug, LogChannel.Simulation))
        {
            return;
        }

        var assaulting = 0;
        var defending = 0;
        foreach (var operatorState in _simulation.State.Operators)
        {
            if (!DamageResolution.IsAlive(operatorState.Health))
            {
                continue;
            }

            if (operatorState.Faction == AssaultingFaction)
            {
                assaulting++;
            }
            else
            {
                defending++;
            }
        }

        if (assaulting == _lastLoggedAssaultingAlive && defending == _lastLoggedDefendingAlive)
        {
            return;
        }

        _lastLoggedAssaultingAlive = assaulting;
        _lastLoggedDefendingAlive = defending;

        _log.SetTick(_nextTick);
        _log.Write(
            LogLevel.Debug, LogChannel.Simulation, LogEvents.SimSandataRoster,
            "assaultingAlive", assaulting, "defendingAlive", defending);
    }

    /// <summary>
    /// The four spectator transport controls, reachable both from the control
    /// bar and from the keyboard: Space toggles pause, the period key steps
    /// one tick while paused, the Tab key cycles the speed fraction, the R key
    /// restarts, and Escape closes the window — the same five
    /// <c>scripts/run.ps1</c> tells a person about on launch.
    /// </summary>
    /// <returns>
    /// Whether this frame's left-button press landed on a control-bar button
    /// and was consumed there, in which case the caller must not also treat it
    /// as the start of a marquee drag.
    /// </returns>
    private bool UpdateTransportControls(MouseState mouseState, KeyboardState keyboardState)
    {
        if (WasJustPressed(keyboardState, Keys.Escape))
        {
            Exit();
            return false;
        }

        if (WasJustPressed(keyboardState, Keys.Space))
        {
            TogglePause();
        }

        if (WasJustPressed(keyboardState, Keys.OemPeriod))
        {
            StepOneTick();
        }

        if (WasJustPressed(keyboardState, Keys.Tab))
        {
            CycleSpeed();
        }

        // F5 rather than R: UpdateGoCodeReleases treats every one of Keys.A
        // through Keys.Z as a go-code release, so a letter key bound here
        // would fire a transport control and submit an order from one press.
        if (WasJustPressed(keyboardState, Keys.F5))
        {
            RestartMission();
        }

        var wasPressed = _previousMouseState.LeftButton == ButtonState.Pressed;
        var isPressed = mouseState.LeftButton == ButtonState.Pressed;
        if (!isPressed || wasPressed)
        {
            return false;
        }

        var barBounds = SandataControlBar.CalculateBounds(GraphicsDevice.Viewport.Bounds);
        foreach (var button in ControlBarButtons)
        {
            if (!SandataControlBar.CalculateButtonBounds(barBounds, button).Contains(mouseState.Position))
            {
                continue;
            }

            switch (button)
            {
                case SandataControlBar.Button.Pause:
                    TogglePause();
                    break;
                case SandataControlBar.Button.StepOneTick:
                    StepOneTick();
                    break;
                case SandataControlBar.Button.Speed:
                    CycleSpeed();
                    break;
                case SandataControlBar.Button.Restart:
                    RestartMission();
                    break;
            }

            return true;
        }

        return false;
    }

    private bool WasJustPressed(KeyboardState keyboardState, Keys key) =>
        keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        _log.Write(
            LogLevel.Debug, LogChannel.Input, LogEvents.InputSandataTransport,
            "control", "pause", "paused", _isPaused, "tick", _nextTick);
    }

    /// <summary>
    /// Queues one tick. Pauses first when the run is playing, because "step
    /// one tick" while thirty more are arriving every second is not a step a
    /// spectator can see.
    /// </summary>
    private void StepOneTick()
    {
        _isPaused = true;
        _pendingSingleSteps++;
        _log.Write(
            LogLevel.Debug, LogChannel.Input, LogEvents.InputSandataTransport,
            "control", "step", "paused", _isPaused, "tick", _nextTick);
    }

    private void CycleSpeed()
    {
        _speedIndex = (_speedIndex + 1) % SpeedSteps.Length;
        _log.Write(
            LogLevel.Debug, LogChannel.Input, LogEvents.InputSandataTransport,
            "control", "speed", "speedNumerator", SpeedSteps[_speedIndex].Numerator,
            "speedDenominator", SpeedSteps[_speedIndex].Denominator, "tick", _nextTick);
    }

    /// <summary>
    /// Rebuilds the simulation from the map and returns every spectator-facing
    /// selection and draft to its launch state. The camera is deliberately
    /// left where the spectator put it: a restart re-runs the mission, it does
    /// not re-frame the window.
    /// </summary>
    private void RestartMission()
    {
        _simulation = CreateSimulation();
        _nextTick = 0;
        _accumulatedMicroseconds = 0;
        _pendingSingleSteps = 0;
        _lastLoggedAssaultingAlive = -1;
        _lastLoggedDefendingAlive = -1;
        _multiSelect = MultiSelectState.Empty;
        _pathDrawState = PathDrawState.CreateEmpty();
        _goCodeEntries = ImmutableArray<GoCodePanel.GoCodeEntry>.Empty;
        _orderQueueEntries = ImmutableArray<OrderQueueView.Entry>.Empty;

        _log.Write(
            LogLevel.Information, LogChannel.Input, LogEvents.InputSandataTransport,
            "control", "restart", "paused", _isPaused, "tick", _nextTick);
    }

    /// <summary>
    /// Task 46's <see cref="UI.DragCapture"/>/<see cref="UI.MultiSelectState"/>
    /// wired to real mouse input: a drag that starts outside every composed
    /// HUD panel produces a marquee; releasing it selects every friendly
    /// placeholder operator pawn inside it, exactly as
    /// <see cref="UI.MultiSelectState.FromMarquee"/> already defines.
    /// </summary>
    private void UpdateDragCapture(MouseState mouseState)
    {
        var windowBounds = GraphicsDevice.Viewport.Bounds;
        var hudLayout = ComposeHudLayout(windowBounds);
        var panelBounds = ComposedPanelBounds(hudLayout);

        var wasPressed = _previousMouseState.LeftButton == ButtonState.Pressed;
        var isPressed = mouseState.LeftButton == ButtonState.Pressed;

        if (isPressed && !wasPressed)
        {
            _dragCapture = DragCapture.Begin(mouseState.Position, panelBounds);
        }
        else if (isPressed && _dragCapture.IsActive)
        {
            _dragCapture = _dragCapture.WithCurrentPosition(mouseState.Position);
        }
        else if (!isPressed && wasPressed && _dragCapture.IsActive)
        {
            if (_dragCapture.MarqueeBounds is { } marqueeBounds)
            {
                var candidates = BuildMarqueeCandidates(windowBounds);
                _multiSelect = MultiSelectState.FromMarquee(marqueeBounds, candidates);
            }

            _dragCapture = _dragCapture.End();
        }
    }

    /// <summary>
    /// Task 71's pointer routing for the in-world path-drawing layer: a
    /// right-button press converts to a world-space point and is appended to
    /// the in-progress drawn path via <see cref="TryAddPathNode"/>, unless it
    /// starts inside any composed HUD panel — mirroring
    /// <see cref="UI.DragCapture.Begin"/>'s own refusal for the same reason,
    /// design section 11's pointer-priority chain: "the topmost consuming
    /// element wins ... the in-world layer is last." The right mouse button
    /// is this task's own input-mapping decision — design section 11 names
    /// no physical input for path drawing, and the left button already
    /// drives drag-capture and marquee selection.
    /// </summary>
    private void UpdatePathDrawing(MouseState mouseState)
    {
        var wasPressed = _previousMouseState.RightButton == ButtonState.Pressed;
        var isPressed = mouseState.RightButton == ButtonState.Pressed;
        if (!isPressed || wasPressed)
        {
            return;
        }

        var windowBounds = GraphicsDevice.Viewport.Bounds;
        var hudLayout = ComposeHudLayout(windowBounds);
        var panelBounds = ComposedPanelBounds(hudLayout);

        var worldPositionWu = _camera.ScreenToWorld(mouseState.Position, windowBounds);
        _pathDrawState = TryAddPathNode(_pathDrawState, mouseState.Position, worldPositionWu, panelBounds);
    }

    /// <summary>
    /// Task 73's submission wiring for the in-world path-drawing layer: the
    /// Enter key, pressed and not yet released, submits the in-progress drawn
    /// path via <see cref="SubmitDrawnPath"/>, addressed to the multi-select
    /// state's current selection. The Enter key is this task's own
    /// input-mapping decision — design section 11 and section 16 name no
    /// physical input for path submission, the same gap task 71 already
    /// documented for the right mouse button that draws a node.
    /// </summary>
    private void UpdatePathSubmission(KeyboardState keyboardState)
    {
        var wasPressed = _previousKeyboardState.IsKeyDown(Keys.Enter);
        var isPressed = keyboardState.IsKeyDown(Keys.Enter);
        if (!isPressed || wasPressed)
        {
            return;
        }

        var addressees = ToAddressees(_multiSelect.SelectedEntityIds);

        (_pathDrawState, _orderQueueEntries) = SubmitDrawnPath(
            _pathDrawState,
            addressees,
                // The next tick this window will execute. Not zero: stage 1
                // applies an order only on the tick that equals its
                // TargetTick exactly ("order.TargetTick != currentTick"), so
                // an order targeting tick 0 submitted at tick 900 would be
                // accepted, queued, hashed — and never applied to anybody.
                _nextTick,
            PlaceholderOrderFactionId,
            _simulation,
            _orderQueueEntries);
    }

    /// <summary>
    /// Task 71's go-code keypress wiring: releasing an A-through-Z key
    /// submits one <see cref="OrderKind.GoCodeRelease"/> order addressed to
    /// whichever operators the marquee currently has selected, via
    /// <see cref="ReleaseGoCode"/> — design section 16: "releasing that
    /// letter is itself an order ... a keypress therefore enters the same
    /// queue as everything else." No earlier task wires a real per-letter
    /// operator assignment into the Sandata client, so the current
    /// multi-select is this task's own provisional stand-in for "the
    /// operators tied to the released code." The <see cref="Keys"/>-to-
    /// <see langword="char"/> conversion happens here, in impure code,
    /// because <see cref="ReleaseGoCode"/> and every other pure helper this
    /// class exposes must not depend on a platform input type (the
    /// <c>hukbo-client-ui</c> skill's platform-input rule).
    /// </summary>
    private void UpdateGoCodeReleases(KeyboardState keyboardState)
    {
        for (var key = Keys.A; key <= Keys.Z; key++)
        {
            var wasDown = _previousKeyboardState.IsKeyDown(key);
            var isDown = keyboardState.IsKeyDown(key);
            if (!wasDown || isDown)
            {
                continue;
            }

            var letter = (char)('A' + (key - Keys.A));
            var addressees = ToAddressees(_multiSelect.SelectedEntityIds);

            (_goCodeEntries, _orderQueueEntries) = ReleaseGoCode(
                letter,
                addressees,
                // The next tick this window will execute. Not zero: stage 1
                // applies an order only on the tick that equals its
                // TargetTick exactly ("order.TargetTick != currentTick"), so
                // an order targeting tick 0 submitted at tick 900 would be
                // accepted, queued, hashed — and never applied to anybody.
                _nextTick,
                PlaceholderOrderFactionId,
                _simulation,
                _goCodeEntries,
                _orderQueueEntries);
        }
    }

    /// <summary>
    /// Composes <see cref="HudComposer.Layout"/> for <paramref name="windowBounds"/>
    /// from this instance's own operator/contact/go-code/order-queue counts —
    /// the one place every <see cref="HudComposer.Compose"/> call site in this
    /// class builds its arguments, so <see cref="UpdateDragCapture"/>,
    /// <see cref="UpdatePathDrawing"/>, and <see cref="Draw(GameTime)"/> can
    /// never drift out of sync with one another.
    /// </summary>
    private HudComposer.Layout ComposeHudLayout(Rectangle windowBounds) =>
        HudComposer.Compose(windowBounds, _operatorCount, _contactCount, _navGrid, _goCodeEntries.Length, _orderQueueEntries.Length);

    /// <summary>
    /// Every panel <paramref name="hudLayout"/> anchors, in the same order
    /// <see cref="UpdateDragCapture"/> already established before task 71 —
    /// now including <see cref="HudComposer.Layout.GoCodePanel"/> and
    /// <see cref="HudComposer.Layout.OrderQueueView"/>, so both drag-capture
    /// refusal and path-node refusal cover them exactly as they already cover
    /// every other panel.
    /// </summary>
    private static Rectangle[] ComposedPanelBounds(HudComposer.Layout hudLayout) =>
    [
        hudLayout.RosterStrip,
        hudLayout.ContactList,
        hudLayout.AlertIndicator,
        hudLayout.MissionClock,
        hudLayout.EventLog,
        hudLayout.OperatorInspector,
        hudLayout.GoCodePanel,
        hudLayout.OrderQueueView,
        hudLayout.ControlBar,
        hudLayout.Minimap,
    ];

    /// <summary>
    /// Whether <paramref name="position"/> falls inside any rectangle in
    /// <paramref name="panelBounds"/> — the same "a higher-priority panel
    /// already consumed this pointer-down" test <see cref="UI.DragCapture.Begin"/>
    /// already applies to the drag-capture layer, reused here so path
    /// drawing obeys the identical pointer-priority rule.
    /// </summary>
    internal static bool IsPointerOverAnyPanel(Point position, IReadOnlyList<Rectangle> panelBounds)
    {
        foreach (var bounds in panelBounds)
        {
            if (bounds.Contains(position))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Appends a node at <paramref name="worldPositionWu"/> to
    /// <paramref name="state"/> via <see cref="UI.PathDrawTool.AddNode"/>,
    /// unless <paramref name="screenPosition"/> falls inside any of
    /// <paramref name="panelBounds"/> — in which case the panel has already
    /// consumed the pointer-down and <paramref name="state"/> is returned
    /// unchanged, so a click on the go-code panel or the order queue view
    /// (or any other composed panel) never becomes a path node.
    /// </summary>
    internal static PathDrawState TryAddPathNode(
        PathDrawState state, Point screenPosition, Vector2 worldPositionWu, IReadOnlyList<Rectangle> panelBounds)
    {
        if (IsPointerOverAnyPanel(screenPosition, panelBounds))
        {
            return state;
        }

        var node = new DrawnPathNode((long)MathF.Round(worldPositionWu.X), (long)MathF.Round(worldPositionWu.Y));
        return PathDrawTool.AddNode(state, node);
    }

    /// <summary>
    /// Converts <paramref name="selectedEntityIds"/> —
    /// <see cref="UI.MultiSelectState.SelectedEntityIds"/>'s own placeholder
    /// entity-id representation — to the <see langword="ulong"/> addressee
    /// list <see cref="OrderQueue.SubmitValidated"/> requires. No
    /// <c>EntityId</c> type wider than <see langword="int"/> exists yet in
    /// the Sandata client, so this is a direct widening, not a lookup.
    /// </summary>
    internal static ImmutableArray<ulong> ToAddressees(ImmutableArray<int> selectedEntityIds)
    {
        if (selectedEntityIds.IsDefaultOrEmpty)
        {
            return ImmutableArray<ulong>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<ulong>(selectedEntityIds.Length);
        foreach (var entityId in selectedEntityIds)
        {
            builder.Add((ulong)entityId);
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// Submits one <see cref="OrderKind.GoCodeRelease"/> order for
    /// <paramref name="letter"/>, addressed to <paramref name="addressees"/>,
    /// through <see cref="SandataSimulation.SubmitOrder"/> — the same door
    /// <see cref="UI.PathDrawTool.Submit"/> uses for a drawn path, and the
    /// only production door into <see cref="OrderQueue"/> that also emits
    /// <see cref="Sandata.Core.Events.MissionEventKind.OrderRejected"/> on rejection — and
    /// folds the result into both the go-code panel's own entry list and the
    /// order queue view's entry list, so an accepted release marks its code
    /// released and a rejected one still becomes an observable queue entry
    /// carrying its specific <see cref="OrderRejectReason"/> (design section
    /// 16: "rejection is observable").
    /// </summary>
    internal static (
        ImmutableArray<GoCodePanel.GoCodeEntry> GoCodeEntries,
        ImmutableArray<OrderQueueView.Entry> OrderQueueEntries) ReleaseGoCode(
        char letter,
        ImmutableArray<ulong> addressees,
        long targetTick,
        int factionId,
        SandataSimulation simulation,
        ImmutableArray<GoCodePanel.GoCodeEntry> existingGoCodeEntries,
        ImmutableArray<OrderQueueView.Entry> existingOrderQueueEntries)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        var (_, submitted, rejection) = simulation.SubmitOrder(
            targetTick, factionId, addressees, OrderKind.GoCodeRelease);

        var goCodeEntries = existingGoCodeEntries.IsDefault
            ? ImmutableArray<GoCodePanel.GoCodeEntry>.Empty
            : existingGoCodeEntries;
        var orderQueueEntries = existingOrderQueueEntries.IsDefault
            ? ImmutableArray<OrderQueueView.Entry>.Empty
            : existingOrderQueueEntries;

        if (submitted is not null)
        {
            goCodeEntries = goCodeEntries.Add(new GoCodePanel.GoCodeEntry(letter, addressees.Length, IsReleased: true));
            orderQueueEntries = orderQueueEntries.Add(OrderQueueView.FromSubmittedOrder(submitted));
        }
        else if (rejection is not null)
        {
            orderQueueEntries = orderQueueEntries.Add(
                OrderQueueView.FromRejection(rejection, OrderKind.GoCodeRelease, targetTick));
        }

        return (goCodeEntries, orderQueueEntries);
    }

    /// <summary>
    /// Submits the in-progress drawn path <paramref name="state"/> holds, as
    /// one <see cref="OrderKind.MoveAlongPath"/> order addressed to
    /// <paramref name="addressees"/>, through <see cref="UI.PathDrawTool.Submit"/>
    /// — the only production caller <see cref="UI.PathDrawTool.Submit"/> had
    /// none of before task 73 — and folds the result into the order queue
    /// view's own entry list, the same "accepted marks it observed, rejected
    /// still becomes an observable entry" shape <see cref="ReleaseGoCode"/>
    /// already establishes for a go-code release.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An empty <paramref name="addressees"/> submits nothing at all</b> —
    /// not an order with no addressees. <see cref="OrderQueue.SubmitValidated"/>
    /// has no rule that rejects an empty addressee set for
    /// <see cref="OrderKind.MoveAlongPath"/> (design section 16's four
    /// rejection rules are all about the polyline, never the addressee set),
    /// so an implementation that passed an empty set through unconditionally
    /// would silently create a real, accepted, un-addressed order — one no
    /// operator is ever assigned to and one that still consumes
    /// <see cref="OrderQueue.NextOrderId"/> and
    /// <see cref="OrderQueue.NextOrderSequence"/> for nothing. This method
    /// refuses that case before calling <see cref="UI.PathDrawTool.Submit"/>
    /// at all: <paramref name="simulation"/>'s own state and
    /// <paramref name="existingOrderQueueEntries"/> all come back byte-for-byte
    /// unchanged, and — because nothing was submitted — <paramref name="state"/>
    /// also comes back unchanged rather than being reset to empty, so a
    /// spectator who pressed Enter before finishing a marquee selection keeps
    /// the path they already drew and can select operators and try again
    /// without redrawing it.
    /// </para>
    /// <para>
    /// <b>Post-submission state, on either outcome.</b> When
    /// <paramref name="addressees"/> is non-empty, <see cref="UI.PathDrawTool.Submit"/>
    /// always returns a fresh, empty <see cref="PathDrawState"/> — whether the
    /// order was accepted or rejected — so this method never leaves a stale
    /// drawn path on screen that would otherwise render as a phantom path
    /// next frame.
    /// </para>
    /// </remarks>
    internal static (
        PathDrawState State,
        ImmutableArray<OrderQueueView.Entry> OrderQueueEntries) SubmitDrawnPath(
        PathDrawState state,
        ImmutableArray<ulong> addressees,
        long targetTick,
        int factionId,
        SandataSimulation simulation,
        ImmutableArray<OrderQueueView.Entry> existingOrderQueueEntries)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        var orderQueueEntries = existingOrderQueueEntries.IsDefault
            ? ImmutableArray<OrderQueueView.Entry>.Empty
            : existingOrderQueueEntries;

        if (addressees.IsDefaultOrEmpty)
        {
            return (state, orderQueueEntries);
        }

        var (updatedState, submitted, rejection) = PathDrawTool.Submit(
            state, simulation, targetTick, factionId, addressees);

        if (submitted is not null)
        {
            orderQueueEntries = orderQueueEntries.Add(OrderQueueView.FromSubmittedOrder(submitted));
        }
        else if (rejection is not null)
        {
            orderQueueEntries = orderQueueEntries.Add(
                OrderQueueView.FromRejection(rejection, OrderKind.MoveAlongPath, targetTick));
        }

        return (updatedState, orderQueueEntries);
    }

    /// <summary>
    /// Converts the in-progress drawn path's nodes to the
    /// <see cref="Vector2"/> world-unit waypoint list
    /// <see cref="UI.OrderPathOverlay.CreateWorldSegments"/> and
    /// <see cref="UI.OrderPathOverlay.CreateWaypointWorldShapes"/> both
    /// already accept — the placeholder empty list <see cref="DrawOrderPath"/>
    /// used before this task retires here.
    /// </summary>
    internal static ImmutableArray<Vector2> ToOrderPathWaypointsWu(ImmutableArray<DrawnPathNode> nodes)
    {
        if (nodes.IsDefaultOrEmpty)
        {
            return ImmutableArray<Vector2>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<Vector2>(nodes.Length);
        foreach (var node in nodes)
        {
            builder.Add(new Vector2(node.X, node.Y));
        }

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// One marquee candidate per living operator, at its live simulated
    /// position and carrying its real <c>OperatorState.EntityId</c> — the
    /// spawn index this used as a placeholder before the client ticked the
    /// simulation is retired, so a selection now addresses the entity the
    /// order layer expects. A casualty is not a candidate: selecting one and
    /// drawing it a path would submit an order no living operator can carry
    /// out.
    /// </summary>
    private ImmutableArray<MarqueeCandidate> BuildMarqueeCandidates(Rectangle contentBounds)
    {
        var operators = _simulation.State.Operators;
        if (operators.IsDefaultOrEmpty)
        {
            return ImmutableArray<MarqueeCandidate>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<MarqueeCandidate>(operators.Length);
        foreach (var operatorState in operators)
        {
            if (!DamageResolution.IsAlive(operatorState.Health))
            {
                continue;
            }

            var worldPosition = new Vector2(
                (float)operatorState.PositionX.ToDouble(),
                (float)operatorState.PositionY.ToDouble());
            var screenPosition = _camera.WorldToScreen(worldPosition, contentBounds);

            builder.Add(new MarqueeCandidate(
                EntityId: (int)operatorState.EntityId,
                ScreenPosition: new Point((int)MathF.Round(screenPosition.X), (int)MathF.Round(screenPosition.Y)),
                IsHostile: operatorState.Faction != AssaultingFaction));
        }

        return builder.ToImmutable();
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(_theme.Colors.ArenaSurface);

        var contentBounds = GraphicsDevice.Viewport.Bounds;
        var spriteBatch = _spriteBatch!;
        spriteBatch.Begin();

        // Draw order follows the .hkmap canonical ordinals (design section
        // 12: wall, door, cover, objective) so a door always paints over the
        // wall line it interrupts and cover always reads on top of the walls
        // around it.
        foreach (var record in _mapRecords)
        {
            switch (record)
            {
                case WallRecord wall:
                    // Provisional role reuse (task 13 has no "wall" role):
                    // ArenaBorder is the closest existing structural-line role.
                    Draw(spriteBatch, WorldRenderer.CreateWallWorldShape(wall), contentBounds, _theme.Colors.ArenaBorder);
                    break;
                case DoorRecord door:
                    // Provisional role reuse: PanelBorder reads as a distinct,
                    // lighter structural line than a solid wall.
                    Draw(spriteBatch, WorldRenderer.CreateDoorWorldShape(door), contentBounds, _theme.Colors.PanelBorder);
                    break;
                case CoverRecord cover:
                    // Provisional role reuse: CoverGood is the one tactical
                    // role already named for "a place that protects you".
                    Draw(spriteBatch, WorldRenderer.CreateCoverWorldShape(cover), contentBounds, _theme.Colors.CoverGood);
                    break;
                case ObjectiveRecord objective:
                    // Provisional role reuse: Waypoint is the one role
                    // already named for "a point on the map to go to".
                    Draw(spriteBatch, WorldRenderer.CreateObjectiveWorldShape(objective), contentBounds, _theme.Colors.Waypoint);
                    break;
            }
        }

        // Real, non-placeholder data: every map-declared breachable wall
        // (design section 12's Material == 3) gets task 45's own marker
        // shape, reusing this class's existing DrawShape helper unchanged.
        foreach (var breachShape in _breachMarkerWorldShapes)
        {
            Draw(spriteBatch, breachShape, contentBounds, _theme.Colors.BreachPoint);
        }

        DrawOperatorsAndFireCones(spriteBatch, contentBounds);
        DrawOrderPath(spriteBatch, contentBounds);

        var hudLayout = ComposeHudLayout(contentBounds);
        DrawHud(spriteBatch, contentBounds, hudLayout);
        DrawMinimapCells(spriteBatch, hudLayout.Minimap);
        DrawMarquee(spriteBatch);

        spriteBatch.End();
        base.Draw(gameTime);
    }

    /// <summary>
    /// One placeholder operator pawn per <see cref="SpawnRecord"/> (task 37's
    /// <see cref="OperatorGeometry"/>/<see cref="OperatorRenderer"/>), plus
    /// its fire cone (task 45's <see cref="FireConeOverlay"/>). Spawn
    /// position and facing are real map data; every other input —
    /// <see cref="PlaceholderOperatorApparentScale"/>,
    /// <see cref="PlaceholderOperatorDetailTier"/>, the fire-cone half-width
    /// and range — stands in for a value no <c>OperatorState</c> or weapon
    /// record exists yet to supply.
    /// </summary>
    private void DrawOperatorsAndFireCones(SpriteBatch spriteBatch, Rectangle contentBounds)
    {
        var operators = _simulation.State.Operators;
        if (operators.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var operatorState in operators)
        {
            var isFriendly = operatorState.Faction == AssaultingFaction;
            var isAlive = DamageResolution.IsAlive(operatorState.Health);
            var worldPosition = new Vector2(
                (float)operatorState.PositionX.ToDouble(),
                (float)operatorState.PositionY.ToDouble());
            var facing = operatorState.AimAngle;
            var isSelected = _multiSelect.SelectedEntityIds.Contains((int)operatorState.EntityId);

            var layout = OperatorGeometry.Create(
                rootPosition: _camera.WorldToScreen(worldPosition, contentBounds),
                apparentScale: PlaceholderOperatorApparentScale * _camera.Zoom,
                detailTier: PlaceholderOperatorDetailTier,
                weaponAimBam: facing,
                // Every frame's smoothingFactor is 1 below, so the displayed
                // angle always snaps straight to weaponAimBam regardless of
                // this value — this class keeps no per-operator smoothing
                // state across frames, a placeholder simplification pending
                // the real per-operator state a future task supplies.
                previousDisplayRotationRawUnits: 0f,
                smoothingFactor: 1f,
                isFiring: operatorState.WeaponChainPhase == (int)WeaponChainPhase.Firing,
                isSelected: isSelected && isAlive);

            // A casualty stays on the map and reads as one. Removing the pawn
            // instead would leave a spectator unable to tell a death from an
            // operator who walked out of view, which is exactly the question
            // the fire-cone and roster panels cannot answer either.
            var bodyColor = isAlive
                ? (isFriendly ? _theme.Colors.Friendly : _theme.Colors.Hostile)
                : _theme.Colors.Downed;

            OperatorRenderer.Draw(
                spriteBatch,
                _pixel!,
                layout,
                bodyColor: bodyColor,
                // The 39-role theme has no dedicated "weapon" role; reusing
                // the operator's own faction color avoids inventing an
                // unlisted 40th role.
                weaponColor: bodyColor,
                muzzleFlashColor: _theme.Colors.StatusDanger,
                selectionColor: _theme.Colors.SelectedTrooper);

            // A dead operator watches nothing. Drawing its last fire cone
            // would read as a live field of view.
            if (isAlive)
            {
                DrawFireCone(spriteBatch, contentBounds, worldPosition, facing);
            }
        }
    }

    private void DrawFireCone(SpriteBatch spriteBatch, Rectangle contentBounds, Vector2 apexWu, Bam16 facing)
    {
        var worldGeometry = FireConeOverlay.CreateWorldGeometry(
            apexWu, facing, PlaceholderFireConeHalfWidthBam, PlaceholderFireConeRangeWu);
        var screenGeometry = FireConeOverlay.ToScreenGeometry(worldGeometry, _camera, contentBounds);

        DrawLine(spriteBatch, screenGeometry.Apex, screenGeometry.LeftEdgeEnd, _theme.Colors.FireConeEdge);
        DrawLine(spriteBatch, screenGeometry.Apex, screenGeometry.RightEdgeEnd, _theme.Colors.FireConeEdge);
    }

    /// <summary>
    /// Task 45's order-path overlay, now (task 71) fed the real in-progress
    /// drawn path's nodes via <see cref="ToOrderPathWaypointsWu"/> instead of
    /// the placeholder empty list task 69 composed it with.
    /// <see cref="OrderPathOverlay.CreateWorldSegments"/> and
    /// <see cref="OrderPathOverlay.CreateWaypointWorldShapes"/> both already
    /// define "fewer than the minimum input" as "produce nothing", so this
    /// draws nothing before the first node is placed and the whole polyline
    /// once <see cref="_pathDrawState"/> has one.
    /// </summary>
    private void DrawOrderPath(SpriteBatch spriteBatch, Rectangle contentBounds)
    {
        var waypointsWu = ToOrderPathWaypointsWu(_pathDrawState.Nodes);

        var worldSegments = OrderPathOverlay.CreateWorldSegments(waypointsWu);
        var screenSegments = OrderPathOverlay.ToScreenSegments(worldSegments, _camera, contentBounds);
        foreach (var segment in screenSegments)
        {
            DrawLine(spriteBatch, segment.Start, segment.End, _theme.Colors.OrderPath);
        }

        var waypointShapes = OrderPathOverlay.CreateWaypointWorldShapes(waypointsWu);
        foreach (var shape in waypointShapes)
        {
            Draw(spriteBatch, shape, contentBounds, _theme.Colors.Waypoint);
        }
    }

    /// <summary>
    /// Draws every panel <see cref="HudComposer.Layout"/> anchors — a filled,
    /// bordered background rectangle for each, since no font/text pipeline
    /// exists anywhere in <c>Sandata.Client</c> to render row content onto
    /// them. Roster tiles, control-bar buttons, go-code rows, and order queue
    /// rows each get one extra layer of sub-rectangle geometry since their
    /// own helpers already expose it cheaply; the rest are a single panel
    /// rectangle.
    /// </summary>
    private void DrawHud(SpriteBatch spriteBatch, Rectangle windowBounds, HudComposer.Layout layout)
    {
        DrawPanel(spriteBatch, layout.RosterStrip);
        DrawRosterTiles(spriteBatch, windowBounds, layout.RosterStrip);

        DrawPanel(spriteBatch, layout.ContactList);
        DrawPanel(spriteBatch, layout.MissionClock);
        DrawPanel(spriteBatch, layout.EventLog);
        DrawPanel(spriteBatch, layout.OperatorInspector);

        DrawPanel(spriteBatch, layout.GoCodePanel);
        DrawGoCodeRows(spriteBatch, layout.GoCodePanel);

        DrawPanel(spriteBatch, layout.OrderQueueView);
        DrawOrderQueueRows(spriteBatch, layout.OrderQueueView);

        DrawPanel(spriteBatch, layout.ControlBar);
        DrawControlButtons(spriteBatch, layout.ControlBar);

        DrawAlertIndicator(spriteBatch, layout.AlertIndicator);
    }

    /// <summary>
    /// Draws one colored row per <see cref="_goCodeEntries"/> entry, using
    /// <see cref="UI.GoCodePanel.CalculateRowBounds"/> and
    /// <see cref="UI.GoCodePanel.ResolveEntryColor"/> — the same "no
    /// font/text pipeline, so a row is a colored rectangle" precedent
    /// <see cref="DrawRosterTiles"/> and <see cref="DrawControlButtons"/>
    /// already establish; <see cref="UI.GoCodePanel.FormatEntryLine"/> is
    /// never called for the same reason no other panel's <c>FormatEntryLine</c>
    /// equivalent is called anywhere in this class's draw path.
    /// </summary>
    private void DrawGoCodeRows(SpriteBatch spriteBatch, Rectangle panelBounds)
    {
        for (var index = 0; index < _goCodeEntries.Length; index++)
        {
            var rowBounds = GoCodePanel.CalculateRowBounds(panelBounds, index);
            var color = GoCodePanel.ResolveEntryColor(_theme.Colors, _goCodeEntries[index]);
            spriteBatch.Draw(_pixel, rowBounds, color);
        }
    }

    /// <summary>
    /// Draws one colored row per <see cref="_orderQueueEntries"/> entry,
    /// using <see cref="UI.OrderQueueView.CalculateRowBounds"/> and
    /// <see cref="UI.OrderQueueView.ResolveEntryColor"/> — see
    /// <see cref="DrawGoCodeRows"/>'s own remarks for why no
    /// <c>FormatEntryLine</c> call belongs in this draw path.
    /// </summary>
    private void DrawOrderQueueRows(SpriteBatch spriteBatch, Rectangle panelBounds)
    {
        for (var index = 0; index < _orderQueueEntries.Length; index++)
        {
            var rowBounds = OrderQueueView.CalculateRowBounds(panelBounds, index);
            var color = OrderQueueView.ResolveEntryColor(_theme.Colors, _orderQueueEntries[index]);
            spriteBatch.Draw(_pixel, rowBounds, color);
        }
    }

    private void DrawRosterTiles(SpriteBatch spriteBatch, Rectangle windowBounds, Rectangle stripBounds)
    {
        var visibleTiles = RosterStrip.CountVisibleTiles(windowBounds, _operatorCount);
        for (var index = 0; index < visibleTiles; index++)
        {
            var tileBounds = RosterStrip.CalculateTileBounds(stripBounds, index);
            spriteBatch.Draw(_pixel, tileBounds, _theme.Colors.PanelAlternate);
            DrawBorder(spriteBatch, tileBounds, BorderThickness(), _theme.Colors.PanelBorder);
        }
    }

    private void DrawControlButtons(SpriteBatch spriteBatch, Rectangle barBounds)
    {
        foreach (var button in ControlBarButtons)
        {
            var buttonBounds = SandataControlBar.CalculateButtonBounds(barBounds, button);
            spriteBatch.Draw(_pixel, buttonBounds, _theme.Colors.ActionDefault);
            DrawBorder(spriteBatch, buttonBounds, BorderThickness(), _theme.Colors.PanelBorder);
        }
    }

    /// <summary>
    /// Draws <see cref="UI.AlertIndicator"/>'s panel plus one glyph reading
    /// <see cref="PlaceholderAlertLevel"/>. <see cref="UI.AlertIndicator.IndicatorShape"/>
    /// names three distinct silhouettes (circle, diamond, triangle) but
    /// <see cref="WorldRenderer.DrawShape"/> — the only geometry primitive
    /// this codebase has — offers only an axis-aligned box and a rotated
    /// block. Circle draws as an unrotated inset square, Diamond as that same
    /// square rotated 45 degrees, and Triangle (approximated) as a narrower
    /// rectangle also rotated 45 degrees: three visually distinct
    /// rectangles standing in for three named shapes until a real
    /// circle/triangle primitive exists.
    /// </summary>
    private void DrawAlertIndicator(SpriteBatch spriteBatch, Rectangle bounds)
    {
        DrawPanel(spriteBatch, bounds);

        const int inset = 6;
        var glyphBounds = new Rectangle(
            bounds.Left + inset,
            bounds.Top + inset,
            Math.Max(0, bounds.Width - (inset * 2)),
            Math.Max(0, bounds.Height - (inset * 2)));

        if (glyphBounds.Width <= 0 || glyphBounds.Height <= 0)
        {
            return;
        }

        var shape = AlertIndicator.GetShape(PlaceholderAlertLevel);
        var color = AlertIndicator.GetColor(PlaceholderAlertLevel, _theme.Colors);

        switch (shape)
        {
            case AlertIndicator.IndicatorShape.Circle:
                spriteBatch.Draw(_pixel, glyphBounds, color);
                break;
            case AlertIndicator.IndicatorShape.Diamond:
                DrawRotatedGlyph(spriteBatch, glyphBounds, MathF.PI / 4f, color);
                break;
            default:
                var narrowed = new Rectangle(
                    glyphBounds.Center.X - (glyphBounds.Width / 4),
                    glyphBounds.Top,
                    Math.Max(1, glyphBounds.Width / 2),
                    glyphBounds.Height);
                DrawRotatedGlyph(spriteBatch, narrowed, MathF.PI / 4f, color);
                break;
        }
    }

    private void DrawRotatedGlyph(SpriteBatch spriteBatch, Rectangle localBounds, float rotationRadians, Color color)
    {
        var center = new Vector2(localBounds.Center.X, localBounds.Center.Y);
        spriteBatch.Draw(
            _pixel,
            center,
            sourceRectangle: null,
            color,
            rotationRadians,
            origin: new Vector2(0.5f, 0.5f),
            scale: new Vector2(localBounds.Width, localBounds.Height),
            SpriteEffects.None,
            layerDepth: 0f);
    }

    /// <summary>
    /// Colors every <see cref="NavGrid"/> cell inside the already-composed
    /// <paramref name="minimapBounds"/>, reusing task 46's own
    /// <see cref="Minimap.CalculateCellPixelBounds"/> and
    /// <see cref="Minimap.ResolveCellColor"/> unchanged. Real, non-placeholder
    /// data: this reads the same <see cref="_navGrid"/> baked from the map's
    /// own walls and doors in the constructor.
    /// </summary>
    private void DrawMinimapCells(SpriteBatch spriteBatch, Rectangle minimapBounds)
    {
        for (var y = 0; y < _navGrid.Height; y++)
        {
            for (var x = 0; x < _navGrid.Width; x++)
            {
                var flags = _navGrid.Passability[_navGrid.CellIndex(x, y)];
                var cellBounds = Minimap.CalculateCellPixelBounds(minimapBounds, _navGrid, x, y);
                spriteBatch.Draw(_pixel, cellBounds, Minimap.ResolveCellColor(_theme.Colors, flags));
            }
        }
    }

    private void DrawMarquee(SpriteBatch spriteBatch)
    {
        if (_dragCapture.MarqueeBounds is { } marqueeBounds)
        {
            DrawBorder(spriteBatch, marqueeBounds, 1, _theme.Colors.SelectedTrooper);
        }
    }

    private void DrawPanel(SpriteBatch spriteBatch, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        spriteBatch.Draw(_pixel, bounds, _theme.Colors.PanelSurface);
        DrawBorder(spriteBatch, bounds, BorderThickness(), _theme.Colors.PanelBorder);
    }

    /// <summary>
    /// Draws a rectangle's four edges as separate axis-aligned bars, each
    /// clamped to the rectangle's own width/height so a panel thinner or
    /// shorter than the theme's border thickness still yields a non-negative
    /// draw rectangle at the smallest supported window.
    /// </summary>
    private void DrawBorder(SpriteBatch spriteBatch, Rectangle bounds, int thickness, Color color)
    {
        var horizontalThickness = Math.Min(thickness, bounds.Height);
        var verticalThickness = Math.Min(thickness, bounds.Width);

        spriteBatch.Draw(_pixel, new Rectangle(bounds.Left, bounds.Top, bounds.Width, horizontalThickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(bounds.Left, bounds.Bottom - horizontalThickness, bounds.Width, horizontalThickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(bounds.Left, bounds.Top, verticalThickness, bounds.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - verticalThickness, bounds.Top, verticalThickness, bounds.Height), color);
    }

    private int BorderThickness() => Math.Max(1, _theme.Metrics.BorderThickness);

    /// <summary>
    /// Draws a straight line from <paramref name="screenStart"/> to
    /// <paramref name="screenEnd"/> as a rotated, stretched pixel — the same
    /// technique <see cref="Draw(SpriteBatch, WorldRenderer.DrawShape, Rectangle, Color)"/>
    /// already uses for a rotated block, except the origin sits at the
    /// segment's own start rather than its center, since a line has no
    /// "center" pivot that matters. <see cref="WorldRenderer"/> defines no
    /// line primitive of its own for either the fire-cone edges or the
    /// order-path polyline to reuse.
    /// </summary>
    private void DrawLine(SpriteBatch spriteBatch, Vector2 screenStart, Vector2 screenEnd, Color color)
    {
        var delta = screenEnd - screenStart;
        var length = delta.Length();
        if (length <= 0f)
        {
            return;
        }

        var rotationRadians = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(
            _pixel,
            screenStart,
            sourceRectangle: null,
            color,
            rotationRadians,
            origin: Vector2.Zero,
            scale: new Vector2(length, LineThicknessPixels),
            SpriteEffects.None,
            layerDepth: 0f);
    }

    private void Draw(SpriteBatch spriteBatch, WorldRenderer.DrawShape worldShape, Rectangle contentBounds, Color color)
    {
        var screenShape = WorldRenderer.ToScreenShape(worldShape, _camera, contentBounds);
        if (screenShape.Kind == WorldRenderer.DrawShapeKind.AxisAligned)
        {
            spriteBatch.Draw(_pixel, screenShape.AxisAlignedBounds, color);
            return;
        }

        // A rotated block's Center is already the final screen-space center
        // of the whole shape (WorldRenderer.ToScreenShape converts it with
        // the same camera pass as everything else), so it needs no separate
        // pivot offset the way a pawn's weapon does around its wielder.
        spriteBatch.Draw(
            _pixel,
            screenShape.RotatedCenter,
            sourceRectangle: null,
            color,
            screenShape.RotationRadians,
            origin: new Vector2(0.5f, 0.5f),
            scale: new Vector2(screenShape.RotatedLength, screenShape.RotatedThickness),
            SpriteEffects.None,
            layerDepth: 0f);
    }

    private static GridRecord? FindGrid(ImmutableArray<MapRecord> records)
    {
        foreach (var record in records)
        {
            if (record is GridRecord grid)
            {
                return grid;
            }
        }

        return null;
    }

    private static ImmutableArray<WallRecord> FindWalls(ImmutableArray<MapRecord> records)
    {
        var builder = ImmutableArray.CreateBuilder<WallRecord>();
        foreach (var record in records)
        {
            if (record is WallRecord wall)
            {
                builder.Add(wall);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<DoorRecord> FindDoors(ImmutableArray<MapRecord> records)
    {
        var builder = ImmutableArray.CreateBuilder<DoorRecord>();
        foreach (var record in records)
        {
            if (record is DoorRecord door)
            {
                builder.Add(door);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Marshals <paramref name="walls"/>' four coordinate fields into the flat
    /// <see langword="long"/> arrays <see cref="WallBuckets.Build"/> requires.
    /// Un-tested data marshaling, matching <see cref="FindWalls"/> and
    /// <see cref="FindDoors"/>'s own precedent immediately above.
    /// </summary>
    private static WallBuckets BuildWallBuckets(NavGrid grid, ImmutableArray<WallRecord> walls)
    {
        var segmentAX = new long[walls.Length];
        var segmentAY = new long[walls.Length];
        var segmentBX = new long[walls.Length];
        var segmentBY = new long[walls.Length];
        for (var index = 0; index < walls.Length; index++)
        {
            var wall = walls[index];
            segmentAX[index] = wall.X1;
            segmentAY[index] = wall.Y1;
            segmentBX[index] = wall.X2;
            segmentBY[index] = wall.Y2;
        }

        return WallBuckets.Build(grid, segmentAX, segmentAY, segmentBX, segmentBY);
    }

    /// <summary>
    /// Builds the client's initial <see cref="MissionState"/> directly from
    /// the map's own <see cref="SpawnRecord"/> values — real position,
    /// faction, and facing, in spawn order (already ascending, satisfying
    /// <see cref="MissionState.Operators"/>'s own ascending-<c>EntityId</c>
    /// requirement). <see cref="OperatorState.AimAngle"/> uses the spawn's
    /// exact raw <see cref="Bam16"/> facing rather than the coarser
    /// <see cref="Facing16"/> reconstruction <c>Sandata.Headless.HeadlessRunner</c>
    /// uses, because this caller — unlike that one — actually has the exact
    /// value to carry. Every other <see cref="OperatorState"/> field
    /// (<c>Health</c>, <c>MagazineRounds</c>, and the rest) has no source in
    /// a <see cref="SpawnRecord"/> at all, so each is a fixed placeholder
    /// copied from <c>HeadlessRunner.BuildInitialState</c> — the one existing
    /// production template for this shape, and the only one this task's file
    /// list permits reading rather than editing.
    /// </summary>
    /// <summary>
    /// Builds a fresh simulation over this window's already-built mission,
    /// grid, wall buckets, and map records. Called once from the constructor
    /// and once per <see cref="SandataControlBar.Button.Restart"/>: a restart
    /// is a new <see cref="SandataSimulation"/> over a newly built initial
    /// state, because <see cref="SandataSimulation"/> has no reset of its own
    /// and its <c>PathService</c>, order queue, and event feed all carry the
    /// previous run's history.
    /// </summary>
    private SandataSimulation CreateSimulation()
    {
        var initialState = BuildInitialState(_spawnRecords, _objectiveRecords, _navGrid);
        return new SandataSimulation(
            _mission, SandataRuleset.ModernTacticalV1, _navGrid, _wallBuckets, initialState, _coverRecords);
    }

    private static MissionState BuildInitialState(
        ImmutableArray<SpawnRecord> spawnRecords,
        ImmutableArray<ObjectiveRecord> objectiveRecords,
        NavGrid navGrid)
    {
        var operators = ImmutableArray.CreateBuilder<OperatorState>(spawnRecords.Length);
        for (var index = 0; index < spawnRecords.Length; index++)
        {
            var spawn = spawnRecords[index];
            var rawFacing = new Bam16((ushort)spawn.FacingBam);

            operators.Add(new OperatorState(
                EntityId: (ulong)(index + 1),
                PositionX: FixedPoint.FromWhole(spawn.X),
                PositionY: FixedPoint.FromWhole(spawn.Y),
                Facing: rawFacing.ToFacing16(),
                AimAngle: rawFacing,
                Health: 100,
                Faction: spawn.Faction,
                Intent: 0,
                IsCrouched: false,
                WeaponLowered: false,
                WeaponChainPhase: 0,
                WeaponChainRemainingTicks: 0,
                MagazineRounds: 30,
                CyclicFireAccumulator: 0,
                SuppressionCounter: 0));
        }

        var built = operators.MoveToImmutable();
        return new MissionState(
            Tick: 0, Phase: 1, Winner: -1, NextEntityId: (ulong)(spawnRecords.Length + 1), NextEventSequence: 0)
        {
            Operators = built,
            FactionAlerts = ImmutableArray.Create(new FactionAlertState(0, 0), new FactionAlertState(1, 0)),
            Doors = ImmutableArray<DoorState>.Empty,

            // Sandata's autonomous destination source. Before this, the array
            // was empty and stage 7 ran its whole search-and-publish machinery
            // every tick with nothing to act on, so no operator ever walked
            // anywhere on its own. See InitialSquadGroups for the rule and for
            // why it lives in the client rather than in the simulation.
            Groups = InitialSquadGroups.Build(
                built,
                objectiveRecords,
                navGrid,
                SandataRuleset.ModernTacticalV1.GroupCohesionRadiusWu,
                AssaultingFaction),

            RngStreams = ImmutableArray<RngStreamState>.Empty,
        };
    }

    /// <summary>
    /// The parsed <c>OBJECTIVE</c> records this map carries, in the same
    /// "filter <see cref="_mapRecords"/> once, at construction" shape as
    /// <see cref="FindSpawns"/> — fed to <see cref="InitialSquadGroups"/> so
    /// an assaulting squad has a destination on the first tick.
    /// </summary>
    private static ImmutableArray<ObjectiveRecord> FindObjectives(ImmutableArray<MapRecord> records)
    {
        var builder = ImmutableArray.CreateBuilder<ObjectiveRecord>();
        foreach (var record in records)
        {
            if (record is ObjectiveRecord objective)
            {
                builder.Add(objective);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<SpawnRecord> FindSpawns(ImmutableArray<MapRecord> records)
    {
        var builder = ImmutableArray.CreateBuilder<SpawnRecord>();
        foreach (var record in records)
        {
            if (record is SpawnRecord spawn)
            {
                builder.Add(spawn);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Task 79d-2b: the parsed <c>COVER</c> records this client's map carries,
    /// in the same "filter <see cref="_mapRecords"/> once, at construction"
    /// shape as <see cref="FindSpawns"/> — fed to <see cref="SandataSimulation"/>
    /// so stage 12's damage resolution can look cover up from the real map
    /// instead of the constant <see cref="CoverState.NotInCover"/> every shot
    /// resolved against before this task.
    /// </summary>
    private static ImmutableArray<CoverRecord> FindCovers(ImmutableArray<MapRecord> records)
    {
        var builder = ImmutableArray.CreateBuilder<CoverRecord>();
        foreach (var record in records)
        {
            if (record is CoverRecord cover)
            {
                builder.Add(cover);
            }
        }

        return builder.ToImmutable();
    }

    private SandataTheme LoadTheme()
    {
        var catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "sandata-theme-standards.json");

        try
        {
            var catalog = SandataThemeCatalog.Load(catalogPath);
            if (catalog.TryGet(catalog.DefaultThemeId, out var theme))
            {
                _log.Write(
                    LogLevel.Information,
                    LogChannel.Assets,
                    LogEvents.AssetsThemeLoaded,
                    "themeId",
                    theme.Id);
                return theme;
            }
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                InvalidDataException)
        {
            _log.Write(
                LogLevel.Warning,
                LogChannel.Assets,
                LogEvents.AssetsThemeLoaded,
                "msg",
                exception.Message,
                "fallback",
                true);
        }

        return new SandataTheme("fallback", "Fallback", FallbackThemeColors, FallbackThemeMetrics);
    }

    /// <summary>
    /// A placeholder <see cref="ISandataSoundOutput"/> that never actually
    /// plays audio — see this class's constructor for why one is
    /// constructed at all.
    /// </summary>
    private sealed class NullSandataSoundOutput : ISandataSoundOutput
    {
        public bool Play(SoundSlot slot, int variantNumber, ulong shooterEntityId) => false;
    }
}
