using System.Collections.Immutable;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Movement;
using Hukbo.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Sandata.Client.Audio;
using Sandata.Client.Rendering;
using Sandata.Client.Simulation;
using Sandata.Client.Theming;
using Sandata.Client.UI;
using Sandata.Core.Combat;
using Sandata.Core.Events;
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
/// Sandata's scaffold plan), and — task 69 — the composed
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
    /// Half the width of an impact mark's X, in world units. 6 wu is a little
    /// under half a metre at Sandata's 16-world-units-per-metre scale, which
    /// puts the mark at roughly the width of the operator it is drawn on.
    /// </summary>
    private const float ImpactMarkArmWu = 6f;

    /// <summary>
    /// The two weapon sprite asset ids, and the pixel inside each sprite that
    /// sits on the operator's grip and that the sprite rotates about. Both
    /// numbers are authored by
    /// <c>tools/sandata-weapon-sprites/generate.py</c> and restated in
    /// <c>src/Sandata.Client/Content/Sprites/README.md</c>; changing one place
    /// without the other swings the weapon about a point off in space.
    /// </summary>
    private const string RifleSpriteAssetId = "Sprites/weapon-rifle";
    private const string PistolSpriteAssetId = "Sprites/weapon-pistol";
    private static readonly Vector2 RifleSpriteGripAnchor = new(10f, 7f);
    private static readonly Vector2 PistolSpriteGripAnchor = new(4f, 6f);

    /// <summary>
    /// The health every operator in the placeholder roster starts with.
    /// <b>Provisional, and a gameplay tuning value rather than a measurement</b>
    /// — no <c>SpawnRecord</c> carries a health figure and no design document
    /// fixes one, so this is a scenario placeholder in the client, not a
    /// simulation constant. It is deliberately not in
    /// <c>SandataRuleset</c>: nothing here folds into
    /// <c>SandataRuleset.ContentHash</c> and changing it costs no preset
    /// version.
    /// </summary>
    /// <remarks>
    /// Raised from <c>100</c> to <c>300</c> on 2026-08-14 so that automatic
    /// fire can be heard at all. At <c>100</c>, and with 7.62x39 doing 25
    /// damage a round, the fourth round killed — so the longest burst the game
    /// could physically produce was four rounds over 0.30 seconds, and smoke
    /// row <c>SD-5</c> asks a person to judge <em>sustained</em> automatic
    /// fire by ear. At <c>300</c> a burst runs twelve rounds, about 1.2
    /// seconds at the AK's 600 rounds per minute, which is long enough to
    /// hear as a burst rather than as a stutter. The cost is that every
    /// engagement on the placeholder map takes proportionally longer to
    /// resolve.
    /// </remarks>
    private const int PlaceholderOperatorHealth = 300;

    /// <summary>
    /// How far from a click, in screen pixels, an operator can be and still be
    /// the one the click selected. An operator's ground ring is 12 world units
    /// across, which at the zoom a spectator actually plays at is a mark of
    /// roughly this size, so a radius of 12 makes the pawn itself the target
    /// rather than demanding a hit on its centre pixel.
    /// </summary>
    private const int OperatorPickRadiusPixels = 12;

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
        Weapon: new Color(220, 228, 238),
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

    /// <summary>
    /// Every theme the shipped catalog declares, with the catalog's own
    /// default theme first — or, on a load failure, the single hardcoded
    /// <see cref="FallbackThemeColors"/> theme. Task 9's F6 switcher cycles
    /// <see cref="_theme"/> through this array via <see cref="NextThemeId"/>;
    /// before that task the client only ever read <c>catalog.DefaultThemeId</c>
    /// and kept nothing else the catalog offered.
    /// </summary>
    private readonly ImmutableArray<SandataTheme> _themes;
    private SandataTheme _theme;
    private readonly NavGrid _navGrid;

    /// <summary>
    /// Which squad each assaulting operator belongs to, frozen at tick zero
    /// from the same clustering that produced <c>MissionState.Groups</c>.
    /// <c>MissionState</c> exposes no operator-to-group link of its own, and
    /// the operator inspector needs one to report a path reason code that
    /// belongs to the group rather than to the operator.
    /// </summary>
    private ImmutableArray<InitialSquadGroups.GroupMember> _groupMembership =
        ImmutableArray<InitialSquadGroups.GroupMember>.Empty;
    private readonly WallBuckets _wallBuckets;
    private readonly ImmutableArray<ObjectiveRecord> _objectiveRecords;
    private readonly Mission _mission;
    private SandataSimulation _simulation;
    private readonly MonoGameSandataSoundOutput _soundOutput;
    private readonly SandataSoundPlayer _soundPlayer;
    private readonly UndoStack<int> _undoStack = new();
    private readonly int _operatorCount;
    private readonly int _contactCount;

    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;

    /// <summary>
    /// The baked font atlases, or <c>null</c> when the content pipeline
    /// produced nothing for this build. Every text draw checks it first: a
    /// missing font costs the labels and leaves the panels, which is what the
    /// client did in full before 2026-08-11, rather than crashing a run that
    /// would otherwise be playable.
    /// </summary>
    private SandataFontSet? _fonts;

    /// <summary>
    /// The two weapon textures, or <c>null</c> on the same terms as
    /// <see cref="_fonts"/>. <see cref="OperatorRenderer"/> falls back to the
    /// primitive weapon bar when a sprite is missing.
    /// </summary>
    private Texture2D? _rifleSprite;
    private Texture2D? _pistolSprite;

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

    /// <summary>
    /// Live muzzle flashes, tracers, and impact marks — the layer that makes a
    /// firefight visible at all. Presentation only: nothing here reaches the
    /// simulation, and <see cref="CombatFeedback"/>'s remarks record why the
    /// flash layer drew nothing before 2026-08-11.
    /// </summary>
    private ImmutableArray<CombatEffect> _combatEffects = ImmutableArray<CombatEffect>.Empty;

    /// <summary>
    /// Every shooter the client still considers mid-burst. Presentation only:
    /// it exists so that a burst's end can be detected and its tail cue can
    /// play, per design section 10.
    /// </summary>
    /// <remarks>
    /// This held "shooters that fired on the previous tick" until 2026-08-14,
    /// which reported a burst as ended on the first tick a shooter did not
    /// fire on and then forgot the shooter. At 600 rounds per minute that
    /// first quiet tick is one of the four gaps inside a burst, so the end was
    /// reported three ticks early, once, and the real end never at all — see
    /// <see cref="AutomaticBurstTracking"/> and decision D4 of that day's
    /// design. A shooter now stays here until
    /// <see cref="SandataSoundPlayer.HandleAutomaticFireStopped"/> reports the
    /// burst genuinely over.
    /// </remarks>
    private ImmutableArray<ulong> _shootersMidBurst = ImmutableArray<ulong>.Empty;

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
        _themes = LoadThemes();
        _theme = _themes[0];

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

        // Task 39 built SandataSoundPlayer and SandataSoundBudget against a
        // null output, on the reasoning that no MonoGame-backed
        // ISandataSoundOutput existed and no shot-event source did either.
        // The second half of that was already wrong when it was written:
        // MissionEventKind.ShotFired has been emitted by SandataSimulation
        // since the caliber work, and CombatFeedback reads it to draw a muzzle
        // flash. Both halves are answered now — SoundShotsFiredOn feeds the
        // player from that same event feed, and the output below actually
        // plays a file.
        _soundOutput = new MonoGameSandataSoundOutput(
            SandataSoundLibrary.GetDefaultDirectoryPath(), _log);
        _soundPlayer = new SandataSoundPlayer(_soundOutput, new SandataSoundBudget());

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

        LoadFonts();
        LoadWeaponSprites();
    }

    /// <summary>
    /// Loads the two baked font atlases. A failure is recorded and swallowed
    /// rather than rethrown, which is the opposite of what
    /// <c>Hukbo.Client.ArenaGame</c> does with its own fonts, and the reason
    /// is that Hukbo cannot draw a single panel without text while Sandata
    /// drew every panel without text for its whole life before this. A
    /// Sandata run with no font is degraded; it is not broken.
    /// </summary>
    private void LoadFonts()
    {
        try
        {
            _fonts = SandataFontSet.Load(Content.Load<SpriteFont>);
            _log.Write(
                LogLevel.Information, LogChannel.Assets, LogEvents.AssetsFontLoaded,
                "roleCount", SandataFontRamp.AllRoles.Count);
        }
        catch (ContentLoadException exception)
        {
            _fonts = null;
            _log.Write(
                LogLevel.Warning, LogChannel.Assets, LogEvents.AssetsFontFailed,
                "msg", exception.Message);
        }
    }

    /// <summary>
    /// Loads the weapon silhouettes on the same terms as
    /// <see cref="LoadFonts"/>: a missing sprite leaves
    /// <see cref="OperatorRenderer"/> drawing the primitive weapon bar it drew
    /// before the textures existed.
    /// </summary>
    private void LoadWeaponSprites()
    {
        try
        {
            _rifleSprite = Content.Load<Texture2D>(RifleSpriteAssetId);
            _pistolSprite = Content.Load<Texture2D>(PistolSpriteAssetId);
            _log.Write(
                LogLevel.Information, LogChannel.Assets, LogEvents.AssetsSpriteLoaded,
                "spriteCount", 2);
        }
        catch (ContentLoadException exception)
        {
            _rifleSprite = null;
            _pistolSprite = null;
            _log.Write(
                LogLevel.Warning, LogChannel.Assets, LogEvents.AssetsSpriteFailed,
                "msg", exception.Message);
        }
    }

    protected override void UnloadContent()
    {
        _soundOutput.Dispose();
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
        UpdateThemeSwitch(keyboardState);

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

        // One frame's worth of ageing, applied once rather than once per tick:
        // these marks are measured in frames, and a frame that happened to
        // execute four ticks must not expire them four times as fast. See
        // CombatEffect's remarks on why the lifetime is frames and not ticks.
        _combatEffects = CombatFeedback.Age(_combatEffects);

        for (var step = 0; step < ticksToRun && _nextTick < _mission.TickPolicy.TickLimit; step++)
        {
            var healthBefore = CombatFeedback.CaptureHealth(_simulation.State.Operators);
            var executedTick = _nextTick;

            _simulation.RunTick(executedTick);
            _nextTick++;

            _combatEffects = CombatFeedback.Append(
                _combatEffects,
                CombatFeedback.ObserveTick(
                    _simulation.State.EventFeed,
                    _simulation.State.Operators,
                    healthBefore,
                    executedTick));

            SoundShotsFiredOn(executedTick);
            LogWeaponStateTransitionsOn(executedTick);
            LogRosterIfChanged();
        }
    }

    /// <summary>
    /// Plays a gunshot for every <c>ShotFired</c> event the tick just executed
    /// emitted. Nothing called <see cref="SandataSoundPlayer.HandleShotFired"/>
    /// before 2026-08-11, on the belief that no shot-event source existed;
    /// <c>MissionEventKind.ShotFired</c> has been emitted by
    /// <c>SandataSimulation</c> since the caliber work, and
    /// <see cref="CombatFeedback.ObserveTick"/> was already reading the same
    /// events to draw a muzzle flash.
    /// <para>
    /// Range is real — the distance to the nearest living hostile, which is
    /// the engagement the shot belongs to. Indoors is real too, since
    /// 2026-08-14: <see cref="IndoorPresence.IsIndoors"/> derives it on the
    /// client from the same baked <see cref="_navGrid"/> and
    /// <see cref="_wallBuckets"/> this class already holds, because nothing in
    /// <c>Sandata.Core</c> knows or should know which side of a wall an
    /// operator is on — that is a sound choice, not a gameplay one. Suppressor
    /// is still always false, because no weapon in the catalog carries one.
    /// </para>
    /// </summary>
    private void SoundShotsFiredOn(long executedTick)
    {
        var feed = _simulation.State.EventFeed.Events;
        var automaticShootersThisTick = ImmutableArray.CreateBuilder<ulong>();

        // An empty feed means nobody fired this tick, which is a tick that can
        // still end a burst — so it walks no events and reports the stops all
        // the same, rather than returning early as it did before 2026-08-14.
        foreach (var missionEvent in feed.IsDefaultOrEmpty ? ImmutableArray<MissionEvent>.Empty : feed)
        {
            if (missionEvent.Kind != MissionEventKind.ShotFired ||
                missionEvent.Tick != executedTick)
            {
                continue;
            }

            var shooterEntityId = unchecked((ulong)missionEvent.SubjectId);
            if (!TryFindOperator(shooterEntityId, out var shooter))
            {
                continue;
            }

            var mode = ToAudioFireMode((FireModeSet)missionEvent.ReasonCode);
            if (mode == FireMode.Auto && !automaticShootersThisTick.Contains(shooterEntityId))
            {
                automaticShootersThisTick.Add(shooterEntityId);
            }

            _soundPlayer.HandleShotFired(
                FirearmCatalog.Rows[(int)shooter.Firearm].Caliber,
                mode,
                RangeToNearestHostileWu(shooter),
                shooterIsIndoors: IsShooterIndoors(shooter),
                suppressorFitted: false,
                executedTick,
                shooterEntityId);
        }

        SoundAutomaticFireStops(executedTick, automaticShootersThisTick.ToImmutable());
    }

    /// <summary>
    /// Reports a possible burst end for every shooter that is mid-burst and
    /// fired no automatic round this tick, and plays the tail for the ones the
    /// sound player says have genuinely stopped — design section 10's
    /// loop-plus-tail model.
    /// </summary>
    /// <remarks>
    /// The report is made on every quiet tick rather than only the first one,
    /// and a shooter leaves <see cref="_shootersMidBurst"/> only when
    /// <see cref="SandataSoundPlayer.HandleAutomaticFireStopped"/> answers
    /// <see langword="true"/>. That is decision D4's client half: the window
    /// that tells a gap between rounds from the end of a burst lives in the
    /// player, in one place, and this method's job is to keep asking until it
    /// answers. A shooter that can no longer be resolved to an operator is
    /// dropped in the same pass — it will never report another round, and
    /// carrying it forever would leak.
    /// </remarks>
    private void SoundAutomaticFireStops(long executedTick, ImmutableArray<ulong> automaticShootersThisTick)
    {
        var quietShooters = AutomaticBurstTracking.QuietShooters(
            _shootersMidBurst, automaticShootersThisTick);

        var endedThisTick = ImmutableArray.CreateBuilder<ulong>();
        foreach (var shooterEntityId in quietShooters)
        {
            if (!TryFindOperator(shooterEntityId, out var shooter))
            {
                endedThisTick.Add(shooterEntityId);
                continue;
            }

            var burstEnded = _soundPlayer.HandleAutomaticFireStopped(
                FirearmCatalog.Rows[(int)shooter.Firearm].Caliber,
                RangeToNearestHostileWu(shooter),
                shooterIsIndoors: IsShooterIndoors(shooter),
                suppressorFitted: false,
                executedTick,
                shooterEntityId);

            if (burstEnded)
            {
                endedThisTick.Add(shooterEntityId);
            }
        }

        _shootersMidBurst = AutomaticBurstTracking.NextMidBurst(
            _shootersMidBurst, automaticShootersThisTick, endedThisTick.ToImmutable());
    }

    /// <summary>
    /// Maps the <see cref="FireModeSet"/> value the simulation chose, carried
    /// on <c>MissionEventKind.ShotFired</c>'s reason code, to the audio
    /// catalog's own <see cref="FireMode"/> axis.
    /// </summary>
    /// <remarks>
    /// Two enums rather than one is deliberate and predates this method:
    /// <see cref="FireModeSet"/> is a <c>Sandata.Core</c> flags enum naming a
    /// weapon's selector options, and <see cref="FireMode"/> is a client audio
    /// enum whose members also cover mechanism and casing sounds a weapon
    /// selector has no concept of. This is the seam between them.
    /// <para>
    /// Anything that is not one of the four firing modes maps to
    /// <see cref="FireMode.Single"/>. <c>FireModeSelection.SelectMode</c>
    /// already guarantees it never returns a combination or
    /// <see cref="FireModeSet.Safe"/>, so that fallback is unreachable through
    /// the simulation and exists for the malformed-value case alone.
    /// </para>
    /// </remarks>
    internal static FireMode ToAudioFireMode(FireModeSet mode) => mode switch
    {
        FireModeSet.Auto => FireMode.Auto,
        FireModeSet.Burst3 => FireMode.Burst3,
        FireModeSet.Burst2 => FireMode.Burst2,
        _ => FireMode.Single,
    };

    private bool TryFindOperator(ulong entityId, out OperatorState found)
    {
        foreach (var operatorState in _simulation.State.Operators)
        {
            if (operatorState.EntityId == entityId)
            {
                found = operatorState;
                return true;
            }
        }

        found = default!;
        return false;
    }

    /// <summary>
    /// The distance from <paramref name="shooter"/> to the nearest living
    /// operator of another faction, in whole world units. This is
    /// presentation-side arithmetic feeding a sound choice, never a
    /// simulation decision, so ordinary floating point is fine here in a way
    /// it would not be inside <c>Sandata.Core</c>.
    /// </summary>
    private int RangeToNearestHostileWu(OperatorState shooter)
    {
        var nearestSquared = double.MaxValue;
        foreach (var candidate in _simulation.State.Operators)
        {
            if (candidate.Faction == shooter.Faction ||
                !DamageResolution.IsAlive(candidate.Health))
            {
                continue;
            }

            var deltaX = candidate.PositionX.ToDouble() - shooter.PositionX.ToDouble();
            var deltaY = candidate.PositionY.ToDouble() - shooter.PositionY.ToDouble();
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared < nearestSquared)
            {
                nearestSquared = distanceSquared;
            }
        }

        return nearestSquared == double.MaxValue
            ? 0
            : (int)Math.Sqrt(nearestSquared);
    }

    /// <summary>
    /// Whether <paramref name="shooter"/>'s committed position reads as
    /// enclosed by wall geometry under <see cref="IndoorPresence.IsIndoors"/>,
    /// against the same baked <see cref="_navGrid"/> and
    /// <see cref="_wallBuckets"/> every other geometry query in this class
    /// already reads.
    /// </summary>
    private bool IsShooterIndoors(OperatorState shooter) =>
        IndoorPresence.IsIndoors(
            WorldUnits.FromFixedPoint(shooter.PositionX),
            WorldUnits.FromFixedPoint(shooter.PositionY),
            _navGrid,
            _wallBuckets);

    /// <summary>
    /// Writes one <see cref="LogEvents.SimSandataWeaponState"/> line for every
    /// <c>WeaponLowered</c> or <c>WeaponRaised</c> event the tick just
    /// executed emitted, so a run leaves behind a record of a transition that
    /// is about half a second of screen time on a fourteen-pixel operator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The simulation has emitted both event kinds since 2026-08-12 and
    /// nothing anywhere read them: there was no <c>LogEvents</c> constant,
    /// both <c>EventFeed</c> readers filter to <c>ShotFired</c>, and
    /// <c>SandataEventLog</c> is never instantiated. Three debug logs from the
    /// 2026-08-14 smoke session contain zero weapon events, which is what that
    /// wiring predicts. This method is decision D3 of that day's design.
    /// </para>
    /// <para>
    /// <c>Debug</c> rather than <c>Trace</c>: the transition fires a handful
    /// of times in a whole mission, not once per tick. The level and channel
    /// are tested before the feed is walked, so a <c>Release</c> run does no
    /// work and allocates nothing, and the feed being walked is one the frame
    /// already holds rather than a new question asked of the simulation.
    /// </para>
    /// </remarks>
    private void LogWeaponStateTransitionsOn(long executedTick)
    {
        if (!_log.IsEnabledFor(LogLevel.Debug, LogChannel.Simulation))
        {
            return;
        }

        var feed = _simulation.State.EventFeed.Events;
        if (feed.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var missionEvent in feed)
        {
            if (missionEvent.Tick != executedTick)
            {
                continue;
            }

            if (missionEvent.Kind != MissionEventKind.WeaponLowered &&
                missionEvent.Kind != MissionEventKind.WeaponRaised)
            {
                continue;
            }

            _log.SetTick(executedTick);
            _log.Write(
                LogLevel.Debug, LogChannel.Simulation, LogEvents.SimSandataWeaponState,
                "entityId", missionEvent.SubjectId,
                "lowered", missionEvent.Kind == MissionEventKind.WeaponLowered);
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
        _shootersMidBurst = ImmutableArray<ulong>.Empty;

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
    /// <para>
    /// A plain click is routed to <see cref="UI.MultiSelectState.FromClick"/>
    /// instead, and until 2026-08-11 it was not routed anywhere. Every click
    /// went through the marquee path, and a click's marquee is zero pixels
    /// wide and zero pixels tall — <c>Rectangle.Contains</c> is false for
    /// every point inside a degenerate rectangle, so clicking an operator
    /// selected nothing, every time, with no error to say so. That is what a
    /// tester meant by "i cannot click any operators".
    /// </para>
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
                _multiSelect = marqueeBounds is { Width: 0, Height: 0 }
                    ? MultiSelectState.FromClick(
                        mouseState.Position, OperatorPickRadiusPixels, candidates)
                    : MultiSelectState.FromMarquee(marqueeBounds, candidates);

                _log.Write(
                    LogLevel.Debug, LogChannel.Input, LogEvents.InputPointer,
                    "action", marqueeBounds is { Width: 0, Height: 0 } ? "click" : "marquee",
                    "x", mouseState.Position.X,
                    "y", mouseState.Position.Y,
                    "candidates", candidates.Length,
                    "selected", _multiSelect.SelectedEntityIds.Length);
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
        var entriesBeforeSubmission = _orderQueueEntries;

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

        LogOrderSubmission(
            OrderKind.MoveAlongPath, addressees.Length, entriesBeforeSubmission, _orderQueueEntries);
    }

    /// <summary>
    /// Writes one line per order submission — the only record a session leaves
    /// of an order having been submitted at all. A submission that produced no
    /// new order queue entry produced no order either (an empty selection, per
    /// <see cref="SubmitDrawnPath"/>'s own refusal), and is not logged.
    /// </summary>
    /// <remarks>
    /// A rejection is a <c>warn</c> rather than a <c>dbg</c>, and names its
    /// <see cref="OrderRejectReason"/>. Design section 16 requires rejection to
    /// be observable, and on the shipped <c>angle-house</c> map it is the
    /// likely outcome of the smoke checklist's own instruction to right-click
    /// points "across the map": that map is a house, the wall-crossing rule is
    /// section 16's third, and a polyline drawn across a house without regard
    /// to its walls crosses one. Before this line existed the whole event was
    /// invisible in the log a tester was told to attach.
    /// </remarks>
    private void LogOrderSubmission(
        OrderKind kind,
        int addresseeCount,
        ImmutableArray<OrderQueueView.Entry> before,
        ImmutableArray<OrderQueueView.Entry> after)
    {
        var beforeLength = before.IsDefault ? 0 : before.Length;
        if (after.IsDefault || after.Length <= beforeLength)
        {
            return;
        }

        var entry = after[^1];

        if (entry.IsRejected)
        {
            _log.Write(
                LogLevel.Warning, LogChannel.Input, LogEvents.InputSandataOrder,
                "kind", kind.ToString(),
                "addressees", addresseeCount,
                "targetTick", entry.TargetTick,
                "orderId", entry.OrderId,
                "accepted", false,
                "rejectReason", entry.RejectReason?.ToString() ?? "unknown");
            return;
        }

        _log.Write(
            LogLevel.Debug, LogChannel.Input, LogEvents.InputSandataOrder,
            "kind", kind.ToString(),
            "addressees", addresseeCount,
            "targetTick", entry.TargetTick,
            "orderId", entry.OrderId,
            "accepted", true);
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
            var entriesBeforeSubmission = _orderQueueEntries;

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

            LogOrderSubmission(
                OrderKind.GoCodeRelease, addressees.Length, entriesBeforeSubmission, _orderQueueEntries);
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

        // Point sampling, not the parameterless Begin's LinearClamp default.
        // It made no difference at all while every draw was a 1x1 white pixel
        // stretched into a rectangle, and it started mattering the moment the
        // weapon sprites landed: a 32-pixel-wide silhouette drawn at ten
        // pixels under bilinear filtering is smeared into an indistinct
        // smudge, which is exactly what "still the guns are unclear" was
        // describing. Text is unaffected either way, because it is drawn at
        // scale 1 from a whole-pixel origin, so every texel already lands on
        // exactly one pixel.
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);

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

        // Under the pawns on purpose. A route is the ground a squad will walk
        // over, and drawing it above the operators cut a line straight through
        // every one of them.
        DrawPublishedPaths(spriteBatch, contentBounds);
        DrawOperatorsAndFireCones(spriteBatch, contentBounds);
        // After the operators: a tracer that a body drew over would defeat the
        // point of drawing it. Before the order path, which is the player's
        // own input and outranks everything the simulation is saying.
        DrawCombatEffects(spriteBatch, contentBounds);
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

            // Task 10: contact-tier gating applies only to a living hostile.
            // A friendly is always fully drawn, and a casualty stays visible
            // on the terms SD-7a already established below regardless of
            // whether anybody ever identified it alive — this is not fog of
            // war, and hiding a corpse would make it one.
            var appearance = isFriendly || !isAlive
                ? ContactAppearance.Identified
                : ContactAppearanceResolver.ResolveHostileAppearance(
                    operators, AssaultingFaction, operatorState.EntityId);
            if (appearance == ContactAppearance.Hidden)
            {
                continue;
            }

            var isUnknownContact = appearance == ContactAppearance.Unknown;
            var worldPosition = new Vector2(
                (float)operatorState.PositionX.ToDouble(),
                (float)operatorState.PositionY.ToDouble());
            var facing = operatorState.AimAngle;
            var isSelected = _multiSelect.SelectedEntityIds.Contains((int)operatorState.EntityId);
            var weaponClass = FirearmCatalog.Rows[(int)operatorState.Firearm].Class;

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
                // Was operatorState.WeaponChainPhase == WeaponChainPhase.Firing,
                // which is always false: WeaponChain never returns Firing as a
                // phase to hold, so the flash layer below it never drew a
                // pixel and a firefight rendered as nothing at all. The live
                // marks from the ShotFired event feed are the real answer —
                // see CombatFeedback's remarks.
                isFiring: CombatFeedback.IsFiring(_combatEffects, operatorState.EntityId),
                isSelected: isSelected && isAlive,
                // Faction shape, added 2026-08-11. Smoke row SD-7a failed
                // because colour was the only thing separating a friendly from
                // a hostile, which is no separation at all for a colour-blind
                // viewer or for anyone looking at a two-pixel mark. A friendly
                // keeps the square ground ring and gains a head pip; a hostile
                // gets the same ring turned forty-five degrees into a diamond.
                isFriendly: isFriendly,
                weaponClass: weaponClass,
                // Smoke row SD-4: the doorway rule is the mechanical core of
                // the product and, until 2026-08-12, nothing on screen showed
                // it happening. The simulation had computed the condition
                // since the weapon chain landed, but never stored the result,
                // so this flag was a constant false for the whole of every
                // run — see SandataSimulation.AdvanceWeaponChain.
                isWeaponLowered: operatorState.WeaponLowered,
                isUnknownContact: isUnknownContact);

            // A casualty stays on the map and reads as one. Removing the pawn
            // instead would leave a spectator unable to tell a death from an
            // operator who walked out of view, which is exactly the question
            // the fire-cone and roster panels cannot answer either.
            var bodyColor = isAlive
                ? (isUnknownContact
                    ? _theme.Colors.UnknownContact
                    : (isFriendly ? _theme.Colors.Friendly : _theme.Colors.Hostile))
                : _theme.Colors.Downed;

            OperatorRenderer.Draw(
                spriteBatch,
                _pixel!,
                layout,
                bodyColor: bodyColor,
                // The 39-role theme has no dedicated "weapon" role; reusing
                // the operator's own faction color avoids inventing an
                // unlisted 40th role.
                // Gunmetal, not the faction colour. The weapon used to be
                // drawn in exactly the operator's own colour, which meant the
                // gun and the body were one undifferentiated blob at the zoom
                // a spectator actually plays at — "still the guns are unclear"
                // was the report, and it was correct. A downed operator keeps
                // its weapon greyed with the rest of it. An unknown contact
                // never draws a weapon layer at all (isUnknownContact above),
                // so this color is unused for it either way.
                weaponColor: isAlive ? _theme.Colors.Weapon : _theme.Colors.Downed,
                muzzleFlashColor: _theme.Colors.StatusDanger,
                selectionColor: _theme.Colors.SelectedTrooper,
                weaponSprite: weaponClass == WeaponClass.Pistol ? _pistolSprite : _rifleSprite,
                weaponSpriteGripAnchor: weaponClass == WeaponClass.Pistol
                    ? PistolSpriteGripAnchor
                    : RifleSpriteGripAnchor);

            // A dead operator watches nothing, and an unknown contact shows
            // no facing at all (see OperatorGeometry.Create's isUnknownContact
            // remarks) — drawing its fire cone would assert a facing the
            // marker itself does not display.
            if (isAlive && !isUnknownContact)
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
    /// Draws the live tracers and impact marks. Muzzle flashes are not drawn
    /// here: they belong to the operator's own layered geometry and are
    /// already drawn by <see cref="OperatorRenderer"/>, gated on
    /// <see cref="CombatFeedback.IsFiring"/>.
    /// </summary>
    /// <remarks>
    /// Both roles are existing members of the theme contract.
    /// <see cref="SandataThemeColors.StatusWarning"/> carries the tracer and
    /// <see cref="SandataThemeColors.StatusDanger"/> the impact, which is the
    /// same role the muzzle flash already uses — a shot and the wound it
    /// leaves reading as one colour is the intent, not an oversight. No
    /// fortieth role is invented here.
    /// </remarks>
    private void DrawCombatEffects(SpriteBatch spriteBatch, Rectangle contentBounds)
    {
        if (_combatEffects.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var effect in _combatEffects)
        {
            switch (effect.Kind)
            {
                case CombatEffectKind.Tracer:
                    DrawLine(
                        spriteBatch,
                        _camera.WorldToScreen(effect.StartWu, contentBounds),
                        _camera.WorldToScreen(effect.EndWu, contentBounds),
                        _theme.Colors.StatusWarning);
                    break;

                case CombatEffectKind.Impact:
                    DrawImpactMark(spriteBatch, contentBounds, effect.StartWu);
                    break;

                case CombatEffectKind.MuzzleFlash:
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// An impact mark: a small X centred on the operator that lost health.
    /// A cross rather than a dot on purpose — <c>SD-1</c> and <c>SD-7a</c>
    /// both failed on 2026-08-11 because everything on this map is a
    /// rectangle and only colour separates one rectangle from another, so a
    /// new mark that is also a rectangle would add nothing a colour-blind
    /// viewer could use.
    /// </summary>
    private void DrawImpactMark(SpriteBatch spriteBatch, Rectangle contentBounds, Vector2 centreWu)
    {
        const float arm = ImpactMarkArmWu;
        var topLeft = _camera.WorldToScreen(centreWu + new Vector2(-arm, -arm), contentBounds);
        var bottomRight = _camera.WorldToScreen(centreWu + new Vector2(arm, arm), contentBounds);
        var bottomLeft = _camera.WorldToScreen(centreWu + new Vector2(-arm, arm), contentBounds);
        var topRight = _camera.WorldToScreen(centreWu + new Vector2(arm, -arm), contentBounds);

        DrawLine(spriteBatch, topLeft, bottomRight, _theme.Colors.StatusDanger);
        DrawLine(spriteBatch, bottomLeft, topRight, _theme.Colors.StatusDanger);
    }

    /// <summary>
    /// Draws the selected operator's inspector rows. This is the half of
    /// smoke row SD-8 that had nothing to do with clicking: the inspector's
    /// eleven formatted lines have existed and been unit tested since task 44,
    /// and until 2026-08-11 nothing in this client drew a single character of
    /// them, because <c>Sandata.Client</c> had no font.
    /// <para>
    /// With no font loaded, or with nothing selected, this draws nothing and
    /// the panel stays the empty outline it has always been.
    /// </para>
    /// </summary>
    private void DrawOperatorInspectorText(SpriteBatch spriteBatch, Rectangle panelBounds)
    {
        if (_fonts is null || _multiSelect.SelectedEntityIds.IsDefaultOrEmpty)
        {
            return;
        }

        var selectedEntityId = (ulong)_multiSelect.SelectedEntityIds[0];
        if (!TryFindOperator(selectedEntityId, out var selected))
        {
            return;
        }

        var font = _fonts.Get(SandataFontRole.Body);
        var lines = OperatorInspector.BuildLines(BuildInspectorContent(selected));
        var x = panelBounds.Left + OperatorInspector.Margin;
        var y = panelBounds.Top + OperatorInspector.Margin;

        foreach (var line in lines)
        {
            if (y + OperatorInspector.LineHeight > panelBounds.Bottom)
            {
                break;
            }

            spriteBatch.DrawString(font, line, new Vector2(x, y), _theme.Colors.TextPrimary);
            y += OperatorInspector.LineHeight;
        }
    }

    /// <summary>
    /// Fills the inspector's content record for one operator from the state
    /// this client can actually see.
    /// <para>
    /// Four of the eleven rows have no source in <c>MissionState</c> yet and
    /// render their own documented empty form rather than a fabricated value:
    /// the slot index, and the three order-layer rows, whose types design
    /// section 16 has not landed. Cover is <c>NotInCover</c> for the same
    /// reason — <c>OperatorState</c> carries no cover affiliation. The rows
    /// SD-8 actually asks about are all real: the weapon chain phase and its
    /// remaining ticks come straight off the operator, and the path reason
    /// code comes from the operator's own group, found through
    /// <see cref="InitialSquadGroups.BuildMembership"/> because nothing links
    /// an operator to a group otherwise.
    /// </para>
    /// </summary>
    private OperatorInspector.InspectorContent BuildInspectorContent(OperatorState selected)
    {
        var groupId = 0UL;
        foreach (var member in _groupMembership)
        {
            if (member.EntityId == selected.EntityId)
            {
                groupId = member.GroupId;
                break;
            }
        }

        return new OperatorInspector.InspectorContent(
            Intent: selected.Intent,
            ReasonCode: _simulation.GetPublishedPathReasonCode(groupId),
            ChainPhase: (WeaponChainPhase)selected.WeaponChainPhase,
            ChainRemainingTicks: selected.WeaponChainRemainingTicks,
            Cover: CoverState.NotInCover,
            GroupId: groupId,
            SlotIndex: null,
            DecisionPositionX: selected.PositionX,
            DecisionPositionY: selected.PositionY,
            ResolutionPositionX: selected.PositionX,
            ResolutionPositionY: selected.PositionY,
            ActiveOrderId: null,
            OrderNodeIndex: null,
            OrderClearReasonCode: null,
            Firearm: selected.Firearm,
            WeaponLowered: selected.WeaponLowered);
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
    /// Draws the route each autonomous group is actually walking, which
    /// nothing in this client drew before 2026-08-11. Smoke row SD-2 asks a
    /// tester to judge whether a squad's path across the map's 26.57-degree
    /// diagonal wall follows the wall as a straight line rather than as a
    /// staircase, and until this existed the only line on screen was the
    /// player's own right-click polyline, so the row could not be judged by
    /// anyone.
    /// <para>
    /// The two are deliberately not drawn alike, and the difference is a
    /// shape rather than a colour. Both use the <c>OrderPath</c> role, so a
    /// viewer learns one colour for "a route"; the autonomous one is
    /// <b>dashed</b> and carries no waypoint markers, and the player's own is
    /// solid and keeps its waypoint squares. That is the SD-7a lesson applied
    /// a second time — the first attempt drew this line in <c>StatusInfo</c>,
    /// which on the shipped <c>night-ops</c> palette is within a few points of
    /// the friendly operator blue, so a route and the warrior walking it were
    /// the same colour.
    /// </para>
    /// <para>
    /// The polyline is re-fetched every frame and never stored. It is derived
    /// state — not hashed, not snapshotted, recomputed from its stored request
    /// on resume — and caching it here would be caching something the
    /// simulation is entitled to recompute differently.
    /// </para>
    /// </summary>
    private void DrawPublishedPaths(SpriteBatch spriteBatch, Rectangle contentBounds)
    {
        var groups = _simulation.State.Groups;
        if (groups.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var group in groups)
        {
            var published = _simulation.GetPublishedPath(group.GroupId);
            if (published.Length < 2)
            {
                continue;
            }

            var waypointsWu = ImmutableArray.CreateBuilder<Vector2>(published.Length);
            foreach (var point in published)
            {
                waypointsWu.Add(new Vector2(point.X, point.Y));
            }

            var worldSegments = OrderPathOverlay.CreateWorldSegments(waypointsWu.MoveToImmutable());
            var screenSegments = OrderPathOverlay.ToScreenSegments(worldSegments, _camera, contentBounds);
            foreach (var segment in screenSegments)
            {
                DrawDashedLine(spriteBatch, segment.Start, segment.End, _theme.Colors.OrderPath);
            }
        }
    }

    /// <summary>
    /// Draws a line as a run of short dashes, in screen pixels, so that a
    /// dashed route and a solid route can be told apart with colour ignored.
    /// The dash length is fixed in pixels rather than in world units on
    /// purpose: it is a legibility device for the person looking at the
    /// screen, so it must not stretch or collapse with zoom.
    /// </summary>
    private void DrawDashedLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color)
    {
        const float dashLengthPixels = 8f;
        const float gapLengthPixels = 6f;

        var direction = end - start;
        var length = direction.Length();
        if (length <= float.Epsilon)
        {
            return;
        }

        direction /= length;
        for (var travelled = 0f; travelled < length; travelled += dashLengthPixels + gapLengthPixels)
        {
            var dashEnd = MathF.Min(travelled + dashLengthPixels, length);
            DrawLine(
                spriteBatch,
                start + (direction * travelled),
                start + (direction * dashEnd),
                color);
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
        DrawOperatorInspectorText(spriteBatch, layout.OperatorInspector);

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
    /// Draws one row per <see cref="_orderQueueEntries"/> entry: the row's
    /// line of text where a font is loaded, and the bare coloured rectangle
    /// where one is not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rectangle-only form was the whole of this method until 2026-08-12,
    /// under the "no font pipeline, so a row is a coloured rectangle"
    /// precedent <see cref="DrawGoCodeRows"/> still follows. That precedent
    /// expired for this panel specifically: the client has baked fonts since
    /// 2026-08-11, and this is the one panel whose content a player has to be
    /// able to read to understand why nothing happened. A rejected drawn path
    /// now names the rule it broke —
    /// <see cref="UI.OrderQueueView.FormatEntryLine"/> writes
    /// "rejected: SegmentCrossesWall" — where before it changed the colour of
    /// a bar in a panel with no legend.
    /// </para>
    /// <para>
    /// The rectangle is still drawn underneath the text rather than replaced
    /// by it, at the row colour <see cref="UI.OrderQueueView.ResolveEntryColor"/>
    /// resolves, so a rejection stays distinguishable at a glance from across
    /// the room and the row is still legible without reading it.
    /// </para>
    /// </remarks>
    private void DrawOrderQueueRows(SpriteBatch spriteBatch, Rectangle panelBounds)
    {
        var font = _fonts?.Get(SandataFontRole.Body);

        for (var index = 0; index < _orderQueueEntries.Length; index++)
        {
            var entry = _orderQueueEntries[index];
            var rowBounds = OrderQueueView.CalculateRowBounds(panelBounds, index);
            var color = OrderQueueView.ResolveEntryColor(_theme.Colors, entry);

            if (font is null)
            {
                spriteBatch.Draw(_pixel, rowBounds, color);
                continue;
            }

            if (rowBounds.Bottom > panelBounds.Bottom)
            {
                break;
            }

            spriteBatch.Draw(_pixel, rowBounds, _theme.Colors.PanelAlternate);
            spriteBatch.DrawString(
                font,
                OrderQueueView.FormatEntryLine(entry),
                new Vector2(rowBounds.Left + OrderQueueView.Margin, rowBounds.Top),
                color);
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
        _groupMembership = InitialSquadGroups.BuildMembership(
            initialState.Operators,
            _objectiveRecords,
            _navGrid,
            SandataRuleset.ModernTacticalV1.GroupCohesionRadiusWu,
            AssaultingFaction);

        // The room layout is what makes the squad sweep rather than stop after
        // its first objective. It is derived from the baked map, so it is a
        // derived structure: never hashed, never snapshotted, and rebuilt here
        // on every restart alongside the simulation it belongs to.
        //
        // Passing it is not optional in practice even though the parameter is.
        // Without it SandataSimulation seeds no RoomClearStates, TrySelectNextRoom
        // finds nothing, and the whole sweep is silently inert while every one of
        // its tests still passes.
        var roomLayout = RoomLayout.Bake(_navGrid, _wallRecords, _doorRecords);

        return new SandataSimulation(
            _mission, SandataRuleset.ModernTacticalV1, _navGrid, _wallBuckets, initialState, _coverRecords,
            roomLayout);
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
                Health: PlaceholderOperatorHealth,
                Faction: spawn.Faction,
                Intent: 0,
                IsCrouched: false,
                WeaponLowered: false,
                WeaponChainPhase: 0,
                WeaponChainRemainingTicks: 0,
                MagazineRounds: 30,
                CyclicFireAccumulator: 0,
                SuppressionCounter: 0)
            {
                Firearm = LoadoutForIndex(index),
            });
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
    /// Which weapon the operator at <paramref name="index"/> carries.
    /// <c>MissionState.Firearm</c>'s own remarks say plainly that what decides
    /// an operator's weapon is undesigned, and this does not design it. It is
    /// the same placeholder shape <c>HeadlessRunner.LoadoutForIndex</c>
    /// already uses, added here because a client where every operator fell to
    /// the same default rifle made smoke row SD-4 — rifle silhouette versus
    /// pistol silhouette — unrunnable by anyone.
    /// <para>
    /// It alternates rather than taking the headless runner's every-fourth
    /// rule, and that difference is deliberate. The shipped <c>angle-house</c>
    /// mission fields four operators in total, two per side, so every fourth
    /// would arm exactly one of them, and that one is a defender standing
    /// still on an objective. SD-4 asks a tester to watch a pistol operator
    /// cross a doorway, which needs the pistol on somebody who walks. When a
    /// real loadout model lands, both copies go.
    /// </para>
    /// </summary>
    private static FirearmId LoadoutForIndex(int index) =>
        index % 2 == 1 ? FirearmId.Glock17Gen5 : FirearmId.Ak47;

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

    /// <summary>
    /// Loads the whole shipped theme catalog, ordered with
    /// <c>catalog.DefaultThemeId</c>'s theme first so index 0 is always what
    /// a run opens on, exactly as the single-theme <c>LoadTheme</c> this
    /// replaced did. On any load or validation failure the returned array
    /// holds only the single hardcoded fallback theme, so <see cref="_theme"/>
    /// and F6 cycling both still have exactly one theme to work with.
    /// </summary>
    private ImmutableArray<SandataTheme> LoadThemes()
    {
        var catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "Themes",
            "sandata-theme-standards.json");

        try
        {
            var catalog = SandataThemeCatalog.Load(catalogPath);
            if (catalog.TryGet(catalog.DefaultThemeId, out var defaultTheme))
            {
                _log.Write(
                    LogLevel.Information,
                    LogChannel.Assets,
                    LogEvents.AssetsThemeLoaded,
                    "themeId",
                    defaultTheme.Id);

                var ordered = ImmutableArray.CreateBuilder<SandataTheme>(catalog.Themes.Count);
                ordered.Add(defaultTheme);
                foreach (var theme in catalog.Themes)
                {
                    if (!string.Equals(theme.Id, defaultTheme.Id, StringComparison.Ordinal))
                    {
                        ordered.Add(theme);
                    }
                }

                return ordered.ToImmutable();
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

        return ImmutableArray.Create(
            new SandataTheme("fallback", "Fallback", FallbackThemeColors, FallbackThemeMetrics));
    }

    /// <summary>
    /// Task 9's F6 cycle helper: a pure function over the catalog's own
    /// theme order and the currently displayed id, with no
    /// <c>GraphicsDevice</c> and no keyboard state, so
    /// <c>tests/Sandata.Client.Tests</c> can pin it directly. Wraps from the
    /// last theme back to the first; returns <paramref name="currentId"/>
    /// unchanged when <paramref name="themes"/> has one entry or fewer, and
    /// returns the first theme's id when <paramref name="currentId"/> is not
    /// found at all (defensive only — every real caller passes an id this
    /// same list produced).
    /// </summary>
    internal static string NextThemeId(IReadOnlyList<SandataTheme> themes, string currentId)
    {
        if (themes.Count <= 1)
        {
            return currentId;
        }

        var currentIndex = -1;
        for (var index = 0; index < themes.Count; index++)
        {
            if (string.Equals(themes[index].Id, currentId, StringComparison.Ordinal))
            {
                currentIndex = index;
                break;
            }
        }

        var nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % themes.Count;
        return themes[nextIndex].Id;
    }

    /// <summary>
    /// F6 cycles <see cref="_theme"/> through <see cref="_themes"/> — not a
    /// letter key, and not persisted: see the key's own remarks at its
    /// <see cref="Keys.F6"/> check in <see cref="UpdateTransportControls"/>'s
    /// sibling call site in <see cref="Update"/>. Sandata has no settings
    /// file, so the choice reverts to the catalog default on the next run.
    /// </summary>
    private void UpdateThemeSwitch(KeyboardState keyboardState)
    {
        if (!WasJustPressed(keyboardState, Keys.F6))
        {
            return;
        }

        var nextId = NextThemeId(_themes, _theme.Id);
        foreach (var theme in _themes)
        {
            if (!string.Equals(theme.Id, nextId, StringComparison.Ordinal))
            {
                continue;
            }

            _theme = theme;
            _log.SetTick(_nextTick);
            _log.Write(
                LogLevel.Information,
                LogChannel.Assets,
                LogEvents.AssetsThemeLoaded,
                "themeId",
                _theme.Id);
            break;
        }
    }

}
