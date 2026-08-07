using Hukbo.Client.Settings;
using Hukbo.Core.Mathematics;
using Hukbo.Core.Simulation;
using Microsoft.Xna.Framework;

namespace Hukbo.Client;

/// <summary>
/// The mode-dependent half of the auto-pan policy, resolved once from the
/// spectator's <see cref="AutoCameraMode"/> so the controller reads plain
/// numbers rather than branching on the mode at every decision.
/// </summary>
/// <param name="IdleGraceSeconds">
/// How long the screen must hold no fighting at all before a pan may start.
/// </param>
/// <param name="DwellSeconds">
/// How long the camera is held still after a pan settles.
/// </param>
/// <param name="OnScreenFraction">
/// The fraction of the visible rectangle searched for fighting when deciding
/// whether the spectator can already see a fight. One means the whole screen.
/// </param>
internal readonly record struct AutoCameraTuning(
    float IdleGraceSeconds,
    float DwellSeconds,
    float OnScreenFraction);

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

    /// <summary>
    /// How long the screen must be empty of fighting, in
    /// <see cref="AutoCameraMode.Assisted"/>, before a pan may start.
    /// </summary>
    /// <remarks>
    /// This grace is what stops the assistant chasing noise. An agent is only
    /// marked <see cref="AgentIntent.Attacking"/> on ticks where its target is
    /// inside contact distance, and the separation stage pushes bodies apart
    /// between blows, so the count of visible fighters drops to zero for a
    /// frame at a time in a fight that is plainly on screen. Sampling that
    /// value once per frame latched a pan on the flicker.
    /// </remarks>
    internal const float AssistedIdleGraceSeconds = 1.2f;

    /// <summary>
    /// How long the camera is held still after a pan settles, in
    /// <see cref="AutoCameraMode.Assisted"/>. Without it the frame after a
    /// settle could start the next pan, and the camera was never still.
    /// </summary>
    internal const float AssistedDwellSeconds = 2.5f;

    /// <summary>
    /// The <see cref="AutoCameraMode.Follow"/> grace. Short on purpose: this
    /// mode is chosen by a spectator who wants the camera to keep up.
    /// </summary>
    internal const float FollowIdleGraceSeconds = 0.35f;

    /// <summary>
    /// The <see cref="AutoCameraMode.Follow"/> dwell.
    /// </summary>
    internal const float FollowDwellSeconds = 0.75f;

    /// <summary>
    /// How often a pan in flight refreshes its target. Fights move while the
    /// camera travels, so a target resolved once at departure aims the camera
    /// at ground the fight has already left.
    /// </summary>
    internal const float RetargetIntervalSeconds = 0.6f;

    /// <summary>
    /// The hard ceiling on a single pan. Whatever the agents do, the camera
    /// stops travelling after this and takes its dwell.
    /// </summary>
    internal const float MaximumPanSeconds = 6f;

    /// <summary>
    /// Fraction of a visible half-extent a candidate must lie beyond before
    /// travelling to it is worth the motion. A fight nearer than this is
    /// already effectively on screen.
    /// </summary>
    internal const float MinimumTravelFraction = 0.5f;

    /// <summary>
    /// Resolves the spectator's mode into the numbers the controller uses.
    /// <see cref="AutoCameraMode.Off"/> resolves to the assisted tuning; the
    /// controller returns before reading it, so the value is never used.
    /// </summary>
    internal static AutoCameraTuning GetTuning(AutoCameraMode mode) =>
        mode == AutoCameraMode.Follow
            ? new AutoCameraTuning(
                FollowIdleGraceSeconds,
                FollowDwellSeconds,
                SettleFraction)
            : new AutoCameraTuning(
                AssistedIdleGraceSeconds,
                AssistedDwellSeconds,
                1f);

    /// <summary>
    /// True when <paramref name="target"/> is far enough from
    /// <paramref name="center"/> that moving the camera earns its keep.
    /// </summary>
    internal static bool IsWorthTravelling(
        Vector2 center,
        Vector2 target,
        Vector2 halfExtents)
    {
        var offset = target - center;
        return MathF.Abs(offset.X) > halfExtents.X * MinimumTravelFraction ||
            MathF.Abs(offset.Y) > halfExtents.Y * MinimumTravelFraction;
    }

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
