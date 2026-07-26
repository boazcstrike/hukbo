using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AutonomousArena.Client;

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

    public void Update(InputEdges input, float elapsedSeconds)
    {
        var direction = Vector2.Zero;

        if (input.IsDown(Keys.A) || input.IsDown(Keys.Left))
        {
            direction.X -= 1f;
        }

        if (input.IsDown(Keys.D) || input.IsDown(Keys.Right))
        {
            direction.X += 1f;
        }

        if (input.IsDown(Keys.W) || input.IsDown(Keys.Up))
        {
            direction.Y -= 1f;
        }

        if (input.IsDown(Keys.S) || input.IsDown(Keys.Down))
        {
            direction.Y += 1f;
        }

        if (direction != Vector2.Zero)
        {
            direction.Normalize();
            _center += direction * (PanPixelsPerSecond / _zoom) * elapsedSeconds;
        }

        if (input.ScrollWheelDelta != 0)
        {
            var wheelSteps = input.ScrollWheelDelta / 120f;
            _zoom = Math.Clamp(
                _zoom * MathF.Pow(WheelZoomFactor, wheelSteps),
                MinimumZoom,
                MaximumZoom);
        }
    }

    public void Fit(Viewport viewport)
    {
        if (_isFitted || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return;
        }

        var horizontalZoom = viewport.Width * 0.88f / _mapWidth;
        var verticalZoom = viewport.Height * 0.80f / _mapHeight;
        _zoom = Math.Clamp(
            MathF.Min(horizontalZoom, verticalZoom),
            MinimumZoom,
            MaximumZoom);
        _isFitted = true;
    }

    public Vector2 WorldToScreen(Vector2 worldPosition, Viewport viewport) =>
        ((worldPosition - _center) * _zoom) +
        new Vector2(viewport.Width / 2f, viewport.Height / 2f);

    public Vector2 ScreenToWorld(Point screenPosition, Viewport viewport) =>
        (screenPosition.ToVector2() -
         new Vector2(viewport.Width / 2f, viewport.Height / 2f)) /
        _zoom +
        _center;
}
