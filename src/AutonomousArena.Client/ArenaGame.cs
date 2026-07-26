using AutonomousArena.Core.Mathematics;
using AutonomousArena.Core.Simulation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AutonomousArena.Client;

public sealed class ArenaGame : Game
{
    private const int InitialWindowWidth = 1280;
    private const int InitialWindowHeight = 720;
    private const ulong DefaultSeed = 1;
    private const int DefaultAgentCount = 200;
    private const double MaximumAccumulatedSeconds = 0.5;

    private static readonly Color BackgroundColor = new(8, 13, 22);
    private static readonly Color MapColor = new(19, 29, 43);
    private static readonly Color MapBorderColor = new(58, 76, 98);
    private static readonly Color FactionOneColor = new(64, 164, 255);
    private static readonly Color FactionTwoColor = new(255, 91, 105);
    private static readonly Color OtherFactionColor = new(231, 199, 84);

    private readonly GraphicsDeviceManager _graphics;
    private readonly InputEdges _input = new();
    private readonly MenuOverlay _menu = new();
    private readonly Scenario _scenario;

    private BattleSimulation _simulation;
    private SpectatorCamera _camera;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private SpriteFont? _font;
    private bool _isPlaying = true;
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
        Window.Title = "Autonomous Arena";
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = false;

        _scenario = Scenario.CreateDefault(DefaultSeed, DefaultAgentCount);
        _simulation = BattleSimulation.Create(_scenario);
        _camera = new SpectatorCamera(_scenario.MapWidth, _scenario.MapHeight);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _font = Content.Load<SpriteFont>("Default");

