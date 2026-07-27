using System.Collections.Immutable;
using System.Globalization;
using Hukbo.Client.Audio;
using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Client.Settings;
using Hukbo.Client.Theming;
using Hukbo.Client.UI;
using Hukbo.Core.Mathematics;
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
    // `SoundLogPanel.Layout.cs:CaptionLineSpacing`. `SoundLogHeightPercent`
    // of the default 1280x720 window's 640px column height must clear the
    // header, the path line, nine binding rows, and the three
    // minimum-reserved cue rows with zero slack, which needs a real panel
    // height of 396px; 640 * 62 / 100 == 396 exactly under integer
    // division. `SoundLogMinimumHeight` is the analogous floor for a
    // shorter window — header, path, one binding row, and the three
    // reserved cue rows — which stays comfortably below the percentage
    // figure at the default window, so the percentage branch keeps
    // deciding there.
    private const int SoundLogMinimumHeight = 236;
    private const int SoundLogHeightPercent = 62;
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
    private readonly PresentationCoordinator _presentation =
        new(EventHistoryCapacity);
    private readonly AgentSelection _hoverSelection = new();
    private readonly ArenaAutoPanController _autoPan = new();
    private readonly MatchSeries _matchSeries = new(DefaultSeed);
    private readonly ClientSettingsStore _settingsStore;
    private readonly GoreIntensityManager _goreManager;
    private readonly ArmyCompositionPanel _armyCompositionPanel;
    private readonly DiagnosticLog _log;

    private Scenario _scenario;
    private BattleSimulation _simulation;
    private SpectatorCamera _camera;
    private ImmutableArray<PlainsDecal> _plainsDecals;
    private SpriteBatch? _spriteBatch;
    private RasterizerState? _arenaRasterizerState;
    private Texture2D? _pixel;
    private UiFontSet? _fonts;
    private MonoGameSoundPlayer? _soundPlayer;
    private Settings.ArmyComposition _activeComposition;
    private bool _isSoundLogVisible;
    private bool _isArmyCompositionPanelVisible;
    private bool _isCompositionStaged;
    private bool _exitRequested;
    private int _speedMultiplier = 1;
    private double _simulationAccumulator;

    // CompleteMatch runs on every frame that follows a decided match, so the
    // outcome line needs its own guard or the log fills with one identical row
    // per frame until the spectator resets.
    private long _loggedOutcomeTick = -1;
    private string _lastFocusTarget = string.Empty;

    /// <param name="log">
    /// The debug log every subsystem in the client writes through. Optional so
    /// nothing outside <c>Program</c> has to supply one; defaults to the
    /// no-op log, which is also what a <c>Release</c> build resolves to.
    /// </param>
    public ArenaGame(DiagnosticLog? log = null)
    {
        _log = log ?? DiagnosticLog.Disabled;
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

        // A restored preference takes effect from tick zero, so the spectator
        // never has to reopen the menu after a relaunch.
        _presentation.Blood.Intensity = _goreManager.Value;
        _menu = new MenuOverlay(catalog.Themes, catalog.Standards);
        _activeComposition =
            _settingsStore.Load(catalog.DefaultThemeId).Composition;
        _armyCompositionPanel = new ArmyCompositionPanel(
            ToPanelComposition(_activeComposition),
            catalog.Standards.Shared.ArmyComposition);

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = InitialWindowWidth,
            PreferredBackBufferHeight = InitialWindowHeight,
            SynchronizeWithVerticalRetrace = true,
        };

        Window.AllowUserResizing = true;
        Window.Title = "Hukbo";
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = false;

        _scenario = BuildScenario(_matchSeries.CurrentSeed, _activeComposition);
        _simulation = BattleSimulation.Create(_scenario);
        _camera = new SpectatorCamera(_scenario.MapWidth, _scenario.MapHeight);
        _plainsDecals = PlainsBackdropGeometry.GenerateDecals(
            _scenario.Seed,
            _scenario.MapWidth,
            _scenario.MapHeight);

        LogScenarioBuilt("startup");
    }

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

        _camera.Fit(GetLayout(GraphicsDevice.Viewport.Bounds).ArenaBounds);
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
        _input.Update();
        _soundDirector.BeginFrame(gameTime.ElapsedGameTime.TotalSeconds);
        _presentation.AdvanceEffects(
            (float)gameTime.ElapsedGameTime.TotalSeconds);
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
                _goreManager.Value);
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

            ApplyClientCommand(menuInteraction.Command);
        }
        else
        {
            var interaction = _summaryPanel.Update(
                _input,
                _presentation.Summary,
                layout.ArenaBounds);
            pointerConsumed = interaction.PointerConsumed;
            consumedBy = pointerConsumed ? "matchSummary" : consumedBy;

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
                _settingsStore.TrySave(
                    _themeManager.ActiveTheme.Id,
                    ToSettingsComposition(_armyCompositionPanel.Saved),
                    _settingsStore
                        .Load(_themeManager.ActiveTheme.Id)
                        .GoreIntensity);
                _isCompositionStaged = true;
                _isArmyCompositionPanelVisible = false;
                return;
            default:
                return;
        }
    }

    private static ImmutableArray<int> ToRosterCounts(
        Settings.ArmyComposition composition) =>
        ImmutableArray.Create(
            composition.GreatBladeCount,
            composition.HeavyChopperCount,
            composition.ThrustingBladeCount,
            composition.WorkBladeCount);

    private static UI.ArmyComposition ToPanelComposition(
        Settings.ArmyComposition composition) =>
        new(ToRosterCounts(composition), composition.UnitsPerTeam);

    private static Settings.ArmyComposition ToSettingsComposition(
        UI.ArmyComposition composition) =>
        new(
            composition.UnitsPerTeam,
            composition.CategoryCounts[0],
            composition.CategoryCounts[1],
            composition.CategoryCounts[2],
            composition.CategoryCounts[3]);

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
        _simulationAccumulator = Math.Min(
            _simulationAccumulator + (elapsedSeconds * _speedMultiplier),
            MaximumAccumulatedSeconds);

        while (_simulationAccumulator >= secondsPerTick &&
               _simulation.Outcome == BattleOutcome.Ongoing)
        {
            _simulation.AdvanceOneTick();
            _log.SetTick(_simulation.Tick);
            _presentation.IngestTick(
                _simulation.LastEvents,
                _simulation.Agents);
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
        _log.SetTick(DiagnosticLog.NoTick);
        _log.Write(
            LogLevel.Information,
            LogChannel.Simulation,
            LogEvents.SimReset,
            "kind",
            resetCommand.ToString(),
            "seed",
            _matchSeries.CurrentSeed);
        LogScenarioBuilt(resetCommand.ToString());
        _plainsDecals = PlainsBackdropGeometry.GenerateDecals(
            _scenario.Seed,
            _scenario.MapWidth,
            _scenario.MapHeight);
        _presentation.ResetFor(resetCommand);
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
