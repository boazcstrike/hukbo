using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Sandata.Client.Rendering;

/// <summary>
/// Sandata's spectator camera: pan, zoom, and the world-to-screen and
/// screen-to-world conversions the renderer and the pointer path both need.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reuse note (plan task 33).</b> This type is an <b>adaptation</b> of
/// <c>Hukbo.Client.SpectatorCamera</c> (<c>src/Hukbo.Client/SpectatorCamera.cs</c>),
/// not a reference to it and not a byte-for-byte copy. The pan, zoom-clamp,
/// fit, and world/screen conversion formulas are carried over unchanged —
/// they are exactly right for a second top-down camera and there was no
/// reason to invent a different one. Two things could not be carried over
/// unchanged:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="Hukbo.Client.SpectatorCamera"/> and its members are
/// <c>internal</c> to the <c>Hukbo.Client</c> assembly, and this task's file
/// list forbids editing anything under <c>src/Hukbo.Client/</c> — a project
/// reference would not have exposed the type even if the architecture allowed
/// two independent MonoGame games to share one client assembly, which it does
/// not (see <c>CLAUDE.md</c> section 3: <c>Hukbo.Client</c> is Hukbo's own
/// shell).
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="Hukbo.Client.SpectatorCamera.Update"/> takes Hukbo's own
/// <c>InputEdges</c> type, which is likewise <c>internal</c> to
/// <c>Hukbo.Client</c> and not shared infrastructure. <see cref="Update"/>
/// here is rewritten directly against MonoGame's own
/// <see cref="KeyboardState"/> and raw scroll-wheel values, which
/// <c>Sandata.Client</c> can reach without any dependency on Hukbo's client
/// assembly.
/// </description>
/// </item>
/// </list>
/// <para>
/// Map dimensions are named in Sandata's own world-unit vocabulary
/// (<c>mapWidthWu</c>/<c>mapHeightWu</c>, design section 12's <c>GRID</c>
/// record) rather than Hukbo's unlabelled <c>float</c> world size, but they
/// are stored and used identically: a <see cref="Vector2"/> center and a
/// scalar zoom, both plain presentation state with no tie to any simulation
/// tick.
/// </para>
/// </remarks>
internal sealed class SandataCamera
{
    private const float MinimumZoom = 0.05f;
    private const float MaximumZoom = 12f;
    private const float PanWorldUnitsPerSecond = 420f;
    private const float WheelZoomFactor = 1.15f;

    private readonly int _mapWidthWu;
    private readonly int _mapHeightWu;
    private Vector2 _center;
    private float _zoom = 1f;
    private bool _isFitted;

    public SandataCamera(int mapWidthWu, int mapHeightWu)
    {
        if (mapWidthWu <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mapWidthWu));
        }

        if (mapHeightWu <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mapHeightWu));
        }

        _mapWidthWu = mapWidthWu;
        _mapHeightWu = mapHeightWu;
        _center = new Vector2(mapWidthWu / 2f, mapHeightWu / 2f);
    }

    /// <summary>Screen pixels per world unit. Always within [<see cref="MinimumZoom"/>, <see cref="MaximumZoom"/>].</summary>
    public float Zoom => _zoom;

    /// <summary>The world-unit point the viewport is currently centered on.</summary>
    public Vector2 Center => _center;

    /// <summary>
    /// Sets zoom directly, clamped to the same range <see cref="Update"/>
    /// enforces. Used by tests that need a fixed, repeatable zoom instead of
    /// driving it through simulated wheel input, and by any future scripted
    /// camera station in the same spirit as
    /// <c>Hukbo.Client.SpectatorCamera.SetZoom</c>.
    /// </summary>
    public void SetZoom(float zoom) => _zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);

    /// <summary>Moves the camera without spectator input driving it.</summary>
    public void MoveCenterTo(Vector2 center) => _center = center;

    /// <summary>
    /// Applies one frame of WASD/arrow-key pan and mouse-wheel zoom.
    /// Presentation-only interactive input handling; not unit tested, the
    /// same way <c>Hukbo.Client.SpectatorCamera.Update</c> is not — the pure,
    /// tested surface is <see cref="WorldToScreen(Vector2, Rectangle)"/>,
    /// <see cref="ScreenToWorld(Point, Rectangle)"/>, and <see cref="Fit(Rectangle)"/>.
    /// </summary>
    /// <param name="keyboardState">This frame's keyboard state.</param>
    /// <param name="scrollWheelValue">
    /// This frame's cumulative <see cref="Microsoft.Xna.Framework.Input.MouseState.ScrollWheelValue"/>.
    /// </param>
    /// <param name="previousScrollWheelValue">
    /// The same value one frame earlier; the caller owns this state because
    /// <see cref="SandataCamera"/> has no per-frame lifecycle hook of its own.
    /// </param>
    /// <param name="elapsedSeconds">Unscaled wall-clock seconds since the last frame.</param>
    public void Update(
        KeyboardState keyboardState,
        int scrollWheelValue,
        int previousScrollWheelValue,
        float elapsedSeconds)
    {
        var direction = Vector2.Zero;

        if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left))
        {
            direction.X -= 1f;
        }

        if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right))
        {
            direction.X += 1f;
        }

        if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up))
        {
            direction.Y -= 1f;
        }

        if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down))
        {
            direction.Y += 1f;
        }

        if (direction != Vector2.Zero)
        {
            direction.Normalize();
            _center += direction * (PanWorldUnitsPerSecond / _zoom) * elapsedSeconds;
        }

        var wheelDelta = scrollWheelValue - previousScrollWheelValue;
        if (wheelDelta != 0)
        {
            var wheelSteps = wheelDelta / 120f;
            _zoom = Math.Clamp(
                _zoom * MathF.Pow(WheelZoomFactor, wheelSteps),
                MinimumZoom,
                MaximumZoom);
        }
    }

    /// <summary>
    /// Sets zoom, once, so the whole map's <c>0.88</c>/<c>0.80</c>-fraction
    /// bounding box fits inside <paramref name="contentBounds"/>. A no-op on
    /// every call after the first, or while <paramref name="contentBounds"/>
    /// has no area (the window has not been sized yet).
    /// </summary>
    public void Fit(Rectangle contentBounds)
    {
        if (_isFitted || contentBounds.Width <= 0 || contentBounds.Height <= 0)
        {
            return;
        }

        var horizontalZoom = contentBounds.Width * 0.88f / _mapWidthWu;
        var verticalZoom = contentBounds.Height * 0.80f / _mapHeightWu;
        _zoom = Math.Clamp(
            MathF.Min(horizontalZoom, verticalZoom),
            MinimumZoom,
            MaximumZoom);
        _isFitted = true;
    }

    /// <summary>Converts a world-unit position to a screen pixel position.</summary>
    public Vector2 WorldToScreen(Vector2 worldPositionWu, Rectangle contentBounds) =>
        ((worldPositionWu - _center) * _zoom) + contentBounds.Center.ToVector2();

    /// <summary>Converts a screen pixel position back to a world-unit position.</summary>
    public Vector2 ScreenToWorld(Point screenPosition, Rectangle contentBounds) =>
        ((screenPosition.ToVector2() - contentBounds.Center.ToVector2()) / _zoom) + _center;
}
