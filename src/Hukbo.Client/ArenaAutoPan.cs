using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client;

/// <summary>
/// Decides where the spectator camera should drift when the fighting has left
/// the screen. Every method here is pure: it takes plain values and returns
/// plain values, so the whole policy is unit tested without a graphics device.
/// </summary>
internal static class ArenaAutoPan
{
    /// <summary>
    /// World-unit radius around the anchor that counts as one melee. Wider than
    /// any single agent's reach so a scrap stays one target, narrow enough that
    /// a separate fight across the field is never averaged into it.
    /// </summary>
    internal const float ClusterRadius = 14f;

    /// <summary>
    /// Fraction of the visible rectangle that must contain a fighter before
    /// auto-pan lets go. Below one, so the camera does not stop with the fight
    /// pinned to the screen edge and immediately re-engage.
    /// </summary>
    internal const float SettleFraction = 0.7f;

    /// <summary>
    /// Screen pixels per second, converted to world units by the caller using
    /// the live zoom, exactly like manual panning.
    /// </summary>
    internal const float MaximumScreenSpeed = 900f;

    /// <summary>
    /// Multiplier on the remaining distance, giving an ease-out arrival instead
    /// of a dead stop.
    /// </summary>
    internal const float Responsiveness = 2.5f;

    /// <summary>
    /// How long spectator pan input keeps auto-pan out of the way.
    /// </summary>
    internal const float ManualOverrideSeconds = 2.5f;

    internal static bool IsFighting(in AgentView agent) =>
        agent.IsAlive && agent.Intent == AgentIntent.Attacking;

    internal static Vector2 GetWorldPosition(in AgentView agent) =>
        new(
            agent.XRaw / (float)FixedPoint.Scale,
            agent.YRaw / (float)FixedPoint.Scale);

    /// <summary>
    /// True when at least one fighting agent sits inside the axis-aligned world
    /// rectangle centred on <paramref name="center"/>.
    /// </summary>
    internal static bool HasFighterInside(
        IReadOnlyList<AgentView> agents,
        Vector2 center,
        Vector2 halfExtents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            if (!IsFighting(agent))
            {
                continue;
            }

            var offset = GetWorldPosition(agent) - center;
            if (MathF.Abs(offset.X) <= halfExtents.X &&
                MathF.Abs(offset.Y) <= halfExtents.Y)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Picks the melee nearest <paramref name="center"/> and returns its
    /// centroid. Returns <see langword="false"/> when nobody is fighting.
    /// </summary>
    /// <remarks>
    /// The anchor is the nearest fighting agent, with ties broken on the lower
    /// entity id so the result never depends on iteration luck. Averaging every
    /// fighter on the map instead would aim the camera at empty ground whenever
    /// two separate fights sit at opposite ends of it.
    /// </remarks>
    internal static bool TryResolveTarget(
        IReadOnlyList<AgentView> agents,
        Vector2 center,
        out Vector2 target)
    {
        ArgumentNullException.ThrowIfNull(agents);

        var anchor = Vector2.Zero;
        var anchorEntityId = 0UL;
        var anchorDistanceSquared = float.MaxValue;
        var hasAnchor = false;

        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            if (!IsFighting(agent))
            {
                continue;
            }

            var position = GetWorldPosition(agent);
            var distanceSquared = (position - center).LengthSquared();
            var isCloser = distanceSquared < anchorDistanceSquared;
            var isTieOnLowerId =
                distanceSquared == anchorDistanceSquared &&
                agent.EntityId < anchorEntityId;

            if (!hasAnchor || isCloser || isTieOnLowerId)
            {
                anchor = position;
                anchorEntityId = agent.EntityId;
                anchorDistanceSquared = distanceSquared;
                hasAnchor = true;
            }
        }

        if (!hasAnchor)
        {
            target = center;
            return false;
        }

        var sum = Vector2.Zero;
        var count = 0;
        var clusterRadiusSquared = ClusterRadius * ClusterRadius;

        for (var index = 0; index < agents.Count; index++)
        {
            var agent = agents[index];
            if (!IsFighting(agent))
            {
                continue;
            }

            var position = GetWorldPosition(agent);
            if ((position - anchor).LengthSquared() > clusterRadiusSquared)
            {
                continue;
            }

            sum += position;
            count++;
        }

        target = sum / count;
        return true;
    }

    /// <summary>
    /// Moves <paramref name="center"/> toward <paramref name="target"/> without
    /// overshooting it.
    /// </summary>
    internal static Vector2 AdvanceCenter(
        Vector2 center,
        Vector2 target,
        float zoom,
        float elapsedSeconds)
    {
        var offset = target - center;
        var distance = offset.Length();
        if (distance <= float.Epsilon || elapsedSeconds <= 0f || zoom <= 0f)
        {
            return target;
        }

        var maximumWorldSpeed = MaximumScreenSpeed / zoom;
        var speed = MathF.Min(maximumWorldSpeed, distance * Responsiveness);
        var step = speed * elapsedSeconds;

        return step >= distance
            ? target
            : center + (offset / distance * step);
    }
}
