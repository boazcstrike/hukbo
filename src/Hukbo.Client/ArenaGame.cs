using Hukbo.Client.Presentation;
using Hukbo.Client.Rendering;
using Hukbo.Client.UI;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client;

public sealed class ArenaGame : Game
{
    private const int InitialWindowWidth = 1280;
    private const int InitialWindowHeight = 720;
    private const int StatusBarHeight = 68;
    private const int EventPanelWidth = 350;
    private const int LayoutMargin = 12;
    private const int LayoutGap = 10;
    private const int InspectorWidth = 310;
    private const int InspectorHeight = 230;
    private const int EventHistoryCapacity = 200;
    private const int MaximumSafeRawCoordinate =
        Scenario.MaximumMapDimension * FixedPoint.Scale;
    private const ulong DefaultSeed = 1;
    private const int DefaultAgentCount = 200;
    private const double MaximumAccumulatedSeconds = 0.5;

    private static readonly Color BackgroundColor = new(8, 13, 22);
    private static readonly Color MapColor = new(19, 29, 43);
    private static readonly Color MapBorderColor = new(58, 76, 98);
    private static readonly Color FactionOneColor = new(64, 164, 255);
    private static readonly Color FactionTwoColor = new(255, 91, 105);
    private static readonly Color OtherFactionColor = new(231, 199, 84);
    private static readonly Color StatusBarColor = new(12, 19, 30);

    private readonly GraphicsDeviceManager _graphics;
    private readonly InputEdges _input = new();
    private readonly MenuOverlay _menu = new();
    private readonly ControlBar _controlBar = new();
    private readonly AgentInspectorPanel _inspectorPanel = new();
    private readonly BattleEventLogPanel _eventLogPanel = new();
    private readonly MatchSummaryPanel _summaryPanel = new();
    private readonly PresentationCoordinator _presentation =
        new(EventHistoryCapacity);
    private readonly AgentSelection _hoverSelection = new();
    private readonly MatchSeries _matchSeries = new(DefaultSeed);

    private Scenario _scenario;
    private BattleSimulation _simulation;
    private SpectatorCamera _camera;
    private SpriteBatch? _spriteBatch;
    private RasterizerState? _arenaRasterizerState;
    private Texture2D? _pixel;
    private SpriteFont? _font;
    private bool _exitRequested;
    private int _speedMultiplier = 1;
    private double _simulationAccumulator;

    public ArenaGame()
    {
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

        _scenario = Scenario.CreateDefault(
            _matchSeries.CurrentSeed,
            DefaultAgentCount);
        _simulation = BattleSimulation.Create(_scenario);
        _camera = new SpectatorCamera(_scenario.MapWidth, _scenario.MapHeight);
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
        _font = Content.Load<SpriteFont>("Default");

        _camera.Fit(GetLayout(GraphicsDevice.Viewport.Bounds).ArenaBounds);
    }

    protected override void UnloadContent()
    {
        _pixel?.Dispose();
        _arenaRasterizerState?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        _input.Update();
        _presentation.AdvanceEffects(
            (float)gameTime.ElapsedGameTime.TotalSeconds);
        var screenBounds = GraphicsDevice.Viewport.Bounds;
        var layout = GetLayout(screenBounds);

        if (_input.WasPressed(Keys.Escape))
        {
            ToggleMenu();
        }

        var pointerConsumed = false;
        if (_menu.IsVisible)
        {
            var menuInteraction = _menu.Update(_input, screenBounds);
            pointerConsumed = menuInteraction.PointerConsumed;
            ApplyClientCommand(menuInteraction.Command);
        }
        else
        {
            var interaction = _summaryPanel.Update(
                _input,
                _presentation.Summary,
                layout.ArenaBounds);
            pointerConsumed = interaction.PointerConsumed;

            if (!pointerConsumed)
            {
                interaction = _controlBar.Update(
                    _input,
                    screenBounds,
                    _presentation.Playback.IsPlaying);
                pointerConsumed = interaction.PointerConsumed;
            }

            if (!pointerConsumed)
            {
                interaction = _eventLogPanel.Update(
                    _input,
                    _presentation.EventFeed,
                    layout.EventBounds);
                pointerConsumed = interaction.PointerConsumed;
            }

            if (!pointerConsumed)
            {
                interaction = _inspectorPanel.Update(
                    _input,
                    _presentation.Selection.Resolve(_simulation.Agents),
                    layout.InspectorBounds);
                pointerConsumed = interaction.PointerConsumed;
            }

            var command = interaction.Command == ClientCommand.None
                ? GetSpectatorKeyboardCommand()
                : interaction.Command;
            ApplyClientCommand(command);
            HandleSpeedInput();
            HandleArenaSelection(layout.ArenaBounds, pointerConsumed);
            _camera.Update(
                _input,
                (float)gameTime.ElapsedGameTime.TotalSeconds,
                allowZoom: !pointerConsumed);
        }

        AdvanceSimulation(gameTime.ElapsedGameTime.TotalSeconds);
        UpdateWindowTitle();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BackgroundColor);

