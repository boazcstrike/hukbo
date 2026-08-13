using Hukbo.Client.Settings;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client;

/// <summary>
/// Two-state camera assistant: idle while the spectator can see someone
/// fighting, panning toward the nearest melee while they cannot.
/// </summary>
/// <remarks>
/// Presentation only. It advances on unscaled frame time, reads completed-tick
/// agent views, and never touches simulation advancement or anything that
/// reaches a state hash. It holds no graphics device, so tests drive it
/// directly.
/// <para>
/// The two states are separated by hysteresis rather than by a single frame's
/// answer. A pan needs an empty screen sustained for a grace period to start,
/// a candidate far enough away to be worth the trip, and it always ends in a
/// dwell during which no further pan may begin. Without those three the
/// assistant panned relentlessly, because the count of visibly fighting agents
/// flickers to zero between blows even in the middle of a battle.
/// </para>
/// </remarks>
internal sealed class ArenaAutoPanController
{
    private Vector2 _target;
    private bool _isPanning;
    private float _manualOverrideRemaining;
    private float _secondsWithoutFighting;
    private float _dwellRemaining;
    private float _retargetRemaining;
    private float _panElapsed;

    internal bool IsPanning => _isPanning;

    /// <summary>
    /// Seconds of spectator-input priority still owed. Exposed for tests; the
    /// game never reads it.
    /// </summary>
    internal float ManualOverrideRemaining => _manualOverrideRemaining;

    /// <summary>
    /// Seconds of enforced stillness still owed after the last pan settled.
    /// Exposed for tests; the game never reads it.
    /// </summary>
    internal float DwellRemaining => _dwellRemaining;

    /// <summary>
    /// Forgets any engagement, for example when a new round replaces the
    /// agent list under the camera.
    /// </summary>
    internal void Reset()
    {
        _isPanning = false;
        _manualOverrideRemaining = 0f;
        _secondsWithoutFighting = 0f;
        _dwellRemaining = 0f;
        _retargetRemaining = 0f;
        _panElapsed = 0f;
    }

    /// <summary>
    /// Returns where the camera centre should be after this frame.
    /// </summary>
    /// <param name="agents">Completed-tick agent views.</param>
    /// <param name="center">The camera centre in world units.</param>
    /// <param name="halfExtents">
    /// Half the visible world rectangle, which the camera derives from the
    /// arena bounds and the live zoom.
    /// </param>
    /// <param name="zoom">Live camera zoom, used to bound pan speed.</param>
    /// <param name="mode">The spectator's chosen camera-assistant mode.</param>
    /// <param name="manualPanApplied">
    /// True when the spectator moved the camera themselves this frame.
    /// </param>
    /// <param name="isSuppressed">
    /// True when auto-pan must stay out of the way entirely, for example while
    /// the menu is open or the match summary is up.
    /// </param>
    /// <param name="elapsedSeconds">Unscaled frame time.</param>
    internal Vector2 Update(
        IReadOnlyList<AgentView> agents,
        Vector2 center,
        Vector2 halfExtents,
        float zoom,
        AutoCameraMode mode,
        bool manualPanApplied,
        bool isSuppressed,
        float elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(agents);

        if (mode == AutoCameraMode.Off)
        {
            // Standing down completely, not merely pausing: a spectator who
            // turns the assistant off mid-pan expects the camera to stop where
            // it is, and expects no accumulated grace to fire the moment they
            // turn it back on.
            Reset();
            return center;
        }

        if (manualPanApplied)
        {
            StopPanning();
            _secondsWithoutFighting = 0f;
            _manualOverrideRemaining = ArenaAutoPan.ManualOverrideSeconds;
            return center;
        }

        if (_manualOverrideRemaining > 0f)
        {
            _manualOverrideRemaining =
                MathF.Max(0f, _manualOverrideRemaining - elapsedSeconds);
            return center;
        }

        if (isSuppressed)
        {
            StopPanning();
            _secondsWithoutFighting = 0f;
            return center;
        }

        var tuning = ArenaAutoPan.GetTuning(mode);
        var onScreenExtents = halfExtents * tuning.OnScreenFraction;
        var canSeeFighting = ArenaAutoPan.HasFighterInside(
            agents,
            center,
            onScreenExtents);

        _secondsWithoutFighting = canSeeFighting
            ? 0f
            : _secondsWithoutFighting + elapsedSeconds;
        _dwellRemaining = MathF.Max(0f, _dwellRemaining - elapsedSeconds);

        if (_isPanning)
        {
            return ContinuePan(
                agents,
                center,
                halfExtents,
                zoom,
                tuning,
                elapsedSeconds);
        }

        if (canSeeFighting ||
            _dwellRemaining > 0f ||
            _secondsWithoutFighting < tuning.IdleGraceSeconds)
        {
            return center;
        }

        if (!ArenaAutoPan.TryResolveTarget(agents, center, out _target) ||
            !ArenaAutoPan.IsWorthTravelling(center, _target, halfExtents))
        {
            return center;
        }

        _isPanning = true;
        _panElapsed = 0f;
        _retargetRemaining = ArenaAutoPan.RetargetIntervalSeconds;
        return ArenaAutoPan.AdvanceCenter(
            center,
            _target,
            zoom,
            elapsedSeconds);
    }

    /// <summary>
    /// Keeps travelling until a fighter is near the centre of the screen, not
    /// merely inside it, refreshing the target on a fixed cadence so the
    /// camera converges on a fight that is still moving. Gives up at
    /// <see cref="ArenaAutoPan.MaximumPanSeconds"/> whatever the agents do.
    /// </summary>
    private Vector2 ContinuePan(
        IReadOnlyList<AgentView> agents,
        Vector2 center,
        Vector2 halfExtents,
        float zoom,
        AutoCameraTuning tuning,
        float elapsedSeconds)
    {
        _panElapsed += elapsedSeconds;

        var centeredExtents = halfExtents * ArenaAutoPan.CenteredFraction;
        if (ArenaAutoPan.HasFighterInside(agents, center, centeredExtents) ||
            _panElapsed >= ArenaAutoPan.MaximumPanSeconds)
        {
            Settle(tuning);
            return center;
        }

        _retargetRemaining -= elapsedSeconds;
        if (_retargetRemaining <= 0f || center == _target)
        {
            _retargetRemaining = ArenaAutoPan.RetargetIntervalSeconds;
            if (!ArenaAutoPan.TryResolveTarget(agents, center, out var refreshed))
            {
                Settle(tuning);
                return center;
            }

            _target = refreshed;
        }

        return ArenaAutoPan.AdvanceCenter(
            center,
            _target,
            zoom,
            elapsedSeconds);
    }

    private void Settle(AutoCameraTuning tuning)
    {
        StopPanning();
        _secondsWithoutFighting = 0f;
        _dwellRemaining = tuning.DwellSeconds;
    }

    private void StopPanning()
    {
        _isPanning = false;
        _panElapsed = 0f;
        _retargetRemaining = 0f;
    }
}