        _camera.Fit(GraphicsDevice.Viewport);
    }

    protected override void UnloadContent()
    {
        _pixel?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        _input.Update();

        if (_input.WasPressed(Keys.Escape))
        {
            ToggleMenu();
        }

        if (_menu.IsVisible)
        {
            HandleMenuAction(
                _menu.Update(
                    _input,
                    GraphicsDevice.Viewport.Bounds));
        }
        else
        {
            HandleSpectatorInput();
            _camera.Update(
                _input,
                (float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        AdvanceSimulation(gameTime.ElapsedGameTime.TotalSeconds);
        UpdateWindowTitle();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BackgroundColor);

        if (_spriteBatch is null || _pixel is null || _font is null)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        _camera.Fit(viewport);

        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp);

        DrawArena(_spriteBatch, _pixel, viewport);
        DrawDiagnostics(_spriteBatch, _font, viewport);
        _menu.Draw(
            _spriteBatch,
            _pixel,
            _font,
            viewport.Bounds);

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void HandleSpectatorInput()
    {
        if (_input.WasPressed(Keys.Space))
        {
            _isPlaying = !_isPlaying;
            _simulationAccumulator = 0;
        }

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

        if (_input.WasPressed(Keys.R))
        {
            ResetSimulation();
        }
    }

    private void ToggleMenu()
    {
        if (_menu.IsVisible)
        {
            _menu.Close();
            return;
        }

        _isPlaying = false;
        _simulationAccumulator = 0;
        _menu.Open();
    }

    private void HandleMenuAction(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.None:
                return;
            case MenuAction.Play:
                _isPlaying = true;
                _simulationAccumulator = 0;
                _menu.Close();
                return;
            case MenuAction.Pause:
                _isPlaying = false;
                _simulationAccumulator = 0;
                return;
            case MenuAction.Exit:
                RequestExit();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
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
        if (!_isPlaying)
        {
            return;
        }

        if (_simulation.Outcome != BattleOutcome.Ongoing)
        {
            _isPlaying = false;
            _simulationAccumulator = 0;
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
            _simulationAccumulator -= secondsPerTick;
        }

        if (_simulation.Outcome != BattleOutcome.Ongoing)
        {
            _isPlaying = false;
            _simulationAccumulator = 0;
        }
    }

    private void ResetSimulation()
    {
        _simulation = BattleSimulation.Create(_scenario);
        _simulationAccumulator = 0;
    }

    private void DrawArena(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Viewport viewport)
    {
        var topLeft = _camera.WorldToScreen(Vector2.Zero, viewport);
        var bottomRight = _camera.WorldToScreen(
            new Vector2(_scenario.MapWidth, _scenario.MapHeight),
            viewport);
        var mapBounds = RectangleFromPoints(topLeft, bottomRight);

        spriteBatch.Draw(pixel, mapBounds, MapColor);
        DrawBorder(spriteBatch, pixel, mapBounds, MapBorderColor);

        var dotSize = Math.Clamp((int)MathF.Round(_camera.Zoom * 1.8f), 3, 11);
        var halfDotSize = dotSize / 2;

        foreach (var agent in _simulation.Agents)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            var worldPosition = new Vector2(
                agent.XRaw / (float)FixedPoint.Scale,
                agent.YRaw / (float)FixedPoint.Scale);
            var screenPosition = _camera.WorldToScreen(worldPosition, viewport);
            var destination = new Rectangle(
                (int)MathF.Round(screenPosition.X) - halfDotSize,
                (int)MathF.Round(screenPosition.Y) - halfDotSize,
                dotSize,
                dotSize);

            spriteBatch.Draw(pixel, destination, GetFactionColor(agent.FactionId));
        }
    }

    private void DrawDiagnostics(
        SpriteBatch spriteBatch,
        SpriteFont font,
        Viewport viewport)
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

        var state = _isPlaying ? "PLAYING" : "PAUSED";
        var status =
            $"{state}  |  Tick {_simulation.Tick:N0}  |  {_speedMultiplier}x  |  " +
            $"Blue {factionZeroAlive} / Red {factionOneAlive}  |  " +
            $"{_simulation.Outcome}";
        var controls =
            "WASD / arrows: pan    Wheel: zoom    Space: play/pause    " +
            "1 / 2 / 4: speed    R: reset    Esc: menu";

        spriteBatch.DrawString(font, status, new Vector2(18, 16), Color.White);
        spriteBatch.DrawString(
            font,
            controls,
            new Vector2(18, viewport.Height - 34),
            new Color(162, 178, 196),
            0f,
            Vector2.Zero,
            0.72f,
            SpriteEffects.None,
            0f);

        var hoveredAgent = FindHoveredAgent(viewport);
        if (hoveredAgent is { } hovered)
        {
            var target = hovered.TargetEntityId?.ToString() ?? "none";
            var hoverText =
                $"Agent {hovered.EntityId}  |  Faction {hovered.FactionId}  |  " +
                $"HP {hovered.HitPoints}/{hovered.MaximumHitPoints}  |  " +
                $"{hovered.Intent}  |  Target {target}";
            spriteBatch.DrawString(
                font,
                hoverText,
                new Vector2(18, 45),
                GetFactionColor(hovered.FactionId),
                0f,
                Vector2.Zero,
                0.78f,
                SpriteEffects.None,
                0f);
        }
    }

    private AgentView? FindHoveredAgent(Viewport viewport)
    {
        var mouseWorld = _camera.ScreenToWorld(_input.MousePosition, viewport);
        var pickRadius = MathF.Max(5f / _camera.Zoom, 1.5f);
        var pickRadiusSquared = pickRadius * pickRadius;
        AgentView? nearestAgent = null;
        var nearestDistanceSquared = float.MaxValue;

        foreach (var agent in _simulation.Agents)
        {
            if (!agent.IsAlive)
            {
                continue;
            }

            var worldPosition = new Vector2(
                agent.XRaw / (float)FixedPoint.Scale,
                agent.YRaw / (float)FixedPoint.Scale);
            var distanceSquared = Vector2.DistanceSquared(mouseWorld, worldPosition);
            if (distanceSquared > pickRadiusSquared ||
                distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }

            nearestAgent = agent;
            nearestDistanceSquared = distanceSquared;
        }

        return nearestAgent;
    }

    private void UpdateWindowTitle()
    {
        Window.Title =
            $"Autonomous Arena — Seed {_scenario.Seed} — Tick {_simulation.Tick:N0} — " +
            $"{_speedMultiplier}x — {(_isPlaying ? "Playing" : "Paused")} — " +
            _simulation.Outcome;
    }

    private static Rectangle RectangleFromPoints(Vector2 first, Vector2 second)
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
            new Rectangle(bounds.Left, bounds.Bottom - thickness, bounds.Width, thickness),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(bounds.Left, bounds.Top, thickness, bounds.Height),
            color);
        spriteBatch.Draw(
            pixel,
            new Rectangle(bounds.Right - thickness, bounds.Top, thickness, bounds.Height),
            color);
    }

    private static Color GetFactionColor(int factionId) =>
        factionId switch
        {
            0 => FactionOneColor,
            1 => FactionTwoColor,
            _ => OtherFactionColor,
        };
}