        if (_spriteBatch is null ||
            _arenaRasterizerState is null ||
            _pixel is null ||
            _font is null)
        {
            return;
        }

        var screenBounds = GraphicsDevice.Viewport.Bounds;
        var layout = GetLayout(screenBounds);
        _camera.Fit(layout.ArenaBounds);
        UpdateHoverSelection(layout.ArenaBounds);

        var selectedAgent =
            _presentation.Selection.Resolve(_simulation.Agents);

        if (layout.ArenaBounds.Width > 0 &&
            layout.ArenaBounds.Height > 0)
        {
            GraphicsDevice.ScissorRectangle = layout.ArenaBounds;
            _spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                _arenaRasterizerState);
            DrawArena(
                _spriteBatch,
                _pixel,
                layout.ArenaBounds);
            _spriteBatch.End();
        }

        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp);
        DrawStatus(
            _spriteBatch,
            _pixel,
            _font,
            screenBounds);
        _controlBar.Draw(
            _spriteBatch,
            _pixel,
            _font,
            screenBounds,
            _presentation.Playback.IsPlaying);
        _inspectorPanel.Draw(
            _spriteBatch,
            _pixel,
            _font,
            selectedAgent,
            layout.InspectorBounds);
        _eventLogPanel.Draw(
            _spriteBatch,
            _pixel,
            _font,
            _presentation.EventFeed,
            layout.EventBounds);
        _summaryPanel.Draw(
            _spriteBatch,
            _pixel,
            _font,
            _presentation.Summary,
            layout.ArenaBounds);
        _menu.Draw(
            _spriteBatch,
            _pixel,
            _font,
            screenBounds);

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private ClientCommand GetSpectatorKeyboardCommand()
    {
        if (_input.WasPressed(Keys.R))
        {
            return _input.IsDown(Keys.LeftShift) ||
                _input.IsDown(Keys.RightShift)
                ? ClientCommand.FullReset
                : ClientCommand.NextRound;
        }

        if (_input.WasPressed(Keys.Space))
        {
            return _presentation.Playback.IsPlaying
                ? ClientCommand.Pause
                : ClientCommand.Play;
        }

        return ClientCommand.None;
    }

    private void HandleSpeedInput()
    {
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

    private void ApplyClientCommand(ClientCommand command)
    {
        switch (command)
        {
            case ClientCommand.None:
                return;
            case ClientCommand.Play:
                if (_simulation.Outcome == BattleOutcome.Ongoing)
                {
                    _presentation.Playback.Play();
                }

                _simulationAccumulator = 0;
                _menu.Close();
                return;
            case ClientCommand.Pause:
                _presentation.Playback.Pause();
                _simulationAccumulator = 0;
                return;
            case ClientCommand.OpenMenu:
                _presentation.Playback.Pause();
                _simulationAccumulator = 0;
                _menu.Open();
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
            _presentation.IngestTick(
                _simulation.LastEvents,
                _simulation.Agents);
            _simulationAccumulator -= secondsPerTick;
        }

        if (_simulation.Outcome != BattleOutcome.Ongoing)
        {
            CompleteMatch();
        }
    }

    private void CompleteMatch()
    {
        _presentation.ProcessTerminal(
            _simulation.Outcome,
            _simulation.Agents,
            _simulation.Tick,
            _scenario.TickRate,
            _scenario.Seed);
        _simulationAccumulator = 0;
    }

    private void ResetSimulation(ClientCommand resetCommand)
    {
        if (resetCommand == ClientCommand.FullReset)
        {
            _matchSeries.FullReset();
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

        _scenario = Scenario.CreateDefault(
            _matchSeries.CurrentSeed,
            DefaultAgentCount);
        _simulation = BattleSimulation.Create(_scenario);
        _presentation.ResetFor(resetCommand);
        _hoverSelection.Clear();
        _simulationAccumulator = 0;
        _menu.Close();

        if (resetCommand == ClientCommand.FullReset)
        {
            _speedMultiplier = 1;
            _camera = new SpectatorCamera(
                _scenario.MapWidth,
                _scenario.MapHeight);
            _camera.Fit(GetLayout(GraphicsDevice.Viewport.Bounds).ArenaBounds);
        }
    }

    private void DrawArena(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle arenaBounds)
    {
        var topLeft = _camera.WorldToScreen(Vector2.Zero, arenaBounds);
        var bottomRight = _camera.WorldToScreen(
            new Vector2(_scenario.MapWidth, _scenario.MapHeight),
            arenaBounds);
        var mapBounds = RectangleFromPoints(topLeft, bottomRight);
        var visibleMapBounds = Rectangle.Intersect(mapBounds, arenaBounds);

        if (visibleMapBounds.Width > 0 && visibleMapBounds.Height > 0)
        {
            spriteBatch.Draw(pixel, visibleMapBounds, MapColor);
            DrawBorder(
                spriteBatch,
                pixel,
                visibleMapBounds,
                MapBorderColor);
        }

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
            var appearance = PawnAppearanceFactory.Create(agent.EntityId);
            var visualBounds = PawnRenderer.GetBounds(
                footAnchor,
                _camera.Zoom,
                appearance);

            if (!arenaBounds.Intersects(visualBounds))
            {
                continue;
            }

            var visualState = agent.EntityId == selectedEntityId
                ? PawnVisualState.Selected
                : agent.EntityId == hoveredEntityId
                    ? PawnVisualState.Hovered
                    : PawnVisualState.Normal;
            PawnRenderer.Draw(
                spriteBatch,
                pixel,
                footAnchor,
                _camera.Zoom,
                appearance,
                GetFactionColor(agent.FactionId),
                visualState,
                hitPulseStrength:
                    _presentation.HitEffects.GetPulseStrength(
                        agent.EntityId));
        }

        HitEffectRenderer.Draw(
            _presentation.HitEffects.ActiveEffects,
            _camera,
            arenaBounds,
            _camera.Zoom,
            spriteBatch,
            pixel);
    }

    private void DrawStatus(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        Rectangle screenBounds)
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

        var statusBounds = new Rectangle(
            screenBounds.Left,
            screenBounds.Top,
            screenBounds.Width,
            Math.Min(StatusBarHeight, screenBounds.Height));
        spriteBatch.Draw(pixel, statusBounds, StatusBarColor);

        var state = _presentation.Playback.IsPlaying ? "PLAYING" : "PAUSED";
        var status =
            $"{state}  |  Tick {_simulation.Tick:N0}  |  {_speedMultiplier}x  |  " +
            $"Team A (Blue) {_matchSeries.TeamAWins}W/{factionZeroAlive} alive  |  " +
            $"Team B (Red) {_matchSeries.TeamBWins}W/{factionOneAlive} alive  |  " +
            $"{_simulation.Outcome}";
        spriteBatch.DrawString(
            font,
            status,
            new Vector2(18, 12),
            Color.White,
            0f,
            Vector2.Zero,
            0.78f,
            SpriteEffects.None,
            0f);
        spriteBatch.DrawString(
            font,
            "Click: select  |  Wheel: zoom/log scroll  |  " +
            "Space: play/pause  |  1/2/4: speed  |  " +
            "R: next round  |  Shift+R: full reset  |  Esc: menu",
            new Vector2(18, 39),
            new Color(162, 178, 196),
            0f,
            Vector2.Zero,
            0.62f,
            SpriteEffects.None,
            0f);
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

    private static ClientLayout GetLayout(Rectangle screenBounds)
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
        var eventBounds = new Rectangle(
            Math.Max(
                screenBounds.Left,
                screenBounds.Right - eventWidth - LayoutMargin),
            contentTop,
            eventWidth,
            contentHeight);
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
        Color color)
    {
        const int thickness = 2;
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

    private static Color GetFactionColor(int factionId) =>
        factionId switch
        {
            0 => FactionOneColor,
            1 => FactionTwoColor,
            _ => OtherFactionColor,
        };

    private readonly record struct ClientLayout(
        Rectangle ArenaBounds,
        Rectangle EventBounds,
        Rectangle InspectorBounds);
}
