using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hukbo.Client;

internal sealed class SpectatorCamera
{
    private const float MinimumZoom = 0.05f;
    private const float MaximumZoom = 12f;
    private const float PanPixelsPerSecond = 420f;
    private const float WheelZoomFactor = 1.15f;

    private readonly int _mapWidth;
    private readonly int _mapHeight;
    private Vector2 _center;
    private float _zoom = 1f;
    private bool _isFitted;

    public SpectatorCamera(int mapWidth, int mapHeight)
    {
        if (mapWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mapWidth));
        }

        if (mapHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mapHeight));
        }

        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _center = new Vector2(mapWidth / 2f, mapHeight / 2f);
    }

    public float Zoom => _zoom;

    public Vector2 Center => _center;

    /// <summary>
    /// Half the world-space rectangle the spectator can currently see inside
    /// <paramref name="contentBounds"/>.
    /// </summary>
    public Vector2 GetVisibleHalfExtents(Rectangle contentBounds) =>
        new(
            contentBounds.Width / 2f / _zoom,
            contentBounds.Height / 2f / _zoom);

    /// <summary>
    /// Moves the camera without spectator input. Used by auto-pan, which owns
    /// the camera only while the spectator is not driving it.
    /// </summary>
    public void MoveCenterTo(Vector2 center) => _center = center;

    /// <summary>
    /// Returns <see langword="true"/> when spectator pan input moved the camera
    /// this frame, so auto-pan can yield to it.
    /// </summary>
    public bool Update(
        InputEdges input,
        float elapsedSeconds,
        bool allowZoom = true,
        SpectatorPanInput panInput = SpectatorPanInput.All)
    {
        var letters = panInput.HasFlag(SpectatorPanInput.Letters);
        var arrows = panInput.HasFlag(SpectatorPanInput.Arrows);
        var direction = Vector2.Zero;

        if ((letters && input.IsDown(Keys.A)) ||
            (arrows && input.IsDown(Keys.Left)))
        {
            direction.X -= 1f;
        }

        if ((letters && input.IsDown(Keys.D)) ||
            (arrows && input.IsDown(Keys.Right)))
        {
            direction.X += 1f;
        }

        if ((letters && input.IsDown(Keys.W)) ||
            (arrows && input.IsDown(Keys.Up)))
        {
            direction.Y -= 1f;
        }

        if ((letters && input.IsDown(Keys.S)) ||
            (arrows && input.IsDown(Keys.Down)))
        {
            direction.Y += 1f;
        }

        var manualPanApplied = direction != Vector2.Zero;
        if (manualPanApplied)
        {
            direction.Normalize();
            _center += direction * (PanPixelsPerSecond / _zoom) * elapsedSeconds;
        }

        if (allowZoom && input.ScrollWheelDelta != 0)
        {
            var wheelSteps = input.ScrollWheelDelta / 120f;
            _zoom = Math.Clamp(
                _zoom * MathF.Pow(WheelZoomFactor, wheelSteps),
                MinimumZoom,
                MaximumZoom);
        }

        return manualPanApplied;
    }

    public void Fit(Viewport viewport) => Fit(viewport.Bounds);

    public void Fit(Rectangle contentBounds)
    {
        if (_isFitted || contentBounds.Width <= 0 || contentBounds.Height <= 0)
        {
            return;
        }

        var horizontalZoom = contentBounds.Width * 0.88f / _mapWidth;
        var verticalZoom = contentBounds.Height * 0.80f / _mapHeight;
        _zoom = Math.Clamp(
            MathF.Min(horizontalZoom, verticalZoom),
            MinimumZoom,
            MaximumZoom);
        _isFitted = true;
    }

    public Vector2 WorldToScreen(Vector2 worldPosition, Viewport viewport) =>
        WorldToScreen(worldPosition, viewport.Bounds);

    public Vector2 WorldToScreen(
        Vector2 worldPosition,
        Rectangle contentBounds) =>
        ((worldPosition - _center) * _zoom) +
        contentBounds.Center.ToVector2();

    public Vector2 ScreenToWorld(Point screenPosition, Viewport viewport) =>
        ScreenToWorld(screenPosition, viewport.Bounds);

    public Vector2 ScreenToWorld(
        Point screenPosition,
        Rectangle contentBounds) =>
        (screenPosition.ToVector2() -
         contentBounds.Center.ToVector2()) /
        _zoom +
        _center;
}
