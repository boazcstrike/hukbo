using System.Collections.Immutable;
using Hukbo.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Sandata.Client.Rendering;
using Sandata.Client.Theming;
using Sandata.Core.Maps;

namespace Sandata.Client;

/// <summary>
/// Sandata's MonoGame window: the game loop, the spectator camera, and the
/// world renderer for walls, doors, cover, and objectives (plan task 33 of
/// docs/plans/2026-08-07-sandata-scaffold.md).
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
/// <see cref="WorldRenderer"/>'s pure, unit-tested helpers; this class's own
/// job is only to own the <see cref="GraphicsDeviceManager"/>, the
/// <see cref="SpriteBatch"/>, the one shared 1x1 pixel texture, and the calls
/// that turn a <see cref="WorldRenderer.DrawShape"/> into a
/// <c>spriteBatch.Draw</c> call — none of which a unit test can or should
/// exercise (the <c>hukbo-client-ui</c> skill's pure-helper/impure-Draw
/// split).
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

    private readonly DiagnosticLog _log;
    private readonly GraphicsDeviceManager _graphics;
    private readonly ImmutableArray<MapRecord> _mapRecords;
    private readonly SandataCamera _camera;
    private readonly SandataTheme _theme;

    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private int _previousScrollWheelValue;

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

        var grid = FindGrid(_mapRecords);
        var mapWidthWu = grid?.WidthWu ?? DefaultMapWidthWu;
        var mapHeightWu = grid?.HeightWu ?? DefaultMapHeightWu;
        _camera = new SandataCamera(mapWidthWu, mapHeightWu);

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
        _camera.Update(
            Keyboard.GetState(),
            mouseState.ScrollWheelValue,
            _previousScrollWheelValue,
            (float)gameTime.ElapsedGameTime.TotalSeconds);
        _previousScrollWheelValue = mouseState.ScrollWheelValue;

        base.Update(gameTime);
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

        spriteBatch.End();
        base.Draw(gameTime);
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
}
