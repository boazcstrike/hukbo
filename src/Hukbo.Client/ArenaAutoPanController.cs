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
/// </remarks>
internal sealed class ArenaAutoPanController
{
    private Vector2 _target;
    private bool _isPanning;
    private float _manualOverrideRemaining;

    internal bool IsPanning => _isPanning;

    /// <summary>
    /// Seconds of spectator-input priority still owed. Exposed for tests; the
    /// game never reads it.
    /// </summary>
    internal float ManualOverrideRemaining => _manualOverrideRemaining;

    /// <summary>
    /// Forgets any engagement, for example when a new round replaces the
    /// agent list under the camera.
    /// </summary>
    internal void Reset()
    {
        _isPanning = false;
        _manualOverrideRemaining = 0f;
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
        bool manualPanApplied,
        bool isSuppressed,
        float elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(agents);

        if (manualPanApplied)
        {
            _isPanning = false;
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
            _isPanning = false;
            return center;
        }

        if (_isPanning)
        {
            return ContinuePan(
                agents,
                center,
                halfExtents,
                zoom,
                elapsedSeconds);
        }

        if (ArenaAutoPan.HasFighterInside(agents, center, halfExtents))
        {
            return center;
        }

        if (!ArenaAutoPan.TryResolveTarget(agents, center, out _target))
        {
            return center;
        }

        _isPanning = true;
        return ArenaAutoPan.AdvanceCenter(
            center,
            _target,
            zoom,
            elapsedSeconds);
    }

    /// <summary>
    /// Keeps travelling until a fighter is comfortably inside the screen, not
    /// merely touching its edge. Re-targets on arrival when the fight has moved
    /// away in the meantime.
    /// </summary>
    private Vector2 ContinuePan(
        IReadOnlyList<AgentView> agents,
        Vector2 center,
        Vector2 halfExtents,
        float zoom,
        float elapsedSeconds)
    {
        var settleExtents = halfExtents * ArenaAutoPan.SettleFraction;
        if (ArenaAutoPan.HasFighterInside(agents, center, settleExtents))
        {
            _isPanning = false;
            return center;
        }

        if (center == _target &&
            !ArenaAutoPan.TryResolveTarget(agents, center, out _target))
        {
            _isPanning = false;
            return center;
        }

        return ArenaAutoPan.AdvanceCenter(
            center,
            _target,
            zoom,
            elapsedSeconds);
    }
}
